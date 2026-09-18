namespace HighSeas.V3
{
    /// <summary>12-weapon combat redesign playtest pool (baseline six + experimental six).</summary>
    public static class V3WeaponPlaytestCatalog
    {
        public static readonly string[] All =
        {
            "deck_cannons",
            "broadside_battery",
            "powder_kegs",
            "flying_cutlasses",
            "flamethrower",
            "lightning_mast",
            "long_nine",
            "swivel_guns",
            "harpoon_gun",
            "mortar"
        };

        public static string GetDisplayLabel(string weaponId) =>
            weaponId?.Replace('_', ' ') ?? "weapon";
    }
}
