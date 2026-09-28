using System;
using System.Linq;
using RoguePirates.V4.Config;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Editor
{
    /// <summary>M13: creates the narrative config (placeholder barks and the nine tutorial prompts) and adds it to the Saltwind set. Idempotent.</summary>
    public static class V4NarrativeConfigGenerator
    {
        public const string NarrativePath = V3ConfigMigrator.DataRoot + "/Narrative/SaltwindNarrative.asset";

        [MenuItem("RoguePirates/V4/Create M13 Narrative Config")]
        static void CreateFromMenu() => Debug.Log(Create());

        /// <summary>The phase-tagged barks: which timeline phase each one waits for (ids from the legacy timeline mapping).</summary>
        public static void ApplyPhaseTags(NarrativeConfig n)
        {
            void Tag(string barkId, string phaseId)
            {
                var b = n.barks.Find(x => x != null && x.barkId == barkId);
                if (b != null) b.tag = phaseId;
            }
            Tag("elite_hammer_penny", RoguePirates.V4.Legacy.LegacyConfigMapping.EliteA);
            Tag("elite_kraken_penny", RoguePirates.V4.Legacy.LegacyConfigMapping.EliteB);
            Tag("reef_crab_penny", RoguePirates.V4.Legacy.LegacyConfigMapping.ReefCrab);
            Tag("overtime_penny", RoguePirates.V4.Legacy.LegacyConfigMapping.Overtime);
            Tag("storm_penny", RoguePirates.V4.Legacy.LegacyConfigMapping.Storm);
        }

        public static string Create()
        {
            if (!AssetDatabase.IsValidFolder(V3ConfigMigrator.DataRoot + "/Narrative")) AssetDatabase.CreateFolder(V3ConfigMigrator.DataRoot, "Narrative");
            var narrative = AssetDatabase.LoadAssetAtPath<NarrativeConfig>(NarrativePath);
            var created = false;
            if (narrative == null)
            {
                narrative = ScriptableObject.CreateInstance<NarrativeConfig>();
                narrative.configId = "narrative.saltwind";
                AssetDatabase.CreateAsset(narrative, NarrativePath);
                created = true;
            }
            ApplyPhaseTags(narrative);
            EditorUtility.SetDirty(narrative);
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(V3ConfigMigrator.ConfigSetPath);
            if (set != null && !set.configs.Contains(narrative))
            {
                set.configs.Add(narrative);
                set.configs = set.configs.Where(c => c != null).OrderBy(c => c.configId, StringComparer.Ordinal).ToList();
                EditorUtility.SetDirty(set);
            }
            AssetDatabase.SaveAssets();
            return $"narrative config {(created ? "created" : "exists")}; set has {set?.configs.Count} configs\n" + (set != null ? ConfigValidator.Validate(set) : "");
        }
    }
}
