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
        static ScenarioConfig Voyage(int seed, bool invulnerable = true)
        {
            var s = ScriptableObject.CreateInstance<ScenarioConfig>();
            s.name = "runlog_voyage_" + seed;
            s.seed = seed;
            s.invulnerable = invulnerable;
            s.skipLevelUpCards = true;
            s.ambientSpawning = true;
            s.scriptedPhases = ScenarioConfig.ScriptedPhaseMode.AsTheGame;
            s.loadout.replaceWeapons = false; // the starting weapon stays (an empty list would mean no weapons)
            return s;
        }

        /// <summary>Loads the voyage, flies the wander pilot at a fixed frame time until <paramref name="seconds"/> of run time.</summary>
        static IEnumerator Play(int seed, float seconds, bool invulnerable = true)
        {
            Time.captureDeltaTime = FrameSeconds;
            yield return Scenario.Load(Voyage(seed, invulnerable));
            Assert.IsNotNull(Scenario.Current, "the scenario loaded");
            CollectionAssert.IsEmpty(Scenario.Current.Errors);
            Scenario.Current.Ship.ConfigureBotControl(new ScriptedPilot());
            var deadline = Time.realtimeSinceStartup + 600f;
            while (RunManager.Instance != null && RunManager.Instance.ElapsedSeconds < seconds && !RunManager.Instance.IsRunEnded && Time.realtimeSinceStartup < deadline) yield return null;
            if (invulnerable) Assert.GreaterOrEqual(RunManager.Instance.ElapsedSeconds, seconds, "the voyage reached its time");
            else Assert.IsTrue(RunManager.Instance.IsRunEnded || RunManager.Instance.ElapsedSeconds >= seconds, "the voyage reached its time or sank");
        }

        [UnityTest]
        public IEnumerator Voyage_KeepsItsRunLog_AndUploadsItWithAMatchingSummary()
        {
            yield return Play(5151, 65f);
            var game = Scenario.Current.Game;
            Assert.IsNotNull(game.RunLog, "a voyage records its run log");
            Assert.AreEqual(1, game.RunLog.Log.MinuteCount - 1, "one minute closed so far");

            var views = game.Context.Get<IRunViews>();
            Assert.IsNull(RunSubmitter.Build(views).logJson, "the presentation does not close a running voyage's log");
            game.RunLog.Close(); // what the run's end does (the runtime closes the log; presentation only reads it)
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

        /// <summary>
        /// V5 M9a (I9a.3): a scripted voyage (the wander pilot, two full minutes and a partial one) passes the run checks'
        /// bounds (server/RoguePirates.Api/Plausibility/plausibility-rules.json, the game's checker RunLogPlausibility), and every
        /// kill was of an enemy whose spawn the log counted (the run checks' kills-within-spawns rule: every runtime spawn
        /// path, the boss and the Powder Cache included, goes through FlowDirector, ScriptedPhases or SpawnExecutor, which
        /// publish Spawned; only the dev tools' spawns do not).
        /// </summary>
        [UnityTest]
        public IEnumerator ScriptedVoyage_PassesTheRunChecks()
        {
            yield return Play(6161, 130f);
            var recorder = Scenario.Current.Game.RunLog;
            var log = recorder.Close();
            Assert.IsNotNull(log, "the voyage closed its log");
            var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "..", "server", "RoguePirates.Api", "Plausibility", "plausibility-rules.json"));
            var rules = RunLogPlausibility.ParseRules(System.IO.File.ReadAllText(path));
            var sameConfig = log.ConfigHash == rules.configHash;
            var verdict = RunLogPlausibility.Evaluate(rules, log.Json, checkConfig: false);
            Debug.Log($"[V5 M9a] scripted voyage: {log.Kills} kills in {log.Seconds:0.0} s, level {log.Level}; run checks (bounds): {verdict}; same config as the rules: {sameConfig}");
            Assert.AreEqual(RunLogPlausibility.Pass, verdict.Status, verdict.ToString());
            Assert.IsTrue(sameConfig, "the scenario voyage runs on the config the rules were made for");
            Assert.AreEqual(RunLogPlausibility.Pass, RunLogPlausibility.Evaluate(rules, log.Json).Status);
            Assert.Greater(log.Kills, 0);
            var spawns = 0;
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(log.Json, "\"spawns\":(\\d+)")) spawns += int.Parse(m.Groups[1].Value);
            Assert.LessOrEqual(log.Kills, spawns, "every kill was of a spawn the log counted");
        }

        /// <summary>
        /// V5 M9a (review): a voyage with damage on (the ship can be hit and can sink): its log, closed by the runtime (at the
        /// sinking, or by the test at 130 s), passes every shipped rule, PR-HULL (the hull against damage taken and heals)
        /// included, and records real damage.
        /// </summary>
        [UnityTest]
        public IEnumerator DamageOnVoyage_PassesTheRunChecks_IncludingTheHullLedger()
        {
            yield return Play(7373, 130f, invulnerable: false);
            var recorder = Scenario.Current.Game.RunLog;
            var sank = RunManager.Instance.IsRunEnded;
            if (sank) for (var i = 0; i < 5 && recorder.Closed == null; i++) yield return null; // the runtime closes it at the run's end
            var log = sank ? recorder.Closed : recorder.Close();
            Assert.IsNotNull(log, "the voyage closed its log");
            var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "..", "server", "RoguePirates.Api", "Plausibility", "plausibility-rules.json"));
            var rules = RunLogPlausibility.ParseRules(System.IO.File.ReadAllText(path));
            var verdict = RunLogPlausibility.Evaluate(rules, log.Json);
            var taken = 0.0;
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(log.Json, "\"taken\":([0-9.eE+-]+)"))
                taken += double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            Debug.Log($"[V5 M9a] damage-on voyage: {(sank ? "sank" : "afloat")} at {log.Seconds:0.0} s, {log.Kills} kills, level {log.Level}, damage taken {taken:0.#}; run checks: {verdict}");
            Assert.Greater(taken, 0.0, "the ship took damage (damage on)");
            Assert.AreEqual(RunLogPlausibility.Pass, verdict.Status, verdict.ToString());
            if (sank) StringAssert.Contains("\"end\":\"sunk\"", log.Json);
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
