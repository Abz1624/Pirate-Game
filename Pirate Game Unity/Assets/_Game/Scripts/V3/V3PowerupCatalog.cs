using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;

namespace HighSeas.V3
{
    public static class V3PowerupCatalog
    {
        public static V3FloorPowerupKind RollKind(RunRng rng, bool expandedPool)
        {
            if (StageBalanceProfileContext.IsActive)
                return RollSaltwindKind(rng);

            if (!expandedPool)
                return V3FloorPowerupWorld.RollKind(rng);

            var roll = rng.PowerupFloat() * 100f;
            if (roll < 20f) return V3FloorPowerupKind.RepairSupplies;
            if (roll < 35f) return V3FloorPowerupKind.BattleSignal;
            if (roll < 48f) return V3FloorPowerupKind.Tailwind;
            if (roll < 58f) return V3FloorPowerupKind.SalvageChime;
            if (roll < 70f) return V3FloorPowerupKind.FullSail;
            if (roll < 80f) return V3FloorPowerupKind.PowderRush;
            if (roll < 90f) return V3FloorPowerupKind.MagneticTide;
            return V3FloorPowerupKind.DeadCalm;
        }

        public static V3FloorPowerupKind RollSaltwindKind(RunRng rng)
        {
            var roll = rng.PowerupFloat() * 100f;
            if (roll < 25f) return V3FloorPowerupKind.RepairSupplies;
            if (roll < 50f) return V3FloorPowerupKind.PowderRush;
            if (roll < 75f) return V3FloorPowerupKind.MagneticTide;
            return V3FloorPowerupKind.DeadCalm;
        }

        public static bool UseExpandedPool() =>
            !StageBalanceProfileContext.IsActive &&
            CombatExperimentContext.Active != null &&
            CombatExperimentContext.Active.encounterSpawnEnabled;
    }
}
