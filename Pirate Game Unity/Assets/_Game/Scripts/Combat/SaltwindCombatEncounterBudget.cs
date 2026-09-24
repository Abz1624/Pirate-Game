using HighSeas.Balance;
using UnityEngine;

namespace HighSeas.Combat
{
    public static class SaltwindCombatEncounterBudget
    {
        public const float MinChargeWarning = 1f;
        public const float MinExplosiveWarning = 1.2f;

        static int _activeCharges;
        static int _activeAreaDenial;
        static int _activeSlowSources;

        public static void ResetForRun()
        {
            _activeCharges = 0;
            _activeAreaDenial = 0;
            _activeSlowSources = 0;
        }

        static int MaxCharges =>
            StageBalanceProfileContext.Export?.Spawn?.MaxSimultaneousChargeWindups ?? 2;

        static int MaxAreaDenial =>
            StageBalanceProfileContext.Export?.Spawn?.MaxSimultaneousAreaDenial ?? 2;

        public static bool TryReserveCharge(int attackId)
        {
            if (_activeCharges >= MaxCharges) return false;
            _activeCharges++;
            return true;
        }

        public static void ReleaseCharge() => _activeCharges = Mathf.Max(0, _activeCharges - 1);

        public static bool TryReserveAreaDenial(int attackId)
        {
            if (_activeAreaDenial >= MaxAreaDenial) return false;
            _activeAreaDenial++;
            return true;
        }

        public static bool TryReserveRanged(int attackId) => true;

        public static bool TryReserveSlow(int attackId)
        {
            if (_activeSlowSources >= 1) return false;
            _activeSlowSources++;
            return true;
        }

        public static void ReleaseSlow() => _activeSlowSources = Mathf.Max(0, _activeSlowSources - 1);
    }
}
