using System.Collections;
using System.Linq;
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
        public IEnumerator Switchover_V3EnemyPiecesGone_V4ReplacementsWired()
        {
            yield return StartRun();
            Assert.IsNull(Object.FindFirstObjectByType<EnemyWorld>(), "V3 EnemyWorld removed from the scene");
            Assert.IsNull(Object.FindFirstObjectByType<StatusController>(), "V3 StatusController removed from the scene");
            Assert.IsNull(Object.FindFirstObjectByType<HostileProjectileWorld>(), "V3 HostileProjectileWorld no longer added");
            Assert.IsNull(Object.FindFirstObjectByType<HellfirePatchWorld>(), "V3 HellfirePatchWorld no longer ensured");
            Assert.IsNull(Object.FindFirstObjectByType<HighSeas.V3.CombatTelemetryDriver>(), "V3 CombatTelemetryDriver no longer added");
            var systems = GameObject.Find("Systems");
            var roles = systems.GetComponents<RoguePirates.V4.Legacy.LegacyUpdateSlot>().Select(sl => sl.role).ToList();
            CollectionAssert.Contains(roles, "hellfire", "hellfire patches tick where V3 ensured HellfirePatchWorld");
            CollectionAssert.Contains(roles, "telemetry", "telemetry samples where CombatTelemetryDriver was added");
            var wiring = Object.FindFirstObjectByType<V4Game>().Shell.Enemies;
            Assert.AreSame(wiring.Services.Hazards, RoguePirates.V4.Legacy.LegacyHazardTelegraphView.Source, "the hazard rings draw the V4 hazards");

            // A hellfire patch burns an enemy standing in it (V3's patch world read the legacy enemy world).
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            ship.Damage.SetPlaytestInvulnerable(true);
            var sim = wiring.Simulation;
            var barge = System.Array.FindIndex(sim.Definitions, d => d != null && d.id == "boarding_barge");
            var at = ship.Position + new Vector3(18f, 0f, 0f);
            var slot = sim.Spawn(barge, at);
            Assert.GreaterOrEqual(slot, 0);
            var hp = sim.Agents[slot].hp;
            typeof(HellfirePatches).GetMethod("AddPatch", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(wiring.Hellfire, new object[] { sim.Agents[slot].position });
            var end = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < end && sim.Agents[slot].active && sim.Agents[slot].hp >= hp) yield return null;
            Assert.IsTrue(!sim.Agents[slot].active || sim.Agents[slot].hp < hp, "the patch damaged the enemy through the V4 hit path");
            Assert.Greater(wiring.Statuses.BurnStacks(slot) + (sim.Agents[slot].active ? 0f : 1f), 0f, "and set it burning");
        }

        [UnityTest]
        public IEnumerator AreaDenialRelease_RunsInTheRun_AndNotUnderTheParityPreset()
        {
            foreach (var parity in new[] { false, true })
            {
                if (parity)
                    RunRequest.RequestPreset(System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../../plan/v4-verification/presets/v3-parity.json")));
                yield return StartRun();
                Object.FindFirstObjectByType<PlayerShipController>().Damage.SetPlaytestInvulnerable(true);
                var budget = Object.FindFirstObjectByType<V4Game>().Shell.Enemies.Services.Budget;
                while (budget.TryReserveAreaDenial(0)) { } // fill it (two Kraken casts)
                var t0 = RunManager.Instance.ElapsedSeconds;
                var deadline = Time.realtimeSinceStartup + 8f;
                while (RunManager.Instance.ElapsedSeconds < t0 + 2.3f && Time.realtimeSinceStartup < deadline)
                {
                    if (RunManager.Instance.IsPaused) RunManager.Instance.SetPause(PauseReason.None); // no level-up card holds the clock
                    yield return null;
                }
                Assert.GreaterOrEqual(RunManager.Instance.ElapsedSeconds, t0 + 2.3f);
                if (parity) Assert.AreEqual(2, budget.ActiveAreaDenial, "v3-parity: never released (R-06 off)");
                else Assert.Less(budget.ActiveAreaDenial, 2, "R-06: the shell frees each cast 2 s later on the run clock");
            }
        }

        [UnityTest]
        public IEnumerator TelemetrySlot_SamplesTheV4EnemyWorld()
        {
            yield return StartRun();
            Object.FindFirstObjectByType<PlayerShipController>().Damage.SetPlaytestInvulnerable(true);
            var sim = V4TestEnemies.Simulation();
            var deadline = Time.realtimeSinceStartup + 10f;
            while (sim.ActiveCount < 3 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.GreaterOrEqual(sim.ActiveCount, 3, "ambient spawns arrived");
            yield return null;
            var peak = (float)typeof(RunCombatTelemetry).GetField("_peakEnemies", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(RunCombatTelemetry.Instance);
            Assert.GreaterOrEqual(peak, 3, "the telemetry slot counts the V4 world's enemies (V3's driver would read 0 now)");
        }

        [UnityTest]
        public IEnumerator Switchover_ParityPresetBarrels_RunOnTheV4World()
        {
            var preset = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../../plan/v4-verification/presets/v3-parity.json"));
            RunRequest.RequestPreset(preset);
            yield return StartRun();
            var legacy = Object.FindFirstObjectByType<HighSeas.V3.V3ExplosiveBarrelWorld>();
            Assert.IsNotNull(legacy, "the parity preset places V3's barrels (their markers stay V3's)");
            Assert.IsFalse(legacy.enabled, "V3's barrel update is switched off");
            var slots = legacy.GetComponents<RoguePirates.V4.Legacy.LegacyUpdateSlot>();
            Assert.AreEqual(1, slots.Count(sl => sl.role == "barrels"), "a V4 barrel slot on the same object");
            Assert.AreEqual(2, Object.FindFirstObjectByType<V4Game>().Shell.Enemies.Barrels.Count);
        }

        [UnityTest]
        public IEnumerator EnemyContracts_MapTheSimulation_SlotBySlot_AndDevActionsUseTheHitPath()
        {
            yield return StartRun();
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            ship.Damage.SetPlaytestInvulnerable(true);
            var sink = Get<IEnemySpawnSink>();
            var enemies = Get<IEnemyWorldView>();
            var dev = Get<IEnemyDevControl>();
            var shots = Get<IHostileProjectileView>();
            var hazards = Get<IHazardView>();
            // M8: the views are the V4 enemy world's; compare them with its own arrays.
            Assert.IsNull(Object.FindFirstObjectByType<EnemyWorld>(), "M8: no V3 EnemyWorld in the run");
            var world = V4TestEnemies.Simulation();

            // Gunners that fire at the ship and a Kraken that lays traps, so shots and hazards exist while comparing.
            foreach (var id in new[] { "cannonboat", "cannonboat", "kraken_spawn", "shark" })
            {
                var index = sink.FindDefinition(id);
                Assert.GreaterOrEqual(index, 0, id);
                sink.Spawn(index, ship.Position + new Vector3(10f, 0f, 6f), ConfigEnums.EliteAffix.None, 1f, 1f, 1f);
            }

            var pool = Object.FindFirstObjectByType<V4Game>().Shell.Enemies.Services.Shots.Pool;
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
