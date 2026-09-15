using System;
using System.Collections.Generic;
using RoguePirates.Simulation.Combat;

namespace RoguePirates.Simulation.Balance
{
    public sealed class BalanceSimulationResult
    {
        public int Seed;
        public int Ticks;
        public int Kills;
        public int Level;
        public bool ObjectiveSecured;
        public bool PlayerSurvived;
        public float SimSeconds;
        public string StateHash = string.Empty;
    }

    public static class BalanceSimulationRunner
    {
        public static BalanceSimulationResult Run(int seed, int ticks, float moveX = 1f, float moveY = 0f)
        {
            var sim = new CombatSimulator(seed);
            for (var t = 0; t < ticks; t++)
            {
                sim.SetMoveInput(moveX, moveY);
                sim.Step(SimConstants.TickDurationSeconds);
            }

            return new BalanceSimulationResult
            {
                Seed = seed,
                Ticks = ticks,
                Kills = sim.Kills,
                Level = sim.Level,
                ObjectiveSecured = sim.ObjectiveSecured,
                PlayerSurvived = sim.State != SimRunState.PlayerDead,
                SimSeconds = sim.SimSeconds,
                StateHash = sim.ComputeStateHash()
            };
        }

        public static IReadOnlyList<BalanceSimulationResult> RunBatch(IEnumerable<int> seeds, int ticksPerSeed)
        {
            var results = new List<BalanceSimulationResult>();
            foreach (var seed in seeds)
                results.Add(Run(seed, ticksPerSeed));
            return results;
        }

        public static bool HasMandatoryPickDominance(IReadOnlyDictionary<string, float> pickRates, float maxRate = 0.65f)
        {
            foreach (var kv in pickRates)
            {
                if (kv.Value > maxRate) return true;
            }
            return false;
        }
    }
}
