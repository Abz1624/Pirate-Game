using UnityEngine;

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

        public static string GetMenuDescription(V3CaptainId id) => id switch
        {
            V3CaptainId.Brine =>
                "A steady hand on the wheel and iron in the hull. Hank holds the line when the sea turns ugly, trading speed for survivability while his crew braces for the worst.",
            V3CaptainId.Flint =>
                "Sparky lives for the blast radius. Powder kegs, running fuses, and reckless acceleration—if it can explode, it belongs on his deck.",
            V3CaptainId.Richie =>
                "Richie chases gold and momentum. Pick up the haul, cut a sharp turn, and let the next broadside hit harder than the last.",
            V3CaptainId.Ember =>
                "Ember turns the deck into a furnace. She stacks burn pressure and stretches flame reach until nothing in the cone gets out clean.",
            V3CaptainId.Roxie =>
                "Roxie rides the storm. Lightning chains and burst speed let her dart in, crack hulls, and vanish before return fire finds her.",
            _ =>
                "Rook is the saltwind default—reliable broadsides and a captain who grows deadlier every level. A balanced start for any first conquest."
        };

        public static Color GetMenuThemeColor(V3CaptainId id) => id switch
        {
            V3CaptainId.Brine => new Color(0.12f, 0.42f, 0.34f, 1f),
            V3CaptainId.Flint => new Color(0.38f, 0.18f, 0.48f, 1f),
            V3CaptainId.Richie => new Color(0.52f, 0.38f, 0.12f, 1f),
            V3CaptainId.Ember => new Color(0.62f, 0.22f, 0.1f, 1f),
            V3CaptainId.Roxie => new Color(0.14f, 0.34f, 0.58f, 1f),
            _ => new Color(0.55f, 0.32f, 0.12f, 1f)
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
