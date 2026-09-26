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
            Assert.AreSame(legacy.RuntimeSchedule, spawner.Schedule, "the spawner must run the per-run schedule copy");
            Assert.AreSame(legacy.RuntimeProfile, StageBalanceProfileContext.Authoring, "the profile context must use the per-run profile");

            // Band 2 rate 1.8 -> 4 spawns/s: the band's spawn interval (read per spawn) becomes 0.25 s.
            Assert.IsTrue(game.Config.TryApplyEdit("timeline.saltwind", "phases[2].ratePerSecond", "4", 1f, out var error), error);
            Assert.AreEqual(0.25f, legacy.RuntimeSchedule.entries[2].profile.timing.spawnIntervalSeconds, 1e-6f);

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
            var legacy = Object.FindFirstObjectByType<V4Game>().Shell.LegacyConfig;
            Assert.AreEqual(0.25f, legacy.RuntimeSchedule.entries[2].profile.timing.spawnIntervalSeconds, 1e-6f, "preset must apply before the run starts");
            Assert.IsNull(RunRequest.PresetJson, "the preset request is consumed");
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
