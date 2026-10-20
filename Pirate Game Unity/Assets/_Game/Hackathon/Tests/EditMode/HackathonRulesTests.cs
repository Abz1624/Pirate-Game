using NUnit.Framework;
using UnityEngine;

namespace RoguePirates.Hackathon.Tests
{
    /// <summary>Hackathon (12 Oct): the wager, race and rewards helpers, and the API JSON as JsonUtility reads it.</summary>
    public sealed class HackathonRulesTests
    {
        [Test]
        public void RaceTime_MinutesSecondsTenths()
        {
            Assert.AreEqual("1:58.4", HackathonRules.RaceTime(118.4));
            Assert.AreEqual("0:05.0", HackathonRules.RaceTime(5));
            Assert.AreEqual("12:00.9", HackathonRules.RaceTime(720.99));
            Assert.AreEqual("0:00.0", HackathonRules.RaceTime(-3));
        }

        [Test]
        public void Sol_And_Explorer()
        {
            Assert.AreEqual("0.05 SOL", HackathonRules.Sol(50_000_000));
            Assert.AreEqual("No stake", HackathonRules.StakeLabel(0));
            Assert.AreEqual("https://explorer.solana.com/tx/abc?cluster=devnet", HackathonRules.ExplorerTx("abc"));
            Assert.IsNull(HackathonRules.ExplorerTx(null));
            CollectionAssert.AreEqual(new[] { 0L, 10_000_000L, 50_000_000L, 100_000_000L }, HackathonRules.Stakes);
        }

        [Test]
        public void RaceFinished_AtTarget()
        {
            Assert.IsFalse(HackathonRules.RaceFinished(99, 100));
            Assert.IsTrue(HackathonRules.RaceFinished(100, 100));
            Assert.IsTrue(HackathonRules.RaceFinished(100, 0));
        }

        const string LobbyJson =
            "{\"lobbyId\":\"L1\",\"status\":\"full\",\"wager\":{\"wagerId\":\"W1\",\"kind\":\"pvp\",\"lobbyId\":\"L1\",\"matchId\":null," +
            "\"stakeLamports\":10000000,\"stakeSol\":0.01,\"potLamports\":20000000,\"vsHouse\":true,\"status\":\"settled\",\"funded\":true," +
            "\"result\":\"paid_winner\",\"memo\":\"wager:L1\",\"treasury\":\"Gw2ao9v6cAB8oQiJJsy7h1wZuVS8wDyZNj6bU8z5GsG7\",\"cluster\":\"devnet\"," +
            "\"host\":{\"playerId\":\"P1\",\"deposited\":true,\"signature\":\"S1\"},\"guest\":{\"playerId\":\"B\",\"house\":true,\"deposited\":true,\"signature\":null}," +
            "\"winnerPlayerId\":\"P1\",\"payoutSignature\":\"PAY\",\"payouts\":[{\"payoutId\":\"X\",\"lamports\":20000000,\"status\":\"sent\",\"signature\":\"PAY\"}]}}";

        [Test]
        public void WagerJson_ReadsAndExplains()
        {
            var lobby = JsonUtility.FromJson<LobbyWithWager>(LobbyJson);
            var w = lobby.wager;
            Assert.IsTrue(w.Exists);
            Assert.IsTrue(w.Closed);
            Assert.IsTrue(w.vsHouse);
            Assert.IsTrue(w.host.deposited);
            Assert.AreEqual("PAY", HackathonRules.PayoutSignature(w));
            Assert.IsFalse(HackathonRules.PayoutPending(w));
            StringAssert.StartsWith("You won the pot: 0.02 SOL", HackathonRules.ResultLine(w, "P1"));
            w.result = "house_won";
            StringAssert.Contains(HackathonRules.CaptainName + " won", HackathonRules.ResultLine(w, "P1"));
            w.result = "refunded";
            StringAssert.StartsWith("The lobby closed before the match", HackathonRules.ResultLine(w, "P1"));
            w.matchId = "M1";
            StringAssert.StartsWith("Draw or no result", HackathonRules.ResultLine(w, "P1"));
            w.result = null;
            w.status = "funded";
            Assert.IsNull(HackathonRules.ResultLine(w, "P1"));
        }

