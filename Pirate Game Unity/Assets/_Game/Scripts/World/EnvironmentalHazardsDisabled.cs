namespace HighSeas.World
{
    /// <summary>Saltwind / V3 runs disable ocean hazards and wind gusts; hazard scripts respect this flag.</summary>
    public static class EnvironmentalHazardsDisabled
    {
        public static bool Value { get; private set; }

        public static void Set(bool disabled) => Value = disabled;
    }
}
