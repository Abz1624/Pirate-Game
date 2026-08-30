#if UNITY_EDITOR
using HighSeas.Enemies;
using HighSeas.Tuning;
using HighSeas.VFX;
using HighSeas.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HighSeas.Editor
{
    public static class HighSeasTestSceneSetup
    {
        const string Root = "Assets/_Game";
        const string RunScenePath = Root + "/Scenes/Run.unity";
        const string CombatScenePath = Root + "/Scenes/Tests/Test_Combat.unity";
        const string PerformanceScenePath = Root + "/Scenes/Tests/Test_Performance.unity";
        const string TuningScenePath = Root + "/Scenes/Tests/Test_Tuning.unity";

        [MenuItem("HighSeas/Integrate Recent Assets")]
        public static void IntegrateRecentAssets()
        {
            HighSeasEnemySetup.SetupEnemyVisuals();
            HighSeasDemoSetup.SetupDemoJuice();
            TuningAssetSync.SyncDataToResources(saveAssets: true);
            Debug.Log("Integrated recent artist assets: enemy visuals, explosion VFX, crystal XP pickups, and synced Resources.");
        }

        [MenuItem("HighSeas/Sync All Test Scenes")]
        public static void SyncAllTestScenes()
        {
            if (!System.IO.File.Exists(RunScenePath))
            {
                Debug.LogError("Run.unity not found. Run HighSeas/Bootstrap Project first.");
                return;
            }

            WireTestScene(CombatScenePath, autoSpawn: true, addTuningSandbox: true, addPerformanceHarness: false);
            WireTestScene(PerformanceScenePath, autoSpawn: false, addTuningSandbox: false, addPerformanceHarness: true);
            WireTestScene(TuningScenePath, autoSpawn: false, addTuningSandbox: true, addPerformanceHarness: false, minimizeIslands: true);

            AssetDatabase.SaveAssets();
            Debug.Log("All test scenes synced with Run mechanics, VFX, player ship visual, and scene-specific harnesses.");
        }

        [MenuItem("HighSeas/Sync Test Combat Scene")]
        public static void SyncTestCombatScene() =>
            WireTestScene(CombatScenePath, autoSpawn: true, addTuningSandbox: true, addPerformanceHarness: false);

        [MenuItem("HighSeas/Sync Test Performance Scene")]
        public static void SyncTestPerformanceScene() =>
            WireTestScene(PerformanceScenePath, autoSpawn: false, addTuningSandbox: false, addPerformanceHarness: true);

        static void WireTestScene(
            string scenePath,
            bool autoSpawn,
            bool addTuningSandbox,
            bool addPerformanceHarness,
            bool minimizeIslands = false)
        {
            if (!System.IO.File.Exists(scenePath))
            {
                Debug.LogWarning($"Scene not found: {scenePath}");
                return;
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            CopyRunGameplayStackFromReference();
            HighSeasDemoSetup.EnsureJuiceSystemsInActiveScene();
            PlayerShipSceneSetup.SetupActiveScene();

            var systems = GameObject.Find("Systems");
            if (systems != null)
            {
                if (systems.GetComponent<Player.PlayerShipVisualBootstrap>() == null)
                    systems.AddComponent<Player.PlayerShipVisualBootstrap>();

                if (addTuningSandbox)
                {
                    var sandbox = systems.GetComponent<TuningSandbox>();
                    if (sandbox == null)
                        sandbox = systems.AddComponent<TuningSandbox>();

                    var so = new SerializedObject(sandbox);
                    so.FindProperty("disableAutoSpawn").boolValue = !autoSpawn;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    HighSeasTuningSetup.WireTuningComponents();
                }

                if (addPerformanceHarness)
                    EnsurePerformanceHarness(systems);

                WireSpawnDirectorSettings(systems);
            }

            if (minimizeIslands)
                HighSeasTuningSetup.MinimizeIslands();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        static void CopyRunGameplayStackFromReference()
        {
            var activePath = SceneManager.GetActiveScene().path;
            var runScene = EditorSceneManager.OpenScene(RunScenePath, OpenSceneMode.Additive);

            GameObject runSystems = null;
            GameObject targetSystems = null;
            foreach (var root in runScene.GetRootGameObjects())
            {
                if (root.name == "Systems")
                    runSystems = root;
            }

            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.scene.path == activePath && root.name == "Systems")
                    targetSystems = root;
            }

            if (runSystems != null && targetSystems != null)
            {
                CopyComponentIfMissing<StageBuilder>(runSystems, targetSystems);
                CopyComponentIfMissing<JuiceVfxBroker>(runSystems, targetSystems);
                CopyComponentIfMissing<CombatVfxController>(runSystems, targetSystems);
                CopyComponentIfMissing<WeaponJuiceController>(runSystems, targetSystems);
                CopyComponentIfMissing<PlayerJuiceController>(runSystems, targetSystems);

                var runStage = runSystems.GetComponent<StageBuilder>();
                var targetStage = targetSystems.GetComponent<StageBuilder>();
                if (runStage != null && targetStage != null)
                    EditorUtility.CopySerialized(runStage, targetStage);

                var runVfxRoot = runSystems.transform.Find("VFXRoot");
                if (runVfxRoot != null && targetSystems.transform.Find("VFXRoot") == null)
                {
                    var vfxRoot = Object.Instantiate(runVfxRoot.gameObject, targetSystems.transform);
                    vfxRoot.name = "VFXRoot";
                }
            }

            EditorSceneManager.CloseScene(runScene, true);
        }

        static void CopyComponentIfMissing<T>(GameObject source, GameObject target) where T : Component
        {
            if (source.GetComponent<T>() == null || target.GetComponent<T>() != null)
                return;
            target.AddComponent<T>();
        }

        static void EnsurePerformanceHarness(GameObject systems)
        {
            var harnessGo = GameObject.Find("PerfHarness") ?? new GameObject("PerfHarness");
            var harness = harnessGo.GetComponent<PerformanceHarness>();
            if (harness == null)
                harness = harnessGo.AddComponent<PerformanceHarness>();

            var so = new SerializedObject(harness);
            so.FindProperty("enemyWorld").objectReferenceValue = systems.GetComponent<EnemyWorld>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireSpawnDirectorSettings(GameObject systems)
        {
            var director = systems.GetComponent<SpawnDirector>();
            if (director == null)
                return;

            var settings = AssetDatabase.LoadAssetAtPath<SpawnDirectorSettings>(
                Root + "/Data/SpawnDirectorSettings.asset");
            if (settings == null)
                return;

            var so = new SerializedObject(director);
            so.FindProperty("settings").objectReferenceValue = settings;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
