namespace RoguePirates.V4
{
    /// <summary>What the next run is: the Saltwind voyage, or a 1v1 match (Playtest, 3 Oct, owner).</summary>
    public enum RunMode { Voyage, Pvp }

    /// <summary>
    /// Scene-transition payload for the next V4 run (seed, retry flag, config preset). The only static in V4 besides constants;
    /// it survives a scene reload because the domain is not reloaded between scenes in a Play session.
    /// </summary>
    public static class RunRequest
    {
        /// <summary>Explicit seed for the next run; 0 means "resolve normally".</summary>
        public static int Seed { get; private set; }

        /// <summary>True when the next run was started from Retry (V3 picks a random seed for retries).</summary>
        public static bool IsRetry { get; private set; }

        /// <summary>Config preset JSON to apply before the next run starts (applies from t=0); null for defaults.</summary>
        public static string PresetJson { get; private set; }

        public static void RequestSeed(int seed) => Seed = seed;

        /// <summary>
        /// The preset of this Play session (M14.13 review fix): set with the request and kept after the run consumed it,
        /// so the menu (which has no run) reads the same configs — e.g. the v3-parity preset's Captain lock rule.
        /// </summary>
        public static string ActivePresetJson { get; private set; }

        public static void RequestPreset(string presetJson)
        {
            PresetJson = presetJson;
            ActivePresetJson = presetJson;
        }

        /// <summary>Back to the default configs for the rest of the session.</summary>
        public static void ClearActivePreset() => ActivePresetJson = null;

        /// <summary>Reads and clears the pending preset.</summary>
        public static string ConsumePreset()
        {
            var preset = PresetJson;
            PresetJson = null;
            return preset;
        }

        public static void MarkRetry() => IsRetry = true;

        /// <summary>
        /// Playtest (3 Oct, owner): the one PvP switch. The next run is a 1v1 match on the match seed (the match itself is
        /// <see cref="Online.PvpSession"/>'s); everything PvE is off in it. Read and cleared by the run at its start, so the
        /// voyage after it is a voyage again.
        /// </summary>
        public static RunMode Mode { get; private set; }

        public static void RequestPvp(int seed)
        {
            Mode = RunMode.Pvp;
            Seed = seed;
            IsRetry = false;
        }

        /// <summary>The next run is a voyage (the menu's Play).</summary>
        public static void ClearPvp() => Mode = RunMode.Voyage;

        /// <summary>Reads and clears the mode.</summary>
        public static RunMode ConsumeMode()
        {
            var mode = Mode;
            Mode = RunMode.Voyage;
            return mode;
        }

        /// <summary>Dev tools: the next run may start with a locked Captain (the console's "restart as").</summary>
        public static void AllowLockedCaptainOnce() => _allowLockedCaptain = true;
        static bool _allowLockedCaptain;

        /// <summary>Reads and clears the dev override.</summary>
        public static bool ConsumeAllowLockedCaptain()
        {
            var allow = _allowLockedCaptain;
            _allowLockedCaptain = false;
            return allow;
        }

        /// <summary>Reads and clears the request.</summary>
        public static (int seed, bool retry) Consume()
        {
            var result = (Seed, IsRetry);
            Seed = 0;
            IsRetry = false;
            return result;
        }
    }
}
