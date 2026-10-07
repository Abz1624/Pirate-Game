using HighSeas.Data;



namespace HighSeas.V3

{

    /// <summary>Shared display names and flavor text for V3 items, weapons, and logbooks.</summary>

    public static class V3ContentDescriptions

    {

        public static string GetItemName(string itemId) => V3ItemCatalog.GetDisplayName(itemId);



        public static string GetItemDescription(string itemId) => V3ItemCatalog.GetDescription(itemId);



        public static string GetWeaponDescription(string weaponId) =>

            weaponId switch

            {

                "deck_cannons" => "Reliable forward cannon fire.",

                "broadside_battery" => "Side-arc volleys into nearby clusters.",

                "powder_kegs" => "Drops explosive kegs along your wake.",

                "flying_cutlasses" => "Orbiting blades strike nearby foes.",

                "long_nine" => "Long forward line for elite priority shots.",

                "swivel_guns" => "Rapid omni-directional cleanup fire.",

                "grapeshot" => "Short forward cone for close clearing.",

                "harpoon_gun" => "Forward tether that slows priority targets.",

                "mortar" => "Delayed cluster bombardment.",

                "tidal_aura" => "Tidal ring that damages foes inside. Grows every level.",

                "boarding_party" => "Autonomous skiffs chase priority targets.",

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


