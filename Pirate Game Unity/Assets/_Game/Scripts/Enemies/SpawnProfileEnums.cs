namespace HighSeas.Enemies
{
    public enum SpawnMode
    {
        Single = 0,
        Sequence = 1,
        Burst = 2,
        Group = 3,
        Continuous = 4,
        TimedWave = 5,
        Triggered = 6
    }

    public enum SpawnGroupLayout
    {
        Cluster = 0,
        Ring = 1,
        Line = 2,
        Arc = 3,
        FlankPair = 4,
        Scatter = 5
    }

    public enum SpawnFallbackPolicy
    {
        Skip = 0,
        RetryLater = 1
    }

    public enum EnemyMovementPattern
    {
        UseDefinition = 0,
        DirectChase = 1,
        Intercept = 2,
        Flank = 3,
        Orbit = 4,
        MaintainRange = 5,
        Strafe = 6,
        TelegraphedCharge = 7,
        DiveLunge = 8,
        Swarm = 9,
        Drift = 10,
        Stationary = 11
    }

    public enum EnemyAttackPattern
    {
        UseDefinition = 0,
        Contact = 1,
        SingleShot = 2,
        BurstVolley = 3,
        SpreadVolley = 4,
        TelegraphedCharge = 5,
        AreaDenial = 6,
        SuicideExplosion = 7,
        None = 8
    }

    public enum EnemySpecialBehavior
    {
        None = 0,
        Formation = 1,
        Summon = 2,
        TetherBeam = 3,
        SupportAura = 4,
        PhaseDash = 5,
        TerrainAmbush = 6,
        Adaptive = 7
    }

    public enum FormationRole
    {
        Auto = 0,
        Leader = 1,
        Follower = 2
    }

    public enum FormationShape
    {
        Line = 0,
        V = 1,
        Wedge = 2,
        Box = 3
    }

    public enum PhaseDashAxis
    {
        TowardPlayer = 0,
        Flank = 1,
        AwayThenReengage = 2
    }

    public enum TerrainAmbushTrigger
    {
        OnSpawn = 0,
        WhenPlayerEntersRadius = 1,
        WhenOffScreenThenReveal = 2
    }

    public enum AdaptiveSignal
    {
        PlayerKiteTime = 0,
        PlayerStandStill = 1,
        HighPlayerDps = 2,
        LowPlayerHp = 3,
        ManyActiveEnemies = 4
    }
}
