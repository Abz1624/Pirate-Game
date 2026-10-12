using NUnit.Framework;

namespace RoguePirates.Hackathon.Tests
{
    /// <summary>Hackathon (owner, 12 Oct): the defence record and key stats lines, the practice verdict, and the captain
    /// name rules (default name detection and the server's name shape).</summary>
    public sealed class HackathonUxRulesTests
    {
        [Test]
        public void DefenceRecordLine_NoRaidsYet_ThenPercentRoundedDown()
        {
            Assert.AreEqual("No raids yet", IslandRules.DefenceRecordLine(null));
            Assert.AreEqual("No raids yet", IslandRules.DefenceRecordLine(new DefenceRecord()));
            Assert.AreEqual("100% (1 of 1 raid defended)", IslandRules.DefenceRecordLine(new DefenceRecord { raidsAgainst = 1, defended = 1 }));
            Assert.AreEqual("66% (2 of 3 raids defended)", IslandRules.DefenceRecordLine(new DefenceRecord { raidsAgainst = 3, defended = 2, fallen = 1 }));
            Assert.AreEqual("0% (0 of 2 raids defended)", IslandRules.DefenceRecordLine(new DefenceRecord { raidsAgainst = 2, fallen = 2 }));
            Assert.IsNull(IslandRules.SuccessPercent(0, 0));
            Assert.AreEqual(75, IslandRules.SuccessPercent(3, 4));
        }

        [Test]
        public void KeyStats_ShowRumPerHourAndTheRecord()
        {
            Assert.AreEqual("180 RUM per hour   Defence: No raids yet", IslandRules.KeyStats(180, null));
            Assert.AreEqual("120 RUM per hour   Defence: 50% (1 of 2 raids defended)",
                IslandRules.KeyStats(120, new DefenceRecord { raidsAgainst = 2, defended = 1, fallen = 1 }));
        }

        [Test]
        public void PracticeVerdict_FollowsTheConquestRules()
        {
            StringAssert.StartsWith("Your defences fell", IslandRules.PracticeVerdict(true, 9, 5));
            StringAssert.StartsWith("Partly held", IslandRules.PracticeVerdict(false, 5, 5));
            Assert.AreEqual("Your defences held: the raid ended in minute 3, before the guardian.", IslandRules.PracticeVerdict(false, 3, 5));
            Assert.AreEqual("Your defences held: the raid ended in minute 1, before the guardian.", IslandRules.PracticeVerdict(false, 0, 5));
        }

        [Test]
        public void GuardianClockText_MinuteFiveArrivesAtFourMinutes()
        {
            Assert.AreEqual("4:00", IslandRules.GuardianArrival(5));
            Assert.AreEqual("1:00", IslandRules.GuardianArrival(2));
            Assert.AreEqual("14:00", IslandRules.GuardianArrival(15));
            Assert.AreEqual("Minute 5 (arrives 4:00)", IslandRules.GuardianMinuteLabel(5));
        }

        [Test]
        public void GuardianLine_CountsDown_ThenRising_ThenUp_ThenDiving()
        {
            Assert.AreEqual("Guardian: Barnacle King rises at 4:00 (in 1:23)", IslandRules.GuardianLine("Barnacle King", 157, 240, false, -1f));
            Assert.AreEqual("Guardian: Barnacle King rises at 4:00 (in 4:00)", IslandRules.GuardianLine(null, 0, 240, false, -1f));
            Assert.AreEqual("Guardian: Barnacle King is rising now", IslandRules.GuardianLine("Barnacle King", 240, 240, false, -1f));
            Assert.AreEqual("Guardian is up: sink it to win (64% hull)", IslandRules.GuardianLine("Barnacle King", 300, 240, true, 0.635f));
            StringAssert.StartsWith("Guardian: Barnacle King is diving", IslandRules.GuardianLine("Barnacle King", 320, 240, true, -1f));
            Assert.AreEqual("No beacon here: the Barnacle King rises on his own at 4:00. Sink him to win.", IslandRules.GuardianNotice("Barnacle King", 240));
        }

        [Test]
        public void IsDefaultName_MatchesTheServersCaptainPlusFourAddressCharacters()
        {
            Assert.IsTrue(Web3Rules.IsDefaultName("Captain 7xKX"));
            Assert.IsTrue(Web3Rules.IsDefaultName("Captain 9fQe"));
            Assert.IsTrue(Web3Rules.IsDefaultName("Captain Jack"));  // the shape alone cannot tell
            Assert.IsFalse(Web3Rules.IsDefaultName("Captain Jack", "7xKXtg2CW87d97TXJSDpbD5jBkheTqA83TZRuJosg9fQe"));
            Assert.IsTrue(Web3Rules.IsDefaultName("Captain 7xKX", "7xKXtg2CW87d97TXJSDpbD5jBkheTqA83TZRuJosg9fQe"));
            Assert.IsTrue(Web3Rules.IsDefaultName("Captain 9fQe", "7xKXtg2CW87d97TXJSDpbD5jBkheTqA83TZRuJosg9fQe"));
            Assert.IsFalse(Web3Rules.IsDefaultName("Captain 0OIl"));
            Assert.IsFalse(Web3Rules.IsDefaultName("Captain 7xK"));
            Assert.IsFalse(Web3Rules.IsDefaultName("Red Beard"));
            Assert.IsFalse(Web3Rules.IsDefaultName(null));
        }

        [Test]
        public void CleanName_FollowsTheServerNameRules()
        {
            Assert.AreEqual("Red Beard", Web3Rules.CleanName("  Red Beard ", out var e1));
            Assert.IsNull(e1);
            Assert.AreEqual("a_b-c 1", Web3Rules.CleanName("a_b-c 1", out _));
            Assert.IsNull(Web3Rules.CleanName("ab", out var e2));
            Assert.AreEqual("Names are 3 to 16 characters.", e2);
            Assert.IsNull(Web3Rules.CleanName("12345678901234567", out _));
            Assert.IsNull(Web3Rules.CleanName("Bad!Name", out var e3));
            Assert.AreEqual("Use letters, digits, spaces, _ or - only.", e3);
            Assert.IsNull(Web3Rules.CleanName(null, out _));
        }
    }
}
