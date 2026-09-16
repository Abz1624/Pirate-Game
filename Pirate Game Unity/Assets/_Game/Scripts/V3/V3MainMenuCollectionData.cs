using System.Collections.Generic;
using HighSeas.Combat;
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
        static readonly (string id, string name, string desc)[] BaselineWeapons =
        {
            ("deck_cannons", "Deck Cannons", "Forward arc generalist."),
            ("broadside_battery", "Broadside Battery", "Port/starboard volleys."),
            ("powder_kegs", "Powder Kegs", "Aft-deployed explosives."),
            ("flying_cutlasses", "Flying Cutlasses", "Orbiting defensive blades."),
            ("lightning_mast", "Lightning Mast", "Omni chain lightning."),
            ("flamethrower", "Flamethrower", "Forward cone burn.")
        };

        static readonly (string id, string name, string desc)[] ExperimentalWeapons =
        {
            ("long_nine", "Long Nine", "Long forward elite line."),
            ("swivel_guns", "Swivel Guns", "Rapid omni cleanup."),
            ("grapeshot", "Grapeshot", "Short forward cone."),
            ("harpoon_gun", "Harpoon Gun", "Forward tether control."),
            ("mortar", "Mortar", "Delayed cluster strike."),
            ("boarding_party", "Boarding Party", "Autonomous skiff chase.")
        };

        public static IReadOnlyList<V3CollectionEntry> GetWeapons(V3CaptainId captain, string profileId = null)
        {
            profileId ??= CombatExperimentContext.SelectedProfileId;
            var profile = CombatExperimentContext.CreateBuiltIn(profileId);
            var entries = new List<V3CollectionEntry>(12);
            for (var i = 0; i < BaselineWeapons.Length; i++)
            {
                var weapon = BaselineWeapons[i];
                entries.Add(WeaponEntry(weapon.id, weapon.name, weapon.desc, captain, IsStarterWeapon(weapon.id, captain)));
            }

            if (profile.weaponSlotCap >= 6)
            {
                for (var i = 0; i < ExperimentalWeapons.Length; i++)
                {
                    var weapon = ExperimentalWeapons[i];
                    entries.Add(WeaponEntry(weapon.id, weapon.name, weapon.desc, captain, false));
                }
            }

            return entries;
        }

        static bool IsStarterWeapon(string id, V3CaptainId captain) =>
            id == V3CaptainProfile.Get(captain).StartingWeaponId
            || (id == "deck_cannons" && captain == V3CaptainId.Rook);

        public static IReadOnlyList<V3CollectionEntry> GetLogbooks()
        {
            var entries = new List<V3CollectionEntry>(V3LogbookCatalog.AllIds.Length);
            for (var i = 0; i < V3LogbookCatalog.AllIds.Length; i++)
            {
                var id = V3LogbookCatalog.AllIds[i];
                entries.Add(new V3CollectionEntry(
                    id,
                    V3LogbookCatalog.GetDisplayName(id),
                    V3ContentDescriptions.GetLogbookDescription(id),
                    V3ContentUnlocks.IsLogbookUnlocked(id),
                    false,
                    "Level-up offers (2 slots)"));
            }

            return entries;
        }

        public static IReadOnlyList<V3CollectionEntry> GetItems()
        {
            var entries = new List<V3CollectionEntry>(V3ItemCatalog.PlaytestItemOrder.Length);
            for (var i = 0; i < V3ItemCatalog.PlaytestItemOrder.Length; i++)
            {
                var id = V3ItemCatalog.PlaytestItemOrder[i];
                entries.Add(new V3CollectionEntry(
                    id,
                    V3ItemCatalog.GetDisplayName(id),
                    V3ItemCatalog.GetDescription(id),
                    V3ContentUnlocks.IsItemUnlocked(id),
                    false,
                    V3PlaytestFlags.UnlockAllContent ? "Playtest — unlocked" : "Chest / POI drop"));
            }

            return entries;
        }

        public static IReadOnlyList<V3CollectionEntry> GetQuests() =>
            new[]
            {
                Entry("saltwind_conquest", "Saltwind Conquest", "Secure the Treasury Beacon.", true, true, "Active voyage"),
                Entry("combat_ab_test", "Combat A/B Test", "Compare profiles A through D.", true, true, "Menu profile selector"),
                Entry("first_home_preview", "First Home Preview", "Preview Ledger after death.", V3PlaytestFlags.UnlockAllContent || V3ContentUnlocks.HasCompletedFirstConquest, false, "Complete first conquest"),
                Entry("flint_unlock", "Unlock Sparky", "Complete first conquest.", V3PlaytestFlags.UnlockAllContent || V3ContentUnlocks.HasCompletedFirstConquest, false, "First conquest")
            };

        static V3CollectionEntry WeaponEntry(
            string id,
            string name,
            string description,
            V3CaptainId captain,
            bool equipped)
        {
            var unlocked = V3ContentUnlocks.IsWeaponUnlocked(id);
            var requirement = V3PlaytestFlags.UnlockAllContent
                ? "Playtest — unlocked"
                : id switch
                {
                    "powder_kegs" => "Captain Flint or first conquest",
                    "flying_cutlasses" => "First conquest reward",
                    "lightning_mast" => "First conquest reward",
                    "flamethrower" => "First conquest reward",
                    "long_nine" or "swivel_guns" or "grapeshot" => "Six-slot combat profile",
                    "harpoon_gun" or "mortar" or "boarding_party" => "Six-slot profile + first conquest",
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
