namespace RoguePirates.V4
{
    /// <summary>
    /// Scene-transition payload for the next V4 run (seed, retry flag). The only static in V4 besides constants;
    /// it survives a scene reload because the domain is not reloaded between scenes in a Play session.
    /// </summary>
    public static class RunRequest
    {
        /// <summary>Explicit seed for the next run; 0 means "resolve normally".</summary>
        public static int Seed { get; private set; }

        /// <summary>True when the next run was started from Retry (V3 picks a random seed for retries).</summary>
        public static bool IsRetry { get; private set; }

        public static void RequestSeed(int seed) => Seed = seed;

        public static void MarkRetry() => IsRetry = true;

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
