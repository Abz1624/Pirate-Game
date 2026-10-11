using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using RoguePirates.V4.DevTools.Scenarios;
using RoguePirates.V4.DevTools.Trace;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M9b close 2 (single-player playable check after the shark and treasure-raider timers moved to the simulation's
    /// step): a short scripted single-player voyage in the real run scene (Scenario Lab, the trace's wander pilot) at a
    /// fixed 60 and 144 frames a second, with sharks and treasure raiders around the ship. The raiders' flights (1.5 s
    /// timers) and a shark's recovery (1.2 s, put on a live shark: no runtime path puts a shark in Recover) last their timers
    /// at both frame rates (before, the timers ran on the frame's delta time once per 60 Hz tick: 2.4 times as long at 144),
    /// and the run plays on (the clock runs, the weapons sink sharks, the ship stays afloat). Report in
    /// Temp/V5Net/singleplayer-shark-raider-&lt;fps&gt;.json.
    /// </summary>
    [Category("V5")]
    public sealed class V5SharkRaiderFrameRatePlayModeTests
    {
        V4TestSaveIsolation _isolation;
        float _captureDeltaTime;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _isolation = new V4TestSaveIsolation();
            _captureDeltaTime = Time.captureDeltaTime;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = _captureDeltaTime;
            yield return SceneManager.LoadSceneAsync(V4SceneNames.Boot, LoadSceneMode.Single);
            yield return null;
            StageBalanceProfileContext.ResetForTests();
            _isolation?.Dispose();
            _isolation = null;
        }

        static ScenarioConfig Voyage()
        {
            var s = ScriptableObject.CreateInstance<ScenarioConfig>();
            s.name = "shark_raider_frame_rate";
            s.seed = 4242;
            s.invulnerable = true;
            s.skipLevelUpCards = true;
            s.ambientSpawning = false;
            s.scriptedPhases = ScenarioConfig.ScriptedPhaseMode.SkipAll;
            s.loadout.replaceWeapons = false; // the starting weapon stays
            s.enemies.Add(new ScenarioEnemy { enemyId = "shark", count = 4, distance = 18f, bearing = 0f, spread = 40f });
            s.enemies.Add(new ScenarioEnemy { enemyId = "shark", count = 1, distance = 30f, bearing = 180f, hpMultiplier = 50f }); // the one put in Recover
            foreach (var bearing in new[] { 60f, 180f, 300f })
                s.enemies.Add(new ScenarioEnemy { enemyId = "treasure_raider", count = 1, distance = 7f, bearing = bearing, hpMultiplier = 50f });
            return s;
        }

        sealed class Track
        {
            public string id;
            public EnemyCombatPhase phase;
            public float timer;
            public float since = -1f;
        }

        [UnityTest]
        public IEnumerator SharkAndRaider_At60fps() => Play(60);

        [UnityTest]
        public IEnumerator SharkAndRaider_At144fps() => Play(144);

        static IEnumerator Play(int fps)
        {
            Time.captureDeltaTime = 1f / fps;
            yield return Scenario.Load(Voyage());
            var runner = Scenario.Current;
            Assert.IsNotNull(runner, "the scenario loaded");
            CollectionAssert.IsEmpty(runner.Errors);
            runner.Ship.ConfigureBotControl(new ScriptedPilot());
            var world = V4TestEnemies.Simulation();
            Assert.IsNotNull(world);
            var run = RunManager.Instance;
            var units = runner.Spawned.ToList();
            Assert.AreEqual(8, units.Count, "4 + 1 sharks and 3 raiders");
            var recoverShark = units[4].unitId; // the lone shark behind the ship
            var tracks = units.ToDictionary(u => u.unitId, u => new Track { id = u.enemyId });
            var flee = new List<float>();
            var recover = new List<float>();
            var naturalSharkRecovers = 0;
            var forced = false;
            var startRun = run.ElapsedSeconds;
            var startKills = run.KillCount;
            var startFrame = Time.frameCount;
            var deadline = Time.realtimeSinceStartup + 120f;
            const float Seconds = 20f;
            while (run.ElapsedSeconds - startRun < Seconds && !run.IsRunEnded && Time.realtimeSinceStartup < deadline)
            {
                if (!forced && run.ElapsedSeconds - startRun > 6f && world.CombatStates.TryGet(recoverShark, out var s0))
                {
                    s0.phase = EnemyCombatPhase.Recover;
                    s0.phaseTimer = 1.2f;
                    forced = true;
                }
                var now = run.ElapsedSeconds;
                foreach (var kv in tracks)
                {
                    var t = kv.Value;
                    if (!world.CombatStates.TryGet(kv.Key, out var state)) { t.since = -1f; continue; }
                    var target = t.id == "shark" ? EnemyCombatPhase.Recover : EnemyCombatPhase.Flee;
                    var inPhase = state.phase == target;
                    var restarted = inPhase && t.phase == target && state.phaseTimer > t.timer + 1e-4f;
                    if (t.since >= 0f && (!inPhase || restarted))
                    {
                        // The phase ended (or ended and began again within this frame's ticks: the timer went back up).
                        (t.id == "shark" ? recover : flee).Add(now - t.since);
                        t.since = -1f;
                    }
                    if (inPhase && (t.phase != target || restarted))
                    {
                        t.since = now;
                        if (t.id == "shark" && kv.Key != recoverShark) naturalSharkRecovers++;
                    }
                    t.phase = state.phase;
                    t.timer = state.phaseTimer;
                }
                yield return null;
            }
            var frames = Time.frameCount - startFrame;
            var ran = run.ElapsedSeconds - startRun;
            var kills = run.KillCount - startKills;
            var sb = new StringBuilder();
            var inv = CultureInfo.InvariantCulture;
            string List(List<float> l) => "[" + string.Join(", ", l.Select(v => v.ToString("0.000", inv))) + "]";
            sb.Append("{\n");
            sb.Append($"  \"report\": \"singleplayer-shark-raider-{fps}fps\",\n  \"utc\": \"{System.DateTime.UtcNow:o}\",\n  \"unity\": \"{Application.unityVersion}\",\n");
            sb.Append($"  \"frameSeconds\": {(1f / fps).ToString("0.000000", inv)},\n  \"frames\": {frames},\n  \"runSeconds\": {ran.ToString("0.000", inv)},\n");
            sb.Append($"  \"raiderFleeSeconds\": {List(flee)},\n  \"raiderFleeTimer\": 1.5,\n");
            sb.Append($"  \"sharkRecoverSeconds\": {List(recover)},\n  \"sharkRecoverTimer\": 1.2,\n  \"sharkRecoverForced\": {(forced ? "true" : "false")},\n  \"sharkRecoversWithoutForcing\": {naturalSharkRecovers},\n");
            sb.Append($"  \"kills\": {kills},\n  \"shipAlive\": {(!run.IsRunEnded ? "true" : "false")},\n  \"enemiesActiveAtEnd\": {world.Agents.Count(a => a.active)}\n}}\n");
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "V5Net", $"singleplayer-shark-raider-{fps}fps.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[V5 M9b close 2] {fps} fps: " + sb.ToString().Replace("\n", " "));

            Assert.GreaterOrEqual(ran, Seconds, "the run clock ran the voyage");
            Assert.IsFalse(run.IsRunEnded, "the run plays on");
            Assert.Greater(kills, 0, "the weapons sank sharks");
            Assert.IsTrue(forced, "a shark was put in Recover");
            Assert.AreEqual(1, recover.Count, "its recovery ended");
            Assert.AreEqual(1.2f, recover[0], 1f / 60f + 1f / fps + 1e-3f, "the recovery lasts its 1.2 s timer");
            Assert.GreaterOrEqual(flee.Count, 3, "every raider fled at least once");
            // A weapon hit's knockback holds the behaviour (and so its timer) for its few ticks: a hit flight can last up to
            // that much longer (seen once a voyage, 0.1 s, the same flight at both frame rates).
            var tolerance = 1f / 60f + 1f / fps + 1e-3f;
            foreach (var f in flee) Assert.That(f, Is.InRange(1.5f - tolerance, 1.5f + 0.15f + tolerance), "a flight lasts its 1.5 s timer (plus a knockback)");
            var sorted = flee.OrderBy(f => f).ToList();
            Assert.AreEqual(1.5f, sorted[sorted.Count / 2], tolerance, "the median flight lasts its 1.5 s timer");
        }
    }
}
