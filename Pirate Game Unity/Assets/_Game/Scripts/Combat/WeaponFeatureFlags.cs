using System.Collections.Generic;

namespace HighSeas.Combat
{
    public static class WeaponFeatureFlags
    {
        static readonly HashSet<string> DisabledWeaponIds = new()
        {
            "grapeshot",
            "boarding_party"
        };

        public static bool IsWeaponEnabled(string weaponId) =>
            !string.IsNullOrEmpty(weaponId) && !DisabledWeaponIds.Contains(weaponId);
    }
}
