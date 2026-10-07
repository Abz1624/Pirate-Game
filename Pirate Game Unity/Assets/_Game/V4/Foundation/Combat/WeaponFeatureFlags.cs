namespace RoguePirates.V4.Foundation.Combat
{
    /// <summary>
    /// Whether a weapon id can be offered. M16: V3 disabled two of its weapons here by id (grapeshot, boarding party);
    /// neither exists in V4 (no weapon config). Playtest: the flamethrower was out from 3 to 5 Oct and is back (owner);
    /// a switched-off weapon is out of voyages and 1v1s (level-up cards, chests, the draft, the shop, starting weapons).
    /// V5 M8z (7 Oct): the list is config (UnlockConfig.disabledWeaponIds), registered here by the menu and the run when
    /// they load their config set (it was an id list in this file).
    /// </summary>
    public static class WeaponFeatureFlags
    {
        static string[] _disabled = System.Array.Empty<string>();

        /// <summary>V5 M8z (7 Oct): the config's switched-off weapons (null = none).</summary>
        public static void Register(string[] disabledWeaponIds) =>
            _disabled = disabledWeaponIds != null ? (string[])disabledWeaponIds.Clone() : System.Array.Empty<string>();

        public static bool IsWeaponEnabled(string weaponId) => !string.IsNullOrEmpty(weaponId) && System.Array.IndexOf(_disabled, weaponId) < 0;
    }
}
