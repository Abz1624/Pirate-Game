using System;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using UnityEngine;

namespace RoguePirates.V4.Enemies
{
    /// <summary>
    /// The enemy simulation's model sink (M14.10; was LegacyEnemyViewSink over V3's EnemyViewPool rooted at /Systems):
    /// every call goes to the enemy view presenter the presentation root registers, if any; without one (tests, batch
    /// runs) enemies simply have no model. The fallback model scale is not presentation — it sets the gameplay radius of
    /// an enemy without a model — so it is answered here, the same with or without a presenter.
    /// <para>Intended change 2: the models no longer come from V3's pool, which switched /Systems off and on around each
    /// new model and left the new model's island colliders in place until the end of the frame (R-72, R-81), where the
    /// simulation's island checks could see them.</para>
    /// </summary>
    public sealed class EnemyViewForwarder : IEnemyViewSink
    {
        readonly Func<IEnemyViewSink> _target;

        public EnemyViewForwarder(Func<IEnemyViewSink> target) { _target = target; }

        IEnemyViewSink T
        {
            get
            {
                var t = _target?.Invoke();
                if (t == null || ReferenceEquals(t, this)) return null;
                if (t is UnityEngine.Object unityObject && unityObject == null) return null;
                return t;
            }
        }

        public void Prepare(EnemyDefinition[] definitions) => T?.Prepare(definitions);

        /// <summary>V3 EnemyVisualFallbackResolver.ResolveVisualScale (the proxy model's scale; it also sets the radius).</summary>
        public float FallbackVisualScale(EnemyDefinition def) => EnemyVisualFallbackResolver.ResolveVisualScale(def);

        public Transform CreateView(EnemyDefinition def, Vector3 position, float scale, bool elite, int animationSeed) =>
            T?.CreateView(def, position, scale, elite, animationSeed);

        public Transform AttachHealthBar(EnemyDefinition def, Transform view, bool elite) => view != null ? T?.AttachHealthBar(def, view, elite) : null;

        public void Release(EnemyDefinition def, Transform view, Transform healthBar)
        {
            if (view == null && healthBar == null) return;
            T?.Release(def, view, healthBar);
        }

        public void TickSpring(Transform view, ref bool springing, bool isSpringing)
        {
            var t = T;
            if (t != null && view != null) t.TickSpring(view, ref springing, isSpringing);
        }

        public void TickWake(Transform view, EnemyCategory category, Vector3 velocity) { if (view != null) T?.TickWake(view, category, velocity); }
        public void SetBurning(Transform view, bool burning) { if (view != null) T?.SetBurning(view, burning); }
        public void SetHealthFill(Transform healthBar, float fill) { if (healthBar != null) T?.SetHealthFill(healthBar, fill); }
    }
}
