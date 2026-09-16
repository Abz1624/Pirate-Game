namespace RoguePirates.Simulation
{
    public static class SimConstants
    {
        public const int TickHz = 30;
        public const float TickDurationSeconds = 1f / TickHz;
        public const float DefaultMoveSpeed = 6.2f;
        public const float DefaultHull = 100f;
        public const float StageDurationSeconds = 600f;
        public const float StageBaseThreat = 2.6f;
        public const float MaxGlobalDamageMultiplier = 11f;
        public const float ContactIFrameSeconds = 0.20f;
        public const float EnemyContactCooldownSeconds = 0.8f;
        public const float DeckCannonBaseDamage = 12f;
        public const float DeckCannonCooldownSeconds = 0.85f;
        public const float SharkBaseDamage = 8f;
        public const float SharkBaseHp = 35f;
        public const float SharkMoveSpeed = 4.5f;
        public const float PlayerRadiusMeters = 1.2f;
        public const float EnemyRadiusMeters = 0.9f;
        public const float ProjectileRadiusMeters = 0.25f;
        public const float ProjectileSpeed = 18f;
        public const int DefaultWeaponSlots = 6;
        public const int DefaultLogbookSlots = 2;

        public static readonly float[] RarityBaseWeights = { 70f, 22.5f, 6f, 1.35f, 0.15f };
    }
}
