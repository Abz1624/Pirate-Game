using System.Collections;
using NUnit.Framework;
using RoguePirates.V4.DevTools.Scenarios;
using RoguePirates.V4.DevTools.Trace;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Presentation.Online;
using RoguePirates.V4.Telemetry;
using RoguePirates.V4.Views;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M9a (I9a.1, I9a.2) in the real run scene through the Scenario Lab: a voyage keeps its run log and uploads it with
    /// a matching summary; the same seed and the same scripted inputs (the trace's wander pilot, a fixed frame time) give
    /// the same per-minute log.
    /// </summary>
    [Category("V5")]
    public sealed class V5RunLogPlayModeTests
    {
        // One physics step per frame (frame time = the fixed step), so the physics steps line up the same way in every run.
        static float FrameSeconds => Time.fixedDeltaTime;
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

        /// <summary>A short voyage: waves on, the scripted phases as the game runs them, the ship cannot sink, no cards.</summary>
        static ScenarioConfig Voyage(int seed)
        {
            var s = ScriptableObject.CreateInstance<ScenarioConfig>();
            s.name = "runlog_voyage_" + seed;
            s.seed = seed;
            s.invulnerable = true;
            s.skipLevelUpCards = true;
            s.ambientSpawning = true;
            s.scriptedPhases = ScenarioConfig.ScriptedPhaseMode.AsTheGame;
            s.loadout.replaceWeapons = false; // the starting weapon stays (an empty list would mean no weapons)
            return s;
        }

        /// <summary>Loads the voyage, flies the wander pilot at a fixed frame time until <paramref name="seconds"/> of run time.</summary>
        static IEnumerator Play(int seed, float seconds)
        {
            Time.captureDeltaTime = FrameSeconds;
            yield return Scenario.Load(Voyage(seed));
            Assert.IsNotNull(Scenario.Current, "the scenario loaded");
            CollectionAssert.IsEmpty(Scenario.Current.Errors);
            Scenario.Current.Ship.ConfigureBotControl(new ScriptedPilot());
            var deadline = Time.realtimeSinceStartup + 600f;
            while (RunManager.Instance != null && RunManager.Instance.ElapsedSeconds < seconds && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.GreaterOrEqual(RunManager.Instance.ElapsedSeconds, seconds, "the voyage reached its time");
        }

        [UnityTest]
        public IEnumerator Voyage_KeepsItsRunLog_AndUploadsItWithAMatchingSummary()
        {
            yield return Play(5151, 65f);
            var game = Scenario.Current.Game;
            Assert.IsNotNull(game.RunLog, "a voyage records its run log");
            Assert.AreEqual(1, game.RunLog.Log.MinuteCount - 1, "one minute closed so far");

            var views = game.Context.Get<IRunViews>();
            var request = RunSubmitter.Build(views);
            Assert.IsNotNull(request.logJson, "the upload carries the log");
            var log = game.RunLog.Result;
            Assert.AreEqual(log.Kills, request.kills);
            Assert.AreEqual(log.Level, request.level);
            Assert.AreEqual(Mathf.FloorToInt(log.Seconds), request.runSeconds);
            Assert.AreEqual(RunManager.Instance.KillCount, log.Kills, "the log's kills are the run's");
            Assert.Greater(log.Kills, 0, "the waves were fought");
            Assert.AreEqual(5151, log.Seed);
            Assert.IsFalse(string.IsNullOrEmpty(log.ConfigHash));
            StringAssert.Contains("\"seedSource\":\"requested\"", log.Json);
            StringAssert.Contains("\"buildId\":\"" + Application.version + "+editor\"", log.Json);
            Assert.Less(log.Bytes, 4096, "a one-minute log is small");
            Debug.Log($"[V5 M9a] scenario voyage: {log.Kills} kills in {log.Seconds:0.0} s, level {log.Level}, log {log.Bytes} bytes");
        }

        [UnityTest]
        public IEnumerator SameSeedAndInputs_GiveTheSamePerMinuteLog()
        {
            yield return Play(8282, 70f);
            var first = Close();
            yield return SceneManager.LoadSceneAsync(V4SceneNames.Boot, LoadSceneMode.Single);
            yield return null;
            StageBalanceProfileContext.ResetForTests();

            yield return Play(8282, 70f);
            var second = Close();
            Debug.Log("[V5 M9a] determinism minutes: " + first);
            StringAssert.Contains("\"kills\":", first);
            StringAssert.DoesNotContain("\"dealt\":0,", first.Substring(0, first.IndexOf("\"m\":1", System.StringComparison.Ordinal)), "the first minute had combat");
            Assert.AreEqual(first, second, "same seed and same scripted inputs: the same per-minute log");
        }

        /// <summary>Closes the log at exactly 70 s of run time (the first frame past it) and returns its minutes.</summary>
        static string Close()
        {
            var recorder = Scenario.Current.Game.RunLog;
            recorder.Close();
            return recorder.Log.MinutesJson();
        }
    }
}
