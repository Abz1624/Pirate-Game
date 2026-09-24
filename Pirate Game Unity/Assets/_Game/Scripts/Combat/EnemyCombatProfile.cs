using HighSeas.Enemies;

namespace HighSeas.Combat
{
    /// <summary>Authoritative combat identity for a spawned enemy definition (movement, attacks, timing).</summary>
    public sealed class EnemyCombatProfile
    {
        public string enemyId;
        public SaltwindCombatArchetype archetype;
        public EnemyMovementPattern movementPattern;
        public EnemyAttackPattern attackPattern;
        public float preferredRange;
        public float attackRange;
        public float attackWindUp;
        public float attackCooldown;
        public int projectileCount;
        public float spreadDegrees;
        public float projectileSpeed;
        public float chargeWindUp;
        public float chargeDuration;
        public float chargeCooldown;
        public float chargeSpeedMult;
        public bool contactDamageEnabled = true;
        public bool eliteHammerheadSecondCharge;
        public bool eliteKrakenSequentialTraps;
    }

    public enum SaltwindCombatArchetype
    {
        LegacyGeneric,
        DeckhandDinghy,
        Shark,
        Swordfish,
        CutthroatSkiff,
        Hammerhead,
        Cannonboat,
        MantaRay,
        NeedleSloop,
        RammerRaft,
        RamRaider,
        BombBoat,
        BombKetch,
        PowderBarge,
        BoardingBarge,
        NetCutter,
        RepairTender,
        ShieldFrigate,
        GunnerCog,
        TreasureRaider,
        KrakenSpawn,
        EliteHammerhead,
        EliteKrakenSpawn,
        ReefCrabMiniboss,
        DrownedPursuer,
        DrownedHarrier
    }

    public enum EnemyCombatPhase
    {
        Approach,
        Reposition,
        Telegraph,
        Execute,
        Recover,
        Flee,
        Support
    }
}
