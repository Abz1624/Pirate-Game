using System;
using NUnit.Framework;

namespace RoguePirates.Hackathon.Tests
{
    /// <summary>Hackathon U-3: the islands, raid and market helpers.</summary>
    public sealed class IslandRulesTests
    {
        [Test]
        public void RaidTarget_ScalesWithDifficultyAndWalls()
        {
            Assert.AreEqual(60, IslandRules.RaidTargetKills(1, 1.0));
            Assert.AreEqual(100, IslandRules.RaidTargetKills(3, 1.0));
            Assert.AreEqual(125, IslandRules.RaidTargetKills(3, 1.25));
            Assert.AreEqual(315, IslandRules.RaidTargetKills(7, 1.75));
            Assert.AreEqual(60, IslandRules.RaidTargetKills(0, double.NaN), "clamped to difficulty 1, hull x1");
            Assert.AreEqual(60, IslandRules.RaidTargetKills(null));
            Assert.AreEqual(80, IslandRules.RaidTargetKills(new DefenderConfig { difficulty = 2, hullMultiplier = 1.0 }));
        }

        [Test]
        public void RaidOutcome_WinPartialLoss()
        {
            Assert.AreEqual((true, false), IslandRules.RaidOutcome(60, 60));
            Assert.AreEqual((true, false), IslandRules.RaidOutcome(75, 60));
            Assert.AreEqual((false, true), IslandRules.RaidOutcome(30, 60));
            Assert.AreEqual((false, false), IslandRules.RaidOutcome(29, 60));
            Assert.AreEqual(31, IslandRules.PartialKills(61));
        }

        [Test]
        public void MapAnchor_FlipsYAndClamps()
        {
            var (x, y) = IslandRules.MapAnchor(120, 140, 1000, 1000);
            Assert.AreEqual(0.12f, x, 1e-5f);
            Assert.AreEqual(0.86f, y, 1e-5f);
            (x, y) = IslandRules.MapAnchor(2000, 2000, 1000, 1000);
            Assert.AreEqual(1f, x, 1e-5f);
            Assert.AreEqual(0f, y, 1e-5f);
            (x, y) = IslandRules.MapAnchor(500, 500, 0, 0);
            Assert.AreEqual(0.5f, x, 1e-5f, "a missing size means 1000");
            Assert.AreEqual(0.5f, y, 1e-5f);
        }

        [Test]
        public void ParseSol_ReadsTypedPrices()
        {
            Assert.AreEqual(50_000_000L, IslandRules.ParseSol("0.05"));
            Assert.AreEqual(50_000_000L, IslandRules.ParseSol(" 0,05 SOL "));
            Assert.AreEqual(1_000_000_000L, IslandRules.ParseSol("1"));
            Assert.AreEqual(-1L, IslandRules.ParseSol("abc"));
            Assert.AreEqual(-1L, IslandRules.ParseSol("-2"));
            Assert.AreEqual(-1L, IslandRules.ParseSol(""));
        }

        [Test]
        public void Labels()
        {
            Assert.AreEqual("RUM store", IslandRules.BuildingName("rum_store"));
            Assert.AreEqual("180 RUM/h, holds 900", IslandRules.BuildingEffect("rum_store", 2));
            Assert.AreEqual("defender hull x1.5", IslandRules.BuildingEffect("walls", 2));
            Assert.AreEqual("+1 defence wave, guns lv 3", IslandRules.BuildingEffect("cannon_tower", 1));
            Assert.AreEqual(0.5f, IslandRules.RumFill(240, 480));
            Assert.AreEqual(1f, IslandRules.RumFill(900, 480));
            Assert.AreEqual(0f, IslandRules.RumFill(10, 0));
            Assert.AreEqual("4:59", IslandRules.Clock(298.2));
            Assert.AreEqual("0:00", IslandRules.Clock(-3));
            var now = new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual("3h 05m", IslandRules.TimeLeft(now, now.AddMinutes(185)));
            Assert.AreEqual("4m 05s", IslandRules.TimeLeft(now, now.AddSeconds(245)));
            Assert.IsNull(IslandRules.TimeLeft(now, now.AddSeconds(-1)));
            Assert.AreEqual("https://explorer.solana.com/address/abc?cluster=devnet", IslandRules.ExplorerAddress("abc"));
            StringAssert.Contains("looted 12 RUM", IslandRules.ResultLine("won", 12));
            StringAssert.Contains("Captain Marrow", IslandRules.ResultLine("lost", 0));
        }

        [Test]
        public void MarkerOf_PicksTheIslandState()
        {
            Assert.AreEqual(IslandRules.Marker.Free, IslandRules.MarkerOf(null));
            Assert.AreEqual(IslandRules.Marker.Mine, IslandRules.MarkerOf(new IslandView { isMine = true, raidable = false }));
            Assert.AreEqual(IslandRules.Marker.Listed, IslandRules.MarkerOf(new IslandView { listed = true }));
            Assert.AreEqual(IslandRules.Marker.Shielded, IslandRules.MarkerOf(new IslandView { shielded = true }));
            Assert.AreEqual(IslandRules.Marker.Raidable, IslandRules.MarkerOf(new IslandView { raidable = true }));
            Assert.AreEqual(IslandRules.Marker.Busy, IslandRules.MarkerOf(new IslandView { raidInProgress = true }));
        }

        [Test]
        public void IslandJson_ReadsTheApiShape()
        {
            const string json = "{\"islandId\":\"i1\",\"name\":\"Rum Haven\",\"slot\":16,\"x\":300,\"y\":780,\"owner\":{\"walletShort\":\"5SBg...jH9A\"},\"isMine\":true," +
                                "\"nft\":{\"status\":\"minted\",\"mint\":\"FCpN\",\"error\":null},\"rum\":{\"exposed\":2,\"vault\":3,\"total\":5,\"cap\":480,\"ratePerHour\":120}," +
                                "\"buildings\":[{\"kind\":\"walls\",\"level\":0,\"maxLevel\":3,\"nextCost\":200},{\"kind\":\"vault\",\"level\":3,\"maxLevel\":3,\"nextCost\":null}]," +
                                "\"defence\":{\"hullMultiplier\":1.25,\"difficulty\":2},\"shieldUntil\":null,\"listingId\":null,\"priceLamports\":null,\"raidable\":false}";
            var v = UnityEngine.JsonUtility.FromJson<IslandView>(json);
            Assert.AreEqual("Rum Haven", v.name);
            Assert.IsTrue(v.nft.Minted);
            Assert.AreEqual(480, v.rum.cap);
            Assert.AreEqual(200, v.Building("walls").nextCost);
            Assert.AreEqual(0, v.Building("vault").nextCost);
            Assert.IsNull(v.Building("rum_store"));
            Assert.AreEqual(1.25, v.defence.hullMultiplier, 1e-9);
            Assert.AreEqual(0, v.priceLamports);
        }
    }
}
