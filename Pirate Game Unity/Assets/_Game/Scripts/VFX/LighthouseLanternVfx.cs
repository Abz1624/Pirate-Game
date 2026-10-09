using System.Collections.Generic;
using HighSeas.Combat;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace HighSeas.VFX
{
    /// <summary>
    /// Lighthouse Lantern. A lantern on the stern throws a torch-like beam across the water: narrow at
    /// the lantern, widening out to the full range. It turns steadily around the ship and burns every
    /// enemy it crosses, each at most once per <see cref="BurnInterval"/>.
    ///
    /// No blind spots:
    ///  - The beam lies on the water and reaches the full radius, so what you see is exactly what it hits.
    ///  - A glowing halo under the lantern burns anything hugging the lantern, where the beam is narrowest.
    ///  - Swept test: if the beam passes over a target between two frames (low FPS, fast enemy), it still counts.
    ///  - Full-turn guarantee: a target that somehow stays inside the radius unlit for a whole revolution
    ///    (e.g. circling in step with the beam) is burned anyway.
    /// Beam, halo, trail and sweep are procedural meshes drawn per camera (nothing is serialized into
    /// the prefab). Damage: optional direct <see cref="IDamageable"/> hit, plus <see cref="onBurn"/>.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class LighthouseLanternVfx : MonoBehaviour
    {
        [System.Serializable] public sealed class TransformEvent : UnityEvent<Transform> { }

        [Header("Rig (filled by the builder)")]
        [Tooltip("Lantern housing - yaws with the beam. Its axis is where the beam starts.")]
        [SerializeField] Transform lanternHead;
        [Tooltip("Centre of the glass.")]
        [SerializeField] Transform lightSource;
        [SerializeField] Renderer glassRenderer;
        [SerializeField] Material beamMaterial;
        [SerializeField] Material trailMaterial;
        [SerializeField] Material sweepMaterial;
        [SerializeField] ParticleSystem burnPuff;
        [SerializeField] ParticleSystem burnRing;

        [Header("Beam")]
        [SerializeField, Min(1f)] float range = 9f;
        [Tooltip("Spread of the beam (degrees). The beam is this wide at the far end.")]
        [SerializeField, Range(5f, 90f)] float beamAngle = 30f;
        [Tooltip("Degrees per second, clockwise seen from above. Independent of the ship's heading.")]
        [SerializeField, Min(0f)] float rotationSpeed = 100f;
        [Tooltip("Height of the beam above the ship's waterline.")]
        [SerializeField] float beamHeight = 0.1f;
        [Tooltip("Half width of the beam at the lantern - it starts this narrow and widens outward.")]
        [SerializeField, Min(0f)] float originHalfWidth = 0.12f;
        [Tooltip("Width of the bright core as a fraction of the beam.")]
        [SerializeField, Range(0.05f, 1f)] float coreWidth = 0.35f;
        [SerializeField, Range(0f, 1f)] float beamAlpha = 1f;
        [Tooltip("Brightness at the far edge relative to the lantern. Keep it high enough that the beam visibly reaches the radius.")]
        [SerializeField, Range(0f, 1f)] float endBrightness = 0.8f;
        [SerializeField, Range(4, 64)] int beamSegments = 32;
        [Header("Volume cone")]
        [Tooltip("Fake-volume cone mesh (e.g. 'V Cone Lit Party'). Its own renderer is switched off; the lantern draws it every frame from the lens down to the water, turning with the beam.")]
        [SerializeField] MeshRenderer volumeCone;
        [Tooltip("Optional real spot light aimed down the cone.")]
        [SerializeField] Light volumeLight;
        [SerializeField] Color volumeColor = new(1f, 0.525f, 0f, 0.039f);
        [SerializeField, Min(0f)] float volumeOpacity = 1.27f;
        [SerializeField, Min(0f)] float volumeLightIntensity = 4f;

        [Header("Lantern halo")]
        [Tooltip("Glow pool under the lantern. Anything inside it burns - covers the spot where the beam is narrowest.")]
        [SerializeField, Min(0f)] float nearRadius = 1.4f;
        [SerializeField, Range(0f, 1f)] float haloAlpha = 0.4f;

        [Header("Radius trail")]
        [Tooltip("How far the glow line at the beam's reach trails behind it (degrees). < 360 so it never closes.")]
        [SerializeField, Range(0f, 330f)] float trailDegrees = 210f;
        [SerializeField, Min(0.01f)] float trailWidth = 0.3f;
        [SerializeField, Range(0f, 1f)] float trailAlpha = 0.45f;
        [Tooltip("Where the trail (and afterglow) starts, in degrees from the beam centre. Positive tucks it under the cone, negative leaves a gap behind it.")]
        [SerializeField, Range(-30f, 30f)] float trailStartOffset = 4f;
        [Tooltip("Degrees over which the trail fades in from the cone. Higher = softer join.")]
        [SerializeField, Range(0f, 90f)] float trailFadeIn = 25f;
        [Tooltip("How fast the trail fades out behind the beam. Higher = shorter, lighter tail.")]
        [SerializeField, Range(0.5f, 4f)] float trailFalloff = 2.2f;
        [Tooltip("Faint swept fill right behind the beam (radar afterglow).")]
        [SerializeField, Range(0f, 180f)] float sweepDegrees = 50f;
        [SerializeField, Range(0f, 1f)] float sweepAlpha = 0.12f;

        [Header("Burn")]
        [SerializeField] string targetTag = "Enemy";
        [Tooltip("Each target burns at most once per this many seconds.")]
        [SerializeField, Min(0.05f)] float burnInterval = 0.5f;
        [Tooltip("Treat targets as discs this wide when testing beam overlap.")]
        [SerializeField, Min(0f)] float targetRadius = 0.6f;
        [Tooltip("Burn any target that stays inside the radius for a full revolution without being lit (stops enemies circling in step with the beam).")]
        [SerializeField] bool guaranteeFullTurn = true;
        [Tooltip("Apply damage directly to IDamageable targets (preview / prototyping). Gameplay can use onBurn instead.")]
        [SerializeField] bool applyDamage = true;
        [SerializeField, Min(0f)] float damagePerBurn = 12f;
        [SerializeField, Range(1, 12)] int puffParticles = 5;
        [SerializeField] bool tintTargets = true;
        [SerializeField] Color burnTint = new(1f, 0.42f, 0.1f, 1f);
        [SerializeField, Min(0.01f)] float tintTime = 0.22f;
        [Tooltip("After each burn the target keeps flickering with small flames for this long (seconds).")]
        [SerializeField, Min(0f)] float afterburnTime = 0.8f;
        [Tooltip("Flames per second while a target is on fire.")]
        [SerializeField, Min(0f)] float afterburnRate = 12f;
        [Tooltip("Destroy targets the lantern kills (prototype). Turn off once enemies handle their own death.")]
        [SerializeField] bool destroyOnKill = true;
        [SerializeField, Min(0f)] float killDelay = 0.25f;

        [Header("Lantern")]
        [Tooltip("Colour of the (unlit) glowing glass. Flicker modulates it.")]
        [SerializeField] Color glassEmission = new(1f, 0.76f, 0.32f, 1f);
        [SerializeField, Range(0f, 0.5f)] float flicker = 0.1f;

        [Header("Events")]
        [Tooltip("Fired for every burn tick (once per target per burn interval).")]
        public TransformEvent onBurn = new();

        public float Range { get => range; set { range = Mathf.Max(1f, value); _dirty = true; } }
        public float BeamAngle { get => beamAngle; set { beamAngle = Mathf.Clamp(value, 5f, 90f); _dirty = true; } }
        public float RotationSpeed { get => rotationSpeed; set => rotationSpeed = Mathf.Max(0f, value); }
        public float BurnInterval { get => burnInterval; set => burnInterval = Mathf.Max(0.05f, value); }
        public float DamagePerBurn { get => damagePerBurn; set => damagePerBurn = Mathf.Max(0f, value); }
        /// <summary>Current world yaw of the beam centre line (degrees).</summary>
        public float BeamYaw => _angle;

        const float CandidateRefresh = 0.1f;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int VolColorId = Shader.PropertyToID("_Color");
        static readonly int VolOpacityId = Shader.PropertyToID("_Opacity");
        MaterialPropertyBlock _volMpb;

        float _angle;
        float _refreshT;
        float _builtHeight = float.NaN;
        bool _dirty = true;
        Mesh _beamMesh, _trailMesh, _sweepMesh;
        MaterialPropertyBlock _mpb;

        readonly List<Transform> _candidates = new();
        readonly Dictionary<Transform, float> _lastBurn = new();
        readonly Dictionary<Transform, float> _seenSince = new();
        readonly Dictionary<Transform, float> _prevRel = new();
        readonly List<Transform> _scratch = new();
        readonly Dictionary<Transform, TintState> _tints = new();
        readonly Dictionary<Transform, float> _onFire = new();

        sealed class TintState
        {
            public Renderer[] Renderers;
            public Color[] BaseColors;
            public float Timer;
        }

        // ---------------------------------------------------------------- lifecycle

void OnEnable()
        {
            _mpb ??= new MaterialPropertyBlock();
            _volMpb ??= new MaterialPropertyBlock();
            _angle = lanternHead != null ? lanternHead.eulerAngles.y : transform.eulerAngles.y;
            _dirty = true;
            if (volumeCone == null) FindVolume();
            if (volumeCone != null) volumeCone.enabled = false; // drawn by OnBeginCamera instead
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        void FindVolume()
        {
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
                if (r.name.Contains("V Cone")) { volumeCone = r; break; }
            if (volumeCone != null && volumeLight == null) volumeLight = volumeCone.GetComponentInChildren<Light>(true);
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            if (volumeCone != null) volumeCone.enabled = true;
            foreach (var kv in _tints) ApplyTint(kv.Value, 0f);
            _tints.Clear();
            _onFire.Clear();
            _lastBurn.Clear();
            _seenSince.Clear();
            _prevRel.Clear();
            DestroyMesh(ref _beamMesh);
            DestroyMesh(ref _trailMesh);
            DestroyMesh(ref _sweepMesh);
        }

        void OnValidate() => _dirty = true;

void Update()
        {
            if (!Application.isPlaying)
            {
                _angle = lanternHead != null ? lanternHead.eulerAngles.y : transform.eulerAngles.y;
                PlaceLight();
                return;
            }

            var dt = Time.deltaTime;
            _angle = Mathf.Repeat(_angle + rotationSpeed * dt, 360f);
            if (lanternHead != null) lanternHead.rotation = Quaternion.Euler(0f, _angle, 0f);
            Flicker();
            PlaceLight();

            _refreshT -= dt;
            if (_refreshT <= 0f)
            {
                _refreshT = CandidateRefresh;
                GatherCandidates();
            }
            CheckBeam();
            UpdateTints(dt);
            UpdateFire(dt);
        }

        // ---------------------------------------------------------------- burning

        void GatherCandidates()
        {
            _candidates.Clear();
            if (!string.IsNullOrEmpty(targetTag))
            {
                GameObject[] found;
                try { found = GameObject.FindGameObjectsWithTag(targetTag); }
                catch (UnityException) { found = System.Array.Empty<GameObject>(); }

                var origin = PivotPosition();
                var reach = range + targetRadius + 2f; // margin so fast enemies are tracked before they enter
                var reachSqr = reach * reach;
                for (var i = 0; i < found.Length; i++)
                {
                    var go = found[i];
                    if (go == null || !go.activeInHierarchy || go.transform.IsChildOf(transform)) continue;
                    var d = go.transform.position - origin;
                    d.y = 0f;
                    if (d.sqrMagnitude <= reachSqr) _candidates.Add(go.transform);
                }
            }

            Prune(_lastBurn);
            Prune(_seenSince);
            Prune(_prevRel);
        }

        void Prune(Dictionary<Transform, float> map)
        {
            _scratch.Clear();
            foreach (var kv in map)
                if (kv.Key == null || !_candidates.Contains(kv.Key)) _scratch.Add(kv.Key);
            for (var i = 0; i < _scratch.Count; i++) map.Remove(_scratch[i]);
        }

void CheckBeam()
        {
            if (_candidates.Count == 0) return;
            var origin = PivotPosition();
            var rot = Quaternion.Euler(0f, _angle, 0f);
            var fwd = rot * Vector3.forward;
            var right = rot * Vector3.right;
            Footprint(out _, out var dc, out var ea, out var eb);
            var ia = ea + targetRadius;
            var ib = eb + targetRadius;
            var maxDist = range + targetRadius;
            var now = Time.time;
            var fullTurn = 360f / Mathf.Max(rotationSpeed, 1f) + burnInterval;

            for (var i = 0; i < _candidates.Count; i++)
            {
                var t = _candidates[i];
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                var d = t.position - origin;
                d.y = 0f;
                var dist = d.magnitude;

                if (dist > maxDist)
                {
                    _seenSince.Remove(t);
                    _prevRel.Remove(t);
                    continue;
                }
                if (!_seenSince.ContainsKey(t)) _seenSince[t] = now;

                var rel = dist < 0.001f ? 0f : Vector3.SignedAngle(fwd, d, Vector3.up);
                var pad = dist < 0.001f ? 180f : Mathf.Asin(Mathf.Min(1f, targetRadius / dist)) * Mathf.Rad2Deg;
                // angular half-width of the lit pool at this distance
                var q = (dist - dc) / ea;
                var ext = Mathf.Abs(q) < 1f ? eb * Mathf.Sqrt(1f - q * q) : 0f;
                var h = (dist < 0.001f ? 0f : Mathf.Atan2(ext, dist) * Mathf.Rad2Deg) + pad;

                // 1) halo under the lantern
                var inside = dist <= nearRadius + targetRadius;
                // 2) inside the lit pool on the water this frame
                if (!inside)
                {
                    var u = (Vector3.Dot(d, fwd) - dc) / ia;
                    var v = Vector3.Dot(d, right) / ib;
                    inside = u * u + v * v <= 1f;
                }
                // 3) swept: the beam passed over the target since last frame
                if (!inside && _prevRel.TryGetValue(t, out var prev))
                {
                    var end = prev + Mathf.DeltaAngle(prev, rel);
                    inside = Mathf.Min(prev, end) <= h && Mathf.Max(prev, end) >= -h;
                }
                _prevRel[t] = rel;

                var hasLast = _lastBurn.TryGetValue(t, out var last);
                // 4) full-turn guarantee
                if (!inside && guaranteeFullTurn)
                {
                    var since = hasLast ? Mathf.Max(last, _seenSince[t]) : _seenSince[t];
                    inside = now - since >= fullTurn;
                }

                if (!inside) continue;
                if (hasLast && now - last < burnInterval) continue;

                _lastBurn[t] = now;
                Burn(t);
            }
        }

void Burn(Transform t)
        {
            var p = t.position;
            var b = TargetBounds(t);
            EmitFlames(b, puffParticles);
            if (burnRing != null)
                burnRing.Emit(new ParticleSystem.EmitParams { position = new Vector3(p.x, transform.position.y + beamHeight + 0.03f, p.z) }, 1);

            if (tintTargets) StartTint(t);
            if (afterburnTime > 0f) _onFire[t] = afterburnTime;

            if (applyDamage && damagePerBurn > 0f)
            {
                var dmg = t.GetComponent<IDamageable>();
                if (dmg != null && dmg.IsAlive)
                {
                    dmg.TakeDamage(damagePerBurn, p, false);
                    if (!dmg.IsAlive) Kill(t, b);
                }
            }

            onBurn?.Invoke(t);
        }

        void Kill(Transform t, Bounds b)
        {
            EmitFlames(b, Mathf.Max(8, puffParticles * 4));
            if (burnRing != null)
                burnRing.Emit(new ParticleSystem.EmitParams { position = new Vector3(b.center.x, transform.position.y + beamHeight + 0.03f, b.center.z), startSize = 2.5f }, 1);
            _onFire.Remove(t);
            if (destroyOnKill) Destroy(t.gameObject, killDelay);
        }

        /// <summary>World bounds of the target's renderers (falls back to a 1 m box at its position).</summary>
        static Bounds TargetBounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            var has = false;
            var b = new Bounds(t.position, Vector3.one);
            for (var i = 0; i < rs.Length; i++)
            {
                if (rs[i] is ParticleSystemRenderer || !rs[i].enabled) continue;
                if (!has) { b = rs[i].bounds; has = true; }
                else b.Encapsulate(rs[i].bounds);
            }
            return b;
        }

        /// <summary>Flames on the target's upper body/outside, so they aren't hidden inside the mesh.</summary>
        void EmitFlames(Bounds b, int count)
        {
            if (burnPuff == null || count <= 0) return;
            var e = b.extents;
            for (var i = 0; i < count; i++)
            {
                var dir = Random.insideUnitCircle.normalized;
                var pos = new Vector3(
                    b.center.x + dir.x * e.x * Random.Range(0.6f, 1.1f),
                    Mathf.Lerp(b.center.y, b.max.y + 0.15f, Random.value),
                    b.center.z + dir.y * e.z * Random.Range(0.6f, 1.1f));
                burnPuff.Emit(new ParticleSystem.EmitParams
                {
                    position = pos,
                    velocity = new Vector3(dir.x * 0.3f, Random.Range(1f, 2f), dir.y * 0.3f),
                    applyShapeToPosition = false
                }, 1);
            }
        }

        void UpdateFire(float dt)
        {
            if (_onFire.Count == 0) return;
            _scratch.Clear();
            foreach (var kv in _onFire) _scratch.Add(kv.Key);
            for (var i = 0; i < _scratch.Count; i++)
            {
                var t = _scratch[i];
                var left = _onFire[t] - dt;
                if (t == null || left <= 0f) { _onFire.Remove(t); continue; }
                _onFire[t] = left;
                // fade the flame rate out as the afterburn ends
                var rate = afterburnRate * Mathf.Clamp01(left / Mathf.Max(0.01f, afterburnTime) * 1.5f);
                var n = rate * dt;
                var count = (int)n + (Random.value < n - (int)n ? 1 : 0);
                if (count > 0) EmitFlames(TargetBounds(t), count);
            }
        }

        void StartTint(Transform t)
        {
            if (!_tints.TryGetValue(t, out var s))
            {
                var rs = t.GetComponentsInChildren<Renderer>();
                s = new TintState { Renderers = rs, BaseColors = new Color[rs.Length] };
                for (var i = 0; i < rs.Length; i++)
                {
                    var m = rs[i].sharedMaterial;
                    s.BaseColors[i] = m != null && m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;
                }
                _tints[t] = s;
            }
            s.Timer = tintTime;
        }

        void UpdateTints(float dt)
        {
            if (_tints.Count == 0) return;
            _scratch.Clear();
            foreach (var kv in _tints)
            {
                var s = kv.Value;
                s.Timer -= dt;
                var k = Mathf.Clamp01(s.Timer / tintTime);
                ApplyTint(s, k * k);
                if (kv.Key == null || s.Timer <= 0f) _scratch.Add(kv.Key);
            }
            for (var i = 0; i < _scratch.Count; i++) _tints.Remove(_scratch[i]);
        }

        void ApplyTint(TintState s, float strength)
        {
            for (var i = 0; i < s.Renderers.Length; i++)
            {
                var r = s.Renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, Color.Lerp(s.BaseColors[i], burnTint, strength));
                r.SetPropertyBlock(_mpb);
            }
        }

        void Flicker()
        {
            if (glassRenderer == null) return;
            var n = Mathf.PerlinNoise(Time.time * 6f, 0.37f) * 0.7f + Mathf.PerlinNoise(Time.time * 17f, 2.1f) * 0.3f;
            var k = 1f + flicker * (n - 0.5f) * 2f;
            var c = glassEmission * k;
            c.a = 1f;
            glassRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c);
            glassRenderer.SetPropertyBlock(_mpb);
        }

        // ---------------------------------------------------------------- rendering

        /// <summary>Lantern axis at glass height - the beam's apex and the centre of the hit test.</summary>
        Vector3 PivotPosition()
        {
            var axis = lanternHead != null ? lanternHead.position : transform.position;
            var y = lightSource != null ? lightSource.position.y : axis.y;
            return new Vector3(axis.x, y, axis.z);
        }

        float SourceHeight() => PivotPosition().y - (transform.position.y + beamHeight);

