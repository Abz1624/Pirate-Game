using System.Collections.Generic;
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
    /// V5 M3d (owner, 1 Oct 2026, notes e, h, k): writes <see cref="CountPressure"/> (fixed enemy stats, the peak-valley
    /// crowd, distinct looks, the tuned HP and late types, the per-minute targets) to the assets, and reports the simulator
    /// on the current data. Every value keeps its value before M3d in CountPressure (and its V3 value in the v3-parity preset).
    /// </summary>
    public static class V5CountPressure
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        public const string BalanceVersion = "saltwind-v5-m3d";
        static string OutFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "plan", "v5", "phase-03d-pressure"));

        [MenuItem("RoguePirates/V5/M3d: write count-based pressure")]
        public static string Write()
        {
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);
            if (set == null) return "config set not found";
            V4ConfigAsset Get(string id) => set.configs.FirstOrDefault(c => c != null && c.configId == id);
            var touched = new HashSet<string>(CountPressure.Edits.Select(e => e.configId)
                .Concat(CountPressure.TunedBefore.Select(e => e.configId))
                .Concat(CountPressure.LateTypes.Select(e => e.configId))
                .Concat(CountPressure.Models.Select(e => e.configId))
                .Concat(new[] { "timeline.saltwind", "balance_targets.saltwind" }));
            foreach (var id in touched) { var c = Get(id); if (c != null) Undo.RecordObject(c, "V5 M3d count pressure"); }
            var errors = CountPressure.Apply(Get, CountPressure.Tuning.Default, path => AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var targets = Get("balance_targets.saltwind");
            foreach (var (path, value, _) in CountPressure.TargetEdits)
            {
                if (!ConfigFieldWalker.TryParse(ConfigPath.TypeAt(targets, path), value, out var v)) { errors.Add("targets." + path); continue; }
                ConfigPath.Set(targets, path, v);
            }
            if (errors.Count > 0) return "errors: " + string.Join("; ", errors);
            foreach (var id in touched)
            {
                var c = Get(id);
                if (c == null) continue;
                c.balanceVersion = BalanceVersion;
                EditorUtility.SetDirty(c);
            }
            AssetDatabase.SaveAssets();
            return $"written: {touched.Count} configs, balance version {BalanceVersion}";
        }

        [MenuItem("RoguePirates/V5/M3d: report the simulator (no data changes)")]
        public static string Report()
        {
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);
            if (set == null) return "config set not found";
            Directory.CreateDirectory(OutFolder);
            using var rt = new ConfigSetRuntime(set);
            var c = new SimContent(rt);
            var r = SimBatch.Run(c, 200, 1);
            var report = new StringBuilder(SimBatch.Report(r, c, "V5 M3d: simulator report with count-based pressure"));
            report.AppendLine().AppendLine("## Barnacle King fight at an early and a late summon (plan review P4)").AppendLine();
            report.AppendLine("Steady Captain, 30 unsinkable runs per summon time (seeds 700-729, the Captains in turn); target 45-90 s.").AppendLine();
            var p = c.Personalities.Find("steady_captain");
            foreach (var summon in new[] { 270f, 510f })
            {
                var fights = new List<float>();
                for (var i = 0; i < 30; i++)
                {
                    var run = new SimRun(c, p, 700 + i, c.Captains[i % c.Captains.Count].captainKey) { SummonBossAtSeconds = summon, Unsinkable = true }.Play();
                    if (run.BossWon) fights.Add(run.BossKillSeconds - run.BossSummonSeconds);
                }
                report.AppendLine($"- summon at {summon / 60f:0.0} min: beaten in {fights.Count} of 30, fight median {SimBatch.Percentile(fights, 0.5f):0} s (p10 {SimBatch.Percentile(fights, 0.1f):0}, p90 {SimBatch.Percentile(fights, 0.9f):0})");
            }
            File.WriteAllText(Path.Combine(OutFolder, "model-report.md"), report.ToString(), new UTF8Encoding(false));
            return $"missed {r.Missed} of {r.AllChecks.Count()}";
        }
    }
}
