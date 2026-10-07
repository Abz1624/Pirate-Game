using NUnit.Framework;
using RoguePirates.V4.Config;
using UnityEditor;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 after the demo (1 Oct 2026): the demo's XP pace and ship speed are config values that must stay positive.
    /// V5 M8z (7 Oct, I8z.5): the checks that the demo's retune was in the data and that the v3-parity preset took it back
    /// were retired with the preset (D18); the game as played is the baseline.
    /// </summary>
    public sealed class V5DemoBaselineTests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        static ConfigSet Set => AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);

        [Test]
        public void XpPace_AndShipSpeed_MustBePositive()
        {
            using var rt = new ConfigSetRuntime(Set);
            rt.Single<PickupConfig>().xpPickupMultiplier = 0f;
            rt.Single<RunConfig>().shipSpeedMultiplier = -1f;
            var report = new ConfigValidationReport();
            rt.Single<PickupConfig>().Validate(report);
            rt.Single<RunConfig>().Validate(report);
            var text = report.ToString();
            StringAssert.Contains("xpPickupMultiplier", text);
            StringAssert.Contains("shipSpeedMultiplier", text);
        }
    }
}
