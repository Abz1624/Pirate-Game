namespace RoguePirates.Simulation.Balance
{
    public enum SimInputPolicy
    {
        Forward,
        Orbit,
        Kite
    }

    public sealed class CombatExperimentSimConfig
    {
        public string ProfileId = "D_FULL_COMBAT_V2";
        public int WeaponSlotCap = 6;
        public bool DirectorV2Enabled = true;
        public bool DirectionalTargetingEnabled = true;
        public float SpawnDensityMultiplier = 1f;
        public float EnemyHpMultiplier = 1f;
        public float EnemyDamageMultiplier = 1f;

        public static CombatExperimentSimConfig FromProfileId(string profileId) =>
            new()
            {
                ProfileId = "D_FULL_COMBAT_V2",
                WeaponSlotCap = 6,
                DirectorV2Enabled = true,
                DirectionalTargetingEnabled = true
            };

        public float WeaponDamageMultiplier =>
            1f + (WeaponSlotCap - 1) * 0.14f + (DirectionalTargetingEnabled ? 0.05f : 0f);
    }
}
