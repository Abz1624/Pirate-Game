namespace HighSeas.V3
{
    /// <summary>Prototype toggles — flip off when moving to gated progression.</summary>
    public static class V3PlaytestFlags
    {
        public const bool UnlockAllContent = true;
        public const bool EnableRunPlaytestHotkeys = true;
        public const bool EnableSpawnDirectorDebugUi = true;
        public const bool EnableEnemyIdentityLabels = true;
        /// <summary>Skip weapon cooldown firing and cutlass orbit damage during behavior tests.</summary>
        public const bool DisableShipAutoFire = true;
    }
}
