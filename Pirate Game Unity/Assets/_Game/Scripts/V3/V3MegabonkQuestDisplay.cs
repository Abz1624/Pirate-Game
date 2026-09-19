using System.Collections.Generic;
using HighSeas.POI;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Megabonk-style run objectives: completed steps + current step only.</summary>
    public static class V3MegabonkQuestDisplay
    {
        public static List<string> BuildVisibleLines(
            BeaconRevealState beaconState,
            bool bossSummoned,
            bool bossDefeated)
        {
            var beaconFound = beaconState >= BeaconRevealState.Discovered;
            var findLabel = beaconState switch
            {
                BeaconRevealState.DirectionSector => "Find the Treasury Beacon (sector hint)",
                BeaconRevealState.ExactMarker => "Find the Treasury Beacon (on map)",
                _ => "Find the Treasury Beacon"
            };

            const string activateLabel = "Activate the Beacon to summon the boss";
            const string defeatLabel = "Defeat the Treasury Boss";

            var lines = new List<string>(3);
            if (!beaconFound)
            {
                lines.Add(FormatStep(findLabel, complete: false, active: true));
                return lines;
            }

            lines.Add(FormatStep(findLabel, complete: true));
            if (!bossSummoned)
            {
                lines.Add(FormatStep(activateLabel, complete: false, active: true));
                return lines;
            }

            lines.Add(FormatStep(activateLabel, complete: true));
            if (!bossDefeated)
                lines.Add(FormatStep(defeatLabel, complete: false, active: true));
            else
                lines.Add(FormatStep(defeatLabel, complete: true));

            return lines;
        }

        public static string FormatStep(string label, bool complete, bool active = false)
        {
            if (complete)
                return $"✓ {label}";
            return active ? $"» {label}" : label;
        }

        public static void GatherRunState(out BeaconRevealState beaconState, out bool bossSummoned, out bool bossDefeated)
        {
            var discovery = V3DiscoverySystem.Instance;
            beaconState = discovery != null ? discovery.BeaconState : BeaconRevealState.Hidden;
            var poi = Object.FindFirstObjectByType<POIManager>();
            bossSummoned = poi != null && poi.BossSummoned;
            bossDefeated = poi != null && poi.BossDefeated;
        }
    }
}
