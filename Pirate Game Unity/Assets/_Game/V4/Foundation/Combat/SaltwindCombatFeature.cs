using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Playtest;

namespace RoguePirates.V4.Foundation.Combat
{
    /// <summary>Saltwind combat redesign gate — active in solo Saltwind runs and the weapon/enemy playtest harness.</summary>
    public static class SaltwindCombatFeature
    {
        public const int CombatBalanceVersion = 3;

        public static bool IsEnabled =>
            StageBalanceProfileContext.IsActive || WeaponEnemyPlaytestSession.Active;
    }
}
