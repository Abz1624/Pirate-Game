namespace RoguePirates.V4.Foundation.Combat
{
    /// <summary>Player-facing weapon names for HUD and results (not internal IDs).</summary>
    public static class WeaponDisplayNames
    {
        public static string GetWeaponName(string weaponId) => weaponId switch
        {
            "deck_cannons" => "Deck Cannons",
            "broadside_battery" => "Broadside Battery",
            "powder_kegs" => "Powder Kegs",
            "flying_cutlasses" => "Flying Cutlasses",
            "flamethrower" => "Flamethrower",
            "burning_oil" => "Burning Oil",
            "lightning_mast" => "Lightning Mast",
            "long_nine" => "Long Nine",
            "swivel_guns" => "Swivel Guns",
            "grapeshot" => "Grapeshot",
            "harpoon_gun" => "Harpoon Gun",
            "mortar" => "Mortar",
            "boarding_party" => "Boarding Party",
            _ => string.IsNullOrEmpty(weaponId) ? "Unknown" : weaponId.Replace('_', ' ')
        };
    }
}
