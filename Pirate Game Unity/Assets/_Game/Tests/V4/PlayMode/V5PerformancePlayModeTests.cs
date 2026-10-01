using System.Collections;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M3e in the real run scene: the top-down camera draws nothing past 250 m (it was 1000 m for a view of about
    /// 60 m of sea).
    /// </summary>
    [Category("V5")]
    public sealed class V5PerformancePlayModeTests
    {
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

        [UnityTest]
        public IEnumerator TheTopDownFarClip_FitsTheView()
        {
            SceneManager.LoadScene(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return null;
            var deadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deadline && !(RunManager.Instance != null && RunManager.Instance.State == RunState.Playing && StageBalanceProfileContext.IsActive))
                yield return null;
            Assert.AreEqual(RunState.Playing, RunManager.Instance.State, "the run started");

            yield return null;
            var cam = Object.FindFirstObjectByType<FollowCamera>();
            Assert.IsNotNull(cam);
            Assert.AreEqual(Mathf.Min(cam.topDownFarClip, 1000f), cam.FarClip, 0.01f, "the top-down far clip");
        }
    }
}
