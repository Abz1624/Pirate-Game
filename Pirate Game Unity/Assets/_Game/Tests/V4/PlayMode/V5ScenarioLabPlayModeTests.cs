using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.DevTools.Scenarios;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Events;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Online;
using RoguePirates.V4.Presentation.Hud;
using RoguePirates.V4.Progression;
using RoguePirates.V4.Spawning;
using RoguePirates.V4.Stage;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M8z (7 Oct, I8z.4): one condition per test, set up directly by a Scenario Lab scenario
    /// (Assets/_Game/V4/DevTools/Scenarios/Scenarios) in the real run scene instead of playing a voyage up to it. Each runs
    /// for a few seconds of game time after the scene loads.
    /// </summary>
    [Category("V5")]
    public sealed class V5ScenarioLabPlayModeTests
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
            Assert.IsNull(Scenario.Current, "the scenario ended with its scene");
            StageBalanceProfileContext.ResetForTests();
            _isolation?.Dispose();
            _isolation = null;
        }

        static ScenarioRunner Runner => Scenario.Current;
        static V4Game Game => Runner.Game;
        static PlayerShipController Ship => Runner.Ship;
        static EnemySimulation World => V4TestEnemies.Simulation();

        static IEnumerator Load(string name)
        {
            yield return Scenario.Load(name);
            Assert.IsNotNull(Runner, name + " is loaded");
            CollectionAssert.IsEmpty(Runner.Errors, name + " set up without problems");
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        [UnityTest]
        public IEnumerator CrabAt10m_KeepsItsFightingDistance()
        {
            yield return Load("crab_at_10m");
            Assert.AreEqual(600f, RunManager.Instance.ElapsedSeconds, 1f, "the clock jumped to 10:00");
            var unit = Runner.Spawned.Single().unitId;
            float Edge()
            {
                Assert.IsTrue(World.TryGetAgentByUnitId(unit, out var i), "the crab is alive");
                return Flat(World.Agents[i].position - Ship.Position) - World.Agents[i].radius;
            }
            var startEdge = Edge();
            // It closes in from 16 m to its band (hull 3 to 5.5 m off the ship), then holds there.
            var deadline = Time.time + 6f;
            while (Edge() > 6f && Time.time < deadline) yield return null;
            Assert.LessOrEqual(Edge(), 6f, $"the crab came in (from {startEdge:0.0} m)");
            var samples = new List<float>();
            var until = Time.time + 2.5f;
            while (Time.time < until) { samples.Add(Edge()); yield return null; }
            var inBand = samples.Count(e => e >= 2f && e <= 7f) / (float)samples.Count;
            Debug.Log($"[V5 Scenario crab] hull gap {samples.Min():0.0} to {samples.Max():0.0} m over {samples.Count} frames, {inBand:P0} in the band");
            Assert.GreaterOrEqual(inBand, 0.85f, "it keeps its distance (not ramming, not drifting off)");
            Assert.Greater(samples.Min(), 1f, "it never touches the ship");
        }

        [UnityTest]
        public IEnumerator EliteHammerheadOffscreen_GetsAnEdgeArrow()
        {
            yield return Load("elite_hammerhead_offscreen");
            yield return null;
            var unit = Runner.Spawned.Single().unitId;
            Assert.IsTrue(World.TryGetAgentByUnitId(unit, out var index));
            var agent = World.Agents[index];
            Assert.IsTrue(agent.focus, "a scheduled elite is a focus unit");
            Assert.IsFalse(ThreatArrowPresenter.IsOnScreen(Camera.main.WorldToViewportPoint(agent.position), 0.04f), "it is off screen");
            var arrows = Object.FindFirstObjectByType<ThreatArrowPresenter>();
            Assert.IsNotNull(arrows);
            Assert.GreaterOrEqual(arrows.VisibleCount, 1, "an edge arrow");
            var name = World.Definitions[agent.definitionIndex].displayName.ToUpperInvariant();
            Assert.IsTrue(Enumerable.Range(0, arrows.VisibleCount).Any(i => arrows.LabelAt(i).StartsWith(name)), "labelled " + name);
        }

        [UnityTest]
        public IEnumerator BossAtBeaconEarly_HasIts1400Hull()
        {
            yield return Load("boss_at_beacon_early");
            Assert.IsTrue(Game.Context.TryGet<IStageContent>(out var stage));
            var deadline = Time.time + 10f;
            while (!stage.BossSummoned && Time.time < deadline) yield return null;
            Assert.IsTrue(stage.BossSummoned, "the Beacon raised the boss");
            var summonedAt = RunManager.Instance.ElapsedSeconds;
            var asIf = Game.Config.Single<DirectorConfig>().bossHpAsIfAtSeconds;
            Assert.Greater(asIf, summonedAt + 60f, "summoned well before its usual time");
            Assert.IsTrue(World.TryFindActiveBoss(out _, out var early));

            // The same boss summoned at its usual time, for comparison.
            Assert.IsTrue(Game.Context.TryGet<IEnemyDevControl>(out var dev));
            dev.DespawnAll();
            stage.Module.ForgetDespawnedBoss();
            yield return null;
            RunManager.Instance.JumpClock(asIf);
            yield return null;
            Assert.IsTrue(stage.TrySummonBoss(), "summoned again at " + asIf);
            yield return null;
            Assert.IsTrue(World.TryFindActiveBoss(out _, out var onTime));
            var curve = Game.Config.Single<DifficultyCurveConfig>();
            var without = onTime.maxHp * Difficulty.CurveEvaluator.ExpectedHpScale(curve, summonedAt) / Difficulty.CurveEvaluator.ExpectedHpScale(curve, asIf);
            Debug.Log($"[V5 Scenario boss] hull at {summonedAt:0} s: {early.maxHp:0}; at {asIf:0} s: {onTime.maxHp:0}; without the rule it would be {without:0}");
            Assert.AreEqual(onTime.maxHp, early.maxHp, onTime.maxHp * 0.02f, "the early boss has its usual-time hull");
        }

        [UnityTest]
        public IEnumerator SixWeaponsLevel12_StillOffersNewLogbooks()
        {
            yield return Load("six_weapons_level_12");
            var progression = Game.Build.Progression;
            Assert.AreEqual(6, progression.OwnedWeapons.Count, "six weapons");
            Assert.GreaterOrEqual(progression.Level, 12);
            Assert.AreEqual(0, progression.OwnedLogbooks.Count);
            var deals = 0;
            var newLogbooks = 0;
            for (; deals < 30 && newLogbooks == 0; deals++)
                newLogbooks += progression.GenerateOffers().Count(o => o.Kind == OfferKind.Logbook && o.IsNew);
            Debug.Log($"[V5 Scenario six weapons] a new Logbook card within {deals} deals");
            Assert.Greater(newLogbooks, 0, "new Logbook cards with all weapon slots full");
            yield return null;
        }

        [UnityTest]
        public IEnumerator IslandHit_Costs5HullAtMostEvery2s()
        {
            yield return Load("island_hit");
            var config = Game.Config.Single<RunConfig>();
            Assert.AreEqual(5f, config.islandImpactDamage, "owner's rule: 5 hull per island hit (update this test with the rule)");
            Assert.AreEqual(2f, config.islandDamageCooldown, "owner's rule: at most one hull cost per 2 s");
            var settings = Ship.IslandImpact;
            Assert.AreEqual(config.islandImpactDamage, settings.Damage, "the ship reads RunConfig");
            Assert.AreEqual(config.islandDamageCooldown, settings.DamageCooldown);
            Assert.IsTrue(Game.Context.TryGet<IPlayerDamageSink>(out var sink));
            var service = (PlayerDamageService)sink;
            var before = service.ImpactDamageTotal;
            var count = Ship.IslandDamageCount;
            const float Seconds = 5f;
            yield return new WaitForSeconds(Seconds);
            var hits = Ship.IslandDamageCount - count;
            var multiplier = Game.Build.IncomingDamageMultiplier();
            Debug.Log($"[V5 Scenario island] {hits} hull costs in {Seconds} s, {service.ImpactDamageTotal - before:0.##} hull (incoming ×{multiplier:0.##})");
            Assert.GreaterOrEqual(hits, 2, "held into the shore, it keeps costing hull");
            Assert.LessOrEqual(hits, Mathf.CeilToInt(Seconds / settings.DamageCooldown) + 1, "at most one per damage cooldown");
            Assert.AreEqual(hits * settings.Damage * multiplier, service.ImpactDamageTotal - before, 0.01f, "5 hull each (through the build's incoming multiplier)");
        }

        [UnityTest]
        public IEnumerator HealthBars_OnElitesMinibossesAndTheBoss_NotOrdinaryEnemies()
        {
            yield return Load("mixed_health_bars");
            yield return null;
            var world = World;
            foreach (var (enemyId, unit) in Runner.Spawned)
            {
                Assert.IsTrue(world.TryGetAgentByUnitId(unit, out var i), enemyId + " alive");
                var a = world.Agents[i];
                var def = world.Definitions[a.definitionIndex];
                var special = a.affix != EliteAffix.None || def.isElite || def.isMiniboss || def.isBoss;
                Assert.AreEqual(special, a.healthBar != null, $"{enemyId} (affix {a.affix}, elite {def.isElite}, miniboss {def.isMiniboss}): health bar {(special ? "shown" : "hidden")}");
            }
            Assert.IsTrue(Runner.Spawned.Any(s => s.enemyId == "shark"), "an ordinary enemy is in the scenario");
            Assert.IsTrue(world.TryFindActiveBoss(out _, out var boss), "the boss is up");
            Assert.IsNotNull(boss.healthBar, "the boss has its health bar");
        }

        [UnityTest]
        public IEnumerator KillBandHigh_BringsKillBandElites()
        {
            var elites = 0;
            var ambient = 0;
            System.Action<IReadOnlyList<CombatEvent>> onTick = batch =>
            {
                for (var i = 0; i < batch.Count; i++)
                {
                    if (batch[i].Kind != CombatEventKind.Spawned) continue;
                    if ((batch[i].Flags & CombatEventFlags.AmbientSpawn) != 0) ambient++;
                    if ((batch[i].Flags & CombatEventFlags.AdaptiveElite) != 0) elites++;
                }
            };
            yield return Load("kill_band_high");
            var events = Game.Events;
            events.TickPublished += onTick;
            try
            {
                Assert.IsTrue(Game.Context.TryGet<Difficulty.IPressureView>(out var pressure));
                var director = (Difficulty.DifficultyDirector)pressure;
                var deadline = Time.time + 10f;
                while (elites == 0 && Time.time < deadline) yield return null;
                Debug.Log($"[V5 Scenario kill band] {ambient} ambient spawns, {Runner.Sunk} sunk, band elite chance {director.KillBandEliteChance:0.##}, {elites} kill-band elites");
                Assert.Greater(Runner.Sunk, 0, "enemies were sunk as they came");
                Assert.AreEqual(1f, director.KillBandEliteChance, 0.001f, "the band was judged high (elite chance as configured)");
                Assert.Greater(elites, 0, "kill-band elites in the next band");
            }
            finally { events.TickPublished -= onTick; }
        }

        [UnityTest]
        public IEnumerator ArenaFight_PausesTheWavesUntilTheFocusDies()
        {
            yield return Load("arena_reef_crab");
            Assert.IsTrue(Game.Context.TryGet<ISpawnController>(out var spawn));
            var deadline = Time.time + 10f;
            while (!spawn.ArenaActive && Time.time < deadline) yield return null;
            Assert.IsTrue(spawn.ArenaActive, "the Reef Crab's arena is on");
            // The frame the crab rose in also delivers that frame's earlier wave spawns (events go out at the frame's end).
            yield return null;
            var ambient = 0;
            var seen = new List<string>();
            System.Action<IReadOnlyList<CombatEvent>> onTick = batch =>
            {
                for (var i = 0; i < batch.Count; i++)
                    if (batch[i].Kind == CombatEventKind.Spawned && (batch[i].Flags & CombatEventFlags.AmbientSpawn) != 0)
                    {
                        ambient++;
                        seen.Add($"{batch[i].Tag} at {batch[i].SimTime:0.0} s");
                    }
            };
            var events = Game.Events;
            events.TickPublished += onTick;
            try
            {
                yield return new WaitForSeconds(3f);
                Assert.IsTrue(spawn.ArenaActive, "still fighting");
                Assert.AreEqual(0, ambient, "no waves during the arena fight: " + string.Join(", ", seen));
                Assert.IsTrue(World.TryGetAgentByUnitId(spawn.ArenaFocusUnitId, out var crab), "the focus unit");
                Assert.IsTrue(Game.Context.TryGet<IEnemyDevControl>(out var dev));
                dev.Kill(crab, "scenario");
                deadline = Time.time + 8f;
                while ((spawn.ArenaActive || ambient == 0) && Time.time < deadline) yield return null;
                Debug.Log($"[V5 Scenario arena] waves back {8f - (deadline - Time.time):0.0} s after the crab sank ({ambient} spawns)");
                Assert.IsFalse(spawn.ArenaActive, "the arena ended with its focus");
                Assert.Greater(ambient, 0, "the waves came back");
            }
            finally { events.TickPublished -= onTick; }
        }

        [UnityTest]
        public IEnumerator ConsoleLoad_SetsUpTheScenarioAcrossTheSceneChange()
        {
            SceneManager.LoadScene(V4SceneNames.Boot, LoadSceneMode.Single);
            yield return null;
            var scenario = Scenario.Find("elite_hammerhead_offscreen");
            Assert.IsNotNull(scenario, "found by name in " + Scenario.Folder);
            Scenario.LoadFromConsole(scenario); // the F1 console's Load button
            var deadline = Time.realtimeSinceStartup + 30f;
            while ((Scenario.Loading || Runner == null) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsNull(Scenario.LastError, Scenario.LastError);
            Assert.IsNotNull(Runner, "loaded");
            Assert.AreSame(scenario, Runner.Config);
            CollectionAssert.IsEmpty(Runner.Errors);
            Assert.AreEqual(scenario.clockSeconds, RunManager.Instance.ElapsedSeconds, 1f);
            Assert.AreEqual(1, Runner.Spawned.Count);
            // The JSON round trip the console's paste box uses.
            var copy = Scenario.FromJson(Scenario.ToJson(scenario));
            Assert.AreEqual(scenario.seed, copy.seed);
            Assert.AreEqual(scenario.enemies[0].enemyId, copy.enemies[0].enemyId);
            Assert.AreEqual(scenario.enemies[0].focus, copy.enemies[0].focus);
            Object.Destroy(copy);
        }

        [UnityTest]
        public IEnumerator PvpBot_DraftPlaysToTheEnd_OverTheFakeRelay()
        {
            yield return Load("pvp_bot_draft_to_end");
            Assert.IsTrue(Game.IsPvp, "a 1v1 run");
            var draft = Runner.Config.pvp;
            var progression = Game.Build.Progression;
            CollectionAssert.AreEqual(draft.weapons, progression.OwnedWeapons, "the drafted weapons, in priority order");
            CollectionAssert.AreEqual(draft.logbooks, progression.OwnedLogbooks, "the drafted Logbooks");
            var match = Game.PvpMatch;
            Assert.IsTrue(match.IsBotMatch, "against the practice bot, simulated here");
            var deadline = Time.time + draft.countdownSeconds + 3f;
            while (!match.Started && Time.time < deadline) yield return null;
            Assert.IsTrue(match.Started, "the countdown ran out");
            deadline = Time.time + draft.matchSeconds + 5f;
            while (!match.Ended && Time.time < deadline) yield return null;
            var relay = Runner.Relay;
            Debug.Log($"[V5 Scenario pvp] ended: {relay.EndReason}, winner {relay.Winner ?? "none"}; relay got " +
                      string.Join(", ", relay.Received.Select(kv => kv.Key + " " + kv.Value)) + $"; dealt {relay.PlayerDealt:0}, taken {relay.BotDealt:0}");
            Assert.IsTrue(match.Ended, "the match ended");
            Assert.IsTrue(relay.EndReason == "time" || relay.EndReason == "sunk", "ended on time or by a sinking: " + relay.EndReason);
            Assert.IsNotNull(PvpSession.Instance.LastMatchEnd, "the session has the result");
            Assert.AreEqual(relay.Match.matchId, PvpSession.Instance.LastMatchEnd.matchId);
            Assert.Greater(relay.Received.TryGetValue("state", out var states) ? states : 0, 0, "the ship's state went through the relay");
        }
    }
}
