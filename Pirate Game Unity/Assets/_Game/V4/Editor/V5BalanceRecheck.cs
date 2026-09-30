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
    /// V5 balance re-check after the demo (30 Sep 2026): the owner kept the demo tuning as the new baseline. This runs the
    /// M2/M3 simulator reports on the current data and on the V5 M3 game (the same data with the pre-demo values and the
    /// pre-retune values put back, in a runtime copy: <see cref="DemoBaseline"/>), and writes both plus a comparison to
    /// plan/v5/balance-recheck/. Report only: no asset is changed.
    /// </summary>
    public static class V5BalanceRecheck
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        const int Runs = 200, Seed = 1; // as the published M2 and M3 reports

        static string OutFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "plan", "v5", "balance-recheck"));

        [MenuItem("RoguePirates/V5/Report balance re-check (no data changes)")]
        public static string Run()
        {
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);
            if (set == null) return "config set not found: " + SetPath;
            Directory.CreateDirectory(OutFolder);

            using var now = new ConfigSetRuntime(set);
            using var before = new ConfigSetRuntime(set);
            var errors = new List<string>();
            if (!DemoBaseline.Revert(before, errors) || !DemoBaseline.RevertRetune(before, errors)) return "the V5 M3 values did not apply: " + string.Join("; ", errors);

            var cNow = new SimContent(now);
            var cBefore = new SimContent(before);
            var rNow = SimBatch.Run(cNow, Runs, Seed);
            var rBefore = SimBatch.Run(cBefore, Runs, Seed);
            Write("model-report.md", SimBatch.Report(rNow, cNow, "V5 balance re-check: simulator report on the current data"));
            Write("model-report-m3.md", SimBatch.Report(rBefore, cBefore, "V5 balance re-check: simulator report on the V5 M3 values (same code)"));
            var curvesNow = WeaponModel.Curves(cNow);
            var curvesBefore = WeaponModel.Curves(cBefore);
            Write("weapon-parity.md", WeaponModel.Report(curvesNow));
            Write("weapon-parity-m3.md", WeaponModel.Report(curvesBefore));
            Write("build-pieces.md", BuildModel.Report(BuildModel.Pieces(cNow)));
            var text = Compare(rBefore, rNow, curvesBefore, curvesNow) + Attribution(set, rNow, errors);
            if (errors.Count > 0) return "a group did not apply: " + string.Join("; ", errors);
            Write("comparison.md", text);
            return $"ok: missed {rBefore.Missed} → {rNow.Missed} of {rNow.AllChecks.Count()}; written to {OutFolder}";
        }

        /// <summary>The changes in groups: which group moves the runs (each put back alone on the current data).</summary>
        static readonly (string name, System.Func<string, string, bool> match)[] Groups =
        {
            ("the retune (enemy growth, evolution level 6, director curve, level targets)", null),
            ("XP pace (xpPickupMultiplier 0.5 → 1)", (id, path) => id == "pickups.saltwind"),
            ("ship speed (shipSpeedMultiplier 1.625 → 1)", (id, path) => id == "run"),
            ("weapon ranges and shot lifetimes", (id, path) => id.StartsWith("weapon.")),
            ("enemies (charge timings, ranged from 1:00, band 0 specialists, kraken spawn in view)", (id, path) => id.StartsWith("enemy.") || id.StartsWith("spawner.") || id == "timeline.saltwind" || id == "difficulty.saltwind"),
        };

        static string Attribution(ConfigSet set, SimBatchResult now, List<string> errors)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("## Which change moves the runs (each group put back alone on the current data)");
            sb.AppendLine();
            sb.AppendLine("| Put back | Targets missed | Most players: median end minute | Steady Captain: end / boss won / level 10:00 | Casual Cruiser: boss won |");
            sb.AppendLine("|---|---|---|---|---|");
            sb.AppendLine(Row("nothing (the current data)", now));
            foreach (var g in Groups)
            {
                using var rt = new ConfigSetRuntime(set);
                if (!(g.match == null ? DemoBaseline.RevertRetune(rt, errors) : DemoBaseline.Revert(rt, errors, g.match))) continue;
                sb.AppendLine(Row(g.name, SimBatch.Run(new SimContent(rt), Runs, Seed)));
            }
            return sb.ToString();
        }

        static string Row(string name, SimBatchResult r)
        {
            var weighted = r.Personalities.Sum(p => p.Personality.share * p.DeathMedian) / Mathf.Max(0.0001f, r.Personalities.Sum(p => p.Personality.share));
            var s = r.For("steady_captain");
            var c = r.For("casual_cruiser");
            return $"| {name} | {r.Missed} of {r.AllChecks.Count()} | {F(weighted)} | {(s == null ? "–" : $"{F(s.DeathMedian)} / {s.BossWinRate:P0} / {F(s.LevelAt10, "0")}")} | {(c == null ? "–" : c.BossWinRate.ToString("P0"))} |";
        }

        static void Write(string name, string text) => File.WriteAllText(Path.Combine(OutFolder, name), text, new UTF8Encoding(false));

        static string F(float v, string f = "0.#") => float.IsNaN(v) ? "–" : v.ToString(f, System.Globalization.CultureInfo.InvariantCulture);

        static string Compare(SimBatchResult a, SimBatchResult b, IReadOnlyList<WeaponPowerCurve> wa, IReadOnlyList<WeaponPowerCurve> wb)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# V5 balance re-check: V5 M3 → current data (the demo baseline)");
            sb.AppendLine();
            sb.AppendLine($"Same code, {Runs} runs per personality, seed {Seed}. Targets missed: **{a.Missed} → {b.Missed}** of {b.AllChecks.Count()}.");
            sb.AppendLine();
            sb.AppendLine("| Personality | Median end minute | Boss won | Level 5:00 | Level 10:00 | Kills/min | Gold | Evolved | Targets now |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (var pb in b.Personalities)
            {
                var pa = a.For(pb.Personality.personalityId);
                if (pa == null) continue;
                var missed = pb.Checks.Where(c => !c.Met).Select(c => c.Name.Substring(c.Name.IndexOf(':') + 2) + " " + F(c.Value, "0.##") + " (" + c.Band + ")").ToList();
                sb.AppendLine($"| {pb.Personality.displayName} | {F(pa.DeathMedian)} → **{F(pb.DeathMedian)}** | {pa.BossWinRate:P0} → **{pb.BossWinRate:P0}** | {F(pa.LevelAt5, "0")} → {F(pb.LevelAt5, "0")} | {F(pa.LevelAt10, "0")} → {F(pb.LevelAt10, "0")} | {F(pa.KillsPerMinute, "0")} → {F(pb.KillsPerMinute, "0")} | {F(pa.Gold, "0")} → {F(pb.Gold, "0")} | {pa.EvolutionRate:P0} → {pb.EvolutionRate:P0} | {(missed.Count == 0 ? "all met" : "✗ " + string.Join("; ", missed))} |");
            }
            float Weighted(SimBatchResult r) => r.Personalities.Sum(p => p.Personality.share * p.DeathMedian) / Mathf.Max(0.0001f, r.Personalities.Sum(p => p.Personality.share));
            sb.AppendLine();
            sb.AppendLine($"Most players (weighted by share): median end minute {F(Weighted(a))} → **{F(Weighted(b))}**.");
            sb.AppendLine();
            sb.AppendLine("## Pacing and per-minute targets that changed");
            sb.AppendLine();
            var before = a.Model.SelectMany(m => m.Checks).Concat(a.Pacing).ToDictionary(c => c.Name, c => c, System.StringComparer.Ordinal);
            var any = false;
            foreach (var c in b.Model.SelectMany(m => m.Checks).Concat(b.Pacing))
            {
                if (!before.TryGetValue(c.Name, out var old) || (old.Met && c.Met)) continue;
                any = true;
                sb.AppendLine($"- {c.Name}: {F(old.Value, "0.##")} → {F(c.Value, "0.##")} (target {c.Band}) {(old.Met ? "met" : "missed")} → **{(c.Met ? "met" : "MISSED")}**");
            }
            if (!any) sb.AppendLine("- none: every per-minute and pacing target met before is still met.");
            sb.AppendLine();
            sb.AppendLine("## Weapons (power ratio to the role at each parity level)");
            sb.AppendLine();
            sb.AppendLine("| Weapon | " + string.Join(" | ", WeaponModel.ParityLevels.Select(l => "L" + l)) + " |");
            sb.AppendLine("|---|" + string.Concat(WeaponModel.ParityLevels.Select(_ => "---|")));
            foreach (var w in wb)
            {
                var old = wa.FirstOrDefault(x => x.Weapon.weaponId == w.Weapon.weaponId);
                sb.Append("| " + w.Weapon.weaponId + " |");
                foreach (var l in WeaponModel.ParityLevels)
                    sb.Append($" {(old != null ? F(WeaponModel.Ratio(wa, old, l), "0.00") : "–")} → {F(WeaponModel.Ratio(wb, w, l), "0.00")} |");
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
