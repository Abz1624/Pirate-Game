using System;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Core
{
    /// <summary>
    /// The run's seeded random streams: the one place gameplay draws come from (V5 M9a, I9a.2). Five shared streams
    /// (map, loot, director, combat, powerup) derived from the run seed, plus <see cref="RunStream"/> for a module's own
    /// stream (seed ^ its salt) and <see cref="RunSeeds"/> for choosing a new local seed. No gameplay code under V4/Runtime
    /// or V4/Foundation may use UnityEngine.Random or System.Random directly (V5RunSeedingTests scans for it), so the same
    /// seed and the same inputs give the same run, and a seed issued by the server (M10) decides every draw.
    /// </summary>
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

    /// <summary>
    /// V5 M9a (I9a.2): a module's own seeded stream (the elite lever, barks, the leash, pickups, the practice bot). It is
    /// the same generator as the <c>System.Random(seed)</c> each used before: a module that was already seeded from the run
    /// seed (the elite lever, barks, pickups, the practice bot) draws the same sequence as before. The enemy leash was not:
    /// it used a fixed <c>System.Random(0x1EA5)</c> for every run and now follows the run seed (run seed ^ 0x1EA5), so its
    /// draws (where a leashed enemy reappears) differ from before by design. Construct it from the run seed mixed with the
    /// module's salt; never from the clock.
    /// </summary>
    public sealed class RunStream
    {
        readonly System.Random _random;

        public RunStream(int seed) { _random = new System.Random(seed); }

        /// <summary>[0, 1) as a double.</summary>
        public double NextDouble() => _random.NextDouble();
        /// <summary>[0, 1) as a float.</summary>
        public float NextFloat() => (float)_random.NextDouble();
        /// <summary>[0, maxExclusive).</summary>
        public int Next(int maxExclusive) => _random.Next(maxExclusive);
        /// <summary>[minInclusive, maxExclusive).</summary>
        public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
    }

    /// <summary>
    /// V5 M9a (I9a.2): where a run's seed comes from when nobody supplied one (a first run, a retry). Not a gameplay draw:
    /// it picks the seed every stream then derives from. A server-issued seed (M10) replaces it through
    /// <c>RunRequest.RequestIssuedSeed</c>.
    /// </summary>
    public static class RunSeeds
    {
        /// <summary>A fresh positive, non-zero seed (from a new GUID; independent of UnityEngine.Random's global state).</summary>
        public static int NewLocalSeed()
        {
            var seed = Guid.NewGuid().GetHashCode() & int.MaxValue;
            return seed == 0 ? 1 : seed;
        }
    }
}
