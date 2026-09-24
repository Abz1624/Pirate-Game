using HighSeas.Core;
using HighSeas.Player;

namespace HighSeas.Combat
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
