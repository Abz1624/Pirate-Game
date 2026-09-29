using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;

namespace RoguePirates.V4.Foundation.Enemies
{
    public static class EnemyRangedAttackRules
    {
        public static bool SupportsRangedAttacks(EnemyDefinition def)
        {
            if (def == null) return false;
            return def.id switch
            {
                "cannonboat" => true,
                "gunner_cog" => true,
                "bomb_boat" => true,
                "manta_ray" => true,
                "drowned_harrier" => true,
                _ => def.category == EnemyCategory.Ship &&
                      (def.behavior == EnemyBehavior.MaintainRange ||
                       def.behavior == EnemyBehavior.FormationRanged ||
                       def.behavior == EnemyBehavior.StationaryTurret)
            };
        }
    }
}
