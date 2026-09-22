using System.Collections;
using HighSeas.Balance;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.V3;
using HighSeas.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HighSeas.Tests
{
    /// <summary>Codex R6/R8: storm safe-region behavior with active stage profile (PlayMode).</summary>
    public sealed class SaltwindStormArenaPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            AppRoot.EnsureInitialized();
            StageBalanceProfileContext.BeginRun(StageBalanceProfile.CreateBuiltInSaltwindDefaults());
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            AppRoot.EndActiveSaltwindRunContext();
            yield return SceneManager.LoadSceneAsync(V3SceneNames.Boot, LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StormSail_ContractedArena_KeepsSpawnAndBeaconInsideSafeZone()
        {
            var export = StageBalanceProfileContext.Export;
            Assert.NotNull(export);

            const int seed = 12345;
            var layoutDef = Resources.Load<IslandLayoutDefinition>("V3SaltwindIslandLayout");
            Assert.NotNull(layoutDef);

            IslandSpawnExclusionRegistry.Clear();
            IslandLayoutSpawner.RegisterPredictedZones(layoutDef, seed);
            PlayBounds.ConfigureFromExclusionRegistry();

            var layout = new StageLayout { seed = seed };
            var options = V3StagePoiPlacementOptions.SaltwindSolo();
            V3StagePoiPlacer.Place(layout, new RunRng(seed + 404), layoutDef, options);

            Assert.IsTrue(PlayBounds.IsInside(layout.playerSpawn, PlayBounds.StormEdgeMargin));

            var beacon = layout.bossBeacon;
            if (beacon == Vector3.zero)
            {
                for (var i = 0; i < layout.pois.Count; i++)
                {
                    if (layout.pois[i].type != POIType.BossBeacon) continue;
                    beacon = layout.pois[i].position;
                    break;
                }
            }

            Assert.AreNotEqual(Vector3.zero, beacon);
            Assert.IsTrue(PlayBounds.IsInside(beacon, PlayBounds.StormEdgeMargin));

            PlayBounds.SetProfileStormSafeScale(SaltwindFinaleLogic.GetStormSafeRegionScale(900f, export));
            Assert.AreEqual(0.4f, PlayBounds.StormSafeScale, 0.001f);

            IslandSpawnExclusionRegistry.Clear();
            PlayBounds.ResetStormSafeRegion();
            yield return null;
        }

        [UnityTest]
        public IEnumerator StormSafeRegion_ContractsBetween720And900Seconds()
        {
            var export = StageBalanceProfileContext.Export;
            Assert.NotNull(export);

            Assert.AreEqual(1f, SaltwindFinaleLogic.GetStormSafeRegionScale(700f, export), 0.001f);
            Assert.IsFalse(SaltwindFinaleLogic.IsStormDamageActive(719f, export));
            Assert.IsTrue(SaltwindFinaleLogic.IsStormDamageActive(720f, export));

            var mid = SaltwindFinaleLogic.GetStormSafeRegionScale(810f, export);
            Assert.Less(mid, 1f);
            Assert.Greater(mid, 0.4f);
            Assert.AreEqual(0.4f, SaltwindFinaleLogic.GetStormSafeRegionScale(900f, export), 0.001f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StormWarning_LeadsStormStartByProfileSeconds()
        {
            var export = StageBalanceProfileContext.Export;
            var lead = export.Timing.StormWarningLeadSeconds;
            var start = export.Timing.StormStartSeconds;
            Assert.IsTrue(SaltwindFinaleLogic.ShouldShowStormWarning(start - lead, export, warned: false));
            Assert.IsFalse(SaltwindFinaleLogic.ShouldShowStormWarning(start - lead - 1f, export, warned: false));
            yield return null;
        }
    }
}
