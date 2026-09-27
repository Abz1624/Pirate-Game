using System;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    /// <summary>A Signal Buoy reward: one stat modifier, drawn uniformly from the list (V3's three choices).</summary>
    [Serializable]
    public sealed class BuoyReward
    {
        [Tooltip("Shared stat id (HighSeas StatId value).")] public int stat;
        public float add;
        [Tooltip("Multiplier (1 = none).")] public float multiply = 1f;
        public string label;
    }

    /// <summary>
    /// Stage content (M7): chests, the four landmarks and the Treasury Beacon, filled with V3's values (POIManager,
    /// V3DiscoverySystem, V3InteractionController and the stage profile fields they read). POI placement stays in the
    /// shared StageBuilder (V3StagePoiPlacer on the map stream) until M15; the counts it reads come from here through the
    /// runtime stage profile.
    /// </summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Stage Content", fileName = "StageContent")]
    public sealed class StageContentConfig : V4ConfigAsset
    {
        [Header("Placement (read by the stage builder when the run starts)")]
        [Tooltip("Salvage chests placed on the map.")] [AppliesFrom(7, "read when the run starts")] public int salvageChestCount = 12;
        [Tooltip("Explosive barrels placed on the map. V4 default 0 (adopted decision: no barrels); V3 placed 2.")]
        [AppliesFrom(7, "read when the run starts")] public int explosiveBarrelCount;
        [AppliesFrom(7, "read when the run starts")] public float explosiveBarrelFuseSeconds = 1.2f;
        [AppliesFrom(7, "read when the run starts")] public float explosiveBarrelRadius = 4f;
        [AppliesFrom(7, "read when the run starts")] public int explosiveBarrelDamage = 20;

        [Header("Chests")]
        [Tooltip("First chest price.")] public int chestBasePrice = 20;
        [Tooltip("Price growth per chest bought (price = ceil(increment base × growth^n) after the first).")] public float chestPriceGrowth = 1.22f;
        public int chestPriceIncrementBase = 25;
        [Tooltip("All chests appear at the start instead of one at a time as gold comes in (Saltwind: on).")] [AppliesFrom(7, "read when the run starts")] public bool disableGoldTriggeredChestSpawn = true;
        [Tooltip("The next chest appears once run gold reaches this fraction of its price (V3 0.8).")] public float chestSpawnGoldFraction = 0.8f;
        [Tooltip("Interaction range for chests (m).")] public float chestInteractRange = 6f;
        [Tooltip("R-47 fix (off = V3): the chest's loot roll knows your equipped weapons (V3 looked for them on the ship and never found any).")]
        public bool chestPlanUsesLoadout;
        [Tooltip("R-96 fix (off = V3): a skipped chest is offered again after the ship leaves its range and comes back (V3 dropped the target before it could forget the skip, so only skipping another chest cleared it).")]
        public bool forgetChestSkipWhenLeaving;
        [Tooltip("R-101 fix (off = V3): the prompt lets go of a landmark once it is used (V3 kept it while the ship stayed in range, so the used Beacon kept asking to hold E, looping 0-100% without effect).")]
        public bool promptReleasesUsedLandmark;

        [Header("Landmarks")]
        [Tooltip("Default interaction range (m).")] public float interactRange = 4f;
        [Tooltip("Repair Wreck heals this fraction of max hull (V3's literal; the profile's repairWreckMaxHullFraction was never read).")]
        public float repairWreckHealFraction = 0.25f;
        [Tooltip("Stored in the stage profile only; V3 never read it.")] public float repairWreckMaxHullFraction = 0.25f;
        [Tooltip("Enemies in the Powder Cache ambush.")] public int powderCacheAmbushCount = 3;
        [Tooltip("Sailing this close to a Powder Cache starts its ambush (m).")] public float powderCacheTriggerRadius = 8f;
        [Tooltip("Gold for clearing a Powder Cache (plus a chest-pool item).")] public int powderCacheBonusGold = 20;
        [Tooltip("R-97 fix (off = V3): the Powder Cache pays out once its ambush is cleared (in V3 the hidden marker could never be used again, so it never paid).")]
        public bool powderCachePaysWhenCleared;
        [Tooltip("Seconds inside the circle to charge a Signal Buoy.")] public float signalBuoyChargeSeconds = 8f;
        [Tooltip("Signal Buoy circle radius (m).")] public float signalBuoyRadius = 4f;
        public BuoyReward[] signalBuoyRewards =
        {
            new BuoyReward { stat = 5, multiply = 1.1f, label = "+10% damage for this run" },
            new BuoyReward { stat = 20, add = 0.5f, label = "+0.5 move speed for this run" },
            new BuoyReward { stat = 14, add = 0.8f, label = "+0.8 pickup radius for this run" },
        };
        [Tooltip("Treasure Chart reveals this many nearest chests on the minimap.")] public int treasureChartRevealCount = 3;
        [Tooltip("Lighthouse of Fortune damage multiplier (not placed in Saltwind).")] public float lighthouseDamageMultiplier = 1.08f;
        [Tooltip("Automatic route events off (the V3 route event director, not used since M3).")] public bool disableAutomaticRouteEvents = true;

        [Header("Treasury Beacon and discovery")]
        [Tooltip("POIs and the Beacon within this distance are discovered (m).")] public float discoveryRadius = 24f;
        [Tooltip("Minimap exploration cell size (m).")] public float exploreCellSize = 6f;
        [Tooltip("Cells within this distance of the ship are explored each frame (m).")] public float exploreRadius = 10f;
        [Tooltip("Direction hint on the minimap from this time (s).")] public float beaconDirectionHintSeconds = 300f;
        [Tooltip("Exact marker on the minimap from this time (s).")] public float beaconExactMarkerSeconds = 420f;
        [Tooltip("Beacon activation range (m).")] public float beaconInteractRange = 6f;
        [Tooltip("Hold E this long to activate the Beacon (s).")] public float beaconHoldSeconds = 0.7f;
        [Tooltip("Telegraph ring before the boss appears (s).")] public float bossTelegraphSeconds = 0.7f;
        [Tooltip("R-93 fix (off = V3): sailing to the Beacon still counts as finding it after a minimap hint has shown.")]
        public bool beaconFoundByProximityAfterHints;
        [Tooltip("R-94 fix (off = V3): the exact marker also follows the direction hint (V3 only reached it from hidden).")]
        public bool beaconExactMarkerAfterHint;
        [Tooltip("R-96 fix (off = V3): after a failed summon (\"Beacon failed — clear space\") the Beacon can be activated again (V3 left its telegraph flag set, so it never worked again).")]
        public bool beaconRetryAfterFailedSummon;

        [Header("Encounters")]
        [Tooltip("The spawner the Beacon summons the boss with.")] public EnemySpawnerConfig bossSpawner;
        [Tooltip("The spawner the Powder Cache ambush uses.")] public EnemySpawnerConfig powderCacheSpawner;

        [Header("Boss victory")]
        [Tooltip("The run continues after the boss dies (V3 Saltwind: on).")] public bool continueAfterBossVictory = true;
        [Tooltip("R-102 fix (off = V3): a level-up during the boss fight returns to the boss fight (V3's level-up always resumed to Playing, which doubled ambient spawns and stopped skipping elite beats while the boss lived).")]
        public bool bossFightSurvivesLevelUp;

        [Header("Floor powerups (V3's floor powerup world until M9)")]
        [Tooltip("R-103 fix (off = V3): Magnetic Tide opens every XP, gold and health barrel on the map and their crystals, coins and health fly to you. V3 pulled the XP and gold barrels, which then vanished without paying, and never pulled health barrels; kills drop barrels, so the powerup gave nothing.")]
        public bool magneticTideOpensBarrels;

        public override void Validate(ConfigValidationReport report)
        {
            if (salvageChestCount < 1) report.Error(this, nameof(salvageChestCount), "must be at least 1 (the stage profile rejects 0)");
            if (explosiveBarrelCount < 0) report.Error(this, nameof(explosiveBarrelCount), "must be 0 or more");
            report.Positive(this, nameof(signalBuoyChargeSeconds), signalBuoyChargeSeconds);
            report.Positive(this, nameof(signalBuoyRadius), signalBuoyRadius);
            report.Positive(this, nameof(discoveryRadius), discoveryRadius);
            report.Positive(this, nameof(exploreCellSize), exploreCellSize);
            if (powderCacheAmbushCount < 1) report.Error(this, nameof(powderCacheAmbushCount), "must be at least 1");
            if (signalBuoyRewards == null || signalBuoyRewards.Length == 0) report.Error(this, nameof(signalBuoyRewards), "needs at least one reward");
            else foreach (var r in signalBuoyRewards)
                if (r.stat < 0 || r.stat > 20) report.Error(this, nameof(signalBuoyRewards), $"stat {r.stat} is outside 0–20");
        }
    }
}
