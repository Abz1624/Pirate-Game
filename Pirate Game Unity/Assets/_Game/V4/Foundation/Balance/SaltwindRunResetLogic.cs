namespace RoguePirates.V4.Foundation.Balance
{
    /// <summary>Documented Saltwind retry/teardown order (Step 10 / C10).</summary>
    public static class SaltwindRunResetLogic
    {
        public const int PhaseCount = 8;

        public static int RetryGeneration { get; private set; }

        public enum Phase
        {
            CancelUiAndPause = 1,
            EndProfileContext = 2,
            TeardownEncountersAndWorld = 3,
            BeginProfileRunIdAndRng = 4,
            BuildLayoutAndPoi = 5,
            ResetDirectors = 6,
            InitializePlayerAndBuild = 7,
            EnterPlaying = 8
        }

        public static void NotifyRetryStarting()
        {
            RetryGeneration++;
        }

    }
}
