using System.Collections.Generic;
using HighSeas.Core;

namespace HighSeas.V3.MultiShip
{
    public static class V3RandomPlayerNames
    {
        static readonly string[] Names =
        {
            "Captain Saltwind",
            "Blacktooth_Ben",
            "MarauderMae",
            "OneEyedOllie",
            "Stormcaller Rex",
            "CoralReef_Ron",
            "DreadPeg",
            "Silverwake",
            "KrakenKira",
            "BarnacleBill",
            "RumRunner Ray",
            "Tidebreaker Tia",
            "GhostWake Gus",
            "Cutlass_Cass",
            "HarborHank",
            "NeptuneNed",
            "Plankwalker Pete",
            "SirenSong Sam",
            "Broadside_Bex",
            "Deckhand Dex",
            "IronHook Ivy",
            "CrowNest Cole",
            "BrineBaron",
            "Wavechaser Wu",
            "GullGang Gabe",
            "AnchorAnya",
            "PowderKeg Pat",
            "Starboard Steve",
            "Portside Polly",
            "TreasureMap Tom",
            "Cannonball Cara",
            "SeaDog Sven",
            "MizzenMick",
            "JollyRoger_J",
            "Windward Will",
            "Leeward Lee",
            "Driftwood Dana",
            "Sharkbait Shane",
            "CompassRose",
            "NightTide Nico"
        };

        static readonly HashSet<string> Used = new();

        public static void ResetSession() => Used.Clear();

        public static string RollUnique(RunRng rng)
        {
            if (rng == null)
                return Names[Used.Count % Names.Length];

            for (var attempt = 0; attempt < 64; attempt++)
            {
                var index = rng.LootInt(0, Names.Length);
                var candidate = Names[index];
                if (Used.Add(candidate))
                    return candidate;
            }

            var fallback = $"Sailor_{Used.Count + 1}";
            Used.Add(fallback);
            return fallback;
        }
    }
}
