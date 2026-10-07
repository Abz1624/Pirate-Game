using System.Collections;
using NUnit.Framework;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Presentation;
using RoguePirates.V4.Presentation.Hud;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 phone performance (2 Oct): the fixes found by the phone benchmark (plan/v5/perf/), in the real run.
    /// </summary>
    [Category("V4")]
    public sealed class V5PerfPlayModeTests
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

        static IEnumerator WaitForPlaying()
        {
            var deadline = Time.realtimeSinceStartup + Timeout;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (RunManager.Instance != null && RunManager.Instance.State == RunState.Playing && StageBalanceProfileContext.IsActive
                    && Object.FindFirstObjectByType<V4PresentationRoot>() != null)
                    yield break;
                yield return null;
            }
            Assert.Fail("V4 run did not reach Playing with its presentation root.");
        }

        [UnityTest]
        public IEnumerator MinimapFog_RebuildsOverSeveralFrames_ThenUploadsOnce()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return WaitForPlaying();
            var minimap = Object.FindFirstObjectByType<V4PresentationRoot>().Minimap;
            Assert.IsNotNull(minimap);
            for (var i = 0; i < 30 && minimap.FogRebuildPending; i++) yield return null; // any rebuild from the start settles

            // Paused, so the ship explores nothing new during the measurement (that would restart the rebuild).
            Assert.IsTrue(RoguePirates.V4.Views.HudCommands.TogglePause(RunManager.Instance), "paused");
            for (var i = 0; i < 30 && minimap.FogRebuildPending; i++) yield return null;
            var texture = (Texture2D)System.Array.Find(minimap.GetComponentsInChildren<UnityEngine.UI.RawImage>(true), r => r.name == "FogMask")?.texture;
            Assert.IsNotNull(texture, "the fog texture");
            var rows = texture.height;
            var before = texture.updateCount;

            // A change that needs a new mask (as a newly explored cell does): the rebuild starts and is spread out.
            minimap.fogEdgeSoftnessWorld += 1f;
            yield return null;
            Assert.IsTrue(minimap.FogRebuildPending, "spread over frames, not done in the frame that asked for it");
            var frames = 1;
            while (minimap.FogRebuildPending && frames < 100) { frames++; yield return null; }
            Assert.IsFalse(minimap.FogRebuildPending);
            var expected = Mathf.CeilToInt(rows / (float)MinimapPresenter.FogRowsPerFrame);
            Assert.LessOrEqual(frames, expected + 2, $"{rows} rows at {MinimapPresenter.FogRowsPerFrame} a frame");
            Assert.GreaterOrEqual(frames, expected - 1);
            Assert.AreEqual(before + 1, texture.updateCount, "uploaded once, at the end");
            var views = (RoguePirates.V4.Views.IRunViews)typeof(MinimapPresenter).GetField("_views", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(minimap);
            var stageView = views?.Stage;
            Assert.IsNotNull(stageView, "the stage view");
            CollectionAssert.AreEqual(minimap.FogMaskInOneGoForTests(stageView), minimap.FogPixelsForTests(), "the spread result equals a one-frame rebuild");

            // A second change mid-rebuild: this pass finishes and uploads, then one more pass runs, then it is idle.
            minimap.fogEdgeSoftnessWorld += 1f;
            yield return null;
            yield return null;
            Assert.IsTrue(minimap.FogRebuildPending);
            minimap.fogEdgeSoftnessWorld += 1f;
            var count = texture.updateCount;
            var guard = 0;
            while (minimap.FogRebuildPending && guard++ < 100) yield return null;
            Assert.AreEqual(count + 2, texture.updateCount, "the interrupted pass and one more");
            CollectionAssert.AreEqual(minimap.FogMaskInOneGoForTests(stageView), minimap.FogPixelsForTests(), "and the result is the latest mask");
            RoguePirates.V4.Views.HudCommands.TogglePause(RunManager.Instance);
        }

        [UnityTest]
        public IEnumerator HealthBars_ShareOneCanvas_FollowTheirShips_AndGoWithThem()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return WaitForPlaying();
            // V5 M8z (7 Oct, I8z.5): bars are on elites, minibosses and the boss only since the playtest (4 Oct), so the
            // test places four slow, tough elites near the ship instead of waiting for ordinary spawns.
            var game = Object.FindFirstObjectByType<V4Game>();
            var ship = Object.FindFirstObjectByType<RoguePirates.V4.Foundation.Player.PlayerShipController>();
            ship.Damage.SetPlaytestInvulnerable(true);
            Assert.IsTrue(game.Context.TryGet<RoguePirates.V4.Spawning.IEnemySpawnSink>(out var sink));
            var shark = sink.FindDefinition("shark");
            for (var i = 0; i < 4; i++)
                sink.Spawn(shark, ship.Position + new Vector3(-12f + 8f * i, 0f, 14f), RoguePirates.V4.Config.ConfigEnums.EliteAffix.Armored, 0.01f, 1000f, 0f);
            var deadline = Time.realtimeSinceStartup + 20f;
            RoguePirates.V4.Foundation.VFX.WorldHealthBar[] bars = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                bars = Object.FindObjectsByType<RoguePirates.V4.Foundation.VFX.WorldHealthBar>(FindObjectsSortMode.None);
                if (bars.Length >= 4) break; // the player's and a few enemies'
                yield return null;
            }
            Assert.GreaterOrEqual(bars.Length, 4, "enemies with bars spawned");
            yield return new WaitForEndOfFrame();

            var cam = Camera.main;
            var batches = new System.Collections.Generic.HashSet<RoguePirates.V4.Foundation.VFX.WorldHealthBarBatch>();
            foreach (var bar in bars)
            {
                var batch = bar.transform.parent != null ? bar.transform.parent.GetComponent<RoguePirates.V4.Foundation.VFX.WorldHealthBarBatch>() : null;
                Assert.IsNotNull(batch, bar.name + " is under a shared canvas");
                batches.Add(batch);
                Assert.IsNull(bar.GetComponent<Canvas>(), "its own canvas is removed"); // V5 M8z (7 Oct, playtest): a switched-off one kept the bar from drawing
                Assert.IsTrue(RoguePirates.V4.Foundation.VFX.WorldHealthBarBatch.IsDrawn(bar, null, out var why), bar.name + " is drawn by the shared canvas: " + why);
                Assert.AreEqual(0.01f, bar.transform.localScale.x, 1e-5f, "the prefab's scale is kept");
            }
            Assert.LessOrEqual(batches.Count, 2, "one shared canvas (two at most: enemy and player pools)");
            var total = 0;
            foreach (var b in batches) total += b.Count;
            Assert.AreEqual(bars.Length, total, "every active bar is placed by its batch");

            // Each bar sits over its ship and faces the camera, as before.
            foreach (var bar in bars)
            {
                var target = (Transform)typeof(RoguePirates.V4.Foundation.VFX.WorldHealthBar).GetField("_target", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(bar);
                var height = (float)typeof(RoguePirates.V4.Foundation.VFX.WorldHealthBar).GetField("_heightOffset", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(bar);
                Assert.Less(Vector3.Distance(bar.transform.position, target.position + Vector3.up * height), 0.5f, bar.name + " over its ship");
                Assert.Greater(Vector3.Dot(bar.transform.forward, (bar.transform.position - cam.transform.position).normalized), 0.99f, "faces away from the camera, as LookRotation did");
            }

            // Far bars draw first, near bars last (on top), as separate canvases did.
            foreach (var b in batches)
            {
                b.SortFarToNear(cam.transform.position);
                var last = float.MaxValue;
                for (var i = 0; i < b.transform.childCount; i++)
                {
                    var d = (b.transform.GetChild(i).position - cam.transform.position).sqrMagnitude;
                    Assert.LessOrEqual(d, last + 1e-3f, "far to near");
                    last = d;
                }
            }

            // Back to its pool and out again (an enemy sinks, another spawns): placed once, over its new ship.
            var enemyBar = bars[bars.Length - 1];
            Assert.IsNotNull(enemyBar);
            var home = enemyBar.transform.parent.GetComponent<RoguePirates.V4.Foundation.VFX.WorldHealthBarBatch>();
            var placed = home.Count;
            enemyBar.gameObject.SetActive(false); // what a pool release does (plus a reparent)
            Assert.AreEqual(placed - 1, home.Count, "released: no longer placed");
            var newShip = new GameObject("new ship").transform;
            newShip.position = new Vector3(5f, 0f, 5f);
            enemyBar.transform.SetParent(null, false); // the pool's root in the real flow
            enemyBar.gameObject.SetActive(true);
            enemyBar.Bind(newShip, 2f);
            enemyBar.Bind(newShip, 2f); // bound twice: still one entry
            Assert.AreEqual(placed, home.Count, "back in the same batch, once");
            Assert.AreSame(home.transform, enemyBar.transform.parent);
            yield return null;
            Assert.Less(Vector3.Distance(enemyBar.transform.position, newShip.position + Vector3.up * 2f), 0.01f);
            Object.Destroy(newShip.gameObject);

            // A bar whose ship is gone goes with it.
            var orphan = Object.Instantiate(bars[0].gameObject);
            var ghost = new GameObject("ghost ship").transform;
            var orphanBar = orphan.GetComponent<RoguePirates.V4.Foundation.VFX.WorldHealthBar>();
            orphanBar.Bind(ghost, 2f);
            yield return null;
            Object.Destroy(ghost.gameObject);
            yield return null;
            yield return null;
            Assert.IsTrue(orphan == null, "destroyed once its ship is gone");
        }

        [UnityTest]
        public IEnumerator MinimapFog_RedrawsOnlyAroundNewlyExploredCells_AndBigRevealsOverFrames()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return WaitForPlaying();
            var minimap = Object.FindFirstObjectByType<V4PresentationRoot>().Minimap;
            Object.FindFirstObjectByType<V4Game>().Context.TryGet<RoguePirates.V4.Stage.IStageContent>(out var content);
            Assert.IsNotNull(content);
            Assert.IsTrue(RoguePirates.V4.Views.HudCommands.TogglePause(RunManager.Instance), "paused: only the test explores");
            for (var i = 0; i < 40 && minimap.FogRebuildPending; i++) yield return null;
            yield return null;
            var texture = (Texture2D)System.Array.Find(minimap.GetComponentsInChildren<UnityEngine.UI.RawImage>(true), r => r.name == "FogMask").texture;
            var views = (RoguePirates.V4.Views.IRunViews)typeof(MinimapPresenter).GetField("_views", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(minimap);
            var all = texture.width * texture.height;

            // A small patch in the fog (as the ship sailing on explores): redrawn at once, only around it, exactly right.
            var c = RoguePirates.V4.Foundation.World.PlayBounds.Center;
            var far = c + new Vector3(RoguePirates.V4.Foundation.World.PlayBounds.HalfExtentX * 0.6f, 0f, RoguePirates.V4.Foundation.World.PlayBounds.HalfExtentZ * 0.6f);
            var uploads = texture.updateCount;
            content.Module.Discovery.ExploreAround(far, 12f);
            yield return null;
            Assert.IsFalse(minimap.FogRebuildPending, "a small patch is not a whole rebuild");
            Assert.AreEqual(uploads + 1, texture.updateCount, "uploaded once");
            Assert.Greater(minimap.LastFogPixelsRedrawn, 0);
            Assert.Less(minimap.LastFogPixelsRedrawn, all / 4, $"only around the patch ({minimap.LastFogPixelsRedrawn} of {all} pixels)");
            CollectionAssert.AreEqual(minimap.FogMaskInOneGoForTests(views.Stage), minimap.FogPixelsForTests(), "the same mask as a full rebuild");

            // A big reveal (a treasure chart): the whole mask, spread over frames, then exactly right.
            content.Module.Discovery.ExploreAround(c - new Vector3(RoguePirates.V4.Foundation.World.PlayBounds.HalfExtentX * 0.4f, 0f, 0f), 90f);
            yield return null;
            Assert.IsTrue(minimap.FogRebuildPending, "a big reveal is rebuilt over frames");
            var guard = 0;
            while (minimap.FogRebuildPending && guard++ < 100) yield return null;
            CollectionAssert.AreEqual(minimap.FogMaskInOneGoForTests(views.Stage), minimap.FogPixelsForTests(), "and ends exactly right");
            RoguePirates.V4.Views.HudCommands.TogglePause(RunManager.Instance);
        }
    }
}
