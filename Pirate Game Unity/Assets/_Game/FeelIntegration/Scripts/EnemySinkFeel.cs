using System.Collections.Generic;
using RoguePirates.V4;
using RoguePirates.V4.Events;
using RoguePirates.V4.Presentation;
using UnityEngine;

namespace RoguePirates.FeelIntegration
{
    /// <summary>
    /// The game removes an enemy's model the moment it dies. This leaves a short-lived wreck in its place: a visual-only
    /// copy of the ship's meshes that first BURNS for a moment (afloat, rocking, listing, charring darker, with flames
    /// and smoke on deck) and then heels over and SINKS while the flames die out. Kills read as a ship going down
    /// instead of vanishing. Listens to the run's combat events (Killed); no game code changed.
    /// Everything is built in this script at runtime (the wreck copies, and the deck fire as particle systems created in
    /// code); it only reuses two existing materials. Mobile friendly: plain MeshRenderers (no scripts, colliders,
    /// shadows or lights), pooled per enemy type, a few flame particles per wreck, a cap on how many burn at once, and
    /// no allocations per frame after warm-up.
    /// </summary>
    [AddComponentMenu("Rogue Pirates/Feel/Enemy Sink Feel")]
    public sealed class EnemySinkFeel : MonoBehaviour
    {
        [Header("Burning (before it sinks)")]
        [Tooltip("How long the wreck burns afloat before going down (seconds).")]
        public float BurnDuration = 1.0f;
        [Tooltip("How far it lists to one side while burning (degrees).")]
        public float BurnList = 9f;
        [Tooltip("Gentle rocking while burning (degrees).")]
        public float BurnRock = 3f;
        [Tooltip("How dark the hull chars (0 = no change, 1 = black).")]
        [Range(0f, 1f)] public float Char = 0.65f;

        [Header("Sinking")]
        [Tooltip("How long a wreck takes to go under after burning (seconds).")]
        public float SinkDuration = 1.3f;
        [Tooltip("How deep it sinks (m, below where it was).")]
        public float SinkDepth = 2.2f;
        [Tooltip("How far it heels over as it sinks (degrees, on top of the list).")]
        public float HeelAngle = 30f;
        [Tooltip("Nose/stern dip as it goes down (degrees).")]
        public float PitchAngle = 14f;
        [Tooltip("A brief jolt upward the moment it's hit (m).")]
        public float Jolt = 0.25f;
        [Tooltip("Most wrecks at once (more kills just skip the wreck).")]
        public int MaxSinking = 8;

        [Header("Deck fire (built in code from these existing materials)")]
        [Tooltip("Flame material (e.g. cfxr fire circle drawn).")]
        public Material FlameMaterial;
        [Tooltip("Smoke material (e.g. cfxr smoke cloud x4).")]
        public Material SmokeMaterial;
        [Tooltip("Flames per second while burning.")]
        public float FlameRate = 12f;
        [Tooltip("Smoke puffs per second while burning.")]
        public float SmokeRate = 4f;

        [Header("Which kills")]
        public bool Normal = true;
        public bool Elites = true;
        public bool Bosses = false;
        [Tooltip("How far from the kill point to look for the enemy's model (m).")]
        public float FindRadius = 3f;
        [Tooltip("Name prefix of the game's pooled enemy models.")]
        public string EnemyViewPrefix = "enemy/";

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        class Part { public Mesh Mesh; public Matrix4x4 LocalToRoot; }

        class Template
        {
            public readonly List<Part> Parts = new List<Part>();
            public readonly Stack<Wreck> Free = new Stack<Wreck>();
        }

        class Wreck
        {
            public GameObject Root;
            public MeshRenderer[] Renderers;
            public Color[][] BaseColors;   // per renderer, per material slot
            public int[][] ColorProps;     // which colour property each slot uses (0 = none)
            public ParticleSystem Flames, Smoke;
            public float Radius;
        }

        struct Sinking
        {
            public Wreck W;
            public Template From;
            public Vector3 Start;
            public Quaternion StartRotation;
            public Vector3 HeelAxis, PitchAxis;
            public float Age;
            public bool FireStopped;
        }

        struct Seen { public Transform View; public Vector3 Position; public Quaternion Rotation; public Vector3 Scale; }

        readonly Dictionary<string, Template> _templates = new Dictionary<string, Template>();
        readonly List<Sinking> _sinking = new List<Sinking>();
        readonly List<MeshFilter> _filters = new List<MeshFilter>();
        readonly List<Seen> _seen = new List<Seen>(64);
        MaterialPropertyBlock _block;
        V4Game _game;
        CombatEvents _events;
        V4PresentationRoot _presentation;
        Transform _poolRoot, _pools;
        float _nextSearch;

        void Awake() => V4Game.RunStarted += OnRunStarted;

