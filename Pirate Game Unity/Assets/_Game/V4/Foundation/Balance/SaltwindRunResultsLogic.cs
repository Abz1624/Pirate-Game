using System;
using System.Collections.Generic;
using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.Simulation.Balance;

namespace RoguePirates.V4.Foundation.Balance
{

    /// <summary>Results copy and reconciliation for Saltwind runs (Step 11 / C11).</summary>
    public static class SaltwindRunResultsLogic
    {
        public static string BuildHeadline(bool objectiveSecured, bool runEndedAfterDeath)
        {
            if (objectiveSecured)
                return runEndedAfterDeath
                    ? "VICTORY SECURED: RUN ENDED"
                    : "TERRITORY OBJECTIVE SECURED";
            return "RUN ENDED";
        }

        public static string BuildOutcomeSubline(bool objectiveSecured, bool runEndedAfterDeath)
        {
            if (objectiveSecured && runEndedAfterDeath)
                return "You secured the treasury, then were sunk: victory stays earned.";
            if (objectiveSecured)
                return "Treasury objective complete. Survive or keep farming.";
            return "Treasury not secured this voyage.";
        }

        public static string FormatProfileFooter(StageBalanceProfileExport export, int runSeed, float elapsedSeconds)
        {
            if (export == null)
                return $"Seed {runSeed} · {elapsedSeconds:0}s survived\n";

            var version = export.BalanceVersion.ToString();
            var hashShort = string.IsNullOrEmpty(export.ContentHash)
                ? "?"
                : export.ContentHash.Substring(0, Math.Min(8, export.ContentHash.Length));
            return $"Saltwind balance v{version} · profile {hashShort}… · seed {runSeed} · {elapsedSeconds:0}s\n";
        }
    }
}
