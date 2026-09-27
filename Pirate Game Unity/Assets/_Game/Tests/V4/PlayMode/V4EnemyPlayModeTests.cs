using System.Collections;
using System.Reflection;
using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Encounters;
using HighSeas.Enemies;
using HighSeas.Player;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Spawning;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoguePirates.V4.Tests
{
    /// <summary>M8: the enemy views and dev actions (the trace recorder, pilot and console read enemies only through them).</summary>
    [Category("V4")]
    public sealed class V4EnemyPlayModeTests
    {
        const float Timeout = 30f;
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
            RunMarksTracker.Reset();
            RunEncounterTracker.ResetForTests();
            SaltwindRunSettlement.ClearLastResults();
            _isolation?.Dispose();
            _isolation = null;
        }

        static IEnumerator StartRun()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            var deadline = Time.realtimeSinceStartup + Timeout;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (RunManager.Instance != null && RunManager.Instance.State == RunState.Playing && StageBalanceProfileContext.IsActive) yield break;
                yield return null;
            }
            Assert.Fail("V4 run did not reach Playing.");
        }

        static T Get<T>() where T : class
        {
            Assert.IsTrue(Object.FindFirstObjectByType<V4Game>().Context.TryGet<T>(out var value), typeof(T).Name + " registered");
            return value;
        }

        [UnityTest]
        public IEnumerator EnemyViews_MirrorTheSimulation_SlotBySlot_AndDevActionsUseTheHitPath()
        {
            yield return StartRun();
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            ship.Damage.SetPlaytestInvulnerable(true);
            var sink = Get<IEnemySpawnSink>();
            var enemies = Get<IEnemyWorldView>();
            var dev = Get<IEnemyDevControl>();
            var shots = Get<IHostileProjectileView>();
            var hazards = Get<IHazardView>();
            var world = Object.FindFirstObjectByType<EnemyWorld>();

            // Gunners that fire at the ship and a Kraken that lays traps, so shots and hazards exist while comparing.
            foreach (var id in new[] { "cannonboat", "cannonboat", "kraken_spawn", "shark" })
            {
                var index = sink.FindDefinition(id);
                Assert.GreaterOrEqual(index, 0, id);
                sink.Spawn(index, ship.Position + new Vector3(10f, 0f, 6f), ConfigEnums.EliteAffix.None, 1f, 1f, 1f);
            }

            var pool = (HostileProjectile[])typeof(HostileProjectileWorld).GetField("_pool", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(HostileProjectileWorld.Instance);
            var sawShot = false;
            var end = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
                Assert.AreEqual(world.Agents.Length, enemies.Capacity);
                Assert.AreEqual(world.ActiveCount, enemies.ActiveCount);
                for (var i = 0; i < world.Agents.Length; i++)
                {
                    var a = world.Agents[i];
                    Assert.AreEqual(a.active, enemies.TryGet(i, out var e), $"slot {i} active");
                    if (!a.active) continue;
                    Assert.AreEqual(i, e.slot);
                    Assert.AreEqual(a.id, e.unitId);
                    Assert.AreEqual(world.Definitions[a.definitionIndex].id, e.enemyId);
                    Assert.AreEqual(a.position, e.position);
                    Assert.AreEqual(a.velocity, e.velocity);
                    Assert.AreEqual(a.radius, e.radius);
                    Assert.AreEqual(a.hp, e.hp);
                    Assert.AreEqual(a.isElite, e.isElite);
                    Assert.AreEqual(a.isBoss, e.isBoss);
                    Assert.IsTrue(enemies.TryFindByUnitId(a.id, out var found) && found == i);
                }
                Assert.AreEqual(pool.Length, shots.Capacity);
                for (var i = 0; i < pool.Length; i++)
                {
                    Assert.AreEqual(pool[i].active, shots.TryGet(i, out var s), $"shot {i} active");
                    if (!pool[i].active) continue;
                    sawShot = true;
                    Assert.AreEqual(pool[i].position, s.position);
                    Assert.AreEqual(pool[i].velocity, s.velocity);
                }
                Assert.AreEqual(24, hazards.Capacity);
            }
            Assert.IsTrue(sawShot, "the cannonboats fired, so the shot view was compared on live shots");

            var target = -1;
            for (var i = 0; i < enemies.Capacity && target < 0; i++) if (enemies.TryGet(i, out _)) target = i;
            Assert.GreaterOrEqual(target, 0);
            dev.SetHealthFraction(target, 0.5f);
            Assert.AreEqual(world.Agents[target].maxHp * 0.5f, world.Agents[target].hp, 0.001f);
            var kills = RunManager.Instance.KillCount;
            dev.Kill(target, "deck_cannons");
            Assert.IsFalse(world.Agents[target].active, "killed");
            Assert.AreEqual(kills + 1, RunManager.Instance.KillCount, "through the kill path (the kill is counted)");
            dev.DespawnAll();
            Assert.AreEqual(0, enemies.ActiveCount);
        }
    }
}
