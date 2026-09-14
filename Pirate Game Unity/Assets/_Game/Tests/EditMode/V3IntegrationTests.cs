using System.Collections.Generic;
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
            Assert.That(V3GameMath.XPToNext(0), Is.EqualTo(25f));
            Assert.That(V3GameMath.XPToNext(3), Is.EqualTo(61f));
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
        public void V3StagePoiPlacer_PlacesTwelveChestsAndFourSpecials()
        {
            var layout = new StageLayout { seed = 12345, playerSpawn = Vector3.zero };
            var rng = new RunRng(12345);
            V3StagePoiPlacer.Place(layout, rng, null);

            var chests = 0;
            var specials = 0;
            for (var i = 0; i < layout.pois.Count; i++)
            {
                if (layout.pois[i].type == POIType.SalvageChest) chests++;
                else specials++;
            }

            Assert.That(chests, Is.EqualTo(12));
            Assert.That(specials, Is.EqualTo(4));

            for (var i = 0; i < layout.pois.Count; i++)
                Assert.That(PlayBounds.IsInsideStormSafeZone(layout.pois[i].position), Is.True);
        }

        [Test]
        public void V3ContentUnlocks_BlocksFlyingCutlassesBeforeFirstConquest()
        {
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
