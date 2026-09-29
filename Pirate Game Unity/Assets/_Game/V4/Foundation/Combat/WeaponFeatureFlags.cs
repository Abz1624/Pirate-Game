namespace RoguePirates.V4.Foundation.Combat
{
    /// <summary>
    /// Whether a weapon id can be offered. M16: V3 disabled two of its weapons here by id (grapeshot, boarding party);
    /// neither exists in V4 (no weapon config), so every configured weapon is enabled.
    /// </summary>
    public static class WeaponFeatureFlags
    {
        public static bool IsWeaponEnabled(string weaponId) => !string.IsNullOrEmpty(weaponId);
    }
}
