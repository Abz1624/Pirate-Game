using NUnit.Framework;
using RoguePirates.V4.Foundation.World;
using RoguePirates.V4.Presentation.Hud;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>Hackathon (12 Oct): the snapshot minimap draws the static world once and keeps no camera; its markers map
    /// world positions to 0..1 within the play-area bounds.</summary>
    [Category("V4")]
    public sealed class HackathonMinimapTests
    {
        GameObject _canvas;

        [TearDown]
        public void TearDown()
        {
            if (_canvas != null) Object.DestroyImmediate(_canvas);
            PlayBounds.ResetToDefaults();
        }

        [Test]
        public void Snapshot_RendersOnce_AndLeavesNoCameraBehind()
        {
            PlayBounds.ResetToDefaults();
            _canvas = new GameObject("canvas", typeof(RectTransform), typeof(Canvas));
            var camerasBefore = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            var map = SnapshotMinimapPresenter.Create(_canvas.transform);
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                // -nographics (batch runs, the dedicated server): no snapshot, and nothing made or left behind.
                Assert.IsFalse(map.TakeSnapshot(), "no snapshot without a graphics device");
                Assert.AreEqual(0, map.SnapshotCount);
                Assert.AreEqual(camerasBefore, Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, "no camera made");
                Assert.Ignore("no graphics device (-nographics): the snapshot is skipped, as intended");
            }
            map.TakeSnapshot();
            Assert.AreEqual(1, map.SnapshotCount);
            Assert.IsNotNull(map.WorldTexture, "the static world is in a texture");
            Assert.IsTrue(map.LastSnapshotCamera == null || !map.LastSnapshotCamera.enabled, "the snapshot camera is disabled");
            Assert.IsTrue(map.LastSnapshotCamera == null, "and destroyed (no per-frame render)");
            Assert.AreEqual(camerasBefore, Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, "no camera left");
            Assert.AreEqual(LayerMask.GetMask("Island"), SnapshotMinimapPresenter.SnapshotCullingMask, "islands only");
        }

        [Test]
        public void Markers_MapWorldPositions_IntoTheBounds()
        {
            var bounds = new Rect(-100f, -50f, 200f, 100f);
            Assert.AreEqual(new Vector2(0f, 0f), SnapshotMinimapPresenter.Normalize(bounds, new Vector3(-100f, 3f, -50f)));
            Assert.AreEqual(new Vector2(1f, 1f), SnapshotMinimapPresenter.Normalize(bounds, new Vector3(100f, 0f, 50f)));
            Assert.AreEqual(new Vector2(0.5f, 0.5f), SnapshotMinimapPresenter.Normalize(bounds, Vector3.zero));
            Assert.AreEqual(new Vector2(0.75f, 0.25f), SnapshotMinimapPresenter.Normalize(bounds, new Vector3(50f, 0f, -25f)), "x maps to x, z to y");
            Assert.AreEqual(new Vector2(1f, 0f), SnapshotMinimapPresenter.Normalize(bounds, new Vector3(500f, 0f, -500f)), "outside: clamped to the edge");
            PlayBounds.ResetToDefaults();
            var play = SnapshotMinimapPresenter.BoundsRect();
            Assert.AreEqual(PlayBounds.HalfExtentX * 2f, play.width, 1e-4f);
            Assert.AreEqual(PlayBounds.HalfExtentZ * 2f, play.height, 1e-4f);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), SnapshotMinimapPresenter.Normalize(play, PlayBounds.Center));
        }
    }
}
