using RoguePirates.V4.Foundation.Data;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Core
{
    /// <summary>
    /// The demo's switches (30 Sep 2026) that reach below the presentation. <see cref="Off"/> turns every demo switch off
    /// (tests of the regular game set it); the switches themselves live on the DemoJuiceSettings asset.
    /// </summary>
    public static class DemoMode
    {
        public static bool Off;

        static DemoJuiceSettings Settings => Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");

        /// <summary>No points of interest on the stage (chests, landmarks, the Treasury Beacon).</summary>
        public static bool NoPointsOfInterest => !Off && Settings != null && Settings.hidePointsOfInterest;

        /// <summary>UI text without em dashes (the demo's style): "A — B" reads "A: B".</summary>
        public static string Text(string s) => string.IsNullOrEmpty(s) ? s : s.Replace(" — ", ": ").Replace("—", ", ");
    }
}
