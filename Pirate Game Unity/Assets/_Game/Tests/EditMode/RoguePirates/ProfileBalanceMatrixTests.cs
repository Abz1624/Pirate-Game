using System.Linq;
using NUnit.Framework;
using RoguePirates.Simulation;
using RoguePirates.Simulation.Balance;

namespace RoguePirates.Tests
{
    public class ProfileBalanceMatrixTests
    {
        static readonly string[] Profiles =
        {
            "A_CURRENT",
            "B_SIX_ONLY",
            "C_SIX_DIRECTIONAL",
            "D_FULL_COMBAT_V2"
        };

        [Test]
        public void ProfileMatrix_DeterministicPerProfile()
        {
            foreach (var profile in Profiles)
            {
                var a = ProfileBalanceHarness.Run(profile, 12345, 300, SimInputPolicy.Forward);
                var b = ProfileBalanceHarness.Run(profile, 12345, 300, SimInputPolicy.Forward);
                Assert.That(b.StateHash, Is.EqualTo(a.StateHash));
            }
        }

        [Test]
        public void ProfileMatrix_SixSlotProfiles_OutkillBaselineOnAverage()
        {
            var seeds = new[] { 1, 42, 12345 };
            var baselineKills = seeds.Sum(s => ProfileBalanceHarness.Run("A_CURRENT", s, 600).Kills);
            var fullKills = seeds.Sum(s => ProfileBalanceHarness.Run("D_FULL_COMBAT_V2", s, 600).Kills);
            Assert.That(fullKills, Is.GreaterThanOrEqualTo(baselineKills));
        }

        [Test]
        public void ComputeV3SpawnRate_MatchesDocSamples()
        {
            Assert.That(SimMath.ComputeV3SpawnRate(0f, 1f, true), Is.EqualTo(2.5f).Within(0.01f));
            Assert.That(SimMath.ComputeV3SpawnRate(10f, 1f, true), Is.EqualTo(10.5f).Within(0.2f));
        }

        [Test]
        public void ProfileMatrix_WritesBalanceReportArtifact()
        {
            var reports = ProfileBalanceHarness.RunMatrix(Profiles, new[] { 1, 42 }, 300);
            var path = System.IO.Path.Combine(
                UnityEngine.Application.dataPath,
                "..",
                "..",
                "plan",
                "balance_reports",
                "auto",
                "profile_matrix_latest.jsonl");
            ProfileBalanceHarness.WriteJsonLines(path, reports);
            Assert.That(System.IO.File.Exists(path), Is.True);
        }
    }
}