        void OnDestroy()
        {
            V4Game.RunStarted -= OnRunStarted;
            Unbind();
        }

        void OnDisable()
        {
            Unbind();
            for (var i = 0; i < _sinking.Count; i++) Return(_sinking[i]);
            _sinking.Clear();
        }

        void OnRunStarted(V4Game game) => _game = game;

        void Unbind()
        {
            if (_events != null) _events.TickPublished -= OnEvents;
            _events = null;
        }

        void LateUpdate()
        {
            if (_game == null && Time.unscaledTime >= _nextSearch)
            {
                _nextSearch = Time.unscaledTime + 0.5f;
                _game = FindFirstObjectByType<V4Game>();
            }
            var events = _game != null ? _game.Events : null;
            if (events != _events)
            {
                Unbind();
                _events = events;
                if (_events != null) _events.TickPublished += OnEvents;
            }

            var dt = Time.deltaTime;
            var total = BurnDuration + SinkDuration;
            for (var i = _sinking.Count - 1; i >= 0; i--)
            {
                var s = _sinking[i];
                if (s.W == null || s.W.Root == null) { _sinking.RemoveAt(i); continue; }
                s.Age += dt;
                var t = s.W.Root.transform;

                // Jolt from the blast at the very start.
                var jolt = s.Age < 0.18f ? Jolt * Mathf.Sin(s.Age / 0.18f * Mathf.PI) : 0f;

                float y, heel, pitch, charT;
                if (s.Age < BurnDuration)
                {
                    // Burning afloat: a slow list to one side, gentle rocking, a little bob, charring.
                    var b = s.Age / Mathf.Max(0.05f, BurnDuration);
                    heel = BurnList * (1f - (1f - b) * (1f - b)) + Mathf.Sin(s.Age * 5f) * BurnRock * (1f - b * 0.5f);
                    pitch = Mathf.Sin(s.Age * 3.7f + 1f) * BurnRock * 0.6f;
                    y = Mathf.Sin(s.Age * 4f) * 0.05f - b * 0.15f;
                    charT = b * 0.7f;
                }
                else
                {
                    // Then it goes down: heels over, dips, sinks; the fire dies.
                    if (!s.FireStopped)
                    {
                        if (s.W.Flames != null) s.W.Flames.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                        if (s.W.Smoke != null) s.W.Smoke.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                        s.FireStopped = true;
                    }
                    var n = Mathf.Clamp01((s.Age - BurnDuration) / Mathf.Max(0.1f, SinkDuration));
                    heel = BurnList + HeelAngle * (1f - (1f - n) * (1f - n));
                    pitch = PitchAngle * n;
                    y = -0.15f - SinkDepth * n * n;
                    charT = 0.7f + 0.3f * n;
                }
                t.position = s.Start + Vector3.up * (jolt + y);
                t.rotation = Quaternion.AngleAxis(heel, s.HeelAxis) * Quaternion.AngleAxis(pitch, s.PitchAxis) * s.StartRotation;
                ApplyChar(s.W, Char * charT);

                if (s.Age >= total) { Return(s); _sinking.RemoveAt(i); continue; }
                _sinking[i] = s;
            }
            Snapshot();
        }

        // The game releases a dead enemy's model before it publishes the kill, so the last pose of every live enemy
        // model is kept each frame; a kill then picks the one that just disappeared nearest to it.
        void Snapshot()
        {
            _seen.Clear();
            if (_pools == null) { var go = GameObject.Find("Pools"); if (go != null) _pools = go.transform; }
            if (_pools == null) return;
            for (var i = 0; i < _pools.childCount; i++)
            {
                var c = _pools.GetChild(i);
                if (!c.gameObject.activeSelf || !c.name.StartsWith(EnemyViewPrefix)) continue;
                _seen.Add(new Seen { View = c, Position = c.position, Rotation = c.rotation, Scale = c.lossyScale });
            }
        }

        bool TryFindJustReleased(Vector3 position, out Seen found)
        {
            found = default;
            var best = FindRadius * FindRadius;
            var ok = false;
            for (var i = 0; i < _seen.Count; i++)
            {
                var v = _seen[i];
                if (v.View == null || v.View.gameObject.activeSelf) continue; // still alive (or already reused)
                var d = v.Position - position; d.y = 0f;
                var sqr = d.sqrMagnitude;
                if (sqr <= best) { best = sqr; found = v; ok = true; }
            }
            return ok;
        }

