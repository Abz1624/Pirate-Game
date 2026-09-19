using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using RoguePirates.Simulation.Combat;

namespace RoguePirates.Simulation.Balance
{
    public static class ProfileBalanceHarness
    {
        public static BalanceRunReport Run(
            string profileId,
            int seed,
            int ticks,
            SimInputPolicy inputPolicy = SimInputPolicy.Forward)
        {
            var config = CombatExperimentSimConfig.FromProfileId(profileId);
            var sim = new CombatSimulator(seed);
            sim.ApplyExperimentConfig(config);

            for (var t = 0; t < ticks; t++)
            {
                ApplyInput(sim, seed, t, inputPolicy);
                sim.Step(SimConstants.TickDurationSeconds);
            }

            return new BalanceRunReport
            {
                ProfileId = config.ProfileId,
                Seed = seed,
                Ticks = ticks,
                InputPolicy = inputPolicy.ToString(),
                Kills = sim.Kills,
                Level = sim.Level,
                ObjectiveSecured = sim.ObjectiveSecured,
                PlayerSurvived = sim.State != SimRunState.PlayerDead,
                SimSeconds = sim.SimSeconds,
                StateHash = sim.ComputeStateHash(),
                PeakEnemyCount = sim.PeakEnemyCount
            };
        }

        public static IReadOnlyList<BalanceRunReport> RunMatrix(
            IEnumerable<string> profileIds,
            IEnumerable<int> seeds,
            int ticksPerSeed,
            SimInputPolicy inputPolicy = SimInputPolicy.Forward)
        {
            var results = new List<BalanceRunReport>();
            foreach (var profileId in profileIds)
            {
                foreach (var seed in seeds)
                    results.Add(Run(profileId, seed, ticksPerSeed, inputPolicy));
            }
            return results;
        }

        public static void WriteJsonLines(string path, IReadOnlyList<BalanceRunReport> reports)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < reports.Count; i++)
            {
                var r = reports[i];
                sb.Append('{');
                sb.Append($"\"profileId\":\"{r.ProfileId}\",");
                sb.Append($"\"seed\":{r.Seed},");
                sb.Append($"\"ticks\":{r.Ticks},");
                sb.Append($"\"inputPolicy\":\"{r.InputPolicy}\",");
                sb.Append($"\"kills\":{r.Kills},");
                sb.Append($"\"level\":{r.Level},");
                sb.Append($"\"objectiveSecured\":{(r.ObjectiveSecured ? "true" : "false")},");
                sb.Append($"\"playerSurvived\":{(r.PlayerSurvived ? "true" : "false")},");
                sb.Append($"\"simSeconds\":{r.SimSeconds.ToString(CultureInfo.InvariantCulture)},");
                sb.Append($"\"peakEnemyCount\":{r.PeakEnemyCount},");
                sb.Append($"\"stateHash\":\"{r.StateHash}\",");
                sb.Append($"\"harnessVersion\":\"{r.HarnessVersion}\"");
                sb.Append('}');
                sb.Append('\n');
            }

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, sb.ToString());
        }

        static void ApplyInput(CombatSimulator sim, int seed, int tick, SimInputPolicy policy)
        {
            switch (policy)
            {
                case SimInputPolicy.Kite:
                    sim.SetKiteInput();
                    break;
                case SimInputPolicy.Orbit:
                {
                    var phase = (tick + seed) * 0.05f;
                    sim.SetMoveInput(MathF.Cos(phase), MathF.Sin(phase));
                    break;
                }
                default:
                    sim.SetMoveInput(1f, 0f);
                    break;
            }
        }
    }
}
