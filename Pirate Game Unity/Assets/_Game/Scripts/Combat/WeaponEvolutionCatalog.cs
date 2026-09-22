using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.V3;

namespace HighSeas.Combat
{
    public readonly struct WeaponEvolutionDefinition
    {
        public readonly string evolutionId;
        public readonly string baseWeaponId;
        public readonly string requiredItemId;
        public readonly string displayName;
        public readonly string behaviorModifierId;
        public readonly string description;

        public WeaponEvolutionDefinition(
            string evolutionId,
            string baseWeaponId,
            string requiredItemId,
            string displayName,
            string behaviorModifierId,
            string description)
        {
            this.evolutionId = evolutionId;
            this.baseWeaponId = baseWeaponId;
            this.requiredItemId = requiredItemId;
            this.displayName = displayName;
            this.behaviorModifierId = behaviorModifierId;
            this.description = description;
        }
    }

    public static class WeaponEvolutionCatalog
    {
        static readonly WeaponEvolutionDefinition[] All =
        {
            new("stormshot_cannon", "deck_cannons", "grapeshot_pouch", "Stormshot Cannon", "stormshot_cannon",
                "Forward arc pierce; first hit emits a bounded grapeshot burst."),
            new("kings_broadside", "broadside_battery", "broadside_drum", "King's Broadside", "kings_broadside",
                "Volley fires a delayed opposite-side follow-up."),
            new("devils_wake", "powder_kegs", "double_fuse_cord", "Devil's Wake", "devils_wake",
                "Wider aft keg pattern with chained detonations."),
            new("bladed_tide", "flying_cutlasses", "serrated_scabbard", "Bladed Tide", "bladed_tide",
                "Cutlass orbit pulses between close and medium radius."),
            new("hellfire_nozzle", "flamethrower", "dragons_breath_nozzle", "Hellfire Nozzle", "hellfire_nozzle",
                "Wider cone; kills leave short flame patches."),
            new("tempest_mast", "lightning_mast", "copper_conductor", "Tempest Mast", "tempest_mast",
                "Extra chain jump and delayed cluster marker.")
        };

        public static IReadOnlyList<WeaponEvolutionDefinition> Definitions => All;

        public static bool TryGetForWeapon(string weaponId, out WeaponEvolutionDefinition def)
        {
            for (var i = 0; i < All.Length; i++)
            {
                if (All[i].baseWeaponId != weaponId) continue;
                def = All[i];
                return true;
            }

            def = default;
            return false;
        }

        public static bool TryGetByRequiredItemId(string itemId, out WeaponEvolutionDefinition def)
        {
            for (var i = 0; i < All.Length; i++)
            {
                if (All[i].requiredItemId != itemId) continue;
                def = All[i];
                return true;
            }

            def = default;
            return false;
        }

        public static bool TryGetEligible(string weaponId, int weaponLevel, out WeaponEvolutionDefinition def)
        {
            def = default;
            if (weaponLevel < GameConstants.MaxWeaponLevel) return false;
            if (WeaponEvolutionRuntime.IsEvolved(weaponId)) return false;
            if (!TryGetForWeapon(weaponId, out def)) return false;
            return V3RunItemInventory.Instance != null && V3RunItemInventory.Instance.HasItem(def.requiredItemId);
        }

        public static string GetBehaviorModifier(string weaponId)
        {
            if (!WeaponEvolutionRuntime.TryGetEvolutionId(weaponId, out var evolutionId)) return null;
            for (var i = 0; i < All.Length; i++)
            {
                if (All[i].evolutionId == evolutionId)
                    return All[i].behaviorModifierId;
            }
            return null;
        }
    }

    public static class WeaponEvolutionRuntime
    {
        static readonly Dictionary<string, string> EvolvedWeapons = new();

        public static void Reset() => EvolvedWeapons.Clear();

        public static bool IsEvolved(string weaponId) =>
            !string.IsNullOrEmpty(weaponId) && EvolvedWeapons.ContainsKey(weaponId);

        public static bool TryGetEvolutionId(string weaponId, out string evolutionId) =>
            EvolvedWeapons.TryGetValue(weaponId, out evolutionId);

        public static void Apply(string weaponId, string evolutionId)
        {
            if (string.IsNullOrEmpty(weaponId) || string.IsNullOrEmpty(evolutionId)) return;
            EvolvedWeapons[weaponId] = evolutionId;
        }

        public static string GetDisplayName(string weaponId, string defaultName)
        {
            if (!TryGetEvolutionId(weaponId, out var evolutionId)) return defaultName;
            for (var i = 0; i < WeaponEvolutionCatalog.Definitions.Count; i++)
            {
                var def = WeaponEvolutionCatalog.Definitions[i];
                if (def.evolutionId == evolutionId)
                    return def.displayName;
            }
            return defaultName;
        }
    }
}
