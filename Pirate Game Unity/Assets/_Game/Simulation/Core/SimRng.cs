using System;

namespace RoguePirates.Simulation
{
    /// <summary>Deterministic xorshift32 RNG stream (see plan/NUMERIC_CONTRACT.md).</summary>
    public sealed class SimRng
    {
        uint _state;

        public SimRng(int seed)
        {
            _state = unchecked((uint)seed);
            if (_state == 0) _state = 0x6d2b79f5u;
            NextUInt(); // warm up
        }

        public static SimRng CreateStream(int runSeed, int streamMix)
        {
            unchecked
            {
                return new SimRng(runSeed * streamMix);
            }
        }

        public uint NextUInt()
        {
            var x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        public float NextFloat01()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            var range = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % range);
        }

        public SimRarity RollRarity(float luck = 0f, bool clampLegendary = true)
        {
            var weights = new float[5];
            for (var i = 0; i < 5; i++)
            {
                var exponent = i;
                weights[i] = SimConstants.RarityBaseWeights[i] * MathF.Pow(1f + luck, 0.55f * exponent);
            }

            if (clampLegendary)
            {
                var totalBefore = 0f;
                for (var i = 0; i < 5; i++) totalBefore += weights[i];
                var legendaryProb = weights[4] / totalBefore;
                if (legendaryProb > 0.04f)
                    weights[4] = totalBefore * 0.04f;
            }

            var total = 0f;
            for (var i = 0; i < 5; i++) total += weights[i];
            var roll = NextFloat01() * total;
            var acc = 0f;
            for (var i = 0; i < 5; i++)
            {
                acc += weights[i];
                if (roll <= acc) return (SimRarity)i;
            }

            return SimRarity.Common;
        }

        public uint StateSnapshot => _state;
    }
}
