using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoguePirates.V5.Net
{
    /// <summary>
    /// V5 M9b (I9b.1): the client's copy of the enemies near its ship, from the server's batches. A snapshot's batches
    /// are collected by server tick; once all of a tick's batches are in, enemies it should have listed and did not are
    /// gone (all of them on a full snapshot; on a near-only one those well inside the near band).
    /// M9b playtest fixes (B, C): each enemy keeps its last snapshots (<see cref="Track"/>) and the mirror keeps a clock of
    /// the server's ticks (<see cref="RenderTick"/>), so a view draws every enemy at one render time a little behind the
    /// newest snapshot, between the two snapshots around it; a move no enemy can sail between two snapshots (the leash
    /// relocating it) is marked a jump and drawn as one, never as a slide across the screen.
    /// </summary>
    public sealed class NetEnemyMirror
    {
        public struct Entry
        {
            public int unitId;
            public byte definition, heading, hp, flags;
            public Vector3 previous, position;
            public uint previousTick, tick;
            /// <summary>The enemy's last snapshots (shared with its view, which keeps drawing it after it is gone).</summary>
            public Track track;
        }

        /// <summary>The fastest an enemy sails (the quickest kind, 14 m/s, at a 3x charge), plus slack: a move faster than
        /// this between two snapshots is a relocation, not sailing.</summary>
        public const float MaxEnemySpeed = 45f;
        public const float JumpSlackMetres = 1f;
        /// <summary>How far past its newest snapshot an enemy is drawn on its last velocity when the next one is late.</summary>
        public const float MaxExtrapolationTicks = 6f;

        /// <summary>An enemy's newest snapshots (tick, position, heading), oldest first, and where it jumped.</summary>
        public sealed class Track
        {
            public const int Capacity = 8;
            readonly uint[] _ticks = new uint[Capacity];
            readonly Vector3[] _positions = new Vector3[Capacity];
            readonly byte[] _headings = new byte[Capacity];
            readonly bool[] _jumps = new bool[Capacity];
            int _start, _count;

            public int Count => _count;
            public int Jumps { get; private set; }
            int Index(int i) => (_start + i) % Capacity;
            public uint TickAt(int i) => _ticks[Index(i)];
            public Vector3 PositionAt(int i) => _positions[Index(i)];
            public bool JumpAt(int i) => _jumps[Index(i)];
            public uint NewestTick => _count > 0 ? _ticks[Index(_count - 1)] : 0;
            public Vector3 Newest => _count > 0 ? _positions[Index(_count - 1)] : Vector3.zero;

            public void Add(uint tick, Vector3 position, byte heading)
            {
                if (_count > 0 && tick <= NewestTick) return;
                var jump = false;
                if (_count > 0)
                {
                    var last = Newest;
                    var dx = position.x - last.x;
                    var dz = position.z - last.z;
                    var allowed = MaxEnemySpeed * (tick - NewestTick) * NetProtocol.TickSeconds + JumpSlackMetres;
                    jump = dx * dx + dz * dz > allowed * allowed;
                    if (jump) Jumps++;
                }
                int slot;
                if (_count < Capacity) slot = Index(_count++);
                else { slot = _start; _start = (_start + 1) % Capacity; }
                _ticks[slot] = tick;
                _positions[slot] = position;
                _headings[slot] = heading;
                _jumps[slot] = jump;
            }

            /// <summary>
            /// The enemy at server tick <paramref name="renderTick"/> (fractional): between the two snapshots around it; held
            /// at the older one when the newer one is a jump (then drawn there at once); before its first snapshot, at that
            /// one; past its newest, on its last velocity for at most <see cref="MaxExtrapolationTicks"/>.
            /// </summary>
            public bool Sample(float renderTick, out Vector3 position, out Vector3 velocity, out byte heading)
            {
                position = velocity = Vector3.zero;
                heading = 0;
                if (_count == 0) return false;
                var newest = _count - 1;
                if (_count == 1 || renderTick <= TickAt(0))
                {
                    position = PositionAt(0);
                    heading = _headings[Index(0)];
                    return true;
                }
                if (renderTick >= TickAt(newest))
                {
                    position = PositionAt(newest);
                    heading = _headings[Index(newest)];
                    if (JumpAt(newest)) return true;
                    var span = TickAt(newest) - TickAt(newest - 1);
                    velocity = (position - PositionAt(newest - 1)) / (span * NetProtocol.TickSeconds);
                    velocity.y = 0f;
                    position += velocity * (Mathf.Min(renderTick - TickAt(newest), MaxExtrapolationTicks) * NetProtocol.TickSeconds);
                    return true;
                }
                var b = 1;
                while (b < newest && TickAt(b) < renderTick) b++;
                var a = b - 1;
                var pa = PositionAt(a);
                var pb = PositionAt(b);
                if (JumpAt(b))
                {
                    position = pa;
                    heading = _headings[Index(a)];
                    return true;
                }
                var ta = TickAt(a);
                var tb = TickAt(b);
                var f = Mathf.Clamp01((renderTick - ta) / (tb - ta));
                // A cubic through the snapshots (Catmull-Rom, by their ticks): the velocity runs on smoothly through each
                // snapshot instead of changing at once there (a straight line between them turned a curve into a polygon
                // with a speed kink at every snapshot). Across a jump, or at the ends, the straight line's own velocity.
                var segment = (tb - ta) * NetProtocol.TickSeconds;
                var secant = (pb - pa) / segment;
                var ma = a > 0 && !JumpAt(a) ? (pb - PositionAt(a - 1)) / ((tb - TickAt(a - 1)) * NetProtocol.TickSeconds) : secant;
                var mb = b < newest && !JumpAt(b + 1) ? (PositionAt(b + 1) - pa) / ((TickAt(b + 1) - ta) * NetProtocol.TickSeconds) : secant;
                float f2 = f * f, f3 = f2 * f;
                position = (2f * f3 - 3f * f2 + 1f) * pa + (f3 - 2f * f2 + f) * segment * ma + (-2f * f3 + 3f * f2) * pb + (f3 - f2) * segment * mb;
                velocity = ((6f * f2 - 6f * f) * pa + (3f * f2 - 4f * f + 1f) * segment * ma + (-6f * f2 + 6f * f) * pb + (3f * f2 - 2f * f) * segment * mb) / segment;
                position.y = pa.y;
                velocity.y = 0f;
                heading = _headings[Index(b)];
                return true;
            }
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
        /// <summary>Moves faster than any enemy sails, marked as jumps (relocations drawn at once).</summary>
        public int Jumps { get; private set; }
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
            _renderAt = -1;
        }

        // ---- The server's clock (M9b playtest fixes, B) ------------------------------------------------------------------

        double _clockOffset;
        bool _haveClock;
        double _lastClockSample;
        float _lateness;

        /// <summary>Snapshots are drawn this far behind the newest one's server time at least (two snapshot intervals:
        /// one lost or late snapshot still finds the next one in time), plus the measured lateness of arrivals.</summary>
        public const float MinDelaySeconds = 2f * NetProtocol.SnapshotEveryTicks * NetProtocol.TickSeconds;
        public const float MaxDelaySeconds = 0.35f;
        public float DelaySeconds => Mathf.Clamp(MinDelaySeconds + _lateness, MinDelaySeconds, MaxDelaySeconds);

        /// <summary>A new server tick arrived at local time <paramref name="now"/>. The clock follows the earliest arrivals
        /// (the least delayed snapshot; it creeps later by 2% so a lasting change of delay is taken up), and the lateness of
        /// the others decays slowly, so the render delay covers the link's jitter.</summary>
        void ClockSample(uint tick, double now)
        {
            var offset = now - tick * (double)NetProtocol.TickSeconds;
            if (!_haveClock || Math.Abs(offset - _clockOffset) > 1.0)
            {
                _clockOffset = offset;
                _lateness = 0f;
                _haveClock = true;
                _lastClockSample = now;
                return;
            }
            var since = Math.Max(0.0, now - _lastClockSample);
            _lastClockSample = now;
            _clockOffset = Math.Min(offset, _clockOffset + since * 0.02);
            _lateness = Mathf.Max((float)(offset - _clockOffset), _lateness - (float)since * 0.05f);
        }

        /// <summary>The server tick (fractional) the enemies are drawn at now: the newest snapshot's time less
        /// <see cref="DelaySeconds"/>.</summary>
        public float RenderTick(double now)
        {
            if (!_haveClock) return LastCompleteTick;
            var target = (now - _clockOffset - DelaySeconds) / NetProtocol.TickSeconds;
            // The render time runs at the frame clock's rate and is steered toward its target by at most 5% of that rate (a
            // snapshot arriving early or the jitter allowance changing would otherwise jerk every enemy forward or back
            // at once); it is set outright when it is a quarter of a second off (a start, a stall).
            if (_renderAt < 0 || Math.Abs(target - _render) > 0.25 / NetProtocol.TickSeconds) { _render = target; _renderAt = now; return (float)_render; }
            var elapsed = now - _renderAt;
            if (elapsed <= 0) return (float)_render;
            _renderAt = now;
            var ticks = elapsed / NetProtocol.TickSeconds;
            var error = target - (_render + ticks);
            var steer = Math.Max(-0.05 * ticks, Math.Min(0.05 * ticks, error * Math.Min(1.0, elapsed / 0.5)));
            _render += ticks + steer;
            return (float)_render;
        }

        double _render, _renderAt = -1;

        /// <summary>One batch message.</summary>
        public void Apply(NetReader r, float y) => Apply(r, y, Time.realtimeSinceStartupAsDouble);

        public void Apply(NetReader r, float y, double now)
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
                ClockSample(h.serverTick, now);
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
                else known = new Entry { unitId = e.unitId, previous = e.position, previousTick = h.serverTick, track = new Track() };
                known.definition = e.definition;
                known.position = e.position;
                known.tick = h.serverTick;
                known.heading = e.heading;
                known.hp = e.hp;
                known.flags = e.flags;
                var jumpsBefore = known.track.Jumps;
                known.track.Add(h.serverTick, e.position, e.heading);
                Jumps += known.track.Jumps - jumpsBefore;
                _entries[e.unitId] = known;
            }
            if (++_batchesIn >= _batchCount) Complete();
        }

        void Complete()
        {
            _gone.Clear();
            if (_truncated) { LastCompleteTick = _tick; CompleteSnapshots++; return; } // capped: an absence means nothing
            // M9b playtest fixes (C): a near-only snapshot drops only enemies well inside the near band; one that just left
            // it (it is now in the far band, sent every 3rd snapshot) stays until the next full snapshot instead of popping
            // out and back in at the band's edge.
            var near = NetProtocol.NearRadius - NetProtocol.NearDropMargin;
            var near2 = near * near;
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

        /// <summary>The drawing before the M9b playtest fixes, kept to measure against: an enemy between its last two
        /// snapshots by the time since the newest complete snapshot over one snapshot interval (<paramref name="t"/> 0 =
        /// the older, 1 = the newer).</summary>
        public static Vector3 Interpolate(in Entry e, float t) => Vector3.Lerp(e.previous, e.position, Mathf.Clamp01(t));
    }
}