/// <summary>Horizontal distance from the lantern axis to the lens face, where the beam begins.</summary>
float LensOffset()
        {
            if (lanternHead == null || lightSource == null) return 0f;
            var d = lightSource.position - lanternHead.position;
            d.y = 0f;
            return Mathf.Min(d.magnitude, range * 0.5f);
        }

        /// <summary>Lit pool on the water where the cone lands: ellipse from <paramref name="near"/> to the full range.
        /// Centre <paramref name="dc"/> along the beam, semi-axes <paramref name="a"/> (along) and <paramref name="b"/> (across).</summary>
void Footprint(out float near, out float dc, out float a, out float b)
        {
            // where the volume cone meets the water (pivot space): near edge .. range along the beam
            Cone(out var tilt, out _, out _);
            var h = Mathf.Max(0.1f, SourceHeight());
            var half = beamAngle * 0.5f * Mathf.Deg2Rad;
            var low = Mathf.Min(tilt + half, 89f * Mathf.Deg2Rad);
            near = Mathf.Min(LensOffset() + h / Mathf.Tan(low), range * 0.9f);
            dc = (near + range) * 0.5f;
            a = (range - near) * 0.5f;
            b = Mathf.Max(0.3f, h / Mathf.Sin(tilt) * Mathf.Tan(half));
        }

        /// <summary>Volume cone: tilt (axis angle below horizontal) chosen so the cone's upper edge lands on the
        /// water exactly at <see cref="range"/>. Length runs until that edge meets the water, so the wide end sits in the water.</summary>
        void Cone(out float tilt, out float length, out float radius)
        {
            var h = Mathf.Max(0.1f, SourceHeight());
            var half = beamAngle * 0.5f * Mathf.Deg2Rad;
            var reach = Mathf.Max(0.5f, range - LensOffset());
            tilt = half + Mathf.Atan2(h, reach);
            var upper = Mathf.Max(0.02f, tilt - half);
            length = h / Mathf.Sin(upper) * Mathf.Cos(half);
            radius = length * Mathf.Tan(half) / 0.9f; // mesh: base radius 1, tip radius 0.1, height 2 along -Y
        }

        Vector3 ConeApex()
        {
            if (lightSource != null) return lightSource.position;
            return PivotPosition() + Quaternion.Euler(0f, _angle, 0f) * Vector3.forward * LensOffset();
        }

        Vector3 ConeAxis(float tilt) => Quaternion.Euler(0f, _angle, 0f) * new Vector3(0f, -Mathf.Sin(tilt), Mathf.Cos(tilt));

        void PlaceLight()
        {
            if (volumeLight == null) return;
            Cone(out var tilt, out var len, out _);
            volumeLight.transform.SetPositionAndRotation(ConeApex(), Quaternion.LookRotation(ConeAxis(tilt)));
            volumeLight.spotAngle = Mathf.Min(170f, beamAngle * 1.3f);
            volumeLight.range = len * 1.4f;
            var c = volumeColor;
            c.a = 1f;
            volumeLight.color = c;
            var n = Application.isPlaying ? Mathf.PerlinNoise(Time.time * 6f, 0.37f) : 0.5f;
            volumeLight.intensity = volumeLightIntensity * (1f + flicker * (n - 0.5f) * 2f);
        }


