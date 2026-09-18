namespace RoguePirates.Simulation.Balance
{
    public enum SimInputPolicy
    {
        Forward,
        Orbit
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

        public static CombatExperimentSimConfig FromProfileId(string profileId)
        {
            var config = new CombatExperimentSimConfig { ProfileId = profileId ?? "D_FULL_COMBAT_V2" };
            switch (profileId)
            {
                case "A_CURRENT":
                    config.WeaponSlotCap = 2;
                    config.DirectorV2Enabled = false;
                    config.DirectionalTargetingEnabled = false;
                    break;
                case "B_SIX_ONLY":
                    config.WeaponSlotCap = 6;
                    config.DirectorV2Enabled = false;
                    config.DirectionalTargetingEnabled = false;
                    break;
                case "C_SIX_DIRECTIONAL":
                    config.WeaponSlotCap = 6;
                    config.DirectorV2Enabled = false;
                    config.DirectionalTargetingEnabled = true;
                    break;
                default:
                    config.WeaponSlotCap = 6;
                    config.DirectorV2Enabled = true;
                    config.DirectionalTargetingEnabled = true;
                    break;
            }
            return config;
        }

        public float WeaponDamageMultiplier =>
            1f + (WeaponSlotCap - 1) * 0.14f + (DirectionalTargetingEnabled ? 0.05f : 0f);
    }
}
