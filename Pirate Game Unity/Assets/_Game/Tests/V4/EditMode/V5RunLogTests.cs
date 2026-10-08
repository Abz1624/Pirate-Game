using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using RoguePirates.V4.Events;
using RoguePirates.V4.Online;
using RoguePirates.V4.Telemetry;
using UnityEngine;
using RunState = RoguePirates.V4.Foundation.Core.RunState;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M9a (I9a.1): the run log. The builder's minutes sum to the summary it is uploaded with, the size of a 15-20
    /// minute voyage, a two-hour voyage still fits, the recorder reads the run's events and counters, the upload payload
    /// carries the log as its "log" field (and the old shape without one), and the format matches the server's fixture
    /// (server/RoguePirates.Api.Tests/Fixtures/runlog-v1-sample.json, accepted by the API's RunLogTests).
    /// </summary>
    [Category("V5")]
    public sealed class V5RunLogTests
    {
        // ---- JsonUtility mirrors of the format (inputs, an array of arrays, are not read this way) ----
        [Serializable] sealed class LogMirror
        {
            public string format; public int v; public int seed; public string seedSource; public string runId; public string configHash;
            public string balanceVersion; public string buildId; public string end; public float seconds; public int minuteSeconds;
            public string[] weapons; public MinuteMirror[] minutes; public float inputEvery; public string[] inputCols;
        }

        [Serializable] sealed class MinuteMirror
        {
            public int m, spawns, kills, eliteKills, bossKills, gold, level, pickups, chests, pois, alive;
            public long xp;
            public float taken, dealt, healed, hull, maxHull;
            public int[] wk;
        }

        static RunLogHeader Header(int seed = 4242) => new RunLogHeader
        {
            Seed = seed, SeedSource = "local", RunId = "run-abc", ConfigHash = "cfg-1", BalanceVersion = "saltwind-v5", ContentHash = "content-1",
            StageProfile = "saltwind", BuildId = "0.5.0+test", Captain = "rook", StartingWeapon = "cannon",
        };

        /// <summary>
        /// A scripted voyage straight into the builder: <paramref name="seconds"/> of run time at 30 ticks a second, a kill
        /// rate rising with time, <paramref name="weapons"/> weapons sharing the kills, elites every 90 s, a boss at 14:00,
        /// chests, points of interest, damage and inputs. Deterministic (no random), so the same call gives the same log.
        /// </summary>
        internal static RunLogBuilder Scripted(float seconds, int weapons = 6, int seed = 4242)
        {
            var b = new RunLogBuilder();
            var c = new RunLogCounters { Level = 1, Hull = 100f, MaxHull = 100f };
            b.Begin(Header(seed), c);
            var ids = new[] { "cannon", "harpoon_gun", "powder_kegs", "chain_shot", "tar_pots", "ink_pots", "mortar", "maelstrom" };
            var weaponKills = new int[ids.Length];
            var weaponDamage = new float[ids.Length];
            const float dt = 1f / 30f;
            var ticks = Mathf.RoundToInt(seconds / dt);
            for (var i = 1; i <= ticks; i++)
            {
                var t = i * dt;
                if (i % 9 == 0) b.AddSpawn();
                if (i % (t < 300f ? 30 : 12) == 0)
                {
                    c.Kills++;
                    var w = (c.Kills * 7) % Math.Min(weapons, 1 + (int)(t / 120f));
                    weaponKills[w]++;
                    weaponDamage[w] += 37.5f;
                    c.Gold += c.Kills % 3 == 0 ? 2 : 0;
                    c.Xp += 3;
                    c.Pickups++;
                }
                if (i % 2700 == 0) b.AddKillKind(elite: true, boss: false); // an elite every 90 s
                if (Mathf.Abs(t - 840f) < dt / 2f) { b.BossSummoned = t; }
                if (Mathf.Abs(t - 900f) < dt / 2f) { b.AddKillKind(false, true); b.BossDefeated = t; }
                if (i % 1800 == 0) b.AddChest();
                if (i % 5400 == 0) b.AddPoi();
                if (i % 150 == 0) { b.AddDamageTaken(4.5f); c.Hull = Mathf.Max(10f, c.Hull - 4.5f); }
                if (i % 900 == 0) { b.AddHealed(10f); c.Hull = Mathf.Min(c.MaxHull, c.Hull + 10f); }
                c.Level = 1 + (int)Mathf.Sqrt(c.Xp / 6f);
                c.Alive = 10 + (i / 300) % 40;
                for (var w = 0; w < weapons && w < ids.Length; w++)
                    if (weaponKills[w] > 0 || w == 0) b.SetWeaponTotal(ids[w], weaponKills[w], weaponDamage[w]);
                b.SampleInput(t, 10f * Mathf.Sin(t * 0.05f), 10f * Mathf.Cos(t * 0.05f), t * 3f, Mathf.Sin(t), 1f, i % 600 < 30);
                b.Advance(t, c);
            }
            b.Close(ticks * dt, "sunk", c);
            Final = c;
            return b;
        }

        internal static RunLogCounters Final;

        static LogMirror Parse(string json) => JsonUtility.FromJson<LogMirror>(json);

        [Test]
        public void Minutes_SumToTheSummary_AndStayInsideTheirKills()
        {
            var b = Scripted(1050.2f);
            var log = Parse(b.ToJson());
            Assert.AreEqual("rp-runlog", log.format);
            Assert.AreEqual(1, log.v);
            Assert.AreEqual(4242, log.seed);
            Assert.AreEqual("cfg-1", log.configHash);
            Assert.AreEqual(60, log.minuteSeconds);
            Assert.AreEqual(18, log.minutes.Length, "17 full minutes and the partial 18th");
            Assert.AreEqual(Final.Kills, log.minutes.Sum(m => m.kills), "minute kills sum to the run's kills");
            Assert.AreEqual(Final.Kills, b.TotalKills());
            Assert.AreEqual(Final.Level, log.minutes.Last().level, "the last minute's level is the run's level");
            Assert.AreEqual(Final.Gold, log.minutes.Sum(m => m.gold));
            Assert.AreEqual(Final.Xp, log.minutes.Sum(m => m.xp));
            Assert.AreEqual(Final.Pickups, log.minutes.Sum(m => m.pickups));
            Assert.AreEqual(1050.2f, log.seconds, 0.05f);
            for (var i = 0; i < log.minutes.Length; i++)
            {
                var m = log.minutes[i];
                Assert.AreEqual(i, m.m);
                Assert.AreEqual(log.weapons.Length, m.wk.Length, "every minute has a count per weapon");
                Assert.LessOrEqual(m.wk.Sum(), m.kills, "weapon kills fit in the minute's kills");
                Assert.LessOrEqual(m.eliteKills + m.bossKills, m.kills);
                if (i > 0) Assert.GreaterOrEqual(m.level, log.minutes[i - 1].level);
            }
            Assert.AreEqual(1, log.minutes.Sum(m => m.bossKills), "the boss kill");
            Assert.AreEqual(11, log.minutes.Sum(m => m.eliteKills), "an elite every 90 s");
            Assert.AreEqual(Final.Kills, log.minutes.Sum(m => m.wk.Sum()), "every scripted kill had a weapon");
            Assert.AreEqual(17, log.minutes.Sum(m => m.chests), "a chest every 60 s");
            Assert.AreEqual(5, log.minutes.Sum(m => m.pois), "a point of interest every 180 s");
            Assert.Greater(log.minutes.Sum(m => m.dealt), 0f);
            Assert.AreEqual(840f, b.BossSummoned, 0.1f);
        }

        [Test]
        public void ExcessWeaponAndEliteKills_CarryIntoTheNextMinute()
        {
            var b = new RunLogBuilder();
            var c = new RunLogCounters { Level = 1 };
            b.Begin(Header(), c);
            // the ledger saw 3 kills before the minute's edge, the counter only 2 (a kill on the edge)
            b.SetWeaponTotal("cannon", 3, 30f);
            b.AddKillKind(true, false);
            b.AddKillKind(true, false);
            b.AddKillKind(true, false);
            c.Kills = 2;
            b.Advance(60f, c);
            c.Kills = 3;
            b.Close(70f, "quit", c);
            var log = Parse(b.ToJson());
            Assert.AreEqual(new[] { 2, 1 }, log.minutes.Select(m => m.kills).ToArray());
            Assert.AreEqual(new[] { 2, 1 }, log.minutes.Select(m => m.wk[0]).ToArray(), "the third weapon kill moved to minute 2");
            Assert.AreEqual(new[] { 2, 1 }, log.minutes.Select(m => m.eliteKills).ToArray());
        }

        [Test]
        public void EndingOnAMinuteEdge_GivesAMinuteCountTheApiAccepts()
        {
            foreach (var end in new[] { 0.5f, 59.96f, 60f, 119.97f, 120f, 600f, 7199.9f })
            {
                var b = new RunLogBuilder();
                var c = new RunLogCounters { Level = 1 };
                b.Begin(Header(), c);
                for (var t = 1f; t < end; t += 1f) b.Advance(t, c);
                b.Close(end, "quit", c);
                var log = Parse(b.ToJson());
                var seconds = log.seconds; // as written (one decimal)
                var least = Math.Max(1, (int)Math.Ceiling(seconds / 60.0));
                var most = (int)Math.Floor(seconds / 60.0) + 1;
                Assert.That(log.minutes.Length, Is.InRange(least, most), $"end {end}: {log.minutes.Length} minutes for {seconds} s");
            }
        }

        [Test]
        public void TypicalVoyage_IsWellUnder64KB()
        {
            var fifteen = Encoding.UTF8.GetByteCount(Scripted(15 * 60f, 6).ToJson());
            var twenty = Encoding.UTF8.GetByteCount(Scripted(20 * 60f, 8).ToJson());
            TestContext.WriteLine($"run log size: 15 min {fifteen} bytes, 20 min {twenty} bytes");
            Debug.Log($"[V5 M9a] run log size: 15 min {fifteen} bytes, 20 min {twenty} bytes");
            Assert.Less(twenty, 24 * 1024, "a 20-minute voyage stays small");
            Assert.Less(fifteen, twenty);
        }

        [Test]
        public void TwoHourVoyage_ThinsItsInputsToFit()
        {
            var b = Scripted(7190f, 8);
            var json = b.ToJson();
            var log = Parse(json);
            Assert.LessOrEqual(Encoding.UTF8.GetByteCount(json), RunLogBuilder.MaxBytes);
            Assert.Greater(log.inputEvery, RunLogBuilder.InputEvery, "the input samples were thinned");
            Assert.AreEqual(120, log.minutes.Length, "the minutes are never thinned");
            Assert.AreEqual(Final.Kills, log.minutes.Sum(m => m.kills));
        }

        [Test]
        public void SameScript_GivesTheSameLog_ADifferentSeedOnlyItsHeader()
        {
            Assert.AreEqual(Scripted(200f).ToJson(), Scripted(200f).ToJson());
            Assert.AreEqual(Scripted(200f).MinutesJson(), Scripted(200f, seed: 7).MinutesJson());
            StringAssert.Contains("\"minutes\":[", Scripted(130f).MinutesJson());
        }

        [Test]
        public void Recorder_ReadsTheRunsEventsAndCounters()
        {
            var events = new CombatEvents();
            var clock = new RunClock();
            var context = new RunContext(clock, events);
            float t = 0f;
            int kills = 0, gold = 0, level = 1;
            var telemetry = new RunTelemetry("test", 6, true, () => t, () => 99);
            var rec = new RunLogRecorder
            {
                RunTime = () => t, Seed = () => 99, Kills = () => kills, Gold = () => gold, Level = () => level, Pickups = () => kills,
                Xp = () => kills * 2L, HullKnown = () => true, Hull = () => 80f, MaxHull = () => 100f, SunkNow = () => false,
                RunEnded = () => true, ObjectiveSecured = () => true, ConfigHash = () => "cfg-x", RunId = () => "rid", BuildId = () => "b1",
                SeedOrigin = () => new RunRequest.SeedOrigin(SeedSource.Issued, "srv-9"), ShipPosition = () => new Vector3(1f, 0f, 2f),
                ShipForward = () => Vector3.right, SteerThrottle = () => new Vector2(0.5f, 1f), Boosting = () => false, Telemetry = telemetry,
            };
            rec.Initialize(context);
            rec.BeginRun(new RunStart(99, false));
            for (var i = 0; i < 90 * 30; i++)
            {
                t = i / 30f;
                if (i % 30 == 0)
                {
                    kills++;
                    telemetry.RecordHit("cannon", 10f, false, false, false);
                    telemetry.RecordKill("cannon");
                    events.Publish(new CombatEvent(CombatEventKind.Spawned, i, t));
                    events.Publish(new CombatEvent(CombatEventKind.Killed, i, t, b: i == 300 ? 1 : 0));
                    events.Publish(new CombatEvent(CombatEventKind.PlayerDamaged, i, t, z: 2f));
                }
                if (i == 600) events.Publish(new CombatEvent(CombatEventKind.ChestOpened, i, t));
                if (i == 1900) events.Publish(new CombatEvent(CombatEventKind.LandmarkUsed, i, t, tag: "Shrine"));
                events.EndTick();
                rec.Tick(new SimTick(i, 1f / 30f, t));
            }
            Assert.IsNull(rec.Closed, "the view shows no log while the run is going");
            var result = rec.Close();
            Assert.IsNotNull(result);
            Assert.AreSame(result, rec.Close(), "closing twice changes nothing");
            Assert.AreSame(result, rec.Closed, "the view shows the closed log");
            rec.Detach();
            var log = Parse(result.Json);
            Assert.AreEqual(kills, result.Kills);
            Assert.AreEqual(kills, log.minutes.Sum(m => m.kills));
            Assert.AreEqual(2, log.minutes.Length);
            Assert.AreEqual("issued", log.seedSource);
            Assert.AreEqual("victory", log.end);
            Assert.AreEqual(new[] { "cannon" }, log.weapons);
            Assert.AreEqual(kills, log.minutes.Sum(m => m.wk[0]));
            Assert.AreEqual(kills, log.minutes.Sum(m => m.spawns));
            Assert.AreEqual(1, log.minutes.Sum(m => m.eliteKills));
            Assert.AreEqual(1, log.minutes.Sum(m => m.chests));
            Assert.AreEqual(1, log.minutes.Sum(m => m.pois));
            Assert.AreEqual(kills * 2f, log.minutes.Sum(m => m.taken), 0.01f);
            Assert.AreEqual(kills * 10f, log.minutes.Sum(m => m.dealt), 0.01f);
            StringAssert.Contains("\"inputs\":[[0,1,2,90,50,100,0]", result.Json, "an input sample: t, x, z, heading, steer, throttle, boost");
            Assert.AreEqual(18, rec.Log.InputCount, "one sample every 5 s");
        }

        /// <summary>
        /// V5 M9a (review): the runtime closes the log at the run's end, on the tick batch that carries the run-ended state
        /// (after counting the whole batch), so presentation only reads it (IRunLogSource.Closed) and never changes it.
        /// </summary>
        [Test]
        public void Recorder_ClosesItselfOnTheRunEndedBatch_BeforeAnyPresenterReadsIt()
        {
            foreach (var endState in new[] { RunState.PlayerDead, RunState.RunComplete })
            {
                var events = new CombatEvents();
                var context = new RunContext(new RunClock(), events);
                float t = 0f;
                var kills = 0;
                var rec = new RunLogRecorder
                {
                    RunTime = () => t, Seed = () => 5, Kills = () => kills, Level = () => 2, ConfigHash = () => "cfg-x", BuildId = () => "b1",
                    HullKnown = () => true, Hull = () => 50f, MaxHull = () => 100f,
                };
                rec.Initialize(context);
                RunLogResult seenByPresenter = null;
                events.TickPublished += batch => { if (batch.Count > 0 && batch[batch.Count - 1].Kind == CombatEventKind.RunState) seenByPresenter = ((IRunLogSource)rec).Closed; };
                rec.BeginRun(new RunStart(5, false));
                for (var i = 0; i < 100; i++)
                {
                    t = i / 30f;
                    rec.Tick(new SimTick(i, 1f / 30f, t));
                    events.EndTick();
                }
                Assert.IsNull(((IRunLogSource)rec).Closed, endState + ": still open while the run is going");
                kills = 3;
                events.Publish(new CombatEvent(CombatEventKind.Spawned, 100, t));
                events.Publish(new CombatEvent(CombatEventKind.Killed, 100, t));
                events.Publish(new CombatEvent(CombatEventKind.RunState, 100, t, a: (int)endState));
                events.EndTick();
                Assert.IsNotNull(seenByPresenter, endState + ": the presenter reading the same batch sees the closed log");
                Assert.AreEqual(3, seenByPresenter.Kills);
                StringAssert.Contains("\"spawns\":1", seenByPresenter.Json, "the batch was counted before closing");
                StringAssert.Contains(endState == RunState.PlayerDead ? "\"end\":\"sunk\"" : "\"end\":", seenByPresenter.Json);
                rec.EndRun(new RunEnd(t, false));
                Assert.AreSame(seenByPresenter, ((IRunLogSource)rec).Closed, "EndRun after that changes nothing");
                rec.Detach();
            }
        }

        sealed class FixedLog : IRunLogSource
        {
            public RunLogResult Result;
            public int Reads;
            public RunLogResult Closed { get { Reads++; return Result; } }
        }

        [Test]
        public void Payload_CarriesTheLogAsItsLogField_AndTheSummaryFromTheClosedLog()
        {
            var b = Scripted(130f);
            var json = b.ToJson();
            var request = new RunSubmitRequest { kills = 1, runSeconds = 1, level = 1, captain = "rook", mapId = "saltwind", buildHash = "h", clientVersion = "0.5.0" };
            var source = new FixedLog { Result = new RunLogResult { Json = json, Kills = b.TotalKills(), Level = b.LastLevel, Seconds = b.Seconds } };
            RoguePirates.V4.Presentation.Online.RunSubmitter.AttachLog(request, source);
            Assert.AreEqual(1, source.Reads);
            Assert.AreEqual(b.TotalKills(), request.kills, "the summary's kills are the log's");
            Assert.AreEqual(Mathf.FloorToInt(b.Seconds), request.runSeconds);
            Assert.AreEqual(b.LastLevel, request.level);

            var body = request.ToJson();
            StringAssert.StartsWith("{\"kills\":" + request.kills + ",", body);
            StringAssert.EndsWith(",\"log\":" + json + "}", body);
            Assert.IsFalse(JsonUtility.ToJson(request).Contains("logJson"), "the log is not a JsonUtility field");

            var old = new RunSubmitRequest { kills = 3, runSeconds = 60, level = 2 };
            Assert.AreEqual(JsonUtility.ToJson(old), old.ToJson(), "no log: the old request shape");
            RoguePirates.V4.Presentation.Online.RunSubmitter.AttachLog(old, new FixedLog());
            Assert.IsNull(old.logJson);
            Assert.AreEqual(3, old.kills, "no log: the summary is untouched");
        }

        /// <summary>The server's fixture is this builder's output: the client and the API agree on the format.</summary>
        [Test]
        public void SampleLog_MatchesTheServerFixture()
        {
            var json = Scripted(745.3f, 4, seed: 20261008).ToJson();
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "server", "RoguePirates.Api.Tests", "Fixtures", "runlog-v1-sample.json"));
            if (!File.Exists(path))
            {
                File.WriteAllText(path, json);
                Assert.Fail("Fixture written to " + path + "; run again.");
            }
            Assert.AreEqual(File.ReadAllText(path), json, "the run log format changed: update the server's RunLogRules and regenerate the fixture (delete it and run this test)");
        }
    }
}
