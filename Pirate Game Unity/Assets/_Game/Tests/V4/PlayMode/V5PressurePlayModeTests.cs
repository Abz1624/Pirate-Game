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
            yield return StartRun();
            var (_, _) = Setup();
            var sim = V4TestEnemies.Simulation();
            Assert.IsNotNull(sim);
            var seen = new Dictionary<string, (float hp, float damage)>[] { new Dictionary<string, (float, float)>(), new Dictionary<string, (float, float)>() };
            var windows = new[] { 60f, 480f };
            for (var w = 0; w < windows.Length; w++)
            {
                RunManager.Instance.JumpClock(windows[w]);
                var known = new HashSet<int>(sim.Agents.Where(a => a.active).Select(a => a.id));
                var deadline = Time.realtimeSinceStartup + 25f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    KeepRunning();
                    yield return null;
                    foreach (var a in sim.Agents)
                    {
                        // Fresh spawns without elite, affix or boss multipliers (the hordes' ships are swordfish, dinghies and
                        // skiffs, none of which spawn at 8:00, so a shared id is an ambient spawn at both times).
                        if (!a.active || known.Contains(a.id) || a.isElite || a.isBoss || a.affix != default) continue;
                        known.Add(a.id);
                        var id = sim.Definitions[a.definitionIndex].id;
                        if (!seen[w].ContainsKey(id)) seen[w][id] = (a.maxHp, a.damage);
                    }
                    if (w == 1 && seen[0].Keys.Any(seen[1].ContainsKey)) break;
                }
            }
            var shared = seen[0].Keys.Where(seen[1].ContainsKey).ToList();
            Debug.Log("[V5 M3d same enemy] 1:00 " + string.Join(", ", seen[0].Select(kv => kv.Key + " " + kv.Value.hp)) + " | 8:00 " + string.Join(", ", seen[1].Select(kv => kv.Key + " " + kv.Value.hp)));
            Assert.IsNotEmpty(shared, "an enemy that spawns at both times (the cannon boat: band 0's specialist, band 4's ordinary)");
            foreach (var id in shared)
            {
                Assert.AreEqual(seen[0][id].hp, seen[1][id].hp, 1e-3f, id + ": the same HP at 1:00 and 8:00");
                Assert.AreEqual(seen[0][id].damage, seen[1][id].damage, 1e-3f, id + ": the same damage at 1:00 and 8:00");
            }
        }
    }
}
