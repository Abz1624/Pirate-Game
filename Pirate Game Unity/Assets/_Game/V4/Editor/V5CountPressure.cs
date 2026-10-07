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
    /// crowd, distinct looks, the tuned HP and late types, the per-minute targets) to the assets (V5 M8z: its simulator report
    /// retired with the in-engine simulator; the balance model is plan/playtest/montecarlo). Every value keeps its value before M3d in CountPressure (its V3 value was in the v3-parity preset, retired with D18; V5 M8z (7 Oct, review)).
    /// </summary>
    public static class V5CountPressure
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        public const string BalanceVersion = "saltwind-v5-m3d";

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
    }
}
