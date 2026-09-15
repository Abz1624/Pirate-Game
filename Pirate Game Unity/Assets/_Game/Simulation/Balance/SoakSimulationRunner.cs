using System.Collections.Generic;

namespace RoguePirates.Simulation.Balance
{
    public static class SoakSimulationRunner
    {
        public static IReadOnlyList<BalanceSimulationResult> RunSoak(int startSeed, int count, int ticksPerSeed = 18000)
        {
            var seeds = new List<int>(count);
            for (var i = 0; i < count; i++)
                seeds.Add(startSeed + i * 9973);
            return BalanceSimulationRunner.RunBatch(seeds, ticksPerSeed);
        }
    }
}
