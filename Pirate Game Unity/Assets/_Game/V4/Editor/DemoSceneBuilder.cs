using System.Collections.Generic;
using System.IO;
using System.Linq;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Presentation.Hud;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// Puts the art team's work into the V4 scenes and prefabs so it can be tweaked in the Editor (demo, 30 Sep 2026):
    /// <list type="bullet">
    /// <item>V4MainMenu becomes the art team's UI scene (water, islands, lighting, the main menu and Captain select
    /// panels, both 3D sets) with the V4 menu controller; its old HUD instance and V3 systems are left out, the Captains'
    /// ships share one position and the lock reason label sits above Set Sail.</item>
    /// <item>V4SaltwindRun gets the HUD_SaltWind prefab on its HUD canvas (level-up cards, item bar and minimap frame included).</item>
    /// <item>One prefab per point of interest in Assets/_Game/V4/Demo/POI (model and effects inside, edited like any
    /// prefab), assigned in DemoJuiceSettings.</item>
    /// </list>
    /// Rebuilding the menu replaces V4MainMenu from the art team's UI scene again (tweak the UI scene, or V4MainMenu and
    /// don't rebuild).
    /// </summary>
    public static class DemoSceneBuilder
    {
        const string ArtScene = "Assets/_Game/Scenes/UI.unity";
        const string MenuScene = "Assets/_Game/Scenes/V4/V4MainMenu.unity";
        const string RunScene = "Assets/_Game/Scenes/V4/V4SaltwindRun.unity";
        const string ConfigSetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        const string PoiFolder = "Assets/_Game/V4/Demo/POI";

        [MenuItem("Tools/Rogue Pirates/Demo/Build Demo Scenes")]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var log = new List<string>();
            BuildPoiPrefabs(log);
            BuildLevelUpCards(log);
            BuildRunScene(log);
            BuildJoystick(log);
            BuildMenuScene(log);
            AssetDatabase.SaveAssets();
            Debug.Log("[DemoSceneBuilder]\n" + string.Join("\n", log));
        }

        // ---- Menu -----------------------------------------------------------------------------------------------------

        public static void BuildMenuScene(List<string> log)
        {
            var scene = EditorSceneManager.OpenScene(ArtScene, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            GameObject Root(string name) => roots.FirstOrDefault(g => g != null && g.name == name);

            // V3's run systems and the HUD mock-up are not part of the menu.
            var systems = Root("Systems");
            if (systems != null) Object.DestroyImmediate(systems);
            var canvasGo = Root("Canvas");
            var canvas = canvasGo != null ? canvasGo.GetComponent<Canvas>() : null;
            if (canvasGo != null)
                foreach (var name in new[] { "HUD_SaltWind", "Image" })
                {
                    var t = canvasGo.transform.Find(name);
                    if (t != null) Object.DestroyImmediate(t.gameObject);
                }
            foreach (var follow in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Foundation.Player.FollowCamera>(true)))
                Object.DestroyImmediate(follow);

            // Every Captain's ship where the art scene shows one (Hank's, the ship that was visible there).
            var select = Root("CharacterSelection Setup");
            if (select != null)
            {
                var boats = select.GetComponentsInChildren<Transform>(true).Where(t => t.name.EndsWith("_Boat")).ToList();
                var reference = boats.FirstOrDefault(b => b.name == "Hank_Boat") ?? boats.FirstOrDefault();
                foreach (var b in boats)
                {
                    b.localPosition = reference.localPosition;
                    b.localRotation = reference.localRotation;
                    b.localScale = reference.localScale;
                }
                select.SetActive(false);
                log.Add($"Menu: {boats.Count} Captain ships aligned to {reference?.name}");
            }
            var main = Root("MainMenuSetup");
            if (main != null) main.SetActive(true);

            // Captain select: the lock reason above Set Sail; the main menu shows first.
            if (canvasGo != null)
            {
                var selectPanel = canvasGo.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "CharacterSelectionPanel");
                var mainPanel = canvasGo.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "MainMenuPanel");
                if (mainPanel != null) mainPanel.gameObject.SetActive(true);
                if (selectPanel != null)
                {
                    selectPanel.gameObject.SetActive(false);
                    var setSail = selectPanel.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "SetSailButton") as RectTransform;
                    if (setSail != null && setSail.parent.Find("LockReason") == null) AddLockReason(setSail);
                }
            }

            // The V4 menu.
            var menu = new GameObject("V4MainMenu");
            var controller = menu.AddComponent<V4MainMenuController>();
            var so = new SerializedObject(controller);
            so.FindProperty("menuCanvas").objectReferenceValue = canvas;
            so.FindProperty("configSet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ConfigSet>(ConfigSetPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, MenuScene);
            log.Add("Menu: " + MenuScene + " rebuilt from " + ArtScene);
        }

        static void AddLockReason(RectTransform setSail)
        {
            var go = new GameObject("LockReason", typeof(RectTransform));
            go.transform.SetParent(setSail.parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = setSail.anchorMin;
            rt.anchorMax = setSail.anchorMax;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(Mathf.Max(360f, setSail.rect.width), 40f);
            rt.anchoredPosition = setSail.anchoredPosition + new Vector2((0.5f - setSail.pivot.x) * setSail.rect.width, (1f - setSail.pivot.y) * setSail.rect.height + 8f);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = "Earn 5,000 gold across voyages";
            t.alignment = TextAlignmentOptions.Center;
            t.fontSize = 24f;
            t.color = new Color(1f, 0.92f, 0.75f);
            t.raycastTarget = false;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/New_items/Font/Times Regular SDF.asset");
            if (font != null) t.font = font;
        }

        // ---- Run --------------------------------------------------------------------------------------------------------

        public static void BuildRunScene(List<string> log)
        {
            var scene = EditorSceneManager.OpenScene(RunScene, OpenSceneMode.Single);
            var canvas = AuthoredHudPresenter.FindAuthoredCanvas();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/SaltWindHUD/Prefabs/Screens/HUD_SaltWind.prefab");
            if (canvas == null || prefab == null) { log.Add("Run: no HUD canvas or HUD_SaltWind prefab"); return; }
            if (canvas.transform.Find("HUD_SaltWind") == null)
            {
                var hud = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
                hud.name = "HUD_SaltWind";
                var rt = (RectTransform)hud.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
                hud.transform.SetAsFirstSibling();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                log.Add("Run: HUD_SaltWind placed on " + canvas.name + " in " + RunScene);
            }
            else log.Add("Run: HUD_SaltWind already in the scene");
        }

        // ---- Level-up cards --------------------------------------------------------------------------------------------

        const string CardFolder = "Assets/_Game/V4/Demo/LevelUpCards";

        [MenuItem("Tools/Rogue Pirates/Demo/Build Level-Up Cards")]
        public static void BuildLevelUpCardsMenu()
        {
            var log = new List<string>();
            BuildLevelUpCards(log);
            AssetDatabase.SaveAssets();
            Debug.Log("[DemoSceneBuilder] " + string.Join(" ", log));
        }

        /// <summary>
        /// One level-up card per weapon: a variant of the art team's UpgradeCard with the weapon's icon (Background/Icon),
        /// listed in V4SaltWindHudSettings. The icon set on a card is the one that weapon's level-up cards show; existing
        /// cards are kept (the artists' edits), new weapons get one.
        /// </summary>
        public static void BuildLevelUpCards(List<string> log)
        {
            var baseCard = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/SaltWindHUD/Prefabs/Elements/UpgradeCard.prefab");
            var settings = AssetDatabase.LoadAssetAtPath<SaltWindHudSettings>("Assets/_Game/V4/Resources/V4SaltWindHudSettings.asset");
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(ConfigSetPath);
            if (baseCard == null || settings == null || set == null) { log.Add("Cards: missing UpgradeCard, settings or config set"); return; }
            if (!AssetDatabase.IsValidFolder(CardFolder)) AssetDatabase.CreateFolder("Assets/_Game/V4/Demo", "LevelUpCards");
            var icons = IconCatalog.Load();
            var entries = new List<SaltWindHudSettings.WeaponCard>();
            var made = 0;
            foreach (var w in set.All<WeaponConfig>().Where(w => w != null && !string.IsNullOrEmpty(w.weaponId)).OrderBy(w => w.displayName))
            {
                var path = CardFolder + "/LevelUpCard_" + (string.IsNullOrEmpty(w.displayName) ? w.weaponId : w.displayName) + ".prefab";
                var card = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (card == null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(baseCard);
                    var sprite = icons != null ? icons.Weapon(w.weaponId) : null;
                    foreach (var name in new[] { "Background/Icon", "Background/Shadow" })
                    {
                        var image = instance.transform.Find(name)?.GetComponent<UnityEngine.UI.Image>();
                        if (image != null && sprite != null) image.sprite = sprite;
                    }
                    var title = instance.transform.Find("Background/Title")?.GetComponent<TMP_Text>();
                    if (title != null) title.text = w.displayName;
                    card = PrefabUtility.SaveAsPrefabAsset(instance, path);
                    Object.DestroyImmediate(instance);
                    made++;
                }
                entries.Add(new SaltWindHudSettings.WeaponCard { weaponId = w.weaponId, cardPrefab = card });
            }
            settings.weaponCards = entries.ToArray();
            EditorUtility.SetDirty(settings);
            log.Add($"Cards: {entries.Count} weapon cards in {CardFolder} ({made} new)");
        }

        // ---- Touch joystick -----------------------------------------------------------------------------------------------

        const string JoystickArt = "Assets/_Game/Art/Joysticks/";

        [MenuItem("Tools/Rogue Pirates/Demo/Build Touch Joystick")]
        public static void BuildJoystickMenu()
        {
            var log = new List<string>();
            BuildJoystick(log);
            Debug.Log("[DemoSceneBuilder]\n" + string.Join("\n", log));
        }

        /// <summary>
        /// The run scene's JoystickBG becomes a floating stick: an invisible touch zone over the left half of the screen,
        /// with the art team's base (compass) and knob (bubble) that appear under the thumb. Shown only in the mobile app.
        /// </summary>
        public static void BuildJoystick(List<string> log)
        {
            Sprite AsSprite(string file)
            {
                var path = JoystickArt + file;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            var baseSprite = AsSprite("Rouge_Pirate_ui_joystick.png");
            var knobSprite = AsSprite("Rouge_Pirate_ui_joystick_2.png");

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != RunScene) scene = EditorSceneManager.OpenScene(RunScene, OpenSceneMode.Single);
            var zone = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RectTransform>(true)).FirstOrDefault(t => t.name == "JoystickBG");
            if (zone == null) { log.Add("Joystick: no JoystickBG in " + RunScene); return; }
            var stick = zone.GetComponent<Foundation.Platform.VirtualJoystick>();

            // The zone: the left half of the screen, invisible but touchable.
            zone.anchorMin = Vector2.zero;
            zone.anchorMax = new Vector2(0.5f, 1f);
            zone.pivot = new Vector2(0.5f, 0.5f);
            zone.offsetMin = Vector2.zero;
            zone.offsetMax = Vector2.zero;
            var zoneImage = zone.GetComponent<UnityEngine.UI.Image>();
            if (zoneImage == null) zoneImage = zone.gameObject.AddComponent<UnityEngine.UI.Image>();
            zoneImage.sprite = null;
            zoneImage.color = new Color(1f, 1f, 1f, 0f);
            zoneImage.raycastTarget = true;
            foreach (Transform child in zone) if (child.name != "Base") child.gameObject.SetActive(false);

            var baseRt = zone.Find("Base") as RectTransform;
            if (baseRt == null)
            {
                baseRt = new GameObject("Base", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
                baseRt.SetParent(zone, false);
            }
            baseRt.anchorMin = baseRt.anchorMax = baseRt.pivot = new Vector2(0.5f, 0.5f);
            baseRt.sizeDelta = new Vector2(260f, 260f);
            var baseImage = baseRt.GetComponent<UnityEngine.UI.Image>();
            baseImage.sprite = baseSprite;
            baseImage.color = new Color(1f, 1f, 1f, 0.85f);
            baseImage.raycastTarget = false;
            var knob = baseRt.Find("Knob") as RectTransform;
            if (knob == null)
            {
                knob = new GameObject("Knob", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
                knob.SetParent(baseRt, false);
            }
            knob.anchorMin = knob.anchorMax = knob.pivot = new Vector2(0.5f, 0.5f);
            knob.sizeDelta = new Vector2(110f, 110f);
            knob.anchoredPosition = Vector2.zero;
            var knobImage = knob.GetComponent<UnityEngine.UI.Image>();
            knobImage.sprite = knobSprite;
            knobImage.raycastTarget = false;

            var so = new SerializedObject(stick);
            so.FindProperty("background").objectReferenceValue = baseRt;
            so.FindProperty("handle").objectReferenceValue = knob;
            so.FindProperty("maxRadius").floatValue = 95f;
            so.FindProperty("floating").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            zone.gameObject.SetActive(false); // switched on at run start in the mobile app only
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.Add("Joystick: floating stick on the left half of the screen (mobile app only)");
        }

        // ---- Points of interest -------------------------------------------------------------------------------------

        public static void BuildPoiPrefabs(List<string> log)
        {
            var juice = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
            if (juice == null) { log.Add("POI: no DemoJuiceSettings"); return; }
            if (!AssetDatabase.IsValidFolder(PoiFolder)) AssetDatabase.CreateFolder("Assets/_Game/V4/Demo", "POI");
            var made = 0;
            GameObject Make(string name, GameObject model, float scale)
            {
                var path = PoiFolder + "/POI_" + name + ".prefab";
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (existing != null) return existing; // the artists' edits are kept
                var root = new GameObject("POI_" + name);
                if (model != null)
                {
                    var child = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                    child.transform.localPosition = Vector3.zero;
                    child.transform.localScale = model.transform.localScale * scale;
                }
                var asset = PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
                made++;
                return asset;
            }
            bool IsPoi(GameObject g) => g != null && AssetDatabase.GetAssetPath(g).StartsWith(PoiFolder);
            GameObject Art(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (!IsPoi(juice.salvageChestPrefab)) { juice.salvageChestPrefab = Make("SalvageChest", juice.salvageChestPrefab != null ? juice.salvageChestPrefab : Art("Assets/GameObject/Treasure_chest.prefab"), juice.salvageChestScale); juice.salvageChestScale = 1f; }
            if (!IsPoi(juice.gildedWreckPrefab)) { juice.gildedWreckPrefab = Make("RepairWreck", juice.gildedWreckPrefab != null ? juice.gildedWreckPrefab : Art("Assets/GameObject/repair_wreck.prefab"), juice.gildedWreckScale); juice.gildedWreckScale = 1f; }
            if (!IsPoi(juice.cursedIdolPrefab)) { juice.cursedIdolPrefab = Make("TreasureChart", juice.cursedIdolPrefab != null ? juice.cursedIdolPrefab : Art("Assets/GameObject/Treasure map.prefab"), juice.cursedIdolScale); juice.cursedIdolScale = 1f; }
            if (!IsPoi(juice.merchantSkiffPrefab)) { juice.merchantSkiffPrefab = Make("PowderCache", juice.merchantSkiffPrefab != null ? juice.merchantSkiffPrefab : Art("Assets/GameObject/pirate_tombstone.prefab"), juice.merchantSkiffScale); juice.merchantSkiffScale = 1f; }
            if (!IsPoi(juice.lighthousePrefab)) { juice.lighthousePrefab = Make("LighthouseOfFortune", juice.lighthousePrefab, juice.lighthouseScale); juice.lighthouseScale = 1f; }
            if (!IsPoi(juice.trialBuoyPrefab)) { juice.trialBuoyPrefab = Make("SignalBuoy", juice.trialBuoyPrefab, juice.trialBuoyScale); juice.trialBuoyScale = 1f; }
            if (!IsPoi(juice.bossBeaconPrefab)) { juice.bossBeaconPrefab = Make("TreasuryBeacon", juice.bossBeaconPrefab, juice.bossBeaconScale); juice.bossBeaconScale = 1f; }
            EditorUtility.SetDirty(juice);
            log.Add($"POI: {made} prefabs made in {PoiFolder} and assigned in DemoJuiceSettings");
        }
    }
}
