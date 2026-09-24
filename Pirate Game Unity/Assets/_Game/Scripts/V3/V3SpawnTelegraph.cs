using HighSeas.Balance;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using UnityEngine;

namespace HighSeas.V3
{
    public static class V3SpawnTelegraph
    {
        public static void NotifyAmbientSpawn(Vector3 worldPosition, EnemyDefinition def)
        {
            if (def == null) return;
            var tier = EnemyTierLogic.ResolveTier(def);
            if (tier == EnemyTier.Soldier)
                return;

            var label = tier switch
            {
                EnemyTier.Captain => "Captain incoming — watch the horizon",
                EnemyTier.General => "Heavy contact approaching",
                _ => "Enemy incoming"
            };
            V3SpawnWarningHud.WarnIncoming(label);
            V3MinimapHud.RegisterSpawnPing(worldPosition, 3f);
            V3SpawnEdgeIndicator.Notify(worldPosition);
        }
    }
}
