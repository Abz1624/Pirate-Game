using System.Collections.Generic;
using RoguePirates.V4.Config;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// V5 M1: writes the first-pass enemy-flow data into the Saltwind timeline (minute waves with a jagged
    /// minimum-alive floor, hordes, release windows) and turns on merge-into-nearest for pickups. Numbers follow
    /// plan/v5/research/BALANCE_RESEARCH.md; M2's balance model retunes them.
    /// </summary>
    public static class V5FlowDefaults
    {
        const string TimelinePath = "Assets/_Game/V4/Data/Timelines/SaltwindTimeline.asset";
        const string PickupsPath = "Assets/_Game/V4/Data/Build/Pickups_Saltwind.asset";
        const string BalanceVersion = "saltwind-v5-m1";

        // minute start (s), floor share of the ambient cap, rate multiplier, top-up interval (s), max per tick.
        // Jagged on purpose: quiet around the 2:30/3:00 arenas and the 8:00 elite, peaks at 2, 4, 7 and 9.
        static readonly (float t, float share, float rate, float interval, int max)[] Waves =
        {
            (0f,   0.08f, 1.00f, 1.00f, 2),
            (60f,  0.20f, 1.00f, 1.00f, 3),
            (120f, 0.32f, 1.00f, 0.75f, 4),
            (180f, 0.22f, 0.90f, 1.00f, 3),
            (240f, 0.38f, 1.00f, 0.75f, 4),
            (300f, 0.30f, 0.95f, 1.00f, 3),
            (360f, 0.45f, 1.00f, 0.60f, 5),
            (420f, 0.55f, 1.05f, 0.50f, 5),
            (480f, 0.40f, 0.90f, 0.75f, 4),
            (540f, 0.62f, 1.10f, 0.50f, 6),
        };

        [MenuItem("RoguePirates/V5/Write M1 enemy-flow defaults")]
        public static string Apply()
        {
            var timeline = AssetDatabase.LoadAssetAtPath<WaveTimelineConfig>(TimelinePath);
            if (timeline == null) return "timeline not found: " + TimelinePath;
            Undo.RecordObject(timeline, "V5 M1 flow defaults");
            var flow = timeline.flow ?? (timeline.flow = new FlowSettings());
            flow.enabled = true;
            flow.waves = new List<MinuteWave>();
            foreach (var w in Waves)
                flow.waves.Add(new MinuteWave { startSeconds = w.t, minAliveShare = w.share, rateMultiplier = w.rate, topUpIntervalSeconds = w.interval, maxTopUpPerTick = w.max });

            // Horde enemies come from the timeline's own fodder (no content ids in code): the two weakest of the first band's
            // ordinary pool (the stage's opening enemies), and the toughest of the second band's for the later pack.
            var fodder = Fodder(timeline, 0);
            var later = Fodder(timeline, 1);
            var a = fodder.Count > 0 ? fodder[0] : null;
            var b = fodder.Count > 1 ? fodder[1] : a;
            var c = later.Count > 0 ? later[later.Count - 1] : b;
            flow.hordes = new List<HordeEvent>
            {
                Horde("ring_1m45", "Ring horde", 105f, HordeFormation.Ring, a, 14, 20f, 1.25f, 0.8f, "HORDE — they're circling you!"),
                Horde("line_5m30", "Line horde", 330f, HordeFormation.Line, b, 16, 22f, 1.2f, 0.8f, "HORDE — a line incoming!"),
                Horde("cluster_7m40", "Pack horde", 460f, HordeFormation.Cluster, c, 18, 24f, 1.35f, 0.7f, "HORDE — a pack from one side!"),
                Horde("ring_9m15", "Ring horde", 555f, HordeFormation.Ring, a, 24, 21f, 1.3f, 0.7f, "HORDE — they're circling you!"),
            };
            flow.release = new ReleaseSettings();
            timeline.balanceVersion = BalanceVersion;
            EditorUtility.SetDirty(timeline);

            var pickups = AssetDatabase.LoadAssetAtPath<PickupConfig>(PickupsPath);
            if (pickups != null)
            {
                Undo.RecordObject(pickups, "V5 M1 pickup merge");
                pickups.mergeIntoNearestWhenFull = true;
                pickups.balanceVersion = BalanceVersion;
                EditorUtility.SetDirty(pickups);
            }
            AssetDatabase.SaveAssets();
            return $"flow: {flow.waves.Count} waves, {flow.hordes.Count} hordes; pickups merge: {(pickups != null ? "on" : "asset not found")}";
        }

        /// <summary>The ordinary pool of the n-th ambient band, archetypes only, weakest (lowest HP) first.</summary>
        static List<EnemyArchetypeConfig> Fodder(WaveTimelineConfig timeline, int band)
        {
            var list = new List<EnemyArchetypeConfig>();
            var ambient = timeline.phases.FindAll(p => p.ratePerSecond > 0f && p.spawner != null);
            ambient.Sort((x, y) => x.trigger.timeSeconds.CompareTo(y.trigger.timeSeconds));
            if (band >= ambient.Count || ambient[band].pools.Count == 0) return list;
            foreach (var e in ambient[band].pools[0].entries)
                if (e.archetype != null && e.variant == null && !e.archetype.isBoss && !list.Contains(e.archetype)) list.Add(e.archetype);
            list.Sort((x, y) => x.stats.baseHP.CompareTo(y.stats.baseHP));
            return list;
        }

        static HordeEvent Horde(string id, string label, float t, HordeFormation formation, EnemyArchetypeConfig enemy, int count,
            float distance, float speed, float hp, string toast) => new HordeEvent
        {
            hordeId = id, label = label, timeSeconds = t, formation = formation, archetype = enemy, count = count,
            distance = distance, spacing = 1.8f, speedMultiplier = speed, hpMultiplier = hp, damageMultiplier = 1f,
            warningLeadSeconds = 3f, warningToast = toast,
        };
    }
}
