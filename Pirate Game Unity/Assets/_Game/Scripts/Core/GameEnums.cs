namespace HighSeas.Core
{
    public enum RunState
    {
        Boot,
        MainMenu,
        RunSetup,
        StageIntro,
        Playing,
        LevelUpPaused,
        POIChoicePaused,
        BossFight,
        BossDefeated,
        StageReward,
        PlayerDead,
        Paused,
        RunComplete
    }

    public enum PauseReason
    {
        None,
        LevelUp,
        POIChoice,
        UserPause,
        BossIntro
    }

    public enum Rarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4
    }

    public enum WeaponPattern
    {
        AutoProjectile,
        FanVolley,
        Orbit,
        TrailZone,
        ChainLightning,
        MineDrop,
        AuraPulse
    }

    public enum TargetingMode
    {
        Nearest,
        NearestElite,
        DensestCluster,
        RandomNearby,
        PlayerTrail,
        AreaAroundShip
    }

    public enum UpgradeAxis
    {
        Damage,
        Cooldown,
        Quantity,
        Speed,
        Size,
        Duration,
        Knockback,
        Crit,
        CritDamage,
        Pierce
    }

    public enum EnemyCategory
    {
        Ship,
        Creature
    }

    public enum EnemyBehavior
    {
        DirectChaser,
        FlankingChaser,
        TelegraphedCharge,
        MaintainRange,
        SuicideApproach,
        TankChaser,
        StrafeBurst,
        FormationRanged,
        FastSwarm,
        DiveLunge,
        DriftPulse,
        StationaryTurret,
        PhaseDash,
        TentacleZone,
        SupportBuff
    }

    public enum EliteAffix
    {
        None,
        Armored,
        Swift,
        Volatile,
        Rich,
        Cursed,
        Captain
    }

    public enum StatusType
    {
        Burn,
        Tarred,
        Breach,
        Shock,
        Curse
    }

    public enum PickupType
    {
        Infamy,
        Gold,
        GoldBarrel,
        Powerup,
        FlyingGold,
        FlyingHealth
    }

    public enum POIType
    {
        SalvageChest,
        LighthouseOfFortune,
        TrialBuoy,
        BossBeacon,
        GildedWreck,
        MerchantSkiff,
        CursedIdol,
        ExitMaelstrom
    }

    public enum TriggerEvent
    {
        OnHit,
        OnKill,
        OnDamageTaken,
        OnChestOpen,
        OnBossKill,
        OnLevelUp,
        OnStatusApplied
    }

    public enum StatId
    {
        Hull,
        HullRegen,
        Shield,
        Armor,
        Evasion,
        Damage,
        AttackSpeed,
        CritChance,
        CritDamage,
        Quantity,
        Size,
        Duration,
        ProjectileSpeed,
        Knockback,
        PickupRadius,
        Luck,
        XPGain,
        GoldGain,
        DoubloonGain,
        EliteDamage,
        MoveSpeed
    }

    public enum QualityTier
    {
        Low,
        Mid,
        High
    }
}
