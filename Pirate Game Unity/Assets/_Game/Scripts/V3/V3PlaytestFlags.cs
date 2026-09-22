namespace HighSeas.V3
{
    /// <summary>Prototype toggles — flip off when moving to gated progression.</summary>
    public static class V3PlaytestFlags
    {
        public const bool UnlockAllContent = true;
        /// <summary>When a stage balance profile is active, only this flag bypasses Marks gates.</summary>
        public const bool SaltwindDebugUnlockAll = false;
        public const bool EnableRunPlaytestHotkeys = true;
        public const bool EnableSpawnDirectorDebugUi = true;
        public const bool EnableEnemyIdentityLabels = true;
        /// <summary>Skip weapon cooldown firing during isolated behavior tests (not solo Saltwind).</summary>
        public const bool DisableShipAutoFire = true;

        public static bool ShouldDisableShipAutoFire =>
            DisableShipAutoFire && !Balance.StageBalanceProfileContext.IsActive;
    }
}
