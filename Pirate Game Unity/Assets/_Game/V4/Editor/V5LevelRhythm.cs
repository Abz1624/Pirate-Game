using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using RoguePirates.V4.Config;
using RoguePirates.V4.DevTools.Balance;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// V5 M3c (owner, 1 Oct 2026): the level-up rhythm. <see cref="Tune"/> fits the XP table to the growing gaps in the
    /// simulator on a runtime copy and writes the tuning log; with <c>write</c> it also writes the table and the pace
    /// targets (<see cref="XpTableTuner.TargetEdits"/>) to the assets. <see cref="Report"/> writes the simulator report on
    /// the current data. Output: plan/v5/phase-03c-levels/.
    /// </summary>
    public static class V5LevelRhythm
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        public const string BalanceVersion = "saltwind-v5-m3c";

        static string OutFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "plan", "v5", "phase-03c-levels"));

        static string F(float v, string f = "0.#") => float.IsNaN(v) ? "–" : v.ToString(f, CultureInfo.InvariantCulture);

        [MenuItem("RoguePirates/V5/M3c: fit the XP table (report only)")]
        public static string TuneReport() => Tune(false);

        [MenuItem("RoguePirates/V5/M3c: fit and write the XP table")]
        public static string TuneWrite() => Tune(true);

        public static string Tune(bool write)
        {
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);
            if (set == null) return "config set not found";
            Directory.CreateDirectory(OutFolder);
            int[] table;
            var sb = new StringBuilder();
            using (var rt = new ConfigSetRuntime(set))
            {
                var c = new SimContent(rt);
                var rounds = XpTableTuner.Tune(c);
                table = c.Progression.xpToNextTable;
                sb.AppendLine("# V5 M3c: fitting the XP table to the level-up gaps");
                sb.AppendLine();
                sb.AppendLine("Target: level 1 with the first kill, then gaps of 10, 10, 20, 30, 50 s (Fibonacci × 10 s), then 60 s. Fitted from the old curve; the baseline personality, 60 unsinkable runs (no deaths, so every level is measured; a flawless ship collects a little faster than a real one), seed 11, per round; stops when the mean time error is under 6%.");
                sb.AppendLine();
                for (var i = 0; i < rounds.Count; i++)
                {
                    var r = rounds[i];
                    sb.AppendLine($"## Round {i + 1}: mean time error {r.MeanTimeError:P1} (worst single gap {r.WorstGapError:P0})");
                    sb.AppendLine();
                    sb.AppendLine("| Level | XP to next | Target time | Median time | Target gap | Median gap |");
                    sb.AppendLine("|---|---|---|---|---|---|");
                    for (var l = 1; l < r.MedianTime.Length; l++)
                    {
                        var gap = l >= 2 ? r.MedianTime[l] - r.MedianTime[l - 1] : r.MedianTime[l];
                        sb.AppendLine($"| {l} | {(l - 1 < r.Table.Length ? r.Table[l - 1].ToString() : "–")} | {F(XpTableTuner.TargetTime(l), "0")} | {F(r.MedianTime[l], "0")} | {F(XpTableTuner.TargetGap(l), "0")} | {F(gap, "0")} |");
                    }
                    sb.AppendLine();
                }
                sb.AppendLine("Chosen table (the round with the smallest mean time error): " + string.Join(", ", table));
            }
            File.WriteAllText(Path.Combine(OutFolder, "xp-table-tuning.md"), sb.ToString(), new UTF8Encoding(false));
            if (!write) return "report only: " + string.Join(",", table);

            var prog = set.All<ProgressionConfig>().First();
            Undo.RecordObject(prog, "V5 M3c XP table");
            prog.xpToNextTable = table;
            prog.balanceVersion = BalanceVersion;
            EditorUtility.SetDirty(prog);
            var targets = set.All<BalanceTargetsConfig>().First();
            Undo.RecordObject(targets, "V5 M3c pace targets");
            foreach (var (path, value, _) in XpTableTuner.TargetEdits)
            {
                ConfigFieldWalker.TryParse(ConfigPath.TypeAt(targets, path), value, out var parsed);
                ConfigPath.Set(targets, path, parsed);
            }
            targets.balanceVersion = BalanceVersion;
            EditorUtility.SetDirty(targets);
            AssetDatabase.SaveAssets();
            return "written: " + string.Join(",", table);
        }

        [MenuItem("RoguePirates/V5/M3c: report the level rhythm (no data changes)")]
        public static string Report()
        {
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);
            if (set == null) return "config set not found";
            Directory.CreateDirectory(OutFolder);
            using var rt = new ConfigSetRuntime(set);
            var c = new SimContent(rt);
            var r = SimBatch.Run(c, 200, 1);
            File.WriteAllText(Path.Combine(OutFolder, "model-report.md"), SimBatch.Report(r, c, "V5 M3c: simulator report with the level-up rhythm"), new UTF8Encoding(false));
            var sb = new StringBuilder();
            sb.AppendLine("# V5 M3c: level times against the targets (baseline personality, 60 runs, seed 11)");
            sb.AppendLine();
            var p = c.Personalities.Find(c.Targets.baselinePersonalityId);
            var median = XpTableTuner.MedianLevelTimes(c, p, 60, 11, 18);
            sb.AppendLine("| Level | Target time | Median time | Target gap | Median gap | Gap within ±20% |");
            sb.AppendLine("|---|---|---|---|---|---|");
            for (var l = 1; l <= 18; l++)
            {
                var gap = l >= 2 ? median[l] - median[l - 1] : median[l];
                var ok = float.IsNaN(gap) ? "–" : Mathf.Abs(gap / XpTableTuner.TargetGap(l) - 1f) <= 0.2f ? "✓" : "✗";
                sb.AppendLine($"| {l} | {F(XpTableTuner.TargetTime(l), "0")} | {F(median[l], "0")} | {F(XpTableTuner.TargetGap(l), "0")} | {F(gap, "0")} | {ok} |");
            }
            File.WriteAllText(Path.Combine(OutFolder, "level-times.md"), sb.ToString(), new UTF8Encoding(false));
            return $"missed {r.Missed} of {r.AllChecks.Count()}";
        }
    }
}
