#if UNITY_EDITOR
using HighSeas.Data;
using HighSeas.Enemies;
using UnityEditor;
using UnityEngine;

namespace HighSeas.Editor
{
    public sealed class HighSeasTuningWindow : EditorWindow
    {
        enum TuningTab
        {
            Pacing,
            Spawn,
            Weapons,
            Enemies,
            Juice,
            LevelUp
        }

        const string Root = TuningAssetSync.Root;

        static readonly string[] TabLabels =
        {
            "Pacing",
            "Spawn",
            "Weapons",
            "Enemies",
            "Juice",
            "Level-up"
        };

        TuningTab _tab = TuningTab.Pacing;
        Vector2 _scroll;
        UnityEditor.Editor _inspectorEditor;
        Object _inspectorTarget;

        GameDatabase _database;
        int _weaponIndex;
        int _enemyIndex;

        [MenuItem("HighSeas/Tuning Window")]
        public static void Open()
        {
            var window = GetWindow<HighSeasTuningWindow>("High Seas Tuning");
            window.minSize = new Vector2(420f, 520f);
            window.Show();
        }

        void OnDisable() => DestroyInspector();

        void OnGUI()
        {
            DrawHeader();
            DrawTabs();

            EditorGUILayout.Space(4f);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawActiveTab();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6f);
            DrawFooter();
        }

