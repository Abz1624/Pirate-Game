using System.IO;
using HighSeas.Balance;
using HighSeas.Enemies;
using HighSeas.V3;
using RoguePirates.Simulation.Balance;
using UnityEditor;
using UnityEngine;

namespace HighSeas.Editor
{
    public static class SaltwindStageBalanceSetup
    {
        const string ProfilePath = "Assets/_Game/Data/Balance/SaltwindSoloStageBalanceProfile.asset";
        const string SchedulePath = "Assets/_Game/Resources/EncounterSpawnSchedule.asset";

        [MenuItem("HighSeas/Saltwind/Create Solo Stage Balance Profile")]
        public static void CreateProfileAsset()
        {
            EnsureFolder("Assets/_Game/Data/Balance");
            var existing = AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(ProfilePath);
            if (existing != null)
            {
                Debug.Log($"Saltwind stage balance profile already exists at {ProfilePath}");
                Selection.activeObject = existing;
                return;
            }

            var profile = ScriptableObject.CreateInstance<StageBalanceProfile>();
            profile.name = "SaltwindSoloStageBalanceProfile";
            profile.encounterSchedule = AssetDatabase.LoadAssetAtPath<EncounterSpawnSchedule>(SchedulePath);
            AssetDatabase.CreateAsset(profile, ProfilePath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = profile;
            Debug.Log($"Created {ProfilePath}");
        }

        [MenuItem("HighSeas/Saltwind/Export Profile Evidence (Codex)")]
        public static void ExportProfileEvidence()
        {
            var profile = AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(ProfilePath);
            if (profile == null)
            {
                Debug.LogError($"Missing profile at {ProfilePath}. Run Create Solo Stage Balance Profile first.");
                return;
            }

            var export = profile.ToExport();
            var json = StageBalanceProfileExportCodec.ToCanonicalJson(export);
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var outDir = Path.Combine(repoRoot, "plan", "saltwind-verification", "profile-export");
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "SaltwindSoloStageBalanceProfile.json"), json);
            File.WriteAllText(Path.Combine(outDir, "SaltwindSoloStageBalanceProfile.hash.txt"), export.ContentHash + "\n");
            Debug.Log($"Exported profile evidence to {outDir} (hash={export.ContentHash}).");
        }

        [MenuItem("HighSeas/Saltwind/Run Balance Lab Matrix (1200 sim runs)")]
        public static void RunBalanceLabMatrix()
        {
            var profile = AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(ProfilePath)
                            ?? StageBalanceProfile.CreateBuiltInSaltwindDefaults();
            var export = profile.ToExport();
            var report = SaltwindBalanceLabBatchRunner.RunMatrix(export, new SaltwindBalanceLabBatchOptions
            {
                BaseSeed = 12000,
                SeedsPerCell = 100
            });
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var outDir = Path.Combine(repoRoot, "plan", "saltwind-verification", "balance-lab");
            SaltwindBalanceLabBatchRunner.WriteReportFiles(report, outDir);
            Debug.Log($"Saltwind balance lab matrix complete: {report.TotalRuns} runs → {outDir}\n{report.ToHumanSummary()}");
        }

        [MenuItem("HighSeas/Saltwind/Wire V3SaltwindRun Stage Balance Host")]
        public static void WireSaltwindRunHost()
        {
            CreateProfileAsset();
            var profile = AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(ProfilePath);
            if (profile == null)
            {
                Debug.LogError("Failed to load Saltwind stage balance profile.");
                return;
            }

            if (!EditorSceneHelper.OpenScene(V3SceneNames.SaltwindRunPath))
                return;

            var systems = GameObject.Find("Systems");
            if (systems == null)
            {
                Debug.LogError("Systems object not found in V3SaltwindRun.");
                return;
            }

            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(systems);

            var hosts = systems.GetComponents<StageBalanceProfileHost>();
            StageBalanceProfileHost host = null;
            for (var i = 0; i < hosts.Length; i++)
            {
                if (hosts[i] == null) continue;
                if (host == null)
                    host = hosts[i];
                else
                    Object.DestroyImmediate(hosts[i], true);
            }

            if (host == null)
                host = systems.AddComponent<StageBalanceProfileHost>();

            var so = new SerializedObject(host);
            so.FindProperty("profile").objectReferenceValue = profile;
            so.FindProperty("requireForSinglePlayerRun").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManagerSave();
            Debug.Log("StageBalanceProfileHost wired on V3SaltwindRun/Systems.");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        static void EditorSceneManagerSave()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }
    }

    static class EditorSceneHelper
    {
        public static bool OpenScene(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                Debug.LogError($"Missing scene asset: {path}");
                return false;
            }

            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
            return true;
        }
    }
}
