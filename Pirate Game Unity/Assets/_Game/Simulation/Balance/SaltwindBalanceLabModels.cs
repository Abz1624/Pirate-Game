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
            "Boss spawns at profile deadline; lab clears trash, stops ambient pressure, snaps player into boss arena (v5), then orbit input until boss kill.";
        public const string VictoryTermination =
            "Sim ends immediately on boss objective; survival rate is not post-victory overtime survival and does not prove 12–15 minute runs.";
        public const string SkillHull =
            "Per-skill/per-pool hull and boss HP multipliers are calibration knobs (v6), not live build stats.";
        public const string DamageAvoidance = "Conservative/Average kite more; Expert orbits (higher contact risk).";

        public static string FullMatrixDisclaimer() =>
            Movement + " " + Collection + " " + Offers + " " + BossTiming + " " + VictoryTermination + " " + SkillHull;

        public static string ForPolicy(SaltwindLabBuildPolicy policy) => policy switch
        {
            SaltwindLabBuildPolicy.Precision => "Precision policy: Long Nine / Swivel Guns / Harpoon — single-target bias.",
            SaltwindLabBuildPolicy.Broadside => "Broadside policy: Deck Cannons / Broadside Battery / Powder Kegs.",
            SaltwindLabBuildPolicy.AreaControl => "Area-control policy: Lightning Mast / Flamethrower / Mortar.",
            _ => "Legacy aggregate weapon proxy."
        };
    }
}
