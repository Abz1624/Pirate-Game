using UnityEngine;

namespace RoguePirates.V4.Rewards
{
    /// <summary>
    /// Gold earned across all voyages (M9 playtest: Richie unlocks at 5,000; M14.13: the menu enforces it). A V4-only
    /// PlayerPrefs key; each run adds its gross gold once at its end.
    /// </summary>
    public static class LifetimeGold
    {
        public const string Key = "v4_lifetime_gold";

        public static int Total => PlayerPrefs.GetInt(Key, 0);

        public static void Add(int earned)
        {
            if (earned <= 0) return;
            PlayerPrefs.SetInt(Key, Total + earned);
            PlayerPrefs.Save();
        }
    }
}
