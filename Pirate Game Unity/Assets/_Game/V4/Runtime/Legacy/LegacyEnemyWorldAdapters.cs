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
        /// <summary>M9: the build's incoming-damage multiplier (V3's DamageReceiver read the Captain and item singletons).</summary>
        public System.Func<float> IncomingDamage;
        public void TakeDamage(float amount, Vector3 source, bool isCrit) { if (HasDamageReceiver) ShipNow.Damage.TakeDamage(amount * (IncomingDamage?.Invoke() ?? 1f), source, isCrit); }
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
        /// <summary>M9: the V4 build modules (pickups, floor powerups, items) in PickupWorld's, V3FloorPowerupWorld's and V3RunItemEffects' places.</summary>
        public LegacyBuildWiring Build;

        /// <summary>M12: publishes the Killed event (unit id, flags, position, run-clock time, weapon) — Rum's minute rows and
        /// scheduled elites read it. Flags: 1 elite, 2 boss, 4 pays ordinary rewards (adaptive elite).</summary>
        public System.Action<int, int, Vector3, float, string> Killed;

        public void OnKilled(in EnemyUnit e, EnemyDefinition def)
        {
            RunManager.Instance?.RegisterKill(e.isBoss);
            Killed?.Invoke(e.id, (e.isElite ? 1 : 0) | (e.isBoss ? 2 : 0) | (e.ordinaryRewards ? 4 : 0), e.position,
                RunManager.Instance != null ? RunManager.Instance.ElapsedSeconds : 0f, e.lastHitWeaponId);
            // M11: an adaptive elite marked "ordinary rewards" pays the unit's ordinary rewards (no Rich gold, elite payout,
            // forced powerup or elite-kill item procs); playtest round 1 made elite rewards the default (a DirectorConfig switch).
            var rewardElite = e.isElite && !e.ordinaryRewards;
            var rewardRich = e.affix == EliteAffix.Rich && !e.ordinaryRewards;
            var export = StageBalanceProfileContext.Export;
            var gold = def.goldValue;
            if (StageBalanceProfileContext.IsActive && export != null)
            {
                gold = SaltwindStageEconomyLogic.ResolveKillGoldDrop(
                    export,
                    def,
                    RunManager.Instance.Rng,
                    rewardRich,
                    rewardElite,
                    e.isBoss);
            }

            // (V3 doubled a Rich elite's gold only outside the stage profile, which V4 runs always use.)
            Build?.SpawnKillDrops(e.position, def.xpValue, gold);
            Build?.RollPowerup(e.position, e.isBoss, rewardElite);
            RunEncounterTracker.RegisterAgentKill(e.id);
            // (M12: scheduled-elite Rum reads the Killed event; the V3 Marks tracker is no longer fed.)
            HighSeas.V3.V3MinibossArena.NotifyAgentKilled(e.id);
            HighSeas.Meta.TriggerBus.Instance?.RaiseKill(rewardElite, e.isBoss, e.position, e.lastHitWeaponId);
            Build?.NotifyItemKill();
            if (!string.IsNullOrEmpty(e.lastHitWeaponId))
                WeaponDamageLedger.RecordKill(e.lastHitWeaponId);
        }
    }

    /// <summary>
    /// Hellfire patch needs: the run gate, the tick damage roll and the evolution check. M9: the roll is the V4 damage
    /// calculator (V3 DamageSystem.RollDamage, base 4 with the Flamethrower's id, on the combat stream) and the check
    /// reads the V4 progression's evolutions (V3 WeaponEvolutionRuntime); both are bound once the modules exist.
    /// </summary>
    [DeletedInPhase(14, "The run gate and the oil effect move to V4 with the run clock and presenters")]
    public sealed class LegacyHellfireEnvironment : IHellfireEnvironment
    {
        const string FlamethrowerId = "flamethrower";
        static RunManager Run => RunManager.Instance;
        public System.Func<RoguePirates.V4.Weapons.DamageCalculator> Calculator;
        public System.Func<string, bool> HasHellfire;
        /// <summary>R-40/R-49: the patch tick × this (status damage); 1 unless the switches are on.</summary>
        public System.Func<float> StatusDamage;
        public bool RunActive => Run != null && !Run.IsPaused && !Run.IsRunEnded;
        public float RollTickDamage(bool targetIsElite) => Calculator().RollAmount(4f, 1f, targetIsElite, 0f, 0f, FlamethrowerId) * (StatusDamage != null ? StatusDamage() : 1f);
        public bool IsHellfireWeapon(string weaponId) => HasHellfire != null && HasHellfire(weaponId);

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
