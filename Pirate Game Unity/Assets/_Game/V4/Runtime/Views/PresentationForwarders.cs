using System;
using HighSeas.Core;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Progression;
using UnityEngine;

namespace RoguePirates.V4.Views
{
    /// <summary>
    /// The simulation's view sinks (M14.9b; were V3's ProjectileViewPool, PickupViewPool, V3PlaceholderFactory powerups,
    /// SaltwindCombatEffects and JuiceVfxBroker's splash). The modules are built before presentation exists and keep
    /// their sink from construction, so each forwarder hands every call to the presenter the presentation root registers
    /// at that moment, if any. Without one (tests, batch runs, after the presenter is destroyed) nothing is drawn and
    /// Acquire returns null, which the modules already treat as "no view".
    /// </summary>
    static class ForwardTarget
    {
        public static T Live<T>(Func<T> target, object self) where T : class
        {
            var t = target?.Invoke();
            if (t == null || ReferenceEquals(t, self)) return null;
            // A destroyed presenter still exists as a C# object; Unity's null check says it is gone.
            if (t is UnityEngine.Object unityObject && unityObject == null) return null;
            return t;
        }
    }

    /// <summary>Enemy-caused blasts (bomb boats, mines, Kraken traps, the boss).</summary>
    public sealed class EnemyEffectsForwarder : IEnemyEffects
    {
        readonly Func<IEnemyEffects> _target;
        public EnemyEffectsForwarder(Func<IEnemyEffects> target) { _target = target; }
        public void PlayExplosion(Vector3 position, float blastRadius) => ForwardTarget.Live(_target, this)?.PlayExplosion(position, blastRadius);
    }

    /// <summary>Enemy cannonball models and their water splash.</summary>
    public sealed class HostileShotViewsForwarder : IHostileShotViews
    {
        readonly Func<IHostileShotViews> _target;
        public HostileShotViewsForwarder(Func<IHostileShotViews> target) { _target = target; }
        public Transform Acquire(Vector3 position, Color color, float visualRadius) => ForwardTarget.Live(_target, this)?.Acquire(position, color, visualRadius);
        public void Release(Transform view) { if (view != null) ForwardTarget.Live(_target, this)?.Release(view); }
        public void Splash(Vector3 position) => ForwardTarget.Live(_target, this)?.Splash(position);
    }

    /// <summary>Pickup models (XP crystals, coins, barrels, flying health).</summary>
    public sealed class PickupViewsForwarder : IPickupViews
    {
        readonly Func<IPickupViews> _target;
        public PickupViewsForwarder(Func<IPickupViews> target) { _target = target; }
        IPickupViews T => ForwardTarget.Live(_target, this);
        public object Acquire(PickupType type, Vector3 position, int value) => T?.Acquire(type, position, value);
        public Vector3 AlignBarrel(object view)
        {
            var t = T;
            if (t != null) return t.AlignBarrel(view);
            return view is Transform tr && tr != null ? tr.position : Vector3.zero;
        }
        public void BeginHoming(object view, PickupType type) => T?.BeginHoming(view, type);
        public void Update(object view, PickupType type, Vector3 position, bool homing, float dt) => T?.Update(view, type, position, homing, dt);
        public void Release(object view, PickupType type, int value) { if (view != null) T?.Release(view, type, value); }
    }

    /// <summary>Floor powerup models.</summary>
    public sealed class PowerupViewsForwarder : IPowerupViews
    {
        readonly Func<IPowerupViews> _target;
        public PowerupViewsForwarder(Func<IPowerupViews> target) { _target = target; }
        public object Create(PowerupKind kind, Vector3 position) => ForwardTarget.Live(_target, this)?.Create(kind, position);
        public void Move(object view, Vector3 position) { if (view != null) ForwardTarget.Live(_target, this)?.Move(view, position); }
        public void Destroy(object view) { if (view != null) ForwardTarget.Live(_target, this)?.Destroy(view); }
    }
}
