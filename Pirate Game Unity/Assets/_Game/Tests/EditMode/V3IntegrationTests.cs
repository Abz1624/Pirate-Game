using System.Collections.Generic;
using HighSeas.Balance;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.V3;
using HighSeas.World;
using NUnit.Framework;
using UnityEngine;

namespace HighSeas.Tests
{
    public class V3IntegrationTests
    {
        [Test]
        public void V3Economy_ChestPrice_MatchesGddProgression()
        {
            Assert.That(V3Economy.GetChestPrice(0), Is.EqualTo(25));
            Assert.That(V3Economy.GetChestPrice(1), Is.EqualTo(29));
            Assert.That(V3Economy.GetChestPrice(11), Is.LessThanOrEqualTo(1200));
        }

        [Test]
        public void V3GameMath_XPToNext_UsesFivePlusTwoL()
        {
            Assert.That(V3GameMath.XPToNext(0), Is.EqualTo(32f));
            Assert.That(V3GameMath.XPToNext(3), Is.EqualTo(77f));
        }

        [Test]
        public void V3HomeSeed_LocksGeographyAfterFirstBoss()
        {
            V3HomeSeed.ClearLockedHomeSeed();
            V3RunContext.IsActive = true;

            Assert.That(V3HomeSeed.HasLockedHomeSeed, Is.False);
            var firstRun = V3HomeSeed.ResolveRunSeed();
            Assert.That(firstRun, Is.GreaterThan(0));

            V3HomeSeed.TryLockFromRun(424242);
            Assert.That(V3HomeSeed.HasLockedHomeSeed, Is.True);
            Assert.That(V3HomeSeed.ResolveRunSeed(), Is.EqualTo(424242));

            V3HomeSeed.ClearLockedHomeSeed();
            V3RunContext.IsActive = false;
        }

        [Test]
        public void V3StagePoiPlacer_PlacesConquestPois()
        {
            const int seed = 12345;
            var layoutDef = Resources.Load<IslandLayoutDefinition>("V3SaltwindIslandLayout");
            Assert.IsNotNull(layoutDef, "V3SaltwindIslandLayout must exist in Resources.");

            IslandSpawnExclusionRegistry.Clear();
            IslandLayoutSpawner.RegisterPredictedZones(layoutDef, seed);
            PlayBounds.ConfigureFromExclusionRegistry();

            var layout = new StageLayout { seed = seed };
            V3SpawnAnchorValidator.TryResolveAnchors(layout, layoutDef, new RunRng(seed));
            var rng = new RunRng(seed + 404);
            var options = V3StagePoiPlacementOptions.SaltwindSolo();
            V3StagePoiPlacer.Place(layout, rng, layoutDef, options);
            Assert.IsTrue(SaltwindPoiLayoutLogic.Validate(layout, options, layoutDef).IsValid);

            var chests = 0;
            var wrecks = 0;
            var caches = 0;
            var buoys = 0;
            var charts = 0;
            var beacons = 0;
            for (var i = 0; i < layout.pois.Count; i++)
            {
                switch (layout.pois[i].type)
                {
                    case POIType.SalvageChest: chests++; break;
                    case POIType.GildedWreck: wrecks++; break;
                    case POIType.MerchantSkiff: caches++; break;
                    case POIType.TrialBuoy: buoys++; break;
                    case POIType.CursedIdol: charts++; break;
                    case POIType.BossBeacon: beacons++; break;
                }
            }

            Assert.That(chests, Is.EqualTo(12));
            Assert.That(wrecks, Is.EqualTo(1));
            Assert.That(caches, Is.EqualTo(1));
            Assert.That(buoys, Is.EqualTo(1));
            Assert.That(charts, Is.EqualTo(1));
            Assert.That(beacons, Is.EqualTo(1));

            for (var i = 0; i < layout.pois.Count; i++)
                Assert.That(PlayBounds.IsInsideStormSafeZone(layout.pois[i].position), Is.True);

            IslandSpawnExclusionRegistry.Clear();
        }

        [Test]
        public void V3ContentUnlocks_BlocksFlyingCutlassesBeforeFirstConquest()
        {
            if (V3PlaytestFlags.UnlockAllContent)
            {
                Assert.That(V3ContentUnlocks.IsWeaponUnlocked("flying_cutlasses"), Is.True);
                return;
            }

            PlayerPrefs.DeleteKey(V3ContentUnlocks.FirstConquestKey);
            Assert.That(V3ContentUnlocks.IsWeaponUnlocked("deck_cannons"), Is.True);
            Assert.That(V3ContentUnlocks.IsWeaponUnlocked("flying_cutlasses"), Is.False);

            PlayerPrefs.SetInt(V3ContentUnlocks.FirstConquestKey, 1);
            Assert.That(V3ContentUnlocks.IsWeaponUnlocked("flying_cutlasses"), Is.True);
            PlayerPrefs.DeleteKey(V3ContentUnlocks.FirstConquestKey);
        }

