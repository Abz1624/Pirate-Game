using System.Collections;
using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HighSeas.Tests
{
    public class LevelUpPlayModeTests
    {
        [UnityTest]
        public IEnumerator LevelUpModal_ShowAndSelect_PausesRunManager()
        {
            var rmGo = new GameObject("RunManager");
            var rm = rmGo.AddComponent<RunManager>();
            rm.BeginRun(12345);

            var panel = new GameObject("LevelUpPanel");
            var card = new GameObject("Card0");
            card.transform.SetParent(panel.transform, false);
            card.AddComponent<Image>();
            card.AddComponent<Button>();
            panel.AddComponent<LevelUpModal>();

            var selected = false;
            Assert.That(LevelUpModal.Instance.TryShow(
                new List<LevelOffer>
                {
                    new()
                    {
                        id = "log_damage",
                        title = "Damage Log",
                        description = "+5% Damage",
                        kind = LevelOfferKind.Logbook,
                        isWeapon = false,
                        rarity = Rarity.Common
                    }
                },
                _ => selected = true), Is.True);

            Assert.That(selected, Is.False);

            yield return null;
            TeardownTestObject(panel);
            TeardownTestObject(rmGo);
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
