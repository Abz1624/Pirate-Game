using HighSeas.Core;
using HighSeas.V3;
using RoguePirates.Simulation.Balance;

namespace HighSeas.Balance
{
    /// <summary>Marks unlock goal selection vs purchase (Step 11 / C11).</summary>
    public static class SaltwindMarksUnlockLogic
    {
        public static bool TrySetUnlockGoal(SaveService save, string contentId)
        {
            if (save == null || string.IsNullOrEmpty(contentId)) return false;
            save.SetSelectedUnlockGoal(contentId);
            return true;
        }

        public static bool TryPurchaseUnlock(SaveService save, StageBalanceProfileExport export, string contentId)
        {
            if (save == null || export == null || string.IsNullOrEmpty(contentId)) return false;
            if (save.HasMarksOwned(contentId)) return false;

            var cost = SaltwindStageSettlementLogic.TryGetContentUnlockCost(export, contentId);
            if (cost < 0) return false;
            return save.TryPurchaseMarksUnlock(contentId, cost);
        }

        public static string FormatSelectedGoalProgress(SaveService save, StageBalanceProfileExport export)
        {
            if (save == null || export == null) return string.Empty;
            var goal = save.Meta.selectedUnlockGoal;
            if (string.IsNullOrEmpty(goal))
                return "\nUnlock goal: not selected (choose one in menu or results).\n";

            if (save.HasMarksOwned(goal))
                return $"\nUnlock goal: {V3WeaponDisplayNames.GetWeaponName(goal)} — owned.\n";

            var cost = SaltwindStageSettlementLogic.TryGetContentUnlockCost(export, goal);
            if (cost < 0)
                return $"\nUnlock goal: {V3WeaponDisplayNames.GetWeaponName(goal)} — not a Marks unlock.\n";

            return
                $"\nUnlock goal: {V3WeaponDisplayNames.GetWeaponName(goal)} — {save.Meta.marks} / {cost} Marks\n";
        }

        public static string GetNextLockedMarksWeapon(StageBalanceProfileExport export, SaveService save)
        {
            if (export?.WeaponUnlocks == null || save == null) return null;
            for (var i = 0; i < export.WeaponUnlocks.Count; i++)
            {
                var entry = export.WeaponUnlocks[i];
                if (entry == null || string.IsNullOrEmpty(entry.ContentId)) continue;
                if (!save.HasMarksOwned(entry.ContentId))
                    return entry.ContentId;
            }

            return null;
        }
    }
}
