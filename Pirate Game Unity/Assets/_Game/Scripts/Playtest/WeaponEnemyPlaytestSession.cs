namespace HighSeas.Playtest
{
    /// <summary>True while the weapon/enemy playtest scene is driving the run (not Saltwind profile).</summary>
    public static class WeaponEnemyPlaytestSession
    {
        public static bool Active { get; internal set; }
    }
}
