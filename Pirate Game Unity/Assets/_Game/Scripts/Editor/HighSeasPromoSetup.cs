#if UNITY_EDITOR
using HighSeas.Promo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HighSeas.Editor
{
    public static class HighSeasPromoSetup
    {
        const string V3RunScene = "Assets/_Game/Scenes/v3/V3SaltwindRun.unity";
        const string LegacyRunScene = "Assets/_Game/Scenes/Run.unity";
        const string PromoScene = "Assets/_Game/Scenes/Promo/Teaser_Surrounded.unity";

        [MenuItem("HighSeas/Promo/Add Teaser Director To Active Scene")]
        public static void AddTeaserDirectorToActiveScene()
        {
            var existing = Object.FindFirstObjectByType<PromoTeaserDirector>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("PromoTeaserDirector already exists in the scene.");
                return;
            }

            var go = new GameObject("PromoTeaserDirector");
            go.AddComponent<PromoTeaserDirector>();
            Undo.RegisterCreatedObjectUndo(go, "Add Promo Teaser Director");
            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Added PromoTeaserDirector. Press Play, Randomize, then Start recording (or use inspector buttons). R replays the zoom.");
        }

        [MenuItem("HighSeas/Promo/Open Teaser Scene (from V3 Run)")]
        public static void OpenTeaserScene()
        {
            if (!System.IO.File.Exists(PromoScene))
                CreateTeaserSceneFromV3();

            if (!System.IO.File.Exists(PromoScene))
            {
                Debug.LogError("Could not create promo teaser scene.");
                return;
            }

            EditorSceneManager.OpenScene(PromoScene, OpenSceneMode.Single);
        }

        [MenuItem("HighSeas/Promo/Recreate Teaser Scene (from V3 Run)")]
        public static void RecreateTeaserScene()
        {
            CreateTeaserSceneFromV3();
            if (System.IO.File.Exists(PromoScene))
                EditorSceneManager.OpenScene(PromoScene, OpenSceneMode.Single);
        }

        static void CreateTeaserSceneFromV3()
        {
            var source = System.IO.File.Exists(V3RunScene) ? V3RunScene : LegacyRunScene;
            if (!System.IO.File.Exists(source))
            {
                Debug.LogError("Run scene not found. Bootstrap the project first.");
                return;
            }

            System.IO.Directory.CreateDirectory("Assets/_Game/Scenes/Promo");
            if (!AssetDatabase.CopyAsset(source, PromoScene))
            {
                Debug.LogError($"Failed to copy {source} to {PromoScene}");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorSceneManager.OpenScene(PromoScene, OpenSceneMode.Single);
            AddTeaserDirectorToActiveScene();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), PromoScene);
            Debug.Log($"Promo teaser scene ready at {PromoScene}. Set Game view to 1080p, press Play, record ~8 seconds.");
        }
    }
}
#endif
