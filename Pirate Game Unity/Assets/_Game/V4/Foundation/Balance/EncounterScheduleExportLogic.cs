using System.Collections.Generic;
using RoguePirates.V4.Foundation.Enemies;
using RoguePirates.Simulation.Balance;

namespace RoguePirates.V4.Foundation.Balance
{
    /// <summary>Captures referenced spawn profile values for reproducible profile content hashing (R9).</summary>
    public static class EncounterScheduleExportLogic
    {
        public static List<StageScheduleProfileSnapshot> SnapshotSchedule(EncounterSpawnSchedule schedule)
        {
            var list = new List<StageScheduleProfileSnapshot>();
            if (schedule == null) return list;

            Add(list, schedule.hammerheadBeat);
            Add(list, schedule.krakenBeat);
            Add(list, schedule.swarmPulse);
            Add(list, schedule.drownedPursuer);
            Add(list, schedule.drownedHarrier);
            Add(list, schedule.powderCacheAmbush);
            Add(list, schedule.bossSummon);
            if (schedule.entries != null)
            {
                for (var i = 0; i < schedule.entries.Length; i++)
                    Add(list, schedule.entries[i]?.profile);
            }

            list.Sort((a, b) => string.CompareOrdinal(a.ProfileKey, b.ProfileKey));
            return list;
        }

        static void Add(List<StageScheduleProfileSnapshot> list, SpawnProfile profile)
        {
            if (profile == null) return;
            var timing = profile.timing ?? new SpawnTimingSettings();
            var elite = profile.elite ?? new SpawnEliteSettings();
            list.Add(new StageScheduleProfileSnapshot
            {
                ProfileKey = profile.ResolvedId,
                EnemyId = profile.enemy != null ? profile.enemy.id : string.Empty,
                SpawnIntervalSeconds = timing.spawnIntervalSeconds,
                DurationSeconds = timing.durationSeconds,
                StartDelaySeconds = timing.startDelaySeconds,
                TriggerTimeSeconds = timing.triggerTimeSeconds,
                SpawnCount = timing.spawnCount,
                EliteHpMultiplier = elite.hpMultiplier,
                EliteDamageMultiplier = elite.damageMultiplier
            });
        }
    }
}
