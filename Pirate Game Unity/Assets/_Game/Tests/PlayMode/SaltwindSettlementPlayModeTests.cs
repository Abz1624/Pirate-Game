using HighSeas.Balance;
using HighSeas.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HighSeas.Tests
{
    /// <summary>Codex R10: RunManager death path applies Marks settlement once (not tracker-only).</summary>
    public sealed class SaltwindSettlementPlayModeTests
    {
        PlayModeSaveIsolation _saveIsolation;

        [UnitySetUp]
        public System.Collections.IEnumerator SetUp()
        {
            _saveIsolation = PlayModeSaveIsolation.Begin();
            StageBalanceProfileContext.BeginRun(StageBalanceProfile.CreateBuiltInSaltwindDefaults());
            SaltwindRunSettlement.ClearLastResults();
            RunMarksTracker.Reset();
            yield return null;
        }

        [UnityTearDown]
        public System.Collections.IEnumerator TearDown()
        {
            StageBalanceProfileContext.ResetForTests();
            RunMarksTracker.Reset();
            SaltwindRunSettlement.ClearLastResults();
            _saveIsolation?.Dispose();
            _saveIsolation = null;
            yield return null;
        }

        [UnityTest]
        public System.Collections.IEnumerator PlayerDeath_SettlesMarksOnce_ThroughRunManager()
        {
            var marksBefore = AppRoot.Instance.Save.Meta.marks;

            var rmGo = new GameObject("RunManager");
            var rm = rmGo.AddComponent<RunManager>();
            rm.BeginRun(9001);
            RunMarksTracker.BeginRun(rm.RunId);
            RunMarksTracker.TryRegisterBossVictory();
            rm.EnterPlaying();
            rm.SetState(RunState.PlayerDead);
            rm.FinalizeRunOnDeath();

            Assert.IsTrue(SaltwindRunSettlement.LastSettlementApplied);
            Assert.Greater(SaltwindRunSettlement.LastMarksAwarded, 0);
            Assert.AreEqual(
                marksBefore + SaltwindRunSettlement.LastMarksAwarded,
                AppRoot.Instance.Save.Meta.marks);

            var marksAfterFirst = AppRoot.Instance.Save.Meta.marks;
            rm.FinalizeRunOnDeath();
            Assert.AreEqual(marksAfterFirst, AppRoot.Instance.Save.Meta.marks);

            TeardownTestObject(rmGo);
            yield return null;
        }

        static void TeardownTestObject(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}
