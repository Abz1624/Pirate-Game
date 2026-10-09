using HighSeas.Core;
using HighSeas.Data;
using UnityEngine;

namespace HighSeas.Combat
{
    public static class WeaponDebugPalette
    {
        public static Color Get(string weaponId, float alpha = 0.55f)
        {
            var rgb = weaponId switch
            {
                "deck_cannons" => new Color(0.35f, 0.85f, 1f),
                "broadside_battery" => new Color(1f, 0.55f, 0.15f),
                "powder_kegs" => new Color(0.95f, 0.35f, 0.25f),
                "flying_cutlasses" => new Color(0.75f, 0.95f, 0.35f),
                "flamethrower" or "burning_oil" => new Color(1f, 0.4f, 0.1f),
                "lightning_mast" => new Color(0.55f, 0.75f, 1f),
                "long_nine" => new Color(0.95f, 0.95f, 0.85f),
                "swivel_guns" => new Color(0.65f, 0.55f, 1f),
                "grapeshot" => new Color(1f, 0.82f, 0.2f),
                "harpoon_gun" => new Color(0.35f, 0.95f, 0.75f),
                "mortar" => new Color(1f, 0.45f, 0.15f),
                "boarding_party" => new Color(0.85f, 0.35f, 0.65f),
                "monkey_buccaneer" => new Color(0.95f, 0.7f, 0.35f),
                _ => new Color(0.7f, 0.7f, 0.7f)
            };
            rgb.a = alpha;
            return rgb;
        }

        public static Color GetLocked(string weaponId) => Get(weaponId, 0.9f);

        public static Color GetIdle(string weaponId) => Get(weaponId, 0.32f);
    }
}
