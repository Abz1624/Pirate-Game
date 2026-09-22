using System.IO;
using HighSeas.Balance;
using NUnit.Framework;
using RoguePirates.Simulation.Balance;
using UnityEngine;

namespace HighSeas.Tests
{
    /// <summary>Codex R9: writes the 1,200-run balance lab matrix to plan/saltwind-verification/balance-lab/.</summary>
    public sealed class SaltwindBalanceLabMatrixEvidenceTests
    {
        [Test]
        [Timeout(900000)]
        public void Matrix_Production1200Runs_WritesVerificationArtifacts()
        {
            StageBalanceProfile profile = null;
#if UNITY_EDITOR
            profile = UnityEditor.AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(
                "Assets/_Game/Data/Balance/SaltwindSoloStageBalanceProfile.asset");
#endif
            profile ??= StageBalanceProfile.CreateBuiltInSaltwindDefaults();
            var export = profile.ToExport();
            Assert.AreEqual(2, export.BalanceVersion);

            var report = SaltwindBalanceLabBatchRunner.RunMatrix(export, new SaltwindBalanceLabBatchOptions
            {
                BaseSeed = 12000,
                SeedsPerCell = 100
            });

            Assert.AreEqual(18, report.Cells.Count);
            Assert.AreEqual(1800, report.TotalRuns);

            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var outDir = Path.Combine(repoRoot, "plan", "saltwind-verification", "balance-lab");
            SaltwindBalanceLabBatchRunner.WriteReportFiles(report, outDir);

            var summaryPath = Path.Combine(outDir, "saltwind-matrix-summary.txt");
            Assert.IsTrue(File.Exists(summaryPath), "Expected matrix summary artifact.");
            Assert.IsTrue(File.ReadAllText(summaryPath).Contains("1800 runs"));
        }
    }
}
