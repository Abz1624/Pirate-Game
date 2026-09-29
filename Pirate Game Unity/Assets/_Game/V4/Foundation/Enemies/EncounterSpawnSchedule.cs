using System;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Enemies
{
    [Serializable]
    public sealed class ScheduledSpawnEntry
    {
        public SpawnProfile profile;
        [Min(0f)] public float startTimeSeconds;
        public bool loop;
        [Min(0f)] public float loopIntervalSeconds = 30f;
    }

    [CreateAssetMenu(fileName = "EncounterSpawnSchedule", menuName = "RoguePirates/Encounter Spawn Schedule")]
    public sealed class EncounterSpawnSchedule : ScriptableObject
    {
        [Min(0f)] public float gracePeriodSeconds = 2f;
        [Min(0)] public int maxActiveEnemies = 55;
        [Min(0.05f)] public float globalMinSpawnGapSeconds = 0.3f;
        public ScheduledSpawnEntry[] entries;

        [Header("Pressure beats")]
        public SpawnProfile hammerheadBeat;
        public SpawnProfile krakenBeat;
        public SpawnProfile swarmPulse;

        [Header("Overtime")]
        public SpawnProfile drownedPursuer;
        public SpawnProfile drownedHarrier;

        [Header("Route events")]
        public SpawnProfile routeTreasureRaider;
        public SpawnProfile routeCursedRingSwordfish;
        public SpawnProfile routeCursedRingCannon;

        [Header("POI / boss")]
        public SpawnProfile powderCacheAmbush;
        public SpawnProfile bossSummon;
    }
}