        [Test]
        public void V3ChestLoot_Seed12345_ProducesVariedDrops()
        {
            var rng = new RunRng(12345);
            var counts = new Dictionary<string, int>();
            const int rolls = 120;

            for (var i = 0; i < rolls; i++)
            {
                var itemId = V3ChestLoot.RollSalvageItem(rng, 0f);
                counts.TryGetValue(itemId, out var count);
                counts[itemId] = count + 1;
            }

            Assert.That(counts.Count, Is.GreaterThan(1), "Chest loot should roll more than one item type.");
            counts.TryGetValue("lucky_doubloon", out var doubloons);
            Assert.That(doubloons, Is.LessThan(rolls * 0.8f), "Lucky Doubloon should not dominate every chest.");
        }

        [Test]
        public void V3Spawn_Seed12345_ResolvesOutsidePredictedIslands()
        {
            IslandSpawnExclusionRegistry.Clear();
            var layoutDef = Resources.Load<IslandLayoutDefinition>("V3SaltwindIslandLayout");
            Assert.IsNotNull(layoutDef, "V3SaltwindIslandLayout must exist in Resources.");

            IslandLayoutSpawner.RegisterPredictedZones(layoutDef, 12345);

            var stageLayout = new StageLayout
            {
                seed = 12345,
                playerSpawn = layoutDef.playerSpawn
            };

            var spawn = V3SpawnAnchorValidator.ResolveRuntimeSpawn(stageLayout, layoutDef, 12345, 1.15f);
            Assert.IsTrue(
                V3SpawnAnchorValidator.IsSpawnSafe(spawn, layoutDef, 1.15f),
                $"Seed 12345 resolved unsafe spawn {spawn}.");
            Assert.Greater(
                Vector3.Distance(spawn, layoutDef.playerSpawn),
                8f,
                $"Seed 12345 should move spawn away from default anchor inside island shadow ({layoutDef.playerSpawn}).");

            IslandSpawnExclusionRegistry.Clear();
        }

        [Test]
        public void V3FloorPowerup_RollKind_UsesFourWaySplit()
        {
            var rng = new RunRng(9001);
            var counts = new Dictionary<V3FloorPowerupKind, int>();
            for (var i = 0; i < 10_000; i++)
            {
                var kind = V3FloorPowerupWorld.RollKind(rng);
                counts.TryGetValue(kind, out var count);
                counts[kind] = count + 1;
            }

            Assert.That(counts[V3FloorPowerupKind.RepairSupplies], Is.InRange(3_000, 4_000));
            Assert.That(counts[V3FloorPowerupKind.BattleSignal], Is.InRange(3_000, 4_000));
            Assert.That(counts[V3FloorPowerupKind.Tailwind], Is.InRange(1_200, 2_800));
            Assert.That(counts[V3FloorPowerupKind.SalvageChime], Is.InRange(700, 1_500));
        }

        [Test]
        public void V3FloorPowerup_Constants_MatchCombatAppendix()
        {
            Assert.That(V3FloorPowerupWorld.DropChance, Is.EqualTo(0.01f));
            Assert.That(V3FloorPowerupWorld.MaxPerStage, Is.EqualTo(6));
            Assert.That(V3FloorPowerupWorld.CollectRadius, Is.EqualTo(1.5f));
            Assert.That(V3FloorPowerupWorld.LifetimeSeconds, Is.EqualTo(30f));
        }

        [Test]
        public void DEC024_SecureObjective_PersistsThroughPlayerDeath()
        {
            var go = new GameObject("RunManagerTest");
            var run = go.AddComponent<RunManager>();
            run.BeginRun(123);
            run.SecureObjective();
            run.SetState(RunState.PlayerDead);
            Assert.That(run.ObjectiveSecured, Is.True);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void V3DiscoverySystem_RevealChests_AddsIndices()
        {
            var layout = new StageLayout
            {
                playerSpawn = Vector3.zero,
                pois =
                {
                    new POISlot { type = POIType.SalvageChest, position = new Vector3(10f, 0f, 0f) },
                    new POISlot { type = POIType.SalvageChest, position = new Vector3(20f, 0f, 0f) },
                    new POISlot { type = POIType.SalvageChest, position = new Vector3(30f, 0f, 0f) }
                }
            };

            var go = new GameObject("DiscoveryTest");
            var discovery = go.AddComponent<V3DiscoverySystem>();
            discovery.Initialize(layout);
            discovery.RevealNearestChests(2);
            Assert.That(discovery.RevealedChestIndices.Count, Is.EqualTo(2));
            Object.DestroyImmediate(go);
        }
    }
}
