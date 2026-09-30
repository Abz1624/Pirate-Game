using System.Linq;
using RoguePirates.V4.Config;
using RoguePirates.V4.DevTools.Balance;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// V5 after the demo (owner, 30 Sep 2026: keep the demo's XP ×0.5 and retune around it): writes
    /// <see cref="DemoBaseline.Retune"/> and the re-measured director curve to the assets. Every value changed here keeps
    /// its V5 M3 value in DemoBaseline (and its V3 value in the v3-parity preset). Safe to re-run: it only writes values
    /// that still hold their pre-retune value.
    /// </summary>
    public static class V5DemoBaselineRetune
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        public const string BalanceVersion = "saltwind-v5-demo";

        [MenuItem("RoguePirates/V5/Write demo-baseline retune")]
        public static string Apply()
        {
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);
            if (set == null) return "config set not found: " + SetPath;
            // Re-runnable: a value is written only while the asset still holds its pre-retune value (a later tune wins).
            int written = 0, already = 0;
            // Balance versions are relabelled only on configs no later phase has tuned (a later version wins).
            var before = new System.Collections.Generic.HashSet<string> { "saltwind-v3-parity", "saltwind-v5-m1", "saltwind-v5-m2", "saltwind-v5-m3" };
            var skipped = new System.Collections.Generic.List<string>();
            foreach (var group in DemoBaseline.Retune.GroupBy(e => e.configId))
            {
                var asset = set.configs.FirstOrDefault(c => c != null && c.configId == group.Key);
                if (asset == null) return "config not found: " + group.Key;
                Undo.RecordObject(asset, "V5 demo-baseline retune");
                foreach (var (_, path, value, pre) in group)
                {
                    var type = ConfigPath.TypeAt(asset, path);
                    if (!ConfigFieldWalker.TryParse(type, value, out var parsed) || !ConfigFieldWalker.TryParse(type, pre, out var parsedPre)) return $"{group.Key}.{path}: does not parse";
                    var current = ConfigFieldWalker.Format(ConfigPath.Get(asset, path));
                    if (current == ConfigFieldWalker.Format(parsed)) { already++; continue; }
                    if (current != ConfigFieldWalker.Format(parsedPre)) { skipped.Add($"{group.Key}.{path} = {current}"); continue; }
                    ConfigPath.Set(asset, path, parsed);
                    written++;
                }
                if (before.Contains(asset.balanceVersion)) asset.balanceVersion = BalanceVersion;
                EditorUtility.SetDirty(asset);
            }
            // The configs the demo itself changed carry the same balance version (their values are the V5 baseline).
            foreach (var id in DemoBaseline.PreDemo.Select(e => e.configId).Distinct())
            {
                var asset = set.configs.FirstOrDefault(c => c != null && c.configId == id);
                if (asset == null || !before.Contains(asset.balanceVersion)) continue;
                Undo.RecordObject(asset, "V5 demo-baseline retune");
                asset.balanceVersion = BalanceVersion;
                EditorUtility.SetDirty(asset);
            }
            var director = set.All<DirectorConfig>().FirstOrDefault();
            if (director == null) return "director config not found";
            var m2 = DemoBaseline.OnTrackGrowthM2;
            if (Mathf.Abs(director.onTrackGrowth.Evaluate(10f) - m2.First(k => k.minute == 10f).growth) < 0.01f)
            {
                Undo.RecordObject(director, "V5 demo-baseline retune");
                var keys = DemoBaseline.OnTrackGrowth.Select(k => new Keyframe(k.minute, k.growth)).ToArray();
                director.onTrackGrowth = new AnimationCurve(keys);
                for (var i = 0; i < keys.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(director.onTrackGrowth, i, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(director.onTrackGrowth, i, AnimationUtility.TangentMode.Linear);
                }
                director.balanceVersion = BalanceVersion;
                EditorUtility.SetDirty(director);
            }
            else if (Mathf.Abs(director.onTrackGrowth.Evaluate(10f) - DemoBaseline.OnTrackGrowth.First(k => k.minute == 10f).growth) >= 0.01f)
                skipped.Add("director.onTrackGrowth (tuned since)");
            AssetDatabase.SaveAssets();
            return $"ok: {written} written, {already} already set" + (skipped.Count > 0 ? "; left alone (changed since): " + string.Join(", ", skipped) : "") + $"; balance version {BalanceVersion}";
        }
    }
}
