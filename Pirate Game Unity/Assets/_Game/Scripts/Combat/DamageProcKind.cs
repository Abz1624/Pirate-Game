namespace HighSeas.Combat
{
    /// <summary>
    /// Classifies damage for crit/proc eligibility per plan/03_SIMULATION_COMBAT.md damage budgets.
    /// </summary>
    public enum DamageProcKind
    {
        DirectWeapon,
        WhitelistedBridge,
        Dot,
        Environment,
        Reflected,
        TriggeredItem
    }

    public static class DamageProcPolicy
    {
        public const int MaxProcEventsPerResolution = 12;
        static int _procEventsThisResolution;

        public static void BeginDamageResolution() => _procEventsThisResolution = 0;

        public static bool TryRegisterProcEvent()
        {
            if (_procEventsThisResolution >= MaxProcEventsPerResolution) return false;
            _procEventsThisResolution++;
            return true;
        }

        public static bool AllowsCrit(DamageProcKind kind) =>
            kind == DamageProcKind.DirectWeapon || kind == DamageProcKind.WhitelistedBridge;

        public static bool AllowsItemProc(DamageProcKind kind) =>
            kind == DamageProcKind.DirectWeapon || kind == DamageProcKind.WhitelistedBridge;
    }
}
