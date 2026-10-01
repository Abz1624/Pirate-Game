using NUnit.Framework;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Platform;
using RoguePirates.V4.Foundation.UI;
using RoguePirates.V4.Presentation.Hud;
using RoguePirates.V4.Presentation.Menu;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M7: mobile UI and input. The safe area, the UI scale and the 48 px touch-target rule at five screen sizes, the
    /// settings' save and limits, the touch boost and pause presses, the player's pause on top of the run's own, the
    /// automatic quality tier, and the generated controls' sizes.
    /// </summary>
    public sealed class V5MobileUiTests
    {
        [TearDown]
        public void TearDown()
        {
            GameSettings.UseForTests(null);
            SafeAreaFitter.SafeArea = () => Screen.safeArea;
            SafeAreaFitter.ScreenSize = () => new Vector2(Screen.width, Screen.height);
        }

        [Test]
        public void TheSafeArea_BecomesAnchors_InsideTheNotchAndTheHomeBar()
        {
            var (min, max) = SafeAreaFitter.Anchors(new Rect(88f, 63f, 2400f - 88f - 88f, 1080f - 63f), new Vector2(2400f, 1080f));
            Assert.AreEqual(88f / 2400f, min.x, 1e-5f);
            Assert.AreEqual(63f / 1080f, min.y, 1e-5f);
            Assert.AreEqual(1f - 88f / 2400f, max.x, 1e-5f);
            Assert.AreEqual(1f, max.y, 1e-5f);
            Assert.AreEqual((Vector2.zero, Vector2.one), SafeAreaFitter.Anchors(new Rect(0, 0, 0, 0), new Vector2(100f, 100f)), "an empty safe area falls back to the whole screen");
        }

        [Test]
        public void Wrap_PutsTheCanvasContent_UnderOneSafeAreaRect()
        {
            var go = new GameObject("Canvas", typeof(Canvas));
            try
            {
                new GameObject("A", typeof(RectTransform)).transform.SetParent(go.transform, false);
                new GameObject("B", typeof(RectTransform)).transform.SetParent(go.transform, false);
                SafeAreaFitter.SafeArea = () => new Rect(100f, 0f, 1720f, 1080f);
                SafeAreaFitter.ScreenSize = () => new Vector2(1920f, 1080f);
                var safe = SafeAreaFitter.Wrap(go.GetComponent<Canvas>());
                Assert.AreEqual(1, go.transform.childCount, "only the safe-area rect stays on the canvas");
                Assert.AreEqual(2, safe.childCount);
                Assert.AreEqual("A", safe.GetChild(0).name, "children keep their order");
                Assert.AreEqual(100f / 1920f, safe.anchorMin.x, 1e-5f);
                Assert.AreSame(safe, SafeAreaFitter.Wrap(go.GetComponent<Canvas>()), "wrapping twice does nothing");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void EveryTouchTarget_IsAtLeast48Pixels_AtFivePhoneAndTabletSizes()
        {
            var screens = new[] { new Vector2(1280f, 720f), new Vector2(1600f, 720f), new Vector2(2400f, 1080f), new Vector2(1920f, 1080f), new Vector2(2048f, 1536f) };
            foreach (var screen in screens)
            {
                var ppu = UiScale.PixelsPerUnit(screen, GameSettings.MinUiScale);
                Assert.GreaterOrEqual(SettingsPanel.RowHeight * ppu, 48f - 1e-3f, $"a 72-unit control at {screen}");
                Assert.GreaterOrEqual(TouchControlsPresenter.PauseSize * ppu, 48f, $"the pause button at {screen}");
            }
        }

        [Test]
        public void TheUiScale_ShrinksTheReferenceResolution()
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
            try
            {
                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                UiScale.Apply(scaler, 1.2f);
                Assert.AreEqual(1600f, scaler.referenceResolution.x, 1e-3f);
                UiScale.Apply(scaler, 1f);
                Assert.AreEqual(1920f, scaler.referenceResolution.x, 1e-3f, "from the stored base, not compounding");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Settings_SaveAndLoad_WithinTheirLimits()
        {
            var had = PlayerPrefs.HasKey(GameSettings.Key);
            var old = PlayerPrefs.GetString(GameSettings.Key, string.Empty);
            try
            {
                var s = new GameSettings { uiScale = 3f, musicVolume = 2f, sfxVolume = -1f, quality = 9, stickDeadZone = 0f, stickOnRight = true, reducedEffects = true };
                var changed = 0;
                void Count() => changed++;
                GameSettings.Changed += Count;
                s.Save();
                GameSettings.Changed -= Count;
                Assert.AreEqual(1, changed);
                var loaded = GameSettings.Load();
                Assert.AreEqual(GameSettings.MaxUiScale, loaded.uiScale, 1e-5f);
                Assert.AreEqual((1f, 0f, 2, 0.05f), (loaded.musicVolume, loaded.sfxVolume, loaded.quality, loaded.stickDeadZone));
                Assert.IsTrue(loaded.stickOnRight && loaded.reducedEffects);
                PlayerPrefs.SetString(GameSettings.Key, "{not json");
                Assert.AreEqual(1f, GameSettings.Load().uiScale, "a damaged value gives the defaults");
            }
            finally
            {
                if (had) PlayerPrefs.SetString(GameSettings.Key, old); else PlayerPrefs.DeleteKey(GameSettings.Key);
            }
        }

        [Test]
        public void ReducedEffects_HalveTheEffectDensity()
        {
            GameSettings.UseForTests(new GameSettings());
            var normal = QualityProfile.Current.vfxDensity;
            GameSettings.UseForTests(new GameSettings { reducedEffects = true });
            Assert.AreEqual(normal * 0.5f, QualityProfile.Current.vfxDensity, 1e-5f);
        }

        [Test]
        public void TouchPresses_AreReadOnce()
        {
            TouchActionButton.Consume(TouchAction.Boost);
            TouchActionButton.Press(TouchAction.Boost);
            Assert.IsFalse(TouchActionButton.Consume(TouchAction.Pause), "each action its own press");
            Assert.IsTrue(TouchActionButton.Consume(TouchAction.Boost));
            Assert.IsFalse(TouchActionButton.Consume(TouchAction.Boost), "once");
        }

        [Test]
        public void ThePlayersPause_NeverOverridesALevelUp()
        {
            var go = new GameObject("Run");
            try
            {
                var run = go.AddComponent<RunManager>();
                run.SetState(RunState.Playing);
                Assert.IsTrue(RoguePirates.V4.Views.HudCommands.TogglePause(run), "pauses a running run");
                Assert.AreEqual(PauseReason.UserPause, run.PauseReason);
                Assert.IsFalse(RoguePirates.V4.Views.HudCommands.TogglePause(run), "and resumes it");
                Assert.AreEqual(PauseReason.None, run.PauseReason);
                run.SetPause(PauseReason.LevelUp);
                Assert.IsFalse(RoguePirates.V4.Views.HudCommands.TogglePause(run));
                Assert.AreEqual(PauseReason.LevelUp, run.PauseReason, "the level-up keeps its pause");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheAutomaticTier_FollowsTheFrameTime()
        {
            Assert.AreEqual(QualityTier.High, AutoQuality.TierFor(1f / 60f));
            Assert.AreEqual(QualityTier.Mid, AutoQuality.TierFor(0.025f));
            Assert.AreEqual(QualityTier.Low, AutoQuality.TierFor(0.040f));
            Assert.Less(AutoQuality.PhoneRenderScale(QualityTier.Low), AutoQuality.PhoneRenderScale(QualityTier.High));
        }

        [Test]
        public void TheGeneratedControls_AreTouchSized()
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var settings = SettingsPanel.Create(canvas.transform);
                settings.Open();
                foreach (var b in settings.GetComponentsInChildren<Button>(true))
                    Assert.GreaterOrEqual(b.GetComponent<LayoutElement>().preferredHeight, 72f, b.name);
                Assert.IsTrue(settings.IsOpen);
                settings.Editing.uiScale = 1.2f;
                var had = PlayerPrefs.HasKey(GameSettings.Key);
                var old = PlayerPrefs.GetString(GameSettings.Key, string.Empty);
                try
                {
                    settings.Close();
                    Assert.AreEqual(1.2f, GameSettings.Current.uiScale, 1e-5f, "Close saves the edits");
                }
                finally { if (had) PlayerPrefs.SetString(GameSettings.Key, old); else PlayerPrefs.DeleteKey(GameSettings.Key); }
                var pause = PauseMenuPresenter.Create(canvas.transform);
                foreach (var b in pause.GetComponentsInChildren<Button>(true))
                    Assert.GreaterOrEqual(b.GetComponent<LayoutElement>().preferredHeight, 72f, b.name);
            }
            finally { Object.DestroyImmediate(canvas); }
        }

        [Test]
        public void TheSettingsRows_Keep72Units_OnAShortLandscapeScreen_AndScroll()
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace; // keeps the size below
                ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920f, 600f); // shorter than the rows
                var settings = SettingsPanel.Create(canvas.transform);
                settings.Open();
                Canvas.ForceUpdateCanvases();
                var rows = settings.transform.Find("Scroll/Rows") as RectTransform;
                Assert.IsNotNull(rows, "the rows live in a ScrollRect");
                LayoutRebuilder.ForceRebuildLayoutImmediate(rows);
                for (var i = 0; i < rows.childCount; i++)
                    Assert.GreaterOrEqual(((RectTransform)rows.GetChild(i)).rect.height, 72f - 1e-3f, rows.GetChild(i).name);
                var view = ((RectTransform)settings.transform.Find("Scroll")).rect.height;
                Assert.Greater(rows.rect.height, view, "taller than the view: it scrolls");
                Assert.IsNotNull(settings.GetComponentInChildren<ScrollRect>());
                settings.gameObject.SetActive(false); // no save
            }
            finally { Object.DestroyImmediate(canvas); }
        }
    }
}