void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if ((cam.cullingMask & (1 << gameObject.layer)) == 0) return;

            var h = SourceHeight();
            if (_dirty || _trailMesh == null || Mathf.Abs(h - _builtHeight) > 0.02f)
                RebuildMeshes(h);

            var pivot = PivotPosition();
            var matrix = Matrix4x4.TRS(pivot, Quaternion.Euler(0f, _angle, 0f), Vector3.one);
            var bounds = new Bounds(pivot, Vector3.one * (range * 2f + 4f));

            // volume cone: lens -> water, turning with the beam
            if (volumeCone != null && volumeCone.sharedMaterial != null)
            {
                var mf = volumeCone.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    Cone(out var tilt, out var len, out var rad);
                    var axis = ConeAxis(tilt);
                    var coneMatrix = Matrix4x4.TRS(ConeApex(), Quaternion.FromToRotation(Vector3.up, -axis), new Vector3(rad, len * 0.5f, rad));
                    var n = Application.isPlaying ? Mathf.PerlinNoise(Time.time * 6f, 0.37f) : 0.5f;
                    _volMpb.SetColor(VolColorId, volumeColor);
                    _volMpb.SetFloat(VolOpacityId, volumeOpacity * (1f + flicker * (n - 0.5f) * 2f));
                    var vp = new RenderParams(volumeCone.sharedMaterial)
                    {
                        camera = cam,
                        layer = volumeCone.gameObject.layer,
                        shadowCastingMode = ShadowCastingMode.Off,
                        receiveShadows = false,
                        matProps = _volMpb,
                        worldBounds = bounds
                    };
                    Graphics.RenderMesh(vp, mf.sharedMesh, 0, coneMatrix);
                }
            }

            var rp = new RenderParams(trailMaterial != null ? trailMaterial : sweepMaterial)
            {
                camera = cam,
                layer = gameObject.layer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = bounds
            };
            if (sweepMaterial != null && _sweepMesh != null && _sweepMesh.vertexCount > 0)
            {
                rp.material = sweepMaterial;
                Graphics.RenderMesh(rp, _sweepMesh, 0, matrix);
            }
            if (trailMaterial != null && _trailMesh != null && trailDegrees > 0f)
            {
                rp.material = trailMaterial;
                Graphics.RenderMesh(rp, _trailMesh, 0, matrix);
            }
        }