        void DrawHeader()
        {
            EditorGUILayout.LabelField("High Seas Tuning", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Source of truth: Assets/_Game/Data/", EditorStyles.miniLabel);

            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Play Mode: tweak values here, then Apply to Play to push Data → Resources live.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Edit tuning values here, then Save & Sync Resources before entering Play Mode.",
                    MessageType.None);
            }
        }

        void DrawTabs()
        {
            EditorGUILayout.Space(4f);
            _tab = (TuningTab)GUILayout.Toolbar((int)_tab, TabLabels);
        }

        void DrawActiveTab()
        {
            switch (_tab)
            {
                case TuningTab.Pacing:
                    DrawAssetInspector(TuningAssetSync.LoadDataAsset<DemoPacingSettings>("DemoPacingSettings.asset"));
                    break;
                case TuningTab.Spawn:
                    DrawSpawnTab();
                    break;
                case TuningTab.Weapons:
                    DrawDatabasePickerTab(true);
                    break;
                case TuningTab.Enemies:
                    DrawDatabasePickerTab(false);
                    break;
                case TuningTab.Juice:
                    DrawAssetInspector(TuningAssetSync.LoadDataAsset<DemoJuiceSettings>("DemoJuiceSettings.asset"));
                    break;
                case TuningTab.LevelUp:
                    DrawAssetInspector(TuningAssetSync.LoadDataAsset<LevelUpSettings>("LevelUpSettings.asset"));
                    break;
            }
        }

        void DrawSpawnTab()
        {
            const string path = Root + "/Data/SpawnDirectorSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<SpawnDirectorSettings>(path);
            if (settings == null)
            {
                EditorGUILayout.HelpBox("SpawnDirectorSettings.asset not found in Data/.", MessageType.Warning);
                if (GUILayout.Button("Create Spawn Settings Asset"))
                {
                    SpawnDirectorEditor.CreateOrSelectSettingsAsset(path);
                    GUIUtility.ExitGUI();
                }
                return;
            }

            DrawAssetInspector(settings);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Spawn Utilities", EditorStyles.boldLabel);

            if (GUILayout.Button("Reset Default Spawn Tuning"))
            {
                SpawnDirectorEditor.ResetDefaultTuning(settings);
                RepaintInspector();
            }

            if (GUILayout.Button("Sync Enemy List From Game Database"))
            {
                SpawnDirectorEditor.SyncEnemyList(settings);
                RepaintInspector();
            }
        }

        void DrawDatabasePickerTab(bool weapons)
        {
            EnsureDatabase();

            if (_database == null)
            {
                EditorGUILayout.HelpBox("GameDatabase.asset not found in Data/.", MessageType.Error);
                return;
            }

            if (weapons)
            {
                if (_database.weapons == null || _database.weapons.Length == 0)
                {
                    EditorGUILayout.HelpBox("No weapons in GameDatabase.", MessageType.Warning);
                    return;
                }

                _weaponIndex = EditorGUILayout.Popup("Weapon", _weaponIndex, GetWeaponLabels(_database));
                var weapon = _database.weapons[Mathf.Clamp(_weaponIndex, 0, _database.weapons.Length - 1)];
                if (weapon == null)
                {
                    EditorGUILayout.HelpBox("Selected weapon slot is empty.", MessageType.Warning);
                    return;
                }

                EditorGUILayout.LabelField(weapon.id, EditorStyles.miniLabel);
                DrawAssetInspector(weapon);
            }
            else
            {
                if (_database.enemies == null || _database.enemies.Length == 0)
                {
                    EditorGUILayout.HelpBox("No enemies in GameDatabase.", MessageType.Warning);
                    return;
                }

                _enemyIndex = EditorGUILayout.Popup("Enemy", _enemyIndex, GetEnemyLabels(_database));
                var enemy = _database.enemies[Mathf.Clamp(_enemyIndex, 0, _database.enemies.Length - 1)];
                if (enemy == null)
                {
                    EditorGUILayout.HelpBox("Selected enemy slot is empty.", MessageType.Warning);
                    return;
                }

                EditorGUILayout.LabelField(enemy.id, EditorStyles.miniLabel);
                DrawAssetInspector(enemy);
            }
        }

        void DrawAssetInspector(Object target)
        {
            if (target == null)
            {
                EditorGUILayout.HelpBox("Asset not found.", MessageType.Warning);
                return;
            }

            if (_inspectorTarget != target)
            {
                DestroyInspector();
                _inspectorTarget = target;
                _inspectorEditor = UnityEditor.Editor.CreateEditor(target);
            }

            if (_inspectorEditor == null) return;

            _inspectorEditor.OnInspectorGUI();
        }

        void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Apply to Play", GUILayout.Height(30f)))
                ApplyToPlay();

            if (GUILayout.Button("Save & Sync Resources", GUILayout.Height(30f)))
                SaveAndSync();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                "Weapons/enemies save directly. Bundled settings copy Data/ → Resources/.",
                EditorStyles.centeredGreyMiniLabel);
        }

        void ApplyToPlay()
        {
            CommitInspector();
            var synced = TuningAssetSync.SyncDataToResources(saveAssets: true, recordUndo: true);
            TuningScenePusher.PushIfPlaying();
            Debug.Log(synced > 0
                ? $"Applied {synced} tuning asset(s) to Resources." +
                  (EditorApplication.isPlaying ? " Live systems refreshed." : " Enter Play Mode to test.")
                : "No tuning assets were synced. Check Assets/_Game/Data/.");
        }

        void SaveAndSync()
        {
            CommitInspector();
            var synced = TuningAssetSync.SyncDataToResources(saveAssets: true, recordUndo: false);
            Debug.Log(synced > 0
                ? $"Saved and synced {synced} asset(s) from Data/ to Resources/."
                : "No tuning assets were synced. Check Assets/_Game/Data/.");
        }

        void CommitInspector()
        {
            if (_inspectorEditor == null) return;
            _inspectorEditor.serializedObject.ApplyModifiedProperties();
            if (_inspectorTarget != null)
                EditorUtility.SetDirty(_inspectorTarget);
            AssetDatabase.SaveAssets();
        }

        void EnsureDatabase()
        {
            if (_database == null)
                _database = AssetDatabase.LoadAssetAtPath<GameDatabase>(Root + "/Data/GameDatabase.asset");
        }

        static string[] GetWeaponLabels(GameDatabase database)
        {
            var labels = new string[database.weapons.Length];
            for (var i = 0; i < database.weapons.Length; i++)
            {
                var weapon = database.weapons[i];
                labels[i] = weapon != null ? weapon.displayName : $"(empty slot {i})";
            }
            return labels;
        }

        static string[] GetEnemyLabels(GameDatabase database)
        {
            var labels = new string[database.enemies.Length];
            for (var i = 0; i < database.enemies.Length; i++)
            {
                var enemy = database.enemies[i];
                labels[i] = enemy != null ? enemy.displayName : $"(empty slot {i})";
            }
            return labels;
        }

        void DestroyInspector()
        {
            if (_inspectorEditor != null)
                DestroyImmediate(_inspectorEditor);
            _inspectorEditor = null;
            _inspectorTarget = null;
        }

        void RepaintInspector()
        {
            DestroyInspector();
            Repaint();
        }
    }
}
#endif
