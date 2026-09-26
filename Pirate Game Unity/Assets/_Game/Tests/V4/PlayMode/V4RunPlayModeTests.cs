using System.Collections;
using System.Linq;
using HighSeas.Balance;
using HighSeas.Core;
using HighSeas.Encounters;
using HighSeas.Enemies;
using HighSeas.Player;
using HighSeas.POI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoguePirates.V4.Tests
{
    /// <summary>Swaps the save for an in-memory one and restores the PlayerPrefs a V4 run can touch.</summary>
    sealed class V4TestSaveIsolation : System.IDisposable
    {
        static readonly string[] IntKeys = { "v3_home_seed", "v3_first_conquest_complete", "v3_saltwind_runs_completed", "v3_selected_captain", "combat_captain_actives_enabled" };
        readonly SaveService _original;
        readonly bool[] _had = new bool[IntKeys.Length];
        readonly int[] _values = new int[IntKeys.Length];
        const string MetaKey = "highseas_meta_v1";
        const string SnapshotKey = "highseas_run_snapshot_v1";
        readonly string _meta;
        readonly string _runSnapshot;

        public V4TestSaveIsolation()
        {
            AppRoot.EnsureInitialized();
            _original = AppRoot.Instance.Save;
            _meta = PlayerPrefs.HasKey(MetaKey) ? PlayerPrefs.GetString(MetaKey) : null;
            _runSnapshot = PlayerPrefs.HasKey(SnapshotKey) ? PlayerPrefs.GetString(SnapshotKey) : null;
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
            if (_meta != null) PlayerPrefs.SetString(MetaKey, _meta);
            if (_runSnapshot != null) PlayerPrefs.SetString(SnapshotKey, _runSnapshot); else PlayerPrefs.DeleteKey(SnapshotKey);
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
            RunMarksTracker.Reset();
            RunEncounterTracker.ResetForTests();
            SaltwindRunSettlement.ClearLastResults();
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
            Assert.IsNull(Object.FindFirstObjectByType<HighSeas.V3.V3RunBootstrap>(), "V3RunBootstrap must not be in the V4 scene");
        }

        [UnityTest]
        public IEnumerator SceneReload_ClearsStaleMinibossArena()
        {
            // A run that ended mid-arena leaves the static arena active; the next V4 run must not inherit it (R-68).
            typeof(HighSeas.V3.V3MinibossArena).GetProperty("Active").SetValue(null, true);
            Assert.IsTrue(HighSeas.V3.V3MinibossArena.Active);
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return WaitForPlaying();
            Assert.IsFalse(HighSeas.V3.V3MinibossArena.Active, "stale miniboss arena must be cleared at run start");
        }

        [UnityTest]
        public IEnumerator LiveConfigEdits_ReachTheLegacyRun()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return WaitForPlaying();
            var game = Object.FindFirstObjectByType<V4Game>();
            var legacy = game.Shell.LegacyConfig;
            var spawner = Object.FindFirstObjectByType<EncounterSpawner>();
            // M3: the legacy spawner only runs encounters, from the per-run copy without ambient entries.
            Assert.AreSame(legacy.EncounterOnlySchedule, spawner.Schedule, "the spawner must run the per-run encounter-only copy");
            Assert.AreSame(legacy.RuntimeSchedule.bossSummon, spawner.Schedule.bossSummon, "same config-driven profile copies");
            Assert.AreSame(legacy.RuntimeProfile, StageBalanceProfileContext.Authoring, "the profile context must use the per-run profile");

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
            var world = Object.FindFirstObjectByType<EnemyWorld>();
            Assert.AreSame(game.Shell.RuntimeDatabase.Database.enemies, world.Definitions, "the enemy world must use the runtime database");
            var shark = world.Definitions.First(d => d != null && d.id == "shark");
            Assert.AreEqual(HideFlags.DontSave, shark.hideFlags & HideFlags.DontSave, "definitions are per-run copies, not assets");
            Assert.IsTrue(game.Config.TryApplyEdit("enemy.shark", "stats.moveSpeed", "9", 1f, out var error), error);
            Assert.AreEqual(9f, shark.moveSpeed, "archetype edits reach the definition new spawns read");

            var schedule = game.Shell.LegacyConfig.RuntimeSchedule;
            foreach (var profile in new[] { schedule.bossSummon, schedule.powderCacheAmbush, schedule.hammerheadBeat, schedule.drownedPursuer }
                         .Concat(schedule.entries.Select(e => e.profile)).Where(p => p != null && p.enemy != null))
                Assert.AreSame(game.Shell.RuntimeDatabase.Database.GetEnemy(profile.enemy.id), profile.enemy,
                    $"{profile.name}: every spawn path reads the per-run copy");
        }

        [UnityTest]
        public IEnumerator SpawnSlots_AllTickEveryFrame()
        {
            // The frame position of each slot (next to the V3 component it replaces) is checked with the profiler in
            // play sessions and by exact trace parity in the player. Unity's test runner schedules scene components in
            // a different order (measured: the ambient slot runs before the run clock here, after it in the Editor and
            // the player), so this test only checks that every slot ticks each frame, and logs the observed order.
            SceneManager.LoadScene(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return null;
            yield return WaitForPlaying();
            for (var i = 0; i < 5; i++) yield return null;
            yield return new WaitForEndOfFrame();
            var slots = Object.FindObjectsByType<RoguePirates.V4.Legacy.LegacyUpdateSlot>(FindObjectsSortMode.None);
            var clock = RunManager.Instance.ElapsedSeconds;
            Debug.Log($"V4 slot clocks (test runner order): clock {clock:R}, " + string.Join(", ", slots.Select(s => $"{s.role}={s.ClockAtTick:R}")));
            foreach (var role in new[] { "ambient", "overtime", "storm", "beats", "storm-edge" })
            {
                var slot = slots.Single(s => s.role == role);
                Assert.AreEqual(Time.frameCount, slot.FrameAtTick, role + " ticked this frame");
                Assert.That(slot.ClockAtTick, Is.GreaterThan(0f).And.LessThanOrEqualTo(clock), role + " saw this frame's clock or the previous one");
            }
        }

        [UnityTest]
        public IEnumerator EliteBeat_AndOvertime_UseTheirOwnEnemies()
        {
            SceneManager.LoadScene(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return null;
            yield return WaitForPlaying();
            var game = Object.FindFirstObjectByType<V4Game>();
            var world = Object.FindFirstObjectByType<EnemyWorld>();
            // R-05: the overtime spawners' runtime profiles spawn the Drowned enemies.
            var schedule = game.Shell.LegacyConfig.RuntimeSchedule;
            Assert.AreEqual("drowned_pursuer", schedule.drownedPursuer.enemy.id);
            Assert.AreEqual("drowned_harrier", schedule.drownedHarrier.enemy.id);
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
            var world = Object.FindFirstObjectByType<EnemyWorld>();
            Object.FindFirstObjectByType<PlayerShipController>().Damage.SetPlaytestInvulnerable(true);
            // Jump past the stage deadline (the console's forward jump), then overtime must spawn Drowned enemies (R-05).
            var run = RunManager.Instance;
            run.RestoreSuspendSnapshot(new HighSeas.Save.RunSnapshotData { runId = run.RunId, elapsedSeconds = 601f, gold = run.Gold, kills = run.KillCount, objectiveSecured = run.ObjectiveSecured });
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
            var reports = 0;
            void Count(float _) => reports++;
            HighSeas.Meta.TriggerBus.Instance.DamageTaken += Count;
            hp = receiver.CurrentHP;
            var start = Time.time;
            var frames = 0;
            while (Time.time - start < 1f) { sink.DamageOverTime(0.05f, Vector3.zero); frames++; yield return null; }
            HighSeas.Meta.TriggerBus.Instance.DamageTaken -= Count;
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
            HighSeas.UI.LevelSystem.Instance.AddXP(12);
            var deadline = Time.realtimeSinceStartup + 5f;
            while (!RunManager.Instance.IsPaused && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(RunManager.Instance.IsPaused, "level-up cards shown");
            var bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            var modal = HighSeas.UI.LevelUpModal.Instance;
            var skip = (UnityEngine.UI.Button)typeof(HighSeas.UI.LevelUpModal).GetField("rejectButton", bf).GetValue(modal);
            var panel = (GameObject)typeof(HighSeas.UI.LevelUpModal).GetField("panel", bf).GetValue(modal);
            for (var i = 0; i < 10 && RunManager.Instance.IsPaused; i++) { skip.onClick.Invoke(); yield return null; }
            Assert.IsFalse(RunManager.Instance.IsPaused, "all level-ups skipped");
            yield return null;
            yield return new WaitForEndOfFrame(); // after V4Game.LateUpdate (the test runner resumes coroutines mid-frame)
            Assert.IsFalse(panel.activeSelf, $"R-83: the cards are hidden after the last Skip (pending={HighSeas.UI.LevelSystem.Instance.PendingLevelUps}, reason={RunManager.Instance.PauseReason}, modals={Object.FindObjectsByType<HighSeas.UI.LevelUpModal>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length}, cached={Diag(modal)})");
        }

        static string Diag(HighSeas.UI.LevelUpModal modal)
        {
            var sf = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            var cached = typeof(RoguePirates.V4.Legacy.LegacyPresentationAdapter).GetField("_levelUpModal", sf)?.GetValue(null) as Object;
            var next = typeof(RoguePirates.V4.Legacy.LegacyPresentationAdapter).GetField("_nextModalLookupFrame", sf)?.GetValue(null);
            return $"{(cached == null ? "null" : cached == modal ? "same" : "other")}, next={next}, frame={Time.frameCount}";
        }

        [UnityTest]
        public IEnumerator PowderCache_StillSpawnsThroughTheEncounterExecutor()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return WaitForPlaying();
            var spawner = Object.FindFirstObjectByType<EncounterSpawner>();
            var world = Object.FindFirstObjectByType<EnemyWorld>();
            var before = world.ActiveCount;
            spawner.TriggerProfileImmediate(spawner.Schedule.powderCacheAmbush, 3, 0);
            Assert.Greater(world.ActiveCount, before, "the kept spawner runs the Powder Cache ambush (register D4)");
        }

        [UnityTest]
        public IEnumerator AmbientSpawns_ComeFromTheV4SpawnController()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return WaitForPlaying();
            var game = Object.FindFirstObjectByType<V4Game>();
            Assert.IsTrue(game.Context.TryGet<RoguePirates.V4.Spawning.ISpawnController>(out var spawn), "spawn controller registered");
            var legacy = Object.FindFirstObjectByType<EncounterSpawner>();
            Assert.IsNotNull(legacy, "the legacy spawner stays for the Beacon boss and Powder Cache (register D4)");
            Assert.AreEqual(0, legacy.Schedule.entries.Length, "the legacy spawner has no ambient entries");
            Assert.IsNotNull(legacy.Schedule.bossSummon, "it keeps the boss and cache profiles");
            var slots = Object.FindObjectsByType<RoguePirates.V4.Legacy.LegacyUpdateSlot>(FindObjectsSortMode.None);
            foreach (var role in new[] { "ambient", "overtime", "storm", "beats", "storm-edge" })
                Assert.IsTrue(slots.Any(s => s.role == role && s.Tick != null), $"{role} slot bound");
            Assert.IsNull(Object.FindFirstObjectByType<HighSeas.V3.V3PressureBeatDirector>(), "no V3 beat director");
            Assert.IsNull(Object.FindFirstObjectByType<HighSeas.V3.V3OvertimeDirector>(), "no V3 overtime director");

            var world = Object.FindFirstObjectByType<EnemyWorld>();
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
            Assert.IsTrue(pools.Stats().All(st => st.InUse == 0), "everything released");
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
            Assert.AreEqual(0, enemyPools.Sum(st => st.Total), "enemy views not prewarmed before V4 enemy views use them (M8)");
            var projectilePools = stats.Where(st => st.Key.StartsWith("projectile/")).ToList();
            Assert.Greater(projectilePools.Count, 0, "M5: projectile views are pooled");
            Assert.IsTrue(projectilePools.All(st => st.Prewarmed > 0), "and prewarmed");
            Assert.AreEqual(0, stats.Sum(st => st.CreatedAfterPrewarm), "nothing created after prewarm at run start");
        }

        static HighSeas.Weapons.WeaponController Record() => Object.FindFirstObjectByType<HighSeas.Weapons.WeaponController>();

        static RoguePirates.V4.Weapons.IWeaponManager Weapons()
        {
            Object.FindFirstObjectByType<V4Game>().Context.TryGet<RoguePirates.V4.Weapons.IWeaponManager>(out var weapons);
            return weapons;
        }

        static int Emissions(string weaponId) =>
            HighSeas.Combat.WeaponDamageLedger.Snapshot().TryGetValue(weaponId, out var s) ? s.emissions : 0;

        /// <summary>A ring of sharks around the ship, and one ahead and to each side, so every weapon has a target.</summary>
        static void SurroundWithEnemies(EnemyWorld world, PlayerShipController ship, int count = 8)
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
        public IEnumerator Weapons_FireFromV4_AndTheRecordIsInert()
        {
            SceneManager.LoadScene(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return null;
            yield return WaitForPlaying();
            var record = Record();
            Assert.IsFalse(record.enabled, "the WeaponController is a switched-off loadout record");
            var weapons = Weapons();
            Assert.IsNotNull(weapons);
            Assert.AreEqual(record.Weapons.Count, weapons.Slots.Count, "V4 slots mirror the record");
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            ship.Damage.SetPlaytestInvulnerable(true);
            var world = Object.FindFirstObjectByType<EnemyWorld>();
            SurroundWithEnemies(world, ship);

            var id = weapons.Slots[0].Id;
            var before = Emissions(id);
            record.CaptainFireAllWeapons(); // the V3 fire path: no owner, returns at once
            Assert.AreEqual(before, Emissions(id), "the record's own fire path is inert");

            var deadline = Time.realtimeSinceStartup + 10f;
            while (Emissions(id) == before && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.Greater(Emissions(id), before, "V4 fired " + id);
            Assert.AreEqual(0, Object.FindFirstObjectByType<HighSeas.Weapons.ProjectileWorld>().ActiveProjectileCount, "V3's ProjectileWorld is not used");
        }

        [UnityTest]
        public IEnumerator LevelUpWrites_ToTheRecord_ReachV4()
        {
            SceneManager.LoadScene(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return null;
            yield return WaitForPlaying();
            var record = Record();
            var weapons = Weapons();
            var count = weapons.Slots.Count;
            Assert.IsTrue(record.EquipWeapon("mortar"), "what LevelSystem.ApplyOffer does for a new weapon");
            yield return null;
            yield return null;
            Assert.AreEqual(count + 1, weapons.Slots.Count);
            var slot = weapons.Slots.Last();
            Assert.AreEqual("mortar", slot.Id);
            record.UpgradeWeapon(record.FindWeaponIndex("mortar"), HighSeas.Core.Rarity.Uncommon);
            yield return null;
            yield return null;
            Assert.AreEqual(2, slot.Level, "upgrades reach the same V4 slot");
            Assert.AreEqual(record.Weapons.Last().damageBonus, slot.DamageBonus);
        }

        [UnityTest]
        public IEnumerator EveryWeapon_Fires_InARun()
        {
            SceneManager.LoadScene(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return null;
            yield return WaitForPlaying();
            var record = Record();
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            ship.Damage.SetPlaytestInvulnerable(true);
            var world = Object.FindFirstObjectByType<EnemyWorld>();
            record.EnsureWeaponSlotCapacity(6);
            foreach (var loadout in new[]
                     {
                         new[] { "deck_cannons", "broadside_battery", "powder_kegs", "flying_cutlasses", "lightning_mast", "flamethrower" },
                         new[] { "swivel_guns", "long_nine", "harpoon_gun", "mortar" },
                     })
            {
                record.SetWeaponLoadout(loadout);
                HighSeas.UI.LevelSystem.Instance.SetPlaytestWeaponLoadout(loadout);
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

        static void PickFirstCard()
        {
            var modal = HighSeas.UI.LevelUpModal.Instance;
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
            var world = Object.FindFirstObjectByType<EnemyWorld>();
            var start = Time.realtimeSinceStartup;
            while (RunManager.Instance.ElapsedSeconds < 6f && Time.realtimeSinceStartup - start < 20f) yield return null;
            Assert.AreEqual(0, world.ActiveCount, "no ambient spawns in the lab");
            Assert.IsFalse(Object.FindFirstObjectByType<EncounterSpawner>().enabled, "spawner stopped: no ambient spawns or queued encounters");
            Assert.IsFalse(Object.FindFirstObjectByType<POIManager>().enabled, "POIs off: no powder-cache ambush or chests");
            var interaction = Object.FindFirstObjectByType<HighSeas.V3.V3InteractionController>();
            Assert.IsTrue(interaction == null || !interaction.enabled, "no hold-to-interact: the Beacon cannot summon the boss");

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

        [UnityTest]
        public IEnumerator Run_BossKill_ThenDeath_SettlesOnce()
        {
            RunRequest.RequestSeed(101);
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return WaitForPlaying();
            Assert.AreEqual(101, RunManager.Instance.RunSeed, "RunRequest seed must reach RunManager");

            var probe = new EndProbe();
            Object.FindFirstObjectByType<V4Game>().Modules.Add(probe);

            var poi = Object.FindFirstObjectByType<POIManager>();
            var world = Object.FindFirstObjectByType<EnemyWorld>();
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

            Assert.IsTrue(RunEncounterTracker.BossObjectiveSecured, "boss encounter must complete");

            var marksBefore = AppRoot.Instance.Save.Meta.marks;
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            ship.Damage.TakeDamage(99999f, Vector3.zero, false);
            deadline = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (RunManager.Instance.IsRunEnded && SaltwindRunSettlement.LastSettlementApplied) break;
                yield return null;
            }
            Assert.IsTrue(RunManager.Instance.IsRunEnded, "death must end the run");
            Assert.IsTrue(SaltwindRunSettlement.LastSettlementApplied, "settlement must apply");
            Assert.Greater(SaltwindRunSettlement.LastBreakdown.BossVictoryCredits, 0, "boss victory must be credited");
            Assert.AreEqual(marksBefore + SaltwindRunSettlement.LastMarksAwarded, AppRoot.Instance.Save.Meta.marks);
            var afterFirst = AppRoot.Instance.Save.Meta.marks;

            RunManager.Instance.FinalizeRunOnDeath();
            Assert.AreEqual(afterFirst, AppRoot.Instance.Save.Meta.marks, "a second finalize must not pay again");

            yield return null; // V4Game notices the run end on its next Update
            Assert.AreEqual(1, probe.EndCount, "modules get RunEnd once, when the run ends (not only on scene unload)");
            Assert.IsTrue(probe.LastEnd.ObjectiveSecured, "RunEnd must carry the secured objective");
            Assert.Greater(probe.LastEnd.SimTime, 0f);
        }
    }
}
