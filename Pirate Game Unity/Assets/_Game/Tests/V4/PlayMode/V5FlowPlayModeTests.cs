using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Spawning;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M1 in the real run scene: jump the clock to minutes 1, 3, 6 and 9 (ship invulnerable, scripted arenas skipped so
    /// ambient spawning runs) and check the current keyframe and that the crowd reaches its floor; then 13:00 (overtime)
    /// keeps running without errors. Frame times per checkpoint are logged for the phase evidence.
    /// </summary>
    [Category("V5")]
    public sealed class V5FlowPlayModeTests
    {
        V4TestSaveIsolation _isolation;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _isolation = new V4TestSaveIsolation();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.Boot, LoadSceneMode.Single);
            yield return null;
            StageBalanceProfileContext.ResetForTests();
            _isolation?.Dispose();
            _isolation = null;
        }

        static IEnumerator WaitForPlaying()
        {
            var deadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (RunManager.Instance != null && RunManager.Instance.State == RunState.Playing && StageBalanceProfileContext.IsActive) yield break;
                yield return null;
            }
            Assert.Fail("V4 run did not reach Playing.");
        }

        static void KeepRunning()
        {
            if (!RunManager.Instance.IsPaused) return;
            var modal = Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Hud.LevelUpPresenter>();
            if (modal != null && modal.IsShowing) modal.Pick(0);
            else RunManager.Instance.SetPause(PauseReason.None);
        }

        [UnityTest]
        public IEnumerator Flow_AtMinutes1_3_6_9_ReachesTheWaveFloor_AndOvertimeRuns()
        {
            SceneManager.LoadScene(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return null;
            yield return WaitForPlaying();
            var game = Object.FindFirstObjectByType<V4Game>();
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            ship.Damage.SetPlaytestInvulnerable(true);
            Assert.IsTrue(game.Context.TryGet<ISpawnController>(out var spawn));
            var flow = spawn as IFlowStatus;
            Assert.IsNotNull(flow);
            Assert.IsTrue(flow.FlowEnabled, "the default config runs the V5 flow");
            Assert.IsTrue(game.Context.TryGet<IEnemySpawnSink>(out var sink));
            foreach (var phase in spawn.Phases)
                if (phase.PhaseId == "reef_crab" || phase.PhaseId.StartsWith("elite_")) spawn.Skip(phase.PhaseId);

            var run = RunManager.Instance;
            var log = new List<string>();
            var timeline = game.Config.Single<RoguePirates.V4.Config.WaveTimelineConfig>();
            foreach (var minute in new[] { 1, 3, 6, 9 })
            {
                var wave = -1; // V5 M3d: the keyframe current at the run time (M1: one wave per minute)
                run.JumpClock(minute * 60f + 5f);
                var deadline = Time.realtimeSinceStartup + 20f;
                var frames = 0;
                var frameTime = 0f;
                var reached = false;
                while (Time.realtimeSinceStartup < deadline)
                {
                    KeepRunning();
                    yield return null;
                    frames++;
                    frameTime += Time.unscaledDeltaTime;
                    timeline.flow.WaveAt(run.ElapsedSeconds, out wave);
                    if (flow.WaveIndex == wave && flow.Floor > 0 && sink.ActiveCount >= flow.Floor && frames > 30) { reached = true; break; }
                }
                log.Add($"{minute}:00 wave {flow.WaveIndex} floor {flow.Floor} alive {sink.ActiveCount} release {flow.InRelease} avg frame {1000f * frameTime / Mathf.Max(1, frames):0.0} ms");
                Assert.AreEqual(wave, flow.WaveIndex, $"minute {minute}: the keyframe for that time");
                Assert.IsTrue(reached, $"minute {minute}: the crowd reached the floor ({flow.Floor}); alive {sink.ActiveCount}");
            }

            run.JumpClock(13 * 60f);
            var until = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < until) { KeepRunning(); yield return null; }
            Assert.Greater(sink.ActiveCount, 0, "overtime keeps spawning");
            log.Add($"13:00 alive {sink.ActiveCount}");
            Debug.Log("[V5 M1 flow checkpoints] " + string.Join(" | ", log));
            Assert.Greater(flow.ToppedUp, 0, "the floor topped the crowd up at least once");
        }
    }
}
