using HighSeas.Data;

namespace HighSeas.V3
{
    /// <summary>Shared display names and flavor text for V3 items, weapons, and logbooks.</summary>
    public static class V3ContentDescriptions
    {
        public static string GetItemName(string itemId) =>
            itemId switch
            {
                "lucky_doubloon" => "Lucky Doubloon",
                "grapeshot_pouch" => "Grapeshot Pouch",
                "double_fuse_cord" => "Double-Fuse Cord",
                "serrated_scabbard" => "Serrated Scabbard",
                "broadside_drum" => "Broadside Drum",
                _ => string.IsNullOrEmpty(itemId) ? "—" : itemId.Replace('_', ' ')
            };

        public static string GetItemDescription(string itemId) =>
            itemId switch
            {
                "lucky_doubloon" => "Increases Luck, improving chest and level-up rarity.",
                "grapeshot_pouch" => "Cannon hits spray shrapnel for bonus damage.",
                "double_fuse_cord" => "Speeds up attacks and weapon cooldowns.",
                "serrated_scabbard" => "Raises critical hit chance.",
                "broadside_drum" => "Adds an extra projectile to qualifying volleys.",
                _ => "Run item with a stacking combat bonus."
            };

        public static string GetWeaponDescription(string weaponId) =>
            weaponId switch
            {
                "deck_cannons" => "Reliable forward cannon fire.",
                "broadside_battery" => "Side-arc volleys into nearby clusters.",
                "powder_kegs" => "Drops explosive kegs along your wake.",
                "flying_cutlasses" => "Orbiting blades strike nearby foes.",
                _ => "Automatic ship weapon."
            };

        public static string GetWeaponDescription(WeaponDefinition def) =>
            def != null && !string.IsNullOrEmpty(def.id)
                ? GetWeaponDescription(def.id)
                : "Automatic ship weapon.";

        public static string GetLogbookDescription(string logbookId) =>
            logbookId switch
            {
                "gunnery_manual" => "Sharpen gunnery — more damage each rank.",
                "powder_drill" => "Powder drill — faster attack speed each rank.",
                "oversized_shot" => "Oversized shot — larger projectiles each rank.",
                "extra_hands" => "Extra hands — more projectiles each rank.",
                "reinforced_hull" => "Reinforced hull — more maximum health each rank.",
                "carpenters_notes" => "Carpenter's notes — stronger hull regen each rank.",
                "infamy_charter" => "Infamy charter — more XP from kills each rank.",
                "treasure_map" => "Treasure map — more Luck each rank.",
                _ => "Passive logbook that scales a core stat."
            };

        public static string GetLogbookDescription(LogbookDefinition def) =>
            def != null && !string.IsNullOrEmpty(def.id)
                ? GetLogbookDescription(def.id)
                : "Passive logbook that scales a core stat.";

        public static string FormatAcquiredItem(string itemId) =>
            $"{GetItemName(itemId)}\n<size=80%>{GetItemDescription(itemId)}</size>";
    }
}
