using HighSeas.Encounters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HighSeas.Tests
{
    /// <summary>PlayMode gate for legacy false boss victory (no spawn / no tracked kill).</summary>
    public sealed class BossEncounterPlayModeTests
    {
        [UnityTest]
        public System.Collections.IEnumerator FailedBossEncounter_DoesNotSecureObjective()
        {
            RunEncounterTracker.ResetForTests();
            var encounter = RunEncounterTracker.Request(RunEncounterKind.Boss, 1);
            RunEncounterTracker.MarkFailed(encounter.EncounterId);
            Assert.IsFalse(RunEncounterTracker.BossObjectiveSecured);
            yield return null;
        }

        [UnityTest]
        public System.Collections.IEnumerator TrackedBossKill_SecuresObjectiveOnce()
        {
            RunEncounterTracker.ResetForTests();
            var encounter = RunEncounterTracker.Request(RunEncounterKind.Boss, 1);
            RunEncounterTracker.RegisterSpawnedAgent(encounter.EncounterId, 101);
            Assert.IsTrue(RunEncounterTracker.RegisterAgentKill(101));
            Assert.IsTrue(RunEncounterTracker.BossObjectiveSecured);
            Assert.IsFalse(RunEncounterTracker.RegisterAgentKill(101));
            yield return null;
        }
    }
}
