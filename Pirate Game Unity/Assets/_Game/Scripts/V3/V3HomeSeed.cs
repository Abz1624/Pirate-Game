using HighSeas.Core;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>
    /// Saltwind geography: random each run until the first boss is defeated,
    /// then locked to that run's seed for all future attempts.
    /// </summary>
    public static class V3HomeSeed
    {
        public const string HomeSeedKey = "v3_home_seed";

        public static bool HasLockedHomeSeed => PlayerPrefs.HasKey(HomeSeedKey);

        public static int GetLockedHomeSeed() => PlayerPrefs.GetInt(HomeSeedKey, GameConstants.DemoRunSeed);

        public static int ResolveRunSeed()
        {
            if (HasLockedHomeSeed)
                return GetLockedHomeSeed();

            return Random.Range(1, int.MaxValue);
        }

        public static void TryLockFromRun(int runSeed)
        {
            if (!V3RunContext.IsActive || HasLockedHomeSeed || runSeed == 0)
                return;

            PlayerPrefs.SetInt(HomeSeedKey, runSeed);
            PlayerPrefs.Save();
            Debug.Log($"V3HomeSeed: locked Saltwind geography to seed {runSeed}.");
        }

        public static void ClearLockedHomeSeed()
        {
            PlayerPrefs.DeleteKey(HomeSeedKey);
        }
    }
}
