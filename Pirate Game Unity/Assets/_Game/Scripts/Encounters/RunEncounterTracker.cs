using System;
using System.Collections.Generic;

namespace HighSeas.Encounters
{
    public enum RunEncounterState
    {
        Requested,
        Spawned,
        Completed,
        Failed
    }

    public enum RunEncounterKind
    {
        Boss,
        PowderCache
    }

    public sealed class RunEncounterRecord
    {
        public int EncounterId;
        public RunEncounterKind Kind;
        public RunEncounterState State;
        public int ExpectedSpawnCount;
        public int SpawnedCount;
        public readonly HashSet<int> SpawnedAgentIds = new();
        public readonly HashSet<int> KilledAgentIds = new();

        public bool IsTerminal =>
            State == RunEncounterState.Completed || State == RunEncounterState.Failed;

        public bool IsObjectiveSecured =>
            State == RunEncounterState.Completed &&
            SpawnedCount >= ExpectedSpawnCount &&
            KilledAgentIds.Count >= ExpectedSpawnCount;
    }

    /// <summary>Per-run required encounter lifecycle (boss, powder cache). Not shared across runs.</summary>
    public static class RunEncounterTracker
    {
        static int _nextEncounterId = 1;
        static readonly Dictionary<int, RunEncounterRecord> _encounters = new();
        static readonly Dictionary<int, int> _agentToEncounter = new();
        static int _activeBossEncounterId;

        public static void ResetForNewRun()
        {
            _nextEncounterId = 1;
            _encounters.Clear();
            _agentToEncounter.Clear();
            _activeBossEncounterId = 0;
        }

        public static RunEncounterRecord Request(RunEncounterKind kind, int expectedSpawnCount)
        {
            if (expectedSpawnCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(expectedSpawnCount));

            var record = new RunEncounterRecord
            {
                EncounterId = _nextEncounterId++,
                Kind = kind,
                State = RunEncounterState.Requested,
                ExpectedSpawnCount = expectedSpawnCount
            };
            _encounters[record.EncounterId] = record;
            if (kind == RunEncounterKind.Boss)
                _activeBossEncounterId = record.EncounterId;
            return record;
        }

        public static RunEncounterRecord Get(int encounterId) =>
            encounterId > 0 && _encounters.TryGetValue(encounterId, out var record) ? record : null;

        public static RunEncounterRecord ActiveBossEncounter =>
            _activeBossEncounterId > 0 ? Get(_activeBossEncounterId) : null;

        public static bool TryGetOpenBossEncounter(out RunEncounterRecord record)
        {
            record = ActiveBossEncounter;
            return record != null && !record.IsTerminal;
        }

        public static bool RegisterSpawnedAgent(int encounterId, int agentId)
        {
            if (encounterId <= 0 || agentId <= 0) return false;
            if (!_encounters.TryGetValue(encounterId, out var record) || record.IsTerminal)
                return false;

            if (!record.SpawnedAgentIds.Add(agentId))
                return false;

            _agentToEncounter[agentId] = encounterId;
            record.SpawnedCount = record.SpawnedAgentIds.Count;
            if (record.State == RunEncounterState.Requested)
                record.State = RunEncounterState.Spawned;
            return true;
        }

        /// <summary>Call when an agent leaves the world without a kill (despawn, cap trim) so pooled ids cannot secure objectives.</summary>
        public static void ReleaseAgentMapping(int agentId)
        {
            if (agentId <= 0) return;
            _agentToEncounter.Remove(agentId);
        }

        public static bool RegisterAgentKill(int agentId)
        {
            if (agentId <= 0 || !_agentToEncounter.TryGetValue(agentId, out var encounterId))
                return false;
            if (!_encounters.TryGetValue(encounterId, out var record) || record.IsTerminal)
                return false;
            if (!record.SpawnedAgentIds.Contains(agentId))
                return false;

            record.KilledAgentIds.Add(agentId);
            if (record.SpawnedCount >= record.ExpectedSpawnCount &&
                record.KilledAgentIds.Count >= record.ExpectedSpawnCount)
            {
                record.State = RunEncounterState.Completed;
                return true;
            }

            return false;
        }

        public static void MarkFailed(int encounterId)
        {
            if (!_encounters.TryGetValue(encounterId, out var record) || record.IsTerminal)
                return;
            record.State = RunEncounterState.Failed;
            if (encounterId == _activeBossEncounterId)
                _activeBossEncounterId = 0;
            ClearAgentMappingsForEncounter(encounterId);
            Enemies.EncounterSpawner.Instance?.CancelPendingEncounter(encounterId);
        }

        static void ClearAgentMappingsForEncounter(int encounterId)
        {
            var toRemove = new List<int>();
            foreach (var pair in _agentToEncounter)
            {
                if (pair.Value == encounterId)
                    toRemove.Add(pair.Key);
            }

            for (var i = 0; i < toRemove.Count; i++)
                _agentToEncounter.Remove(toRemove[i]);
        }

        public static bool BossObjectiveSecured =>
            ActiveBossEncounter != null && ActiveBossEncounter.IsObjectiveSecured;

        public static void ResetForTests() => ResetForNewRun();
    }
}
