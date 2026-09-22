#if UNITY_EDITOR
using HighSeas.Balance;
using HighSeas.Data;
using HighSeas.Enemies;
using UnityEditor;
using UnityEngine;

namespace HighSeas.Editor
{
    public static class SaltwindEncounterSpawnSetup
    {
        const string ProfileFolder = "Assets/_Game/Data/SpawnProfiles/Saltwind";
        const string SchedulePath = "Assets/_Game/Data/EncounterSpawnSchedule.asset";
        const string BalanceProfilePath = "Assets/_Game/Data/Balance/SaltwindSoloStageBalanceProfile.asset";
        const string DatabasePath = "Assets/_Game/Resources/GameDatabase.asset";

        [MenuItem("HighSeas/Saltwind/Author Step 04 Spawn Schedule")]
        public static void AuthorSaltwindSchedule()
        {
            EnsureFolder("Assets/_Game/Data/SpawnProfiles");
            EnsureFolder(ProfileFolder);

            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            if (db == null)
            {
                Debug.LogError($"Missing {DatabasePath}");
                return;
            }

            var schedule = AssetDatabase.LoadAssetAtPath<EncounterSpawnSchedule>(SchedulePath);
            if (schedule == null)
            {
                schedule = ScriptableObject.CreateInstance<EncounterSpawnSchedule>();
                AssetDatabase.CreateAsset(schedule, SchedulePath);
            }

            schedule.gracePeriodSeconds = 2f;
            schedule.maxActiveEnemies = 80;
            schedule.globalMinSpawnGapSeconds = 0.35f;

            var bandRates = new[] { 0.6f, 1.2f, 1.8f, 2.4f, 3.0f };
            var bandStarts = new[] { 0f, 120f, 240f, 360f, 480f };
            var bandAnchorEnemyIds = new[] { "shark", "cannonboat", "manta_ray", "hammerhead", "manta_ray" };
            var ambientEntries = new ScheduledSpawnEntry[bandRates.Length];
            for (var i = 0; i < bandRates.Length; i++)
            {
                var anchor = db.GetEnemy(bandAnchorEnemyIds[i]) ?? db.GetEnemy("shark");
                var profile = CreateOrUpdateProfile(
                    $"{ProfileFolder}/SaltwindAmbientBand{i}.asset",
                    $"saltwind_ambient_band_{i}",
                    anchor,
                    SpawnMode.Continuous,
                    spawnInterval: 1f / bandRates[i],
                    duration: 0f);
                ambientEntries[i] = new ScheduledSpawnEntry
                {
                    profile = profile,
                    startTimeSeconds = bandStarts[i],
                    loop = false
                };
            }

            schedule.entries = ambientEntries;
            schedule.bossSummon = CreateOrUpdateProfile(
                $"{ProfileFolder}/SaltwindBossSummon.asset",
                "saltwind_boss",
                db.GetEnemy("barnacle_king"),
                SpawnMode.Single,
                spawnCount: 1);
            schedule.powderCacheAmbush = CreateOrUpdateProfile(
                $"{ProfileFolder}/SaltwindPowderCache.asset",
                "saltwind_powder_cache",
                db.GetEnemy("cutthroat_skiff"),
                SpawnMode.Group,
                spawnCount: 3);
            schedule.hammerheadBeat = CreateOrUpdateProfile(
                $"{ProfileFolder}/SaltwindEliteHammerhead.asset",
                "saltwind_elite_hammerhead",
                db.GetEnemy("hammerhead"),
                SpawnMode.Single,
                spawnCount: 1);
            schedule.krakenBeat = CreateOrUpdateProfile(
                $"{ProfileFolder}/SaltwindEliteKraken.asset",
                "saltwind_elite_kraken",
                db.GetEnemy("kraken_spawn"),
                SpawnMode.Single,
                spawnCount: 1);
            schedule.swarmPulse = CreateOrUpdateProfile(
                $"{ProfileFolder}/SaltwindSwarmPulse.asset",
                "saltwind_swarm_pulse",
                db.GetEnemy("shark"),
                SpawnMode.Burst,
                spawnCount: 8);
            schedule.drownedPursuer = CreateOrUpdateProfile(
                $"{ProfileFolder}/SaltwindDrownedPursuer.asset",
                "saltwind_drowned_pursuer",
                db.GetEnemy("shark"),
                SpawnMode.Single,
                spawnCount: 1,
                profileIdOverride: "drowned_pursuer");
            schedule.drownedHarrier = CreateOrUpdateProfile(
                $"{ProfileFolder}/SaltwindDrownedHarrier.asset",
                "saltwind_drowned_harrier",
                db.GetEnemy("cannonboat"),
                SpawnMode.Single,
                spawnCount: 1,
                profileIdOverride: "drowned_harrier");

            EditorUtility.SetDirty(schedule);

            var balance = AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(BalanceProfilePath);
            if (balance != null)
            {
                balance.encounterSchedule = schedule;
                balance.enforceCompleteScheduleReferences = true;
                EditorUtility.SetDirty(balance);
            }

            HighSeasEncounterSpawnSetup.SyncScheduleToResources();
            AssetDatabase.SaveAssets();
            Debug.Log("Saltwind Step 04 spawn schedule authored. Resources schedule synced; balance profile linked.");
        }

        static SpawnProfile CreateOrUpdateProfile(
            string path,
            string profileId,
            EnemyDefinition enemy,
            SpawnMode mode,
            float spawnInterval = 1.5f,
            float duration = 0f,
            int spawnCount = 1,
            string profileIdOverride = null)
        {
            var profile = AssetDatabase.LoadAssetAtPath<SpawnProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<SpawnProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            profile.profileId = profileIdOverride ?? profileId;
            profile.enemy = enemy;
            profile.spawnMode = mode;
            profile.timing.spawnIntervalSeconds = spawnInterval;
            profile.timing.durationSeconds = duration;
            profile.timing.spawnCount = spawnCount;
            profile.timing.startDelaySeconds = 0f;
            profile.placement.requireOutsidePlayerFov = true;
            profile.placement.fallbackPolicy = SpawnFallbackPolicy.RetryLater;
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