void RebuildMeshes(float sourceHeight)
        {
            _builtHeight = sourceHeight;
            _dirty = false;
            var water = -sourceHeight; // pivot sits at glass height; water plane is this far below
            BuildTrail(ref _trailMesh, water + 0.015f);
            BuildSweep(ref _sweepMesh, water + 0.005f);
        }

        // warm palette: hot core, orange body, deep orange fringe
        static readonly Color Core = new(1f, 0.8f, 0.42f);
        static readonly Color Body = new(1f, 0.62f, 0.16f);
        static readonly Color Fringe = new(1f, 0.6f, 0.12f);
        static readonly Color TrailSide = new(1f, 0.66f, 0.2f);
        static readonly Color Centre = new(1f, 0.8f, 0.4f);



void BuildTrail(ref Mesh mesh, float y)
        {
            // soft glow line at the beam's reach, trailing behind the cone. Fades IN from the cone over
            // trailFadeIn degrees (no hard join), then fades OUT over trailDegrees.
            var start = trailStartOffset;
            var end = start - trailDegrees;
            var segs = Mathf.Max(8, Mathf.CeilToInt((start - end) / 2f));
            // feathered on both sides, never past the beam's own reach
            float[] radii = { range - trailWidth, range - trailWidth * 0.5f, range - trailWidth * 0.15f, range };
            float[] profile = { 0f, 0.85f, 1f, 0f };

            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (var ri = 0; ri < radii.Length; ri++)
            {
                for (var s = 0; s <= segs; s++)
                {
                    var deg = Mathf.Lerp(start, end, s / (float)segs);
                    var along = start - deg;
                    var behind = Mathf.Clamp01(along / Mathf.Max(1f, trailDegrees));
                    var fadeIn = trailFadeIn > 0f ? Smooth01(along / trailFadeIn) : 1f;
                    var a = deg * Mathf.Deg2Rad;
                    var c = Color.Lerp(Centre, TrailSide, behind);
                    c.a = trailAlpha * profile[ri] * fadeIn * Mathf.Pow(1f - behind, trailFalloff);
                    verts.Add(new Vector3(Mathf.Sin(a) * radii[ri], y, Mathf.Cos(a) * radii[ri]));
                    cols.Add(c);
                    uvs.Add(new Vector2(s / (float)segs, ri / (float)(radii.Length - 1)));
                }
            }
            Grid(tris, 0, segs, radii.Length - 1);
            Assign(ref mesh, "LighthouseLantern_Trail", verts, cols, uvs, tris);
        }

