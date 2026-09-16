using System.Collections.Generic;
using HighSeas.Core;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Rolls salvage-chest run Items with GDD rarity weights and uniform picks within tier.</summary>
    public static class V3ChestLoot
    {
        static readonly float[] BaseRarityWeights = { 35f, 45f, 12f, 2f, 0.2f };

        public static string RollSalvageItem(RunRng rng, float luck, int stageDepth = 1)
        {
            var rarity = RollChestRarity(rng, luck, stageDepth);
            return PickUniformItem(rng, rarity);
        }

        static Rarity RollChestRarity(RunRng rng, float luck, int stageDepth)
        {
            var weights = new float[BaseRarityWeights.Length];
            var stageRareBoost = 1f + 0.15f * Mathf.Max(0, stageDepth - 1);
            var luckBoost = 1f + Mathf.Max(0f, luck) / 100f;

            for (var i = 0; i < weights.Length; i++)
            {
                weights[i] = BaseRarityWeights[i];
                if (i >= (int)Rarity.Rare)
                    weights[i] *= stageRareBoost;
                if (i >= (int)Rarity.Uncommon)
                    weights[i] *= luckBoost;
            }

            var total = 0f;
            for (var i = 0; i < weights.Length; i++)
                total += weights[i];

            var roll = rng.LootFloat() * total;
            var acc = 0f;
            for (var i = 0; i < weights.Length; i++)
            {
                acc += weights[i];
                if (roll <= acc)
                    return (Rarity)i;
            }

            return Rarity.Common;
        }

        static string PickUniformItem(RunRng rng, Rarity rarity)
        {
            var pool = V3ItemCatalog.ChestPool;
            for (var tier = (int)rarity; tier >= 0; tier--)
            {
                var candidates = new List<string>();
                for (var i = 0; i < pool.Count; i++)
                {
                    if ((int)pool[i].rarity == tier)
                        candidates.Add(pool[i].id);
                }

                if (candidates.Count == 0)
                    continue;

                return candidates[rng.LootInt(0, candidates.Count)];
            }

            return pool[0].id;
        }
    }
}
