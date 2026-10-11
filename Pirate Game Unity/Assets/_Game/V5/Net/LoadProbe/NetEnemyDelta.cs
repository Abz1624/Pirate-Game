using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoguePirates.V5.Net
{
    /// <summary>One enemy as a client holds it: quantized to the delta grid (V5 M9c S0b prototype).</summary>
    public struct QEnemy : IEquatable<QEnemy>
    {
        public int id;
        /// <summary>World position in steps of <see cref="NetEnemyDelta.GridStep"/>.</summary>
        public int gx, gz;
        public byte def, heading, hp, flags;

        public bool Equals(QEnemy o) => id == o.id && gx == o.gx && gz == o.gz && def == o.def && heading == o.heading && hp == o.hp && flags == o.flags;
        public override bool Equals(object obj) => obj is QEnemy o && Equals(o);
        public override int GetHashCode() => id;
        public override string ToString() => $"#{id} d{def} ({gx},{gz}) h{heading} hp{hp} f{flags}";
    }

    /// <summary>A client's enemy table, sorted by unit id (what it holds after a snapshot).</summary>
    public sealed class EnemyTable
    {
        public QEnemy[] Items = new QEnemy[128];
        public int Count;
        public ushort SnapshotId;

        public void Clear() => Count = 0;

        /// <summary>Appends (the caller keeps the ids ascending, or calls <see cref="SortById"/>).</summary>
        public void Add(in QEnemy e)
        {
            if (Count == Items.Length) Array.Resize(ref Items, Count * 2);
            Items[Count++] = e;
        }

        public void SortById() => Array.Sort(Items, 0, Count, IdOrder.Instance);

        public int IndexOf(int id)
        {
            int lo = 0, hi = Count - 1;
            while (lo <= hi)
            {
                var mid = (lo + hi) >> 1;
                var v = Items[mid].id;
                if (v == id) return mid;
                if (v < id) lo = mid + 1; else hi = mid - 1;
            }
            return -1;
        }

        public void CopyFrom(EnemyTable other)
        {
            if (Items.Length < other.Count) Items = new QEnemy[other.Items.Length];
            Array.Copy(other.Items, Items, other.Count);
            Count = other.Count;
            SnapshotId = other.SnapshotId;
        }

        public bool SameAs(EnemyTable other)
        {
            if (other == null || other.Count != Count) return false;
            for (var i = 0; i < Count; i++) if (!Items[i].Equals(other.Items[i])) return false;
            return true;
        }

        sealed class IdOrder : IComparer<QEnemy>
        {
            public static readonly IdOrder Instance = new IdOrder();
            public int Compare(QEnemy a, QEnemy b) => a.id.CompareTo(b.id);
        }
    }

    /// <summary>
    /// V5 M9c S0b (measurement prototype, plan "Mirroring and bandwidth design"): the enemies a client sees, delta-encoded.
    /// The client's table after snapshot n is its table after n-1 with the snapshot's removals and changes applied; a
    /// change is written against the entity's state in the client's newest ACKED snapshot (the baseline, named in the
    /// header) as a small zigzag varint step on a 5 cm grid, or in full (New) when the baseline does not hold it. An
    /// entity the snapshot leaves out keeps the client's newest value (no step back), so a byte budget can leave entities
    /// out and they only grow stale. Over WSS (TCP: in order, nothing lost) the server's model of the client's table is
    /// exact; over a lossy transport an entity changed only in a lost snapshot would stay stale until its next change
    /// (a full resend of entities changed since the baseline fixes that; recorded in the S0b memo).
    /// Layout: version u8, snapshot u16, baseline u16 (0xFFFF none), origin gx i32, gz i32, changed varint, then per
    /// change: id step varint (ascending), mask u8, [New: def u8, x i16, z i16 (grid steps from the origin), heading u8,
    /// hp u8, flags u8] or [Pos: dx, dz zigzag varint][Heading u8][Hp u8][Flags u8]; removed varint, id steps varint.
    /// </summary>
    public static class NetEnemyDelta
    {
        public const byte Version = 1;
        public const float GridStep = 0.05f;
        public const ushort NoBaseline = 0xFFFF;
        public const byte MaskNew = 1, MaskPos = 2, MaskHeading = 4, MaskHp = 8, MaskFlags = 16;
        /// <summary>Header: version, snapshot, baseline, origin, plus a one-byte changed and removed count.</summary>
        public const int HeaderBytes = 1 + 2 + 2 + 8 + 1 + 1;

        public static int Grid(float v) => Mathf.RoundToInt(v / GridStep);

        public static QEnemy Quantize(int unitId, int definition, Vector3 position, Vector3 velocity, float hpFraction, byte flags) => new QEnemy
        {
            id = unitId,
            def = (byte)Mathf.Clamp(definition, 0, 255),
            gx = Grid(position.x),
            gz = Grid(position.z),
            heading = NetCodec.HeadingByte(velocity),
            hp = (byte)Mathf.Clamp(Mathf.RoundToInt(hpFraction * 255f), 0, 255),
            flags = flags,
        };

        public static uint ZigZag(int v) => (uint)((v << 1) ^ (v >> 31));
        public static int UnZigZag(uint v) => (int)(v >> 1) ^ -(int)(v & 1);

        public static int VarBytes(uint v)
        {
            var n = 1;
            while (v >= 0x80) { v >>= 7; n++; }
            return n;
        }

        /// <summary>Bytes of one change (without its id step): in full, or against <paramref name="b"/> when <paramref name="hasBase"/>.</summary>
        public static int ChangeBytes(in QEnemy q, bool hasBase, in QEnemy b)
        {
            if (!hasBase || b.def != q.def) return 1 + 1 + 2 + 2 + 3;
            var n = 1;
            if (q.gx != b.gx || q.gz != b.gz) n += VarBytes(ZigZag(q.gx - b.gx)) + VarBytes(ZigZag(q.gz - b.gz));
            if (q.heading != b.heading) n++;
            if (q.hp != b.hp) n++;
            if (q.flags != b.flags) n++;
            return n;
        }

        static readonly List<int> _changed = new List<int>(256);
        static readonly List<int> _removed = new List<int>(256);

        /// <summary>
        /// Writes the step from <paramref name="previous"/> (the client's table after the last snapshot) to
        /// <paramref name="target"/> (its table after this one), changes against <paramref name="baseline"/> (null: none).
        /// All three sorted by id. Returns the bytes written.
        /// </summary>
        public static int Write(NetWriter w, ushort snapshotId, EnemyTable baseline, EnemyTable previous, EnemyTable target, int originGx, int originGz)
        {
            var start = w.Length;
            _changed.Clear();
            _removed.Clear();
            int i = 0, j = 0;
            var prevCount = previous != null ? previous.Count : 0;
            while (i < target.Count || j < prevCount)
            {
                var t = i < target.Count ? target.Items[i].id : int.MaxValue;
                var p = j < prevCount ? previous.Items[j].id : int.MaxValue;
                if (t == p)
                {
                    if (!target.Items[i].Equals(previous.Items[j])) _changed.Add(i);
                    i++; j++;
                }
                else if (t < p) { _changed.Add(i); i++; }
                else { _removed.Add(p); j++; }
            }

            w.U8(Version);
            w.U16(snapshotId);
            w.U16(baseline != null ? baseline.SnapshotId : NoBaseline);
            w.I32(originGx);
            w.I32(originGz);
            w.VarU32((uint)_changed.Count);
            var prevId = 0;
            foreach (var index in _changed)
            {
                var q = target.Items[index];
                w.VarU32((uint)(q.id - prevId));
                prevId = q.id;
                var bi = baseline != null ? baseline.IndexOf(q.id) : -1;
                var b = bi >= 0 ? baseline.Items[bi] : default;
                if (bi < 0 || b.def != q.def)
                {
                    w.U8(MaskNew);
                    w.U8(q.def);
                    w.I16(ClampShort(q.gx - originGx));
                    w.I16(ClampShort(q.gz - originGz));
                    w.U8(q.heading); w.U8(q.hp); w.U8(q.flags);
                    continue;
                }
                byte mask = 0;
                if (q.gx != b.gx || q.gz != b.gz) mask |= MaskPos;
                if (q.heading != b.heading) mask |= MaskHeading;
                if (q.hp != b.hp) mask |= MaskHp;
                if (q.flags != b.flags) mask |= MaskFlags;
                w.U8(mask);
                if ((mask & MaskPos) != 0) { w.VarU32(ZigZag(q.gx - b.gx)); w.VarU32(ZigZag(q.gz - b.gz)); }
                if ((mask & MaskHeading) != 0) w.U8(q.heading);
                if ((mask & MaskHp) != 0) w.U8(q.hp);
                if ((mask & MaskFlags) != 0) w.U8(q.flags);
            }
            w.VarU32((uint)_removed.Count);
            prevId = 0;
            foreach (var id in _removed) { w.VarU32((uint)(id - prevId)); prevId = id; }
            return w.Length - start;
        }

        /// <summary>A new enemy's offset from the origin must fit an int16 of grid steps (+-1,638 m at 5 cm; the stage is
        /// under 300 m across). The encoder clamps; the probe counts clamps (none expected).</summary>
        public static int Clamps;

        static short ClampShort(int v)
        {
            if (v < short.MinValue || v > short.MaxValue) { Clamps++; return v < 0 ? short.MinValue : short.MaxValue; }
            return (short)v;
        }

        static readonly List<QEnemy> _readChanged = new List<QEnemy>(256);
        static readonly HashSet<int> _readRemoved = new HashSet<int>();

        /// <summary>
        /// The client's side (the round-trip test's decoder): applies a snapshot to <paramref name="table"/> (its table
        /// after the previous snapshot; the result replaces it). <paramref name="baselineOf"/> gives the table the client
        /// held after a snapshot id (its history). False on a malformed message or a missing baseline.
        /// </summary>
        public static bool Read(NetReader r, Func<ushort, EnemyTable> baselineOf, EnemyTable table, EnemyTable scratch, out ushort snapshotId)
        {
            snapshotId = 0;
            if (r.Remaining < HeaderBytes || r.U8() != Version) return false;
            snapshotId = r.U16();
            var baseId = r.U16();
            var originGx = r.I32();
            var originGz = r.I32();
            var baseline = baseId == NoBaseline ? null : baselineOf(baseId);
            if (baseId != NoBaseline && baseline == null) return false;
            _readChanged.Clear();
            _readRemoved.Clear();
            var changed = (int)r.VarU32();
            var id = 0;
            for (var k = 0; k < changed; k++)
            {
                id += (int)r.VarU32();
                var mask = r.U8();
                QEnemy q;
                if ((mask & MaskNew) != 0)
                {
                    q = new QEnemy { id = id, def = r.U8() };
                    q.gx = originGx + r.I16();
                    q.gz = originGz + r.I16();
                    q.heading = r.U8(); q.hp = r.U8(); q.flags = r.U8();
                }
                else
                {
                    var bi = baseline != null ? baseline.IndexOf(id) : -1;
                    if (bi < 0) return false;
                    q = baseline.Items[bi];
                    if ((mask & MaskPos) != 0) { q.gx += UnZigZag(r.VarU32()); q.gz += UnZigZag(r.VarU32()); }
                    if ((mask & MaskHeading) != 0) q.heading = r.U8();
                    if ((mask & MaskHp) != 0) q.hp = r.U8();
                    if ((mask & MaskFlags) != 0) q.flags = r.U8();
                }
                _readChanged.Add(q);
            }
            var removed = (int)r.VarU32();
            id = 0;
            for (var k = 0; k < removed; k++) { id += (int)r.VarU32(); _readRemoved.Add(id); }

            scratch.Clear();
            int i = 0, c = 0;
            while (i < table.Count || c < _readChanged.Count)
            {
                var t = i < table.Count ? table.Items[i].id : int.MaxValue;
                var n = c < _readChanged.Count ? _readChanged[c].id : int.MaxValue;
                if (n <= t)
                {
                    scratch.Add(_readChanged[c]);
                    c++;
                    if (n == t) i++;
                }
                else
                {
                    if (!_readRemoved.Contains(t)) scratch.Add(table.Items[i]);
                    i++;
                }
            }
            table.CopyFrom(scratch);
            table.SnapshotId = snapshotId;
            return true;
        }
    }
}
