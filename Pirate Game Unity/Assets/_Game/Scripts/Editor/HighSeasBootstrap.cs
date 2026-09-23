#if UNITY_EDITOR
using System.IO;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.Meta;
using HighSeas.Pickups;
using HighSeas.Player;
using HighSeas.POI;
using HighSeas.UI;
using HighSeas.VFX;
using HighSeas.Weapons;
using HighSeas.World;
using HighSeas.Playtest;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HighSeas.Editor
{
    public static class HighSeasBootstrap
    {
        const string Root = "Assets/_Game";
        const string DataPath = Root + "/Data/GameDatabase.asset";

        [MenuItem("HighSeas/Bootstrap Project")]
        public static void BootstrapAll()
        {
            EnsureFolders();
            var db = CreateOrLoadDatabase();
            if (db == null)
            {
                Debug.LogError("Bootstrap aborted: GameDatabase could not be created. Wait for scripts to compile, then try again.");
                return;
            }
            HighSeasIslandSetup.EnsureLibraryAsset();
            HighSeasEnemySetup.SetupEnemyVisuals();
            CreateScenes(db);
            ConfigurePlayerSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("High Seas bootstrap complete.");
        }

        [MenuItem("HighSeas/Fix URP Pipeline")]
        public static void FixUrpPipelineMenu()
        {
            AssignProjectRenderPipelines();
            EnableUrpCompatibilityMode();
            Debug.Log("Assigned PC/Mobile URP assets and enabled compatibility mode.");
        }

        [MenuItem("HighSeas/Run EditMode Tests")]
        public static void RunEditModeTestsMenu()
        {
            BootstrapAll();
            Debug.Log("Bootstrap complete. Run tests via Unity Test Runner or tools/run-highseas-tests.bat");
        }

        static void EnsureFolders()
        {
            string[] dirs =
            {
                Root, Root + "/Art", Root + "/Data", Root + "/Data/Ships", Root + "/Data/Weapons",
                Root + "/Data/Logbooks", Root + "/Data/Relics", Root + "/Data/Enemies",
                Root + "/Resources", Root + "/Prefabs", Root + "/Scenes",
                Root + "/Scenes/Tests", Root + "/Scripts/Editor"
            };
            foreach (var d in dirs)
                if (!AssetDatabase.IsValidFolder(d))
                {
                    var parent = Path.GetDirectoryName(d)?.Replace("\\", "/");
                    var leaf = Path.GetFileName(d);
                    AssetDatabase.CreateFolder(parent, leaf);
                }
        }

        static T SaveDefinition<T>(string subfolder, string id, System.Action<T> configure) where T : ScriptableObject
        {
            var path = $"{Root}/Data/{subfolder}/{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                var stale = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (stale != null)
                    AssetDatabase.DeleteAsset(path);
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            configure(asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static GameDatabase CreateOrLoadDatabase()
        {
            EnsureDatabaseAssetExists();

            var ships = new[] { CreateShip() };
            var weapons = CreateWeapons();
            var logbooks = CreateLogbooks();
            var relics = CreateRelics();
            var enemies = CreateEnemies();

            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DataPath);
            if (db == null)
            {
                Debug.LogError($"Failed to load GameDatabase at {DataPath}");
                return null;
            }

            db.ships = ships;
            db.weapons = weapons;
            db.logbooks = logbooks;
            db.relics = relics;
            db.enemies = enemies;
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            SyncResourcesDatabaseCopy();
            return AssetDatabase.LoadAssetAtPath<GameDatabase>(DataPath);
        }

        static void EnsureDatabaseAssetExists()
        {
            if (AssetDatabase.LoadAssetAtPath<GameDatabase>(DataPath) != null)
                return;

            var stale = AssetDatabase.LoadAssetAtPath<Object>(DataPath);
            if (stale != null)
                AssetDatabase.DeleteAsset(DataPath);

            var db = ScriptableObject.CreateInstance<GameDatabase>();
            AssetDatabase.CreateAsset(db, DataPath);
            AssetDatabase.SaveAssets();
        }

        static void SyncResourcesDatabaseCopy()
        {
            var resourcesPath = Root + "/Resources/GameDatabase.asset";
            if (AssetDatabase.LoadAssetAtPath<Object>(resourcesPath) != null)
                AssetDatabase.DeleteAsset(resourcesPath);
            if (!AssetDatabase.CopyAsset(DataPath, resourcesPath))
                Debug.LogWarning($"Could not copy GameDatabase to {resourcesPath}");
        }

        static ShipDefinition CreateShip() =>
            SaveDefinition<ShipDefinition>("Ships", "cutlass_sloop", ship =>
            {
                ship.id = "cutlass_sloop";
                ship.displayName = "Cutlass Sloop";
                ship.baseHull = 100f;
                ship.baseMoveSpeed = 6.2f;
                ship.startingWeaponId = "swivel_guns";
            });

        static WeaponDefinition[] CreateWeapons()
        {
            return new[]
            {
                MakeWeapon("deck_cannons", "Deck Cannons", WeaponPattern.AutoProjectile, TargetingMode.Nearest, 10f, 1.2f, Color.gray),
                MakeWeapon("broadside_battery", "Broadside Battery", WeaponPattern.FanVolley, TargetingMode.DensestCluster, 8f, 1.5f, new Color(0.7f, 0.7f, 0.8f)),
                MakeWeapon("powder_kegs", "Powder Kegs", WeaponPattern.MineDrop, TargetingMode.PlayerTrail, 20f, 2.2f, Color.red),
                MakeWeapon("lightning_mast", "Lightning Mast", WeaponPattern.ChainLightning, TargetingMode.Nearest, 14f, 1.8f, Color.cyan),
                MakeWeapon("flying_cutlasses", "Flying Cutlasses", WeaponPattern.Orbit, TargetingMode.AreaAroundShip, 7f, 1f, Color.white),
                MakeWeapon("flamethrower", "Flamethrower", WeaponPattern.TrailZone, TargetingMode.PlayerTrail, 8f, 0.7f, new Color(1f, 0.4f, 0f))
            };
        }

        static WeaponDefinition MakeWeapon(string id, string name, WeaponPattern pattern, TargetingMode targeting, float dmg, float cd, Color color) =>
            SaveDefinition<WeaponDefinition>("Weapons", id, w =>
            {
                w.id = id;
                w.displayName = name;
                w.pattern = pattern;
                w.targeting = targeting;
                w.baseDamage = dmg;
                w.baseCooldown = cd;
                w.projectileColor = color;
                w.validUpgradeAxes = new[] { UpgradeAxis.Damage, UpgradeAxis.Cooldown, UpgradeAxis.Quantity };
            });

        static LogbookDefinition[] CreateLogbooks()
        {
            return new[]
            {
                MakeLogbook("gunnery_manual", "Gunnery Manual", StatId.Damage, 0.08f),
                MakeLogbook("powder_drill", "Powder Drill", StatId.AttackSpeed, 0.075f),
                MakeLogbook("oversized_shot", "Oversized Shot Chart", StatId.Size, 0.10f),
                MakeLogbook("extra_hands", "Extra Hands Ledger", StatId.Quantity, 1f),
                MakeLogbook("reinforced_hull", "Reinforced Hull Plans", StatId.Hull, 20f),
                MakeLogbook("carpenters_notes", "Carpenter's Notes", StatId.HullRegen, 0.35f),
                MakeLogbook("infamy_charter", "Infamy Charter", StatId.XPGain, 0.08f),
                MakeLogbook("treasure_map", "Treasure Map Folio", StatId.Luck, 0.07f)
            };
        }

        static LogbookDefinition MakeLogbook(string id, string name, StatId stat, float perLevel) =>
            SaveDefinition<LogbookDefinition>("Logbooks", id, l =>
            {
                l.id = id;
                l.displayName = name;
                l.primaryStat = stat;
                l.perLevelAdditive = perLevel;
            });

        static RelicDefinition[] CreateRelics()
        {
            return new[]
            {
                MakeRelic("lucky_doubloon", "Lucky Doubloon", Rarity.Common, new StatModifier(StatId.Luck, 0.07f)),
                MakeRelic("gold_tooth", "Gold Tooth", Rarity.Common, new StatModifier(StatId.GoldGain, 0f, 1.12f)),
                MakeRelic("carpenter_kit", "Carpenter Kit", Rarity.Common, new StatModifier(StatId.Hull, 18f)),
                MakeRelic("powder_flask", "Powder Flask", Rarity.Common, new StatModifier(StatId.Damage, 0f, 1.10f)),
                MakeRelic("spyglass", "Spyglass", Rarity.Common, new StatModifier(StatId.CritChance, 0.05f)),
                MakeRelic("tar_bucket", "Tar Bucket", Rarity.Common, new StatModifier(StatId.Knockback, 0f, 1.05f)),
                MakeRelic("chain_locker", "Chain Locker", Rarity.Uncommon, new StatModifier(StatId.Knockback, 0f, 1.15f)),
                MakeRelic("double_rammer", "Double Rammer", Rarity.Uncommon, new StatModifier(StatId.Damage, 0f, 1.08f)),
                MakeRelic("black_powder_barrel", "Black Powder Barrel", Rarity.Uncommon, new StatModifier(StatId.Size, 0f, 1.12f)),
                MakeRelic("stormglass", "Stormglass", Rarity.Uncommon, new StatModifier(StatId.Duration, 0f, 1.10f)),
                MakeRelic("blood_rum", "Blood Rum", Rarity.Rare, new StatModifier(StatId.HullRegen, 0.15f)),
                MakeRelic("powder_monkey", "Powder Monkey", Rarity.Rare, new StatModifier(StatId.AttackSpeed, 0f, 1.12f))
            };
        }

        static RelicDefinition MakeRelic(string id, string name, Rarity rarity, StatModifier mod) =>
            SaveDefinition<RelicDefinition>("Relics", id, r =>
            {
                r.id = id;
                r.displayName = name;
                r.rarity = rarity;
                r.passiveModifiers = new[] { mod };
            });

        static EnemyDefinition[] CreateEnemies()
        {
            return new[]
            {
                MakeEnemy("cannonboat", "Cannonboat", EnemyCategory.Ship, 18f, 8f, 3.8f, 3, 1.2f, EnemyBehavior.DirectChaser, new Color(0.75f, 0.45f, 0.2f)),
                MakeEnemy("shark", "Shark", EnemyCategory.Creature, 16f, 9f, 4.75f, 4, 1.1f, EnemyBehavior.DirectChaser, new Color(0.35f, 0.55f, 0.9f)),
                MakeEnemy("swordfish", "Swordfish", EnemyCategory.Creature, 10f, 6f, 5.004f, 2, 0.9f, EnemyBehavior.FastSwarm, new Color(0.2f, 0.7f, 0.95f)),
                MakeEnemy("hammerhead", "Hammerhead", EnemyCategory.Creature, 14f, 7f, 4.75f, 3, 1.1f, EnemyBehavior.FlankingChaser, new Color(0.25f, 0.6f, 0.85f)),
                MakeEnemy("kraken_spawn", "Kraken Spawn", EnemyCategory.Creature, 28f, 11f, 2.38f, 6, 1.6f, EnemyBehavior.TankChaser, new Color(0.45f, 0.2f, 0.65f)),
                MakeEnemy("manta_ray", "Manta Ray", EnemyCategory.Creature, 22f, 8f, 4.28f, 4, 1.4f, EnemyBehavior.FlankingChaser, new Color(0.3f, 0.55f, 0.9f)),
                MakeEnemy("reef_crab_miniboss", "Reef Crab", EnemyCategory.Creature, 120f, 14f, 2f, 8, 2.2f, EnemyBehavior.MaintainRange, new Color(0.4f, 0.3f, 0.55f), isMiniboss: true),
                MakeEnemy("barnacle_king", "Barnacle King", EnemyCategory.Creature, 800f, 18f, 2.5f, 9, 2.8f, EnemyBehavior.TankChaser, new Color(0.2f, 0.5f, 0.35f), isBoss: true)
            };
        }

        static EnemyDefinition MakeEnemy(
            string id,
            string name,
            EnemyCategory category,
            float hp,
            float dmg,
            float speed,
            int threat,
            float radius,
            EnemyBehavior behavior,
            Color color,
            bool isBoss = false,
            bool isMiniboss = false) =>
            SaveDefinition<EnemyDefinition>("Enemies", id, e =>
            {
                e.id = id;
                e.displayName = name;
                e.category = category;
                e.baseHP = hp;
                e.baseDamage = dmg;
                e.moveSpeed = speed;
                e.threatCost = threat;
                e.radius = radius;
                e.behavior = behavior;
                e.placeholderColor = color;
                e.isBoss = isBoss;
                e.isMiniboss = isMiniboss;
                e.xpValue = isBoss ? 100 : isMiniboss ? 35 : threat + 1;
                e.goldValue = isBoss ? 150 : isMiniboss ? 50 : 1;
            });

        static void CreateScenes(GameDatabase db)
        {
            CreateBootScene();
            CreateMainMenuScene();
            CreateRunScene(db);
            CreateTestCombatScene(db);
            CreateTestWeaponEnemyPlaytestScene(db);
            CreateTestPerformanceScene(db);

            var scenes = new[]
            {
                Root + "/Scenes/Boot.unity",
                Root + "/Scenes/MainMenu.unity",
                Root + "/Scenes/Run.unity",
                Root + "/Scenes/Tests/Test_Combat.unity",
                Root + "/Scenes/Tests/Test_WeaponEnemy.unity",
                Root + "/Scenes/Tests/Test_Performance.unity"
            };
            var sceneList = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (var path in scenes)
            {
                if (string.IsNullOrEmpty(path)) continue;
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"Scene not found for build settings: {path}");
                    continue;
                }
                sceneList.Add(new EditorBuildSettingsScene(path, true));
            }
            if (sceneList.Count > 0)
                EditorBuildSettings.scenes = sceneList.ToArray();
        }

        static void CreateBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var boot = new GameObject("BootLoader");
            boot.AddComponent<BootLoader>();
            SaveScene(Root + "/Scenes/Boot.unity");
        }

        static void CreateMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateEventSystem();
            var canvas = CreateCanvas("MainMenuCanvas");
            var title = CreateText(canvas.transform, "Title", "Project High Seas", new Vector2(0, 120), 48);
            var play = CreateButton(canvas.transform, "PlayButton", "Play", new Vector2(0, 0), OnPlayClicked);
            var menu = canvas.gameObject.AddComponent<MainMenuController>();
            SetPrivateField(menu, "playButton", play);
            SaveScene(Root + "/Scenes/MainMenu.unity");
        }

        static void OnPlayClicked()
        {
            AppRoot.Instance?.LoadScene("Run");
        }

        static void CreateRunScene(GameDatabase db)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var cam = Camera.main;
            if (cam != null)
            {
                cam.gameObject.AddComponent<FollowCamera>();
                cam.fieldOfView = GameConstants.CameraFov;
            }

            CreateEventSystem();
            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Ocean";
            water.transform.localScale = new Vector3(22f, 1f, 22f);
            water.GetComponent<Renderer>().material.color = new Color(0.1f, 0.35f, 0.55f);

            var systems = new GameObject("Systems");
            systems.AddComponent<RunManager>();
            systems.AddComponent<RunBootstrap>();
            systems.AddComponent<EnemyWorld>();
            systems.AddComponent<EncounterSpawner>();
            systems.AddComponent<ProjectileWorld>();
            systems.AddComponent<PickupWorld>();
            systems.AddComponent<WeaponController>();
            systems.AddComponent<LevelSystem>();
            systems.AddComponent<StatusController>();
            systems.AddComponent<TriggerBus>();
            systems.AddComponent<CombatVfxController>();
            systems.AddComponent<LootService>();
            systems.AddComponent<QuestService>();
            systems.AddComponent<POIManager>();
            systems.AddComponent<StageBuilder>();

            var islandRoot = new GameObject("IslandRoot");
            var stageBuilder = systems.GetComponent<StageBuilder>();
            var islandLibrary = HighSeasIslandSetup.EnsureLibraryAsset();
            SetPrivateField(stageBuilder, "islandRoot", islandRoot.transform);
            SetPrivateField(stageBuilder, "visuals", islandLibrary);
            SetPrivateField(stageBuilder, "useVisualPrefabs", true);

            var player = CreatePlayerShip(db);

            var bootstrap = systems.GetComponent<RunBootstrap>();
            SetPrivateField(bootstrap, "database", db);
            SetPrivateField(bootstrap, "stageBuilder", stageBuilder);
            SetPrivateField(bootstrap, "player", player.GetComponent<PlayerShipController>());
            SetPrivateField(bootstrap, "runSeed", GameConstants.DemoRunSeed);

            var canvas = CreateCanvas("RunCanvas");
            BuildRunHud(canvas);
            BuildLevelUpModal(canvas);
            BuildRunEnd(canvas);
            canvas.gameObject.AddComponent<TutorialOverlay>();

            var poi = systems.GetComponent<POIManager>();
            SetPrivateField(poi, "database", db);
            SetPrivateField(poi, "loot", systems.GetComponent<LootService>());
            SetPrivateField(poi, "encounterSpawner", systems.GetComponent<EncounterSpawner>());

            var loot = systems.GetComponent<LootService>();
            SetPrivateField(loot, "database", db);

            var level = systems.GetComponent<LevelSystem>();
            SetPrivateField(level, "database", db);
            SetPrivateField(level, "levelUpSettings",
                AssetDatabase.LoadAssetAtPath<LevelUpSettings>("Assets/_Game/Data/LevelUpSettings.asset"));

            SaveScene(Root + "/Scenes/Run.unity");
        }

        static GameObject CreatePlayerShip(GameDatabase db)
        {
            var player = new GameObject("PlayerShip");
            player.tag = "Player";
            player.AddComponent<PlayerShipController>();
            player.AddComponent<DamageReceiver>();

            var controller = player.GetComponent<PlayerShipController>();
            PlayerShipSceneSetup.SetupPlayer(controller);
            return player;
        }

        static void BuildRunHud(Canvas canvas)
        {
            var hud = canvas.gameObject.AddComponent<RunHud>();
            hud.RebuildUiLayout();
        }

        static void BuildLevelUpModal(Canvas canvas)
        {
            var panel = new GameObject("LevelUpPanel");
            panel.transform.SetParent(canvas.transform, false);
            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var modal = panel.AddComponent<LevelUpModal>();
            SetPrivateField(modal, "panel", panel);
            modal.RebuildUiLayout();
            panel.SetActive(false);
        }

        static void BuildRunEnd(Canvas canvas)
        {
            var panel = new GameObject("RunEndPanel");
            panel.transform.SetParent(canvas.transform, false);
            var rt = panel.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(600, 400);
            var img = panel.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.9f);
            var end = panel.AddComponent<RunEndScreen>();
            var summary = CreateText(panel.transform, "Summary", "Run Complete", new Vector2(0, 40), 24);
            var btn = CreateButton(panel.transform, "Continue", "Continue", new Vector2(0, -120), null);
            SetPrivateField(end, "panel", panel);
            SetPrivateField(end, "summaryText", summary);
            SetPrivateField(end, "continueButton", btn);
            panel.SetActive(false);
        }

        static void BuildJoystick(Canvas canvas)
        {
            var bg = new GameObject("JoystickBG");
            bg.transform.SetParent(canvas.transform, false);
            var rt = bg.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(120, 120);
            rt.sizeDelta = new Vector2(160, 160);
            bg.AddComponent<Image>().color = new Color(1, 1, 1, 0.2f);
            var handle = new GameObject("Handle");
            handle.transform.SetParent(bg.transform, false);
            var hrt = handle.AddComponent<RectTransform>();
            hrt.sizeDelta = new Vector2(60, 60);
            handle.AddComponent<Image>().color = new Color(1, 1, 1, 0.5f);
            var joy = bg.AddComponent<Platform.VirtualJoystick>();
            SetPrivateField(joy, "background", rt);
            SetPrivateField(joy, "handle", hrt);
        }

        static void CreateTestCombatScene(GameDatabase db)
        {
            CreateRunScene(db);
            SaveScene(Root + "/Scenes/Tests/Test_Combat.unity");
        }

        static void CreateTestWeaponEnemyPlaytestScene(GameDatabase db)
        {
            HighSeasWeaponEnemyPlaytestSceneSetup.CreateOrUpdateSceneBatch();
        }

        static void CreateTestPerformanceScene(GameDatabase db)
        {
            CreateRunScene(db);
            var harness = new GameObject("PerfHarness");
            harness.AddComponent<PerformanceHarness>();
            SaveScene(Root + "/Scenes/Tests/Test_Performance.unity");
        }

        static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            UiCanvasConfigurator.Configure(canvas);
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        static void CreateEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        static TextMeshProUGUI CreateText(Transform parent, string name, string text, Vector2 pos, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(500, 60);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            return tmp;
        }

        static Button CreateButton(Transform parent, string name, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(200, 60);
            go.AddComponent<Image>().color = new Color(0.2f, 0.45f, 0.7f);
            var btn = go.AddComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(onClick);
            CreateText(go.transform, "Label", label, Vector2.zero, 22);
            return btn;
        }

        static Slider CreateSlider(Transform parent, string name, Vector2 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(220, 20);
            var slider = go.AddComponent<Slider>();
            var bg = new GameObject("BG").AddComponent<RectTransform>();
            bg.SetParent(go.transform, false);
            bg.sizeDelta = new Vector2(220, 20);
            go.AddComponent<Image>().color = Color.gray;
            slider.targetGraphic = go.GetComponent<Image>();
            return slider;
        }

        static void SaveScene(string path)
        {
            var dir = Path.GetDirectoryName(path)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                var parts = dir.Split('/');
                var current = parts[0];
                for (var i = 1; i < parts.Length; i++)
                {
                    var next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), path);
            AssetDatabase.SaveAssets();
        }

        static void ConfigurePlayerSettings()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            AssignProjectRenderPipelines();
            EnableUrpCompatibilityMode();
        }

        static void AssignProjectRenderPipelines()
        {
            const string pcPipelinePath = "Assets/Settings/PC_RPAsset.asset";
            const string mobilePipelinePath = "Assets/Settings/Mobile_RPAsset.asset";
            var pcPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(pcPipelinePath);
            var mobilePipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(mobilePipelinePath);
            if (pcPipeline == null || mobilePipeline == null)
            {
                Debug.LogWarning("HighSeas: PC or Mobile URP asset missing; pipeline assignment skipped.");
                return;
            }

            GraphicsSettings.defaultRenderPipeline = pcPipeline;
            QualitySettings.SetQualityLevel(1, applyExpensiveChanges: false);
            QualitySettings.renderPipeline = pcPipeline;
            QualitySettings.SetQualityLevel(0, applyExpensiveChanges: false);
            QualitySettings.renderPipeline = mobilePipeline;
            QualitySettings.SetQualityLevel(1, applyExpensiveChanges: false);
        }

        static void EnableUrpCompatibilityMode()
        {
            const string path = "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (settings == null) return;
            var so = new SerializedObject(settings);
            var prop = so.FindProperty("m_Settings.m_SettingsList.m_List");
            if (prop == null || !prop.isArray) return;
            for (var i = 0; i < prop.arraySize; i++)
            {
                var element = prop.GetArrayElementAtIndex(i);
                var compat = element.FindPropertyRelative("m_EnableRenderCompatibilityMode");
                if (compat == null) continue;
                compat.boolValue = true;
                break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        static void SetPrivateField(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetPrivateField(Object target, string field, System.Array value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            prop.arraySize = value.Length;
            for (var i = 0; i < value.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = value.GetValue(i) as Object;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetPrivateField(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetPrivateField(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetPrivateField(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
