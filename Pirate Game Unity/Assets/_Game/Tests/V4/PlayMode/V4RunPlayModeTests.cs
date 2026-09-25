using System.Collections;
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
        static readonly string[] IntKeys = { "v3_home_seed", "v3_first_conquest_complete", "v3_saltwind_runs_completed" };
        readonly SaveService _original;
        readonly bool[] _had = new bool[IntKeys.Length];
        readonly int[] _values = new int[IntKeys.Length];
        const string MetaKey = "highseas_meta_v1";
        readonly string _meta;

        public V4TestSaveIsolation()
        {
            AppRoot.EnsureInitialized();
            _original = AppRoot.Instance.Save;
            _meta = PlayerPrefs.HasKey(MetaKey) ? PlayerPrefs.GetString(MetaKey) : null;
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
            PlayerPrefs.Save();
        }
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
            StageBalanceProfileContext.ResetForTests();
            RunMarksTracker.Reset();
            RunEncounterTracker.ResetForTests();
            SaltwindRunSettlement.ClearLastResults();
            _isolation?.Dispose();
            _isolation = null;
            yield return SceneManager.LoadSceneAsync(V4SceneNames.Boot, LoadSceneMode.Single);
            yield return null;
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
        public IEnumerator Run_BossKill_ThenDeath_SettlesOnce()
        {
            RunRequest.RequestSeed(101);
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return WaitForPlaying();
            Assert.AreEqual(101, RunManager.Instance.RunSeed, "RunRequest seed must reach RunManager");

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
        }
    }
}
