using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Events;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Foundation.Progression;
using RoguePirates.V4.Stage;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M8z (7 Oct, I8z.3): the playtest switches the owner plays, as shipped (<see cref="UseShippedPlaytestSettingsAttribute"/>):
    /// the stage's points of interest and the Beacon's whirlpool boss, everything unlocked, and the voyage's session file.
    /// Each test first checks the shipped switch it relies on, so a changed preset says which test to update.
    /// </summary>
    [Category("V5")]
    public sealed class V5PlaytestPresetPlayModeTests
    {
        const float Timeout = 30f;
        V4TestSaveIsolation _isolation;
        readonly List<string> _sessionFiles = new List<string>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _isolation = new V4TestSaveIsolation();
            _sessionFiles.Clear();
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

        /// <summary>A solo voyage to Playing; its session file (when it records one) goes to this test, not to disk.</summary>
        IEnumerator StartVoyage()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            var deadline = Time.realtimeSinceStartup + Timeout;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (RunManager.Instance != null && RunManager.Instance.State == RunState.Playing && StageBalanceProfileContext.IsActive) break;
                yield return null;
            }
            Assert.IsTrue(RunManager.Instance != null && RunManager.Instance.State == RunState.Playing, "the voyage reached Playing");
            var session = Game().Session;
            if (session != null) session.Save = (name, json) => _sessionFiles.Add(name);
        }

        static V4Game Game() => Object.FindFirstObjectByType<V4Game>();

        static IStageContent Stage()
        {
            Assert.IsTrue(Game().Context.TryGet<IStageContent>(out var stage), "stage content registered");
            return stage;
        }

        [UnityTest, UseShippedPlaytestSettings]
        public IEnumerator Shipped_AllPointsOfInterest_AndTheBeaconsWhirlpoolBoss()
        {
            var shipped = ShippedPlaytestSettings.Asset;
            Assert.IsFalse(shipped.hidePointsOfInterest, "the shipped preset hides every point of interest: update this test");
            Assert.IsFalse(shipped.chestsOnlyPointsOfInterest, "the shipped preset keeps only the chests: update this test");
            Assert.AreEqual(0f, shipped.autoBossSeconds, "the shipped preset raises the boss on a timer: update this test");
            Assert.IsNotNull(shipped.bossArrivalPrefab, "the shipped preset has no whirlpool: update this test");

            yield return StartVoyage();
            var stage = Stage();
            var pois = stage.Module.Layout.pois;
            var types = pois.Select(p => p.type).Distinct().ToList();
            CollectionAssert.Contains(types, POIType.SalvageChest, "the chests");
            CollectionAssert.Contains(types, POIType.BossBeacon, "the Treasury Beacon (no timed boss)");
            Assert.Greater(types.Count, 2, "the landmarks too (not chests only): " + string.Join(", ", types));
            Assert.AreEqual(pois.Count, stage.Module.MarkerOrder.Count, "every point of interest has its marker");

            var arrivals = 0;
            System.Action<IReadOnlyList<CombatEvent>> onTick = batch =>
            {
                for (var i = 0; i < batch.Count; i++) if (batch[i].Kind == CombatEventKind.BossArriving) arrivals++;
            };
            var events = Game().Events;
            events.TickPublished += onTick;
            try
            {
                var ship = Object.FindFirstObjectByType<PlayerShipController>();
                ship.Damage.SetPlaytestInvulnerable(true);
                var beacon = pois.FindIndex(p => p.type == POIType.BossBeacon);
                ship.SetWorldPosition(pois[beacon].position + new Vector3(3f, 0f, 0f));
                yield return null;
                stage.Module.TryCompleteInteraction(beacon);
                yield return null;
                Assert.IsFalse(stage.BossSummoned, "the whirlpool swirls first: the boss is not up at once");
                var deadline = Time.realtimeSinceStartup + 15f;
                while (!stage.BossSummoned && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsTrue(stage.BossSummoned, "the Beacon summoned the boss");
                Assert.AreEqual(1, arrivals, "the boss rose out of its whirlpool (one BossArriving event)");
                Assert.AreEqual(RunState.BossFight, RunManager.Instance.State);
                Assert.IsTrue(V4TestEnemies.Simulation().TryFindActiveBoss(out _, out _), "the boss is in the world");
            }
            finally { events.TickPublished -= onTick; }
        }

        [UnityTest, UseShippedPlaytestSettings]
        public IEnumerator Shipped_UnlocksEverything_ACaptainAndALogbookThatCostRum()
        {
            Assert.IsTrue(ShippedPlaytestSettings.Asset.unlockEverything, "the shipped preset no longer unlocks everything: update this test");
            Assume.That(!PlaytestFlags.DeveloperPresetActive, "the developer preset unlocks everything by itself");
            yield return StartVoyage();
            var config = Game().Config;
            var captain = config.All<CaptainConfig>().FirstOrDefault(c => c != null && c.rumCost > 0);
            Assert.IsNotNull(captain, "a Captain that costs Rum");
            var logbook = config.Single<UnlockConfig>().logbookUnlocks.FirstOrDefault(u => u != null && u.rumCost > 0)?.contentId;
            Assert.IsFalse(string.IsNullOrEmpty(logbook), "a Logbook that costs Rum");

            var locks = Composition.MenuServices.CreateLocks(() => config);
            Assert.IsFalse(locks.StateOf(captain).Locked, $"{captain.captainKey} ({captain.rumCost} Rum) is open");
            Assert.IsTrue(Composition.BuildModule.IsBought(config, logbook), $"{logbook} (Rum) is offered on level-ups");

            // The same (empty) save with the switches off: both cost Rum again, so the switch opened them.
            DemoMode.Off = true;
            try
            {
                Assert.IsTrue(locks.StateOf(captain).Locked, "locked without the playtest switch");
                Assert.IsFalse(Composition.BuildModule.IsBought(config, logbook), "not offered without the playtest switch");
            }
            finally { DemoMode.Off = false; }
        }

        [UnityTest, UseShippedPlaytestSettings]
        public IEnumerator Shipped_SoloVoyage_RecordsItsSession()
        {
            yield return StartVoyage();
            var session = Game().Session;
            Assert.IsNotNull(session, "a solo voyage records its session with the playtest switches on");
            yield return new WaitForSeconds(0.5f);
            session.Finish();
            Assert.IsTrue(session.Saved);
            Assert.AreEqual(1, _sessionFiles.Count, "one session file");
            StringAssert.StartsWith("session-", _sessionFiles[0]);
            StringAssert.Contains("\"samples\":[", session.LastJson);
        }

        // V5 M8z (7 Oct, D20): session files only in playtest builds. The shipped switches with downloadSessionFiles off (a
        // public build): no recorder, so nothing is recorded, saved or downloaded.
        [UnityTest, UseShippedPlaytestSettings]
        public IEnumerator Shipped_DownloadSessionFilesOff_RecordsNoSession()
        {
            var shipped = ShippedPlaytestSettings.Asset;
            Assert.IsTrue(shipped.downloadSessionFiles, "the playtest build downloads session files (switch off for a public build)");
            shipped.downloadSessionFiles = false;
            try
            {
                Assert.IsFalse(DemoMode.DownloadSessionFiles);
                yield return StartVoyage();
                Assert.IsNull(Game().Session, "no session recorder with downloadSessionFiles off");
                Assert.AreEqual(0, _sessionFiles.Count);
            }
            finally { shipped.downloadSessionFiles = true; }
        }

        [UnityTest]
        public IEnumerator SwitchesOff_SoloVoyage_RecordsNoSession()
        {
            Assert.IsTrue(DemoMode.Off, "tests run with the switches off unless they opt in");
            yield return StartVoyage();
            Assert.IsNull(Game().Session, "no session recorder (tests write no session files)");
        }
    }
}
