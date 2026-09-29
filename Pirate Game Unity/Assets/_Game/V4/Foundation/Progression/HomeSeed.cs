using RoguePirates.V4.Foundation.Core;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Progression
{
    /// <summary>
    /// Saltwind geography for the territory owner: random each run until Barnacle King
    /// is defeated, then locked to that run's seed for all future attempts (local PlayerPrefs).
    /// See plan/02_GAMEPLAY_MODES.md and Integration Checklist M2-029 / D-005.
    /// </summary>
    public static class HomeSeed
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
            if (!ActiveRun.IsActive || HasLockedHomeSeed || runSeed == 0)
                return;

            PlayerPrefs.SetInt(HomeSeedKey, runSeed);
            PlayerPrefs.Save();
            Debug.Log($"HomeSeed: locked Saltwind geography to seed {runSeed}.");
        }

        public static void ClearLockedHomeSeed()
        {
            PlayerPrefs.DeleteKey(HomeSeedKey);
        }
    }
}
