using System;
using System.Linq;
using HighSeas.Balance;
using RoguePirates.V4.Config;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Editor
{
    /// <summary>M12: creates the Rum config from the V3 stage profile's Marks economy and adds it to the Saltwind set. Idempotent.</summary>
    public static class V4RewardConfigGenerator
    {
        public const string RewardsPath = V3ConfigMigrator.DataRoot + "/Rewards/SaltwindRewards.asset";

        [MenuItem("RoguePirates/V4/Create M12 Reward Config")]
        static void CreateFromMenu() => Debug.Log(Create());

        public static string Create()
        {
            if (!AssetDatabase.IsValidFolder(V3ConfigMigrator.DataRoot + "/Rewards")) AssetDatabase.CreateFolder(V3ConfigMigrator.DataRoot, "Rewards");
            var rewards = AssetDatabase.LoadAssetAtPath<RewardConfig>(RewardsPath);
            var created = false;
            if (rewards == null)
            {
                rewards = ScriptableObject.CreateInstance<RewardConfig>();
                rewards.configId = "rewards.saltwind";
                AssetDatabase.CreateAsset(rewards, RewardsPath);
                created = true;
            }
            var profile = AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(V3ConfigMigrator.ProfilePath);
            if (profile != null) FromV3(profile, rewards);
            EditorUtility.SetDirty(rewards);
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(V3ConfigMigrator.ConfigSetPath);
            if (set != null && !set.configs.Contains(rewards))
            {
                set.configs.Add(rewards);
                set.configs = set.configs.Where(c => c != null).OrderBy(c => c.configId, StringComparer.Ordinal).ToList();
                EditorUtility.SetDirty(set);
            }
            AssetDatabase.SaveAssets();
            return $"reward config {(created ? "created" : "updated")}; set has {set?.configs.Count} configs\n" + (set != null ? ConfigValidator.Validate(set) : "");
        }

        /// <summary>V3's Marks economy values into the default rows (row labels and the extra chest row stay V4's).</summary>
        public static void FromV3(StageBalanceProfile p, RewardConfig r)
        {
            void Set(string id, int rum, int minKills = -1, int maxCredits = -1, int maxRum = -1)
            {
                var row = r.Row(id);
                if (row == null) return;
                row.rum = rum;
                if (minKills >= 0) row.minKills = minKills;
                if (maxCredits >= 0) row.maxCredits = maxCredits;
                if (maxRum >= 0) row.maxRum = maxRum;
            }
            Set("minutes", p.marksPerQualifyingMinute, p.marksPerQualifyingMinuteMinKills);
            Set("landmarks", p.marksPerSpecialPoi);
            Set("elites", p.marksPerScheduledElite, maxCredits: 2); // V3: a hard cap of 2 (RunMarksTracker)
            Set("boss", p.marksBossVictory, maxCredits: 1);
            Set("overtime", p.marksPerOvertimeMinute, p.marksPerQualifyingMinuteMinKills, maxRum: p.marksOvertimeCap);
        }
    }
}
