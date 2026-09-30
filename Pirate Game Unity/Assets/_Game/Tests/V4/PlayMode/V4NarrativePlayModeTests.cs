using System.Collections;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Enemies;
using RoguePirates.V4.Foundation.POI;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Narrative;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoguePirates.V4.Tests
{
    /// <summary>M13: the tutorial prompt and barks in the real run scene.</summary>
    [Category("V4")]
    public sealed class V4NarrativePlayModeTests
    {
        V4TestSaveIsolation _isolation;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _isolation = new V4TestSaveIsolation();
            PlayerPrefs.DeleteKey(PlayerPrefsTutorialStore.Key); // a fresh First Conquest (restored after the test)
            RoguePirates.V4.Presentation.Hud.SaltWindHudPresenter.Disabled = true; // the demo hides barks and prompts
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

        [UnityTest]
        public IEnumerator TutorialPrompt_AndBarks_ShowWithoutPausing()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            var deadline = Time.realtimeSinceStartup + 30f;
            while ((RunManager.Instance == null || RunManager.Instance.State != RunState.Playing) && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            yield return null;
            var game = Object.FindFirstObjectByType<V4Game>();
            Assert.IsTrue(game.Context.TryGet<ITutorialControl>(out var tutorial));
            Assert.IsTrue(game.Context.TryGet<IBarkControl>(out var barks));
            var presenter = Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Hud.NarrativePresenter>();
            Assert.IsNotNull(presenter, "the narrative presenter is installed");

            Assert.IsTrue(tutorial.Active);
            Assert.AreEqual(0, tutorial.Index);
            Assert.IsTrue(presenter.PromptShown, "step 1's prompt is on screen");
            StringAssert.Contains("Sail", presenter.PromptText);
            Assert.IsFalse(RunManager.Instance.IsPaused, "prompts never pause");

            Assert.IsTrue(tutorial.Complete(TutorialTrigger.ShipMoved));
            yield return null;
            StringAssert.Contains("guns fire on their own", presenter.PromptText, "the next prompt");
            Assert.AreEqual(1, PlayerPrefs.GetInt(PlayerPrefsTutorialStore.Key, 0), "progress saved under the V4 key");

            Assert.IsTrue(barks.Play("chest_chippy"));
            yield return null;
            Assert.IsTrue(presenter.BarkShown);
            StringAssert.StartsWith("Chippy: *Chippy", presenter.BarkText);
            Assert.IsFalse(RunManager.Instance.IsPaused, "barks never pause");
        }
    }
}
