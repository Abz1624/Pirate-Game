namespace HighSeas.V3
{
    /// <summary>Narrative captain roster (doc §9) mapped to playable V3CaptainId values.</summary>
    public static class V3CaptainCatalog
    {
        public static readonly V3CaptainId[] MenuOrder =
        {
            V3CaptainId.Rook,
            V3CaptainId.Brine,
            V3CaptainId.Richie,
            V3CaptainId.Flint,
            V3CaptainId.Ember,
            V3CaptainId.Roxie
        };

        public static string GetMenuLabel(V3CaptainId id) => id switch
        {
            V3CaptainId.Brine => "Hank",
            V3CaptainId.Flint => "Sparky",
            _ => id.ToString()
        };

        public static string GetDisplayTitle(V3CaptainId id) => id switch
        {
            V3CaptainId.Brine => "Captain Hank (Brine)",
            V3CaptainId.Flint => "Captain Sparky (Flint)",
            V3CaptainId.Richie => "Captain Richie",
            V3CaptainId.Ember => "Captain Ember",
            V3CaptainId.Roxie => "Captain Roxie",
            _ => "Captain Rook"
        };

        public static string GetPassiveSummary(V3CaptainId id) => id switch
        {
            V3CaptainId.Brine => "110 Hull · +0.5 armor per level (cap 25)",
            V3CaptainId.Flint => "Starts Powder Kegs · Running Fuse keg damage while speeding",
            V3CaptainId.Richie => "Gold-focused runner · stronger post-pickup damage spikes",
            V3CaptainId.Ember => "Burn specialist · stronger flame pressure",
            V3CaptainId.Roxie => "High tempo · lightning-forward mobility",
            _ => "+0.5% damage per level (cap 25%)"
        };

        public static string GetActiveSummary(V3CaptainId id) => id switch
        {
            V3CaptainId.Brine => "Brace! (20s) — 60% DR for 2s, −25% speed",
            V3CaptainId.Flint => "Light the Fuse (18s) — arm kegs, +20% blast radius 4s",
            V3CaptainId.Richie => "Cut and Run (16s) — +35% speed, +50% pickup radius 3s",
            V3CaptainId.Ember => "Stoke the Flames (18s) — +30% burn dmg, +20% cone length 5s",
            V3CaptainId.Roxie => "Ride the Gale (15s) — +40% speed 2.5s + bonus chain",
            _ => "All Guns! (18s) — fire all weapons once; cutlass spin boost"
        };

        public static bool IsLocked(V3CaptainId id) =>
            !V3PlaytestFlags.UnlockAllContent
            && id == V3CaptainId.Flint
            && !V3ContentUnlocks.HasCompletedFirstConquest;
    }
}