void BuildSweep(ref Mesh mesh, float y)
        {
            // radar afterglow behind the beam + the halo pool under the lantern (close-range burn zone).
            // Depth-tested, so the halo sits on the water around the hull instead of covering the lantern.
            var half = beamAngle * 0.5f;
            var segs = Mathf.Max(4, Mathf.CeilToInt(sweepDegrees / 3f));
            const int rings = 6;
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var inner = Mathf.Min(1f, range * 0.2f);
            if (sweepDegrees > 0f && sweepAlpha > 0f)
            {
                for (var ri = 0; ri <= rings; ri++)
                {
                    var k = ri / (float)rings;
                    var r = Mathf.Lerp(inner, range, k);
                    var radial = Smooth01(k / 0.5f);
                    for (var s = 0; s <= segs; s++)
                    {
                        var u = s / (float)segs;
                        var deg = trailStartOffset - u * sweepDegrees; // same soft start as the trail
                        var a = deg * Mathf.Deg2Rad;
                        var c = TrailSide;
                        var fadeIn = trailFadeIn > 0f ? Smooth01(u * sweepDegrees / trailFadeIn) : 1f;
                        c.a = sweepAlpha * radial * fadeIn * (1f - u) * (1f - u);
                        verts.Add(new Vector3(Mathf.Sin(a) * r, y, Mathf.Cos(a) * r));
                        cols.Add(c);
                        uvs.Add(new Vector2(u, k));
                    }
                }
                Grid(tris, 0, segs, rings);
            }

            if (nearRadius > 0f && haloAlpha > 0f)
            {
                const int hs = 32, hr = 4;
                var baseIndex = verts.Count;
                for (var ri = 0; ri <= hr; ri++)
                {
                    var k = ri / (float)hr;
                    var r = k * nearRadius * 1.15f;
                    var a = haloAlpha * (1f - Smooth01((k - 0.55f) / 0.45f));
                    for (var s = 0; s <= hs; s++)
                    {
                        var ang = s / (float)hs * Mathf.PI * 2f;
                        var c = Color.Lerp(Core, Body, k);
                        c.a = a;
                        verts.Add(new Vector3(Mathf.Sin(ang) * r, y, Mathf.Cos(ang) * r));
                        cols.Add(c);
                        uvs.Add(new Vector2(s / (float)hs, 0f));
                    }
                }
                Grid(tris, baseIndex, hs, hr);
            }
            Assign(ref mesh, "LighthouseLantern_Sweep", verts, cols, uvs, tris);
        }

        static void Grid(List<int> tris, int baseIndex, int cols, int rows)
        {
            var w = cols + 1;
            for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
            {
                var a = baseIndex + r * w + c;
                var b = a + w;
                tris.Add(a); tris.Add(b); tris.Add(a + 1);
                tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
            }
        }

