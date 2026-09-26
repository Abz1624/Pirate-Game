using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HighSeas.Balance;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Events;
using RoguePirates.V4.Legacy;
using RoguePirates.V4.Spawning;
using RoguePirates.Simulation.Balance;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// M3 spawn controller: semantics with in-memory fakes (grace, interval, cap retry without debt, gap clamp, boss
    /// rate, pulses, arenas, overtime), formulas and composition checked against the V3 code they replace.
    /// </summary>
    [Category("V4")]
    public sealed class V4SpawnM3Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        const string ProfilePath = "Assets/_Game/Data/Balance/SaltwindSoloStageBalanceProfile.asset";
        const float Dt = 1f / 60f;

        ConfigSet _set;
        readonly List<Object> _created = new List<Object>();

        [SetUp] public void SetUp() => _set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        // ---- Fakes ---------------------------------------------------------------------------------------------

        sealed class FakeRun : IRunStatus, IRunRandom
        {
            public float Elapsed;
            public bool Paused, Ended, BossFight;
            public int Level = 10, KillCount;
            public RunRng Rng = new RunRng(12345);
            public bool Exists => true;
            public float ElapsedSeconds => Elapsed;
            public bool IsPaused => Paused;
            public bool IsRunEnded => Ended;
            public bool IsGameplayActive => !Ended;
            public bool IsBossFight => BossFight;
            public int PlayerLevel => Level;
            public int Kills => KillCount;
            public float DirectorFloat() => Rng.DirectorFloat();
            public int DirectorInt(int min, int max) => Rng.DirectorInt(min, max);
            public float LootFloat() => Rng.LootFloat();
        }

        sealed class FakeSink : IEnemySpawnSink
        {
            struct Agent { public int Id, Def; public SpawnProfile Profile; public bool Active; public float Time; }
            readonly List<Agent> _agents = new List<Agent>();
            readonly List<(string id, bool boss, int threat)> _defs = new List<(string, bool, int)>();
            int _nextId = 1;
            public int Blockers; // extra "alive" enemies (cap tests)
            public FakeRun Run;
            public readonly List<(float t, string id, int unit)> Spawns = new List<(float, string, int)>();

            public FakeSink(ConfigSetRuntime config)
            {
                foreach (var a in config.All<EnemyArchetypeConfig>()) _defs.Add((a.enemyId, a.isBoss, a.stats.threatCost));
                foreach (var v in config.All<EnemyVariantConfig>()) _defs.Add((v.variantId, false, 1)); // aliases and registry elites
            }

            public int ActiveCount => Blockers + _agents.Count(a => a.Active);
            public int FindDefinition(string enemyId) => _defs.FindIndex(d => d.id == enemyId);
            public int IndexOf(EnemyDefinition definition) => definition == null ? -1 : FindDefinition(definition.id);
            public string DefinitionId(int index) => index >= 0 && index < _defs.Count ? _defs[index].id : null;
            public bool DefinitionIsBoss(int index) => index >= 0 && index < _defs.Count && _defs[index].boss;
            public float DefinitionRadius(int index) => 1f;
            public float DefinitionHeightOffset(int index) => 0f;
            int Add(int def, SpawnProfile profile)
            {
                _agents.Add(new Agent { Id = _nextId++, Def = def, Profile = profile, Active = true, Time = Run.Elapsed });
                Spawns.Add((Run.Elapsed, _defs[def].id, _nextId - 1));
                return _agents.Count - 1;
            }
            public int SpawnFromProfile(int definitionIndex, Vector3 position, SpawnProfile profile, int profileInstanceId, FormationRole role, int formationSlot, int leaderUnitId, float hpMultiplier, float damageMultiplier) => Add(definitionIndex, profile);
            public int Spawn(int definitionIndex, Vector3 position, ConfigEnums.EliteAffix affix, float speedMultiplier, float hpMultiplier, float damageMultiplier) => Add(definitionIndex, null);
            public int UnitIdAt(int slot) => _agents[slot].Id;
            public bool IsUnitActive(int unitId) => _agents.Any(a => a.Active && a.Id == unitId);
            public int MaxActiveUnitId() => _agents.Where(a => a.Active).Select(a => a.Id).DefaultIfEmpty(0).Max();
            public int NewestUnitAfter(int afterUnitId, int definitionIndex) => _agents.Where(a => a.Active && a.Def == definitionIndex && a.Id > afterUnitId).Select(a => a.Id).DefaultIfEmpty(-1).Max();
            public int CountActive(int definitionIndex) => _agents.Count(a => a.Active && a.Def == definitionIndex);
            public int CountActiveFromProfile(SpawnProfile profile) => _agents.Count(a => a.Active && a.Profile == profile);
            public float ThreatOfNewestAfter(int afterUnitId) => 1f;
            public void SetExperimentMultipliers(float hpMultiplier, float damageMultiplier) { }
            public void Kill(int unitId) { for (var i = 0; i < _agents.Count; i++) if (_agents[i].Id == unitId) { var a = _agents[i]; a.Active = false; _agents[i] = a; } }
            public void KillAll() { for (var i = 0; i < _agents.Count; i++) { var a = _agents[i]; a.Active = false; _agents[i] = a; } }
            public void KillAllExcept(int unitId) { for (var i = 0; i < _agents.Count; i++) { if (_agents[i].Id == unitId) continue; var a = _agents[i]; a.Active = false; _agents[i] = a; } }
        }

        sealed class FakePlacement : ISpawnPlacement
        {
            public bool Ready => true;
            public Vector3 PlayerPosition => Vector3.zero;
            public bool TryPick(SpawnPlacementSettings settings, float spawnRadius, bool allowNearPlayerFallback, out Vector3 position) { position = new Vector3(30f, 0f, 0f); return true; }
            public void OffsetGroup(SpawnGroupLayout layout, int index, int count, float spacing, ref Vector3 position) { }
            public bool IsFormationPositionValid(Vector3 position, float spawnRadius, SpawnPlacementSettings settings) => true;
        }

        sealed class FakeFeedback : ISpawnFeedback
        {
            public readonly List<string> Toasts = new List<string>();
            public readonly List<string> Telemetry = new List<string>();
            public readonly List<(int unit, int index)> Elites = new List<(int, int)>();
            void ISpawnFeedback.Toast(string text, float seconds) => Toasts.Add(text);
            void ISpawnFeedback.Telemetry(string eventName, Dictionary<string, object> fields) => Telemetry.Add(eventName);
            public void RegisterScheduledElite(int unitId, int scheduledEliteIndex) => Elites.Add((unitId, scheduledEliteIndex));
        }

        sealed class FakeStorm : IStormArena, IPlayerDamageSink
        {
            public float Scale = 1f, Damage;
            public bool Inside = true;
            public void ResetForRun() => Scale = 1f;
            public void SetSafeScale(float scale) => Scale = scale;
            public void ShowRing() { }
            public void RefreshRing() { }
            public bool IsInsideSafeZone(Vector3 position) => Inside;
            public float Depth(Vector3 position) => 5f;
            public bool Exists => true;
            public bool IsAlive => true;
            public Vector3 Position => Vector3.zero;
            public float Hits, Steady;
            void IPlayerDamageSink.Damage(float amount, Vector3 source) { Damage += amount; Hits += amount; }
            void IPlayerDamageSink.DamageOverTime(float amount, Vector3 source) { Damage += amount; Steady += amount; }
        }

        sealed class FakeProfiles : ISpawnerProfiles
        {
            readonly Dictionary<EnemySpawnerConfig, SpawnProfile> _profiles = new Dictionary<EnemySpawnerConfig, SpawnProfile>();
            readonly List<Object> _created;
            public FakeProfiles(List<Object> created) { _created = created; }
            public SpawnProfile For(EnemySpawnerConfig spawner)
            {
                if (spawner == null) return null;
                if (_profiles.TryGetValue(spawner, out var p)) return p;
                p = ScriptableObject.CreateInstance<SpawnProfile>();
                var def = ScriptableObject.CreateInstance<EnemyDefinition>();
                def.id = spawner.anchor != null ? spawner.anchor.enemyId : "shark";
                p.enemy = def;
                LegacyConfigMapping.ApplySpawner(spawner, p);
                _created.Add(p);
                _created.Add(def);
                return _profiles[spawner] = p;
            }
        }

        sealed class Harness
        {
            public ConfigSetRuntime Config;
            public FakeRun Run = new FakeRun();
            public FakeSink Sink;
            public FakeFeedback Feedback = new FakeFeedback();
            public FakeStorm Storm = new FakeStorm();
            public SpawnController Spawn = new SpawnController();
            public CombatEvents Events = new CombatEvents();
            public WaveTimelineConfig Timeline => Config.Single<WaveTimelineConfig>();

            /// <summary>One frame in V3's order: overtime slot, clock, ambient slot, storm, beats, storm edge.</summary>
            public void Frame()
            {
                Spawn.TickOvertime(Dt);
                if (!Run.Paused && !Run.Ended) Run.Elapsed += Dt;
                Spawn.TickAmbient(Dt);
                Spawn.TickStorm(Dt);
                Spawn.TickBeats(Dt);
                Spawn.TickStormEdge(Dt);
                Events.EndTick();
            }

            public void RunUntil(float seconds) { while (Run.Elapsed < seconds) Frame(); }
            public int SpawnsBetween(float from, float to) => Sink.Spawns.Count(s => s.t >= from && s.t < to);
        }

        Harness Make()
        {
            var h = new Harness { Config = new ConfigSetRuntime(_set) };
            h.Sink = new FakeSink(h.Config) { Run = h.Run };
            var context = new RunContext(new RunClock(), h.Events, h.Config);
            context.Register<IEnemySpawnSink>(h.Sink);
            context.Register<ISpawnPlacement>(new FakePlacement());
            context.Register<IRunRandom>(h.Run);
            context.Register<IRunStatus>(h.Run);
            context.Register<ISpawnFeedback>(h.Feedback);
            context.Register<IStormArena>(h.Storm);
            context.Register<IPlayerDamageSink>(h.Storm);
            context.Register<ISpawnerProfiles>(new FakeProfiles(_created));
            h.Spawn.Initialize(context);
            h.Spawn.BeginRun(new RunStart(1, false));
            return h;
        }

        // ---- Semantics -----------------------------------------------------------------------------------------

        [Test]
        public void Ambient_WaitsForTheGrace_ThenSpawnsAtTheBandRate()
        {
            var h = Make();
            using (h.Config)
            {
                h.RunUntil(1.9f);
                Assert.AreEqual(0, h.Sink.Spawns.Count, "no spawns inside the 2 s grace");
                h.RunUntil(60f);
                // Band 0 = 0.5/s (R-01): first at 2 s, then every 2 s.
                Assert.That(h.Sink.Spawns.Count, Is.InRange(28, 30));
                var first = h.Sink.Spawns[0].t;
                Assert.That(first, Is.InRange(2f, 2f + 2 * Dt));
                Assert.That(h.Sink.Spawns[1].t - first, Is.InRange(2f - Dt, 2f + 2 * Dt), "interval = 1 / rate");
            }
        }

        [Test]
        public void Ambient_AtTheCap_RetriesWithoutStoringDebt()
        {
            var h = Make();
            using (h.Config)
            {
                h.RunUntil(10f);
                h.Sink.Blockers = 1000;
                h.RunUntil(20f);
                var before = h.Sink.Spawns.Count;
                h.Sink.Blockers = 0;
                h.RunUntil(20f + h.Timeline.spawnRetryIntervalSeconds + 2 * Dt);
                Assert.AreEqual(before + 1, h.Sink.Spawns.Count, "one attempt within the retry interval after the cap frees");
                h.RunUntil(24f);
                Assert.That(h.Sink.Spawns.Count - before, Is.InRange(2, 3), "no burst of missed spawns: 10 s blocked does not queue 5 spawns");
            }
        }

        [Test]
        public void Ambient_Interval_IsClampedToTheGlobalGap()
        {
            var h = Make();
            using (h.Config)
            {
                Assert.IsTrue(h.Config.TryApplyEdit("timeline.saltwind", "phases[0].ratePerSecond", "100", 0f, out var e), e);
                h.RunUntil(10f);
                var times = h.Sink.Spawns.Select(s => s.t).ToList();
                for (var i = 1; i < times.Count; i++)
                    Assert.GreaterOrEqual(times[i] - times[i - 1], h.Timeline.globalMinSpawnGapSeconds - Dt, "gap clamp 0.35 s");
            }
        }

        [Test]
        public void BossFight_HalvesTheAmbientRate_AndPulsesRaiseIt()
        {
            var h = Make();
            using (h.Config)
            {
                h.RunUntil(5f);
                Assert.AreEqual(1f, h.Spawn.EffectiveRateMultiplier);
                h.Run.BossFight = true;
                Assert.AreEqual(h.Timeline.bossAmbientRateMultiplier, h.Spawn.EffectiveRateMultiplier);
                h.Run.BossFight = false;
                h.RunUntil(245f);
                Assert.AreEqual(1.25f, h.Spawn.EffectiveRateMultiplier, 1e-6f, "swarm pulse A (4:00, 30 s)");
                h.RunUntil(275f);
                Assert.AreEqual(1f, h.Spawn.EffectiveRateMultiplier, "pulse ended");
            }
        }

        [Test]
        public void ReefCrab_PausesAmbientUntilItDies_ThenAmbientResumesAtOnce()
        {
            var h = Make();
            using (h.Config)
            {
                h.RunUntil(148f);
                h.Sink.KillAll(); // fake enemies never die; keep the ambient cap out of the picture
                h.RunUntil(151f);
                Assert.IsTrue(h.Spawn.ArenaActive, "reef crab arena at 2:30");
                var crab = h.Sink.Spawns.Last(s => s.id == "reef_crab_miniboss");
                Assert.AreEqual(crab.unit, h.Spawn.ArenaFocusUnitId);
                CollectionAssert.Contains(h.Feedback.Telemetry, "miniboss_spawned");
                var during = h.Sink.Spawns.Count;
                Assert.Less(h.Sink.ActiveCount, 72, "below the ambient cap, so only the arena can stop spawns");
                h.RunUntil(165f);
                Assert.AreEqual(during, h.Sink.Spawns.Count, "no ambient spawns while the focus enemy lives");
                h.Sink.KillAll(); // the focus enemy dies (and the ambient cap frees: fake enemies never die on their own)
                h.Frame();
                Assert.IsFalse(h.Spawn.ArenaActive);
                Assert.AreEqual(during + 1, h.Sink.Spawns.Count, "every ambient run tries once immediately after the arena");
            }
        }

        [Test]
        public void EliteBeat_RegistersMarks_AndIsSkippedForGoodDuringTheBossFight()
        {
            var h = Make();
            using (h.Config)
            {
                h.RunUntil(151f);
                h.Sink.Kill(h.Spawn.ArenaFocusUnitId); // clear the reef crab arena
                h.RunUntil(181f);
                Assert.AreEqual(1, h.Feedback.Elites.Count, "elite hammerhead registered for Marks");
                Assert.AreEqual(0, h.Feedback.Elites[0].index);

                var boss = Make();
                using (boss.Config)
                {
                    boss.RunUntil(151f);
                    boss.Sink.Kill(boss.Spawn.ArenaFocusUnitId);
                    boss.Run.BossFight = true;
                    boss.RunUntil(185f);
                    Assert.AreEqual(PhaseState.Skipped, boss.Spawn.Phases.First(p => p.PhaseId == LegacyConfigMapping.EliteA).State);
                    CollectionAssert.Contains(boss.Feedback.Telemetry, "pressure_elite_skipped");
                    boss.Run.BossFight = false;
                    boss.RunUntil(190f);
                    Assert.IsEmpty(boss.Feedback.Elites, "a skipped elite never comes back");
                }
            }
        }

        [Test]
        public void Overtime_StopsAmbient_AndSpawnsOnItsOwnInterval()
        {
            var h = Make();
            using (h.Config)
            {
                h.Config.TryApplyEdit("timeline.saltwind", "stageDeadlineSeconds", "20", 0f, out _);
                foreach (var p in h.Timeline.phases.Where(p => p.isOvertime)) p.trigger.timeSeconds = 20f;
                h.RunUntil(20f);
                var atDeadline = h.Sink.Spawns.Count;
                h.RunUntil(40f);
                var overtime = h.Sink.Spawns.Skip(atDeadline).ToList();
                Assert.IsNotEmpty(overtime);
                CollectionAssert.Contains(h.Feedback.Telemetry, "overtime_started");
                var interval = OvertimePhase.SpawnInterval(h.Timeline, 30f);
                Assert.That(overtime.Count, Is.InRange((int)(20f / interval) - 1, (int)(20f / interval) + 1), "one enemy per overtime interval, no ambient spawns");
            }
        }

        [Test]
        public void Storm_Contracts_AndDamagesOutsideAfterTheGrace()
        {
            var h = Make();
            using (h.Config)
            {
                var storm = h.Timeline.phases.First(p => p.isStorm);
                h.RunUntil(storm.trigger.timeSeconds - storm.warning.leadSeconds + Dt);
                Assert.IsTrue(h.Feedback.Toasts.Any(t => t.Contains("10 seconds")), "V3's storm warning text from the template");
                h.Storm.Inside = false;
                h.RunUntil(800f);
                Assert.AreEqual(StormPhase.SafeScale(h.Timeline.storm, h.Run.Elapsed), h.Storm.Scale, 1e-5f);
                Assert.Greater(h.Storm.Damage, 0f);
            }
        }


        // ---- Live timeline edits, triggers, caps, lab, storm edge, Dead Calm (review findings) -----------------

        static TimelinePhase CopyPhase(TimelinePhase source, string id)
        {
            var copy = JsonUtility.FromJson<TimelinePhase>(JsonUtility.ToJson(source));
            copy.phaseId = id;
            return copy;
        }

        [Test]
        public void LiveEdit_AnAmbientPhaseAddedMidRun_StartsOnTime_AndEndsThePreviousBand()
        {
            var h = Make();
            using (h.Config)
            {
                h.RunUntil(125f);
                var bombs = CopyPhase(h.Timeline.Phase("band_2"), "bombs_only");
                bombs.label = "Bomb boats";
                bombs.trigger.timeSeconds = 140f;
                bombs.pools.Clear();
                bombs.pools.Add(new SpawnPool { poolId = "ordinary", share = 1f, entries = { new PoolEntry { enemyId = "bomb_boat", archetype = h.Config.Get<EnemyArchetypeConfig>("enemy.bomb_boat"), weight = 1f } } });
                h.Timeline.phases.Add(bombs); // appended at the end of the list, like the console's Duplicate
                h.RunUntil(139f);
                h.Sink.KillAll();
                h.RunUntil(149f);
                var after = h.Sink.Spawns.Where(s => s.t > 140.5f && s.t < 149f).ToList();
                Assert.IsNotEmpty(after, "the added phase spawns from 2:20");
                CollectionAssert.AreEqual(Enumerable.Repeat("bomb_boat", after.Count), after.Select(s => s.id), "band 1 ended when the added band began (time order, not list order)");
                Assert.AreEqual(PhaseState.Active, h.Spawn.Phases.First(p => p.PhaseId == "bombs_only").State);
                Assert.AreEqual(PhaseState.Done, h.Spawn.Phases.First(p => p.PhaseId == "band_1").State);
            }
        }

        [Test]
        public void LiveEdit_RetimingABandBeforeItStarts_KeepsTheBandsSequential()
        {
            var h = Make();
            using (h.Config)
            {
                h.Spawn.Skip("reef_crab");
                h.Spawn.Skip("elite_hammerhead");
                h.RunUntil(100f);
                Assert.IsTrue(h.Config.TryApplyEdit("timeline.saltwind", "phases[2].trigger.timeSeconds", "200", 100f, out var e), e);
                h.RunUntil(199f);
                Assert.AreEqual(PhaseState.Active, h.Spawn.Phases.First(p => p.PhaseId == "band_1").State, "band 1 runs until band 2's new start");
                Assert.AreEqual(PhaseState.Waiting, h.Spawn.Phases.First(p => p.PhaseId == "band_2").State);
                h.RunUntil(201f);
                Assert.AreEqual(PhaseState.Done, h.Spawn.Phases.First(p => p.PhaseId == "band_1").State);
                Assert.AreEqual(PhaseState.Active, h.Spawn.Phases.First(p => p.PhaseId == "band_2").State, "no gap and no overlap");
            }
        }

        [Test]
        public void ConditionTrigger_EliteWaitsForPlayerLevel()
        {
            var h = Make();
            using (h.Config)
            {
                var elite = h.Timeline.Phase(LegacyConfigMapping.EliteA);
                elite.trigger.kind = PhaseTriggerKind.Condition;
                elite.trigger.conditionKind = PhaseConditionKind.PlayerLevelAtLeast;
                elite.trigger.conditionValue = 8f;
                h.Spawn.Skip("reef_crab");
                h.Run.Level = 5;
                h.RunUntil(200f);
                Assert.IsEmpty(h.Feedback.Elites, "level 5: the elite waits");
                h.Run.Level = 8;
                h.RunUntil(201f);
                Assert.AreEqual(1, h.Feedback.Elites.Count, "level 8: the elite spawns");
            }
        }

        [Test]
        public void EventAndAfterClearedTriggers_StartPulses()
        {
            var h = Make();
            using (h.Config)
            {
                var a = h.Timeline.Phase(LegacyConfigMapping.SwarmA);
                a.trigger.kind = PhaseTriggerKind.Event;
                a.trigger.eventName = nameof(CombatEventKind.LevelUp);
                var b = h.Timeline.Phase(LegacyConfigMapping.SwarmB);
                b.trigger.kind = PhaseTriggerKind.AfterPhaseCleared;
                b.trigger.afterPhaseId = a.phaseId;
                h.RunUntil(10f);
                Assert.AreEqual(1f, h.Spawn.EffectiveRateMultiplier);
                h.Events.Publish(new CombatEvent(CombatEventKind.LevelUp, 0, 10f));
                h.RunUntil(10.5f);
                Assert.AreEqual(1.25f * 1.25f, h.Spawn.EffectiveRateMultiplier, 1e-5f, "pulse A on the event, pulse B once A is done");
            }
        }

        [Test]
        public void TriggerNowAndSkip_ControlPhasesByHand()
        {
            var h = Make();
            using (h.Config)
            {
                h.RunUntil(10f);
                Assert.IsTrue(h.Spawn.TriggerNow(LegacyConfigMapping.SwarmA));
                h.Frame();
                Assert.AreEqual(1.25f, h.Spawn.EffectiveRateMultiplier, 1e-6f);
                Assert.IsTrue(h.Spawn.Skip("band_0"));
                var before = h.Sink.Spawns.Count;
                h.RunUntil(30f);
                Assert.AreEqual(before, h.Sink.Spawns.Count, "a skipped band spawns nothing");
                Assert.AreEqual(PhaseState.Skipped, h.Spawn.Phases.First(p => p.PhaseId == "band_0").State);
                Assert.IsFalse(h.Spawn.TriggerNow("overtime"), "overtime follows the stage deadline");
            }
        }

        [Test]
        public void AmbientCap_KeepsTheReservedEncounterSlotsFree()
        {
            var h = Make();
            using (h.Config)
            {
                var cap = h.Timeline.maxActiveEnemies - h.Timeline.reservedEncounterSlots;
                Assert.AreEqual(72, cap);
                h.RunUntil(10f);
                h.Sink.KillAll();
                h.Sink.Blockers = cap;
                var before = h.Sink.Spawns.Count;
                h.RunUntil(20f);
                Assert.AreEqual(before, h.Sink.Spawns.Count, "72 alive: no ambient spawn");
                h.Sink.Blockers = cap - 1;
                h.RunUntil(30f);
                Assert.AreEqual(before + 1, h.Sink.Spawns.Count, "71 alive: exactly one more, then the cap holds");
            }
        }

        [Test]
        public void ModuleLab_RunsNoTimelineSpawnsOrPulses()
        {
            var h = Make();
            using (h.Config)
            {
                h.Spawn.LabMode = true;
                h.RunUntil(250f);
                Assert.IsEmpty(h.Sink.Spawns);
                Assert.AreEqual(1f, h.Spawn.EffectiveRateMultiplier, "no swarm pulse in the lab");
                Assert.IsEmpty(h.Feedback.Toasts.Where(t => t.StartsWith("SWARM") || t.StartsWith("WARNING")));
            }
        }

        [Test]
        public void StormEdge_WaitsForTheGraceAndTheStorm_ThenDamagesByDepth()
        {
            var h = Make();
            using (h.Config)
            {
                Assert.IsTrue(h.Config.TryApplyEdit("timeline.saltwind", $"phases[{h.Timeline.phases.FindIndex(p => p.isStorm)}].trigger.timeSeconds", "30", 0f, out var e), e);
                h.Storm.Inside = false;
                h.RunUntil(29.9f);
                Assert.AreEqual(0f, h.Storm.Damage, "no damage before the storm starts (and inside the 8 s grace)");
                h.RunUntil(30.05f);
                var start = h.Storm.Damage;
                for (var i = 0; i < 60; i++) h.Frame();
                var perFrame = h.Timeline.storm.outsideDamagePerSecond * (1f + 5f * 0.08f) * Dt; // fake depth 5
                Assert.AreEqual(60 * perFrame, h.Storm.Damage - start, 1e-3f, "8 x (1 + depth x 0.08) per second, per frame");
                Assert.AreEqual(h.Storm.Damage, h.Storm.Steady, 1e-6f, "steady damage by default (R-57): no per-frame hits");

                Assert.IsTrue(h.Config.TryApplyEdit("timeline.saltwind", "storm.damageMode", "PerFrameHit", 31f, out e), e);
                var steady = h.Storm.Steady;
                for (var i = 0; i < 10; i++) h.Frame();
                Assert.AreEqual(steady, h.Storm.Steady, "PerFrameHit (V3) uses hits instead");
                Assert.Greater(h.Storm.Hits, 0f);
            }
        }

        [Test]
        public void EliteBeat_SpawnsTheEliteVariant_FlaggedByItsSpawner()
        {
            var h = Make();
            using (h.Config)
            {
                h.Spawn.Skip("reef_crab");
                h.RunUntil(181f);
                var elite = h.Sink.Spawns.Last(s => s.t > 179f && s.id.StartsWith("elite_"));
                Assert.AreEqual("elite_hammerhead", elite.id, "R-03: the scheduled elite spawns its elite variant");
                Assert.AreEqual((elite.unit, 0), h.Feedback.Elites.Single(), "Marks register the elite unit");
                Assert.IsTrue(h.Config.Get<EnemySpawnerConfig>("spawner.saltwind_elite_hammerhead").elite.isElite, "the beat spawner flags it elite");
                Assert.AreEqual(h.Spawn.ArenaFocusUnitId, elite.unit);
            }
        }

        [Test]
        public void OvertimeSpawnRamp_RisesSmoothlyFromTheDeadline()
        {
            var h = Make();
            using (h.Config)
            {
                var t = h.Timeline;
                Assert.AreEqual(1f, OvertimePhase.SpawnStatRamp(t, 600f), 1e-5f, "1x at the deadline");
                Assert.AreEqual(1.5f, OvertimePhase.SpawnStatRamp(t, 750f), 1e-5f, "linear, evaluated per spawn (no minute steps)");
                Assert.AreEqual(1.0033f, OvertimePhase.SpawnStatRamp(t, 601f), 1e-4f);
                Assert.AreEqual(2f, OvertimePhase.SpawnStatRamp(t, 900f), 1e-5f, "2x five minutes in");
                Assert.AreEqual(2f, OvertimePhase.SpawnStatRamp(t, 1200f), 1e-5f, "then held");
                Assert.IsTrue(h.Config.TryApplyEdit("timeline.saltwind", "overtime.spawnStatRampEnd", "1", 0f, out var e), e);
                Assert.AreEqual(1f, OvertimePhase.SpawnStatRamp(h.Timeline, 900f), 1e-5f, "end 1 = V3 (the v3-parity preset)");
            }
        }

        [Test]
        public void AreaDenialSlots_AreReleasedAfterTheConfiguredHold()
        {
            var h = Make();
            using (h.Config)
            {
                SaltwindCombatEncounterBudgetReset();
                var release = new LegacyAreaDenialRelease(h.Config);
                Assert.IsTrue(LegacyAreaDenialRelease.Available);
                Assert.IsTrue(HighSeas.Combat.SaltwindCombatEncounterBudget.TryReserveAreaDenial(1));
                Assert.IsTrue(HighSeas.Combat.SaltwindCombatEncounterBudget.TryReserveAreaDenial(2));
                release.Tick(100f);
                Assert.IsFalse(HighSeas.Combat.SaltwindCombatEncounterBudget.TryReserveAreaDenial(3), "two casts active: full");
                release.Tick(101.9f);
                Assert.IsFalse(HighSeas.Combat.SaltwindCombatEncounterBudget.TryReserveAreaDenial(3), "still inside the 2 s hold");
                release.Tick(102f);
                Assert.IsTrue(HighSeas.Combat.SaltwindCombatEncounterBudget.TryReserveAreaDenial(3), "R-06: slots free again after the hold");

                SaltwindCombatEncounterBudgetReset();
                h.Config.TryApplyEdit("timeline.saltwind", "areaDenialReleaseSeconds", "0", 0f, out _);
                var v3 = new LegacyAreaDenialRelease(h.Config);
                HighSeas.Combat.SaltwindCombatEncounterBudget.TryReserveAreaDenial(1);
                HighSeas.Combat.SaltwindCombatEncounterBudget.TryReserveAreaDenial(2);
                v3.Tick(100f); v3.Tick(500f);
                Assert.IsFalse(HighSeas.Combat.SaltwindCombatEncounterBudget.TryReserveAreaDenial(3), "0 = never released (V3)");
                SaltwindCombatEncounterBudgetReset();
            }
        }

        static void SaltwindCombatEncounterBudgetReset() => HighSeas.Combat.SaltwindCombatEncounterBudget.ResetForRun();

        [Test]
        public void Overtime_IsNotHeldBackByAnArena()
        {
            var h = Make();
            using (h.Config)
            {
                h.Config.TryApplyEdit("timeline.saltwind", "stageDeadlineSeconds", "160", 0f, out _);
                foreach (var p in h.Timeline.phases.Where(p => p.isOvertime)) p.trigger.timeSeconds = 160f;
                h.RunUntil(151f);
                Assert.IsTrue(h.Spawn.ArenaActive, "the Reef Crab arena is running and its crab never dies here");
                h.RunUntil(158f);
                h.Sink.KillAllExcept(h.Spawn.ArenaFocusUnitId); // fake enemies never die on their own; keep the cap out of it
                var before = h.Sink.Spawns.Count;
                h.RunUntil(175f);
                Assert.IsTrue(h.Spawn.ArenaActive);
                Assert.Greater(h.Sink.Spawns.Count, before, "R-75: overtime spawns keep coming during an arena");
                CollectionAssert.Contains(h.Feedback.Telemetry, "overtime_started");
            }
        }

        [Test]
        public void DeadCalmProbe_ReadsTheLegacyBaseWithoutTheBossFactor()
        {
            Assert.AreEqual(0.7f, LegacyEncounterExecutor.WithoutBossFactor(0.35f, true, 0.5f), 1e-6f);
            Assert.AreEqual(0.7f, LegacyEncounterExecutor.WithoutBossFactor(0.7f, false, 0.5f), 1e-6f);
            Assert.AreEqual(0.7f, LegacyEncounterExecutor.WithoutBossFactor(0.7f, true, 0f), 1e-6f, "no division by zero");
            var go = new GameObject("dead-calm-probe");
            try
            {
                var spawner = go.AddComponent<EncounterSpawner>();
                spawner.SetSpawnRateMultiplier(0.7f); // what V3FloorPowerupWorld's Dead Calm writes
                Assert.AreEqual(0.7f, new LegacyEncounterExecutor().Multiplier, 1e-6f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ---- Formulas equal V3 ---------------------------------------------------------------------------------

        StageBalanceProfileExport V3Export()
        {
            var profile = AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(ProfilePath);
            return profile.ToExport();
        }

        [Test]
        public void OvertimeAndStormFormulas_EqualV3()
        {
            var export = V3Export();
            using (var config = new ConfigSetRuntime(_set))
            {
                var t = config.Single<WaveTimelineConfig>();
                var d = config.Single<DifficultyCurveConfig>();
                for (var time = 590f; time < 960f; time += 3.7f)
                {
                    Assert.AreEqual(SaltwindFinaleLogic.GetOvertimeSpawnInterval(time, export), OvertimePhase.SpawnInterval(t, time), $"interval @{time}");
                    Assert.AreEqual(SaltwindFinaleLogic.GetOvertimePursuerWeight(time, export), OvertimePhase.PursuerWeight(t, time), $"weight @{time}");
                    Assert.AreEqual(SaltwindFinaleLogic.GetOvertimeEnemyHpMultiplier(time, export), OvertimePhase.HpMultiplier(t, d, time), $"hp @{time}");
                    Assert.AreEqual(SaltwindFinaleLogic.GetOvertimeEnemyDamageMultiplier(time, export), OvertimePhase.DamageMultiplier(t, d, time), $"dmg @{time}");
                    Assert.AreEqual(SaltwindFinaleLogic.GetStormSafeRegionScale(time, export), StormPhase.SafeScale(t.storm, time), $"storm @{time}");
                }
            }
        }

        [Test]
        public void OrdinaryTimeMultipliers_EqualV3()
        {
            var h = Make();
            using (h.Config)
            {
                var executor = new SpawnExecutor(ServicesOf(h), null);
                for (var time = 0f; time < 900f; time += 1.3f)
                {
                    Assert.AreEqual(SaltwindFinaleLogic.GetOrdinaryTimeHpMultiplier(time), executor.OrdinaryHpMultiplier(time), $"hp @{time}");
                    Assert.AreEqual(SaltwindFinaleLogic.GetOrdinaryTimeDamageMultiplier(time), executor.OrdinaryDamageMultiplier(time), $"dmg @{time}");
                }
            }
        }

        static SpawnServices ServicesOf(Harness h)
        {
            var s = new SpawnServices();
            var context = new RunContext(new RunClock(), new CombatEvents(), h.Config);
            context.Register<IEnemySpawnSink>(h.Sink);
            context.Register<ISpawnPlacement>(new FakePlacement());
            context.Register<IRunRandom>(h.Run);
            context.Register<IRunStatus>(h.Run);
            context.Register<ISpawnFeedback>(h.Feedback);
            context.Register<IStormArena>(h.Storm);
            context.Register<IPlayerDamageSink>(h.Storm);
            context.Register<ISpawnerProfiles>(new FakeProfiles(new List<Object>()));
            s.Resolve(context);
            return s;
        }

        /// <summary>
        /// Same seed, same alive counts, same time and level: V4's pool pick returns V3's enemy on every draw and leaves
        /// the director stream in the same state (all five bands, caps hit and not hit, tiers locked and unlocked).
        /// </summary>
        [Test]
        public void Composition_PicksWhatV3Picks_DrawForDraw()
        {
            var export = V3Export();
            var h = Make();
            using (h.Config)
            {
                var services = ServicesOf(h);
                var v4Rng = new FakeRun { Rng = new RunRng(99) };
                services.Random = v4Rng;
                var composition = new SpawnComposition(services);
                var v3Rng = new RunRng(99);
                var counts = new Dictionary<string, int>();
                var chaos = new System.Random(5);
                var bands = h.Timeline.phases.Where(PhaseKinds.IsAmbient).ToList();
                Assert.AreEqual(5, bands.Count);
                for (var draw = 0; draw < 4000; draw++)
                {
                    counts["cannonboat"] = chaos.Next(0, 6);
                    counts["kraken_spawn"] = chaos.Next(0, 4);
                    counts["ram_raider"] = chaos.Next(0, 4);
                    var band = draw % 5;
                    var elapsed = (float)(chaos.NextDouble() * 600.0);
                    var level = chaos.Next(0, 9);
                    h.Sink.KillAll();
                    foreach (var pair in counts)
                        for (var n = 0; n < pair.Value; n++) h.Sink.Spawn(h.Sink.FindDefinition(pair.Key), Vector3.zero, ConfigEnums.EliteAffix.None, 1f, 1f, 1f);
                    composition.Invalidate();
                    var v3Ok = SaltwindAmbientCompositionLogic.TryPickEnemyId(band, v3Rng, id => counts.TryGetValue(id, out var c) ? c : 0, export, out var v3Id, elapsed, level);
                    var v4Id = composition.Pick(bands[band], elapsed, level);
                    Assert.AreEqual(v3Ok ? v3Id : null, v4Id, $"draw {draw}, band {band}, t={elapsed:0.0}, level {level}");
                }
                Assert.AreEqual(v3Rng.DirectorFloat(), v4Rng.DirectorFloat(), "director streams in step");
            }
        }

        // ---- Text, references, structure -----------------------------------------------------------------------

        [Test]
        public void Warnings_AreGeneratedFromTemplates_NoHardCodedTimes()
        {
            var root = Path.Combine(Application.dataPath, "_Game", "V4", "Runtime", "Spawning");
            var clock = new Regex("\"[^\"]*\\b\\d{1,2}:\\d{2}\\b[^\"]*\"");
            var offenders = Directory.GetFiles(root, "*.cs").SelectMany(f => File.ReadAllLines(f).Select((l, i) => (f, l, i)))
                .Where(x => clock.IsMatch(x.l)).Select(x => $"{Path.GetFileName(x.f)}:{x.i + 1}").ToList();
            CollectionAssert.IsEmpty(offenders, "warning texts must come from phase templates, not fixed times");
            using (var config = new ConfigSetRuntime(_set))
                foreach (var p in config.Single<WaveTimelineConfig>().phases.Where(p => p.warning.leadSeconds > 0f))
                    StringAssert.DoesNotMatch("\\d{1,2}:\\d{2}", p.warning.template, p.phaseId);
        }

        [Test]
        public void ConfigReferences_CanBeEdited_AndTravelInPresets()
        {
            using (var config = new ConfigSetRuntime(_set))
            {
                var timeline = config.Single<WaveTimelineConfig>();
                var index = timeline.phases.FindIndex(p => p.phaseId == LegacyConfigMapping.ReefCrab);
                Assert.IsTrue(config.TryApplyEdit(timeline.configId, $"phases[{index}].focus.archetype", "enemy.shark", 0f, out var error), error);
                Assert.AreSame(config.Get<EnemyArchetypeConfig>("enemy.shark"), timeline.phases[index].focus.archetype, "the run's copy, not the asset");
                Assert.IsFalse(config.TryApplyEdit(timeline.configId, $"phases[{index}].focus.archetype", "spawner.nope", 0f, out _));
                var preset = PresetCodec.Capture(config, "ref", config.Hash);
                StringAssert.Contains("enemy.shark", PresetCodec.ToJson(preset));
                using (var fresh = new ConfigSetRuntime(_set))
                {
                    Assert.IsTrue(PresetCodec.Apply(fresh, PresetCodec.FromJson(PresetCodec.ToJson(preset)), 0f, new List<string>()));
                    Assert.AreSame(fresh.Get<EnemyArchetypeConfig>("enemy.shark"), fresh.Single<WaveTimelineConfig>().phases[index].focus.archetype);
                    fresh.ResetField(timeline.configId, $"phases[{index}].focus.archetype", 0f, out _);
                    Assert.AreSame(fresh.Get<EnemyArchetypeConfig>("enemy.reef_crab_miniboss"), fresh.Single<WaveTimelineConfig>().phases[index].focus.archetype, "reset points at the copy");
                }
            }
        }

        [Test]
        public void Timeline_ScriptedPhases_CarryV3Values()
        {
            using (var config = new ConfigSetRuntime(_set))
            {
                var t = config.Single<WaveTimelineConfig>();
                var reef = t.Phase(LegacyConfigMapping.ReefCrab);
                Assert.IsTrue(reef.focus.enabled);
                Assert.AreEqual(FocusPlacement.RingAroundPlayer, reef.focus.placement);
                Assert.AreEqual("reef_crab_miniboss", reef.focus.archetype.enemyId);
                Assert.AreEqual((22f, 4f, 1.15f), (reef.focus.ringDistance, reef.focus.hpMultiplier, reef.focus.damageMultiplier));
                Assert.AreEqual(BossConflict.WaitUntilBossEnds, reef.focus.bossConflict);
                Assert.AreEqual(0f, reef.focus.retryWindowSeconds);
                var pacing = Resources.Load<DemoPacingSettings>("DemoPacingSettings");
                Assert.AreEqual(pacing.firstMinibossSeconds, reef.trigger.timeSeconds, "R-02: 150 s as V3");
                foreach (var (id, index) in new[] { (LegacyConfigMapping.EliteA, 0), (LegacyConfigMapping.EliteB, 1) })
                {
                    var elite = t.Phase(id);
                    Assert.IsTrue(elite.focus.enabled && elite.pauseAmbient, id);
                    Assert.AreEqual(index, elite.focus.scheduledEliteIndex, id);
                    Assert.AreEqual(SaltwindPressureBeatLogic.SpawnRetryWindowSeconds, elite.focus.retryWindowSeconds, id);
                }
                Assert.AreEqual(2, config.Single<DifficultyCurveConfig>().tierUnlocks.Count);
                var report = new ConfigValidationReport();
                t.Validate(report);
                CollectionAssert.IsEmpty(report.Errors.Select(e => e.ToString()));
            }
        }
    }
}
