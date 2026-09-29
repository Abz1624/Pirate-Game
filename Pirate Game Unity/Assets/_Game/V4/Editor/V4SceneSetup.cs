using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
                    var legacyProfile = so.FindProperty("legacyBaseProfile"); // M1-M13 only: M14 builds the profile from config
                    if (legacyProfile != null) legacyProfile.objectReferenceValue = profile;
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

        /// <summary>
        /// M3 (V4SaltwindRun and V4ModuleLab, idempotent): the kept EncounterSpawner gets the encounter-only schedule
        /// (no ambient entries), so it can never run ambient spawns even if V4's schedule install fails; the ambient
        /// spawn slot is added to /Systems and placed in the frame right after the run clock (see
        /// <see cref="PlaceAmbientSlotAfterRunClock"/>).
        /// </summary>
        public static string ApplyM3()
        {
            var log = new List<string>();
            if (AssetDatabase.LoadAssetAtPath<HighSeas.Enemies.EncounterSpawnSchedule>(V3ConfigMigrator.EncounterOnlySchedulePath) == null)
                return "missing encounter-only schedule (run V3ConfigMigrator.ApplyM3 first)";
            foreach (var path in new[] { V4SceneNames.SaltwindRunPath, V4SceneNames.ModuleLabPath })
                Edit(path, log, () =>
                {
                    var schedule = AssetDatabase.LoadAssetAtPath<HighSeas.Enemies.EncounterSpawnSchedule>(V3ConfigMigrator.EncounterOnlySchedulePath);
                    var systems = GameObject.Find("Systems");
                    var spawner = systems != null ? systems.GetComponent<HighSeas.Enemies.EncounterSpawner>() : null;
                    if (spawner == null) { log.Add("  no EncounterSpawner on /Systems"); return; }
                    var so = new SerializedObject(spawner);
                    so.FindProperty("schedule").objectReferenceValue = schedule;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    log.Add("  EncounterSpawner.schedule = V4EncounterOnlySchedule");

                    // (M14.14: the ambient slot placed here is gone; the frame pipeline runs the ambient tick.)
                });
            return string.Join("\n", log);
        }

        /// <summary>
        /// M5: the V4 weapon tick takes WeaponController's place in the frame. The controller becomes the switched-off
        /// loadout record (it never updates, so its own place no longer matters): its file ID moves to a free one and the
        /// new "weapons" LegacyUpdateSlot on /Systems takes the old one, so the weapon tick runs where WeaponController.Update
        /// ran (after LevelSystem, before PickupWorld). Idempotent.
        /// </summary>
        public static string ApplyM5()
        {
            var log = new List<string>();
            foreach (var path in new[] { V4SceneNames.SaltwindRunPath, V4SceneNames.ModuleLabPath })
            {
                Edit(path, log, () =>
                {
                    var systems = GameObject.Find("Systems");
                    var record = systems != null ? systems.GetComponent<HighSeas.Weapons.WeaponController>() : null;
                    if (record == null) { log.Add("  no WeaponController on /Systems"); return; }
                    if (record.enabled) { record.enabled = false; log.Add("  WeaponController switched off (loadout record)"); }
                    // (M14.14: the weapons slot added here is gone; the frame pipeline runs the weapon tick.)
                });
            }
            return string.Join("\n", log);
        }

        /// <summary>
        /// M6: the V3 views the V4 presenters replace leave the run scenes: V3AuthoredGameplayHud on /Canvas (the HUD
        /// presenter binds that canvas instead) and the two V3RunResultsScreen copies on /Canvas and /RunCanvas.
        /// Removing a component from the /Canvas prefab instance is saved as a scene override; the prefab is untouched.
        /// WeaponJuiceController and ProjectileWorld stay on /Systems: the juice controller is switched off at run start
        /// (the V4 weapon presenter draws the weapon effects) but SaltwindCombatEffects still calls its blast effect (the
        /// Hellfire burning oil is no longer played since the M8 playtest, R-104); ProjectileWorld (unticked and empty
        /// since M5) was read by CombatTelemetryDriver until M8.
        /// </summary>
        public static string ApplyM6()
        {
            var log = new List<string>();
            foreach (var path in new[] { V4SceneNames.SaltwindRunPath, V4SceneNames.ModuleLabPath })
            {
                Edit(path, log, () =>
                {
                    RemoveAll<HighSeas.V3.V3AuthoredGameplayHud>(log);
                    RemoveAll<HighSeas.V3.V3RunResultsScreen>(log);
                });
            }
            return string.Join("\n", log);
        }

        static void RemoveAll<T>(List<string> log) where T : Component
        {
            foreach (var c in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var owner = c.gameObject.name;
                Object.DestroyImmediate(c); // also removes components the /Canvas prefab itself has (saved as a removed-component override)
                log.Add($"  removed {typeof(T).Name} from /{owner}");
            }
        }

        /// <summary>
        /// M7: POIManager leaves the run scenes. A stage LegacyUpdateSlot takes its file ID (so the V4 stage manager tick
        /// runs where POIManager.Update ran), then POIManager is removed. EncounterSpawner is switched off: the V4 encounter
        /// executor runs the Beacon boss and Powder Cache, and V3's Dead Calm still writes its rate multiplier to it
        /// (register D4) until M9.
        /// </summary>
        public static string ApplyM7()
        {
            var log = new List<string>();
            foreach (var path in new[] { V4SceneNames.SaltwindRunPath, V4SceneNames.ModuleLabPath })
            {
                Edit(path, log, () =>
                {
                    var systems = GameObject.Find("Systems");
                    if (systems == null) { log.Add("  no /Systems"); return; }
                    // (M14.14: the stage slot added here is gone; the frame pipeline runs the stage tick.)
                    var spawner = systems.GetComponent<HighSeas.Enemies.EncounterSpawner>();
                    if (spawner != null && spawner.enabled) { spawner.enabled = false; log.Add("  EncounterSpawner switched off (Dead Calm sink)"); }
                });
                Edit(path, log, () => RemoveAll<HighSeas.POI.POIManager>(log));
            }
            return string.Join("\n", log);
        }

        /// <summary>
        /// M8: V3's EnemyWorld and StatusController leave the run scenes (the V4 enemy world replaces them; neither had an
        /// Update, so the frame order of the remaining components is unchanged). HostileProjectileWorld was only ever
        /// added at runtime.
        /// </summary>
        public static string ApplyM8()
        {
            var log = new List<string>();
            foreach (var path in new[] { V4SceneNames.SaltwindRunPath, V4SceneNames.ModuleLabPath })
            {
                Edit(path, log, () =>
                {
                    RemoveAll<HighSeas.Enemies.EnemyWorld>(log);
                    RemoveAll<HighSeas.Combat.StatusController>(log);
                    RemoveAll<HighSeas.Combat.HostileProjectileWorld>(log);
                });
            }
            return string.Join("\n", log);
        }

        /// <summary>
        /// M9: V3's build systems leave the run scenes — LevelSystem, the switched-off WeaponController record, PickupWorld
        /// and the switched-off EncounterSpawner (Dead Calm's sink). The V4 progression, pickup and powerup modules replace
        /// them. None of them updated (PickupWorld was ticked by the bootstrap, the other two were disabled, LevelSystem
        /// has no Update), and removing components keeps the other file IDs, so the frame order is unchanged.
        /// </summary>
        public static string ApplyM9()
        {
            var log = new List<string>();
            foreach (var path in new[] { V4SceneNames.SaltwindRunPath, V4SceneNames.ModuleLabPath })
            {
                Edit(path, log, () =>
                {
                    RemoveAll<HighSeas.UI.LevelSystem>(log);
                    RemoveAll<HighSeas.Weapons.WeaponController>(log);
                    RemoveAll<HighSeas.Pickups.PickupWorld>(log);
                    RemoveAll<HighSeas.Enemies.EncounterSpawner>(log);
                });
            }
            return string.Join("\n", log);
        }

        /// <summary>
        /// M14.9b: the last V3 effect and event components leave the run scenes — JuiceVfxBroker, CombatVfxController,
        /// WeaponJuiceController and PlayerJuiceController (every effect is a V4 presenter on the event stream), the three
        /// MetaSystems components (TriggerBus, whose raises are gone, and LootService / QuestService, which only V3's
        /// POIManager read) and ProjectileWorld (empty and unticked since M5). Every remaining V3 reader of them uses
        /// <c>?.</c> or a null check (audit in the M14 checklist), and none of them is in the V4 scenes.
        /// </summary>
        public static string ApplyM14Effects()
        {
            var log = new List<string>();
            foreach (var path in new[] { V4SceneNames.SaltwindRunPath, V4SceneNames.ModuleLabPath })
            {
                Edit(path, log, () =>
                {
                    RemoveAll<HighSeas.VFX.JuiceVfxBroker>(log);
                    RemoveAll<HighSeas.VFX.CombatVfxController>(log);
                    RemoveAll<HighSeas.VFX.WeaponJuiceController>(log);
                    RemoveAll<HighSeas.VFX.PlayerJuiceController>(log);
                    RemoveAll<HighSeas.Meta.TriggerBus>(log);
                    RemoveAll<HighSeas.Meta.LootService>(log);
                    RemoveAll<HighSeas.Meta.QuestService>(log);
                    RemoveAll<HighSeas.Weapons.ProjectileWorld>(log);
                    RemoveAll<HighSeas.VFX.PlayerHealthBarBinder>(log);
                    RemoveAll<HighSeas.V3.V3PlayBoundsWallVisual>(log);
                    RemoveAll<HighSeas.V3.V3SaltwindStormRingVisual>(log);
                    // M14.11: the level-up presenter takes the LevelUpPanel object over and lays out its own cards.
                    RemoveAll<HighSeas.UI.LevelUpModal>(log);
                    // M14.11: V3's RunHud (hidden under the authored HUD), its run-end screen and main-menu controller on /RunCanvas.
                    RemoveAll<HighSeas.UI.RunHud>(log);
                    RemoveAll<HighSeas.UI.RunEndScreen>(log);
                    RemoveAll<HighSeas.UI.MainMenuController>(log);
                    // M14.14: the legacy update slots (ambient, weapons, stage) left the code; their components are
                    // missing scripts now. The frame pipeline runs those ticks.
                    foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        var removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                        if (removed > 0) log.Add($"  removed {removed} missing-script component(s) from /{go.name}");
                    }
                });
            }
            return string.Join("\n", log);
        }

        /// <summary>
        /// M14.13: the V4 menu reads the Captain configs and lock rules: its controller gets the run's config set.
        /// </summary>
        public static string ApplyM14Menu()
        {
            var log = new List<string>();
            Edit(V4SceneNames.MainMenuPath, log, () =>
            {
                var set = AssetDatabase.LoadAssetAtPath<RoguePirates.V4.Config.ConfigSet>("Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset");
                foreach (var menu in Object.FindObjectsByType<V4MainMenuController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var so = new SerializedObject(menu);
                    so.FindProperty("configSet").objectReferenceValue = set;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    log.Add($"  {menu.name}: config set {(set != null ? set.name : "MISSING")}");
                }
                RemoveAll<HighSeas.V3.V3MainMenuCharacterSelect>(log);
            });
            return string.Join("\n", log);
        }

        /// <summary>
        /// M2: the Module Lab is a copy of V4SaltwindRun with V4Game in lab mode (replaces the M0 copy of the legacy
        /// weapon/enemy playtest scene). Overwrites V4ModuleLab.
        /// </summary>
        public static string CreateModuleLab()
        {
            var log = new List<string>();
            if (!Copy(V4SceneNames.SaltwindRunPath, V4SceneNames.ModuleLabPath, overwrite: true, log)) return string.Join("\n", log);
            Edit(V4SceneNames.ModuleLabPath, log, () =>
            {
                foreach (var game in Object.FindObjectsByType<V4Game>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var so = new SerializedObject(game);
                    so.FindProperty("moduleLab").boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    log.Add($"  {game.gameObject.name}: V4Game lab mode on");
                }
            });
            return string.Join("\n", log);
        }

        static void AddToBuild(List<string> log, params string[] paths)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            var stale = scenes.Where(s => paths.Contains(s.path) && s.guid.ToString() != AssetDatabase.AssetPathToGUID(s.path)).ToList();
            if (stale.Count > 0)
            {
                // A re-created scene gets a new GUID. Replacing an entry with the same path is ignored by the setter,
                // so write the list without the stale entries first, then put them back with the current GUID.
                EditorBuildSettings.scenes = scenes.Except(stale).ToArray();
                foreach (var s in stale)
                {
                    scenes[scenes.IndexOf(s)] = new EditorBuildSettingsScene(s.path, s.enabled);
                    log.Add($"build list GUID refreshed: {s.path}");
                }
            }
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
