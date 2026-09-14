using System.Collections.Generic;
using HighSeas.Core;
using UnityEngine;

namespace HighSeas.V3
{
    public readonly struct V3CollectionEntry
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public bool Unlocked { get; }
        public bool Equipped { get; }
        public string UnlockRequirement { get; }

        public V3CollectionEntry(
            string id,
            string displayName,
            string description,
            bool unlocked,
            bool equipped,
            string unlockRequirement)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            Unlocked = unlocked;
            Equipped = equipped;
            UnlockRequirement = unlockRequirement;
        }
    }

    /// <summary>Local prototype collection data until M3 account persistence exists.</summary>
    public static class V3MainMenuCollectionData
    {
        public static IReadOnlyList<V3CollectionEntry> GetWeapons(V3CaptainId captain) =>
            new[]
            {
                WeaponEntry("deck_cannons", "Deck Cannons", "Reliable forward fire.", captain, true),
                WeaponEntry("broadside_battery", "Broadside Battery", "Side arc volleys.", captain, false),
                WeaponEntry("powder_kegs", "Powder Kegs", "Dropped explosives.", captain, captain == V3CaptainId.Flint),
                WeaponEntry("flying_cutlasses", "Flying Cutlasses", "Orbiting blades.", captain, false)
            };

        public static IReadOnlyList<V3CollectionEntry> GetLogbooks() =>
            new[]
            {
                LogbookEntry("gunner", "Gunner's Log", "+Damage per rank."),
                LogbookEntry("powdermaster", "Powdermaster", "+Attack speed."),
                LogbookEntry("navigator", "Navigator", "+Move speed."),
                LogbookEntry("carpenter", "Carpenter", "+Hull."),
                LogbookEntry("lookout", "Lookout", "+Pickup radius."),
                LogbookEntry("quartermaster", "Quartermaster", "+Gold gain."),
                LogbookEntry("scavenger", "Scavenger", "+Luck."),
                LogbookEntry("cartographer", "Cartographer", "Chest discount.")
            };

        public static IReadOnlyList<V3CollectionEntry> GetItems() =>
            new[]
            {
                Entry("lucky_doubloon", "Lucky Doubloon", "+Luck.", true, false, "Common drop"),
                Entry("grapeshot_pouch", "Grapeshot Pouch", "+Damage.", false, false, "Powder Cache"),
                Entry("double_fuse_cord", "Double-Fuse Cord", "+Attack speed.", false, false, "Chest drop"),
                Entry("serrated_scabbard", "Serrated Scabbard", "+Crit chance.", false, false, "Chest drop"),
                Entry("broadside_drum", "Broadside Drum", "+Projectiles.", false, false, "Boss chest")
            };

        public static IReadOnlyList<V3CollectionEntry> GetQuests() =>
            new[]
            {
                Entry("saltwind_conquest", "Saltwind Conquest", "Secure the Treasury Beacon.", true, true, "Active voyage"),
                Entry("first_home_preview", "First Home Preview", "Preview Ledger after death.", false, false, "Complete first conquest"),
                Entry("flint_unlock", "Unlock Flint", "Complete first conquest.", V3ContentUnlocks.HasCompletedFirstConquest, false, "First conquest")
            };

        static V3CollectionEntry WeaponEntry(
            string id,
            string name,
            string description,
            V3CaptainId captain,
            bool equipped)
        {
            var unlocked = V3ContentUnlocks.IsWeaponUnlocked(id);
            var requirement = id switch
            {
                "powder_kegs" => "Captain Flint or first conquest",
                "flying_cutlasses" => "First conquest reward",
                _ => "Starter"
            };
            return new V3CollectionEntry(id, name, description, unlocked, equipped && unlocked, requirement);
        }

        static V3CollectionEntry LogbookEntry(string id, string name, string description) =>
            new(id, name, description, V3ContentUnlocks.IsLogbookUnlocked(id), false, "Level-up pool");

        static V3CollectionEntry Entry(
            string id,
            string name,
            string description,
            bool unlocked,
            bool equipped,
            string requirement) =>
            new(id, name, description, unlocked, equipped, requirement);
    }
}
