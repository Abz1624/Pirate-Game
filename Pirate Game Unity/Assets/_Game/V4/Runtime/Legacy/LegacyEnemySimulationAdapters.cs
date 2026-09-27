using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.VFX;
using RoguePirates.V4.Enemies;
using UnityEngine;

namespace RoguePirates.V4.Legacy
{
    /// <summary>Run values the V4 enemy simulation reads at spawn (RunManager, V3RunContext).</summary>
    [DeletedInPhase(14, "V4 run clock")]
    public sealed class LegacyEnemyRunState : IEnemyRunState
    {
        static RunManager Run => RunManager.Instance;
        public bool Exists => Run != null;
        public float ElapsedSeconds => Run != null ? Run.ElapsedSeconds : 0f;
        public int Tier => Run != null ? Run.Tier : 1;
        public int StageIndex => Run != null ? Run.StageIndex : 0;
        public float EnemyVisualScale => HighSeas.V3.V3RunContext.IsActive ? HighSeas.V3.V3RunContext.EnemyVisualScale : 1f;
    }

    /// <summary>
    /// V3's enemy views for the V4 simulation: <c>EnemyViewPool</c> under the given root (the V3 scene's EnemyWorld had
    /// no enemy root, so its views sat under /Systems), the placeholder cube, spawn animation variation, health bars
    /// and the burn flash, made at the same moments as V3 (the simulation depends on it, see <see cref="IEnemyViewSink"/>).
    /// </summary>
    [DeletedInPhase(14, "V4 enemy view presenter")]
    public sealed class LegacyEnemyViewSink : IEnemyViewSink
    {
        readonly Transform _root;
        readonly EnemyViewPool _pool;
        readonly DemoJuiceSettings _juice;

        public LegacyEnemyViewSink(Transform root, DemoJuiceSettings juice = null)
        {
            _root = root;
            _pool = new EnemyViewPool(root);
            _juice = juice != null ? juice : Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
        }

        public void Prepare(EnemyDefinition[] definitions)
        {
            for (var i = 0; i < definitions.Length; i++)
                EnemyViewPool.StripNavMeshFromPrefab(definitions[i]?.visualPrefab);
        }

        public float FallbackVisualScale(EnemyDefinition def) => EnemyVisualFallbackResolver.ResolveVisualScale(def);

        public Transform CreateView(EnemyDefinition def, Vector3 position, float scale, bool elite, int animationSeed)
        {
            var prefab = def.visualPrefab != null ? def.visualPrefab : EnemyVisualFallbackResolver.ResolvePrefab(def);
            if (prefab != null)
            {
                var yaw = Quaternion.Euler(0f, def.visualYawOffset, 0f);
                var view = _pool.Acquire(prefab, position, yaw, scale);
                EnemyViewAnimation.ApplySpawnVariation(view, animationSeed);
                return view;
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = string.IsNullOrEmpty(def.displayName) ? def.id : def.displayName;
            go.transform.SetParent(_root, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * (def.radius * 2f * scale);
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
                rend.material.color = elite ? Color.magenta : def.placeholderColor;
            return go.transform;
        }

        public Transform AttachHealthBar(EnemyDefinition def, Transform view, bool elite)
        {
            if (_juice?.enemyHealthBarPrefab == null || view == null) return null;
            var barGo = Object.Instantiate(_juice.enemyHealthBarPrefab);
            var bar = barGo.GetComponent<WorldHealthBar>();
            if (bar == null)
                bar = barGo.AddComponent<WorldHealthBar>();
            bar.Bind(view, _juice.enemyHealthBarHeight);
            bar.SetFill(1f);
            return barGo.transform;
        }

        public void Release(EnemyDefinition def, Transform view, Transform healthBar)
        {
            var releasePrefab = def.visualPrefab != null ? def.visualPrefab : EnemyVisualFallbackResolver.ResolvePrefab(def);
            EnemyBurnFlash.SetBurning(view, false);
            if (view != null)
            {
                if (releasePrefab != null)
                    _pool.Release(view, releasePrefab);
                else
                    Object.Destroy(view.gameObject);
            }
            if (healthBar != null) Object.Destroy(healthBar.gameObject);
        }

        public void TickSpring(Transform view, ref bool springing, bool isSpringing) => EnemyViewAnimation.TickSwordfish(view, ref springing, isSpringing);
        public void TickWake(Transform view, EnemyCategory category, Vector3 velocity) => EnemyShipWakeJuice.Tick(view, category, velocity);
        public void SetBurning(Transform view, bool burning) => EnemyBurnFlash.SetBurning(view, burning);

        public void SetHealthFill(Transform healthBar, float fill)
        {
            var bar = healthBar != null ? healthBar.GetComponent<WorldHealthBar>() : null;
            bar?.SetFill(fill);
        }
    }

    /// <summary>
    /// Spawn movement/attack parameters from V3's resolver over the combat-profile registry (which
    /// <see cref="LegacyCombatProfileOverride"/> fills from the archetype combat blocks). Kept through M8 to keep the
    /// port's size down: the resolver's own enemy-id rules stay in legacy code until it is ported.
    /// </summary>
    [DeletedInPhase(14, "V4 spawn parameter resolver")]
    public sealed class LegacyEnemySpawnParams : IEnemySpawnParams
    {
        public bool CombatEnabled => SaltwindCombatFeature.IsEnabled;
        public bool HasCombatProfile(EnemyDefinition def) => def != null && EnemyCombatProfileRegistry.TryGet(def.id, out _);
        public EnemySpawnRuntimeParams ResolveForDefinition(EnemyDefinition def) => SpawnProfileResolver.ResolveForDefinition(def);
        public EnemySpawnRuntimeParams Resolve(SpawnProfile profile, EnemyDefinition def, int profileInstanceId, FormationRole role, int formationSlot, int leaderUnitId) =>
            SpawnProfileResolver.Resolve(profile, def, profileInstanceId, role, formationSlot, leaderUnitId);
    }
}
