using System.Collections;
using System.Linq;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Enemies;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Foundation.POI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoguePirates.V4.Tests
{
    /// <summary>Swaps the save for an in-memory one and restores the PlayerPrefs a V4 run can touch.</summary>
    sealed class V4TestSaveIsolation : System.IDisposable
    {
        static readonly string[] IntKeys = { "v3_home_seed", "v3_first_conquest_complete", "v3_saltwind_runs_completed", "v3_selected_captain", "combat_captain_actives_enabled", "v4_lifetime_gold", "v4_tutorial_progress" };
        readonly SaveService _original;
        readonly bool[] _had = new bool[IntKeys.Length];
        readonly int[] _values = new int[IntKeys.Length];
        const string MetaKey = "highseas_meta_v1";
        const string SnapshotKey = "highseas_run_snapshot_v1";
        const string RecordsKey = "v4_run_records"; // M11 run records
        readonly string _meta;
        readonly string _runSnapshot;
        readonly string _records;

        public V4TestSaveIsolation()
        {
            AppRoot.EnsureInitialized();
            _original = AppRoot.Instance.Save;
            _meta = PlayerPrefs.HasKey(MetaKey) ? PlayerPrefs.GetString(MetaKey) : null;
            _runSnapshot = PlayerPrefs.HasKey(SnapshotKey) ? PlayerPrefs.GetString(SnapshotKey) : null;
            _records = PlayerPrefs.HasKey(RecordsKey) ? PlayerPrefs.GetString(RecordsKey) : null;
            AppRoot.Instance.ReplaceSave(new SaveService(new MemorySaveBackend()));
            for (var i = 0; i < IntKeys.Length; i++)
            {
                _had[i] = PlayerPrefs.HasKey(IntKeys[i]);
                _values[i] = PlayerPrefs.GetInt(IntKeys[i], 0);
            }
        }

        public void Dispose()
        {
            if (AppRoot.Instance != null) AppRoot.Instance.ReplaceSave(_original);
            for (var i = 0; i < IntKeys.Length; i++)
            {
                if (_had[i]) PlayerPrefs.SetInt(IntKeys[i], _values[i]);
                else PlayerPrefs.DeleteKey(IntKeys[i]);
            }
            if (_meta != null) SaveService.RestoreSnapshot(_meta); // M16: through the save guard (never drops owned content)
            if (_runSnapshot != null) PlayerPrefs.SetString(SnapshotKey, _runSnapshot); else PlayerPrefs.DeleteKey(SnapshotKey);
            if (_records != null) PlayerPrefs.SetString(RecordsKey, _records); else PlayerPrefs.DeleteKey(RecordsKey);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Counts the RunEnd notifications a module receives.</summary>
    sealed class EndProbe : IRunModule
    {
        public int EndCount;
        public RunEnd LastEnd;
        public string Id => "test.end-probe";
        public int TickOrder => 0;
        public void Initialize(RunContext context) { }
        public void BeginRun(in RunStart start) { }
        public void Tick(in SimTick tick) { }
        public void EndRun(in RunEnd end) { EndCount++; LastEnd = end; }
    }

    [Category("V4")]
    public sealed class V4RunPlayModeTests
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
            // Leave the run scene first so nothing still running writes to the real save after it is restored.
            yield return SceneManager.LoadSceneAsync(V4SceneNames.Boot, LoadSceneMode.Single);
            yield return null;
            StageBalanceProfileContext.ResetForTests();
            _isolation?.Dispose();
            _isolation = null;
        }

        static IEnumerator WaitForPlaying()
        {
            var deadline = Time.realtimeSinceStartup + Timeout;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (RunManager.Instance != null && RunManager.Instance.State == RunState.Playing && StageBalanceProfileContext.IsActive)
                    yield break;
                yield return null;
            }
            Assert.Fail("V4 run did not reach Playing with an active stage profile.");
        }

        [UnityTest]
        public IEnumerator BootFlow_ReachesMenu_ThenRunPlays()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.Boot, LoadSceneMode.Single);
            var deadline = Time.realtimeSinceStartup + Timeout;
            while (SceneManager.GetActiveScene().name != V4SceneNames.MainMenu && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(V4SceneNames.MainMenu, SceneManager.GetActiveScene().name, "V4Boot must load V4MainMenu");

            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return WaitForPlaying();
            Assert.IsNotNull(Object.FindFirstObjectByType<V4Game>(), "V4SaltwindRun must be driven by V4Game");
        }

        [UnityTest]
        public IEnumerator LiveConfigEdits_ReachTheLegacyRun()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return WaitForPlaying();
            var game = Object.FindFirstObjectByType<V4Game>();
            // M14: the profile the shared V3 code reads is built from the V4 configs (no V3 asset clone).
            Assert.AreSame(game.Shell.StageProfile.Profile, StageBalanceProfileContext.Authoring, "the profile context must use the per-run profile");

            // Band 2 rate 1.8 -> 4 spawns/s: the V4 spawn controller reads the phase's rate live (interval 0.25 s).
            Assert.IsTrue(game.Config.TryApplyEdit("timeline.saltwind", "phases[2].ratePerSecond", "4", 1f, out var error), error);
            Assert.AreEqual(4f, game.Config.Get<RoguePirates.V4.Config.WaveTimelineConfig>("timeline.saltwind").phases[2].ratePerSecond);

            // A profile-owned field: live in Authoring and in the rebuilt snapshot; chest purchases survive the rebuild.
            StageBalanceProfileContext.Runtime.RegisterSuccessfulChestPurchase();
            Assert.IsTrue(game.Config.TryApplyEdit("timeline.saltwind", "reservedEncounterSlots", "10", 2f, out error), error);
            Assert.AreEqual(10, StageBalanceProfileContext.Authoring.reservedEncounterSlots);
            Assert.AreEqual(10, StageBalanceProfileContext.Export.Spawn.ReservedEncounterSlots);
            Assert.AreEqual(1, StageBalanceProfileContext.Runtime.SuccessfulChestPurchases);
            Assert.AreEqual(2, game.Config.ChangeLog.Count);
        }

        [UnityTest]
        public IEnumerator Preset_RequestedBeforeTheRun_AppliesFromTheStart()
        {
            var set = Resources.FindObjectsOfTypeAll<RoguePirates.V4.Config.ConfigSet>().FirstOrDefault(s => s.setId == "saltwind-default");
            if (set == null)
            {
                yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
                yield return WaitForPlaying();
                set = Resources.FindObjectsOfTypeAll<RoguePirates.V4.Config.ConfigSet>().First(s => s.setId == "saltwind-default");
            }
            string json;
            using (var runtime = new RoguePirates.V4.Config.ConfigSetRuntime(set))
            {
                runtime.TryApplyEdit("timeline.saltwind", "phases[2].ratePerSecond", "4", 0f, out _);
                json = RoguePirates.V4.Config.PresetCodec.ToJson(RoguePirates.V4.Config.PresetCodec.Capture(runtime, "band2x", runtime.Hash));
            }
            RunRequest.RequestPreset(json);
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return WaitForPlaying();
            var game = Object.FindFirstObjectByType<V4Game>();
            // The V4 spawn controller reads the band rate from the run's config copy.
            Assert.AreEqual(4f, game.Config.Get<RoguePirates.V4.Config.WaveTimelineConfig>("timeline.saltwind").phases[2].ratePerSecond, "preset must apply before the run starts");
            Assert.AreEqual(1, game.Config.ChangeLog.Count, "applied once, before the run");
            Assert.IsNull(RunRequest.PresetJson, "the preset request is consumed");
        }

        [UnityTest]
        public IEnumerator EnemyWorld_UsesTheConfigBuiltRuntimeDatabase()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return WaitForPlaying();
            var game = Object.FindFirstObjectByType<V4Game>();
            var world = V4TestEnemies.Simulation();
            Assert.AreSame(game.Shell.RuntimeDatabase.Database.enemies, world.Definitions, "the enemy world must use the runtime database");
            var shark = world.Definitions.First(d => d != null && d.id == "shark");
            Assert.AreEqual(HideFlags.DontSave, shark.hideFlags & HideFlags.DontSave, "definitions are per-run copies, not assets");
            Assert.IsTrue(game.Config.TryApplyEdit("enemy.shark", "stats.moveSpeed", "9", 1f, out var error), error);
            Assert.AreEqual(9f, shark.moveSpeed, "archetype edits reach the definition new spawns read");

            // M14: every spawner's runtime profile (built from its config) reads the per-run copy of its anchor enemy.
            game.Context.TryGet<RoguePirates.V4.Spawning.ISpawnerProfiles>(out var profiles);
            var checkedProfiles = 0;
            foreach (var spawner in game.Config.All<RoguePirates.V4.Config.EnemySpawnerConfig>())
            {
                var profile = profiles.For(spawner);
                Assert.IsNotNull(profile?.enemy, spawner.configId);
                Assert.AreSame(game.Shell.RuntimeDatabase.Database.GetEnemy(profile.enemy.id), profile.enemy, $"{spawner.configId}: every spawn path reads the per-run copy");
                checkedProfiles++;
            }
            Assert.GreaterOrEqual(checkedProfiles, 14);
        }

        [UnityTest]
        public IEnumerator FrameSteps_AllTickEveryFrame_AfterTheRunClock()
        {
            // M14.14: one driver runs the steps in FrameStep order after the run manager (execution order 10 > 0), so
            // every step sees this frame's clock (the legacy slots' order depended on scene file IDs).
            SceneManager.LoadScene(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return null;
            yield return WaitForPlaying();
            for (var i = 0; i < 5; i++) yield return null;
            yield return new WaitForEndOfFrame();
            var frame = Object.FindFirstObjectByType<V4Game>().Frame;
            var clock = RunManager.Instance.ElapsedSeconds;
            foreach (var step in new[] { FrameStep.Stage, FrameStep.Weapons, FrameStep.Ambient, FrameStep.Captain, FrameStep.Items,
                         FrameStep.Discovery, FrameStep.Interaction, FrameStep.StormEdge, FrameStep.Telemetry, FrameStep.Overtime,
                         FrameStep.Storm, FrameStep.Beats, FrameStep.OrbitPositions })
            {
                Assert.AreEqual(Time.frameCount, frame.FrameAtTick(step), step + " ticked this frame");
                Assert.AreEqual(clock, frame.ClockAtTick(step), step + " saw this frame's clock");
            }
            Assert.IsEmpty(Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(b => b.GetType().Name.StartsWith("Legacy")), "no legacy components in the run");
        }

        [UnityTest]
        public IEnumerator EliteBeat_AndOvertime_UseTheirOwnEnemies()
        {
            SceneManager.LoadScene(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return null;
            yield return WaitForPlaying();
            var game = Object.FindFirstObjectByType<V4Game>();
            var world = V4TestEnemies.Simulation();
            // R-05: the overtime spawners' runtime profiles spawn the Drowned enemies.
            game.Context.TryGet<RoguePirates.V4.Spawning.ISpawnerProfiles>(out var profiles);
            RoguePirates.V4.Config.EnemySpawnerConfig Spawner(string legacyId) => game.Config.All<RoguePirates.V4.Config.EnemySpawnerConfig>().First(s => s.legacyProfileId == legacyId);
            Assert.AreEqual("drowned_pursuer", profiles.For(Spawner("drowned_pursuer")).enemy.id);
            Assert.AreEqual("drowned_harrier", profiles.For(Spawner("drowned_harrier")).enemy.id);
            Assert.GreaterOrEqual(System.Array.FindIndex(world.Definitions, d => d != null && d.id == "drowned_pursuer"), 0, "in the run database");
            // R-03: the elite beat spawns elite_hammerhead flagged elite.
            game.Context.TryGet<RoguePirates.V4.Spawning.ISpawnController>(out var spawn);
            Assert.IsTrue(spawn.TriggerNow("elite_hammerhead"));
            var deadline = Time.realtimeSinceStartup + 10f;
            while (!spawn.ArenaActive && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(spawn.ArenaActive, "the elite spawned");
            Assert.IsTrue(world.TryGetAgentByUnitId(spawn.ArenaFocusUnitId, out var index));
            Assert.AreEqual("elite_hammerhead", world.Definitions[world.Agents[index].definitionIndex].id);
            Assert.IsTrue(world.Agents[index].isElite, "flagged elite");
        }

        [UnityTest]
        public IEnumerator Overtime_SpawnsTheDrownedEnemies()
        {
            SceneManager.LoadScene(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return null;
            yield return WaitForPlaying();
            var world = V4TestEnemies.Simulation();
            Object.FindFirstObjectByType<PlayerShipController>().Damage.SetPlaytestInvulnerable(true);
            // Jump past the stage deadline (the console's forward jump), then overtime must spawn Drowned enemies (R-05).
            var run = RunManager.Instance;
            run.JumpClock(601f);
            var deadline = Time.realtimeSinceStartup + 30f;
            var found = false;
            while (!found && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                foreach (var agent in world.Agents)
                    if (agent.active && world.Definitions[agent.definitionIndex] is var d && d != null && d.id.StartsWith("drowned_")) { found = true; break; }
            }
            Assert.IsTrue(found, "a Drowned enemy spawned in overtime");
        }

        /// <summary>M14 playtest: outside the storm's safe zone during the storm, the ship loses hull (through the frame's storm-edge step).</summary>
        [UnityTest]
        public IEnumerator Storm_OutsideTheSafeZone_DamagesTheShip()
        {
            SceneManager.LoadScene(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return null;
            yield return WaitForPlaying();
            var game = Object.FindFirstObjectByType<V4Game>();
            var run = RunManager.Instance;
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            ship.Damage.SetPlaytestInvulnerable(false);
            for (var i = 0; i < 10; i++) yield return null;
            run.JumpClock(800f);
            var deadline = Time.realtimeSinceStartup + 20f;
            while (!game.Context.Get<RoguePirates.V4.Views.IWorldView>().StormRingVisible && Time.realtimeSinceStartup < deadline) { KeepRunningStorm(); yield return null; }
            Assert.IsTrue(game.Context.Get<RoguePirates.V4.Views.IWorldView>().StormRingVisible, "the storm ring is up at 13:20");
            var outside = RoguePirates.V4.Foundation.World.PlayBounds.Center + new Vector3(RoguePirates.V4.Foundation.World.PlayBounds.HalfExtentX - 10f, 0f, 0f);
            ship.SetMovementEnabled(false);
            ship.SetWorldPosition(outside);
            yield return null;
            Assert.IsFalse(RoguePirates.V4.Foundation.World.PlayBounds.IsInsideStormSafeZone(ship.Position), "the ship is outside the safe zone");
            var hp = ship.Damage.CurrentHP;
            deadline = Time.realtimeSinceStartup + 12f; // the storm edge's 8 s grace from run start is long past
            while (Mathf.Approximately(ship.Damage.CurrentHP, hp) && Time.realtimeSinceStartup < deadline) { KeepRunningStorm(); ship.SetWorldPosition(outside); yield return null; }
            Assert.Less(ship.Damage.CurrentHP, hp, $"storm damage outside the ring (edge step ticked at frame {game.Frame.FrameAtTick(FrameStep.StormEdge)}, now {Time.frameCount}; paused={run.IsPaused})");
        }

        static void KeepRunningStorm()
        {
            if (!RunManager.Instance.IsPaused) return;
            var modal = Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Hud.LevelUpPresenter>();
            if (modal != null && modal.IsShowing) modal.Pick(0);
            else RunManager.Instance.SetPause(PauseReason.None);
        }

        [UnityTest]
        public IEnumerator SteadyStormDamage_RespectsGodMode_ThrottlesFeedback_AndCanKill()
        {
            SceneManager.LoadScene(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return null;
            yield return WaitForPlaying();
            var game = Object.FindFirstObjectByType<V4Game>();
            Assert.IsTrue(game.Context.TryGet<RoguePirates.V4.Spawning.IPlayerDamageSink>(out var sink));
            var receiver = Object.FindFirstObjectByType<PlayerShipController>().Damage;

            receiver.SetPlaytestInvulnerable(true);
            var hp = receiver.CurrentHP;
            sink.DamageOverTime(5f, Vector3.zero);
            Assert.AreEqual(hp, receiver.CurrentHP, "god mode blocks steady damage");
            receiver.SetPlaytestInvulnerable(false);

            // One small tick per frame for about a second: HP drops every frame, feedback at most every 0.5 s.
            // M14: the steady-damage report is a PlayerDamaged event flagged steady (B = 1); TriggerBus is gone.
            var reports = 0;
            void Count(System.Collections.Generic.IReadOnlyList<RoguePirates.V4.Events.CombatEvent> batch)
            {
                foreach (var e in batch) if (e.Kind == RoguePirates.V4.Events.CombatEventKind.PlayerDamaged && e.B == 1) reports++;
            }
            game.Events.TickPublished += Count;
            hp = receiver.CurrentHP;
            var start = Time.time;
            var frames = 0;
            while (Time.time - start < 1f) { sink.DamageOverTime(0.05f, Vector3.zero); frames++; yield return null; }
            game.Events.TickPublished -= Count;
            Assert.AreEqual(hp - 0.05f * frames, receiver.CurrentHP, 0.05f * frames * 0.5f, "every tick lands (captain/item multipliers aside)");
            Assert.Less(receiver.CurrentHP, hp);
            Assert.LessOrEqual(reports, 3, $"feedback throttled ({frames} ticks)");
            Assert.GreaterOrEqual(reports, 1);

            var died = false;
            receiver.Died += () => died = true;
            sink.DamageOverTime(receiver.CurrentHP + 10f, Vector3.zero);
            Assert.IsTrue(died, "a lethal tick kills through TakeDamage");
            Assert.IsFalse(receiver.IsAlive);
        }

        [UnityTest]
        public IEnumerator SkippingTheLastLevelUp_HidesTheCards()
        {
            SceneManager.LoadScene(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return null;
            yield return WaitForPlaying();
            var levels = Object.FindFirstObjectByType<V4Game>().Build.Progression;
            levels.AddXp(12);
            var deadline = Time.realtimeSinceStartup + 5f;
            while (!RunManager.Instance.IsPaused && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(RunManager.Instance.IsPaused, "level-up cards shown");
            // M14.11: the V4 level-up presenter (was V3's LevelUpModal, read by reflection).
            var modal = Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Hud.LevelUpPresenter>();
            Assert.IsNotNull(modal, "the level-up presenter is installed");
            Assert.IsTrue(modal.IsShowing);
            var skip = modal.SkipButton;
            var panel = modal.Panel;
            for (var i = 0; i < 10 && RunManager.Instance.IsPaused; i++) { skip.onClick.Invoke(); yield return null; }
            Assert.IsFalse(RunManager.Instance.IsPaused, "all level-ups skipped");
            yield return null;
            Assert.IsFalse(panel.activeSelf, $"R-83: the cards are hidden after the last Skip (pending={levels.PendingLevelUps}, reason={RunManager.Instance.PauseReason})");
        }

        [UnityTest]
        public IEnumerator PowderCache_StillSpawnsThroughTheEncounterExecutor()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return WaitForPlaying();
            // M7: the V4 encounter executor runs the ambush (the legacy spawner is off, register D4).
            var game = Object.FindFirstObjectByType<V4Game>();
            Assert.IsTrue(game.Context.TryGet<RoguePirates.V4.Spawning.IEncounterExecutor>(out var executor) || game.Context.TryGet<RoguePirates.V4.Spawning.ISpawnController>(out _));
            executor = (RoguePirates.V4.Spawning.IEncounterExecutor)game.Context.Get<RoguePirates.V4.Spawning.ISpawnController>();
            game.Context.TryGet<RoguePirates.V4.Spawning.IEncounterTracker>(out var tracker);
            var world = V4TestEnemies.Simulation();
            var before = world.ActiveCount;
            var id = tracker.Request(RoguePirates.V4.Spawning.EncounterKind.PowderCache, 3);
            executor.TriggerImmediate(game.Config.Single<RoguePirates.V4.Config.StageContentConfig>().powderCacheSpawner, 3, id);
            Assert.Greater(world.ActiveCount, before, "the V4 encounter executor spawns the Powder Cache ambush");
            Assert.AreEqual(3, tracker.SpawnedCount(id), "registered with the encounter tracker (the legacy kill path completes it)");
        }

        [UnityTest]
        public IEnumerator AmbientSpawns_ComeFromTheV4SpawnController()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return WaitForPlaying();
            var game = Object.FindFirstObjectByType<V4Game>();
            Assert.IsTrue(game.Context.TryGet<RoguePirates.V4.Spawning.ISpawnController>(out var spawn), "spawn controller registered");
            foreach (var step in new[] { FrameStep.Ambient, FrameStep.Overtime, FrameStep.Storm, FrameStep.Beats, FrameStep.StormEdge })
                Assert.IsTrue(game.Frame.Has(step), $"{step} step bound");

            var world = V4TestEnemies.Simulation();
            var start = Time.realtimeSinceStartup;
            while (RunManager.Instance.ElapsedSeconds < 9f && Time.realtimeSinceStartup - start < 30f) yield return null;
            Assert.Greater(world.ActiveCount, 0, "ambient enemies spawned by V4");
            Assert.AreEqual(RoguePirates.V4.Spawning.PhaseState.Active, spawn.Phases.First(p => p.PhaseId == "band_0").State);
        }

        [UnityTest]
        public IEnumerator PoolStress_InTheModuleLab_CreatesNothingAfterPrewarm()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.ModuleLab, LoadSceneMode.Single);
            yield return WaitForPlaying();
            var root = Object.FindFirstObjectByType<RoguePirates.V4.Presentation.V4PresentationRoot>();
            Assert.IsNotNull(root, "the presentation root installs itself on run start");
            var pools = root.Pools;
            var created = pools.TotalCreatedAfterPrewarm;
            var stress = new RoguePirates.V4.DevTools.Diagnostics.PoolStressScenario(pools, Vector3.zero);
            Assert.AreEqual(2, stress.PoolCount, "the two Module Lab stress pools");
            var deadline = Time.realtimeSinceStartup + 30f;
            while (!stress.Tick(1f / 60f) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(stress.Done, stress.Summary);
            Assert.AreEqual(0, stress.ChurnCreatesAfterPrewarm, "churn within prewarm creates nothing: " + stress.Summary);
            Assert.Greater(stress.Acquires, 1000);
            Assert.Greater(stress.Recycled, 0, "the recycle pool recycles at its cap");
            Assert.AreEqual(16, stress.OverflowCreatesAfterPrewarm, "the grow pool grows past its cap of 16 to 32 and counts it");
            Assert.AreEqual(0, stress.Rejected);
            Assert.AreEqual(created + 16, pools.TotalCreatedAfterPrewarm);
            // M14.9b: the ship health bar is a pooled view held for the whole run; the stress pools are what must be released.
            Assert.IsTrue(pools.Stats().Where(st => st.Key.StartsWith("lab/")).All(st => st.InUse == 0), "everything the stress took is released");
        }

        [UnityTest]
        public IEnumerator CreationProbe_CountsObjectsOutsideThePool_Only()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.ModuleLab, LoadSceneMode.Single);
            yield return WaitForPlaying();
            var pools = Object.FindFirstObjectByType<RoguePirates.V4.Presentation.V4PresentationRoot>().Pools;
            var probeHost = new GameObject("ProbeUnderTest");
            var probe = probeHost.AddComponent<RoguePirates.V4.DevTools.Diagnostics.SceneObjectCreationProbe>();
            try
            {
                probe.SetTracking(true);
                yield return null; // first scan: baseline only
                yield return null;
                var held = new System.Collections.Generic.List<GameObject>();
                for (var i = 0; i < 20; i++) held.Add(pools.Acquire("lab/stress_grow", Vector3.zero, Quaternion.identity)); // 4 grown past the cap
                var foreign = new GameObject("ForeignThing(Clone)");
                new GameObject("Child").transform.SetParent(foreign.transform);
                yield return null;
                yield return null;
                Assert.AreEqual(1, probe.Total, "one object tree created outside the pool; pooled creates are left out");
                Assert.AreEqual(1, probe.ByName["ForeignThing"], "named without (Clone)");
                foreach (var h in held) pools.Release(h);
                Object.Destroy(foreign);
            }
            finally { Object.Destroy(probeHost); }
        }

        [UnityTest]
        public IEnumerator Run_HasAViewPool_ThatCreatesNothingBeforeItsConsumersExist()
        {
            SceneManager.LoadScene(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return null;
            yield return WaitForPlaying();
            var root = Object.FindFirstObjectByType<RoguePirates.V4.Presentation.V4PresentationRoot>();
            Assert.IsNotNull(root);
            var stats = root.Pools.Stats().ToList();
            Assert.IsFalse(stats.Any(st => st.Key.StartsWith("lab/")), "no Module Lab pools in a run");
            var enemyPools = stats.Where(st => st.Key.StartsWith("enemy/")).ToList();
            Assert.Greater(enemyPools.Count, 5, "derived enemy view pools are listed");
            // M14.10: the enemy view presenter renders through these pools, so every reachable model is prewarmed.
            Assert.IsTrue(enemyPools.Where(st => st.Source != null && st.Source.StartsWith("derived")).All(st => st.Prewarmed > 0), "reachable enemy models are prewarmed");
            var projectilePools = stats.Where(st => st.Key.StartsWith("projectile/")).ToList();
            Assert.Greater(projectilePools.Count, 0, "M5: projectile views are pooled");
            Assert.IsTrue(projectilePools.All(st => st.Prewarmed > 0), "and prewarmed");
            Assert.AreEqual(0, stats.Sum(st => st.CreatedAfterPrewarm), "nothing created after prewarm at run start");
        }

        static RoguePirates.V4.Progression.ProgressionModule Levels() => Object.FindFirstObjectByType<V4Game>().Build.Progression;

        static RoguePirates.V4.Weapons.IWeaponManager Weapons()
        {
            Object.FindFirstObjectByType<V4Game>().Context.TryGet<RoguePirates.V4.Weapons.IWeaponManager>(out var weapons);
            return weapons;
        }

        static int Emissions(string weaponId) =>
            Object.FindFirstObjectByType<V4Game>().Telemetry.Emissions(weaponId); // M14: the V4 telemetry

        /// <summary>A ring of sharks around the ship, and one ahead and to each side, so every weapon has a target.</summary>
        static void SurroundWithEnemies(RoguePirates.V4.Enemies.EnemySimulation world, PlayerShipController ship, int count = 8)
        {
            var shark = System.Array.FindIndex(world.Definitions, d => d != null && d.id == "shark");
            var forward = ship.MovementForward.sqrMagnitude > 0.01f ? ship.MovementForward.normalized : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, forward);
            foreach (var offset in new[] { forward * 5f, right * 6f, -right * 6f, -forward * 4f })
                world.Spawn(shark, ship.Position + offset, EliteAffix.None, 0.01f, 50f);
            for (var i = 0; i < count; i++)
            {
                var a = i * Mathf.PI * 2f / count;
                world.Spawn(shark, ship.Position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 9f, EliteAffix.None, 0.01f, 50f);
            }
        }

        [UnityTest]
        public IEnumerator Weapons_FireFromV4_OnTheV4Loadout()
        {
            SceneManager.LoadScene(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return null;
            yield return WaitForPlaying();
            var weapons = Weapons();
            Assert.IsNotNull(weapons);
            Assert.AreEqual(Levels().Loadout.Count, weapons.Slots.Count, "V4 slots follow the progression loadout");
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            ship.Damage.SetPlaytestInvulnerable(true);
            var world = V4TestEnemies.Simulation();
            SurroundWithEnemies(world, ship);

            var id = weapons.Slots[0].Id;
            var before = Emissions(id);

            var deadline = Time.realtimeSinceStartup + 10f;
            while (Emissions(id) == before && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.Greater(Emissions(id), before, "V4 fired " + id);
        }

        [UnityTest]
        public IEnumerator LoadoutChanges_ReachTheV4WeaponSlots()
        {
            SceneManager.LoadScene(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return null;
            yield return WaitForPlaying();
            var levels = Levels();
            var weapons = Weapons();
            var count = weapons.Slots.Count;
            levels.DevSetWeapons(levels.Loadout.Weapons.Select(w => w.Id).Append("mortar").ToList());
            yield return null;
            yield return null;
            Assert.AreEqual(count + 1, weapons.Slots.Count);
            var slot = weapons.Slots.Last();
            Assert.AreEqual("mortar", slot.Id);
            levels.DevRankUp(levels.Loadout.IndexOf("mortar"));
            yield return null;
            yield return null;
            Assert.AreEqual(2, slot.Level, "upgrades reach the same V4 slot");
            Assert.AreEqual(levels.Loadout.Weapons.Last().DamageBonus, slot.DamageBonus);
        }

        [UnityTest]
        public IEnumerator EveryWeapon_Fires_InARun()
        {
            SceneManager.LoadScene(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return null;
            yield return WaitForPlaying();
            var game = Object.FindFirstObjectByType<V4Game>();
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            ship.Damage.SetPlaytestInvulnerable(true);
            var world = V4TestEnemies.Simulation();
            foreach (var loadout in new[]
                     {
                         new[] { "deck_cannons", "broadside_battery", "powder_kegs", "flying_cutlasses", "lightning_mast", "flamethrower" },
                         new[] { "swivel_guns", "long_nine", "harpoon_gun", "mortar" },
                     })
            {
                RoguePirates.V4.DevTools.WeaponLab.SetLoadout(game, loadout);
                var before = loadout.ToDictionary(id => id, Emissions);
                var deadline = Time.realtimeSinceStartup + 20f;
                while (Time.realtimeSinceStartup < deadline && loadout.Any(id => Emissions(id) <= before[id]))
                {
                    if (world.ActiveCount < 6) SurroundWithEnemies(world, ship);
                    if (RunManager.Instance.IsPaused) PickFirstCard();
                    yield return null;
                }
                foreach (var id in loadout) Assert.Greater(Emissions(id), before[id], id + " fired");
            }
        }

        /// <summary>
        /// M9 wiring: a card picked in the kept V3 modal reaches the V4 progression and the weapon slots, and the build's
        /// incoming-damage multiplier (Brace!) reaches enemy contact hits and hostile-shot hits (V3's DamageReceiver read it
        /// from the Captain singleton, which left the run).
        /// </summary>
        [UnityTest]
        public IEnumerator BuildWiring_CardPicksReachTheSlots_AndBraceReachesEnemyHits()
        {
            SceneManager.LoadScene(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return null;
            yield return WaitForPlaying();
            var game = Object.FindFirstObjectByType<V4Game>();
            var build = game.Build;
            var levels = build.Progression;
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            ship.Damage.SetPlaytestInvulnerable(true);

            int Progress() => levels.OwnedWeapons.Sum(levels.WeaponLevel) + levels.OwnedLogbooks.Sum(levels.LogbookLevel);
            var before = Progress();
            levels.AddXp(levels.XpToNext - Mathf.CeilToInt(levels.CurrentXp)); // exactly one level: one card
            var deadline = Time.realtimeSinceStartup + 5f;
            while (!RunManager.Instance.IsPaused && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(PauseReason.LevelUp, RunManager.Instance.PauseReason, "the modal paused the run for the V4 cards");
            PickFirstCard();
            yield return null;
            yield return null;
            Assert.IsFalse(RunManager.Instance.IsPaused, "picking resumes the run");
            Assert.AreEqual(before + 1, Progress(), "the card was applied (a new weapon, an upgrade or a Logbook rank)");
            Assert.AreEqual(levels.Loadout.Count, Weapons().Slots.Count, "the weapon slots follow the loadout");

            var hank = game.Config.All<RoguePirates.V4.Config.CaptainConfig>().First(c => c.active != null && c.active.braceSeconds > 0f);
            build.Captain.BeginRun(hank);
            Assert.IsTrue(build.Captain.TryActivate(), "Brace! (actives on by default)");
            // Brace!, times Hank's armor at the current level (R-49, approved at the M9 gate).
            var armor = hank.passive.armorApplied ? 100f / (100f + Mathf.Min(hank.passive.armorCap, hank.passive.armorPerLevel * build.Progression.Level)) : 1f;
            var brace = hank.active.braceIncomingDamageMultiplier * armor;
            Assert.Less(brace, 1f);
            Assert.AreEqual(brace, build.IncomingDamageMultiplier(), 1e-5f, "the build multiplier is Brace! times the armor");
            ship.Damage.SetPlaytestInvulnerable(false);
            var hp = ship.Damage.CurrentHP;
            game.Shell.Enemies.Services.HitPlayer(ship.Damage, 10f, ship.Position);
            Assert.AreEqual(hp - 10f * brace, ship.Damage.CurrentHP, 1e-3f, "a contact hit is braced");
            yield return new WaitForSeconds(GameConstants.ContactIFrame + 0.05f);
            hp = ship.Damage.CurrentHP;
            game.Shell.Enemies.Target.TakeDamage(10f, ship.Position, false);
            Assert.AreEqual(hp - 10f * brace, ship.Damage.CurrentHP, 1e-3f, "a hostile shot is braced");
            ship.Damage.SetPlaytestInvulnerable(true);
        }

        static void PickFirstCard()
        {
            var modal = Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Hud.LevelUpPresenter>(); // M14.11
            var card = modal != null ? modal.transform.Find("CardRow/Card0") : null;
            if (card != null && card.gameObject.activeInHierarchy) card.GetComponent<UnityEngine.UI.Button>()?.onClick.Invoke();
        }

        [UnityTest]
        public IEnumerator ModuleLab_SpawnsOnlyOnRequest()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.ModuleLab, LoadSceneMode.Single);
            yield return WaitForPlaying();
            var game = Object.FindFirstObjectByType<V4Game>();
            Assert.IsTrue(game.IsModuleLab);
            var world = V4TestEnemies.Simulation();
            var start = Time.realtimeSinceStartup;
            while (RunManager.Instance.ElapsedSeconds < 6f && Time.realtimeSinceStartup - start < 20f) yield return null;
            Assert.AreEqual(0, world.ActiveCount, "no ambient spawns in the lab");
            Assert.IsTrue(game.Context.TryGet<RoguePirates.V4.Stage.IStageContent>(out var stage));
            Assert.IsTrue(stage.Module.Enabled, "M8 playtest: chests, landmarks and the Beacon work in the lab");
            Assert.IsTrue(stage.Interaction.Enabled, "M8 playtest: prompts in the lab");
            Assert.IsFalse(stage.BossSummoned, "nothing summoned by itself");

            var index = System.Array.FindIndex(world.Definitions, d => d != null && d.id == "needle_sloop");
            Assert.GreaterOrEqual(index, 0, "specialist variants are in the runtime database");
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            Assert.GreaterOrEqual(world.Spawn(index, ship.Position + Vector3.forward * 14f), 0);
            Assert.AreEqual(1, world.ActiveCount);
            var variant = game.Config.Get<RoguePirates.V4.Config.EnemyVariantConfig>("variant.needle_sloop");
            Assert.AreEqual(variant.baseHP.value, world.Definitions[index].baseHP);
            Assert.IsTrue(ship.Damage.IsAlive);
            ship.Damage.TakeDamage(99999f, Vector3.zero, false);
            Assert.IsTrue(ship.Damage.IsAlive, "the lab ship is invulnerable");
        }

        /// <summary>M12: with continue-after-boss-victory off, the boss kill ends the run: no Rum (paid only at death, playtest round 1), no Doubloons.</summary>
        [UnityTest]
        public IEnumerator VictoryThatEndsTheRun_PaysNothing_AndBanksNothing()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return WaitForPlaying();
            var game = Object.FindFirstObjectByType<V4Game>();
            Assert.IsTrue(game.Config.TryApplyEdit("stage.saltwind", "continueAfterBossVictory", "false", 1f, out var error), error);
            Assert.IsTrue(game.Context.TryGet<RoguePirates.V4.Rewards.IRumView>(out var rum));
            game.Context.TryGet<RoguePirates.V4.Stage.IStageContent>(out var poi);
            var world = V4TestEnemies.Simulation();
            var summoned = false;
            for (var i = 0; i < 60 && !summoned; i++) { summoned = poi.TrySummonBoss() && poi.BossSummoned; if (!summoned) yield return null; }
            Assert.IsTrue(summoned);
            var bossIndex = -1;
            var deadline = Time.realtimeSinceStartup + 8f;
            while (bossIndex < 0 && Time.realtimeSinceStartup < deadline)
            {
                for (var i = 0; i < world.Agents.Length; i++) if (world.Agents[i].active && world.Agents[i].isBoss) { bossIndex = i; break; }
                if (bossIndex < 0) yield return null;
            }
            Assert.GreaterOrEqual(bossIndex, 0);
            var doubloons = AppRoot.Instance.Save.Meta.doubloons;
            var before = AppRoot.Instance.Save.Meta.marks;
            world.ApplyDamage(bossIndex, world.Agents[bossIndex].maxHp + 500f, false, weaponId: "deck_cannons");
            deadline = Time.realtimeSinceStartup + 4f;
            while (RunManager.Instance.State != RunState.RunComplete && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.AreEqual(RunState.RunComplete, RunManager.Instance.State);
            Assert.IsFalse(rum.Settled, "playtest round 1: Rum is paid only at death");
            Assert.AreEqual(before, AppRoot.Instance.Save.Meta.marks);
            Assert.AreEqual(doubloons, AppRoot.Instance.Save.Meta.doubloons, "no Doubloons (V3's CompleteRun paid them)");
        }

        [UnityTest]
        public IEnumerator Run_BossKill_ThenDeath_SettlesOnce()
        {
            RunRequest.RequestSeed(101);
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return WaitForPlaying();
            Assert.AreEqual(101, RunManager.Instance.RunSeed, "RunRequest seed must reach RunManager");

            var probe = new EndProbe();
            Object.FindFirstObjectByType<V4Game>().Modules.Add(probe);

            Object.FindFirstObjectByType<V4Game>().Context.TryGet<RoguePirates.V4.Stage.IStageContent>(out var poi); // M7: V4 stage content
            var world = V4TestEnemies.Simulation();
            var summoned = false;
            for (var i = 0; i < 60 && !summoned; i++)
            {
                summoned = poi.TrySummonBoss() && poi.BossSummoned;
                if (!summoned) yield return null;
            }
            Assert.IsTrue(summoned, "boss summon failed");

            var bossIndex = -1;
            var deadline = Time.realtimeSinceStartup + 8f;
            while (bossIndex < 0 && Time.realtimeSinceStartup < deadline)
            {
                for (var i = 0; i < world.Agents.Length; i++)
                    if (world.Agents[i].active && world.Agents[i].isBoss) { bossIndex = i; break; }
                if (bossIndex < 0) yield return null;
            }
            Assert.GreaterOrEqual(bossIndex, 0, "boss agent never appeared");

            var boss = world.Agents[bossIndex];
            world.ApplyDamage(bossIndex, Mathf.Max(boss.hp, boss.maxHp) + 500f, false, weaponId: "deck_cannons");
            yield return null;
            yield return null;
            Assert.IsTrue(poi.BossDefeated, "boss defeat must be recorded");
            Assert.IsTrue(RunManager.Instance.ObjectiveSecured, "objective must be secured");
            Assert.IsFalse(RunManager.Instance.IsRunEnded, "boss victory must not end the run");

            Assert.IsTrue(Object.FindFirstObjectByType<V4Game>().Shell.Stage.Encounters.BossObjectiveSecured, "boss encounter must complete");

            // M12: Rum is the V4 reward module's, settled at death (no FinalizeRunOnDeath, no Doubloons).
            var game = Object.FindFirstObjectByType<V4Game>();
            Assert.IsTrue(game.Context.TryGet<RoguePirates.V4.Rewards.IRumView>(out var rum));
            var rumBefore = AppRoot.Instance.Save.Meta.marks;
            var doubloonsBefore = AppRoot.Instance.Save.Meta.doubloons;
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            ship.Damage.TakeDamage(99999f, Vector3.zero, false);
            deadline = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (RunManager.Instance.IsRunEnded && rum.Settled) break;
                yield return null;
            }
            Assert.IsTrue(RunManager.Instance.IsRunEnded, "death must end the run");
            Assert.AreEqual(RunState.RunComplete, RunManager.Instance.State);
            Assert.IsTrue(rum.SettlementApplied, "settlement must apply");
            Assert.IsTrue(rum.EndedByDeath);
            Assert.AreEqual(25, rum.SettledRumOf(RoguePirates.V4.Config.RumSource.BossVictory), "boss victory must be credited");
            Assert.AreEqual(rumBefore + rum.SettledTotal, AppRoot.Instance.Save.Meta.marks);
            Assert.AreEqual(doubloonsBefore, AppRoot.Instance.Save.Meta.doubloons, "gold is run-only: nothing banked");
            var afterFirst = AppRoot.Instance.Save.Meta.marks;

            game.Lifecycle.OnPlayerDied(null, null); // a second death callback
            Assert.AreEqual(afterFirst, AppRoot.Instance.Save.Meta.marks, "a second death callback must not pay again");
            Assert.AreEqual(rum.SettledTotal, ((RoguePirates.V4.Rewards.RewardModule)rum).Settle(RunManager.Instance.RunId, true), "a second settle returns the first result");
            Assert.AreEqual(afterFirst, AppRoot.Instance.Save.Meta.marks, "and pays nothing");
            Assert.IsTrue(game.Context.TryGet<RoguePirates.V4.Views.IRunViews>(out var views));
            StringAssert.Contains("RUM EARNED  +" + rum.SettledTotal, views.Results.RewardSection);

            yield return null; // V4Game notices the run end on its next Update
            Assert.AreEqual(1, probe.EndCount, "modules get RunEnd once, when the run ends (not only on scene unload)");
            Assert.IsTrue(probe.LastEnd.ObjectiveSecured, "RunEnd must carry the secured objective");
            Assert.Greater(probe.LastEnd.SimTime, 0f);
        }
    }
}
