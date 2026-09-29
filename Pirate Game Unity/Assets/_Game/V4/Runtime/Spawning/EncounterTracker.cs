using System;
using System.Collections.Generic;

namespace RoguePirates.V4.Spawning
{
    /// <summary>
    /// Required encounters for one run (M14; port of V3's static RunEncounterTracker, was LegacyEncounterTracker): records,
    /// the unit → encounter map and the kill bookkeeping that completes them. Same id sequence and rules; per run instead of
    /// static, and no V3 spawner to cancel pending encounters with (it left the V4 scenes in M9).
    /// </summary>
    public sealed class EncounterTracker : IEncounterTracker
    {
        sealed class Record
        {
            public EncounterKind Kind;
            public EncounterState State;
            public int ExpectedSpawnCount;
            public int SpawnedCount;
            public readonly HashSet<int> Spawned = new HashSet<int>();
            public readonly HashSet<int> Killed = new HashSet<int>();
            public bool IsTerminal => State == EncounterState.Completed || State == EncounterState.Failed;
            public bool IsObjectiveSecured => State == EncounterState.Completed && SpawnedCount >= ExpectedSpawnCount && Killed.Count >= ExpectedSpawnCount;
        }

        int _nextId = 1;
        int _activeBoss;
        readonly Dictionary<int, Record> _records = new Dictionary<int, Record>();
        readonly Dictionary<int, int> _unitToEncounter = new Dictionary<int, int>();
        readonly List<int> _scratch = new List<int>();

        public void ResetForRun()
        {
            _nextId = 1;
            _records.Clear();
            _unitToEncounter.Clear();
            _activeBoss = 0;
        }

        public int Request(EncounterKind kind, int expectedSpawnCount)
        {
            if (expectedSpawnCount <= 0) throw new ArgumentOutOfRangeException(nameof(expectedSpawnCount));
            var id = _nextId++;
            _records[id] = new Record { Kind = kind, State = EncounterState.Requested, ExpectedSpawnCount = expectedSpawnCount };
            if (kind == EncounterKind.Boss) _activeBoss = id;
            return id;
        }

        Record Get(int id) => id > 0 && _records.TryGetValue(id, out var r) ? r : null;

        public bool Exists(int encounterId) => Get(encounterId) != null;
        public EncounterState State(int encounterId) => Get(encounterId)?.State ?? EncounterState.Failed;
        public int SpawnedCount(int encounterId) => Get(encounterId)?.SpawnedCount ?? 0;
        public bool IsTerminal(int encounterId) => Get(encounterId)?.IsTerminal ?? true;

        public void RegisterSpawned(int encounterId, int unitId)
        {
            if (encounterId <= 0 || unitId <= 0) return;
            var r = Get(encounterId);
            if (r == null || r.IsTerminal || !r.Spawned.Add(unitId)) return;
            _unitToEncounter[unitId] = encounterId;
            r.SpawnedCount = r.Spawned.Count;
            if (r.State == EncounterState.Requested) r.State = EncounterState.Spawned;
        }

        /// <summary>A unit left the world without a kill (despawn, cap trim): pooled ids can no longer secure objectives.</summary>
        public void ReleaseUnit(int unitId)
        {
            if (unitId > 0) _unitToEncounter.Remove(unitId);
        }

        /// <summary>The kill fan-out's step (V3 RunEncounterTracker.RegisterAgentKill): true when it completed the encounter.</summary>
        public bool RegisterKill(int unitId)
        {
            if (unitId <= 0 || !_unitToEncounter.TryGetValue(unitId, out var encounterId)) return false;
            var r = Get(encounterId);
            if (r == null || r.IsTerminal || !r.Spawned.Contains(unitId)) return false;
            r.Killed.Add(unitId);
            if (r.SpawnedCount >= r.ExpectedSpawnCount && r.Killed.Count >= r.ExpectedSpawnCount)
            {
                r.State = EncounterState.Completed;
                return true;
            }
            return false;
        }

        public void MarkFailed(int encounterId)
        {
            var r = Get(encounterId);
            if (r == null || r.IsTerminal) return;
            r.State = EncounterState.Failed;
            if (encounterId == _activeBoss) _activeBoss = 0;
            _scratch.Clear();
            foreach (var pair in _unitToEncounter) if (pair.Value == encounterId) _scratch.Add(pair.Key);
            foreach (var unit in _scratch) _unitToEncounter.Remove(unit);
        }

        public bool TryGetOpenBossEncounter(out int encounterId)
        {
            var r = Get(_activeBoss);
            var open = r != null && !r.IsTerminal;
            encounterId = open ? _activeBoss : 0;
            return open;
        }

        public bool BossObjectiveSecured => Get(_activeBoss) is Record r && r.IsObjectiveSecured;
    }
}
