using System.Collections.Generic;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.Enemies;
using RoguePirates.V4.Foundation.VFX;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Presentation.Pooling;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Presentation.Effects
{
    /// <summary>
    /// Enemy models and their health bars (M14.10; port of V3's EnemyViewPool use in EnemyWorld, was LegacyEnemyViewSink):
    /// the enemy's model (or V3's proxy model, or the placeholder cube tinted by the definition, magenta when elite) at
    /// its yaw offset and scale with V3's spawn animation variation, the health bar at the enemy bar height, the swordfish
    /// spring, the ship wake and the burn flash (V3's shared helpers). Models come from the pool catalog's "enemy/&lt;model&gt;"
    /// pools (prewarmed per reachable model; a model outside the catalog gets its pool on first use), bars from
    /// "enemy/health_bar".
    /// <para>Intended change 2: each pooled model has its colliders, rigidbodies and NavMesh components removed at once
    /// on first use, while V3's pool left a new model's island colliders until the end of the frame (R-81) and switched
    /// /Systems off and on around each new model (R-72).</para>
    /// </summary>
    public sealed class EnemyViewPresenter : IEnemyViewSink
    {
        public const string HealthBarKey = PoolCatalog.EnemyKeyPrefix + "health_bar";
        const int PrewarmBars = 80; // M14.15: ~70 enemies peaked at 73 bars
        const int PrewarmCubes = 8;

        readonly PoolService _pools;
        readonly DemoJuiceSettings _juice;
        readonly Dictionary<GameObject, string> _keys = new Dictionary<GameObject, string>();
        readonly Dictionary<Transform, string> _liveKey = new Dictionary<Transform, string>();
        readonly HashSet<int> _prepared = new HashSet<int>();
        readonly Dictionary<int, (TrailRenderer[] trails, ParticleSystem[] particles)> _motion =
            new Dictionary<int, (TrailRenderer[], ParticleSystem[])>();
        readonly EnemyViewTint _tint = new EnemyViewTint();
        GameObject _cube;

        public int LiveCount => _liveKey.Count;
        /// <summary>V5 M3d: the views' tints (tests).</summary>
        public EnemyViewTint Tint => _tint;
        /// <summary>Demo: the artists' per-enemy VFX profiles (trail, hit).</summary>
        public VfxProfilePlayer Profiles { get => _profiles; set { _profiles = value; RegisterEliteBases(); } }
        VfxProfilePlayer _profiles;
        /// <summary>Playtest (3 Oct, owner): the live boss's view (null when there is none), for the off-screen boss arrow.</summary>
        public Transform BossView { get; private set; }

        public EnemyViewPresenter(PoolService pools, DemoJuiceSettings juice)
        {
            _pools = pools;
            _juice = juice;
            if (_pools == null) return;
            // V5 M3e: models are stripped while still inactive (their NavMeshAgents woke and logged on first acquire).
            _pools.RegisterPreparer(PoolCatalog.EnemyKeyPrefix, go => { if (_prepared.Add(go.GetInstanceID())) StripImmediate(go); });
            if (_juice != null && _juice.enemyHealthBarPrefab != null && !_pools.Has(HealthBarKey))
            {
                _pools.Register(new PoolDefinition(HealthBarKey, _juice.enemyHealthBarPrefab, PrewarmBars, 0, ConfigEnums.PoolOverflowPolicy.Grow, "enemy health bars"));
                _pools.Prewarm(HealthBarKey);
            }
            _cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cube.name = "EnemyPlaceholder";
            _cube.SetActive(false);
            _cube.transform.SetParent(_pools.Staging, false);
            Object.DestroyImmediate(_cube.GetComponent<Collider>());
            Register(_cube, PrewarmCubes, "placeholder cubes");
        }

        // Playtest (3 Oct, owner): one colour per enemy ship type and variant, readable at a glance from the camera (sails
        // and flags in the colour, the hull leaning towards it). V5 M8z (7 Oct): the colours are enemy data (the archetype's
        // visual.typeColor, a variant's look.typeColor), no longer a table keyed by enemy id here; an enemy without one
        // (creatures, the boss) keeps its data tint.
        const float HullTintStrength = 0.6f;

        /// <summary>The enemy type's colour (its data tint when it has none). V5 M8z (7 Oct): read from the definition.</summary>
        public static Color TypeColor(EnemyDefinition def) => def != null ? def.SeenColor : Color.white;

        EnemyDefinition[] _definitions;

        /// <summary>V3 only recorded the model ids; models are stripped per instance. V5 M8z (7 Oct): the elites' base
        /// enemies go to the VFX profiles (an elite without its own profile uses its base enemy's).</summary>
        public void Prepare(EnemyDefinition[] definitions)
        {
            _definitions = definitions;
            RegisterEliteBases();
        }

        void RegisterEliteBases()
        {
            if (_profiles == null || _definitions == null) return;
            foreach (var def in _definitions)
                if (def != null && def.isElite && !string.IsNullOrEmpty(def.baseEnemyId)) _profiles.SetEnemyFallback(def.id, def.baseEnemyId);
        }

        public float FallbackVisualScale(EnemyDefinition def) => EnemyVisualFallbackResolver.ResolveVisualScale(def);

        public Transform CreateView(EnemyDefinition def, Vector3 position, float scale, bool elite, int animationSeed)
        {
            var view = CreateModel(def, position, scale, elite, animationSeed);
            if (view != null && def.isBoss) BossView = view;
            return view;
        }

        Transform CreateModel(EnemyDefinition def, Vector3 position, float scale, bool elite, int animationSeed)
        {
            if (def == null || _pools == null) return null;
            var prefab = def.visualPrefab != null ? def.visualPrefab : EnemyVisualFallbackResolver.ResolvePrefab(def);
            if (prefab != null)
            {
                var view = Acquire(prefab, position, Quaternion.Euler(0f, def.visualYawOffset, 0f));
                if (view == null) return null;
                view.localScale = Vector3.one * (scale * (def.viewSize > 0f ? def.viewSize : 1f));
                _tint.Record(view); // its own materials, recorded on its first use (before any burn flash)
                // V5 M3d: a variant's own colours (pooled views share models). Playtest (3 Oct, owner: "most of the ships
                // were visually the same"): each ship type has its own colour on its sails and flags, and its hull leans
                // towards it; a creature keeps its data tint. Playtest (3 Oct, owner, round 7): no masthead pennant ("a coloured
                // bar above almost every enemy").
                var accent = TypeColor(def);
                var hullTint = accent == def.visualTint ? def.visualTint : Color.Lerp(Color.white, accent, HullTintStrength);
                _tint.Apply(view, hullTint, accent);
                EnemyViewAnimation.ApplySpawnVariation(view, animationSeed);
                Profiles?.EnemyViewCreated(def.id, view);
                return view;
            }

            // V3's placeholder: a collider-less cube the size of the enemy, tinted (magenta when elite).
            var cube = Acquire(_cube, position, Quaternion.identity);
            if (cube == null) return null;
            cube.name = string.IsNullOrEmpty(def.displayName) ? def.id : def.displayName;
            cube.localScale = Vector3.one * (def.radius * 2f * scale);
            var renderer = cube.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = elite ? Color.magenta : def.placeholderColor;
            Profiles?.EnemyViewCreated(def.id, cube);
            return cube;
        }

        public Transform AttachHealthBar(EnemyDefinition def, Transform view, bool elite)
        {
            if (view == null || _pools == null || !_pools.Has(HealthBarKey)) return null;
            // Playtest (4 Oct, owner): health bars on elites, minibosses and the boss only (not ordinary enemies). An elite is
            // a unit with an elite affix or an elite variant (V5 M8z, 7 Oct: the variant's isElite flag, not its id prefix).
            var isElite = elite || (def != null && def.isElite);
            if (def == null || (!def.isBoss && !def.isMiniboss && !isElite)) return null;
            var go = _pools.Acquire(HealthBarKey, view.position, Quaternion.identity);
            if (go == null) return null;
            var bar = go.GetComponent<WorldHealthBar>();
            if (bar == null) bar = go.AddComponent<WorldHealthBar>();
            bar.Bind(view, _juice.enemyHealthBarHeight);
            bar.SetFill(1f);
            return go.transform;
        }

        /// <summary>Playtest (3 Oct, owner, round 7: "in pvp i couldn't see other player's ... health"): the enemy health bar
        /// over the 1v1 opponent's ship (the caller sets its fill and releases it).</summary>
        public WorldHealthBar AttachOpponentHealthBar(Transform view)
        {
            if (view == null || _pools == null || !_pools.Has(HealthBarKey)) return null;
            var go = _pools.Acquire(HealthBarKey, view.position, Quaternion.identity);
            if (go == null) return null;
            var bar = go.GetComponent<WorldHealthBar>();
            if (bar == null) bar = go.AddComponent<WorldHealthBar>();
            bar.Bind(view, _juice != null ? _juice.enemyHealthBarHeight : 3f);
            bar.SetFill(1f);
            return bar;
        }

        public void Release(EnemyDefinition def, Transform view, Transform healthBar)
        {
            if (view != null && view == BossView) BossView = null;
            if (view != null)
            {
                Profiles?.EnemyViewReleased(view);
                EnemyBurnFlash.SetBurning(view, false);
                ClearMotion(view.gameObject);
                _liveKey.Remove(view);
                if (!_pools.Release(view.gameObject)) Object.Destroy(view.gameObject);
            }
            if (healthBar != null && !_pools.Release(healthBar.gameObject)) Object.Destroy(healthBar.gameObject);
        }

        /// <summary>Playtest (3 Oct, owner): the live enemy view nearest a point within a radius (null when none), for the
        /// grouped hit feedback (burn ticks and other damage without a view of its own).</summary>
        public Transform NearestView(Vector3 position, float radius)
        {
            Transform best = null;
            var bestSqr = radius * radius;
            foreach (var view in _liveKey.Keys)
            {
                if (view == null) continue;
                var d = view.position - position;
                d.y = 0f;
                var sqr = d.sqrMagnitude;
                if (sqr <= bestSqr) { bestSqr = sqr; best = view; }
            }
            return best;
        }

        public void TickSpring(Transform view, ref bool springing, bool isSpringing) => EnemyViewAnimation.TickSwordfish(view, ref springing, isSpringing);
        /// <summary>
        /// The ship wake (V5 M3e, replacing a per-frame search): a ship's wake and foam parts (names with "Wake" or
        /// "Foam") are switched on while it moves faster than 0.35 squared m/s, its trails emit and its wake particles
        /// play; still, they stop and the trails clear. The parts are found once per life of a pooled view and touched
        /// only when the ship starts or stops moving (the old helper searched the model's children every frame).
        /// </summary>
        public void TickWake(Transform view, EnemyCategory category, Vector3 velocity)
        {
            if (view == null || category != EnemyCategory.Ship) return;
            if (!_wakes.TryGetValue(view, out var wake)) _wakes[view] = wake = FindWake(view);
            // Playtest (3 Oct, owner: "the enemy trails were behaving weirdly"). A new life sizes the trails to the
            // enemy's drawn size (they were the model's 2 m whatever the enemy's scale); a jump (the leash moving a far
            // enemy ahead of the player, a respawn) clears them instead of drawing a line across the sea; and the wake
            // starts and stops with some slack, fading out rather than vanishing when a ship slows.
            var position = view.position;
            if (wake.State == -1) ScaleTrails(view, wake);
            else if ((position - wake.LastPosition).sqrMagnitude > WakeJumpSqr) ClearWake(wake);
            wake.LastPosition = position;
            var speedSqr = velocity.sqrMagnitude;
            var moving = wake.State == 1 ? speedSqr > WakeStopSpeedSqr : speedSqr > WakeSpeedSqr;
            if (moving)
                for (var i = 0; i < wake.Roots.Length; i++)
                    if (wake.Roots[i] != null && !wake.Roots[i].activeSelf) wake.Roots[i].SetActive(true);
            var state = moving ? 1 : 0;
            if (wake.State == state) return;
            wake.State = state;
            for (var i = 0; i < wake.Trails.Length; i++)
            {
                var trail = wake.Trails[i];
                if (trail == null || !trail.gameObject.activeInHierarchy) continue;
                trail.emitting = moving;
            }
            for (var i = 0; i < wake.Particles.Length; i++)
            {
                var ps = wake.Particles[i];
                if (ps == null) continue;
                if (moving) { if (!ps.isPlaying) ps.Play(true); }
                else if (ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        const float WakeSpeedSqr = 0.35f;
        const float WakeStopSpeedSqr = 0.12f;
        const float WakeJumpSqr = 4f * 4f; // no ship moves 4 m in a frame
        readonly Dictionary<TrailRenderer, float> _trailWidths = new Dictionary<TrailRenderer, float>();

        void ScaleTrails(Transform view, Wake wake)
        {
            var scale = Mathf.Abs(view.lossyScale.x);
            for (var i = 0; i < wake.Trails.Length; i++)
            {
                var trail = wake.Trails[i];
                if (trail == null) continue;
                if (!_trailWidths.TryGetValue(trail, out var width)) _trailWidths[trail] = width = trail.widthMultiplier; // the model's own
                trail.widthMultiplier = width * scale;
                trail.Clear();
            }
        }

        static void ClearWake(Wake wake)
        {
            for (var i = 0; i < wake.Trails.Length; i++) if (wake.Trails[i] != null) wake.Trails[i].Clear();
            for (var i = 0; i < wake.Particles.Length; i++) if (wake.Particles[i] != null) wake.Particles[i].Clear(true);
        }

        sealed class Wake
        {
            public GameObject[] Roots;
            public TrailRenderer[] Trails;
            public ParticleSystem[] Particles;
            /// <summary>-1 unknown (a fresh or reused view), 0 still, 1 moving.</summary>
            public int State = -1;
            public Vector3 LastPosition;
        }

        readonly Dictionary<Transform, Wake> _wakes = new Dictionary<Transform, Wake>();

        /// <summary>A pooled view starts a new life: its wake parts are found again on its first tick (an effect attached
        /// in its last life, e.g. a VFX-profile trail, may have moved to another view since).</summary>
        public void OnViewReused(Transform view)
        {
            if (view != null) _wakes.Remove(view);
        }

        static bool IsWakePart(string name) => !string.IsNullOrEmpty(name) && (name.Contains("Wake") || name.Contains("Foam"));

        static Wake FindWake(Transform view)
        {
            var roots = new List<GameObject>();
            foreach (var t in view.GetComponentsInChildren<Transform>(true))
                if (t != view && IsWakePart(t.name)) roots.Add(t.gameObject);
            var particles = new List<ParticleSystem>();
            foreach (var ps in view.GetComponentsInChildren<ParticleSystem>(true))
                if (IsWakePart(ps.gameObject.name)) particles.Add(ps);
            return new Wake { Roots = roots.ToArray(), Trails = view.GetComponentsInChildren<TrailRenderer>(true), Particles = particles.ToArray(), LastPosition = view.position };
        }
        public void SetBurning(Transform view, bool burning) => EnemyBurnFlash.SetBurning(view, burning);

        public void SetHealthFill(Transform healthBar, float fill)
        {
            var bar = healthBar != null ? healthBar.GetComponent<WorldHealthBar>() : null;
            if (bar != null) bar.SetFill(fill);
        }

        public void Dispose()
        {
            _tint.Clear();
            _wakes.Clear();
            _trailWidths.Clear();
            if (_cube != null) Object.Destroy(_cube);
            _cube = null;
        }

        Transform Acquire(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            var key = KeyFor(prefab);
            var go = _pools.Acquire(key, position, rotation);
            if (go == null) return null;
            if (_prepared.Add(go.GetInstanceID())) StripImmediate(go);
            // A reused model keeps the trail points of its last life; cleared now it is at its new place, the wake
            // does not draw a streak across the water from where the last enemy died.
            ClearMotion(go);
            OnViewReused(go.transform);
            _liveKey[go.transform] = key;
            return go.transform;
        }

        /// <summary>Drops the model's trail points and particles (emission stays as it was; the wake tick drives it).</summary>
        void ClearMotion(GameObject go)
        {
            var id = go.GetInstanceID();
            if (!_motion.TryGetValue(id, out var motion))
            {
                motion = (go.GetComponentsInChildren<TrailRenderer>(true), go.GetComponentsInChildren<ParticleSystem>(true));
                _motion[id] = motion;
            }
            for (var i = 0; i < motion.trails.Length; i++)
                if (motion.trails[i] != null) motion.trails[i].Clear();
            for (var i = 0; i < motion.particles.Length; i++)
                if (motion.particles[i] != null) motion.particles[i].Clear(false);
        }

        /// <summary>The catalog's pool for the model, or a pool made on first use (a model the timeline cannot reach).</summary>
        string KeyFor(GameObject prefab)
        {
            if (_keys.TryGetValue(prefab, out var key)) return key;
            key = PoolCatalog.EnemyKeyPrefix + prefab.name;
            if (!_pools.Has(key)) _pools.Register(new PoolDefinition(key, prefab, 0, 0, ConfigEnums.PoolOverflowPolicy.Grow, "enemy model (on demand)"));
            _keys[prefab] = key;
            return key;
        }

        void Register(GameObject prefab, int prewarm, string source)
        {
            var key = PoolCatalog.EnemyKeyPrefix + prefab.name;
            if (!_pools.Has(key))
            {
                _pools.Register(new PoolDefinition(key, prefab, prewarm, 0, ConfigEnums.PoolOverflowPolicy.Grow, source));
                _pools.Prewarm(key);
            }
            _keys[prefab] = key;
        }

        /// <summary>V3's StripPhysics and StripNavMeshComponentsImmediate, done at once (not at the end of the frame).</summary>
        static void StripImmediate(GameObject root)
        {
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var b in root.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(b);
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                var typeName = component.GetType().Name;
                if (typeName == "NavMeshAgent" || typeName == "NavMeshObstacle") Object.DestroyImmediate(component);
            }
        }
    }
}
