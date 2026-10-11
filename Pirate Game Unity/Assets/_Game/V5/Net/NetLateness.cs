using System;

namespace RoguePirates.V5.Net
{
    /// <summary>
    /// V5 M9b close: the late-input rules on both sides, as pure functions so EditMode tests can pin them.
    /// Server: each snapshot carries the least input margin over the ticks applied since the previous snapshot (newest input
    /// received minus the tick, read when the tick is applied). Client: a snapshot showing a negative margin counts as late;
    /// <see cref="SnapshotsToResync"/> late ones among the last <see cref="WindowSnapshots"/> (or one
    /// <see cref="TicksToResync"/> or more ticks late) move the client's tick ahead.
    /// </summary>
    public static class NetLateness
    {
        /// <summary>Late snapshots are counted over this many (about half a second at 15 Hz).</summary>
        public const int WindowSnapshots = 8;
        /// <summary>Late snapshots in the window before the client moves its tick ahead.</summary>
        public const int SnapshotsToResync = 3;
        /// <summary>A snapshot this many ticks late (or more) moves it ahead at once.</summary>
        public const int TicksToResync = 3;

        /// <summary>Pushes one snapshot's late flag into the window mask; returns how many of the window's snapshots were late.</summary>
        public static int Push(ref int mask, bool late)
        {
            mask = ((mask << 1) | (late ? 1 : 0)) & ((1 << WindowSnapshots) - 1);
            var count = 0;
            for (var m = mask; m != 0; m &= m - 1) count++;
            return count;
        }

        /// <summary>Whether a late snapshot (with this margin and this many late in the window) moves the tick ahead.</summary>
        public static bool Behind(bool late, int margin, int lateInWindow) =>
            late && (margin <= -TicksToResync || lateInWindow >= SnapshotsToResync);

        /// <summary>Server: the least margin so far, with the tick <paramref name="tick"/> applied while the newest input
        /// received is <paramref name="newestReceived"/> (negative when the tick's own input is not here).</summary>
        public static int LeastMargin(int least, uint newestReceived, uint tick) =>
            (int)Math.Min(least, Math.Max(sbyte.MinValue, Math.Min(sbyte.MaxValue, (long)newestReceived - tick)));

        /// <summary>Server: the margin a snapshot carries: the margin now (newest received minus the last applied tick) or
        /// the least at the ticks applied since the last snapshot, whichever is smaller.</summary>
        public static sbyte SnapshotMargin(uint newestReceived, uint lastAck, int least) =>
            (sbyte)Math.Max(sbyte.MinValue, Math.Min(sbyte.MaxValue, Math.Min((long)newestReceived - lastAck, least)));
    }
}
