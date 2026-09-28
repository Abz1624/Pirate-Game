using System;
using System.Linq;
using RoguePirates.V4.Config;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Editor
{
    /// <summary>M10: creates the director config and adds it to the Saltwind config set. Idempotent. Default mode: Observe in M10, Adaptive from M11 (the v3-parity preset sets Observe).</summary>
    public static class V4DirectorConfigGenerator
    {
        public const string DirectorPath = V3ConfigMigrator.DataRoot + "/Difficulty/SaltwindDirector.asset";

        [MenuItem("RoguePirates/V4/Create M10 Director Config")]
        static void CreateFromMenu() => Debug.Log(Create());

        public static string Create()
        {
            var director = AssetDatabase.LoadAssetAtPath<DirectorConfig>(DirectorPath);
            var created = false;
            if (director == null)
            {
                director = ScriptableObject.CreateInstance<DirectorConfig>();
                director.configId = "director.saltwind";
                AssetDatabase.CreateAsset(director, DirectorPath);
                created = true;
            }
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(V3ConfigMigrator.ConfigSetPath);
            if (set != null && !set.configs.Contains(director))
            {
                set.configs.Add(director);
                set.configs = set.configs.Where(c => c != null).OrderBy(c => c.configId, StringComparer.Ordinal).ToList();
                EditorUtility.SetDirty(set);
            }
            AssetDatabase.SaveAssets();
            return $"director config {(created ? "created" : "exists")}; set has {set?.configs.Count} configs\n" + (set != null ? ConfigValidator.Validate(set) : "");
        }
    }
}
