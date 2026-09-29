using System.Text.RegularExpressions;
using NUnit.Framework;
using RoguePirates.V4.Foundation.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// M16 save safety (a player lost an unlock between two playtests): no write through the save service — including a
    /// stale copy of the save or a snapshot restore by a test or trace helper — can drop owned content or settled-run
    /// receipts the stored save holds; such a write is reported.
    /// </summary>
    [Category("V4")]
    public sealed class V4SaveSafetyTests
    {
        [Test]
        public void AStaleCopyOfTheSave_CannotDropAnUnlock()
        {
            var backend = new MemorySaveBackend();
            var early = new SaveService(backend); // loaded before the purchase
            var player = new SaveService(backend);
            player.TrySettleRun("run-1", 150);
            Assert.IsTrue(player.TryPurchaseMarksUnlock("brine", 100));

            LogAssert.Expect(LogType.Error, new Regex("would have dropped owned content \\[brine\\]"));
            early.TrySettleRun("run-2", 10); // the stale copy writes back
            var reloaded = new SaveService(backend);
            Assert.IsTrue(reloaded.HasEffectiveContentOwned("brine"), "the unlock survives");
            CollectionAssert.Contains(reloaded.Meta.settledRunReceiptIds, "run-1", "and the earlier receipt, so no run pays twice");
        }

        [Test]
        public void RestoringASnapshot_BringsValuesBack_ButKeepsNewUnlocks()
        {
            var backend = new MemorySaveBackend();
            var save = new SaveService(backend);
            save.TrySettleRun("run-1", 120);
            backend.TryLoad("highseas_meta_v1", out var snapshot); // a helper snapshots the save
            Assert.IsTrue(save.TryPurchaseMarksUnlock("flint", 100));

            LogAssert.Expect(LogType.Error, new Regex("would have dropped owned content \\[flint\\]"));
            SaveService.RestoreSnapshot(snapshot, backend);
            var reloaded = new SaveService(backend);
            Assert.AreEqual(120, reloaded.Meta.marks, "the snapshot's values come back");
            Assert.IsTrue(reloaded.HasEffectiveContentOwned("flint"), "but an unlock bought since is kept");
        }

        static string SnapshotFile => System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "Temp", "v4-prefs-snapshot.json");

        static void Invoke(string method, params object[] args) =>
            typeof(RoguePirates.V4.Editor.V4TraceSession).GetMethod(method, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                .Invoke(null, args);

        /// <summary>A snapshot of the save with a fake old meta, as a stale leftover would hold.</summary>
        static void WriteStaleSnapshot(long takenUtcTicks) =>
            System.IO.File.WriteAllText(SnapshotFile, "{\"takenUtcTicks\":" + takenUtcTicks + ",\"entries\":[{\"key\":\"highseas_meta_v1\",\"has\":true,\"isInt\":false,\"i\":0,\"s\":\"{\\\"version\\\":2,\\\"marks\\\":1}\"}]}");

        /// <summary>M16 review: ending a Play session that no trace of this session started (the player's own playtest)
        /// never writes a pending snapshot back; a stale snapshot found at Editor start is discarded too.</summary>
        [TestCase("EnteredEditMode")]
        [TestCase("CrashRecovery")]
        public void AStaleTraceSnapshot_IsDiscarded_NeverRestored(string path)
        {
            var had = PlayerPrefs.HasKey("highseas_meta_v1");
            var meta = PlayerPrefs.GetString("highseas_meta_v1", string.Empty);
            UnityEditor.SessionState.EraseString("v4trace.active");
            try
            {
                LogAssert.ignoreFailingMessages = true; // the discard warning
                WriteStaleSnapshot(System.DateTime.UtcNow.AddDays(-2).Ticks);
                if (path == "EnteredEditMode") Invoke("OnPlayModeChanged", UnityEditor.PlayModeStateChange.EnteredEditMode);
                else Invoke("RecoverAfterCrash");
                Assert.IsFalse(System.IO.File.Exists(SnapshotFile), "discarded");
                Assert.AreEqual(had, PlayerPrefs.HasKey("highseas_meta_v1"));
                Assert.AreEqual(meta, PlayerPrefs.GetString("highseas_meta_v1", string.Empty), "the player's save is untouched");
            }
            finally
            {
                if (System.IO.File.Exists(SnapshotFile)) System.IO.File.Delete(SnapshotFile);
                if (had) PlayerPrefs.SetString("highseas_meta_v1", meta); else PlayerPrefs.DeleteKey("highseas_meta_v1");
            }
        }

        [Test]
        public void NormalWrites_ReportNothing()
        {
            var backend = new MemorySaveBackend();
            var save = new SaveService(backend);
            save.TrySettleRun("run-1", 200);
            save.TryPurchaseMarksUnlock("brine", 100);
            new SaveService(backend).SetSelectedUnlockGoal("flint");
            LogAssert.NoUnexpectedReceived();
            Assert.AreEqual(100, new SaveService(backend).Meta.marks);
        }
    }
}
