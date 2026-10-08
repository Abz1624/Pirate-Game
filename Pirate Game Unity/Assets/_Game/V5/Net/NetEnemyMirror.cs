using System.Collections.Generic;
using UnityEngine;

namespace RoguePirates.V5.Net
{
    /// <summary>
    /// V5 M9b (I9b.1): the client's copy of the enemies near its ship, from the server's batches. A snapshot's batches
    /// are collected by server tick; once all of a tick's batches are in, enemies it should have listed and did not are
    /// gone (all of them on a full snapshot, the near ones otherwise). Positions are kept for the last two snapshots so
    /// a view can draw them a snapshot behind, interpolated.
    /// </summary>
    public sealed class NetEnemyMirror
    {
        public struct Entry
        {
            public int unitId;
            public byte definition, heading, hp, flags;
            public Vector3 previous, position;
            public uint previousTick, tick;
        }

        readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        readonly HashSet<int> _seen = new HashSet<int>();
        readonly List<int> _gone = new List<int>();
        readonly Dictionary<byte, string> _defs = new Dictionary<byte, string>();
        uint _tick;
        int _batchesIn, _batchCount;
        bool _full, _truncated;
        Vector3 _origin;

        public int Count => _entries.Count;
        public uint LastCompleteTick { get; private set; }
        public int CompleteSnapshots { get; private set; }
        public int DroppedSnapshots { get; private set; }
        public IEnumerable<Entry> All => _entries.Values;
        public bool TryGet(int unitId, out Entry entry) => _entries.TryGetValue(unitId, out entry);
        public string DefinitionId(byte index) => _defs.TryGetValue(index, out var id) ? id : "?";
        public void SetDefinition(byte index, string id) => _defs[index] = id;
        public int KnownDefinitions => _defs.Count;

        public void Clear()
        {
            _entries.Clear();
            _seen.Clear();
            _tick = 0;
            _batchesIn = _batchCount = 0;
        }

        /// <summary>One batch message.</summary>
        public void Apply(NetReader r, float y)
        {
            if (!NetCodec.ReadBatchHeader(r, out var h)) return;
            if (h.serverTick < _tick || h.serverTick <= LastCompleteTick) return; // older than the one being collected
            if (h.serverTick != _tick)
            {
                if (_tick != 0 && _batchesIn < _batchCount) DroppedSnapshots++; // a batch of the previous one was lost
                _tick = h.serverTick;
                _batchesIn = 0;
                _batchCount = h.batchCount;
                _full = h.full;
                _truncated = h.truncated;
                _seen.Clear();
            }
            _origin = new Vector3(h.originX, y, h.originZ);
            for (var i = 0; i < h.count; i++)
            {
                var e = NetCodec.ReadEnemy(r, h, y);
                _seen.Add(e.unitId);
                if (_entries.TryGetValue(e.unitId, out var known))
                {
                    known.previous = known.position;
                    known.previousTick = known.tick;
                }
                else known = new Entry { unitId = e.unitId, previous = e.position, previousTick = h.serverTick };
                known.definition = e.definition;
                known.position = e.position;
                known.tick = h.serverTick;
                known.heading = e.heading;
                known.hp = e.hp;
                known.flags = e.flags;
                _entries[e.unitId] = known;
            }
            if (++_batchesIn >= _batchCount) Complete();
        }

        void Complete()
        {
            _gone.Clear();
            if (_truncated) { LastCompleteTick = _tick; CompleteSnapshots++; return; } // capped: an absence means nothing
            var near2 = NetProtocol.NearRadius * NetProtocol.NearRadius;
            foreach (var pair in _entries)
            {
                if (_seen.Contains(pair.Key)) continue;
                var d = pair.Value.position - _origin;
                if (_full || d.x * d.x + d.z * d.z <= near2) _gone.Add(pair.Key);
            }
            foreach (var id in _gone) _entries.Remove(id);
            LastCompleteTick = _tick;
            CompleteSnapshots++;
        }

        /// <summary>An enemy's position between its last two snapshots (<paramref name="t"/> 0 = the older, 1 = the newer).</summary>
        public static Vector3 Interpolate(in Entry e, float t) => Vector3.Lerp(e.previous, e.position, Mathf.Clamp01(t));
    }
}
