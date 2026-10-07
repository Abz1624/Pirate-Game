namespace RoguePirates.V4
{
    /// <summary>What the next run is: the Saltwind voyage, or a 1v1 match (Playtest, 3 Oct, owner).</summary>
    public enum RunMode { Voyage, Pvp }

    /// <summary>V5 M9a (I9a.2): where the run's seed came from (recorded in the run log).</summary>
    public enum SeedSource
    {
        /// <summary>Chosen on this device (a first run, a retry, the locked home seed).</summary>
        Local,
        /// <summary>Asked for by the client (dev tools, the Scenario Lab, a 1v1 match seed).</summary>
        Requested,
        /// <summary>Issued by the server for this run (M10: every run that earns value), with its run id.</summary>
        Issued,
    }

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

        public static void RequestSeed(int seed)
        {
            Seed = seed;
            PendingSource = seed != 0 ? SeedSource.Requested : SeedSource.Local;
            PendingIssuedRunId = null;
        }

        /// <summary>
        /// V5 M9a (I9a.2): the seed injection point for a server-issued seed (M10, D6): the next run plays on
        /// <paramref name="seed"/> and its run log records it as issued, with the server's run id. Every gameplay draw then
        /// comes from that seed's streams (RunRng, RunStream), so the client cannot reroll it.
        /// </summary>
        public static void RequestIssuedSeed(int seed, string issuedRunId)
        {
            if (seed == 0) throw new System.ArgumentException("An issued seed is never 0.", nameof(seed));
            Seed = seed;
            PendingSource = SeedSource.Issued;
            PendingIssuedRunId = issuedRunId;
        }

        static SeedSource PendingSource;
        static string PendingIssuedRunId;

        /// <summary>V5 M9a: the seed origin of the run that consumed the last request (<see cref="Consume"/>).</summary>
        public static SeedSource ConsumedSource { get; private set; }
        /// <summary>V5 M9a: the server's run id for an issued seed (null otherwise).</summary>
        public static string ConsumedIssuedRunId { get; private set; }

        /// <summary>V5 M9a: the consumed request's seed origin as one value (the run log's header).</summary>
        public static SeedOrigin ConsumedOrigin => new SeedOrigin(ConsumedSource, ConsumedIssuedRunId);

        /// <summary>V5 M9a (I9a.2): where a run's seed came from, and the server's run id when it was issued.</summary>
        public readonly struct SeedOrigin
        {
            public readonly SeedSource Source;
            public readonly string IssuedRunId;

            public SeedOrigin(SeedSource source, string issuedRunId)
            {
                Source = source;
                IssuedRunId = issuedRunId;
            }
        }

        /// <summary>
        /// The preset of this Play session (M14.13 review fix): set with the request and kept after the run consumed it,
        /// so the menu (which has no run) reads the same configs — e.g. a preset's Captain lock rule (V5 M8z (7 Oct, review)).
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
            PendingSource = SeedSource.Requested;
            PendingIssuedRunId = null;
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
            ConsumedSource = Seed != 0 ? PendingSource : SeedSource.Local;
            ConsumedIssuedRunId = ConsumedSource == SeedSource.Issued ? PendingIssuedRunId : null;
            Seed = 0;
            IsRetry = false;
            PendingSource = SeedSource.Local;
            PendingIssuedRunId = null;
            return result;
        }
    }
}
