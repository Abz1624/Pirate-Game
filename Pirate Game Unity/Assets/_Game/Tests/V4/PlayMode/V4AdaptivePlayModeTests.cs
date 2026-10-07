using System.Collections;
using System.Collections.Generic;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Enemies;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Foundation.POI;
using NUnit.Framework;
using RoguePirates.V4.Difficulty;
using RoguePirates.V4.Events;
using RoguePirates.V4.Spawning;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoguePirates.V4.Tests
{
    /// <summary>M11: the adaptive levers in the real run scene, and the run record written at death.</summary>
    [Category("V4")]
    public sealed class V4AdaptivePlayModeTests
    {
        V4TestSaveIsolation _isolation;

        sealed class MemoryRecords : IRunRecordStore
        {
            public readonly List<RunScoreRecord> List = new List<RunScoreRecord>();
            public void Add(RunScoreRecord record) => List.Add(record);
            public IReadOnlyList<RunScoreRecord> Records => List;
        }

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

        /// <summary>The cap-HP lever on the real spawn path: fill the ambient cap, free a slot, the next ambient spawn has more HP.</summary>
        [UnityTest]
        public IEnumerator CapHpLever_RaisesHp_OfSpawnsMadeWhileTheCapBlocks()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return WaitForPlaying();
            var game = Object.FindFirstObjectByType<V4Game>();
            Object.FindFirstObjectByType<PlayerShipController>().Damage.SetPlaytestInvulnerable(true);
            // V5 M8z (7 Oct, I8z.5): no weapons, so the cap stays full (the playtest's starter weapons one-shot the first
            // band's dinghies and emptied the cap before it could block, so the lever never applied).
            game.Build.Progression.DevSetWeapons(System.Array.Empty<string>());
            // V5 M8z (7 Oct, review): level-ups from the kills below apply without a card (a card left the run in
            // LevelUpPaused, where nothing spawns: flaky, about 1 run in 3).
            game.Build.Progression.AutoLevelUp = () => { };
            Assert.IsTrue(game.Context.TryGet<IPressureView>(out var view));
            Assert.IsTrue(game.Config.TryApplyEdit("director.saltwind", "eliteLever", "false", 1f, out var error), error);
            Assert.IsTrue(game.Config.TryApplyEdit("director.saltwind", "toughnessLever", "false", 1f, out error), error); // cap HP alone
            Assert.IsTrue(game.Config.TryApplyEdit("director.saltwind", "capHpLever", "true", 1f, out error), error); // V5 M3d switched it off in the data; the mechanism stays
            Assert.IsTrue(game.Config.TryApplyEdit("timeline.saltwind", "reservedEncounterSlots", "0", 1f, out error), error);
            Assert.IsTrue(game.Config.TryApplyEdit("timeline.saltwind", "maxActiveEnemies", "8", 1f, out error), error);
            Assert.IsTrue(game.Config.TryApplyEdit("timeline.saltwind", "phases[0].ratePerSecond", "6", 1f, out error), error);
            var world = V4TestEnemies.Simulation();

            // Each ambient spawn's HP against the difficulty curve at its spawn time (early run: tier 1, stage 0, no ordinary
            // scaling yet), so the ratio is exactly the lever's HP multiplier.
            view.ForcedPressure = 1f;
            var curve = game.Config.Single<RoguePirates.V4.Config.DifficultyCurveConfig>();
            var uncapped = new List<(string id, float ratio)>();
            var capped = new List<(string id, float ratio)>();
            var collect = false;
            var experimentHp = Object.FindFirstObjectByType<V4Game>().Config.Single<RoguePirates.V4.Config.ExperimentConfig>().enemyHpMultiplier;
            void OnEvents(IReadOnlyList<CombatEvent> batch)
            {
                foreach (var e in batch)
                {
                    if (e.Kind != CombatEventKind.Spawned || (e.Flags & CombatEventFlags.AmbientSpawn) == 0) continue;
                    if (!world.TryGetAgentByUnitId(e.A, out var i) || world.Agents[i].isElite) continue;
                    var def = world.Definitions[world.Agents[i].definitionIndex];
                    // × the experiment's flat enemy HP (V5 M3c: an interim 0.85).
                    var expected = RoguePirates.V4.Difficulty.CurveEvaluator.EnemyHp(curve, def.baseHP, RunManager.Instance.ElapsedSeconds / 60f, 1, 0) * experimentHp;
                    (collect ? capped : uncapped).Add((e.Tag + $" (alive {world.ActiveCount})", world.Agents[i].maxHp / expected));
                }
            }
            game.Events.TickPublished += OnEvents;
            var deadline = Time.realtimeSinceStartup + 20f;
            while (world.ActiveCount < 8 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.GreaterOrEqual(world.ActiveCount, 8, "the cap filled");

            // At the top of the band: the cap blocks, one enemy dies, the next ambient spawn gets the cap HP.
            view.ForcedPressure = 1.35f;
            // Let the ambient spawner try (and be blocked) at least once: its next attempt is up to a retry interval away.
            var until = RunManager.Instance.ElapsedSeconds + 1f;
            while (RunManager.Instance.ElapsedSeconds < until) yield return null;
            collect = true;
            deadline = Time.realtimeSinceStartup + 10f;
            while (capped.Count == 0 && Time.realtimeSinceStartup < deadline)
            {
                if (RunManager.Instance.IsPaused) RunManager.Instance.SetPause(PauseReason.None); // no level-up card holds the clock
                for (var i = 0; i < world.Agents.Length; i++)
                    if (world.Agents[i].active && !world.Agents[i].isBoss) { world.ApplyDamage(i, world.Agents[i].maxHp + 1000f, false, weaponId: "deck_cannons"); break; }
                yield return null;
                yield return null;
            }
            game.Events.TickPublished -= OnEvents;
            Assert.Greater(uncapped.Count, 0);
            foreach (var (id, ratio) in uncapped) Assert.AreEqual(1f, ratio, 0.03f, $"{id}: ordinary HP at Pressure 1");
            Assert.Greater(capped.Count, 0, $"an ambient spawn filled the freed slot (alive {world.ActiveCount}, t {RunManager.Instance.ElapsedSeconds:0.0}, paused {RunManager.Instance.IsPaused}, state {RunManager.Instance.State})");
            foreach (var (id, ratio) in capped) Assert.AreEqual(1.2f, ratio, 0.03f, $"{id}: cap HP ×1.2 at Pressure 1.35");
        }

        [UnityTest]
        public IEnumerator Levers_BendAmbientSpawning_AndTheRunRecordIsWrittenAtDeath()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return WaitForPlaying();
            var game = Object.FindFirstObjectByType<V4Game>();
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            ship.Damage.SetPlaytestInvulnerable(true);
            Assert.IsTrue(game.Context.TryGet<ISpawnController>(out var spawn));
            Assert.IsTrue(game.Context.TryGet<IPressureView>(out var view));
            Assert.AreEqual(RoguePirates.V4.Config.DirectorMode.Adaptive, view.Mode, "Adaptive is the shipping default");
            // V5 M8z (7 Oct, I8z.5): the playtest switched the count and elite levers off in the data; the mechanism stays.
            Assert.IsTrue(game.Config.TryApplyEdit("director.saltwind", "threatRateLever", "true", 1f, out var error), error);
            Assert.IsTrue(game.Config.TryApplyEdit("director.saltwind", "eliteLever", "true", 1f, out error), error);
            var director = game.Config.Single<RoguePirates.V4.Config.DirectorConfig>();

            // Threat rate: forced Pressure moves the ambient rate inside its band.
            view.ForcedPressure = 1f;
            var baseRate = spawn.EffectiveRateMultiplier;
            view.ForcedPressure = 1.35f;
            Assert.AreEqual(baseRate * director.threatRateMax, spawn.EffectiveRateMultiplier, 1e-4f, "the band's top (playtest round 1: +75 %)");
            view.ForcedPressure = 0.85f;
            Assert.AreEqual(baseRate * director.threatRateMin, spawn.EffectiveRateMultiplier, 1e-4f, "the band's bottom (−15 %)");
            Assert.IsTrue(game.Config.TryApplyEdit("director.saltwind", "mode", "Observe", 1f, out error), error);
            view.ForcedPressure = 1.35f;
            Assert.AreEqual(baseRate, spawn.EffectiveRateMultiplier, 1e-6f, "Observe: no effect");
            Assert.IsTrue(game.Config.TryApplyEdit("director.saltwind", "mode", "Adaptive", 1f, out error), error);

            // Elite lever at 100 %: ambient spawns arrive as elites (flagged), with the elite settings.
            Assert.IsTrue(game.Config.TryApplyEdit("director.saltwind", "eliteChanceAtMax", "1", 1f, out error), error);
            // The ordinary-rewards path (playtest round 1 defaults to elite rewards; the switch's off path is still checked here).
            Assert.IsTrue(game.Config.TryApplyEdit("director.saltwind", "eliteLeverPaysEliteRewards", "false", 1f, out error), error);
            var world = V4TestEnemies.Simulation();
            var elites = new List<int>();
            void OnEvents(IReadOnlyList<CombatEvent> batch)
            {
                foreach (var e in batch)
                    if (e.Kind == CombatEventKind.Spawned && (e.Flags & CombatEventFlags.AdaptiveElite) != 0) elites.Add(e.A);
            }
            game.Events.TickPublished += OnEvents;
            var deadline = Time.realtimeSinceStartup + 20f;
            while (elites.Count < 2 && Time.realtimeSinceStartup < deadline) yield return null;
            game.Events.TickPublished -= OnEvents;
            Assert.GreaterOrEqual(elites.Count, 2, "ambient spawns became elites");
            Assert.IsTrue(world.TryGetAgentByUnitId(elites[0], out var index));
            Assert.IsTrue(world.Agents[index].isElite, "flagged elite in the world");
            Assert.IsTrue(world.Agents[index].ordinaryRewards, "pays ordinary rewards");
            Assert.IsNotNull(world.Agents[index].spawnProfile);
            StringAssert.DoesNotContain("adaptive elite", world.Agents[index].spawnProfile.name, "counts against its source spawner (the copy is gone)");

            // Per-kill rewards never adapt: with the powerup chance at 0, killing adaptive elites drops nothing
            // (a real elite would force a drop).
            Assert.IsTrue(game.Config.TryApplyEdit("powerups.saltwind", "dropChancePerKill", "0", 1f, out error), error);
            var powerupsBefore = game.Build.Powerups.SpawnedThisStage;
            foreach (var unit in elites)
                if (world.TryGetAgentByUnitId(unit, out var i) && world.Agents[i].active)
                    world.ApplyDamage(i, world.Agents[i].maxHp + 1000f, false, weaponId: "deck_cannons");
            yield return null;
            yield return null;
            Assert.AreEqual(powerupsBefore, game.Build.Powerups.SpawnedThisStage, "adaptive elites force no powerup drop");

            // Death writes one record: kills beside average/peak Pressure; a forced, edited run is not ranked.
            var records = new MemoryRecords();
            game.RunRecords = records;
            ship.Damage.SetPlaytestInvulnerable(false);
            ship.Damage.TakeDamage(99999f, Vector3.zero, false);
            deadline = Time.realtimeSinceStartup + 6f;
            while (records.List.Count == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(1, records.List.Count, "one record per run");
            var r = records.List[0];
            Assert.AreEqual(RunManager.Instance.KillCount, r.kills);
            Assert.AreEqual("Adaptive", r.directorMode);
            Assert.AreEqual(game.Config.Hash, r.configHash);
            Assert.IsTrue(r.pressureForced);
            Assert.IsFalse(r.ranked, "forced and edited runs are not ranked");
            Assert.LessOrEqual(r.peakPressure, 1.35f + 1e-5f);
            yield return null;
            Assert.AreEqual(1, records.List.Count, "still one record");
        }
    }
}
