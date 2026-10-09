using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Progression;
using RoguePirates.V4.Foundation.Player;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V5.Net.Tests
{
    /// <summary>V5 M9b (I9b.1): the join ticket, the endpoint switch, the codecs, the enemy mirror, the server preset and
    /// the release exclusion, without a network.</summary>
    [Category("V5")]
    public sealed class V5NetEditModeTests
    {
        static readonly byte[] Key = Encoding.UTF8.GetBytes("test-only-key-0123456789abcdef-0123456789");
        const long Now = 1_800_000_000;

        static JoinTicketVerifier Verifier(long now = Now) => new JoinTicketVerifier(Key, () => now);

        // ---- Join ticket ----------------------------------------------------------------------------------------------

        [Test]
        public void Ticket_ValidOnceThenReplayed()
        {
            var ticket = new JoinTicketIssuer(Key).Issue("player-7", "match-1", Now, 60);
            StringAssert.StartsWith("v1.", ticket);
            Assert.AreEqual(3, ticket.Split('.').Length);
            var verifier = Verifier();
            Assert.AreEqual(JoinTicketError.None, verifier.Verify(ticket, out var claims));
            Assert.AreEqual("player-7", claims.sub);
            Assert.AreEqual("match-1", claims.mid);
            Assert.AreEqual(JoinTicketError.Replayed, verifier.Verify(ticket, out _), "single use");
        }

        [Test]
        public void Ticket_TamperedOrWrongKey_BadSignature()
        {
            var ticket = new JoinTicketIssuer(Key).Issue("player-7", null, Now);
            var parts = ticket.Split('.');
            var body = Encoding.UTF8.GetString(JoinTicket.FromBase64Url(parts[1])).Replace("player-7", "player-8");
            var tampered = parts[0] + "." + JoinTicket.Base64Url(Encoding.UTF8.GetBytes(body)) + "." + parts[2];
            Assert.AreEqual(JoinTicketError.BadSignature, Verifier().Verify(tampered, out _));
            var other = new JoinTicketIssuer(Encoding.UTF8.GetBytes(new string('k', 40))).Issue("player-7", null, Now);
            Assert.AreEqual(JoinTicketError.BadSignature, Verifier().Verify(other, out _));
        }

        [Test]
        public void Ticket_TimeWindowAndMatch()
        {
            var issuer = new JoinTicketIssuer(Key);
            Assert.AreEqual(JoinTicketError.Expired, Verifier(Now + 66).Verify(issuer.Issue("p", null, Now, 60), out _), "exp + 5 s skew passed");
            Assert.AreEqual(JoinTicketError.None, Verifier(Now + 64).Verify(issuer.Issue("p", null, Now, 60), out _), "inside the skew");
            Assert.AreEqual(JoinTicketError.NotYetValid, Verifier(Now - 10).Verify(issuer.Issue("p", null, Now, 60), out _));
            Assert.AreEqual(JoinTicketError.LifetimeTooLong, Verifier().Verify(issuer.Issue("p", null, Now, 600), out _));
            var verifier = Verifier();
            verifier.ExpectedMatchId = "match-1";
            Assert.AreEqual(JoinTicketError.WrongMatch, verifier.Verify(issuer.Issue("p", "match-2", Now), out _));
            Assert.AreEqual(JoinTicketError.None, verifier.Verify(issuer.Issue("p", "match-1", Now), out _));
        }

        [Test]
        public void Ticket_MalformedAndNoSecret()
        {
            Assert.AreEqual(JoinTicketError.Malformed, Verifier().Verify("v1.abc", out _));
            Assert.AreEqual(JoinTicketError.WrongVersion, Verifier().Verify("v2.abc.def", out _));
            Assert.AreEqual(JoinTicketError.Malformed, Verifier().Verify("", out _));
            Assert.AreEqual(JoinTicketError.NoSecret, new JoinTicketVerifier(null).Verify("v1.a.b", out _));
            Assert.IsNull(JoinTicket.KeyFromSecret("too-short"), "secrets under 32 characters are refused");
            var payload = new JoinTicketPayload { tid = "t", sub = "p", iat = Now, exp = Now + 30, aud = "someone-else" };
            Assert.AreEqual(JoinTicketError.WrongAudience, Verifier().Verify(new JoinTicketIssuer(Key).Issue(payload), out _));
        }

        /// <summary>The contract the API (part 2) implements: this exact ticket, from this payload and key, verifies.</summary>
        [Test]
        public void Ticket_ContractVector()
        {
            const string json = "{\"v\":1,\"tid\":\"0f8e\",\"sub\":\"guest-42\",\"mid\":\"m-1\",\"mode\":\"voyage\",\"aud\":\"rp-match\",\"iat\":1800000000,\"exp\":1800000060}";
            var body = JoinTicket.Base64Url(Encoding.UTF8.GetBytes(json));
            var ticket = "v1." + body + "." + JoinTicket.Base64Url(JoinTicket.Sign(Key, "v1." + body));
            Assert.AreEqual(JoinTicketError.None, Verifier().Verify(ticket, out var claims), "a ticket written by another issuer (field order free)");
            Assert.AreEqual("guest-42", claims.sub);
            Debug.Log("[V5 Net] contract vector: key \"" + Encoding.UTF8.GetString(Key) + "\" ticket " + ticket);
        }

        // ---- Endpoint -----------------------------------------------------------------------------------------------

        [Test]
        public void Endpoint_LocalWsAndRailwayWss()
        {
            var local = NetEndpoint.Parse("ws://127.0.0.1:7777/");
            Assert.IsFalse(local.Secure);
            Assert.AreEqual(7777, local.Port);
            var railway = NetEndpoint.Parse("wss://match-test.up.railway.app/");
            Assert.IsTrue(railway.Secure);
            Assert.AreEqual(443, railway.Port, "wss defaults to 443 (Railway's edge)");
            Assert.AreEqual("match-test.up.railway.app", railway.Host);
            Assert.IsFalse(NetEndpoint.TryParse("http://example.com", out _, out var error));
            StringAssert.Contains("ws://", error);

            var go = new GameObject("transport-test");
            try
            {
                var t = go.AddComponent<UnityTransport>();
                railway.ApplyClient(t);
                Assert.IsTrue(t.UseWebSockets);
                Assert.IsTrue(t.UseEncryption, "wss: TLS to Railway's edge");
                Assert.AreEqual("match-test.up.railway.app", t.ConnectionData.Address);
                Assert.AreEqual(443, t.ConnectionData.Port);
                local.ApplyClient(t);
                Assert.IsTrue(t.UseWebSockets);
                Assert.IsFalse(t.UseEncryption, "ws: plain locally");
                NetEndpoint.ApplyServer(t, 8080);
                Assert.IsTrue(t.UseWebSockets);
                Assert.IsFalse(t.UseEncryption, "the server listens on plain WS behind Railway's TLS");
                Assert.AreEqual("0.0.0.0", t.ConnectionData.ServerListenAddress);
                Assert.AreEqual(8080, t.ConnectionData.Port);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ---- Codecs -------------------------------------------------------------------------------------------------

        [Test]
        public void Inputs_RoundTripAndExtraBytesAreNotRead()
        {
            var inputs = new[] { NetInput.From(new Vector2(0.5f, 1f), true), NetInput.From(new Vector2(-1f, -0.25f), false) };
            var w = new NetWriter();
            NetCodec.WriteInputs(w, 120, inputs, 2);
            Assert.AreEqual(6 + 2 * 3, w.Length, "12 bytes for two inputs");
            w.F32(99999f);
            var read = new NetInput[8];
            Assert.IsTrue(NetCodec.ReadInputs(new NetReader(w.ToArray()), out var newest, read, out var count, out var ignored));
            Assert.AreEqual(120u, newest);
            Assert.AreEqual(2, count);
            Assert.AreEqual(inputs[0], read[0]);
            Assert.AreEqual(inputs[1], read[1]);
            Assert.IsTrue(read[0].Boost);
            Assert.AreEqual(4, ignored);
            Assert.AreEqual(1f, read[0].SteerThrottle.y, 1e-6f);
        }

        [Test]
        public void Ship_RoundTripsExactly()
        {
            var m = new PlayerShipController.MotionState
            {
                position = new Vector3(12.345f, 0.5f, -98.7654f),
                rotation = Quaternion.Euler(0f, 37.123f, 0f),
                velocity = new Vector3(3.3f, 0f, -4.4f),
                knockback = new Vector3(0.1f, 0f, 0.2f),
                yawRate = 51.5f, steerCommit = 0.4f, lastSteer = -1f, slowMultiplier = 1f, moveEffectMultiplier = 1.2f,
                moveSpeedMultiplier = 1.4f, turnRateMultiplier = 1f, runSpeedMultiplier = 1.625f, stepTime = 33.3f,
                nextIslandImpactAt = float.NegativeInfinity, nextIslandDamageAt = 12f, movementEnabled = true,
            };
            var s = new ShipSnapshot { serverTick = 900, ackTick = 870, motion = m, hull = 75.5f, maxHull = 100f, runTime = 61f, level = 7, kills = 300, gold = 12345, flags = 5,
                boost = new RoguePirates.V4.Foundation.PlayerSystems.ShipBoostController.NetState { boostTimer = 3.25f, cooldownTimer = 0f } };
            var w = new NetWriter();
            NetCodec.WriteShip(w, s);
            Assert.IsTrue(NetCodec.ReadShip(new NetReader(w.ToArray()), out var back));
            Assert.AreEqual(s.ackTick, back.ackTick);
            Assert.AreEqual(m.position, back.motion.position);
            Assert.AreEqual(m.rotation, back.motion.rotation);
            Assert.AreEqual(m.velocity, back.motion.velocity);
            Assert.AreEqual(m.stepTime, back.motion.stepTime);
            Assert.AreEqual(float.NegativeInfinity, back.motion.nextIslandImpactAt);
            Assert.AreEqual(s.hull, back.hull);
            Assert.AreEqual(s.gold, back.gold);
            Assert.IsTrue(back.motion.movementEnabled);
            Assert.AreEqual(3.25f, back.boost.boostTimer, "part 2b: the boost's timers ride with the motion");
            Assert.LessOrEqual(w.Length, 148, "the ship snapshot stays small (145 bytes with the boost)");
        }

        [Test]
        public void Enemies_QuantizedWithinOneCentimetrePerAxis()
        {
            var h = new EnemyBatchHeader { serverTick = 5, batchCount = 1, originX = 100.25f, originZ = -40f, count = 3 };
            var w = new NetWriter();
            NetCodec.WriteBatchHeader(w, h);
            var header = w.Length;
            var positions = new[] { new Vector3(159.987f, 0f, -40.003f), new Vector3(100.25f, 0f, -40f), new Vector3(-19.5f, 0f, 79.99f) };
            for (var i = 0; i < 3; i++) NetCodec.WriteEnemy(w, h, 70000 + i, 12, positions[i], new Vector3(1f, 0f, 1f), 0.5f, NetProtocol.EnemyElite);
            Assert.LessOrEqual((w.Length - header) / 3f, 11f, "at most 11 bytes an enemy (unit ids past 16383 take 3)");
            var r = new NetReader(w.ToArray());
            Assert.IsTrue(NetCodec.ReadBatchHeader(r, out var back));
            Assert.AreEqual(3, back.count);
            for (var i = 0; i < 3; i++)
            {
                var e = NetCodec.ReadEnemy(r, back, 0f);
                Assert.AreEqual(70000 + i, e.unitId);
                Assert.AreEqual(12, e.definition);
                Assert.LessOrEqual(Mathf.Abs(e.position.x - positions[i].x), 0.0101f, "x error at most 1 cm");
                Assert.LessOrEqual(Mathf.Abs(e.position.z - positions[i].z), 0.0101f, "z error at most 1 cm");
                Assert.AreEqual(45f, NetCodec.HeadingDegrees(e.heading), 1.5f);
                Assert.AreEqual(NetProtocol.EnemyElite, e.flags);
            }
        }

        [Test]
        public void Mirror_DropsNearEnemiesMissingFromASnapshot()
        {
            var mirror = new NetEnemyMirror();
            void Batch(uint tick, bool full, params (int id, float x)[] enemies)
            {
                var h = new EnemyBatchHeader { serverTick = tick, batchCount = 1, full = full, count = (ushort)enemies.Length };
                var w = new NetWriter();
                NetCodec.WriteBatchHeader(w, h);
                foreach (var (id, x) in enemies) NetCodec.WriteEnemy(w, h, id, 1, new Vector3(x, 0f, 0f), Vector3.zero, 1f, 0);
                mirror.Apply(new NetReader(w.ToArray()), 0f);
            }
            Batch(2, true, (1, 10f), (2, 100f), (3, 20f));
            Assert.AreEqual(3, mirror.Count);
            Batch(4, false, (1, 11f)); // a near-only snapshot: 3 (near) is gone, 2 (far) is kept until the next full one
            Assert.AreEqual(2, mirror.Count);
            Assert.IsTrue(mirror.TryGet(2, out _));
            Assert.IsTrue(mirror.TryGet(1, out var one));
            Assert.AreEqual(10f, one.previous.x, 0.02f);
            Assert.AreEqual(11f, one.position.x, 0.02f);
            Batch(6, true, (1, 12f));
            Assert.AreEqual(1, mirror.Count, "a full snapshot drops everything it does not list");
            Batch(5, true); // late
            Assert.AreEqual(1, mirror.Count, "a late snapshot changes nothing");
        }

        [Test]
        public void Ticket_RefusedJoinDoesNotBurnIt()
        {
            var ticket = new JoinTicketIssuer(Key).Issue("p", null, Now);
            var verifier = Verifier();
            Assert.AreEqual(JoinTicketError.None, verifier.Verify(ticket, out var claims, false));
            Assert.AreEqual(JoinTicketError.None, verifier.Verify(ticket, out _, false), "still unused");
            verifier.Consume(claims);
            Assert.AreEqual(JoinTicketError.Replayed, verifier.Verify(ticket, out _));
        }

        [Test]
        public void Mirror_TruncatedSnapshotRemovesNothing()
        {
            var mirror = new NetEnemyMirror();
            void Batch(uint tick, bool truncated, params int[] ids)
            {
                var h = new EnemyBatchHeader { serverTick = tick, batchCount = 1, full = true, truncated = truncated, count = (ushort)ids.Length };
                var w = new NetWriter();
                NetCodec.WriteBatchHeader(w, h);
                foreach (var id in ids) NetCodec.WriteEnemy(w, h, id, 1, new Vector3(5f, 0f, 0f), Vector3.zero, 1f, 0);
                mirror.Apply(new NetReader(w.ToArray()), 0f);
            }
            Batch(2, false, 1, 2, 3);
            Batch(4, true, 1);
            Assert.AreEqual(3, mirror.Count, "a capped snapshot's absences mean nothing");
            Batch(6, false, 1);
            Assert.AreEqual(1, mirror.Count);
        }

        // ---- Level-up cards (part 2b) ----------------------------------------------------------------------------------

        static List<Offer> Cards() => new List<Offer>
        {
            new Offer { Kind = OfferKind.WeaponUpgrade, Id = "cannon", Rarity = 2, CurrentLevel = 2, NextLevel = 3, IsWeapon = true,
                Lines = new[] { new UpgradeLine(ConfigEnums.UpgradeAxis.Damage, 0.15f), new UpgradeLine(ConfigEnums.UpgradeAxis.Cooldown, -0.1f) } },
            new Offer { Kind = OfferKind.NewWeapon, Id = "harpoon", IsWeapon = true, IsNew = true, NextLevel = 1 },
            new Offer { Kind = OfferKind.WeaponEvolution, Id = "cannon", EvolutionId = "broadside", Rarity = 4 },
        };

        [Test]
        public void LevelUpMessages_RoundTrip()
        {
            var m = new OfferMessage { seq = 513, pauseFrom = 90001, refreshesRemaining = 1, pendingLevelUps = 2, timeoutSeconds = 29.5f };
            m.offers.AddRange(Cards());
            var w = new NetWriter();
            NetCodec.WriteOffer(w, m);
            Assert.IsTrue(NetCodec.ReadOffer(new NetReader(w.ToArray()), out var back));
            Assert.AreEqual(513, back.seq);
            Assert.AreEqual(90001u, back.pauseFrom);
            Assert.AreEqual(2, back.pendingLevelUps);
            Assert.AreEqual(3, back.offers.Count);
            for (var i = 0; i < 3; i++)
            {
                Assert.AreEqual(m.offers[i].Key, back.offers[i].Key);
                Assert.AreEqual(m.offers[i].Rarity, back.offers[i].Rarity);
                Assert.AreEqual(m.offers[i].IsNew, back.offers[i].IsNew);
                Assert.AreEqual(m.offers[i].EvolutionId, back.offers[i].EvolutionId, "a null evolution stays null");
                Assert.AreEqual(m.offers[i].Lines == null, back.offers[i].Lines == null);
            }
            Assert.AreEqual(ConfigEnums.UpgradeAxis.Cooldown, back.offers[0].Lines[1].Axis);
            Assert.AreEqual(-0.1f, back.offers[0].Lines[1].Value);

            w.Clear();
            NetCodec.WritePick(w, new PickMessage { seq = 513, action = NetProtocol.PickAction.Pick, index = 2, resumeTick = 90050 });
            Assert.IsTrue(NetCodec.ReadPick(new NetReader(w.ToArray()), out var pick));
            Assert.AreEqual(NetProtocol.PickAction.Pick, pick.action);
            Assert.AreEqual(2, pick.index);
            Assert.AreEqual(90050u, pick.resumeTick);
            w.Clear();
            NetCodec.WriteOfferClosed(w, new OfferClosedMessage { seq = 513, how = NetProtocol.OfferResolution.TimedOut, resumeTick = 7 });
            Assert.IsTrue(NetCodec.ReadOfferClosed(new NetReader(w.ToArray()), out var closed));
            Assert.AreEqual(NetProtocol.OfferResolution.TimedOut, closed.how);
            Assert.AreEqual(7u, closed.resumeTick);
            Assert.IsFalse(NetCodec.ReadPick(new NetReader(new byte[] { NetProtocol.Version, 1 }), out _), "a short pick is malformed");
        }

        /// <summary>The server side of the cards: the module's callbacks run only for a pick that matches the cards on the
        /// table; a refused pick changes nothing; an unanswered set takes the first card; the pause holds the ship from the
        /// next unapplied tick until the resume tick.</summary>
        [Test]
        public void LevelUps_CheckPicksAndTimeOut()
        {
            uint next = 100;
            var sent = new List<OfferMessage>();
            var closed = new List<OfferClosedMessage>();
            var catchUps = new List<(uint, uint)>();
            var levelUps = new NetLevelUps { NextInputTick = () => next, Send = sent.Add, SendClosed = closed.Add, CatchUp = (a, b) => catchUps.Add((a, b)) };
            var chosen = new List<string>();
            var skips = 0;
            IOfferPresenter presenter = levelUps;
            Assert.IsTrue(presenter.TryShow(Cards(), o => chosen.Add(o.Key), () => skips++, () => { }, 0), "the run pauses on the cards");
            Assert.AreEqual(1, sent.Count);
            Assert.AreEqual(100u, sent[0].pauseFrom, "the pause starts at the next unapplied tick");
            Assert.IsTrue(levelUps.Holds(100) && levelUps.Holds(500), "the ship holds while the cards are up");
            Assert.IsFalse(levelUps.Holds(99));
            var seq = levelUps.Seq;

            StringAssert.Contains("stale", levelUps.Apply(new PickMessage { seq = (ushort)(seq + 1), action = NetProtocol.PickAction.Pick }));
            StringAssert.Contains("no card", levelUps.Apply(new PickMessage { seq = seq, action = NetProtocol.PickAction.Pick, index = 3 }));
            StringAssert.Contains("refresh", levelUps.Apply(new PickMessage { seq = seq, action = NetProtocol.PickAction.Refresh }));
            StringAssert.Contains("unknown", levelUps.Apply(new PickMessage { seq = seq, action = (NetProtocol.PickAction)9 }));
            Assert.AreEqual(4, levelUps.Rejected);
            CollectionAssert.IsEmpty(chosen, "a refused pick applies nothing");
            Assert.IsTrue(levelUps.Pending);

            next = 140;
            Assert.IsNull(levelUps.Apply(new PickMessage { seq = seq, action = NetProtocol.PickAction.Pick, index = 1, resumeTick = 120 }));
            CollectionAssert.AreEqual(new[] { "NewWeapon:harpoon" }, chosen, "the picked card goes to the module");
            // M9b review: a late pick (its packet resent) names a tick already held: the held ticks since then are stepped
            // now on the inputs the server has (at most LateTickWindow back), so the resume is exact on both sides.
            Assert.AreEqual(120u, closed[0].resumeTick, "a late pick keeps its resume tick");
            CollectionAssert.AreEqual(new[] { (120u, 140u) }, catchUps, "the held ticks since the resume tick are caught up");
            Assert.AreEqual(1, levelUps.LatePicksCaughtUp);
            Assert.IsTrue(levelUps.Holds(119));
            Assert.IsFalse(levelUps.Holds(120), "moving again from the resume tick");
            Assert.IsNotNull(levelUps.Apply(new PickMessage { seq = seq, action = NetProtocol.PickAction.Pick }), "the same set twice is refused");

            presenter.TryShow(Cards(), o => chosen.Add(o.Key), () => skips++, () => { }, 0);
            levelUps.ExpireForTest();
            levelUps.Tick();
            Assert.AreEqual(1, levelUps.TimedOut);
            Assert.AreEqual("WeaponUpgrade:cannon", chosen[1], "an unanswered set takes the first card");
            Assert.AreEqual(NetProtocol.OfferResolution.TimedOut, closed[1].how);
            Assert.IsFalse(levelUps.Pending);
        }

        /// <summary>V5 M9b review (D9b.1 level-up pull-back, fix 5): with a client there, the ship is not held until the client
        /// names its hold tick (the tick after the one it had reached when the cards arrived); the hold is clamped to at
        /// least the next unapplied tick and at most the newest input plus the lead; no hold in time holds from the next
        /// unapplied tick; a pick's resume tick is capped at twice the lead ahead; a refused pick re-sends the cards once a set.</summary>
        [Test]
        public void LevelUps_ClientNamesTheHold_ClampedAndBounded()
        {
            uint next = 100, newest = 104;
            var sent = new List<OfferMessage>();
            var closed = new List<OfferClosedMessage>();
            var levelUps = new NetLevelUps { NextInputTick = () => next, NewestInputTick = () => newest, ClientCanNameHold = () => true, Send = sent.Add, SendClosed = closed.Add };
            IOfferPresenter presenter = levelUps;
            Assert.IsTrue(presenter.TryShow(Cards(), _ => { }, () => { }, () => { }, 0));
            Assert.IsTrue(levelUps.AwaitingHold);
            Assert.AreEqual(0u, sent[0].pauseFrom, "the client names the hold tick");
            Assert.IsFalse(levelUps.Holds(100) || levelUps.Holds(103), "the ship keeps applying the client's inputs until then");
            Assert.IsNull(levelUps.ApplyHold(105));
            Assert.AreEqual(105u, levelUps.PauseFrom);
            Assert.AreEqual(1, sent.Count, "an accepted hold needs no answer");
            Assert.IsFalse(levelUps.Holds(104));
            Assert.IsTrue(levelUps.Holds(105) && levelUps.Holds(300));
            Assert.IsNotNull(levelUps.ApplyHold(110), "a second hold for the same set changes nothing");
            Assert.AreEqual(105u, levelUps.PauseFrom);

            Assert.IsTrue(levelUps.TakeResendAfterRefusal(), "a refused pick sends the cards again");
            Assert.IsFalse(levelUps.TakeResendAfterRefusal(), "once a set");

            next = 110;
            Assert.IsNull(levelUps.Apply(new PickMessage { seq = levelUps.Seq, action = NetProtocol.PickAction.Pick, index = 0, resumeTick = 5000 }));
            Assert.AreEqual(110u + 2 * NetProtocol.HoldLeadTicks, closed[0].resumeTick, "a far resume tick is capped at twice the lead");

            next = 130; newest = 131;
            presenter.TryShow(Cards(), _ => { }, () => { }, () => { }, 0);
            Assert.IsTrue(levelUps.AwaitingHold, "a new pause after the last resume: the client names it again");
            Assert.IsNull(levelUps.ApplyHold(100000));
            Assert.AreEqual(131u + NetProtocol.HoldLeadTicks, levelUps.PauseFrom, "a hold past the newest input plus the lead is clamped");
            Assert.AreEqual(1, levelUps.HoldsClamped);
            Assert.AreEqual(131u + NetProtocol.HoldLeadTicks, sent[sent.Count - 1].pauseFrom, "and the client is told the tick taken");
            Assert.IsTrue(levelUps.TakeResendAfterRefusal(), "a new set may re-send again");
            levelUps.Apply(new PickMessage { seq = levelUps.Seq, action = NetProtocol.PickAction.Skip, resumeTick = 140 });

            next = 160; newest = 165;
            presenter.TryShow(Cards(), _ => { }, () => { }, () => { }, 0);
            Assert.IsNull(levelUps.ApplyHold(150));
            Assert.AreEqual(160u, levelUps.PauseFrom, "a hold before the cards were dealt is moved up to that tick");
            levelUps.Apply(new PickMessage { seq = levelUps.Seq, action = NetProtocol.PickAction.Skip, resumeTick = 161 });

            next = 170;
            var rewound = new List<uint>();
            levelUps.Rewind = rewound.Add;
            presenter.TryShow(Cards(), _ => { }, () => { }, () => { }, 0);
            next = 175; // a late hold: the server applied 170-174 meanwhile
            Assert.IsNull(levelUps.ApplyHold(172));
            Assert.AreEqual(172u, levelUps.PauseFrom, "a late hold is kept");
            CollectionAssert.AreEqual(new[] { 172u }, rewound, "and the server's ship goes back to its state before that tick");
            Assert.AreEqual(1, levelUps.HoldsRewound);

            levelUps.Apply(new PickMessage { seq = levelUps.Seq, action = NetProtocol.PickAction.Skip, resumeTick = 176 });
            next = 200;
            levelUps.HoldWaitSeconds = 0f;
            var before = sent.Count;
            presenter.TryShow(Cards(), _ => { }, () => { }, () => { }, 0);
            levelUps.Tick();
            Assert.IsFalse(levelUps.AwaitingHold);
            Assert.AreEqual(1, levelUps.HoldsTimedOut);
            Assert.AreEqual(200u, levelUps.PauseFrom, "no hold named in time: the next unapplied tick");
            Assert.AreEqual(before + 2, sent.Count, "the cards, then the hold the server took");
            Assert.AreEqual(200u, sent[sent.Count - 1].pauseFrom);
        }

        // ---- Composition and release exclusion --------------------------------------------------------------------

        [Test]
        public void ServerPreset_SetsThe30HzTick()
        {
            var set = AssetDatabase.FindAssets("t:" + nameof(ConfigSet)).Select(g => AssetDatabase.LoadAssetAtPath<ConfigSet>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(s => s != null && s.setId == "saltwind-default");
            Assert.IsNotNull(set, "the default config set");
            using (var runtime = new ConfigSetRuntime(set))
            {
                var errors = new System.Collections.Generic.List<string>();
                Assert.IsTrue(PresetCodec.Apply(runtime, PresetCodec.FromJson(NetServerComposition.ThirtyHertzPreset()), 0f, errors), string.Join("; ", errors));
                Assert.AreEqual(NetProtocol.TickRate, runtime.Single<RunConfig>().tickRate);
            }
        }

        [Test]
        public void NetAssembly_IsKeptOutOfReleaseBuilds()
        {
            var asmdef = File.ReadAllText(Path.Combine(Application.dataPath, "_Game/V5/Net/RoguePirates.V5.Net.asmdef"));
            StringAssert.Contains("\"UNITY_EDITOR || V5_NET\"", asmdef, "compiled only in the Editor or with the V5_NET define");
            foreach (var scene in EditorBuildSettings.scenes)
                StringAssert.DoesNotContain("V5NetSpike", scene.path, "the spike scene is not in the build list");
            foreach (var guid in AssetDatabase.FindAssets("t:BuildProfile"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                StringAssert.DoesNotContain("V5_NET", File.ReadAllText(path), path + " (a build profile) does not define V5_NET");
            }
            foreach (var target in new[] { UnityEditor.Build.NamedBuildTarget.WebGL, UnityEditor.Build.NamedBuildTarget.Android, UnityEditor.Build.NamedBuildTarget.Standalone, UnityEditor.Build.NamedBuildTarget.Server })
                StringAssert.DoesNotContain("V5_NET", PlayerSettings.GetScriptingDefineSymbols(target), target.TargetName + " builds do not define V5_NET");
        }

        /// <summary>V5 M9b part 2a: the Linux match server build gives itself V5_NET (its build options only) and boots the
        /// spike scene, whose Server role runs the match server and whose Client role is the scripted test client.</summary>
        [Test]
        public void ServerBuild_OwnsTheDefineAndBootsTheSpikeScene()
        {
            Assert.AreEqual("V5_NET", Editor.V5NetServerBuild.Define);
            Assert.AreEqual(Editor.V5NetServerBuild.SpikeScenePath, Editor.V5NetServerBuild.Scenes[0], "the server boots the spike scene");
            CollectionAssert.Contains(Editor.V5NetServerBuild.Scenes, RoguePirates.V4.V4SceneNames.SaltwindRunPath, "and has the run scene to load");
            foreach (var scene in Editor.V5NetServerBuild.Scenes) FileAssert.Exists(scene);
            CollectionAssert.DoesNotContain(EditorBuildSettings.scenes.Select(s => s.path).ToArray(), Editor.V5NetServerBuild.SpikeScenePath,
                "the game's build list stays without the spike scene");
            StringAssert.EndsWith("Builds" + Path.DirectorySeparatorChar + "LinuxServer", Editor.V5NetServerBuild.OutputFolder);
        }

        /// <summary>V5 M9b part 2b: the WebGL client build gives itself V5_NET (its build options only, like the server's);
        /// the game's own builds (V4BuildSettings, the WebGL and Android profiles) never name the define; the server voyage
        /// entry lives in the net assembly and is off in the Editor unless switched on.</summary>
        [Test]
        public void WebGLClientBuild_OwnsTheDefineAndTheEntryStaysInTheNetAssembly()
        {
            Assert.AreEqual("V5_NET", Editor.V5NetWebGLBuild.Define);
            StringAssert.EndsWith("Builds" + Path.DirectorySeparatorChar + "WebGL-Desktop-Net", Editor.V5NetWebGLBuild.OutputFolder);
            foreach (var file in Directory.GetFiles(Path.Combine(Application.dataPath, "_Game/V4"), "*.cs", SearchOption.AllDirectories))
                StringAssert.DoesNotContain("\"V5_NET\"", File.ReadAllText(file), file + ": the game's code and build scripts never add V5_NET");
            Assert.AreEqual("RoguePirates.V5.Net", typeof(NetVoyageEntry).Assembly.GetName().Name, "the entry compiles only with the net assembly");
            Assert.AreEqual("RoguePirates.V5.Net", typeof(NetVoyageClient).Assembly.GetName().Name);
            var was = NetVoyageEntry.EditorEnabled;
            try
            {
                NetVoyageEntry.EditorEnabled = false;
                Assert.IsFalse(NetVoyageEntry.Enabled, "off in the Editor by default");
            }
            finally { NetVoyageEntry.EditorEnabled = was; }
        }
    }
}