        [Test]
        public void NoWager_IsNotAWager()
        {
            var lobby = JsonUtility.FromJson<LobbyWithWager>("{\"lobbyId\":\"L1\",\"wager\":null}");
            Assert.IsFalse(lobby.wager != null && lobby.wager.Exists);
            Assert.AreEqual("house matches", HackathonRules.SeatFunding(null, true));
            Assert.AreEqual("no player yet", HackathonRules.SeatFunding(new WagerSeat(), false));
        }

        [Test]
        public void RaceAndRewardsJson_Read()
        {
            var race = JsonUtility.FromJson<DailyRaceResponse>(
                "{\"race\":{\"raceId\":1,\"date\":\"2026-10-12\",\"seed\":1614732695,\"mapId\":\"saltwind\",\"targetKills\":100},\"entries\":2,\"me\":null}");
            Assert.AreEqual(1614732695, race.race.seed);
            Assert.IsFalse(race.HasMe);
            var rewards = JsonUtility.FromJson<WeeklyRewardsResponse>(
                "{\"table\":[{\"rank\":1,\"lamports\":500000000,\"sol\":0.5}],\"winners\":[{\"rank\":1,\"name\":\"A\",\"walletShort\":\"Ab...yz\",\"sol\":0.5,\"payout\":{\"signature\":\"SIG\"}}]}");
            Assert.AreEqual(0.5, rewards.table[0].sol, 1e-9);
            Assert.AreEqual("SIG", rewards.winners[0].payout.signature);
        }

        [Test]
        public void WithRunLog_AddsTheLogAsRawJson_OrLeavesTheBody()
        {
            // V5 M9j: the raid result and the race entry carry the run's own log for the server's checks
            var body = JsonUtility.ToJson(new ConquestResultRequest { won = true, guardianSunk = true, minuteReached = 5, seconds = 301 });
            var log = "{\"format\":\"rp-runlog\",\"v\":1,\"minutes\":[{\"m\":0}]}";
            var withLog = HackathonRules.WithRunLog(body, "runLog", log);
            Assert.AreEqual(body.Substring(0, body.Length - 1) + ",\"runLog\":" + log + "}", withLog);
            var back = JsonUtility.FromJson<ConquestResultRequest>(withLog);
            Assert.IsTrue(back.guardianSunk);
            Assert.AreEqual(301, back.seconds);
            Assert.AreEqual(body, HackathonRules.WithRunLog(body, "runLog", null));
            Assert.AreEqual(body, HackathonRules.WithRunLog(body, "runLog", "not json"));
            Assert.AreEqual("{\"log\":{}}", HackathonRules.WithRunLog("{}", "log", "{}"));

            var entry = JsonUtility.ToJson(new RaceEntryRequest { date = "2026-10-20", seed = 7, timeSeconds = 140.3, kills = 101, level = 5 });
            var withEntryLog = HackathonRules.WithRunLog(entry, "log", log);
            Assert.IsTrue(withEntryLog.EndsWith(",\"log\":" + log + "}"));
            Assert.AreEqual(101, JsonUtility.FromJson<RaceEntryRequest>(withEntryLog).kills);
        }

        [Test]
        public void RaceEntryResponse_ReadsTheRaceCheck()
        {
            var r = JsonUtility.FromJson<RaceEntryResponse>("{\"accepted\":true,\"verdict\":\"pass\",\"raceCheck\":\"too_fast\",\"checked\":false}");
            Assert.AreEqual("too_fast", r.raceCheck);
            Assert.IsFalse(r.@checked);
        }
    }
}
