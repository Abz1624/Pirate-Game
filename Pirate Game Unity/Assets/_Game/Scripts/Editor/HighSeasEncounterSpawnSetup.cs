#if UNITY_EDITOR
using HighSeas.Enemies;
using UnityEditor;
using UnityEngine;

namespace HighSeas.Editor
{
    public static class HighSeasEncounterSpawnSetup
    {
        const string ProfileFolder = "Assets/_Game/Data/SpawnProfiles";
        const string SchedulePath = "Assets/_Game/Data/EncounterSpawnSchedule.asset";
        const string ResourcesSchedulePath = "Assets/_Game/Resources/EncounterSpawnSchedule.asset";

        [MenuItem("HighSeas/Encounter Spawns/Ensure Spawn Profile Folder")]
        public static void EnsureProfileFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Data"))
            {
                Debug.LogError("Assets/_Game/Data missing.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(ProfileFolder))
                AssetDatabase.CreateFolder("Assets/_Game/Data", "SpawnProfiles");

            AssetDatabase.SaveAssets();
            Debug.Log($"Spawn profile folder ready: {ProfileFolder}. Create profiles via Assets → Create → HighSeas/Spawn Profile.");
        }

        [MenuItem("HighSeas/Encounter Spawns/Ensure Empty Schedule Shell")]
        public static void EnsureEmptyScheduleShell()
        {
            EnsureProfileFolder();
            var schedule = LoadOrCreateSchedule(SchedulePath);
            ClearSchedule(schedule);
            EditorUtility.SetDirty(schedule);
            SyncScheduleToResources();
            AssetDatabase.SaveAssets();
            Debug.Log(
                "Empty EncounterSpawnSchedule ready in Data/ and Resources/. " +
                "Assign profiles on the schedule and ambient entries when authoring.");
        }

        [MenuItem("HighSeas/Encounter Spawns/Sync Schedule To Resources")]
        public static void SyncScheduleToResources()
        {
            var schedule = AssetDatabase.LoadAssetAtPath<EncounterSpawnSchedule>(SchedulePath);
            if (schedule == null)
            {
                Debug.LogError($"Missing {SchedulePath}. Run HighSeas/Encounter Spawns/Ensure Empty Schedule Shell.");
                return;
            }

            var existing = AssetDatabase.LoadAssetAtPath<EncounterSpawnSchedule>(ResourcesSchedulePath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(Object.Instantiate(schedule), ResourcesSchedulePath);
            }
            else
            {
                EditorUtility.CopySerialized(schedule, existing);
                EditorUtility.SetDirty(existing);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Synced {SchedulePath} → {ResourcesSchedulePath}.");
        }

        static EncounterSpawnSchedule LoadOrCreateSchedule(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<EncounterSpawnSchedule>(path);
            if (existing != null)
                return existing;
            var schedule = ScriptableObject.CreateInstance<EncounterSpawnSchedule>();
            AssetDatabase.CreateAsset(schedule, path);
            return schedule;
        }

        static void ClearSchedule(EncounterSpawnSchedule schedule)
        {
            schedule.gracePeriodSeconds = 2f;
            schedule.maxActiveEnemies = 55;
            schedule.globalMinSpawnGapSeconds = 0.3f;
            schedule.entries = System.Array.Empty<ScheduledSpawnEntry>();
            schedule.hammerheadBeat = null;
            schedule.krakenBeat = null;
            schedule.swarmPulse = null;
            schedule.drownedPursuer = null;
            schedule.drownedHarrier = null;
            schedule.routeTreasureRaider = null;
            schedule.routeCursedRingSwordfish = null;
            schedule.routeCursedRingCannon = null;
            schedule.powderCacheAmbush = null;
            schedule.bossSummon = null;
        }
    }
}
#endif
