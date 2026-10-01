using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
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
    /// V5 M3d in the real run scene (owner, 1 Oct 2026, notes e and h): the clock jumped to a peak and a valley of the
    /// keyframed crowd gives the floor the keyframes' line says; an enemy spawned at 1:00 and the same enemy spawned at
    /// 8:00 have the same HP and damage.
    /// </summary>
    [Category("V5")]
    public sealed class V5PressurePlayModeTests
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

        static IEnumerator StartRun()
        {
            SceneManager.LoadScene(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return null;
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

        static (V4Game game, ISpawnController spawn) Setup()
        {
            var game = Object.FindFirstObjectByType<V4Game>();
            Object.FindFirstObjectByType<PlayerShipController>().Damage.SetPlaytestInvulnerable(true);
            Assert.IsTrue(game.Context.TryGet<ISpawnController>(out var spawn));
            foreach (var phase in spawn.Phases)
                if (phase.PhaseId == "reef_crab" || phase.PhaseId.StartsWith("elite_")) spawn.Skip(phase.PhaseId);
            return (game, spawn);
        }

        [UnityTest]
        public IEnumerator TheFloor_FollowsTheKeyframes_AtAPeakAndAValley()
        {
            yield return StartRun();
            var (game, spawn) = Setup();
            var flow = (IFlowStatus)spawn;
            var timeline = game.Config.Single<WaveTimelineConfig>();
            Assert.IsTrue(timeline.flow.smoothWaves);
            var floors = new List<int>();
            // The 3:35 peak's approach (before its horde's lull), then the valley at 5:00.
            foreach (var at in new[] { 200f, 300f })
            {
                RunManager.Instance.JumpClock(at);
                for (var i = 0; i < 10; i++) { KeepRunning(); yield return null; }
                var now = RunManager.Instance.ElapsedSeconds;
                Assert.IsTrue(timeline.flow.Sample(now, out var share, out _));
                // A horde the jump passed may open a release window (the floor × the release's floor multiplier).
                var release = flow.InRelease ? timeline.flow.release.floorMultiplier : 1f;
                var cap = timeline.maxActiveEnemies - timeline.reservedEncounterSlots;
                var density = Mathf.Max(0.1f, game.Config.Single<ExperimentConfig>().spawnDensityMultiplier);
                var levers = game.Context.TryGet<RoguePirates.V4.Difficulty.IDifficultyLevers>(out var l) && l.Adaptive ? l.ThreatRateMultiplier : 1f;
                Assert.AreEqual(Mathf.Min(cap, Mathf.RoundToInt(share * release * levers * density * cap)), flow.Floor, 1, $"{now:0} s: the floor on the keyframes' line");
                floors.Add(flow.Floor);
            }
            Debug.Log("[V5 M3d floors] 3:20 " + floors[0] + ", 5:00 " + floors[1]);
        }

        [UnityTest]
        public IEnumerator AnEnemy_HasTheSameHpAndDamage_At1And8Minutes()
        {
            // The same spawner (band 0's ambient spawner: a shark, not elite) spawned on purpose at 1:00 and at 8:00 through
            // the game's spawn path; with no HP or damage growth over time both sharks match (owner note h).
            yield return StartRun();
            var (game, spawn) = Setup();
            var sim = V4TestEnemies.Simulation();
            Assert.IsNotNull(sim);
            var executor = (IEncounterExecutor)spawn;
            Assert.IsTrue(game.Context.TryGet<IEncounterTracker>(out var tracker));
            // Spawned through the encounter path, so the ambient levers are not exercised: they must stay off (M3d), else
            // ambient enemies could still grow tougher with time.
            var director = game.Config.Single<DirectorConfig>();
            Assert.IsFalse(director.toughnessLever || director.capHpLever || director.focusLever, "the director's HP and damage levers are off");
            var spawner = game.Config.Get<EnemySpawnerConfig>("spawner.saltwind_ambient_band_0");
            Assert.IsNotNull(spawner);
            Assert.IsFalse(spawner.elite.isElite);
            var stats = new List<(float hp, float damage, string id)>();
            foreach (var at in new[] { 60f, 480f })
            {
                RunManager.Instance.JumpClock(at);
                KeepRunning();
                yield return null;
                var known = new HashSet<int>(sim.Agents.Where(a => a.active).Select(a => a.id));
                Assert.AreEqual(1, executor.TriggerImmediate(spawner, 1, tracker.Request(EncounterKind.PowderCache, 1)), $"spawned at {at} s");
                var fresh = sim.Agents.Where(a => a.active && !known.Contains(a.id)).ToList();
                Assert.AreEqual(1, fresh.Count, $"one new enemy at {at} s");
                stats.Add((fresh[0].maxHp, fresh[0].damage, sim.Definitions[fresh[0].definitionIndex].id));
            }
            Debug.Log($"[V5 M3d same enemy] {stats[0].id}: 1:00 HP {stats[0].hp} damage {stats[0].damage} | 8:00 HP {stats[1].hp} damage {stats[1].damage}");
            Assert.AreEqual(stats[0].id, stats[1].id);
            Assert.AreEqual(stats[0].hp, stats[1].hp, 1e-3f, "the same HP at 1:00 and 8:00");
            Assert.AreEqual(stats[0].damage, stats[1].damage, 1e-3f, "the same damage at 1:00 and 8:00");
        }
    }
}
