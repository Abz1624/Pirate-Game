using System.Collections.Generic;
using System.Linq;
using HighSeas.V3;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// Creates the V4 scenes as copies of the V3 scenes (fresh GUIDs) and swaps the V3 composition components for the
    /// V4 ones, keeping serialized references and component order. Idempotent: existing V4 scenes are left alone
    /// unless <c>overwrite</c> is set. Run from the menu or `unity command eval_file`.
    /// </summary>
    public static class V4SceneSetup
    {
        const string V3Folder = "Assets/_Game/Scenes/v3";
        const string LabSource = "Assets/_Game/Scenes/Tests/Test_WeaponEnemy.unity";

        [MenuItem("RoguePirates/V4/Create V4 Scenes")]
        public static void CreateFromMenu() => Debug.Log(CreateAll(false));

        public static string CreateAll(bool overwrite)
        {
            var log = new List<string>();
            if (!AssetDatabase.IsValidFolder(V4SceneNames.Folder))
                AssetDatabase.CreateFolder("Assets/_Game/Scenes", "V4");

            if (Copy($"{V3Folder}/V3Boot.unity", V4SceneNames.BootPath, overwrite, log))
                Edit(V4SceneNames.BootPath, log, () => Swap<V3BootLoader, V4BootLoader>(log));
            if (Copy($"{V3Folder}/V3MainMenu.unity", V4SceneNames.MainMenuPath, overwrite, log))
                Edit(V4SceneNames.MainMenuPath, log, () => Swap<V3MainMenuController, V4MainMenuController>(log, "menuCanvas"));
            if (Copy($"{V3Folder}/V3SaltwindRun.unity", V4SceneNames.SaltwindRunPath, overwrite, log))
                Edit(V4SceneNames.SaltwindRunPath, log, () => Swap<V3RunBootstrap, V4Game>(log,
                    "database", "stageBuilder", "player", "islandLayout", "usePlaceholderPoiVisuals"));
            // M0: the lab keeps its legacy playtest bootstrap; V4 lab mode arrives with archetypes in M2.
            Copy(LabSource, V4SceneNames.ModuleLabPath, overwrite, log);

            AddToBuild(log, V4SceneNames.BootPath, V4SceneNames.MainMenuPath, V4SceneNames.SaltwindRunPath, V4SceneNames.ModuleLabPath);
            AssetDatabase.SaveAssets();
            return string.Join("\n", log);
        }

        static bool Copy(string from, string to, bool overwrite, List<string> log)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(to) != null)
            {
                if (!overwrite) { log.Add($"exists: {to}"); return false; }
                AssetDatabase.DeleteAsset(to);
            }
            if (!AssetDatabase.CopyAsset(from, to)) { log.Add($"COPY FAILED: {from} -> {to}"); return false; }
            log.Add($"copied {from} -> {to}");
            return true;
        }

        static void Edit(string path, List<string> log, System.Action edit)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            edit();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.Add($"saved {path}");
        }

        /// <summary>Replaces every TOld with TNew at the same component index, copying the named serialized fields.</summary>
        static void Swap<TOld, TNew>(List<string> log, params string[] fields) where TOld : MonoBehaviour where TNew : MonoBehaviour
        {
            var found = Object.FindObjectsByType<TOld>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (found.Length == 0) { log.Add($"  no {typeof(TOld).Name} found"); return; }
            foreach (var old in found)
            {
                var go = old.gameObject;
                var fresh = go.AddComponent<TNew>();
                var src = new SerializedObject(old);
                var dst = new SerializedObject(fresh);
                foreach (var field in fields)
                {
                    var s = src.FindProperty(field);
                    if (s == null || dst.FindProperty(field) == null) { log.Add($"  field missing: {field}"); continue; }
                    dst.CopyFromSerializedProperty(s);
                }
                dst.ApplyModifiedPropertiesWithoutUndo();

                var components = go.GetComponents<Component>().ToList();
                var target = components.IndexOf(old);
                for (var i = components.IndexOf(fresh); i > target; i--) ComponentUtility.MoveComponentUp(fresh);
                Object.DestroyImmediate(old);
                log.Add($"  {go.name}: {typeof(TOld).Name} -> {typeof(TNew).Name} at index {target}");
            }
        }

        /// <summary>
        /// M1: V4Game holds the config set and the V3 base profile, so the scene's StageBalanceProfileHost goes.
        /// Idempotent.
        /// </summary>
        public static string ApplyM1(string configSetPath, string legacyProfilePath)
        {
            var log = new List<string>();
            if (AssetDatabase.LoadAssetAtPath<RoguePirates.V4.Config.ConfigSet>(configSetPath) == null ||
                AssetDatabase.LoadAssetAtPath<HighSeas.Balance.StageBalanceProfile>(legacyProfilePath) == null)
                return "missing config set or legacy profile";
            Edit(V4SceneNames.SaltwindRunPath, log, () =>
            {
                // Load after OpenScene: opening a scene in Single mode unloads unused assets loaded before it.
                var configSet = AssetDatabase.LoadAssetAtPath<RoguePirates.V4.Config.ConfigSet>(configSetPath);
                var profile = AssetDatabase.LoadAssetAtPath<HighSeas.Balance.StageBalanceProfile>(legacyProfilePath);
                foreach (var game in Object.FindObjectsByType<V4Game>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var so = new SerializedObject(game);
                    so.FindProperty("configSet").objectReferenceValue = configSet;
                    so.FindProperty("legacyBaseProfile").objectReferenceValue = profile;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    log.Add($"  {game.gameObject.name}: V4Game config assigned");
                }
                foreach (var host in Object.FindObjectsByType<HighSeas.Balance.StageBalanceProfileHost>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    log.Add($"  {host.gameObject.name}: StageBalanceProfileHost removed");
                    Object.DestroyImmediate(host);
                }
            });
            return string.Join("\n", log);
        }

        static void AddToBuild(List<string> log, params string[] paths)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            foreach (var path in paths)
            {
                if (scenes.Any(s => s.path == path)) continue;
                scenes.Add(new EditorBuildSettingsScene(path, true));
                log.Add($"build list += {path}");
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
