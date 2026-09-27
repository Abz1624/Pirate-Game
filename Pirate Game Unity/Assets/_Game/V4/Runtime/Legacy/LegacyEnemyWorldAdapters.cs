using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Encounters;
using HighSeas.Player;
using HighSeas.VFX;
using HighSeas.Weapons;
using RoguePirates.V4.Enemies;
using UnityEngine;

namespace RoguePirates.V4.Legacy
{
    /// <summary>The player ship for the V4 enemy world and its hostile shots (the kept PlayerShipController / DamageReceiver).</summary>
    [DeletedInPhase(14, "V4 ship")]
    public sealed class LegacyEnemyTarget : IEnemyTarget, IHostileShotTarget
    {
        readonly System.Func<PlayerShipController> _find;
        PlayerShipController _ship;

        public LegacyEnemyTarget(PlayerShipController ship) { _ship = ship; }
        /// <summary>The run's ship, looked up when first needed (the shell finds it during bootstrap).</summary>
        public LegacyEnemyTarget(System.Func<PlayerShipController> find) { _find = find; }

        PlayerShipController ShipNow => _ship != null ? _ship : (_ship = _find?.Invoke());
        public PlayerShipController Ship => ShipNow;
        public bool Exists => ShipNow != null;
        public Vector3 Position => ShipNow != null ? ShipNow.Position : Vector3.zero;

        public Vector3 Velocity
        {
            get
            {
                if (ShipNow == null) return Vector3.zero;
                var v = ShipNow.Velocity;
                v.y = 0f;
                return v;
            }
        }

        public float ContactRadius => ShipNow != null ? ShipNow.IslandCollisionRadius : 1.15f;
        public bool HasDamageReceiver => ShipNow != null && ShipNow.Damage != null;
        public bool IsAlive => HasDamageReceiver && ShipNow.Damage.IsAlive;
        public Vector3 HullPosition => HasDamageReceiver ? ShipNow.Damage.transform.position : Position;
        public void TakeDamage(float amount, Vector3 source, bool isCrit) { if (HasDamageReceiver) ShipNow.Damage.TakeDamage(amount, source, isCrit); }
        public void ApplySlow(float speedMultiplier, float seconds) { if (ShipNow != null) ShipNow.ApplySlow(speedMultiplier, seconds); }
    }

    /// <summary>Enemy explosions: V3's SaltwindCombatEffects (the weapon juice controller's mine blast; visual only).</summary>
    [DeletedInPhase(14, "V4 enemy effect presenter")]
    public sealed class LegacyEnemyEffects : IEnemyEffects
    {
        public void PlayExplosion(Vector3 position, float blastRadius) => SaltwindCombatEffects.PlayExplosion(position, blastRadius);
    }

    /// <summary>Hostile shot models (V3's ProjectileViewPool, parented where V3's HostileProjectileWorld sat) and the water splash.</summary>
    [DeletedInPhase(14, "V4 projectile presenter")]
    public sealed class LegacyHostileShotViews : IHostileShotViews
    {
        readonly ProjectileViewPool _pool;

        public LegacyHostileShotViews(Transform root)
        {
            _pool = new ProjectileViewPool(root, Resources.Load<DemoJuiceSettings>("DemoJuiceSettings"));
        }

        public Transform Acquire(Vector3 position, Color color, float visualRadius) => _pool.Acquire(position, color, visualRadius);
        public void Release(Transform view) => _pool.Release(view);
        public void Splash(Vector3 position) => JuiceVfxBroker.Instance?.PlayWaterSplash(position);
    }

    /// <summary>Hit feedback: V3's damage highlight, camera shake and hit sound, TriggerBus hit event and damage numbers.</summary>
    [DeletedInPhase(14, "V4 hit presenters")]
    public sealed class LegacyEnemyFeedback : IEnemyFeedback
    {
        public void DamageHighlight(Transform view) => JuiceVfxBroker.Instance?.PlayDamageHighlight(view);

        public void HitShakeAndSound(float damage)
        {
            CameraShakeService.Shake(Mathf.Min(0.08f, damage * 0.01f), 0.1f);
            HighSeas.Audio.SfxService.PlayPlaceholder("enemy_hit");
        }

        public void RaiseHit(float damage, bool isCrit, Vector3 position, string weaponId) => HighSeas.Meta.TriggerBus.Instance?.RaiseHit(damage, isCrit, position, weaponId);
        public void DamageNumber(Vector3 position, float damage, bool isCrit) => FloatingDamageNumbers.Instance?.ShowEnemyDamage(position, damage, isCrit);
    }

