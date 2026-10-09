using UnityEngine;

namespace RoguePirates.V5.Net
{
    /// <summary>
    /// V5 M9b playtest fixes (A, B): the net client's clock, the frame's own time. The ship and the enemies are drawn at a
    /// time between net ticks and snapshots; read from the real-time clock in the middle of the frame (after other work)
    /// that time wobbled with the work before it, a wobble the display does not have (it shows frames at a steady rate,
    /// the rate Unity's frame time follows). Unscaled (not slowed by a pause) and not capped like
    /// <see cref="Time.deltaTime"/> (a third of a second a frame), so the net tick count still follows the wall clock after
    /// a hitch.
    /// </summary>
    public static class NetFrameClock
    {
        public static double Now => Time.unscaledTimeAsDouble;
    }
}
