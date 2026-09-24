namespace HighSeas.V3
{
    /// <summary>
    /// Solo Saltwind combat weapon pool — matches
    /// <see cref="HighSeas.Balance.StageBalanceProfile.starterWeaponIds"/> /
    /// <c>SaltwindSoloStageBalanceProfile</c> (6 weapons, 6 loadout slots).
    /// </summary>
    public static class V3WeaponPlaytestCatalog
    {
        public const string DefaultStartingWeaponId = "swivel_guns";

        /// <summary>Marks-gated weapons (meta unlocks); not in <see cref="All"/>.</summary>
        public static readonly string[] MarksUnlockWeaponIds =
        {
            "lightning_mast",
            "flamethrower",
            "mortar",
            "harpoon_gun"
        };

        public static readonly string[] All =
        {
            "deck_cannons",
            "broadside_battery",
            "powder_kegs",
            "flying_cutlasses",
            "long_nine",
            "swivel_guns"
        };

        public static int IndexOf(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return -1;
            for (var i = 0; i < All.Length; i++)
            {
                if (All[i] == weaponId) return i;
            }

            return -1;
        }

        public static string GetDisplayLabel(string weaponId) =>
            weaponId?.Replace('_', ' ') ?? "weapon";
    }
}
