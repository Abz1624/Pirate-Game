namespace RoguePirates.Simulation.Balance
{
    /// <summary>Modeled offer preferences for Saltwind lab batches (not live navigation).</summary>
    public enum SaltwindLabBuildPolicy
    {
        None,
        Precision,
        Broadside,
        AreaControl
    }

    public enum SaltwindLabUnlockPool
    {
        FreshSave,
        MarksUnlocked
    }

    /// <summary>Human-readable labels for lab reports (Step 12).</summary>
    public static class SaltwindLabBehaviorLabels
    {
        public const string Movement = "Modeled movement: orbit/kite/forward by skill preset (not real route choice).";
        public const string Collection = "Modeled POI/chest rewards via route progress + chest price curve (not POIManager).";
        public const string Offers = "Modeled weapon growth via level-up policy queue (not LevelOfferGenerator).";
        public const string BossTiming = "Boss spawns at profile stage deadline seconds.";
        public const string DamageAvoidance = "Conservative/Average kite more; Expert orbits (higher contact risk).";

        public static string ForPolicy(SaltwindLabBuildPolicy policy) => policy switch
        {
            SaltwindLabBuildPolicy.Precision => "Precision policy: Long Nine / Swivel Guns / Harpoon — single-target bias.",
            SaltwindLabBuildPolicy.Broadside => "Broadside policy: Deck Cannons / Broadside Battery / Powder Kegs.",
            SaltwindLabBuildPolicy.AreaControl => "Area-control policy: Lightning Mast / Flamethrower / Mortar.",
            _ => "Legacy aggregate weapon proxy."
        };
    }
}
