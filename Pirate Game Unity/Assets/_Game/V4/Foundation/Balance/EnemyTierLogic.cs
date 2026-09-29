using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;

namespace RoguePirates.V4.Foundation.Balance
{
    public static class EnemyTierLogic
    {
        public static EnemyTier ResolveTier(EnemyDefinition def)
        {
            if (def == null) return EnemyTier.Soldier;
            if (def.isBoss) return EnemyTier.Boss;
            if (def.isMiniboss) return EnemyTier.General;
            return def.tier; // M16: filled from the archetype/variant config (V3 looked Soldier-tier ids up in a table)
        }

    }
}
