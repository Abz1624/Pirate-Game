using HighSeas.Balance;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.UI;
using HighSeas.V3;
using NUnit.Framework;
using UnityEngine;

namespace HighSeas.Tests
{
    public class PlaytestFeedbackGateTests
    {
        [Test]
        public void LogbookOffer_HullUsesFlatMaxHull_NotPercent()
        {
            var def = ScriptableObject.CreateInstance<LogbookDefinition>();
            def.id = "carpenters_notes";
            def.displayName = "Carpenter's Notes";
            def.primaryStat = StatId.Hull;
            def.perLevelAdditive = 10f;

            var offer = LevelOfferGenerator.BuildLogbookOffer(def, 0, Rarity.Common);
            Assert.That(offer.description, Does.Contain("+10 Max Hull"));
            Assert.That(offer.description, Does.Not.Contain("1000%"));
        }

        [Test]
        public void StatDisplay_PercentStat_FormatsAsPercent()
        {
            var line = StatDisplay.FormatLogbookBonus(StatId.Damage, 0.08f);
            Assert.That(line, Is.EqualTo("+8% Damage"));
        }

        [Test]
        public void V3LogbookCatalog_HasTenPlaytestBooks()
        {
            Assert.That(V3LogbookCatalog.AllIds.Length, Is.EqualTo(10));
        }

        [Test]
        public void EnemyTierLogic_BlocksGeneralBeforeLevelFive()
        {
            Assert.That(EnemyTierLogic.CanSpawnTier(EnemyTier.General, 400f, 4), Is.False);
            Assert.That(EnemyTierLogic.CanSpawnTier(EnemyTier.General, 400f, 5), Is.True);
        }

        [Test]
        public void RarityRoll_AtLuckZero_CommonIsMajority()
        {
            var rng = new RunRng(42);
            var counts = new int[5];
            for (var i = 0; i < 2000; i++)
            {
                var r = rng.RollRarity(0f);
                counts[(int)r]++;
            }

            var commonPct = counts[0] / 2000f * 100f;
            Assert.That(commonPct, Is.InRange(55f, 65f));
        }

        [Test]
        public void SaltwindProfile_BalanceVersionIsThree()
        {
#if UNITY_EDITOR
            var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(
                "Assets/_Game/Data/Balance/SaltwindSoloStageBalanceProfile.asset");
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.balanceVersion, Is.EqualTo(3));
            Assert.That(profile.logbookSlotCap, Is.EqualTo(4));
#else
            Assert.Pass();
#endif
        }
    }
}