        void OnEvents(IReadOnlyList<CombatEvent> batch)
        {
            if (batch == null) return;
            for (var i = 0; i < batch.Count; i++)
            {
                var e = batch[i];
                if (e.Kind != CombatEventKind.Killed) continue;
                var boss = (e.B & 2) != 0;
                var elite = (e.B & 1) != 0 && (e.B & 4) == 0;
                if (boss ? !Bosses : elite ? !Elites : !Normal) continue;
                if (_sinking.Count >= MaxSinking) continue;
                var at = new Vector3(e.X, 0f, e.Y);
                Seen seen;
                if (TryFindJustReleased(at, out seen)) StartWreck(seen.View, seen.Position, seen.Rotation, seen.Scale);
                else
                {
                    var view = NearestEnemyView(at);
                    if (view != null) StartWreck(view, view.position, view.rotation, view.lossyScale);
                }
            }
        }

        Transform NearestEnemyView(Vector3 position)
        {
            if (_presentation == null) _presentation = FindFirstObjectByType<V4PresentationRoot>();
            var views = _presentation != null ? _presentation.EnemyViews : null;
            return views != null ? views.NearestView(position, FindRadius) : null;
        }

        void StartWreck(Transform view, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var template = TemplateFor(view);
            if (template == null || template.Parts.Count == 0) return;
            var w = template.Free.Count > 0 ? template.Free.Pop() : Build(template, view.name);
            if (w == null) return;
            CopyCurrentMaterials(view, w);
            var t = w.Root.transform;
            t.SetPositionAndRotation(position, rotation);
            t.localScale = scale;
            w.Root.SetActive(true);
            ApplyChar(w, 0f);
            if (w.Flames != null) { w.Flames.Clear(); w.Flames.Play(); }
            if (w.Smoke != null) { w.Smoke.Clear(); w.Smoke.Play(); }

            // List/heel to a random side around the ship's own length, and dip its nose or stern.
            var fwd = rotation * Vector3.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            var side = Vector3.Cross(Vector3.up, fwd);
            _sinking.Add(new Sinking
            {
                W = w, From = template, Start = position, StartRotation = rotation,
                HeelAxis = fwd * (Random.value < 0.5f ? -1f : 1f),
                PitchAxis = side * (Random.value < 0.5f ? -1f : 1f),
            });
        }

        // Enemy materials may be per-view tinted copies the game destroys later, so take them from the dying ship now,
        // and remember each slot's own colour so the char darkens from it.
        void CopyCurrentMaterials(Transform view, Wreck w)
        {
            _filters.Clear();
            view.GetComponentsInChildren(true, _filters);
            var part = 0;
            foreach (var f in _filters)
            {
                if (f == null || f.sharedMesh == null) continue;
                var r = f.GetComponent<MeshRenderer>();
                if (r == null || !r.enabled || !ActiveUnder(f.transform, view) || IsSkippedPart(f.transform, view)) continue;
                if (part >= w.Renderers.Length) break;
                var mats = r.sharedMaterials;
                w.Renderers[part].sharedMaterials = mats;
                if (w.BaseColors[part] == null || w.BaseColors[part].Length != mats.Length)
                {
                    w.BaseColors[part] = new Color[mats.Length];
                    w.ColorProps[part] = new int[mats.Length];
                }
                for (var m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    var prop = mat != null && mat.HasProperty(BaseColorId) ? BaseColorId : mat != null && mat.HasProperty(ColorId) ? ColorId : 0;
                    w.ColorProps[part][m] = prop;
                    w.BaseColors[part][m] = prop != 0 ? mat.GetColor(prop) : Color.white;
                }
                part++;
            }
        }

        void ApplyChar(Wreck w, float amount)
        {
            if (_block == null) _block = new MaterialPropertyBlock();
            for (var r = 0; r < w.Renderers.Length; r++)
            {
                var rend = w.Renderers[r];
                var cols = w.BaseColors[r];
                if (rend == null || cols == null) continue;
                for (var m = 0; m < cols.Length; m++)
                {
                    var prop = w.ColorProps[r][m];
                    if (prop == 0) continue;
                    var c = cols[m];
                    var charred = new Color(c.r * 0.16f + 0.05f, c.g * 0.12f + 0.03f, c.b * 0.1f + 0.02f, c.a);
                    rend.GetPropertyBlock(_block, m);
                    _block.SetColor(prop, Color.Lerp(c, charred, amount));
                    rend.SetPropertyBlock(_block, m);
                }
            }
        }

        Template TemplateFor(Transform view)
        {
            Template template;
            if (_templates.TryGetValue(view.name, out template)) return template;
            template = new Template();
            _filters.Clear();
            view.GetComponentsInChildren(true, _filters);
            var toRoot = view.worldToLocalMatrix;
            foreach (var f in _filters)
            {
                if (f == null || f.sharedMesh == null) continue;
                var r = f.GetComponent<MeshRenderer>();
                if (r == null || !r.enabled || !ActiveUnder(f.transform, view)) continue;
                if (IsSkippedPart(f.transform, view)) continue;
                template.Parts.Add(new Part { Mesh = f.sharedMesh, LocalToRoot = toRoot * f.transform.localToWorldMatrix });
            }
            _templates[view.name] = template;
            return template;
        }

