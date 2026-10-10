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
        /// <summary>How far past its newest snapshot an enemy is drawn on its last velocity when the next one is late (6 ticks).
        /// V5 M9b enemy stutter fix (10 Oct): back to 6 from the 9 of the owner re-test fixes; after a hold the render time no
        /// longer runs on past the snapshots (see <see cref="RenderTick(double, bool)"/>), so nothing needs the longer bridge.</summary>
        public const float MaxExtrapolationTicks = 6f;
        /// <summary>The owner re-test fixes' cap (9 ticks), kept to measure their clock against (tests only).</summary>
        public const float ReTestExtrapolationTicks = 9f;

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
            public bool Sample(float renderTick, out Vector3 position, out Vector3 velocity, out byte heading) =>
                Sample(renderTick, MaxExtrapolationTicks, out position, out velocity, out heading);

            public bool Sample(float renderTick, float maxExtrapolationTicks, out Vector3 position, out Vector3 velocity, out byte heading)
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
                    position += velocity * (Mathf.Min(renderTick - TickAt(newest), maxExtrapolationTicks) * NetProtocol.TickSeconds);
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
        ulong _batchMask;
        bool _full, _truncated;
        Vector3 _origin;

        public int Count => _entries.Count;
        public uint LastCompleteTick { get; private set; }
        public int CompleteSnapshots { get; private set; }
        public int DroppedSnapshots { get; private set; }
        /// <summary>Moves faster than any enemy sails, marked as jumps (relocations drawn at once).</summary>
        public int Jumps { get; private set; }
        public Dictionary<int, Entry>.ValueCollection All => _entries.Values; // a struct enumerator: no allocation per frame
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
            _batchMask = 0;
            _renderAt = -1;
            _reanchor = _worldStood = false;
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
        void ClockSample(uint tick, double now, bool steady)
        {
            var offset = now - tick * (double)NetProtocol.TickSeconds;
            // V5 M9b enemy stutter fix (10 Oct): the first snapshot of the world moving again after a hold sets the clock
            // outright. The server's world stood still longer than this client's hold, by about the input delay (the pick's
            // way to the server, the server reaching the pick's tick, the next snapshot's way back). Before, that difference
            // was taken for lateness: the render delay widened to its 350 ms cap (it needed about 470 ms at a 270 ms round
            // trip) and the clock crept back at 20 ms a second, so for 20 s or more after every pick the enemies were drawn
            // past their newest snapshot on their velocity and put back by the next one: the owner's stutter.
            // The same when the server repeated its newest tick (the world stood still: the voyage's start before the first
            // inputs, a pause this client did not hold for): the stand-still is not lateness either. In the local Chrome run
            // (270 ms round trip) of the fix without this, the start alone (the world held until the page's first inputs,
            // under a second) left the delay at its cap and the enemies past their snapshots for the first half minute.
            var stood = _worldStood && _haveClock && !_holding && !ReTestClockForTest;
            _worldStood = false;
            if (_reanchor && !_holding && tick > _releaseTick || stood)
            {
                if (tick > _releaseTick) _reanchor = false;
                _clockOffset = offset;
                _lastClockSample = now;
                Reanchors++;
                return;
            }
            // V5 M9b owner re-test fixes: an arrival handled in a slow frame (a hitch, a hidden page's frame) is late because the
            // page was; it says nothing about the link, so it neither moves the clock nor widens the render delay.
            if (_haveClock && !steady && Math.Abs(offset - _clockOffset) <= 1.0) { _lastClockSample = now; return; }
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
        public float RenderTick(double now) => RenderTick(now, false);

        /// <summary>
        /// <paramref name="held"/> (V5 M9b owner re-test fixes, 10 Oct): the client's ship holds for the level-up cards (or its
        /// page is away): the enemies' clock stands still with it, the clock's offset moving on by the time held, so after
        /// the pick the render time runs on from the same place. The server's world tick (protocol 5) stood still meanwhile.
        /// V5 M9b enemy stutter fix (10 Oct): the server's world moves again only when the pick reaches it and its tick comes
        /// (the input delay later), and its first snapshot after that arrives a further half round trip later; until then
        /// the render time has only the snapshots it already had (its render delay, about 140 ms). So after a hold it runs
        /// on slowed (at most 0.6 of real time, easing toward a quarter as it nears the newest snapshot) instead of past the
        /// snapshots on the enemies' velocity; the first snapshot of the moving world then sets the clock
        /// (<see cref="ClockSample"/>) and the render time eases back to its usual delay within about a second. The
        /// enemies never stand still (the pick fix) and never run ahead of the snapshots to be put back (the stutter).
        /// </summary>
        public float RenderTick(double now, bool held)
        {
            if (!_haveClock) return LastCompleteTick;
            if (held && _holding && _renderAt >= 0)
            {
                if (now > _renderAt) _clockOffset += now - _renderAt;
                _renderAt = now;
                _heldAt = now;
                return (float)_render;
            }
            if (!held && _holding && !ReTestClockForTest)
            {
                // The hold ends (the pick, or the welcome after an away): the next new world tick sets the clock.
                _reanchor = true;
                _releaseTick = Math.Max(LastCompleteTick, _tick);
            }
            // The frame a hold begins still runs the clock up to now first: a page hidden runs no frame for up to a second,
            // and its first hidden frame (the one that sees it is away) must not freeze the enemies where they were a second
            // before; the server's world ran on until the away message reached it.
            var render = Advance(now);
            _holding = held;
            if (held) _heldAt = now;
            else
            {
                RenderFrames++;
                if (render > LastCompleteTick) RenderPastNewestFrames++;
            }
            return render;
        }

        bool _holding, _reanchor, _worldStood;
        uint _releaseTick;

        /// <summary>After a hold, until the world's first new snapshot: the render time's rate (of real time) is the snapshots
        /// it has left ahead of it (ticks, plus one) over <see cref="BridgeGapTicks"/>, between these two.</summary>
        public const float BridgeMaxRate = 0.6f, BridgeMinRate = 0.25f, BridgeGapTicks = 8f;
        /// <summary>For this long after a hold the render time is eased to its target (down to 0.4 of real time, up to 1.25) in
        /// about <see cref="ResumeEaseSeconds"/> (elsewhere it is steered by at most 5%).</summary>
        public const double ResumeSeconds = 3.0, ResumeEaseSeconds = 0.4;
        /// <summary>Tests: the owner re-test fixes' clock (no reset after a hold or a stand-still of the world, steered back at
        /// 15% past the newest snapshot), to measure the enemy stutter against.</summary>
        public bool ReTestClockForTest;
        /// <summary>Times the first snapshot after a hold, or after the world stood still, set the clock.</summary>
        public int Reanchors { get; private set; }
        /// <summary>Frames the render time was asked for while not held, and of those, frames it was past the newest complete
        /// snapshot (each such frame draws the enemies on their velocity, to be put back by the next snapshot).</summary>
        public int RenderFrames { get; private set; }
        public int RenderPastNewestFrames { get; private set; }
        public float RenderPastNewestShare => RenderFrames > 0 ? RenderPastNewestFrames / (float)RenderFrames : 0f;
        public void ResetRenderCounters() { RenderFrames = RenderPastNewestFrames = 0; }

        float Advance(double now)
        {
            if (_reanchor && _renderAt >= 0)
            {
                // After a hold, before the world's first new snapshot: slowed, never far past the snapshots it has.
                var since = now - _renderAt;
                if (since <= 0) return (float)_render;
                _renderAt = now;
                var gap = LastCompleteTick + 1.0 - _render;
                var bridge = Math.Max(BridgeMinRate, Math.Min(BridgeMaxRate, gap / BridgeGapTicks));
                _render += since / NetProtocol.TickSeconds * bridge;
                return (float)_render;
            }
            var target = (now - _clockOffset - DelaySeconds) / NetProtocol.TickSeconds;
            // The render time runs at the frame clock's rate and is steered toward its target by at most 5% of that rate (a
            // snapshot arriving early or the jitter allowance changing would otherwise jerk every enemy forward or back
            // at once); it is set outright when it is a quarter of a second off (a start, a stall). For a few seconds after
            // a hold it is eased back instead (set outright only a second off).
            var resuming = now - _heldAt < ResumeSeconds;
            var off = Math.Abs(target - _render);
            double snapAt = ReTestClockForTest ? (resuming ? 0.6 : 0.25) : resuming ? 1.0 : 0.25;
            if (_renderAt < 0 || off > snapAt / NetProtocol.TickSeconds) { _render = target; _renderAt = now; return (float)_render; }
            var elapsed = now - _renderAt;
            if (elapsed <= 0) return (float)_render;
            _renderAt = now;
            var ticks = elapsed / NetProtocol.TickSeconds;
            var error = target - (_render + ticks);
            double rate, gain;
            if (ReTestClockForTest)
            {
                rate = error < 0 && _render > LastCompleteTick ? 0.15 : 0.05;
                gain = Math.Min(1.0, elapsed / 0.5);
            }
            else if (resuming)
            {
                // Behind its target (the usual case: the server's pause outlasted the hold) it eases back at down to 0.4 of
                // real time; ahead of it (the first snapshot after the hold was handled in a slow frame and set the clock
                // late; the next on-time one moves it earlier) it catches up at up to 1.25.
                rate = error < 0 ? 1.0 - BridgeMaxRate : 0.25;
                gain = Math.Min(1.0, elapsed / ResumeEaseSeconds);
            }
            else
            {
                rate = 0.05;
                gain = Math.Min(1.0, elapsed / 0.5);
            }
            var steer = Math.Max(-rate * ticks, Math.Min(rate * ticks, error * gain));
            _render += ticks + steer;
            return (float)_render;
        }

        double _render, _renderAt = -1, _heldAt = double.NegativeInfinity;

        /// <summary>One batch message.</summary>
        public void Apply(NetReader r, float y) => Apply(r, y, Time.realtimeSinceStartupAsDouble);

        public void Apply(NetReader r, float y, double now) => Apply(r, y, now, true);

        /// <param name="steady">False when this batch is handled in a slow frame (its lateness is the page's, not the link's).</param>
        public void Apply(NetReader r, float y, double now, bool steady)
        {
            if (!NetCodec.ReadBatchHeader(r, out var h)) return;
            // V5 M9b enemy stutter fix: a repeat of the newest tick means the server's world stood still for a snapshot
            // interval at least (a running world has a new tick in every snapshot): its next new tick sets the clock.
            if (h.serverTick == LastCompleteTick && h.serverTick == _tick) _worldStood = true;
            if (h.serverTick < _tick || h.serverTick <= LastCompleteTick) return; // older than the one being collected
            if (h.serverTick != _tick)
            {
                if (_tick != 0 && _batchesIn < _batchCount) DroppedSnapshots++; // a batch of the previous one was lost
                _tick = h.serverTick;
                _batchesIn = 0;
                _batchMask = 0;
                _batchCount = h.batchCount;
                _full = h.full;
                _truncated = h.truncated;
                _seen.Clear();
                ClockSample(h.serverTick, now, steady);
            }
            // Protocol 5: while the run is paused the world tick stands still and later snapshots repeat its batches; one
            // already in is not counted again (a repeat must not complete a tick whose other batch was lost).
            var bit = 1UL << (h.batchIndex & 63);
            if ((_batchMask & bit) != 0) { _worldStood = true; return; }
            _batchMask |= bit;
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
