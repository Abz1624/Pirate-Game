using System.Collections.Generic;

namespace HighSeas.VFX
{
    public static class CannonImpactVfx
    {
        static readonly HashSet<string> CannonWeaponIds = new()
        {
            "deck_cannons",
            "broadside_battery",
            "swivel_guns"
        };

        public static bool IsCannonWeapon(string weaponId) =>
            !string.IsNullOrEmpty(weaponId) && CannonWeaponIds.Contains(weaponId);
    }
}