        // Active relative to the model's root (the root itself is switched off once the game releases it).
        static bool ActiveUnder(Transform t, Transform root)
        {
            for (; t != null && t != root; t = t.parent) if (!t.gameObject.activeSelf) return false;
            return true;
        }

        // Health bars, wakes, foam and effects are not part of the ship.
        static bool IsSkippedPart(Transform t, Transform root)
        {
            for (; t != null && t != root; t = t.parent)
            {
                var n = t.name;
                if (n.Contains("Health") || n.Contains("Bar") || n.Contains("Wake") || n.Contains("Foam") || n.Contains("Shadow") || n.Contains("VFX") || n.Contains("Fx")) return true;
            }
            return false;
        }

        Wreck Build(Template template, string name)
        {
            if (_poolRoot == null) _poolRoot = new GameObject("EnemyWrecks").transform;
            var root = new GameObject("Wreck " + name);
            root.transform.SetParent(_poolRoot, false);
            var w = new Wreck { Root = root, Renderers = new MeshRenderer[template.Parts.Count] };
            w.BaseColors = new Color[template.Parts.Count][];
            w.ColorProps = new int[template.Parts.Count][];
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            for (var i = 0; i < template.Parts.Count; i++)
            {
                var p = template.Parts[i];
                var go = new GameObject("part");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = p.LocalToRoot.GetColumn(3);
                go.transform.localRotation = p.LocalToRoot.rotation;
                go.transform.localScale = p.LocalToRoot.lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh = p.Mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                w.Renderers[i] = r;
                var b = p.Mesh.bounds;
                bounds.Encapsulate(p.LocalToRoot.MultiplyPoint3x4(b.center));
            }
            w.Radius = Mathf.Max(0.5f, bounds.extents.magnitude);
            BuildFire(w);
            root.SetActive(false);
            return w;
        }

        // The deck fire, made in code: a few flames licking up and a thin smoke trail, both following the wreck.
        void BuildFire(Wreck w)
        {
            if (FlameMaterial != null) w.Flames = MakeFire(w.Root.transform, "Flames", FlameMaterial, true, w.Radius);
            if (SmokeMaterial != null) w.Smoke = MakeFire(w.Root.transform, "Smoke", SmokeMaterial, false, w.Radius);
        }

        ParticleSystem MakeFire(Transform parent, string name, Material material, bool flames, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * 0.4f;
            go.SetActive(false); // configure before it starts
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // flames and smoke trail behind as it moves
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var em = ps.emission;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Clamp(radius * 0.35f, 0.3f, 1.6f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var g = new Gradient();
            if (flames)
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.2f);
                main.gravityModifier = -0.4f;
                main.maxParticles = 10;
                em.rateOverTime = FlameRate;
                g.SetKeys(
                    new[] { new GradientColorKey(new Color(1f, 0.92f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.08f), 0.35f), new GradientColorKey(new Color(0.9f, 0.2f, 0.05f), 0.75f), new GradientColorKey(new Color(0.5f, 0.06f, 0.08f), 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.85f, 0.7f), new GradientAlphaKey(0f, 1f) });
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.25f)));
            }
            else
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.3f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
                main.gravityModifier = -0.15f;
                main.maxParticles = 6;
                em.rateOverTime = SmokeRate;
                g.SetKeys(
                    new[] { new GradientColorKey(new Color(1f, 0.5f, 0.2f), 0f), new GradientColorKey(new Color(0.5f, 0.36f, 0.3f), 0.2f), new GradientColorKey(new Color(0.32f, 0.28f, 0.34f), 0.6f), new GradientColorKey(new Color(0.25f, 0.23f, 0.27f), 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.7f, 0.12f), new GradientAlphaKey(0.45f, 0.65f), new GradientAlphaKey(0f, 1f) });
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(1f, 1.6f)));
                var ts = ps.textureSheetAnimation; // the cfxr smoke sheet is 2x2
                ts.enabled = true; ts.numTilesX = 2; ts.numTilesY = 2;
                ts.animation = ParticleSystemAnimationType.WholeSheet;
                ts.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
                ts.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.99f);
            }
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = flames ? 6 : 2;
            go.SetActive(true);
            return ps;
        }

        void Return(Sinking s)
        {
            if (s.W == null || s.W.Root == null) return;
            if (s.W.Flames != null) s.W.Flames.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (s.W.Smoke != null) s.W.Smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ApplyChar(s.W, 0f);
            s.W.Root.SetActive(false);
            if (s.From != null) s.From.Free.Push(s.W);
        }
    }
}
