using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.VFX;
using RoguePirates.V4.Enemies;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// Test helper (M14.10; was LegacyEnemyViewSink): V3's enemy views for the V4 simulation, kept so the lockstep tests
    /// compare the V4 simulation with V3's EnemyWorld under the same view behaviour (V3's pool and its R-81 colliders).
    /// V3's enemy views for the V4 simulation: <c>EnemyViewPool</c> under the given root (the V3 scene's EnemyWorld had
    /// no enemy root, so its views sat under /Systems), the placeholder cube, spawn animation variation, health bars
    /// and the burn flash, made at the same moments as V3 (the simulation depends on it, see <see cref="IEnemyViewSink"/>).
    /// </summary>
    public sealed class V3EnemyViewSink : IEnemyViewSink
    {
        readonly Transform _root;
        readonly EnemyViewPool _pool;
        readonly DemoJuiceSettings _juice;

        public V3EnemyViewSink(Transform root, DemoJuiceSettings juice = null)
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

}
