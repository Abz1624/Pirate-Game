using System.Collections;
using System.Linq;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Enemies;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Foundation.POI;
using NUnit.Framework;
using RoguePirates.V4.Stage;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoguePirates.V4.Tests
{
    /// <summary>M7: chests, landmarks, the Beacon and the Barnacle King run in V4 stage content (POIManager is gone).</summary>
    [Category("V4")]
    public sealed class V4StagePlayModeTests
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

        static IStageContent Stage()
        {
            Assert.IsTrue(Object.FindFirstObjectByType<V4Game>().Context.TryGet<IStageContent>(out var stage), "stage content registered");
            return stage;
        }

        static PlayerShipController Ship()
        {
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            ship.Damage.SetPlaytestInvulnerable(true);
            return ship;
        }

        [UnityTest]
        public IEnumerator StageContent_ReplacesPOIManager_AndBuildsTheMarkers()
        {
            yield return StartRun();
            var stage = Stage();

            var layout = stage.Module.Layout;
            Assert.IsNotNull(layout);
            Assert.AreEqual(0, layout.explosiveBarrelPositions.Count, "the placer placed no barrels");
            Assert.AreEqual(layout.pois.Count, stage.Module.MarkerOrder.Count, "Saltwind: every marker at the start");
            var chests = layout.pois.Count(p => p.type == POIType.SalvageChest);
            Assert.AreEqual(Object.FindFirstObjectByType<V4Game>().Config.Single<RoguePirates.V4.Config.StageContentConfig>().salvageChestCount, chests);
            Assert.AreEqual(chests, Object.FindObjectsByType<SalvageChestWaterFloat>(FindObjectsSortMode.None).Length, "chests bob on the water, as V3");
        }

        [UnityTest]
        public IEnumerator Beacon_SummonsTheBoss_AndTheV4DirectorAttacks()
        {
            yield return StartRun();
            var stage = Stage();
            var ship = Ship();
            var beacon = stage.Module.Layout.pois.FindIndex(p => p.type == POIType.BossBeacon);
            Assert.GreaterOrEqual(beacon, 0);
            ship.SetWorldPosition(stage.Module.Layout.pois[beacon].position + new Vector3(3f, 0f, 0f));
            yield return null;
            stage.Module.TryCompleteInteraction(beacon);
            var deadline = Time.realtimeSinceStartup + 5f;
            while (!stage.BossSummoned && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(stage.BossSummoned, "the Beacon telegraph ended in a summon");
            Assert.AreEqual(RunState.BossFight, RunManager.Instance.State);
            var world = V4TestEnemies.Simulation();
            Assert.IsTrue(world.TryFindActiveBoss(out _, out _), "the boss is in the world");

            Assert.IsTrue(Object.FindFirstObjectByType<V4Game>().Context.TryGet<IBossView>(out var boss));
            var attacks = new System.Collections.Generic.HashSet<RoguePirates.V4.Config.BossAttackKind>();
            deadline = Time.realtimeSinceStartup + 12f;
            while (attacks.Count < 2 && Time.realtimeSinceStartup < deadline)
            {
                if (boss.CurrentAttack != RoguePirates.V4.Config.BossAttackKind.None) attacks.Add(boss.CurrentAttack);
                yield return null;
            }
            Assert.GreaterOrEqual(attacks.Count, 2, "the director telegraphs its rotation (spread, then shell slam)");
        }

        [UnityTest]
        public IEnumerator Chest_PromptPausesTheRun_AndSkipResumes()
        {
            yield return StartRun();
            var stage = Stage();
            var ship = Ship();
            RunManager.Instance.AddGold(500);
            var chest = stage.Module.Layout.pois.FindIndex(p => p.type == POIType.SalvageChest);
            ship.SetWorldPosition(stage.Module.MarkerPosition(chest) + new Vector3(2f, 0f, 0f));
            var deadline = Time.realtimeSinceStartup + 3f;
            while (RunManager.Instance.PauseReason != PauseReason.POIChoice && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(PauseReason.POIChoice, RunManager.Instance.PauseReason, "the chest's Take/Skip choice pauses the run");
            Assert.AreEqual(chest, stage.PromptTarget);
            yield return null;
            var root = Object.FindFirstObjectByType<RoguePirates.V4.Presentation.V4PresentationRoot>();
            Assert.IsTrue(root.InteractionPrompt.IsVisible);
            StringAssert.Contains("Take (E)", root.InteractionPrompt.Text);
            stage.SkipChest();
            yield return null;
            Assert.AreEqual(PauseReason.None, RunManager.Instance.PauseReason);
            Assert.AreEqual(RunState.Playing, RunManager.Instance.State);
        }

        [UnityTest]
        public IEnumerator PowderCache_V3Ambush_UnderTheParityPreset_SpawnsThroughTheV4Executor()
        {
            // V3's proximity ambush of 3 (the default is the elite challenge since the M8 playtest).
            RunRequest.RequestPreset(System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../../plan/v4-verification/presets/v3-parity.json")));
            yield return StartRun();
            var stage = Stage();
            var ship = Ship();
            var cache = stage.Module.Layout.pois.FindIndex(p => p.type == POIType.MerchantSkiff);
            if (cache < 0) Assert.Ignore("no Powder Cache on this seed");
            var world = V4TestEnemies.Simulation();
            var before = world.ActiveCount;
            ship.SetWorldPosition(stage.Module.Layout.pois[cache].position + new Vector3(4f, 0f, 0f));
            var deadline = Time.realtimeSinceStartup + 3f;
            while (stage.Module.PowderCacheEncounterId == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.Greater(stage.Module.PowderCacheEncounterId, 0, "the ambush started on approach");
            Assert.IsTrue(stage.Module.IsHidden(cache));
            deadline = Time.realtimeSinceStartup + 3f;
            // M14: the run's V4 encounter tracker (was V3's static RunEncounterTracker).
            Object.FindFirstObjectByType<V4Game>().Context.TryGet<RoguePirates.V4.Spawning.IEncounterTracker>(out var tracker);
            while (tracker.SpawnedCount(stage.Module.PowderCacheEncounterId) < 3 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(3, tracker.SpawnedCount(stage.Module.PowderCacheEncounterId), "three ambushers, registered for the kill path");
            Assert.GreaterOrEqual(world.ActiveCount, before + 3);
        }
    }
}
