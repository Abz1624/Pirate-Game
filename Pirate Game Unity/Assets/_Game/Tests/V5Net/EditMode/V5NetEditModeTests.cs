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
            var s = new ShipSnapshot { serverTick = 900, ackTick = 870, motion = m, hull = 75.5f, maxHull = 100f, runTime = 61f, level = 7, kills = 300, gold = 12345, flags = 5, inputMargin = -7,
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
            Assert.AreEqual(-7, back.inputMargin, "M9b playtest fixes: the input margin rides along (negative: the client is late)");
            Assert.LessOrEqual(w.Length, 149, "the ship snapshot stays small (146 bytes with the boost and the input margin)");
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

        // ---- M9b playtest fixes (9 Oct) ------------------------------------------------------------------------------------

        static void EnemyBatch(NetEnemyMirror mirror, uint tick, bool full, double now, params (int id, float x, float z)[] enemies)
        {
            var h = new EnemyBatchHeader { serverTick = tick, batchCount = 1, full = full, count = (ushort)enemies.Length };
            var w = new NetWriter();
            NetCodec.WriteBatchHeader(w, h);
            foreach (var (id, x, z) in enemies) NetCodec.WriteEnemy(w, h, id, 1, new Vector3(x, 0f, z), new Vector3(1f, 0f, 0f), 1f, 0);
            mirror.Apply(new NetReader(w.ToArray()), 0f, now);
        }

        /// <summary>
        /// M9b playtest fixes (B, C): every enemy is drawn at one render time behind the newest snapshot, between the two
        /// snapshots around it (a far-band enemy sent every 3rd snapshot moves evenly, not in a burst then a stop); a late
        /// snapshot is bridged on the last velocity for at most 6 ticks; a move no enemy can sail between two snapshots (the
        /// leash) is a jump: held, then drawn there at once, never slid across the screen.
        /// </summary>
        [Test]
        public void Mirror_DrawsEnemiesAtOneRenderTime_JumpsAreNotSlid()
        {
            var mirror = new NetEnemyMirror();
            const double t0 = 100.0;
            const float dt = NetProtocol.TickSeconds;
            // Unit 1 sails +x at 6 m/s (0.4 m a snapshot); unit 2 is in the far band (every 3rd snapshot, 1.2 m apart).
            for (uint k = 0; k < 6; k++)
            {
                var tick = 10 + 2 * k;
                if (k % 3 == 0) EnemyBatch(mirror, tick, true, t0 + tick * dt, (1, 0.4f * k, 0f), (2, 100f + 0.4f * k, 0f));
                else EnemyBatch(mirror, tick, false, t0 + tick * dt, (1, 0.4f * k, 0f));
            }
            Assert.AreEqual(2, mirror.Count);
            Assert.AreEqual(NetEnemyMirror.MinDelaySeconds, mirror.DelaySeconds, 1e-4f, "on time: drawn two snapshot intervals behind");
            var renderAt = mirror.RenderTick(t0 + 20 * dt);
            Assert.AreEqual(20f - NetEnemyMirror.MinDelaySeconds / dt, renderAt, 1e-3f);
            Assert.IsTrue(mirror.TryGet(1, out var one));
            Assert.IsTrue(mirror.TryGet(2, out var two));
            // At render tick 17 (between unit 1's snapshots at 16 and 18; just past unit 2's newest, at 16): both at the same time.
            one.track.Sample(17f, out var p1, out var v1, out _);
            two.track.Sample(17f, out var p2, out var v2, out _);
            Assert.AreEqual(0.4f * 3.5f, p1.x, 1e-3f, "between its snapshots");
            Assert.AreEqual(100f + 0.4f * 3.5f, p2.x, 1e-3f, "the far-band enemy too, by the ticks between its own snapshots");
            Assert.AreEqual(6f, v1.x, 0.01f);
            Assert.AreEqual(6f, v2.x, 0.01f, "at the same speed (it covers three intervals' move over three intervals)");
            // Late: past the newest snapshot it goes on at its velocity for at most 6 ticks.
            one.track.Sample(22f, out var late, out _, out _);
            Assert.AreEqual(0.4f * 5 + 6f * 2 * dt, late.x, 1e-3f, "two ticks past its newest");
            one.track.Sample(60f, out var capped, out _, out _);
            Assert.AreEqual(0.4f * 5 + 6f * NetEnemyMirror.MaxExtrapolationTicks * dt, capped.x, 1e-3f, "capped");
            // Before its first snapshot it waits there.
            one.track.Sample(1f, out var first, out _, out _);
            Assert.AreEqual(0f, first.x, 1e-3f);

            // The leash moves unit 1 25 m in one snapshot: a jump.
            EnemyBatch(mirror, 22, false, t0 + 22 * dt, (1, 2f + 25f, 0f));
            Assert.AreEqual(1, mirror.Jumps);
            Assert.IsTrue(mirror.TryGet(1, out one));
            one.track.Sample(21.5f, out var before, out _, out _);
            Assert.AreEqual(2f, before.x, 1e-3f, "held where it was until the jump's tick (no slide toward it)");
            one.track.Sample(22f, out var after, out var vAfter, out _);
            Assert.AreEqual(27f, after.x, 1e-3f, "then drawn there at once");
            Assert.AreEqual(Vector3.zero, vAfter, "and not carried on at the jump's speed");
            // A long gap at a normal speed is not a jump (an enemy back in a far-band snapshot, a resume).
            EnemyBatch(mirror, 52, true, t0 + 52 * dt, (1, 27f + 5f, 0f));
            Assert.AreEqual(1, mirror.Jumps, "5 m in a second is sailing");
        }

        /// <summary>M9b playtest fixes (C): a near-only snapshot drops only enemies well inside the near band; one just past
        /// its edge waits for the next full snapshot (no pop out and back in at the band's edge).</summary>
        [Test]
        public void Mirror_KeepsEnemiesAtTheNearBandsEdgeUntilAFullSnapshot()
        {
            var mirror = new NetEnemyMirror();
            var edge = NetProtocol.NearRadius - 2f;
            EnemyBatch(mirror, 2, true, 1.0, (1, 10f, 0f), (2, edge, 0f), (3, 20f, 0f));
            EnemyBatch(mirror, 4, false, 1.07, (1, 10.2f, 0f));
            Assert.IsTrue(mirror.TryGet(2, out _), "an enemy at the band's edge is kept until the next full snapshot");
            Assert.IsFalse(mirror.TryGet(3, out _), "one well inside it that is not listed is gone");
            EnemyBatch(mirror, 6, true, 1.13, (1, 10.4f, 0f));
            Assert.IsFalse(mirror.TryGet(2, out _), "the full snapshot drops it");
        }

        static void EnemyBatchAt(NetEnemyMirror mirror, uint tick, double now, bool steady, float x)
        {
            var h = new EnemyBatchHeader { serverTick = tick, batchCount = 1, full = true, count = 1 };
            var w = new NetWriter();
            NetCodec.WriteBatchHeader(w, h);
            NetCodec.WriteEnemy(w, h, 1, 1, new Vector3(x, 0f, 0f), new Vector3(1f, 0f, 0f), 1f, 0);
            mirror.Apply(new NetReader(w.ToArray()), 0f, now, steady);
        }

        /// <summary>
        /// V5 M9b owner re-test fixes (10 Oct, issue 2): the server's world tick stands still while the run is paused for the
        /// cards (protocol 5), and the client's enemy clock stands still while its ship holds: the render time does not move
        /// through the hold and runs on from the same place after the pick, and the snapshots the paused server repeats are
        /// not counted twice. Enemy stutter fix (10 Oct): until the first snapshot after the pick (a 270 ms round trip later)
        /// it runs on slowed (0.6 to 0.25 of real time) within the snapshots it has, never more than a tick past the newest
        /// one, and that snapshot sets the clock; the enemy moves every frame, never faster than it sails.
        /// </summary>
        [Test]
        public void Mirror_EnemyClockHoldsWithTheShip_AndRunsOnFromThereAfterThePick()
        {
            var mirror = new NetEnemyMirror();
            const float dt = NetProtocol.TickSeconds;
            const double t0 = 50.0;
            // Unit 1 sails +x at 6 m/s; world ticks 10..40 on time.
            for (uint tick = 10; tick <= 40; tick += 2) EnemyBatchAt(mirror, tick, t0 + tick * dt, true, 0.2f * (tick - 10));
            var now = t0 + 40 * dt + 0.01;
            var before = mirror.RenderTick(now);
            // The cards: the client's ship holds for 3 s; the paused server repeats world tick 40 meanwhile.
            var held = mirror.RenderTick(now + 0.02, true);
            Assert.AreEqual(before, held, 0.02f / dt + 1e-3f, "the hold stops the clock where it was");
            var completes = mirror.CompleteSnapshots;
            for (var k = 1; k <= 45; k++)
            {
                EnemyBatchAt(mirror, 40, now + k * 0.066, true, 6f);
                Assert.AreEqual(held, mirror.RenderTick(now + k * 0.066, true), 1e-4f, "still while held");
            }
            Assert.AreEqual(completes, mirror.CompleteSnapshots, "the paused server's repeats are not counted again");
            // The pick at now + 3 s: the clock runs on from the same place, slowed, no jump.
            var lastHeldAt = now + 45 * 0.066;
            var pick = now + 3.0;
            var last = mirror.RenderTick(pick, false);
            var heldFor = (float)((pick - lastHeldAt) / dt);
            Assert.Greater(last, held + 0.2f * heldFor, "runs on from where it stopped (by the time since the last held frame)");
            Assert.LessOrEqual(last, held + NetEnemyMirror.BridgeMaxRate * heldFor + 1e-3f, "slowed until the world's snapshots come");
            Assert.IsTrue(mirror.TryGet(1, out var e));
            e.track.Sample(last, out var lastPos, out _, out _);
            const double frame = 1.0 / 60.0;
            var maxStep = 0f;
            var pastNewest = 0f;
            for (var f = 1; f <= 240; f++)
            {
                var t = pick + f * frame;
                // The first snapshot after the pick arrives a 270 ms round trip later (the server resumed at the pick's arrival).
                var since = t - (pick + 0.27);
                if (since >= 0 && f % 4 == 0)
                {
                    var tick = (uint)(41 + (int)(since / dt));
                    EnemyBatchAt(mirror, tick, t, true, 0.2f * (tick - 10)); // sailing on as before the pause
                }
                var r = mirror.RenderTick(t, false);
                Assert.Greater(r, last, "the clock moves every frame after the pick");
                Assert.LessOrEqual(r - last, (float)(frame / dt) * 1.3f, "never jumps ahead");
                Assert.IsTrue(mirror.TryGet(1, out e));
                pastNewest = Mathf.Max(pastNewest, r - e.track.NewestTick);
                e.track.Sample(r, out var p, out _, out _);
                maxStep = Mathf.Max(maxStep, Mathf.Abs(p.x - lastPos.x));
                Assert.Greater(p.x - lastPos.x, 0.2f * 6f * (float)frame, $"the enemy moves on every frame after the pick (frame {f}), at a fifth of its speed at least");
                last = r;
                lastPos = p;
            }
            Assert.Less(maxStep, 6f * (float)frame * 1.35f, "at its own speed at most (no jump)");
            Assert.LessOrEqual(pastNewest, 1.5f, "never more than a tick and a half past its newest snapshot (not run on its velocity)");
            Assert.AreEqual(1, mirror.Reanchors, "the first snapshot after the pick set the clock");
            Assert.AreEqual(NetEnemyMirror.MinDelaySeconds, mirror.DelaySeconds, 0.02f, "the render delay is not widened by the pause");
        }

        /// <summary>
        /// V5 M9b enemy stutter fix (10 Oct; the owner: "the enemies seem to be stuttering now a bit" after the re-test
        /// fixes). A weaving enemy (8 m/s, its heading swinging 60 degrees either way every 2.5 s) over a 270 ms round trip
        /// with 15 ms of jitter, three level-up holds of 3 s, and the server's pause outlasting each hold by the input delay
        /// (the pick's way there, two ticks of input lead, the snapshot's way back). With the re-test fixes' clock the pause's
        /// extra length was taken for lateness: the delay hit its 350 ms cap, short of the 470 ms needed, and the clock crept
        /// back at 20 ms a second, so the enemies were drawn past their newest snapshot and put back, frame after frame, for
        /// the rest of the voyage. With the fix the first snapshot after the pick sets the clock: from 1.5 s after each pick
        /// the drawing is as smooth as before any hold (no frame past the newest snapshot, no speed jumps).
        /// </summary>
        [Test]
        public void Mirror_AfterAHold_TheClockIsSetAgain_NotWidened_NoStutter()
        {
            var results = new Dictionary<string, (int pastBase, int framesBase, int jerkBase, float jitterBase, int pastAfter, int framesAfter, int jerkAfter, float jitterAfter, float delay)>();
            foreach (var reTest in new[] { true, false })
            {
                var mirror = new NetEnemyMirror { ReTestClockForTest = reTest };
                const float dt = NetProtocol.TickSeconds;
                const double oneWay = 0.135, lead = 2 * dt, hold = 3.0, frame = 1.0 / 60.0;
                var rng = new System.Random(7);
                // The server: world time stands still from each level-up until the pick reaches it and its tick comes.
                var levelUps = new[] { 6.0, 21.0, 36.0 };
                bool Paused(double t) { foreach (var at in levelUps) if (t >= at && t < at + oneWay + hold + oneWay + lead) return true; return false; }
                bool Held(double t) { foreach (var at in levelUps) if (t >= at + oneWay && t < at + oneWay + hold) return true; return false; }
                var path = new List<Vector2> { Vector2.zero };
                Vector2 At(double worldTime)
                {
                    var k = (int)(worldTime / 0.005);
                    while (path.Count <= k + 1)
                    {
                        var h = 60f * Mathf.Deg2Rad * Mathf.Sin(2f * Mathf.PI * (path.Count * 0.005f) / 2.5f);
                        path.Add(path[path.Count - 1] + new Vector2(Mathf.Sin(h), Mathf.Cos(h)) * 8f * 0.005f);
                    }
                    return path[k];
                }
                var arrivals = new List<(double at, uint tick, Vector2 p)>();
                uint world = 0;
                var worldTime = 0.0;
                for (var s = 1; s * dt < 50.0; s++)
                {
                    var t = s * dt;
                    if (!Paused(t)) { world++; worldTime += dt; }
                    if (s % NetProtocol.SnapshotEveryTicks == 0 && rng.NextDouble() >= 0.01)
                        arrivals.Add((t + oneWay + rng.NextDouble() * 0.015, world, At(worldTime)));
                }
                arrivals.Sort((a, b) => a.at.CompareTo(b.at));
                int next = 0, pastBase = 0, framesBase = 0, jerkBase = 0, pastAfter = 0, framesAfter = 0, jerkAfter = 0;
                var jitterBase = new List<float>();
                var jitterAfter = new List<float>();
                Vector2? lastP = null;
                var lastSpeed = -1f;
                for (var f = 0; f * frame < 50.0; f++)
                {
                    var now = f * frame;
                    while (next < arrivals.Count && arrivals[next].at <= now)
                    {
                        var a = arrivals[next++];
                        var h = new EnemyBatchHeader { serverTick = a.tick, batchCount = 1, full = true, count = 1 };
                        var w = new NetWriter();
                        NetCodec.WriteBatchHeader(w, h);
                        NetCodec.WriteEnemy(w, h, 1, 1, new Vector3(a.p.x, 0f, a.p.y), Vector3.zero, 1f, 0);
                        mirror.Apply(new NetReader(w.ToArray()), 0f, now, true);
                    }
                    var held = Held(now);
                    var render = mirror.RenderTick(now, held);
                    if (!mirror.TryGet(1, out var e)) continue;
                    e.track.Sample(render, reTest ? NetEnemyMirror.ReTestExtrapolationTicks : NetEnemyMirror.MaxExtrapolationTicks, out var p3, out _, out _);
                    var p = new Vector2(p3.x, p3.z);
                    var sincePick = double.MaxValue;
                    foreach (var at in levelUps) if (now >= at + oneWay + hold) sincePick = Mathf.Min((float)sincePick, (float)(now - at - oneWay - hold));
                    var baseline = now > 2.0 && now < levelUps[0];
                    var after = sincePick >= 1.5 && sincePick < 12.0;
                    if (!held && lastP.HasValue)
                    {
                        var speed = (p - lastP.Value).magnitude / (float)frame;
                        if (lastSpeed >= 0f)
                        {
                            var jitter = Mathf.Abs(speed - lastSpeed);
                            if (baseline) { framesBase++; jitterBase.Add(jitter); if (jitter > NetSmoothness.JerkMps) jerkBase++; if (render > e.track.NewestTick) pastBase++; }
                            if (after) { framesAfter++; jitterAfter.Add(jitter); if (jitter > NetSmoothness.JerkMps) jerkAfter++; if (render > e.track.NewestTick) pastAfter++; }
                        }
                        lastSpeed = speed;
                    }
                    else if (held) lastSpeed = -1f;
                    lastP = p;
                }
                results[reTest ? "re-test clock" : "fixed clock"] = (pastBase, framesBase, jerkBase, NetSmoothness.Mean(jitterBase), pastAfter, framesAfter, jerkAfter, NetSmoothness.Mean(jitterAfter), mirror.DelaySeconds);
            }
            var report = string.Join("; ", results.Select(r => $"{r.Key}: before the first hold {r.Value.pastBase}/{r.Value.framesBase} frames past the newest snapshot, " +
                $"{r.Value.jerkBase} speed jumps, jitter {r.Value.jitterBase:0.000} m/s; 1.5-12 s after the picks {r.Value.pastAfter}/{r.Value.framesAfter} past, " +
                $"{r.Value.jerkAfter} speed jumps, jitter {r.Value.jitterAfter:0.000} m/s; delay at the end {r.Value.delay * 1000f:0} ms"));
            Debug.Log("[V5 Net] enemy clock after holds: " + report);
            var old = results["re-test clock"];
            var fixedClock = results["fixed clock"];
            Assert.AreEqual(0, old.pastBase, "before any hold both clocks draw within the snapshots: " + report);
            Assert.Greater(old.pastAfter, old.framesAfter / 5, "the re-test clock runs past the snapshots after the picks (the regression, caught): " + report);
            Assert.Greater(old.jitterAfter, 1.5f * old.jitterBase, "and its per-frame speed jitter rises (caught): " + report);
            Assert.AreEqual(0, fixedClock.pastAfter, "the fixed clock never runs past the snapshots after the picks: " + report);
            Assert.AreEqual(0, fixedClock.jerkAfter, "no speed jumps: " + report);
            Assert.Less(fixedClock.jitterAfter, 1.15f * fixedClock.jitterBase + 0.02f, "as smooth as before any hold: " + report);
            Assert.AreEqual(NetEnemyMirror.MinDelaySeconds, fixedClock.delay, 0.03f, "the render delay is back to the link's own: " + report);
        }

        /// <summary>
        /// V5 M9b enemy stutter fix (10 Oct): the server's world stands still at the voyage's start until the page's first
        /// inputs (and repeats its tick meanwhile). That stand-still is not lateness: the first new tick sets the clock, the
        /// render delay stays the link's own and the enemies are never drawn past their newest snapshot once they sail. (In the
        /// local Chrome run at a 270 ms round trip, without this, the delay sat at its 350 ms cap and the render time was past
        /// the newest snapshot on every frame for the first half minute.)
        /// </summary>
        [Test]
        public void Mirror_WorldStandingStillAtTheStart_IsNotLateness()
        {
            var mirror = new NetEnemyMirror();
            const float dt = NetProtocol.TickSeconds;
            const double oneWay = 0.135, frame = 1.0 / 60.0;
            // World tick 5 repeated for 0.8 s (the server waits for the first inputs), then it sails at 6 m/s.
            var arrivals = new List<(double at, uint tick, float x)>();
            for (var k = 0; k < 12; k++) arrivals.Add((1.0 + oneWay + k * 2 * dt, 5u, 0f));
            for (uint tick = 7; tick < 7 + 300; tick += 2) arrivals.Add((1.8 + oneWay + (tick - 5) * dt, tick, 0.2f * (tick - 5)));
            int next = 0, frames = 0, past = 0;
            for (var f = 0; f * frame < 10.0; f++)
            {
                var now = 1.0 + f * frame;
                while (next < arrivals.Count && arrivals[next].at <= now) { var a = arrivals[next++]; EnemyBatchAt(mirror, a.tick, now, true, a.x); }
                var render = mirror.RenderTick(now, false);
                if (now < 3.5) continue;
                frames++;
                if (render > mirror.LastCompleteTick) past++;
            }
            Assert.AreEqual(1, mirror.Reanchors, "the first new tick after the stand-still set the clock");
            Assert.AreEqual(0, past, $"never past the newest snapshot once sailing ({past} of {frames} frames)");
            Assert.AreEqual(NetEnemyMirror.MinDelaySeconds, mirror.DelaySeconds, 0.02f, "the render delay is the link's own");
        }

        /// <summary>V5 M9b owner re-test fixes: a hold that begins after a long frame (a page's first hidden frame comes up to a
        /// second after it was hidden) first runs the enemy clock up to that frame, then stands still: the enemies are not
        /// frozen where they were a second before (the server's world ran on until the away reached it), so coming back
        /// does not jump them forward.</summary>
        [Test]
        public void Mirror_HoldAfterALongFrame_RunsTheClockUpToItFirst()
        {
            var mirror = new NetEnemyMirror();
            const float dt = NetProtocol.TickSeconds;
            for (uint tick = 2; tick <= 60; tick += 2) EnemyBatchAt(mirror, tick, 10.0 + tick * dt, true, tick * 0.2f);
            var before = mirror.RenderTick(10.0 + 60 * dt);
            var held = mirror.RenderTick(10.0 + 60 * dt + 1.0, true); // the first hidden frame, a second later
            Assert.AreEqual(before + 1f / dt, held, 0.6f, "the second before the hold is run first");
            Assert.AreEqual(held, mirror.RenderTick(10.0 + 60 * dt + 3.0, true), 1e-4f, "then it stands still");
        }

        /// <summary>V5 M9b owner re-test fixes: batches handled in a slow frame (a hitch, a hidden page's frame) are late because
        /// the page was; they do not widen the render delay (the owner's 350 ms after a return).</summary>
        [Test]
        public void Mirror_ArrivalsInASlowFrame_DoNotWidenTheRenderDelay()
        {
            var mirror = new NetEnemyMirror();
            const float dt = NetProtocol.TickSeconds;
            for (uint tick = 2; tick <= 30; tick += 2) EnemyBatchAt(mirror, tick, 10.0 + tick * dt, true, tick * 0.2f);
            Assert.AreEqual(NetEnemyMirror.MinDelaySeconds, mirror.DelaySeconds, 1e-4f);
            // A one-second hitch: the next second's batches all handled in one late frame.
            for (uint tick = 32; tick <= 60; tick += 2) EnemyBatchAt(mirror, tick, 10.0 + 60 * dt + 0.05, false, tick * 0.2f);
            Assert.AreEqual(NetEnemyMirror.MinDelaySeconds, mirror.DelaySeconds, 1e-4f, "the page's own lateness is not the link's");
            // The same lateness on steady frames (the link) does widen it.
            for (uint tick = 62; tick <= 70; tick += 2) EnemyBatchAt(mirror, tick, 10.0 + tick * dt + 0.1, true, tick * 0.2f);
            Assert.Greater(mirror.DelaySeconds, NetEnemyMirror.MinDelaySeconds + 0.05f);
        }

        /// <summary>M9b playtest fixes (B): everything the top-down camera can show is in the every-snapshot band. The run
        /// scene's camera: 25 m above and 20 m behind the ship, looking at it, 50 degree vertical field of view; checked at a
        /// 2.4:1 window (wider than the 16:9 and 21:9 desktops), with the camera's follow lag and an enemy's size on top.
        /// The far band stays off screen; the quantized offsets (int16 x 2 cm) cover it.</summary>
        [Test]
        public void InterestBands_CoverTheCamerasView()
        {
            var cam = new Vector3(0f, 25f, -20f);
            var rotation = Quaternion.LookRotation(-cam, Vector3.up);
            var tan = Mathf.Tan(25f * Mathf.Deg2Rad);
            var reach = 0f;
            foreach (var aspect in new[] { 16f / 9f, 21f / 9f, 2.4f })
                for (var sx = -1; sx <= 1; sx += 2)
                    for (var sy = -1; sy <= 1; sy += 2)
                    {
                        var dir = rotation * new Vector3(sx * tan * aspect, sy * tan, 1f);
                        Assert.Less(dir.y, 0f, "the corner looks down at the sea");
                        var hit = cam + dir * (-cam.y / dir.y);
                        reach = Mathf.Max(reach, new Vector2(hit.x, hit.z).magnitude);
                    }
            const float followLag = 2f, enemySize = 5f;
            Assert.LessOrEqual(reach + followLag + enemySize, NetProtocol.NearRadius, $"the view reaches {reach:0.0} m from the ship");
            Assert.Greater(NetProtocol.FarRadius, NetProtocol.NearRadius);
            Assert.Less(NetProtocol.FarRadius * 1.5f, short.MaxValue * NetProtocol.PositionStep, "positions up to 1.5x the far band fit the int16 offsets");
            Assert.GreaterOrEqual(NetProtocol.NearRadius - NetProtocol.NearDropMargin, reach - 0.5f, "an on-screen enemy a near-only snapshot leaves out is gone at once (a death)");
        }

        [Test]
        public void Presence_RoundTrips()
        {
            foreach (var away in new[] { true, false })
            {
                var w = new NetWriter();
                NetCodec.WritePresence(w, away);
                Assert.IsTrue(NetCodec.ReadPresence(new NetReader(w.ToArray()), out var back));
                Assert.AreEqual(away, back);
            }
            Assert.IsFalse(NetCodec.ReadPresence(new NetReader(new byte[] { NetProtocol.Version }), out _), "a short message is malformed");
            Assert.AreEqual(5, NetProtocol.Version, "protocol 5 (10 Oct): enemy batches on the world tick");
        }

        /// <summary>M9b playtest fixes (E): while the player is away the card timeout and the hold wait stop; back, they go
        /// on with the time they had left.</summary>
        [Test]
        public void LevelUps_ClockPausesWhileAway()
        {
            var closed = new List<OfferClosedMessage>();
            var sent = new List<OfferMessage>();
            var levelUps = new NetLevelUps { NextInputTick = () => 10, Send = sent.Add, SendClosed = closed.Add, TimeoutSeconds = 0.05f };
            IOfferPresenter presenter = levelUps;
            Assert.IsTrue(presenter.TryShow(Cards(), _ => { }, () => { }, () => { }, 0));
            levelUps.PauseClock(true);
            var until = Time.realtimeSinceStartup + 0.15f;
            while (Time.realtimeSinceStartup < until) levelUps.Tick();
            Assert.IsTrue(levelUps.Pending, "no timeout while away");
            Assert.AreEqual(0, levelUps.TimedOut);
            Assert.Greater(levelUps.Message().timeoutSeconds, 0f, "the time left is kept");
            levelUps.PauseClock(false);
            Assert.IsTrue(levelUps.Pending, "back: the time left still runs");
            until = Time.realtimeSinceStartup + 0.15f;
            while (Time.realtimeSinceStartup < until && levelUps.Pending) levelUps.Tick();
            Assert.AreEqual(1, levelUps.TimedOut, "then it times out");
            Assert.AreEqual(NetProtocol.OfferResolution.TimedOut, closed[0].how);
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
        /// <summary>V5 M9b close (review): a decoded input stays in [-1, 1]: a forged steer or throttle of -128 (one past the
        /// quantizer's -127) reads as full lock, not -1.008.</summary>
        [Test]
        public void Input_DecodedSteerAndThrottle_StayWithinFullLock()
        {
            var forged = new NetInput { steer = sbyte.MinValue, throttle = sbyte.MinValue };
            Assert.AreEqual(-1f, forged.SteerThrottle.x);
            Assert.AreEqual(-1f, forged.SteerThrottle.y);
            var max = new NetInput { steer = sbyte.MaxValue, throttle = sbyte.MaxValue };
            Assert.AreEqual(1f, max.SteerThrottle.x);
            Assert.AreEqual(1f, max.SteerThrottle.y);
            for (var v = -1f; v <= 1f; v += 0.05f)
            {
                var st = NetInput.From(new Vector2(v, -v), false).SteerThrottle;
                Assert.AreEqual(v, st.x, 0.5f / 127f + 1e-5f, "the quantizer's round trip is unchanged");
                Assert.AreEqual(-v, st.y, 0.5f / 127f + 1e-5f);
            }
            // and through the wire: the server reads a forged -128 as -1
            var w = new NetWriter();
            NetCodec.WriteInputs(w, 7, new[] { forged }, 1);
            var into = new NetInput[4];
            Assert.IsTrue(NetCodec.ReadInputs(new NetReader(w.ToArray()), out _, into, out var count, out _));
            Assert.AreEqual(1, count);
            Assert.AreEqual(new Vector2(-1f, -1f), into[0].SteerThrottle);
        }

        /// <summary>
        /// V5 M9b close: the server's tick and snapshot statistics keep a rolling window (an idle Railway server had kept
        /// 1.72 million tick samples after 16 h): after 16 h of 30 Hz ticks the window holds at most
        /// <see cref="NetMetrics.ServerWindowSamples"/> (about 5 to 10 minutes), the newest ones, while the voyage's tick
        /// count and slowest tick cover every tick; Reset clears both.
        /// </summary>
        [Test]
        public void ServerMetrics_KeepARollingWindow_AndVoyageTotals()
        {
            var m = new NetMetrics("server");
            const int ticks = 16 * 3600 * 30; // 16 h at 30 Hz
            for (var i = 0; i < ticks; i++)
            {
                m.AddTick(i == 1000 ? 9f : 0.25f + (i % 7) * 0.01f);
                if (i % 2 == 0) m.AddSnapshot(300f, 20f);
            }
            Assert.AreEqual(ticks, m.TicksMeasured, "every tick counted");
            Assert.AreEqual(9f, m.TickMsMax, "the slowest tick of the voyage, though it left the window");
            Assert.LessOrEqual(m.TickMs.Count, NetMetrics.ServerWindowSamples);
            Assert.GreaterOrEqual(m.TickMs.Count, NetMetrics.ServerWindowSamples / 2, "at least the newest half window");
            Assert.LessOrEqual(m.SnapshotBytes.Count, NetMetrics.ServerWindowSamples);
            Assert.LessOrEqual(m.SnapshotEnemies.Count, NetMetrics.ServerWindowSamples);
            Assert.AreEqual(0.25f + ((ticks - 1) % 7) * 0.01f, m.TickMs[m.TickMs.Count - 1], 1e-6f, "the newest sample is kept");
            Assert.Less(NetMetrics.Max(m.TickMs), 1f, "the old spike left the window");
            Assert.AreEqual(0.28f, NetMetrics.Percentile(m.TickMs, 50f), 0.011f);
            StringAssert.Contains("\"ticksMeasured\": " + ticks, m.ToJson());
            m.Reset();
            Assert.AreEqual(0, m.TicksMeasured);
            Assert.AreEqual(0f, m.TickMsMax);
            Assert.AreEqual(0, m.TickMs.Count);
        }
    }
}
