using HighSeas.Encounters;
using HighSeas.V3.MultiShip;

namespace HighSeas.Balance
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

        public static void AssertEncountersClearedForTests()
        {
            if (RunEncounterTracker.ActiveBossEncounter != null)
                throw new System.InvalidOperationException("Boss encounter still active after teardown.");
        }

        public static bool ShouldUseStageBalanceProfile() =>
            !V3RunSession.IsLocalMultiplayer;
    }
}
