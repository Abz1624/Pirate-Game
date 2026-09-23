using HighSeas.Core;
using HighSeas.Player;

namespace HighSeas.Combat
{
    public static class PlayerHullSync
    {
        public static void SyncMaxFromRunStats(PlayerShipController player, bool preserveRatio = true)
        {
            if (player?.Damage == null || RunManager.Instance == null)
                return;

            player.Damage.AdjustMaxHP(RunManager.Instance.Stats.MaxHull, preserveRatio);
        }
    }
}
