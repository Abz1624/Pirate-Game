namespace RoguePirates.Content
{
    public static class BiomeCatalog
    {
        public const string Saltwind = "saltwind";
        public const string Gravewater = "gravewater";
        public const string DevilsMaw = "devils_maw";

        public static readonly string[] LaunchBiomes = { Saltwind, Gravewater, DevilsMaw };
    }

    public static class MasteryPresets
    {
        public const string FastFlag = "fast_flag";
        public const string LightlyArmed = "lightly_armed";
        public const string SteadyHands = "steady_hands";

        public static readonly string[] ApprovedLaunchPresets = { FastFlag, LightlyArmed, SteadyHands };
    }
}
