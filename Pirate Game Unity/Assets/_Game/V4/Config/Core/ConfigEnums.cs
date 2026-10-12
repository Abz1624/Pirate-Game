using System;

namespace RoguePirates.V4.Config
{
    /// <summary>
    /// Enums used by V4 config data. The config assembly cannot reference the V3 runtime, so these mirror the V3
    /// enums with the same names and values (a test compares them); adapters convert by value.
    /// </summary>
    public static class ConfigEnums
    {
        public enum EnemyBehavior { DirectChaser, FlankingChaser, TelegraphedCharge, MaintainRange, SuicideApproach, TankChaser, StrafeBurst, FormationRanged, FastSwarm, DiveLunge, DriftPulse, StationaryTurret, PhaseDash, TentacleZone, SupportBuff }
        public enum EnemyCategory { Ship, Creature }
        /// <summary>What a view pool does when it is at its cap: create anyway (counted), reuse the oldest in use, or refuse.</summary>
        public enum PoolOverflowPolicy { Grow = 0, RecycleOldest = 1, Reject = 2 }
        public enum EnemyTier { Soldier = 0, Captain = 1, General = 2, Boss = 3 }
        public enum EliteAffix { None, Armored, Swift, Volatile, Rich, Cursed, Captain }
        public enum WeaponPattern { AutoProjectile, FanVolley, Orbit, TrailZone, ChainLightning, MineDrop, AuraPulse, PredictedAoE, SummonChase }
        public enum TargetingMode { Nearest, NearestElite, DensestCluster, RandomNearby, PlayerTrail, AreaAroundShip, ForwardArc, ForwardCone, AftDeploy, PortStarboard, OmniClusterGround, TetherTarget, TrailZone }
        /// <summary>V4 weapon firing logic (M5); a V3 pattern plus the id checks WeaponController made.</summary>
        /// <summary>Aim overlay shape (the O-key debug overlay, M6). ByTargeting = chosen from pattern and targeting as V3 did.</summary>
        public enum AimShape { ByTargeting = 0, PredictedArea = 1, TargetFan = 2, ForwardFan = 3, StrikeDisc = 4 }
        public enum WeaponBehavior { Projectile = 0, Fan = 1, Orbit = 2, ConeStream = 3, Trail = 4, Mine = 5, AreaStrike = 6, Aura = 7, PredictedArea = 8, Summon = 9, ChainedMace = 10 }
        /// <summary>What an evolution changes in its base weapon's behavior.</summary>
        /// <summary>The hit and kill effect a weapon's hits play (V3 CombatVfxController: cannon weapons a cannon impact, powder kegs nothing).</summary>
        public enum WeaponHitEffect { Standard = 0, CannonImpact = 1, None = 2 }
        public enum EvolutionEffect { None = 0, LaneWithBurst = 1, OppositeSideFollowUp = 2, WakeKegs = 3, PulsingOrbit = 4, WideConeWithPatches = 5, JumpAndDelayedStrike = 6, Empowered = 7 }
        // V5 M8z (7 Oct, D21): Slow (a slowing weapon's strength, the Squid Ink Pots) appended; the values are serialized.
        public enum UpgradeAxis { Damage, Cooldown, Quantity, Speed, Size, Duration, Knockback, Crit, CritDamage, Pierce, Slow }
        public enum SpawnMode { Single = 0, Sequence = 1, Burst = 2, Group = 3, Continuous = 4, TimedWave = 5, Triggered = 6 }
        public enum SpawnGroupLayout { Cluster = 0, Ring = 1, Line = 2, Arc = 3, FlankPair = 4, Scatter = 5 }
        public enum SpawnFallbackPolicy { Skip = 0, RetryLater = 1 }
        public enum EnemyMovementPattern { UseDefinition = 0, DirectChase = 1, Intercept = 2, Flank = 3, Orbit = 4, MaintainRange = 5, Strafe = 6, TelegraphedCharge = 7, DiveLunge = 8, Swarm = 9, Drift = 10, Stationary = 11 }
        public enum EnemyAttackPattern { UseDefinition = 0, Contact = 1, SingleShot = 2, BurstVolley = 3, SpreadVolley = 4, TelegraphedCharge = 5, AreaDenial = 6, SuicideExplosion = 7, None = 8 }
        public enum EnemySpecialBehavior { None = 0, Formation = 1, Summon = 2, TetherBeam = 3, SupportAura = 4, PhaseDash = 5, TerrainAmbush = 6, Adaptive = 7 }
        public enum FormationRole { Auto = 0, Leader = 1, Follower = 2 }
        public enum FormationShape { Line = 0, V = 1, Wedge = 2, Box = 3 }
        public enum PhaseDashAxis { TowardPlayer = 0, Flank = 1, AwayThenReengage = 2 }
        public enum TerrainAmbushTrigger { OnSpawn = 0, WhenPlayerEntersRadius = 1, WhenOffScreenThenReveal = 2 }
        public enum AdaptiveSignal { PlayerKiteTime = 0, PlayerStandStill = 1, HighPlayerDps = 2, LowPlayerHp = 3, ManyActiveEnemies = 4 }
        public enum SaltwindCombatArchetype { LegacyGeneric, DeckhandDinghy, Shark, Swordfish, CutthroatSkiff, Hammerhead, Cannonboat, MantaRay, NeedleSloop, RammerRaft, RamRaider, BombBoat, BombKetch, PowderBarge, BoardingBarge, NetCutter, RepairTender, ShieldFrigate, GunnerCog, TreasureRaider, KrakenSpawn, EliteHammerhead, EliteKrakenSpawn, ReefCrabMiniboss, DrownedPursuer, DrownedHarrier }
    }

    /// <summary>
    /// Marks a config field whose value is stored and validated now but only changes the game once the named V4
    /// phase replaces the legacy code that uses it. The console shows the tag next to the field.
    /// </summary>
    /// <remarks>Phase 0 means "takes effect when a run starts with it" (restart with a preset): values the legacy
    /// code caches at run start. The console shows "(restart)".</remarks>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class AppliesFromAttribute : Attribute
    {
        public int Phase { get; }
        public string Note { get; }
        public AppliesFromAttribute(int phase, string note = null) { Phase = phase; Note = note; }
    }

    /// <summary>Identity field (ids other data or legacy code refer to). Shown read-only in the console.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ConfigIdentityAttribute : Attribute { }
}
