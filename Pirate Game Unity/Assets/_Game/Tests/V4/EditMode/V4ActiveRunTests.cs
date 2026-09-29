using NUnit.Framework;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Progression;
using RoguePirates.V4.Ports;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// M15 review: the run-active flag (V3's run context until M15) is set at run start and cleared when the run's
    /// stage profile ends, as V3 did; while it is clear, enemy models use scale 1 and the home seed does not lock.
    /// </summary>
    [Category("V4")]
    public sealed class V4ActiveRunTests
    {
        bool _was;

        [SetUp] public void SetUp() => _was = ActiveRun.IsActive;
        [TearDown] public void TearDown() => ActiveRun.IsActive = _was;

        [Test]
        public void EndingTheRunContext_ClearsTheFlag_AndTheRunScale()
        {
            WorldPort.MarkRunContextActive();
            Assert.IsTrue(ContentPort.RunContextActive);
            Assert.AreEqual(ActiveRun.EnemyVisualScale, new RunPort().EnemyVisualScale, "run scale while a run is active");

            AppRoot.EndActiveSaltwindRunContext();
            Assert.IsFalse(ActiveRun.IsActive, "cleared with the run's stage profile, as V3 cleared its run context");
            Assert.AreEqual(1f, new RunPort().EnemyVisualScale, "scale 1 outside a run");
        }

        [Test]
        public void HomeSeed_DoesNotLock_OutsideARun()
        {
            // The machine's saved home seed (a player's progress) is kept aside and put back.
            var saved = HomeSeed.HasLockedHomeSeed ? (int?)PlayerPrefs.GetInt(HomeSeed.HomeSeedKey) : null;
            try
            {
                PlayerPrefs.DeleteKey(HomeSeed.HomeSeedKey);
                ActiveRun.IsActive = false;
                HomeSeed.TryLockFromRun(12345);
                Assert.IsFalse(HomeSeed.HasLockedHomeSeed, "no run, no lock (the guard V3 had)");
            }
            finally
            {
                if (saved.HasValue) PlayerPrefs.SetInt(HomeSeed.HomeSeedKey, saved.Value);
                else PlayerPrefs.DeleteKey(HomeSeed.HomeSeedKey);
                PlayerPrefs.Save();
            }
        }
    }
}
