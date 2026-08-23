#if UNITY_EDITOR
using HighSeas.Data;
using UnityEditor;
using UnityEngine;

namespace HighSeas.Editor
{
    [CustomEditor(typeof(Enemies.SpawnDirector))]
    public sealed class SpawnDirectorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var settings = serializedObject.FindProperty("settings");

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Spawn Tuning", EditorStyles.boldLabel);

            if (GUILayout.Button("Create / Select Spawn Settings Asset"))
                CreateOrSelectSettings(settings);

            if (settings.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign Spawn Director Settings to tune scripted waves, enemy speed, timing, and per-enemy weights.",
                    MessageType.Info);
                return;
            }

            var settingsAsset = (Enemies.SpawnDirectorSettings)settings.objectReferenceValue;

            if (GUILayout.Button("Reset Default Wave Script"))
                ResetDefaultWaves(settingsAsset);

            if (GUILayout.Button("Sync Enemy List From Game Database"))
                SyncEnemyList(settingsAsset);
        }

        static void CreateOrSelectSettings(SerializedProperty settingsProp)
        {
            const string path = "Assets/_Game/Data/SpawnDirectorSettings.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Enemies.SpawnDirectorSettings>(path);
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<Enemies.SpawnDirectorSettings>();
                existing.timingMode = Enemies.SpawnTimingMode.ScriptedWaves;
                existing.threatMultiplier = 0.85f;
                existing.creatureChanceAtLateGame = 0.5f;
                existing.waves = Enemies.SpawnDirectorSettings.CreateDefaultWaveScript();
                existing.enemies = Enemies.SpawnDirectorSettings.CreateDefaultEnemyTuning();
                AssetDatabase.CreateAsset(existing, path);
                AssetDatabase.SaveAssets();
                Debug.Log($"Created {path}");
            }

            settingsProp.objectReferenceValue = existing;
            settingsProp.serializedObject.ApplyModifiedProperties();
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
        }

        static void ResetDefaultWaves(Enemies.SpawnDirectorSettings settings)
        {
            settings.timingMode = Enemies.SpawnTimingMode.ScriptedWaves;
            settings.waves = Enemies.SpawnDirectorSettings.CreateDefaultWaveScript();
            settings.enemies = Enemies.SpawnDirectorSettings.CreateDefaultEnemyTuning();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("Reset scripted wave sequence and default enemy speed tuning.");
        }

        static void SyncEnemyList(Enemies.SpawnDirectorSettings settings)
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Game/Data/GameDatabase.asset");
            if (database == null || database.enemies == null)
            {
                Debug.LogError("GameDatabase not found. Run HighSeas/Setup Enemy Visuals first.");
                return;
            }

            var existing = new System.Collections.Generic.Dictionary<string, Enemies.EnemySpawnTuning>();
            if (settings.enemies != null)
            {
                for (var i = 0; i < settings.enemies.Length; i++)
                {
                    var entry = settings.enemies[i];
                    if (entry != null && !string.IsNullOrEmpty(entry.enemyId))
                        existing[entry.enemyId] = entry;
                }
            }

            var list = new System.Collections.Generic.List<Enemies.EnemySpawnTuning>();
            for (var i = 0; i < database.enemies.Length; i++)
            {
                var def = database.enemies[i];
                if (def == null) continue;
                if (existing.TryGetValue(def.id, out var kept))
                {
                    list.Add(kept);
                    continue;
                }

                list.Add(new Enemies.EnemySpawnTuning
                {
                    enemyId = def.id,
                    spawnEnabled = !def.isBoss && !def.isMiniboss,
                    weight = def.id == "swordfish" ? 0.35f : 1f,
                    maxConcurrent = def.id == "swordfish" ? 10 : 0,
                    speedMultiplier = 1f,
                    availableAfterMinutes = def.id switch
                    {
                        "swordfish" => 1f,
                        "hammerhead" => 1.5f,
                        "kraken_spawn" => 3f,
                        _ => 0f
                    }
                });
            }

            settings.enemies = list.ToArray();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"Synced {settings.enemies.Length} enemy tuning entries.");
        }
    }
}
#endif
