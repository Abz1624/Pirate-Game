using System;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Core
{
    public sealed class RunRng
    {
        readonly System.Random _map;
        readonly System.Random _loot;
        readonly System.Random _director;
        readonly System.Random _combat;
        readonly System.Random _powerup;

        public int Seed { get; }

        public RunRng(int seed)
        {
            Seed = seed;
            _map = new System.Random(unchecked(seed * 73856093));
            _loot = new System.Random(unchecked(seed * 19349663));
            _director = new System.Random(unchecked(seed * 83492791));
            _combat = new System.Random(unchecked(seed * 50331653));
            _powerup = new System.Random(unchecked(seed * 27182818));
        }

        public float MapFloat() => (float)_map.NextDouble();
        public int MapInt(int min, int max) => _map.Next(min, max);
        public float LootFloat() => (float)_loot.NextDouble();
        public int LootInt(int min, int max) => _loot.Next(min, max);
        public float DirectorFloat() => (float)_director.NextDouble();
        public int DirectorInt(int min, int max) => _director.Next(min, max);
        public float CombatFloat() => (float)_combat.NextDouble();
        public float PowerupFloat() => (float)_powerup.NextDouble();

        public Rarity RollRarity(float luck, bool clampLegendary = true)
        {
            var weights = new float[5];
            for (var i = 0; i < 5; i++)
            {
                var exponent = i;
                weights[i] = GameConstants.RarityBaseWeights[i] * Mathf.Pow(1f + luck, 0.55f * exponent);
            }

            if (clampLegendary)
            {
                var totalBefore = 0f;
                for (var i = 0; i < 5; i++) totalBefore += weights[i];
                var legendaryProb = weights[4] / totalBefore;
                if (legendaryProb > 0.06f)
                {
                    weights[4] = totalBefore * 0.06f;
                }
            }

            var total = 0f;
            for (var i = 0; i < 5; i++) total += weights[i];
            var roll = LootFloat() * total;
            var acc = 0f;
            for (var i = 0; i < 5; i++)
            {
                acc += weights[i];
                if (roll <= acc) return (Rarity)i;
            }

            return Rarity.Common;
        }

        public int WeightedPick(ReadOnlySpan<float> weights)
        {
            var total = 0f;
            for (var i = 0; i < weights.Length; i++) total += weights[i];
            var roll = DirectorFloat() * total;
            var acc = 0f;
            for (var i = 0; i < weights.Length; i++)
            {
                acc += weights[i];
                if (roll <= acc) return i;
            }

            return weights.Length - 1;
        }
    }
}
