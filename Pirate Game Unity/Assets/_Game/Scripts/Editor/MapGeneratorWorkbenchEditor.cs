#if UNITY_EDITOR

using HighSeas.World;

using UnityEditor;

using UnityEditor.SceneManagement;

using UnityEngine;

#if ENABLE_INPUT_SYSTEM

using UnityEngine.InputSystem;

#endif



namespace HighSeas.Editor

{

    [CustomEditor(typeof(MapGeneratorWorkbench))]

    public sealed class MapGeneratorWorkbenchInspector : UnityEditor.Editor

    {

        public override void OnInspectorGUI()

        {

            DrawDefaultInspector();

            EditorGUILayout.Space(8f);

            EditorGUILayout.HelpBox(

                "Edit mode (Scene view focused):\n" +

                "M — next seed (+1) + procedural rebuild\n" +

                "Shift+M — gameplay/locked seed resolver + rebuild\n" +

                "Ctrl+M — rebuild same seed again\n" +

                "+ / = — +50% island density (V3 layout + extra islands), rebuild same seed",

                MessageType.Info);



            var workbench = (MapGeneratorWorkbench)target;

            using (new EditorGUILayout.HorizontalScope())

            {

                if (GUILayout.Button("Build (M)"))

                    MapGeneratorWorkbenchHotkeys.Build(workbench, MapGeneratorWorkbenchHotkeys.BuildKind.Advance);

                if (GUILayout.Button("Same seed (Ctrl+M)"))

                    MapGeneratorWorkbenchHotkeys.Build(workbench, MapGeneratorWorkbenchHotkeys.BuildKind.Hold);

                if (GUILayout.Button("Gameplay seed"))

                    MapGeneratorWorkbenchHotkeys.Build(workbench, MapGeneratorWorkbenchHotkeys.BuildKind.Gameplay);

            }



            using (new EditorGUILayout.HorizontalScope())

            {

                if (GUILayout.Button("Islands +50% (+)"))

                    MapGeneratorWorkbenchHotkeys.IncreaseIslands(workbench);

                if (GUILayout.Button("Sync bootstrap seed"))

                {

                    workbench.SyncSeedFromGameplay();

                    EditorUtility.SetDirty(workbench);

                }

            }



            if (GUILayout.Button("Reset island counts to default"))

            {

                workbench.ResetProceduralDefaults();

                EditorUtility.SetDirty(workbench);

            }

        }

    }



    [InitializeOnLoad]

    static class MapGeneratorWorkbenchHotkeys

    {

        public enum BuildKind { Advance, Hold, Gameplay }



        static bool _mWasDown;

        static bool _plusWasDown;



        static MapGeneratorWorkbenchHotkeys() => EditorApplication.update += PollEditorKeys;



        static void PollEditorKeys()

        {

            if (EditorApplication.isPlayingOrWillChangePlaymode)

                return;



            if (EditorGUIUtility.editingTextField)

            {

                _mWasDown = false;

                _plusWasDown = false;

                return;

            }



            var workbench = FindWorkbench();

            if (workbench == null)

                return;



#if ENABLE_INPUT_SYSTEM

            var kb = Keyboard.current;

            if (kb == null)

                return;



            var mDown = kb.mKey.isPressed;

            if (mDown && !_mWasDown)

            {

                if (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed)

                    Build(workbench, BuildKind.Hold);

                else if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)

                    Build(workbench, BuildKind.Gameplay);

                else

                    Build(workbench, BuildKind.Advance);

            }



            _mWasDown = mDown;



            var plusDown = kb.equalsKey.isPressed || kb.numpadPlusKey.isPressed;

            if (plusDown && !_plusWasDown)

                IncreaseIslands(workbench);

            _plusWasDown = plusDown;

#endif

        }



        static MapGeneratorWorkbench FindWorkbench()

        {

            var active = EditorSceneManager.GetActiveScene();

            if (!active.IsValid())

                return null;



            var all = Object.FindObjectsByType<MapGeneratorWorkbench>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (var i = 0; i < all.Length; i++)

            {

                if (all[i] != null && all[i].gameObject.scene == active)

                    return all[i];

            }



            return null;

        }



        public static void Build(MapGeneratorWorkbench workbench, BuildKind kind)

        {

            if (workbench == null)

                return;



            var mode = kind switch

            {

                BuildKind.Hold => MapGeneratorSeedMode.Hold,

                BuildKind.Gameplay => MapGeneratorSeedMode.GameplayResolver,

                _ => MapGeneratorSeedMode.Advance

            };



            workbench.Regenerate(mode);

            MarkDirty(workbench);

            SceneView.RepaintAll();

        }



        public static void IncreaseIslands(MapGeneratorWorkbench workbench)

        {

            if (workbench == null)

                return;



            workbench.MultiplyIslandCounts(1.5f);

            workbench.Regenerate(MapGeneratorSeedMode.Hold);

            MarkDirty(workbench);

            SceneView.RepaintAll();

        }



        static void MarkDirty(MapGeneratorWorkbench workbench)

        {

            EditorUtility.SetDirty(workbench);

            if (workbench.TryGetComponent<StageBuilder>(out var builder))

                EditorUtility.SetDirty(builder);



            if (workbench.gameObject.scene.IsValid())

                EditorSceneManager.MarkSceneDirty(workbench.gameObject.scene);

        }

    }

}

#endif


