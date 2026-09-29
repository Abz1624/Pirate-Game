using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Player;

namespace RoguePirates.V4.Foundation.Combat
{
    public static class PlayerHullSync
    {
        public static void SyncMaxFromRunStats(PlayerShipController player, bool preserveRatio = true, float healFlat = 0f)
        {
            if (player?.Damage == null || RunManager.Instance == null)
                return;

            player.Damage.AdjustMaxHP(RunManager.Instance.Stats.MaxHull, preserveRatio);
            if (healFlat > 0f)
                player.Damage.Heal(healFlat);
        }
    }
}
