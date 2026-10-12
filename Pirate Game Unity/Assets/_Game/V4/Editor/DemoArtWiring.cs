using System.Collections.Generic;
using System.IO;
using System.Linq;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.UI;
using RoguePirates.V4.Presentation.Hud;
using RoguePirates.V4.Presentation.Menu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// Wires the art team's gameplay-branch assets into V4 (demo, 30 Sep 2026), all from their files, so it can be re-run
    /// after the artists change them: the main menu and Captain select panels (and the 3D backdrops from their UI scene),
    /// the SaltWind HUD with the Captain portraits, sails and flags, the new weapon and item icons, the level-up card
    /// backgrounds, the treasure chest, repair wreck, treasure map and cursed tombstone landmarks, and the VFX profiles.
    /// </summary>
    public static class DemoArtWiring
    {
        const string DemoFolder = "Assets/_Game/V4/Demo";
        const string ResourcesFolder = "Assets/_Game/V4/Resources";
        const string ArtScene = "Assets/_Game/Scenes/UI.unity";
        const string CharacterUi = "Assets/UI/New_items/character ui";

        [MenuItem("Tools/Rogue Pirates/Demo/Wire Art Team Assets")]
        public static void WireAll()
        {
            var log = new List<string>();
            WireMenu(log);
            WireHud(log);
            WireIcons(log);
            WireCards(log);
            WireLandmarks(log);
            var profiles = VfxProfileEditorWindow.LoadOrCreate();
            log.Add("VFX profiles: " + VfxProfileEditorWindow.Sync(profiles) + " entries added");
            SeedExamples(profiles, log);
            AssetDatabase.SaveAssets();
            Debug.Log("[DemoArtWiring]\n" + string.Join("\n", log));
        }

        static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) Debug.LogWarning("[DemoArtWiring] missing " + path);
            return a;
        }

        static T Settings<T>(string path) where T : ScriptableObject
        {
            var s = AssetDatabase.LoadAssetAtPath<T>(path);
            if (s != null) return s;
            s = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(s, path);
            return s;
        }

        // ---- Menu -----------------------------------------------------------------------------------------------------

        static void WireMenu(List<string> log)
        {
            if (!AssetDatabase.IsValidFolder(DemoFolder)) AssetDatabase.CreateFolder("Assets/_Game/V4", "Demo");
            var s = Settings<ArtMenuSettings>(ResourcesFolder + "/V4ArtMenuSettings.asset");
            s.mainMenuPanel = Load<GameObject>("Assets/UI/MainMenu/Prefabs/MainMenuPanel.prefab");
            s.characterSelectionPanel = Load<GameObject>("Assets/UI/CharacterSelection/Prefabs/Panels/CharacterSelectionPanel.prefab");
            s.setSailNoText = FindSprite("Assets/UI", "set sail button no txt");
            s.font = Load<TMPro.TMP_FontAsset>("Assets/UI/New_items/Font/Times Regular SDF.asset");
            s.mainMenuBackdrop = BackdropPrefab("MainMenuSetup", DemoFolder + "/MainMenuBackdrop.prefab", log);
            s.characterSelectBackdrop = BackdropPrefab("CharacterSelection Setup", DemoFolder + "/CharacterSelectBackdrop.prefab", log);
            EditorUtility.SetDirty(s);
            log.Add($"Menu: panels {(s.Usable ? "ok" : "MISSING")}, backdrops {(s.mainMenuBackdrop != null)}/{(s.characterSelectBackdrop != null)}");
        }

        /// <summary>A prefab of a root object in the art team's UI scene (opened additively, then closed unchanged).</summary>
        static GameObject BackdropPrefab(string rootName, string prefabPath, List<string> log)
        {
            if (!File.Exists(ArtScene)) return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var scene = EditorSceneManager.OpenScene(ArtScene, OpenSceneMode.Additive);
            try
            {
                var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == rootName);
                if (root == null) { log.Add("Menu: no " + rootName + " in " + ArtScene); return null; }
                var copy = Object.Instantiate(root);
                copy.name = rootName;
                copy.SetActive(true);
                // The art scene's cameras followed V3 targets; the menu backdrop camera stays where the artists put it.
                foreach (var follow in copy.GetComponentsInChildren<Foundation.Player.FollowCamera>(true)) Object.DestroyImmediate(follow);
                foreach (var listener in copy.GetComponentsInChildren<AudioListener>(true)) Object.DestroyImmediate(listener);
                var prefab = PrefabUtility.SaveAsPrefabAsset(copy, prefabPath);
                Object.DestroyImmediate(copy);
                return prefab;
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        // ---- HUD ------------------------------------------------------------------------------------------------------

        static void WireHud(List<string> log)
        {
            var s = Settings<SaltWindHudSettings>(ResourcesFolder + "/V4SaltWindHudSettings.asset");
            s.hudPrefab = Load<GameObject>("Assets/UI/SaltWindHUD/Prefabs/Screens/HUD_SaltWind.prefab");
            s.markerDone = Load<Sprite>("Assets/UI/New_items/ui/red skull.png");
            s.markerOpen = Load<Sprite>("Assets/UI/New_items/ui/skull.png");
            var entries = new List<SaltWindHudSettings.Portrait>();
            foreach (var guid in AssetDatabase.FindAssets("t:CaptainConfig", new[] { "Assets/_Game/V4/Data/Build/Captains" }))
            {
                var c = AssetDatabase.LoadAssetAtPath<CaptainConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (c == null || string.IsNullOrEmpty(c.displayName)) continue;
                var folder = CharacterUi + "/" + c.displayName;
                if (!AssetDatabase.IsValidFolder(folder)) { log.Add("HUD: no art folder for " + c.displayName); continue; }
                entries.Add(new SaltWindHudSettings.Portrait
                {
                    captainId = c.legacyId,
                    sprite = FindSprite(folder, "select"),
                    sail = AssetDatabase.LoadAssetAtPath<Material>(folder + "/_Sail_Mat.mat"),
                    flag = AssetDatabase.LoadAssetAtPath<Material>(folder + "/_Flag_Mat.mat"),
                });
            }
            s.portraits = entries.ToArray();
            EditorUtility.SetDirty(s);
            log.Add($"HUD: prefab {(s.hudPrefab != null)}, {entries.Count} Captains (portrait, sail, flag)");
        }

        // ---- Icons and cards ---------------------------------------------------------------------------------------------

        static string Norm(string s) => new string((s ?? "").ToLowerInvariant().Replace("_s ", "s ").Where(char.IsLetterOrDigit).ToArray());

        static Sprite Match(string folder, string id)
        {
            var key = Norm(id);
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Norm(Path.GetFileNameWithoutExtension(path));
                if (name.Length == 0) continue;
                if (name == key || key.StartsWith(name) || name.StartsWith(key)) return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return null;
        }

        /// <summary>Hackathon (13 Oct): the art team's icon set, one file per content id (Weapons/, Items/, Logbooks/;
        /// Unmapped/ holds the art whose content the owner has not named yet). These win over the older New_items files.</summary>
        const string IconsFolder = "Assets/_Game/V4/Demo/Art/Icons";

        static Sprite ById(string kind, string id) =>
            string.IsNullOrEmpty(id) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(IconsFolder + "/" + kind + "/" + id + ".png");

        [MenuItem("Tools/Rogue Pirates/Demo/Wire Icons")]
        public static void WireIconsOnly()
        {
            var log = new List<string>();
            WireIcons(log);
            AssetDatabase.SaveAssets();
            Debug.Log("[DemoArtWiring]\n" + string.Join("\n", log));
        }

        static void WireIcons(List<string> log)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<IconCatalog>(ResourcesFolder + "/V4IconCatalog.asset");
            if (catalog == null) { log.Add("Icons: no catalog"); return; }
            int w = 0, i = 0;
            for (var k = 0; k < catalog.weapons.Count; k++)
            {
                var sprite = ById("Weapons", catalog.weapons[k].id) ?? Match("Assets/UI/New_items/weapons", catalog.weapons[k].id);
                if (sprite != null) { catalog.weapons[k] = new IconCatalog.Entry { id = catalog.weapons[k].id, sprite = sprite }; w++; }
            }
            for (var k = 0; k < catalog.items.Count; k++)
            {
                var sprite = ById("Items", catalog.items[k].id) ?? Match("Assets/UI/New_items/Items", catalog.items[k].id);
                if (sprite != null) { catalog.items[k] = new IconCatalog.Entry { id = catalog.items[k].id, sprite = sprite }; i++; }
            }
            // Playtest (3 Oct): Logbook icons by file name from New_items/Logbooks (one entry per Logbook; no file: the HUD and cards show the name).
            var lb = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:LogbookConfig"))
            {
                var logbook = AssetDatabase.LoadAssetAtPath<LogbookConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (logbook == null || string.IsNullOrEmpty(logbook.logbookId)) continue;
                var index = catalog.logbooks.FindIndex(e => e.id == logbook.logbookId);
                if (index < 0) { catalog.logbooks.Add(new IconCatalog.Entry { id = logbook.logbookId }); index = catalog.logbooks.Count - 1; }
                var sprite = ById("Logbooks", logbook.logbookId);
                if (sprite == null) sprite = AssetDatabase.IsValidFolder("Assets/UI/New_items/Logbooks") ? Match("Assets/UI/New_items/Logbooks", logbook.logbookId) : null;
                if (sprite == null) sprite = AssetDatabase.IsValidFolder("Assets/UI/New_items/Logbooks") ? Match("Assets/UI/New_items/Logbooks", logbook.displayName) : null;
                catalog.logbooks[index] = new IconCatalog.Entry { id = logbook.logbookId, sprite = sprite };
                if (sprite != null) lb++;
            }
            EditorUtility.SetDirty(catalog);
            log.Add($"Icons: {w}/{catalog.weapons.Count} weapons, {i}/{catalog.items.Count} items, {lb}/{catalog.logbooks.Count} Logbooks from Demo/Art/Icons and New_items");
        }

        static void WireCards(List<string> log)
        {
            var settings = Resources.Load<LevelUpCardArtSettings>("LevelUpCardArtSettings");
            if (settings == null || settings.rarityArt == null) { log.Add("Cards: no LevelUpCardArtSettings"); return; }
            var files = new Dictionary<Rarity, string>
            {
                [Rarity.Common] = "common", [Rarity.Uncommon] = "uncommon", [Rarity.Rare] = "rare", [Rarity.Epic] = "epic", [Rarity.Legendary] = "legendary 2",
            };
            var n = 0;
            for (var k = 0; k < settings.rarityArt.Length; k++)
            {
                if (!files.TryGetValue(settings.rarityArt[k].rarity, out var file)) continue;
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/New_items/Upgrade Cards/" + file + ".png");
                if (sprite == null) continue;
                settings.rarityArt[k].background = sprite;
                settings.rarityArt[k].effectOverlay = null; // the art team's cards are complete; V4's frame overlay hid them
                n++;
            }
            settings.artTextColor = new Color(0.24f, 0.12f, 0.04f, 1f);
            EditorUtility.SetDirty(settings);
            log.Add($"Cards: {n} rarity backgrounds from New_items/Upgrade Cards");
        }

        // ---- Landmarks ----------------------------------------------------------------------------------------------------

        static void WireLandmarks(List<string> log)
        {
            var juice = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
            if (juice == null) { log.Add("Landmarks: no DemoJuiceSettings"); return; }
            // Once the POI prefabs exist (Build Demo Scenes), they are the artists' and are left alone.
            bool Poi(GameObject g) => g != null && AssetDatabase.GetAssetPath(g).StartsWith("Assets/_Game/V4/Demo/POI");
            var chest = Load<GameObject>("Assets/GameObject/Treasure_chest.prefab");
            if (chest != null && !Poi(juice.salvageChestPrefab)) { juice.salvageChestPrefab = chest; juice.salvageChestGlowPrefab = null; }
            if (!Poi(juice.gildedWreckPrefab)) juice.gildedWreckPrefab = Load<GameObject>("Assets/GameObject/repair_wreck.prefab");
            if (!Poi(juice.cursedIdolPrefab)) juice.cursedIdolPrefab = Load<GameObject>("Assets/GameObject/Treasure map.prefab");
            if (!Poi(juice.merchantSkiffPrefab)) juice.merchantSkiffPrefab = Load<GameObject>("Assets/GameObject/pirate_tombstone.prefab");
            EditorUtility.SetDirty(juice);
            log.Add("Landmarks: treasure chest, repair wreck, treasure map, cursed tombstone");
        }

        /// <summary>A few profiles that show the art team's effects in the demo; a slot an artist already set is left alone.</summary>
        static void SeedExamples(VfxProfileLibrary library, List<string> log)
        {
            var tornado = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TornadoVFX/Tornado_VFX.prefab");
            var whirlpool = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/VFX/Whirlpool/PF_Whirlpool.prefab");
            var greenFire = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/FX_Fire_04_2_GREEN.prefab");
            var fire = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hun0FX/FX/FireFX_vol1/Prefabs/FX_Fire_05.prefab");
            var n = 0;
            // By display name: V4 code holds no content ids (V4EnemyM2Tests.NoV4Code_UsesContentIds).
            void Set(VfxSubject subject, string name, VfxSlotKind kind, GameObject prefab, float scale, float lifetime, bool replace)
            {
                var slot = library.profiles.FirstOrDefault(p => p != null && p.subject == subject && p.displayName == name)?.Slot(kind);
                if (slot == null || prefab == null || slot.prefab != null) return;
                slot.prefab = prefab; slot.scale = scale; slot.lifetime = lifetime; slot.replaceDefault = replace; slot.enabled = true;
                n++;
            }
            Set(VfxSubject.Enemy, "Barnacle King", VfxSlotKind.Spawn, tornado, 1.5f, 4f, false);
            Set(VfxSubject.Enemy, "Barnacle King", VfxSlotKind.Death, fire, 2.5f, 4f, false);
            Set(VfxSubject.Enemy, "Kraken Spawn", VfxSlotKind.Spawn, whirlpool, 0.6f, 3f, false);
            Set(VfxSubject.Enemy, "Reef Crab", VfxSlotKind.Spawn, whirlpool, 0.8f, 3f, false);
            Set(VfxSubject.Enemy, "Reef Crab", VfxSlotKind.Death, greenFire, 1.5f, 3f, false);
            Set(VfxSubject.PlayerShip, "Player ship", VfxSlotKind.Death, fire, 2f, 5f, false);
            EditorUtility.SetDirty(library);
            log.Add("VFX profiles: " + n + " example slots set (boss tornado and fire, kraken and crab whirlpools, ship fire)");
        }

        static Sprite FindSprite(string folder, string nameContains)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path).IndexOf(nameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return null;
        }
    }
}
