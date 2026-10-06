using System.Collections.Generic;
using NUnit.Framework;
using RoguePirates.V4.Online;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>Playtest (3 Oct, owner): the 1v1 mode's pure rules (auto level-up order, PvP damage, storm, clock, JSON).</summary>
    public sealed class PlaytestPvpTests
    {
        static readonly string[] W = { "w1", "w2", "w3" };
        static readonly string[] L = { "l1", "l2", "l3" };

        [Test]
        public void Level_order_interleaves_weapons_and_logbooks()
        {
            var order = new PvpLevelOrder(W, L);
            CollectionAssert.AreEqual(new[] { "w:w1", "l:l1", "w:w2", "l:l2", "w:w3", "l:l3" }, Names(order.Order));
        }

        [Test]
        public void Level_order_walks_round_robin_and_wraps()
        {
            var order = new PvpLevelOrder(W, L);
            var picked = new List<string>();
            for (var i = 0; i < 8; i++) picked.Add(order.Next(_ => false).ToString());
            CollectionAssert.AreEqual(new[] { "w:w1", "l:l1", "w:w2", "l:l2", "w:w3", "l:l3", "w:w1", "l:l1" }, picked);
        }

        [Test]
        public void Level_order_skips_maxed_and_ends_when_all_are_maxed()
        {
            var order = new PvpLevelOrder(W, L);
            var maxed = new HashSet<string> { "w:w1", "l:l1", "l:l2" };
            Assert.AreEqual("w:w2", order.Next(p => maxed.Contains(p.ToString())).ToString());
            Assert.AreEqual("w:w3", order.Next(p => maxed.Contains(p.ToString())).ToString());
            Assert.AreEqual("l:l3", order.Next(p => maxed.Contains(p.ToString())).ToString());
            Assert.AreEqual("w:w2", order.Next(p => maxed.Contains(p.ToString())).ToString());
            Assert.IsNull(order.Next(_ => true));
        }

        [Test]
        public void Level_order_handles_uneven_drafts()
        {
            var order = new PvpLevelOrder(new[] { "w1", "w2" }, new[] { "l1" });
            CollectionAssert.AreEqual(new[] { "w:w1", "l:l1", "w:w2" }, Names(order.Order));
        }

        [Test]
        public void Incoming_damage_is_scaled_by_the_build_and_capped()
        {
            Assert.AreEqual(10f * PvpRules.DamageScale, PvpRules.IncomingHull(10f, 1f, 1000f, false), 1e-4f);
            Assert.AreEqual(10f * PvpRules.DamageScale * 0.5f, PvpRules.IncomingHull(10f, 0.5f, 1000f, false), 1e-4f);
            // No one-shots: a huge hit takes at most the cap.
            Assert.AreEqual(100f * PvpRules.MaxHitFraction, PvpRules.IncomingHull(500f, 1f, 100f, false), 1e-4f);
            Assert.AreEqual(0f, PvpRules.IncomingHull(50f, 1f, 100f, true));
            Assert.AreEqual(0f, PvpRules.IncomingHull(-5f, 1f, 100f, false));
        }

        [Test]
        public void Invulnerability_lasts_its_window()
        {
            Assert.IsTrue(PvpRules.Invulnerable(10f + PvpRules.InvulnerableSeconds * 0.5f, 10f));
            Assert.IsFalse(PvpRules.Invulnerable(10f + PvpRules.InvulnerableSeconds + 0.01f, 10f));
            Assert.IsFalse(PvpRules.Invulnerable(0f, -10f)); // no hit yet
        }

        [Test]
        public void Hit_window_pools_hits_inside_it_and_lands_them_capped_when_it_ends()
        {
            var w = new PvpHitWindow();
            var maxHull = 1000f;
            Assert.AreEqual(10f * PvpRules.DamageScale, w.Hit(0f, 10f, 1f, maxHull), 1e-4f); // lands, opens the window
            Assert.IsTrue(w.Invulnerable(0.1f));
            Assert.AreEqual(0f, w.Hit(0.1f, 10f, 1f, maxHull));
            Assert.AreEqual(0f, w.Hit(0.2f, 20f, 1f, maxHull));
            Assert.AreEqual(0f, w.Tick(0.3f, maxHull)); // still inside
            Assert.AreEqual(30f * PvpRules.DamageScale, w.Tick(PvpRules.InvulnerableSeconds + 0.01f, maxHull), 1e-4f);
            Assert.IsTrue(w.Invulnerable(PvpRules.InvulnerableSeconds + 0.02f)); // the pooled hit opened a new window
            // A huge pool still lands as one capped hit.
            var big = new PvpHitWindow();
            big.Hit(0f, 1f, 1f, 100f);
            for (var i = 0; i < 10; i++) big.Hit(0.01f * i, 500f, 1f, 100f);
            Assert.AreEqual(100f * PvpRules.MaxHitFraction, big.Tick(1f, 100f), 1e-4f);
            Assert.AreEqual(0f, big.Pending);
        }

        [Test]
        public void Hits_are_clamped_to_what_the_server_takes()
        {
            Assert.AreEqual(500f, PvpRules.ClampHit(9999f));
            Assert.AreEqual(0f, PvpRules.ClampHit(-3f));
            Assert.AreEqual(12.3f, PvpRules.ClampHit(12.34f), 1e-4f);
            // V5 M8z (6 Oct, audit): the server's announced limit (2500), or 500 from an older server
            Assert.AreEqual(2500f, PvpRules.ClampHit(9999f, PvpRules.HitLimit(2500f)));
            Assert.AreEqual(500f, PvpRules.HitLimit(0f));
        }

        [Test]
        public void Hits_are_tagged_with_a_drafted_weapon_or_weapon()
        {
            // V5 M8z (6 Oct, audit): the server refuses any other id
            var draft = new[] { "deck_cannons", "powder_kegs" };
            string Base(string id) => id == "devils_wake" ? "powder_kegs" : id == "stormshot_cannon" ? "deck_cannons" : null;
            Assert.AreEqual("powder_kegs", PvpRules.HitTag("powder_kegs", draft, Base));
            Assert.AreEqual("powder_kegs", PvpRules.HitTag("devils_wake", draft, Base)); // an evolution: its base weapon
            Assert.AreEqual("weapon", PvpRules.HitTag("leviathan_heart", draft, Base)); // an item's pulse
            Assert.AreEqual("weapon", PvpRules.HitTag("mortar", draft, Base)); // not drafted
            Assert.AreEqual("weapon", PvpRules.HitTag(null, draft, Base)); // a burn tick
            Assert.AreEqual("mortar", PvpRules.HitTag("mortar", new string[0], Base)); // no draft on record: as is
        }

        [Test]
        public void Storm_shrinks_linearly_between_its_times()
        {
            var storm = new StormInfo { shrinkStartMs = 1000, shrinkEndMs = 3000, startRadius = 120, endRadius = 20, damagePerSecond = 8, damageGrowthPerSecond = 2 };
            Assert.AreEqual(120f, PvpRules.StormRadius(storm, 0));
            Assert.AreEqual(70f, PvpRules.StormRadius(storm, 2000), 1e-3f);
            Assert.AreEqual(20f, PvpRules.StormRadius(storm, 5000));
            Assert.AreEqual(8f, PvpRules.StormDps(storm, 0f));
            Assert.AreEqual(14f, PvpRules.StormDps(storm, 3f));
        }

        [Test]
        public void Clock_offset_uses_the_round_trip_midpoint()
        {
            Assert.AreEqual(500, PvpRules.ClockOffset(1000, 1200, 1600));
            Assert.AreEqual("5:00", PvpRules.Clock(300000));
            Assert.AreEqual("0:01", PvpRules.Clock(1));
            Assert.AreEqual("0:00", PvpRules.Clock(-5));
        }

        [Test]
        public void Yaw_matches_the_server_convention()
        {
            Assert.AreEqual(0f, PvpRules.YawOf(Vector3.forward), 1e-3f);
            Assert.AreEqual(90f, PvpRules.YawOf(Vector3.right), 1e-3f);
            Assert.AreEqual(270f, PvpRules.YawOf(Vector3.left), 1e-3f);
            var f = PvpRules.Forward(90f);
            Assert.AreEqual(1f, f.x, 1e-4f);
            Assert.AreEqual(0f, f.z, 1e-4f);
        }

        [Test]
        public void Interpolator_draws_between_two_states()
        {
            var interp = new PvpInterpolator();
            interp.Add(new PvpSnapshot { Time = 1f, Position = Vector3.zero, Yaw = 0f });
            interp.Add(new PvpSnapshot { Time = 2f, Position = new Vector3(10f, 0f, 0f), Yaw = 90f });
            Assert.IsTrue(interp.Sample(1.5f + PvpRules.InterpolationDelaySeconds, out var p, out var yaw));
            Assert.AreEqual(5f, p.x, 1e-3f);
            Assert.AreEqual(45f, yaw, 1e-3f);
        }

        const string MatchEnd = "{\"t\":\"match_end\",\"matchId\":\"m1\",\"winnerPlayerId\":\"a\",\"reason\":\"time\",\"rated\":true,\"durationSeconds\":300," +
            "\"stats\":{\"a\":{\"damageDealt\":900.5,\"damageTaken\":400,\"level\":12,\"barrelsTaken\":7,\"finalHp\":55,\"maxHp\":130,\"bot\":false}," +
            "\"b\":{\"damageDealt\":400,\"damageTaken\":900.5,\"level\":9,\"barrelsTaken\":3,\"finalHp\":10,\"maxHp\":120,\"bot\":false}}," +
            "\"ratings\":{\"a\":{\"before\":1000,\"after\":1016,\"change\":16},\"b\":{\"before\":1000,\"after\":984,\"change\":-16}},\"rematchUntil\":123}";

        [Test]
        public void Match_end_reads_both_players()
        {
            Assert.AreEqual("match_end", PvpJson.TypeOf(MatchEnd));
            var end = JsonUtility.FromJson<MatchEndMsg>(MatchEnd);
            Assert.AreEqual("a", end.winnerPlayerId);
            Assert.AreEqual(300, end.durationSeconds);
            var a = PvpJson.Entry<EndStats>(MatchEnd, "stats", "a");
            var b = PvpJson.Entry<EndStats>(MatchEnd, "stats", "b");
            Assert.AreEqual(900.5f, a.damageDealt, 1e-3f);
            Assert.AreEqual(7, a.barrelsTaken);
            Assert.AreEqual(9, b.level);
            Assert.AreEqual(-16, PvpJson.Entry<RatingChange>(MatchEnd, "ratings", "b").change);
            Assert.IsNull(PvpJson.Entry<EndStats>(MatchEnd, "stats", "nobody"));
        }

        [Test]
        public void Json_scanner_ignores_keys_inside_strings_and_deeper_objects()
        {
            const string json = "{\"note\":\"\\\"stats\\\":{\",\"inner\":{\"stats\":{\"x\":1}},\"stats\":{\"p\":{\"level\":3}}}";
            Assert.AreEqual("{\"p\":{\"level\":3}}", PvpJson.ObjectAt(json, "stats"));
            Assert.AreEqual(3, PvpJson.Entry<EndStats>(json, "stats", "p").level);
            Assert.IsNull(PvpJson.TypeOf("ping"));
            Assert.IsNull(PvpJson.TypeOf(null));
        }

        [Test]
        public void Lobby_state_reads_seats_and_match()
        {
            const string json = "{\"t\":\"lobby\",\"lobbyId\":\"L\",\"code\":\"K7PQ2M\",\"public\":true,\"status\":\"starting\",\"hostPlayerId\":\"a\"," +
                "\"seats\":[{\"seat\":\"host\",\"playerId\":\"a\",\"name\":\"Anne\",\"tag\":\"#1\",\"rating\":1000,\"status\":\"ready\",\"ready\":true}," +
                "{\"seat\":\"guest\",\"playerId\":\"00000000-0000-0000-0000-0000000000b0\",\"name\":\"Practice Bot\",\"bot\":true,\"status\":\"ready\"}]," +
                "\"match\":{\"matchId\":\"m\",\"seed\":42,\"startsAt\":10,\"endsAt\":20,\"arena\":{\"centerX\":0,\"centerZ\":0,\"radius\":120},\"botSeat\":1}}";
            var state = JsonUtility.FromJson<LobbyState>(json);
            Assert.IsTrue(state.@public);
            Assert.IsTrue(state.HasMatch);
            Assert.AreEqual(42, state.match.seed);
            Assert.AreEqual("Anne#1", state.Host.Display);
            Assert.IsTrue(state.Guest.bot);
            var noMatch = JsonUtility.FromJson<LobbyState>("{\"lobbyId\":\"L\",\"match\":null}");
            Assert.IsFalse(noMatch.HasMatch);
        }

        [Test]
        public void Ws_base_follows_the_http_scheme()
        {
            Assert.AreEqual("wss://api.roguepirates.fun", LobbySockets.WsBase("https://api.roguepirates.fun"));
            Assert.AreEqual("ws://localhost:8080", LobbySockets.WsBase("http://localhost:8080"));
            Assert.IsNull(LobbySockets.WsBase(null));
        }

        static List<string> Names(IReadOnlyList<PvpPick> picks)
        {
            var names = new List<string>();
            foreach (var p in picks) names.Add(p.ToString());
            return names;
        }
    }
}
