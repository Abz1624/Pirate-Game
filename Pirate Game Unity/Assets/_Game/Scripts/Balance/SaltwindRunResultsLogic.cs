using System;
using System.Collections.Generic;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.V3;
using RoguePirates.Simulation.Balance;

namespace HighSeas.Balance
{
    public readonly struct WeaponContributionLine
    {
        public string DisplayName { get; }
        public float DamageSharePercent { get; }

        public WeaponContributionLine(string displayName, float damageSharePercent)
        {
            DisplayName = displayName;
            DamageSharePercent = damageSharePercent;
        }
    }

    /// <summary>Results copy and reconciliation for Saltwind runs (Step 11 / C11).</summary>
    public static class SaltwindRunResultsLogic
    {
        public static string BuildHeadline(bool objectiveSecured, bool runEndedAfterDeath)
        {
            if (objectiveSecured)
                return runEndedAfterDeath
                    ? "VICTORY SECURED — RUN ENDED"
                    : "TERRITORY OBJECTIVE SECURED";
            return "RUN ENDED";
        }

        public static string BuildOutcomeSubline(bool objectiveSecured, bool runEndedAfterDeath)
        {
            if (objectiveSecured && runEndedAfterDeath)
                return "You secured the treasury, then were sunk — victory stays earned.";
            if (objectiveSecured)
                return "Treasury objective complete. Survive or keep farming.";
            return "Treasury not secured this voyage.";
        }

        public static IReadOnlyList<WeaponContributionLine> BuildWeaponContributionLines(
            IReadOnlyDictionary<string, WeaponContributionStats> snapshot)
        {
            if (snapshot == null || snapshot.Count == 0)
                return Array.Empty<WeaponContributionLine>();

            var total = 0f;
            foreach (var pair in snapshot)
                total += pair.Value.totalDamage;
            if (total <= 0f)
                return Array.Empty<WeaponContributionLine>();

            var lines = new List<WeaponContributionLine>(snapshot.Count);
            foreach (var pair in snapshot)
            {
                if (pair.Value.totalDamage <= 0f) continue;
                var pct = pair.Value.totalDamage / total * 100f;
                lines.Add(new WeaponContributionLine(V3WeaponDisplayNames.GetWeaponName(pair.Key), pct));
            }

            lines.Sort((a, b) => b.DamageSharePercent.CompareTo(a.DamageSharePercent));
            return lines;
        }

        public static bool MarksUiMatchesLedger(RunMarksBreakdown breakdown, int marksAwarded) =>
            breakdown.TotalMarks == marksAwarded;

        public static string FormatMarksBreakdownForPlayer(RunMarksBreakdown breakdown, int marksAwarded, int treasuryMarks)
        {
            if (marksAwarded <= 0 && breakdown.TotalMarks <= 0)
                return "\nMarks: none earned this run.\n";

            return
                $"\nMarks earned this run: {marksAwarded}\n" +
                $"  Active minutes: {breakdown.QualifyingOrdinaryMinutes}\n" +
                $"  Special discoveries: {breakdown.SpecialPoiCredits}\n" +
                $"  Scheduled elite: {breakdown.ScheduledEliteCredits}\n" +
                $"  Boss victory: {breakdown.BossVictoryCredits}\n" +
                $"  Overtime: {breakdown.OvertimeMinuteCredits}\n" +
                $"Treasury Marks: {treasuryMarks}\n";
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
