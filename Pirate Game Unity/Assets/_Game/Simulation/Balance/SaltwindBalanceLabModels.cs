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
        public const string Movement =
            "Modeled movement: Conservative kites pre-boss; all skills orbit during boss finale. Average/Expert orbit throughout (not real route choice).";
        public const string Collection =
            "Modeled POI/chest rewards via export route checkpoints (first-chest travel + chest spacing) + chest price curve (not POIManager pathfinding).";
        public const string Offers =
            "Modeled weapon growth via export fill-state weights + pity + weapon stat snapshots (SimRng autopick; not live LevelOfferGenerator UI/logbooks/run items).";
        public const string BossTiming =
            "Boss spawns at profile deadline with export HP/damage; lab clears trash, stops ambient, and focuses weapon DPS on the boss.";
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