    /// <summary>
    /// V3's kill path (EnemyWorld.ApplyDamage), same calls in the same order. Each target leaves when its owner moves to
    /// V4: pickups, floor powerups and item counters in M9, the encounter tracker with the stage port, Marks and the damage
    /// ledger in M12, the TriggerBus kill event in M14. V3 also called POIManager.NotifyBossDefeated here; POIManager
    /// left the V4 scenes in M7 and the stage content hears the kill event instead (first subscriber).
    /// </summary>
    [DeletedInPhase(14, "V4 kill fan-out targets")]
    public sealed class LegacyEnemyKillTargets : IEnemyKillTargets
    {
        public void OnKilled(in EnemyUnit e, EnemyDefinition def)
        {
            RunManager.Instance?.RegisterKill(e.isBoss);
            var export = StageBalanceProfileContext.Export;
            var gold = def.goldValue;
            if (StageBalanceProfileContext.IsActive && export != null)
            {
                gold = SaltwindStageEconomyLogic.ResolveKillGoldDrop(
                    export,
                    def,
                    RunManager.Instance.Rng,
                    e.affix == EliteAffix.Rich,
                    e.isElite,
                    e.isBoss);
            }

            HighSeas.Pickups.PickupWorld.Instance?.SpawnFromEnemy(
                e.position,
                def.xpValue,
                gold,
                !StageBalanceProfileContext.IsActive && e.affix == EliteAffix.Rich);
            HighSeas.V3.V3FloorPowerupWorld.Instance?.TryRollOnKill(e.position, e.isBoss, e.isElite);
            RunEncounterTracker.RegisterAgentKill(e.id);
            if (StageBalanceProfileContext.IsActive && def != null)
                RunMarksTracker.TryCreditScheduledEliteKill(e.id, def.id);
            HighSeas.V3.V3MinibossArena.NotifyAgentKilled(e.id);
            HighSeas.Meta.TriggerBus.Instance?.RaiseKill(e.isElite, e.isBoss, e.position, e.lastHitWeaponId);
            HighSeas.V3.V3RunItemEffects.Instance?.NotifyKill();
            if (!string.IsNullOrEmpty(e.lastHitWeaponId))
                WeaponDamageLedger.RecordKill(e.lastHitWeaponId);
        }
    }

    /// <summary>Hellfire patch needs: RunManager, V3's damage roll on the combat stream, the evolution catalog, the oil effect.</summary>
    [DeletedInPhase(9, "Build & Progression (the damage roll and the evolution check)")]
    public sealed class LegacyHellfireEnvironment : IHellfireEnvironment
    {
        static RunManager Run => RunManager.Instance;
        public bool RunActive => Run != null && !Run.IsPaused && !Run.IsRunEnded;
        public float RollTickDamage(bool targetIsElite) => DamageSystem.RollDamage(4f, 1f, Run.Stats, targetIsElite, 0f, Run.Rng, 0f, "flamethrower");
        public bool IsHellfireWeapon(string weaponId) => WeaponEvolutionCatalog.GetBehaviorModifier(weaponId) == "hellfire_nozzle";

        /// <summary>
        /// No effect: V3's burning-oil prefab (played through the disabled WeaponJuiceController) renders with a broken
        /// pink material (R-104). The patches show as ground rings instead (<see cref="LegacyHazardTelegraphView.Patches"/>,
        /// M8 playtest).
        /// </summary>
        public void OilDropped(Vector3 position, float radius) { }
    }

    /// <summary>Explosive barrel settings and gate (the stage profile export) and V3's toast.</summary>
    [DeletedInPhase(14, "V4 stage config and toasts")]
    public sealed class LegacyBarrelEnvironment : IBarrelEnvironment
    {
        public bool RunActive => StageBalanceProfileContext.IsActive && RunManager.Instance != null && !RunManager.Instance.IsPaused && !RunManager.Instance.IsRunEnded;
        public float FuseSeconds => StageBalanceProfileContext.Export?.Exploration?.ExplosiveBarrelFuseSeconds ?? 1.2f;
        public float Radius => StageBalanceProfileContext.Export?.Exploration?.ExplosiveBarrelRadius ?? 4f;
        public int Damage => StageBalanceProfileContext.Export?.Exploration?.ExplosiveBarrelDamage ?? 20;
        public bool ShouldDetonate(float fuse) => SaltwindExplosiveBarrelLogic.ShouldDetonate(fuse);
        public void Toast(string text, float seconds) => HighSeas.V3.V3ToastHud.Instance?.Show(text, seconds);
    }
}
