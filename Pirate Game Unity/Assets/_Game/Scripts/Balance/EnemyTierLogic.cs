using HighSeas.Core;
using HighSeas.Data;

namespace HighSeas.Balance
{
    public static class EnemyTierLogic
    {
        public static EnemyTier ResolveTier(EnemyDefinition def)
        {
            if (def == null) return EnemyTier.Soldier;
            if (def.isBoss) return EnemyTier.Boss;
            if (def.isMiniboss) return EnemyTier.General;
            if (def.tier != EnemyTier.Soldier)
                return def.tier;
            return ResolveTierFromId(def.id);
        }

        public static EnemyTier ResolveTierFromId(string enemyId)
        {
            if (string.IsNullOrEmpty(enemyId)) return EnemyTier.Soldier;
            switch (enemyId)
            {
                case "barnacle_king":
                    return EnemyTier.Boss;
                case "reef_crab_miniboss":
                case "kraken_spawn":
                case "boarding_barge":
                    return EnemyTier.General;
                case "shark":
                case "cannonboat":
                case "gunner_cog":
                case "bomb_boat":
                case "rammer_raft":
                case "manta_ray":
                case "needle_sloop":
                case "ram_raider":
                    return EnemyTier.Captain;
                default:
                    return EnemyTier.Soldier;
            }
        }

        public static bool CanSpawnTier(EnemyTier tier, float elapsedSeconds, int playerLevel)
        {
            var minutes = elapsedSeconds / 60f;
            switch (tier)
            {
                case EnemyTier.Soldier:
                    return true;
                case EnemyTier.Captain:
                    return minutes >= 1.5f || playerLevel >= 2;
                case EnemyTier.General:
                    return minutes >= 5f && playerLevel >= 5;
                case EnemyTier.Boss:
                    return true;
                default:
                    return true;
            }
        }

        public static bool CanSpawnEnemyId(string enemyId, float elapsedSeconds, int playerLevel) =>
            CanSpawnTier(ResolveTierFromId(enemyId), elapsedSeconds, playerLevel);
    }
}