static void Assign(ref Mesh mesh, string name, List<Vector3> v, List<Color> c, List<Vector2> uv, List<int> t)
        {
            // palette is authored in sRGB; vertex colours aren't converted automatically in Linear space
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                for (var i = 0; i < c.Count; i++)
                {
                    var a = c[i].a;
                    var lin = c[i].linear;
                    lin.a = a;
                    c[i] = lin;
                }
            if (mesh == null) mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            mesh.Clear();
            mesh.SetVertices(v);
            mesh.SetColors(c);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
        }

        static void DestroyMesh(ref Mesh m)
        {
            if (m == null) return;
            if (Application.isPlaying) Destroy(m); else DestroyImmediate(m);
            m = null;
        }

        static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        void OnDrawGizmosSelected()
        {
            var c = PivotPosition();
            c.y = transform.position.y + beamHeight;
            DrawCircle(c, range, new Color(1f, 0.8f, 0.3f, 0.6f));
            DrawCircle(c, nearRadius, new Color(1f, 0.55f, 0.2f, 0.6f));
        }

        static void DrawCircle(Vector3 c, float radius, Color col)
        {
            Gizmos.color = col;
            const int n = 48;
            var prev = c + new Vector3(radius, 0f, 0f);
            for (var i = 1; i <= n; i++)
            {
                var a = i / (float)n * Mathf.PI * 2f;
                var p = c + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
    }
}
