using System.Collections.Generic;
using HighSeas.Data;

namespace HighSeas.V3
{
    /// <summary>
    /// Enemy cycle order for weapon/enemy sandboxes — first-seen order in solo V3SaltwindRun
    /// (ambient bands 0→4, specialist aliases, pressure beats, boss).
    /// </summary>
    public static class V3SaltwindEnemyPlaytestCatalog
    {
        public static readonly string[] All =
        {
            "swordfish",
            "deckhand_dinghy",
            "shark",
            "cannonboat",
            "cutthroat_skiff",
            "needle_sloop",
            "ram_raider",
            "hammerhead",
            "reef_crab_miniboss",
            "manta_ray",
            "bomb_boat",
            "rammer_raft",
            "bomb_ketch",
            "boarding_barge",
            "net_cutter",
            "powder_barge",
            "repair_tender",
            "shield_frigate",
            "treasure_raider",
            "kraken_spawn",
            "gunner_cog",
            "barnacle_king"
        };

        static readonly HashSet<string> BossIds = new() { "barnacle_king" };
        static readonly HashSet<string> MinibossIds = new() { "reef_crab_miniboss" };

        public static bool IsBoss(string enemyId) => !string.IsNullOrEmpty(enemyId) && BossIds.Contains(enemyId);

        public static bool IsMiniboss(string enemyId) =>
            !string.IsNullOrEmpty(enemyId) && MinibossIds.Contains(enemyId);

        public static string GetDisplayLabel(string enemyId) =>
            enemyId?.Replace('_', ' ') ?? "enemy";

        /// <summary>Builds definition-array indices in <see cref="All"/> order; skips missing ids.</summary>
        public static void CollectCycleDefinitionIndices(
            EnemyDefinition[] definitions,
            bool includeBossesInCycle,
            List<int> destination)
        {
            destination.Clear();
            if (definitions == null || definitions.Length == 0)
                return;

            var idToIndex = new Dictionary<string, int>();
            for (var i = 0; i < definitions.Length; i++)
            {
                var def = definitions[i];
                if (def == null || string.IsNullOrEmpty(def.id)) continue;
                if (!idToIndex.ContainsKey(def.id))
                    idToIndex.Add(def.id, i);
            }

            for (var o = 0; o < All.Length; o++)
            {
                var id = All[o];
                if (!includeBossesInCycle && (IsBoss(id) || IsMiniboss(id)))
                    continue;
                if (!idToIndex.TryGetValue(id, out var index))
                    continue;
                destination.Add(index);
            }

            for (var i = 0; i < definitions.Length; i++)
            {
                var def = definitions[i];
                if (def == null || string.IsNullOrEmpty(def.id)) continue;
                if (!includeBossesInCycle && (def.isBoss || def.isMiniboss)) continue;
                if (ContainsIndex(destination, i)) continue;
                if (IndexOfId(All, def.id) >= 0) continue;
                destination.Add(i);
            }
        }

        static bool ContainsIndex(List<int> list, int index)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] == index) return true;
            }

            return false;
        }

        static int IndexOfId(string[] ids, string id)
        {
            for (var i = 0; i < ids.Length; i++)
            {
                if (ids[i] == id) return i;
            }

            return -1;
        }
    }
}
