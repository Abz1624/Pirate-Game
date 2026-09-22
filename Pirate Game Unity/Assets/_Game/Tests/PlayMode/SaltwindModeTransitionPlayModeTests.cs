using System.Collections;
using HighSeas.Balance;
using HighSeas.Core;
using HighSeas.V3;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HighSeas.Tests
{
    /// <summary>Codex R8: solo profile context must not leak across scene/mode transitions (PlayMode).</summary>
    public sealed class SaltwindModeTransitionPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            V3PlaytestFlags.ResetForTests();
            AppRoot.EndActiveSaltwindRunContext();
            yield return SceneManager.LoadSceneAsync(V3SceneNames.Boot, LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SoloProfile_EndContextThenBootScene_KeepsStarterUnlockStable()
        {
            AppRoot.EnsureInitialized();
            V3PlaytestFlags.ResetForTests();

            var starterUnlocked = V3ContentUnlocks.IsWeaponUnlocked("deck_cannons");
            Assert.IsTrue(starterUnlocked);

            StageBalanceProfileContext.BeginRun(StageBalanceProfile.CreateBuiltInSaltwindDefaults());
            V3RunContext.IsActive = true;
            yield return null;

            Assert.IsTrue(StageBalanceProfileContext.IsActive);
            AppRoot.EndActiveSaltwindRunContext();
            Assert.IsFalse(StageBalanceProfileContext.IsActive);
            Assert.IsFalse(V3RunContext.IsActive);

            yield return SceneManager.LoadSceneAsync(V3SceneNames.Boot, LoadSceneMode.Single);
            yield return null;

            Assert.AreEqual(starterUnlocked, V3ContentUnlocks.IsWeaponUnlocked("deck_cannons"));
            Assert.IsFalse(V3PlaytestFlags.EnableRunPlaytestHotkeys);
        }

        [UnityTest]
        public IEnumerator LoadSceneHelper_EndsProfileWhenLeavingSaltwindRun()
        {
            AppRoot.EnsureInitialized();
            StageBalanceProfileContext.BeginRun(StageBalanceProfile.CreateBuiltInSaltwindDefaults());
            Assert.IsTrue(StageBalanceProfileContext.IsActive);

            AppRoot.Instance.LoadScene(V3SceneNames.Boot);
            yield return null;

            Assert.IsFalse(StageBalanceProfileContext.IsActive);
        }
    }
}
