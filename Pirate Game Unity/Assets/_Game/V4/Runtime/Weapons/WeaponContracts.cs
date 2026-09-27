using UnityEngine;

namespace RoguePirates.V4.Weapons
{
    /// <summary>Statuses weapons apply (V3 StatusType values the weapons use).</summary>
    public enum WeaponStatus { Burn, Shock, Curse, Tarred }

    /// <summary>
    /// Enemies as weapons see them, by slot index (the legacy EnemyWorld until M8, the V4 enemy world after). Indices
    /// are stable while an enemy lives; a freed slot can be reused by a later spawn.
    /// </summary>
    public interface IEnemyQuery
    {
        bool Exists { get; }
        int Capacity { get; }
        bool IsActive(int index);
        Vector3 Position(int index);
        Vector3 Velocity(int index);
        float Radius(int index);
        float Hp(int index);
        bool IsElite(int index);
        bool IsBoss(int index);
        bool TryGetNearest(Vector3 from, float range, out int index, bool preferElite = false);
    }

    /// <summary>Damage and statuses on enemies (V3 kill path until M8).</summary>
    public interface IEnemyDamageSink
    {
        void ApplyDamage(int index, float amount, bool isCrit, bool showHitFeedback = true, string weaponId = null);
        void ApplyStatus(int index, WeaponStatus status, float strength);
    }

    /// <summary>The player ship as weapons read it.</summary>
    public interface IShipState
    {
        bool Exists { get; }
        Vector3 Position { get; }
        Vector3 Velocity { get; }
        Vector3 MovementForward { get; }
        /// <summary>A point in the ship's local space in world space (orbit blades circle the ship's transform).</summary>
        Vector3 TransformPoint(Vector3 local);
    }

    /// <summary>
    /// Player stats and the captain/item multipliers weapons use. Read at each use, as V3 does: some change during a
    /// frame (gold for Golden Figurehead, a kill starting a timer).
    /// </summary>
    public interface IWeaponModifiers
    {
        float AttackSpeed { get; }
        float ProjectileSpeed { get; }
        float Size { get; }
        float Duration { get; }
        float Quantity { get; }
        float CritChance { get; }
        float CritDamage { get; }
        float EliteDamage { get; }
        float GlobalDamage { get; }
        /// <summary>Captain passive additive damage plus the active's damage buff.</summary>
        float CaptainAdditiveDamage { get; }
        float CaptainWeaponMultiplier(string weaponId);
        float ItemGlobalDamageMultiplier { get; }
        float ItemCritChanceBonus { get; }
        float ItemAttackSpeedMultiplier { get; }
        float FlameRangeMultiplier { get; }
        float BurnDamageMultiplier { get; }
        float PowderKegBlastRadiusMultiplier { get; }
        /// <summary>R-40: explosion damage (Black Powder Keg, Powder Fuse's charge); 1 unless those item effects are on.</summary>
        float ExplosionDamageMultiplier { get; }
        /// <summary>R-40: an explosion used Powder Fuse's charge.</summary>
        void ExplosionResolved();
        /// <summary>R-40: extra chain-lightning jumps (Stormglass); 0 unless the effect is on.</summary>
        int ExtraLightningJumps { get; }
    }

    /// <summary>The combat stream of the run's random numbers (crit rolls, boarding sides).</summary>
    public interface ICombatRandom
    {
        float CombatFloat();
    }

    /// <summary>Damage bookkeeping and legacy combat rules the weapons call into.</summary>
    public interface IWeaponCombatHooks
    {
        /// <summary>Resets the per-resolution proc counter (V3 CombatProcRuntime).</summary>
        void BeginDamageResolution();
        /// <summary>Counts a proc event; false once the resolution has used its budget (12).</summary>
        bool TryRegisterProcEvent();
        void RecordHit(string weaponId, float damage, bool isCrit, bool isElite, bool isBoss);
        void RecordEmission(string weaponId);
        void RecordConcurrentTargets(string weaponId, int count);
        void RecordControl(string weaponId, float seconds);
        void SpawnFlamePatch(Vector3 position, string weaponId);
        /// <summary>Ship-versus-ship radius damage (PvP modes only; nothing in solo).</summary>
        void ApplyShipRadiusDamage(Vector3 center, float radius, float damage, string weaponId);
        /// <summary>Aim at a rival ship when there is no enemy target (PvP modes only).</summary>
        bool TryResolveShipAim(string weaponId, out Vector3 direction);
        bool ShipVersusShip { get; }
        bool IsPvP { get; }
        /// <summary>A playtest flag that switches the ship's auto-fire off.</summary>
        bool AutoFireDisabled { get; }
    }

    /// <summary>Weapon effects (the legacy juice controller and aim overlay until V4 presenters replace them in M6).</summary>
    public interface IWeaponEffects
    {
        void Fired(string weaponId, Vector3 origin, Vector3 direction);
        void Aimed(string weaponId, Vector3 aimDirection, Vector3 targetPosition, bool hasTarget);
        void Beam(Vector3 origin, Vector3 direction, float range, float halfWidth);
        void SideSalvo(string weaponId, Vector3 shipPosition, Vector3 sideDirection);
        int MineDropped(Vector3 position);
        void MineRemoved(int handle);
        void Explosion(Vector3 position, float radius);
        void LightningStrike(Vector3 position, float radius);
        void FlameBurst(Vector3 origin, Vector3 aim);
        void AreaTelegraph(Vector3 position, float radius);
        /// <summary>A landing ring that fills over exactly <paramref name="seconds"/> (mortar, R-27 fix).</summary>
        void TimedRing(Vector3 position, float radius, float seconds);
        void AreaImpact(Vector3 position, float radius);
        void BurningPatch(Vector3 position, float radius);
        void WaterSplash(Vector3 position);
        int SkiffSpawned(Vector3 position, Vector3 forward);
        void SkiffMoved(int handle, Vector3 position, Vector3 facing);
        void SkiffRemoved(int handle);
    }

    /// <summary>Views for player projectiles (V4 view pool).</summary>
    public interface IProjectileViews
    {
        int Acquire(string weaponId, Vector3 position, Color color, float radius);
        void Move(int handle, Vector3 position, Vector3 velocity);
        void Release(int handle);
    }

    /// <summary>One equipped weapon as the loadout record holds it (the legacy level system writes it until M9).</summary>
    public struct LoadoutEntry
    {
        /// <summary>Identity of the record entry: a new object means a newly equipped (or replaced) weapon.</summary>
        public object Key;
        public string WeaponId;
        public string EvolutionId;
        public int Level;
        public float DamageBonus;
        public int QuantityBonus;
        public float CooldownMult, SpeedMult, SizeMult, DurationMult, KnockbackMult;
    }

    /// <summary>Runtime state the weapon module writes back so legacy readers (HUD, flame visuals) stay live.</summary>
    public struct LoadoutMirror
    {
        public float CooldownTimer;
        public bool AwaitingBurstShot;
        public float StreamRemaining, RechargeRemaining, StreamTickTimer;
    }

    public interface IWeaponLoadout
    {
        int Count { get; }
        LoadoutEntry Get(int index);
        void Mirror(int index, in LoadoutMirror state);
    }
}
