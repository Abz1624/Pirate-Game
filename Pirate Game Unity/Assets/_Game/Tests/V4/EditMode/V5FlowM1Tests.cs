using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RoguePirates.Simulation.Balance;
using RoguePirates.V4.Config;
using RoguePirates.V4.Events;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.Enemies;
using RoguePirates.V4.Progression;
using RoguePirates.V4.Spawning;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M1 enemy flow: the minimum-alive floor, minute waves, hordes, release windows and the pickup merge, each with
    /// in-memory fakes and targeted clock jumps (no whole-run traces). Off with the flow's switch.
    /// </summary>
    [Category("V5")]
    public sealed class V5FlowM1Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
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

        // ---- Fakes (same shape as the M3 spawn tests) ------------------------------------------------------------

        sealed class FakeRun : IRunStatus, IRunRandom
        {
            public float Elapsed;
            public bool Paused, Ended, BossFight;
            public RunRng Rng = new RunRng(4242);
            public bool Exists => true;
            public float ElapsedSeconds => Elapsed;
            public bool IsPaused => Paused;
            public bool IsRunEnded => Ended;
            public bool IsGameplayActive => !Ended;
            public bool IsBossFight => BossFight;
            public int PlayerLevel => 10;
            public int Kills => 0;
            public float DirectorFloat() => Rng.DirectorFloat();
            public int DirectorInt(int min, int max) => Rng.DirectorInt(min, max);
            public float LootFloat() => Rng.LootFloat();
        }

        sealed class FakeSink : IEnemySpawnSink
        {
            struct Agent { public int Id, Def; public SpawnProfile Profile; public bool Active; }
            readonly List<Agent> _agents = new List<Agent>();
            readonly List<(string id, bool boss)> _defs = new List<(string, bool)>();
            int _nextId = 1;
            public int Blockers;
            public FakeRun Run;
            public readonly List<(float t, string id, int unit, Vector3 pos, bool viaProfile)> Spawns = new List<(float, string, int, Vector3, bool)>();

            public FakeSink(ConfigSetRuntime config)
            {
                foreach (var a in config.All<EnemyArchetypeConfig>()) _defs.Add((a.enemyId, a.isBoss));
                foreach (var v in config.All<EnemyVariantConfig>()) _defs.Add((v.variantId, false));
            }

            public int ActiveCount => Blockers + _agents.Count(a => a.Active);
            public int FindDefinition(string enemyId) => _defs.FindIndex(d => d.id == enemyId);
            public int IndexOf(EnemyDefinition definition) => definition == null ? -1 : FindDefinition(definition.id);
            public string DefinitionId(int index) => index >= 0 && index < _defs.Count ? _defs[index].id : null;
            public bool DefinitionIsBoss(int index) => index >= 0 && index < _defs.Count && _defs[index].boss;
            public float DefinitionRadius(int index) => 1f;
            public float DefinitionHeightOffset(int index) => 0f;
            int Add(int def, SpawnProfile profile, Vector3 pos)
            {
                _agents.Add(new Agent { Id = _nextId++, Def = def, Profile = profile, Active = true });
                Spawns.Add((Run.Elapsed, _defs[def].id, _nextId - 1, pos, profile != null));
                return _agents.Count - 1;
            }
            public int SpawnFromProfile(int definitionIndex, Vector3 position, SpawnProfile profile, int profileInstanceId, FormationRole role, int formationSlot, int leaderUnitId, float hpMultiplier, float damageMultiplier) => Add(definitionIndex, profile, position);
            public void MarkAdaptiveElite(int slot, SpawnProfile sourceProfile, bool ordinaryRewards) { }
            public int Spawn(int definitionIndex, Vector3 position, ConfigEnums.EliteAffix affix, float speedMultiplier, float hpMultiplier, float damageMultiplier) => Add(definitionIndex, null, position);
            public int UnitIdAt(int slot) => _agents[slot].Id;
            public bool IsUnitActive(int unitId) => _agents.Any(a => a.Active && a.Id == unitId);
            public int MaxActiveUnitId() => _agents.Where(a => a.Active).Select(a => a.Id).DefaultIfEmpty(0).Max();
            public int NewestUnitAfter(int afterUnitId, int definitionIndex) => _agents.Where(a => a.Active && a.Def == definitionIndex && a.Id > afterUnitId).Select(a => a.Id).DefaultIfEmpty(-1).Max();
            public int CountActive(int definitionIndex) => _agents.Count(a => a.Active && a.Def == definitionIndex);
            public int CountActiveFromProfile(SpawnProfile profile) => _agents.Count(a => a.Active && a.Profile == profile);
            public float ThreatOfNewestAfter(int afterUnitId) => 1f;
            public void SetExperimentMultipliers(float hpMultiplier, float damageMultiplier) { }
            public void KillAll() { for (var i = 0; i < _agents.Count; i++) { var a = _agents[i]; a.Active = false; _agents[i] = a; } }
            public int Alive => _agents.Count(a => a.Active);
        }

        sealed class FakePlacement : ISpawnPlacement
        {
            public bool Ready => true;
            public Vector3 PlayerPosition => Vector3.zero;
            public bool TryPick(SpawnPlacementSettings settings, float spawnRadius, bool allowNearPlayerFallback, out Vector3 position) { position = new Vector3(30f, 0f, 0f); return true; }
            public void OffsetGroup(SpawnGroupLayout layout, int index, int count, float spacing, ref Vector3 position) { }
            public bool IsFormationPositionValid(Vector3 position, float spawnRadius, SpawnPlacementSettings settings) => true;
            public float ViewEdgeDistance => 0f;
        }

        sealed class FakeFeedback : ISpawnFeedback
        {
            public readonly List<string> Toasts = new List<string>();
            public readonly List<string> Telemetry = new List<string>();
            void ISpawnFeedback.Toast(string text, float seconds) => Toasts.Add(text);
            void ISpawnFeedback.Telemetry(string eventName, Dictionary<string, object> fields) => Telemetry.Add(eventName);
        }

        sealed class FakeStorm : IStormArena, IPlayerDamageSink
        {
            public void ResetForRun() { }
            public void SetSafeScale(float scale) { }
            public void ShowRing() { }
            public void RefreshRing() { }
            public bool IsInsideSafeZone(Vector3 position) => true;
            public float Depth(Vector3 position) => 0f;
            public bool IsInsideBounds(Vector3 position) => true;
            public float BoundsDepth(Vector3 position) => 0f;
            public bool Exists => true;
            public bool IsAlive => true;
            public Vector3 Position => Vector3.zero;
            void IPlayerDamageSink.Damage(float amount, Vector3 source) { }
            void IPlayerDamageSink.DamageOverTime(float amount, Vector3 source) { }
        }

        sealed class FakeProfiles : ISpawnerProfiles
        {
            readonly Dictionary<EnemySpawnerConfig, SpawnProfile> _profiles = new Dictionary<EnemySpawnerConfig, SpawnProfile>();
            readonly List<Object> _created;
            readonly SpawnerProfiles _elites = new SpawnerProfiles(null);
            public FakeProfiles(List<Object> created) { _created = created; }
            public SpawnProfile EliteCopy(SpawnProfile source, DirectorConfig d) => _elites.EliteCopy(source, d);
            public SpawnProfile For(EnemySpawnerConfig spawner)
            {
                if (spawner == null) return null;
                if (_profiles.TryGetValue(spawner, out var p)) return p;
                p = ScriptableObject.CreateInstance<SpawnProfile>();
                var def = ScriptableObject.CreateInstance<EnemyDefinition>();
                def.id = spawner.anchor != null ? spawner.anchor.enemyId : "shark";
                p.enemy = def;
                SpawnerProfiles.Apply(spawner, p);
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
            public SpawnController Spawn = new SpawnController();
            public CombatEvents Events = new CombatEvents();
            public RunContext Context;
            public WaveTimelineConfig Timeline => Config.Single<WaveTimelineConfig>();
            public IFlowStatus Flow => Spawn;

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

            /// <summary>Jumps the run clock (no spawning in between), like the console's jump-to-time.</summary>
            public void JumpTo(float seconds) => Run.Elapsed = seconds;
        }

        /// <param name="flowOff">The flow's switch off (V5 M8z, 7 Oct, I8z.5: was the v3-parity preset, retired with D18).</param>
        Harness Make(bool flowOff = false)
        {
            var h = new Harness { Config = new ConfigSetRuntime(_set) };
            if (flowOff) h.Config.Single<WaveTimelineConfig>().flow.enabled = false;
            h.Sink = new FakeSink(h.Config) { Run = h.Run };
            h.Context = new RunContext(new RunClock(), h.Events, h.Config);
            h.Context.Register<IEnemySpawnSink>(h.Sink);
            h.Context.Register<ISpawnPlacement>(new FakePlacement());
            h.Context.Register<IRunRandom>(h.Run);
            h.Context.Register<IRunStatus>(h.Run);
            h.Context.Register<ISpawnFeedback>(h.Feedback);
            var storm = new FakeStorm();
            h.Context.Register<IStormArena>(storm);
            h.Context.Register<IPlayerDamageSink>(storm);
            h.Context.Register<ISpawnerProfiles>(new FakeProfiles(_created));
            h.Spawn.Initialize(h.Context);
            h.Spawn.BeginRun(new RunStart(1, false));
            return h;
        }

        /// <summary>Enemies never die in the fake world, so a scripted arena would pause ambient spawning for good.</summary>
        static void NoArenas(Harness h)
        {
            foreach (var p in h.Timeline.phases) if (p.focus != null && p.focus.enabled) h.Spawn.Skip(p.phaseId);
        }

        int AmbientCap(Harness h) => h.Timeline.maxActiveEnemies - h.Timeline.reservedEncounterSlots;

        /// <summary>
        /// The first time after the first minute when no horde's lull or release window touches the next
        /// <paramref name="seconds"/> and the floor is above zero (V5 M8z, 7 Oct, I8z.5: read from the data, not fixed times).
        /// </summary>
        static float QuietTime(Harness h, float seconds)
        {
            var flow = h.Timeline.flow;
            var r = flow.release;
            for (var t = 61f; t < h.Timeline.stageDeadlineSeconds - seconds; t += 1f)
            {
                var clear = flow.hordes.All(x => t + seconds < x.timeSeconds - r.preHordeLullSeconds - 1f
                    || t > x.timeSeconds + r.hordeReleaseDelaySeconds + r.afterHordeSeconds + 1f);
                if (clear && Now(h, t).share > 0.05f) return t;
            }
            Assert.Fail("no quiet stretch between the hordes");
            return 0f;
        }

        /// <summary>The flow's floor share and rate now (V5 M3d: between keyframes when smoothWaves is on).</summary>
        static (float share, float rate) Now(Harness h, float? at = null)
        {
            h.Timeline.flow.Sample(at ?? h.Run.Elapsed, out var share, out var rate);
            return (share, rate);
        }

        // ---- Data ----------------------------------------------------------------------------------------------

        [Test]
        public void DefaultFlow_IsOn_KeyframesTo10Minutes_WithAJaggedFloor()
        {
            // V5 M3d (owner's sketch): keyframes from 0:00 to 10:00 the floor moves between in a line (M1: one step a minute).
            using var config = new ConfigSetRuntime(_set);
            var flow = config.Single<WaveTimelineConfig>().flow;
            Assert.IsTrue(flow.enabled);
            Assert.IsTrue(flow.smoothWaves);
            Assert.GreaterOrEqual(flow.waves.Count, 10, "at least a keyframe a minute");
            Assert.AreEqual(0f, flow.waves[0].startSeconds);
            Assert.GreaterOrEqual(flow.waves[flow.waves.Count - 1].startSeconds, 600f, "keyframes to the deadline");
            for (var i = 1; i < flow.waves.Count; i++) Assert.Greater(flow.waves[i].startSeconds, flow.waves[i - 1].startSeconds, "in time order");
            var drops = 0;
            for (var i = 1; i < flow.waves.Count; i++) if (flow.waves[i].minAliveShare < flow.waves[i - 1].minAliveShare) drops++;
            Assert.GreaterOrEqual(drops, 2, "the floor falls back after peaks (tension and release), it is not a ramp");
            Assert.Greater(flow.waves[flow.waves.Count - 1].minAliveShare, flow.waves[0].minAliveShare * 4f, "the crowd grows over the stage");
            Assert.IsTrue(flow.hordes.All(x => x.archetype != null), "every horde has an enemy");
            var report = new ConfigValidationReport();
            config.Single<WaveTimelineConfig>().Validate(report);
            Assert.IsEmpty(report.Errors.ToList(), string.Join("\n", report.Errors));
        }

        [Test]
        public void FlowSwitchedOff_RunsNoFloorHordesOrWaves()
        {
            var h = Make(flowOff: true);
            using (h.Config)
            {
                Assert.IsFalse(h.Timeline.flow.enabled);
                h.RunUntil(125f);
                Assert.IsFalse(h.Flow.FlowEnabled);
                Assert.AreEqual(0, h.Flow.ToppedUp, "no floor top-ups");
                Assert.AreEqual(0, h.Flow.HordesSpawned, "no hordes");
                Assert.AreEqual(-1, h.Flow.WaveIndex);
            }
        }

        [Test]
        public void Validation_RejectsBrokenFlowData()
        {
            using var config = new ConfigSetRuntime(_set);
            var timeline = config.Single<WaveTimelineConfig>();
            timeline.flow.waves[0].minAliveShare = 1.5f;
            timeline.flow.waves[1].topUpIntervalSeconds = 0f;
            timeline.flow.hordes[0].archetype = null;
            timeline.flow.hordes[1].hordeId = timeline.flow.hordes[2].hordeId;
            var report = new ConfigValidationReport();
            timeline.Validate(report);
            var text = string.Join("\n", report.Errors);
            StringAssert.Contains("minAliveShare", text);
            StringAssert.Contains("topUpIntervalSeconds", text);
            StringAssert.Contains("needs an archetype", text);
            StringAssert.Contains("duplicate horde id", text);
        }

        // ---- Floor ---------------------------------------------------------------------------------------------

        [Test]
        public void Floor_TopsUpToTheCurrentWave_AfterTheCrowdIsCleared()
        {
            var h = Make();
            using (h.Config)
            {
                NoArenas(h);
                // V5 M8z (7 Oct, I8z.5): a time between hordes, out of their lulls and release windows, read from the data
                // (the playtest moved the hordes; this was a fixed 2:22).
                var at = QuietTime(h, 12f);
                h.RunUntil(at);
                h.Timeline.flow.WaveAt(h.Run.Elapsed, out var index);
                Assert.AreEqual(index, h.Flow.WaveIndex);
                Assert.IsFalse(h.Flow.InRelease);
                var expected = Mathf.RoundToInt(Now(h).share * AmbientCap(h));
                Assert.Greater(expected, 0, "a floor to restore");
                Assert.AreEqual(expected, h.Flow.Floor);
                h.Sink.KillAll();
                var before = h.Flow.ToppedUp;
                h.RunUntil(at + 10f);
                Assert.GreaterOrEqual(h.Sink.Alive, Mathf.Min(expected, Mathf.RoundToInt(Now(h).share * AmbientCap(h))), "the floor is restored within ten seconds");
                Assert.Greater(h.Flow.ToppedUp, before, "the floor, not only the band rate, refilled it");
                var topUps = h.Sink.Spawns.Where(s => s.t >= at && s.viaProfile).Select(s => s.id).Distinct().ToList();
                Assert.IsNotEmpty(topUps, "top-ups come from the band's spawner and pools");
            }
        }

        [Test]
        public void Floor_NeverPassesTheAmbientCap()
        {
            var h = Make();
            using (h.Config)
            {
                NoArenas(h);
                h.Timeline.flow.waves[1].minAliveShare = 1f; // the floor asks for the whole ambient cap
                h.RunUntil(62f);
                h.Sink.KillAll();
                h.Sink.Blockers = AmbientCap(h) - 2; // two below the cap
                var before = h.Flow.ToppedUp;
                for (var i = 0; i < 600; i++)
                {
                    h.Frame();
                    Assert.LessOrEqual(h.Sink.ActiveCount, AmbientCap(h), "never above the ambient cap");
                }
                Assert.LessOrEqual(h.Flow.ToppedUp - before, 2, "only the two free slots were filled");
                Assert.AreEqual(AmbientCap(h), h.Sink.ActiveCount, "and they were filled");
            }
        }

        [Test]
        public void Floor_TopUps_KeepTheGlobalSpawnGap()
        {
            var h = Make();
            using (h.Config)
            {
                NoArenas(h);
                h.RunUntil(540.5f); // minute 9: the highest floor and six per interval
                h.Sink.KillAll();
                var gap = h.Timeline.globalMinSpawnGapSeconds;
                var last = float.NegativeInfinity;
                var previous = h.Flow.ToppedUp;
                for (var i = 0; i < 600; i++)
                {
                    h.Frame();
                    var now = h.Flow.ToppedUp;
                    Assert.LessOrEqual(now - previous, 1, "one top-up per frame at most");
                    if (now > previous)
                    {
                        Assert.GreaterOrEqual(h.Run.Elapsed - last, gap - Dt, "top-ups keep the 0.35 s gap");
                        last = h.Run.Elapsed;
                    }
                    previous = now;
                }
                Assert.Greater(h.Flow.ToppedUp, 0);
            }
        }

        [Test]
        public void Floor_TopUpsAreLimitedPerTick()
        {
            var h = Make();
            using (h.Config)
            {
                NoArenas(h);
                h.RunUntil(420.5f); // minute 7: the high floor
                h.Sink.KillAll();
                var wave = h.Timeline.flow.WaveAt(h.Run.Elapsed, out _);
                var before = h.Flow.ToppedUp;
                h.RunUntil(420.5f + wave.topUpIntervalSeconds * 0.99f);
                Assert.LessOrEqual(h.Flow.ToppedUp - before, wave.maxTopUpPerTick, "at most maxTopUpPerTick in one interval");
            }
        }

        [Test]
        public void MinuteWaves_ScaleTheBandRate()
        {
            var h = Make();
            using (h.Config)
            {
                NoArenas(h);
                h.RunUntil(165f); // between keyframes, out of the hordes' release windows and lulls
                var other = h.Spawn.EffectiveRateMultiplier / Now(h).rate;
                h.Timeline.flow.enabled = false;
                Assert.AreEqual(other, h.Spawn.EffectiveRateMultiplier, 1e-4f, "flow off removes exactly the wave's multiplier");
            }
        }

        [Test]
        public void BossFight_LowersTheFloor()
        {
            var h = Make();
            using (h.Config)
            {
                NoArenas(h);
                h.RunUntil(305f);
                var normal = h.Flow.Floor;
                h.Run.BossFight = true;
                h.Frame();
                Assert.AreEqual(Mathf.RoundToInt(Now(h).share * h.Timeline.flow.release.bossFightFloorMultiplier * AmbientCap(h)), h.Flow.Floor);
                Assert.Less(h.Flow.Floor, normal);
            }
        }

        // ---- Hordes --------------------------------------------------------------------------------------------

        [Test]
        public void Horde_IsWarned_ThenArrivesAsOneFormation()
        {
            var h = Make();
            using (h.Config)
            {
                NoArenas(h);
                var horde = h.Timeline.flow.hordes[0];
                h.RunUntil(horde.timeSeconds - 1f);
                Assert.That(h.Feedback.Toasts, Has.Some.EqualTo(horde.warningToast), "warned before it arrives");
                h.RunUntil(horde.timeSeconds + 0.1f);
                var members = h.Sink.Spawns.Where(s => !s.viaProfile && s.t >= horde.timeSeconds && s.id == horde.archetype.enemyId).ToList();
                Assert.AreEqual(horde.count, members.Count, "every member spawned");
                foreach (var m in members) Assert.AreEqual(horde.distance, new Vector2(m.pos.x, m.pos.z).magnitude, 0.01f, "a ring around the ship");
                Assert.AreEqual(1, h.Flow.HordesSpawned);
                h.RunUntil(horde.timeSeconds + 5f);
                Assert.AreEqual(horde.count, h.Sink.Spawns.Count(s => !s.viaProfile && s.t >= horde.timeSeconds && s.id == horde.archetype.enemyId), "only once");
            }
        }

        [Test]
        public void Horde_IsSkippedDuringTheBossFight()
        {
            var h = Make();
            using (h.Config)
            {
                NoArenas(h);
                var horde = h.Timeline.flow.hordes[1];
                h.JumpTo(horde.timeSeconds - 0.5f);
                h.Run.BossFight = true;
                h.RunUntil(horde.timeSeconds + 1f);
                Assert.AreEqual(0, h.Flow.HordesSpawned);
                Assert.That(h.Feedback.Telemetry, Has.Member("horde_skipped"));
                h.Run.BossFight = false;
                h.RunUntil(horde.timeSeconds + 5f);
                Assert.AreEqual(0, h.Flow.HordesSpawned, "a skipped horde does not come back");
            }
        }

        [Test]
        public void HordeFormations_HaveTheirShape()
        {
            var line = new HordeEvent { formation = HordeFormation.Line, count = 5, distance = 20f, spacing = 2f };
            var positions = Enumerable.Range(0, 5).Select(i => FlowDirector.Offset(line, i, 0f)).ToList();
            foreach (var p in positions) Assert.AreEqual(20f, p.x, 1e-4f, "a wall at the distance, across the approach");
            Assert.AreEqual(8f, positions[4].z - positions[0].z, 1e-4f, "spaced side by side");

            var cluster = new HordeEvent { formation = HordeFormation.Cluster, count = 16, distance = 20f, spacing = 2f };
            var centre = new Vector3(20f, 0f, 0f);
            foreach (var i in Enumerable.Range(0, 16))
                Assert.LessOrEqual((FlowDirector.Offset(cluster, i, 0f) - centre).magnitude, 2f * 0.75f * 4f + 1e-3f, "a tight pack");
        }

        // ---- Release -------------------------------------------------------------------------------------------

        [Test]
        public void Release_OpensAfterTheBossIsSunk_AndCutsRateAndFloor()
        {
            var h = Make();
            using (h.Config)
            {
                NoArenas(h);
                h.RunUntil(400f);
                var rateBefore = h.Spawn.EffectiveRateMultiplier;
                var floorBefore = h.Flow.Floor;
                h.Events.Publish(new CombatEvent(CombatEventKind.BossState, 0, h.Run.Elapsed, 2, 1, 0f, 0f, 0f, null, 0));
                h.Events.EndTick();
                h.Frame();
                Assert.IsTrue(h.Flow.InRelease);
                var release = h.Timeline.flow.release;
                Assert.AreEqual(rateBefore * release.rateMultiplier, h.Spawn.EffectiveRateMultiplier, 1e-4f);
                Assert.Less(h.Flow.Floor, floorBefore);
                h.RunUntil(400f + release.afterBossKillSeconds + 0.5f);
                Assert.IsFalse(h.Flow.InRelease, "then the minute's peak resumes");
            }
        }

        [Test]
        public void Release_OpensWhenAnArenaEnds()
        {
            var h = Make();
            using (h.Config)
            {
                var crab = h.Timeline.Phase("reef_crab");
                h.RunUntil(crab.trigger.timeSeconds + 1f);
                Assert.IsTrue(h.Spawn.ArenaActive, "the Reef Crab's arena is on");
                Assert.IsFalse(h.Flow.InRelease);
                h.Sink.KillAll();
                h.Frame();
                h.Frame();
                Assert.IsFalse(h.Spawn.ArenaActive);
                Assert.IsTrue(h.Flow.InRelease, "breathing room after the miniboss");
            }
        }

        [Test]
        public void Release_FollowsAHorde_AfterItsDelay()
        {
            var h = Make();
            using (h.Config)
            {
                NoArenas(h);
                var horde = h.Timeline.flow.hordes[1];
                var release = h.Timeline.flow.release;
                h.JumpTo(horde.timeSeconds - 0.1f);
                h.RunUntil(horde.timeSeconds + 0.1f);
                Assert.AreEqual(1, h.Flow.HordesSpawned);
                Assert.IsFalse(h.Flow.InRelease, "not yet: the horde is the tension");
                h.RunUntil(horde.timeSeconds + release.hordeReleaseDelaySeconds + 0.2f);
                Assert.IsTrue(h.Flow.InRelease);
            }
        }

        // ---- Pickups -------------------------------------------------------------------------------------------

        sealed class Rewards : IPickupRewards
        {
            public bool RunEnded => false;
            public float XpGain => 1f;
            public void GrantXp(int amount) { }
            public void GrantGold(int amount) { }
            public void HealShip(int amount) { }
            public bool RollHealthDrop(float chance) => false;
            public void PlaySound(string id) { }
        }

        sealed class Views : IPickupViews
        {
            public readonly Dictionary<object, int> Values = new Dictionary<object, int>();
            public object Acquire(PickupType type, Vector3 position, int value) { var v = new object(); Values[v] = value; return v; }
            public Vector3 AlignBarrel(object view) => Vector3.zero;
            public void BeginHoming(object view, PickupType type) { }
            public void Update(object view, PickupType type, Vector3 position, bool homing, float dt) { }
            public void Release(object view, PickupType type, int value) => Values.Remove(view);
        }

        static int TotalXp(PickupModule m)
        {
            var total = 0;
            for (var i = 0; i < m.Capacity; i++)
                if (m.TryGet(i, out var type, out var value, out _, out _) && type == PickupType.Infamy) total += value;
            return total;
        }

        [Test]
        public void PickupsFull_XpMergesIntoTheNearest_GoldKeepsTheV4Rule()
        {
            using var config = new ConfigSetRuntime(_set);
            Assert.IsTrue(config.Single<PickupConfig>().mergeIntoNearestWhenFull);
            var views = new Views();
            var m = new PickupModule(() => config.Single<PickupConfig>(), 2, new Rewards(), views, () => false, () => 1f);
            m.Seed(1);
            m.Spawn(PickupType.Infamy, 3, new Vector3(0f, 0f, 0f));
            m.Spawn(PickupType.Gold, 4, new Vector3(50f, 0f, 0f));
            m.Spawn(PickupType.Infamy, 5, new Vector3(45f, 0f, 0f)); // full, no XP within mergeRadius
            Assert.AreEqual(8, TotalXp(m), "no XP lost");
            Assert.That(views.Values.Values, Has.Member(8), "the merged pickup's view shows its new value");
            Assert.AreEqual(2, views.Values.Count, "the old view went back to the pool");
            m.Spawn(PickupType.Gold, 6, new Vector3(-80f, 0f, 0f)); // gold: nothing within mergeRadius, lost as in V4
            var gold = 0;
            for (var i = 0; i < m.Capacity; i++) if (m.TryGet(i, out var t, out var v, out _, out _) && t == PickupType.Gold) gold += v;
            Assert.AreEqual(4, gold, "only XP merges into the nearest");

            config.Single<PickupConfig>().mergeIntoNearestWhenFull = false;
            m.Spawn(PickupType.Infamy, 6, new Vector3(-80f, 0f, 0f));
            Assert.AreEqual(8, TotalXp(m), "switched off: the V4 behaviour (dropped) is kept");
        }

        // ---- Review round: hordes at the cap, lull, holds, late skip, warnings, overlapping releases --------------

        [Test]
        public void Horde_AtTheAmbientCap_StillUsesHalfTheReservedSlots()
        {
            var h = Make();
            using (h.Config)
            {
                NoArenas(h);
                var horde = h.Timeline.flow.hordes.OrderByDescending(x => x.count).First(); // V5 M8z: the largest, so the slots limit it
                h.JumpTo(horde.timeSeconds - 0.5f);
                h.Sink.Blockers = AmbientCap(h);
                h.RunUntil(horde.timeSeconds + 0.2f);
                var members = h.Sink.Spawns.Count(x => !x.viaProfile && x.id == horde.archetype.enemyId && x.t >= horde.timeSeconds);
                // V5 M8z (7 Oct, I8z.5): a horde smaller than half the reserved slots fits whole (the playtest's are).
                Assert.AreEqual(Mathf.Min(horde.count, h.Timeline.reservedEncounterSlots / 2), members, "room for part of the horde, the Beacon keeps the rest");
                Assert.AreEqual(1, h.Flow.HordesSpawned);
            }
        }

        [Test]
        public void Horde_ThatCannotSpawn_IsNotCounted()
        {
            var h = Make();
            using (h.Config)
            {
                NoArenas(h);
                var horde = h.Timeline.flow.hordes[1];
                h.JumpTo(horde.timeSeconds - 0.5f);
                h.Sink.Blockers = 500; // every cap full
                h.RunUntil(horde.timeSeconds + h.Timeline.flow.release.hordeReleaseDelaySeconds + 1f);
                Assert.AreEqual(0, h.Flow.HordesSpawned);
                Assert.That(h.Feedback.Telemetry, Has.Member("horde_empty"));
                Assert.IsFalse(h.Flow.InRelease, "an empty horde opens no release window");
            }
        }

        [Test]
        public void Lull_BeforeAHorde_LowersRateAndFloor()
        {
            var h = Make();
            using (h.Config)
            {
                NoArenas(h);
                var horde = h.Timeline.flow.hordes[1];
                var release = h.Timeline.flow.release;
                h.JumpTo(horde.timeSeconds - release.preHordeLullSeconds - 2f);
                h.Frame();
                var other = h.Spawn.EffectiveRateMultiplier / Now(h).rate; // all but the flow's own rate
                h.JumpTo(horde.timeSeconds - release.preHordeLullSeconds / 2f);
                h.Frame();
                var (share, rate) = Now(h);
                Assert.AreEqual(other * rate * release.rateMultiplier, h.Spawn.EffectiveRateMultiplier, 1e-4f);
                Assert.Less(h.Flow.Floor, Mathf.RoundToInt(share * AmbientCap(h)), "the lull lowers the floor below the keyframes' line");
            }
        }

        [Test]
        public void Horde_WaitsForAnOpenRelease_ThenArrives()
        {
            var h = Make();
            using (h.Config)
            {
                NoArenas(h);
                var horde = h.Timeline.flow.hordes[1];
                h.JumpTo(horde.timeSeconds - 1f);
                h.Events.Publish(new CombatEvent(CombatEventKind.BossState, 0, h.Run.Elapsed, 2, 1, 0f, 0f, 0f, null, 0)); // boss sunk: 25 s release
                h.Events.EndTick();
                h.RunUntil(horde.timeSeconds + 5f);
                Assert.AreEqual(0, h.Flow.HordesSpawned, "held during the release");
                h.RunUntil(horde.timeSeconds - 1f + h.Timeline.flow.release.afterBossKillSeconds + 1f);
                Assert.AreEqual(1, h.Flow.HordesSpawned, "arrives when the breathing room ends");
            }
        }

        [Test]
        public void Horde_MoreThan30sLate_IsSkipped()
        {
            var h = Make();
            using (h.Config)
            {
                NoArenas(h);
                var horde = h.Timeline.flow.hordes[1];
                h.JumpTo(horde.timeSeconds + 31f);
                h.Frame();
                Assert.AreEqual(0, h.Flow.HordesSpawned);
                Assert.That(h.Feedback.Telemetry, Has.Member("horde_skipped"));
            }
        }

        [Test]
        public void HordeWarning_CarriesItsDirection_ForTheEdgeMarker()
        {
            var h = Make();
            using (h.Config)
            {
                NoArenas(h);
                var horde = h.Timeline.flow.hordes[0];
                var warnings = new List<CombatEvent>();
                h.Events.TickPublished += batch => { foreach (var e in batch) if (e.Kind == CombatEventKind.Warning && (e.Flags & CombatEventFlags.HordeWarning) != 0) warnings.Add(e); };
                h.RunUntil(horde.timeSeconds + 0.1f);
                Assert.AreEqual(1, warnings.Count);
                Assert.AreEqual(horde.distance, new Vector2(warnings[0].X, warnings[0].Z).magnitude, 0.01f, "the marker points where the horde forms");
                var first = h.Sink.Spawns.First(x => !x.viaProfile && x.t >= horde.timeSeconds && x.id == horde.archetype.enemyId);
                Assert.AreEqual(warnings[0].X, first.pos.x, 0.01f, "the ring's first member is where the marker pointed");
                Assert.AreEqual(warnings[0].Z, first.pos.z, 0.01f);
            }
        }

        [Test]
        public void ReleaseWindows_Overlap_ToTheLatestEnd()
        {
            var h = Make();
            using (h.Config)
            {
                var crab = h.Timeline.Phase("reef_crab");
                h.RunUntil(crab.trigger.timeSeconds + 1f);
                Assert.IsTrue(h.Spawn.ArenaActive);
                var release = h.Timeline.flow.release;
                h.Events.Publish(new CombatEvent(CombatEventKind.BossState, 0, h.Run.Elapsed, 2, 1, 0f, 0f, 0f, null, 0));
                h.Events.EndTick();
                var bossEnd = h.Run.Elapsed + release.afterBossKillSeconds;
                h.RunUntil(h.Run.Elapsed + 10f);
                h.Sink.KillAll(); // the arena ends 10 s later: its 20 s window ends after the boss's
                h.Frame();
                var arenaEnd = h.Run.Elapsed + release.afterArenaSeconds;
                Assert.Greater(arenaEnd, bossEnd);
                h.RunUntil(bossEnd + 1f);
                Assert.IsTrue(h.Flow.InRelease, "still open: the later window wins");
                h.RunUntil(arenaEnd + 0.5f);
                Assert.IsFalse(h.Flow.InRelease);
            }
        }

        [Test]
        public void Status_ReadsNotRunning_DuringAnArena()
        {
            var h = Make();
            using (h.Config)
            {
                var crab = h.Timeline.Phase("reef_crab");
                h.RunUntil(crab.trigger.timeSeconds + 1f);
                Assert.IsTrue(h.Spawn.ArenaActive);
                Assert.AreEqual(-1, h.Flow.WaveIndex);
                Assert.AreEqual(0, h.Flow.Floor);
            }
        }
    }
}
