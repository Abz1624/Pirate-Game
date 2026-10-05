namespace RoguePirates.V4.Foundation.Combat
{
    /// <summary>
    /// Whether a weapon id can be offered. M16: V3 disabled two of its weapons here by id (grapeshot, boarding party);
    /// neither exists in V4 (no weapon config). Playtest: the flamethrower was out from 3 to 5 Oct and is back (owner);
    /// listing an id here switches it off in voyages and 1v1s (level-up cards, chests, the draft, the shop, starting weapons).
    /// </summary>
    public static class WeaponFeatureFlags
    {
        static readonly string[] Disabled = { };

        public static bool IsWeaponEnabled(string weaponId) => !string.IsNullOrEmpty(weaponId) && System.Array.IndexOf(Disabled, weaponId) < 0;
    }
}
