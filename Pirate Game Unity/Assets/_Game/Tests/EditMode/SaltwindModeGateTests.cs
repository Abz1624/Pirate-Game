using HighSeas.Balance;
using HighSeas.Core;
using HighSeas.V3;
using HighSeas.World;
using NUnit.Framework;
using UnityEngine;

namespace HighSeas.Tests
{
    public sealed class SaltwindModeGateTests
    {
        [TearDown]
        public void TearDown()
        {
            V3PlaytestFlags.ResetForTests();
            StageBalanceProfileContext.ResetForTests();
            V3RunContext.IsActive = false;
            V3RunContext.Layout = null;
        }

        [Test]
        public void NormalPlay_RunHotkeysDisabled()
        {
            V3PlaytestFlags.ResetForTests();
            Assert.IsFalse(V3PlaytestFlags.EnableRunPlaytestHotkeys);
            Assert.IsFalse(V3PlaytestFlags.BypassProgressionGates);
        }

        [Test]
        public void DeveloperPreset_EnablesHotkeysAndBypass()
        {
            V3PlaytestFlags.ForceDeveloperPresetForTests = true;
            Assert.IsTrue(V3PlaytestFlags.EnableRunPlaytestHotkeys);
            Assert.IsTrue(V3PlaytestFlags.BypassProgressionGates);
        }

        [Test]
        public void EndActiveSaltwindRunContext_ClearsProfileAndRunLayout()
        {
            StageBalanceProfileContext.BeginRun(StageBalanceProfile.CreateBuiltInSaltwindDefaults());
            V3RunContext.IsActive = true;
            V3RunContext.Layout = new StageLayout { seed = 42 };

            AppRoot.EndActiveSaltwindRunContext();

            Assert.IsFalse(StageBalanceProfileContext.IsActive);
            Assert.IsFalse(V3RunContext.IsActive);
            Assert.IsNull(V3RunContext.Layout);
        }

        [Test]
        public void MenuWeaponGate_StableWithAndWithoutActiveProfile()
        {
            PlayerPrefs.DeleteKey(V3ContentUnlocks.FirstConquestKey);
            V3PlaytestFlags.ResetForTests();

            var starterUnlocked = V3ContentUnlocks.IsWeaponUnlocked("deck_cannons");
            Assert.IsTrue(starterUnlocked);

            StageBalanceProfileContext.BeginRun(StageBalanceProfile.CreateBuiltInSaltwindDefaults());
            Assert.AreEqual(starterUnlocked, V3ContentUnlocks.IsWeaponUnlocked("deck_cannons"));

            StageBalanceProfileContext.EndRun();
            Assert.AreEqual(starterUnlocked, V3ContentUnlocks.IsWeaponUnlocked("deck_cannons"));
        }
    }
}
