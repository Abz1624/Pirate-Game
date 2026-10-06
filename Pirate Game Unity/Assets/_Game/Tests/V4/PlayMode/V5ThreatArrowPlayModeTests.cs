using System.Collections;
using NUnit.Framework;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Presentation.Hud;
using RoguePirates.V4.Spawning;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M8z (6 Oct, owner: "show an onscreen arrow/pointer towards the miniboss/scheduled elites when they are out of
    /// player's view"): in the real run scene, the Reef Crab's focus phase spawns a unit marked focus; held far off screen
    /// it gets a red-orange edge arrow with its name, and the arrow goes when the crab is on screen.
    /// </summary>
    [Category("V5")]
    public sealed class V5ThreatArrowPlayModeTests
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

        [UnityTest]
        public IEnumerator AFarMiniboss_GetsAnEdgeArrow_HiddenOnScreen()
        {
            SceneManager.LoadScene(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return null;
            var deadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deadline && (RunManager.Instance == null || RunManager.Instance.State != RunState.Playing)) yield return null;
            Assert.AreEqual(RunState.Playing, RunManager.Instance.State, "the run is playing");
            var game = Object.FindFirstObjectByType<V4Game>();
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            ship.Damage.SetPlaytestInvulnerable(true);
            var arrows = Object.FindFirstObjectByType<ThreatArrowPresenter>();
            Assert.IsNotNull(arrows, "the threat arrows are on the run canvas");
            Assert.AreEqual(0, arrows.VisibleCount, "no arrow before a miniboss");

            Assert.IsTrue(game.Context.TryGet<ISpawnController>(out var spawn));
            Assert.IsTrue(spawn.TriggerNow("reef_crab"));
            deadline = Time.realtimeSinceStartup + 10f;
            while (!spawn.ArenaActive && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(spawn.ArenaActive, "the Reef Crab spawned");
            var world = V4TestEnemies.Simulation();
            Assert.IsTrue(world.TryGetAgentByUnitId(spawn.ArenaFocusUnitId, out var index));
            Assert.IsTrue(world.Agents[index].focus, "a focus phase's unit is marked focus");

            // Held far to the right of the ship: off screen, one red-orange arrow named after it.
            var cam = Camera.main;
            for (var i = 0; i < 5; i++)
            {
                if (RunManager.Instance.IsPaused) RunManager.Instance.SetPause(PauseReason.None);
                world.Agents[index].position = ship.transform.position + cam.transform.right * 250f;
                yield return null;
            }
            world.Agents[index].position = ship.transform.position + cam.transform.right * 250f;
            yield return null;
            Assert.IsFalse(ThreatArrowPresenter.IsOnScreen(cam.WorldToViewportPoint(world.Agents[index].position), 0.04f), "the crab is off screen");
            Assert.GreaterOrEqual(arrows.VisibleCount, 1, "an arrow towards the crab");
            var name = world.Definitions[world.Agents[index].definitionIndex].displayName;
            var found = false;
            for (var i = 0; i < arrows.VisibleCount; i++)
                if (arrows.LabelAt(i).StartsWith(name.ToUpperInvariant())) found = true;
            Assert.IsTrue(found, "the arrow is labelled " + name);
            Debug.Log("[V5 M8z threat arrow] " + arrows.LabelAt(0).Replace("\n", " "));

            // On screen (at the ship): no arrow.
            for (var i = 0; i < 3; i++)
            {
                world.Agents[index].position = ship.transform.position + cam.transform.right * 2f;
                yield return null;
            }
            Assert.AreEqual(0, arrows.VisibleCount, "no arrow while the crab is on screen");
        }
    }
}
