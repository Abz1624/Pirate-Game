using System.Collections.Generic;
using NUnit.Framework;
using RoguePirates.Hackathon.UI;
using RoguePirates.V4;
using RoguePirates.V4.Spawning;

namespace RoguePirates.Hackathon.Tests
{
    /// <summary>Hackathon (12 Oct): island defence designs (designer helpers, the raid's spawn schedule).</summary>
    public sealed class DefenceRulesTests
    {
        static readonly Dictionary<string, int> Costs = new Dictionary<string, int> { ["deckhand_dinghy"] = 1, ["cannonboat"] = 3, ["elite_hammerhead"] = 12 };

        static DefenceDesign Sample() => new DefenceDesign
        {
            windows = new[]
            {
                new DefenceWindow { minute = 3, enemies = new[] { new DefenceEnemy { kind = "cannonboat", count = 4 }, new DefenceEnemy { kind = "deckhand_dinghy", count = 0 } } },
                new DefenceWindow { minute = 1, enemies = new[] { new DefenceEnemy { kind = "deckhand_dinghy", count = 10 } } },
            },
            guardian = new DefenceGuardian { kind = "barnacle_king", minute = 2 },
        };

        [TearDown]
        public void Clear() { RaidSchedule.Clear(); RunRequest.Consume(); RaidSchedule.Clear(); }

        [Test]
        public void Normalize_FillsAllWindowsInOrder_DropsZeroCounts()
        {
            var d = DefenceRules.Normalize(Sample());
            Assert.AreEqual(15, d.windows.Length);
            for (var i = 0; i < 15; i++) { Assert.AreEqual(i + 1, d.windows[i].minute); Assert.AreEqual(i * 60, d.windows[i].startSecond); }
            Assert.AreEqual(1, d.windows[2].enemies.Length, "the zero count is dropped");
            Assert.AreEqual(12, DefenceRules.Cost(d.windows[2], Costs));
            Assert.AreEqual(10, DefenceRules.Cost(d.windows[0], Costs));
            Assert.AreEqual(0, d.windows[1].enemies.Length);
            Assert.AreEqual(2, d.guardian.minute);
            Assert.AreEqual(60, d.guardian.arrivesAtSecond);
            Assert.AreEqual(5, DefenceRules.Normalize(null).guardian.minute, "no design: the default guardian minute");
        }

        [Test]
        public void Add_ClampsAndRemovesAtZero()
        {
            var w = new DefenceWindow { minute = 1, enemies = new DefenceEnemy[0] };
            DefenceRules.Add(w, "cannonboat", -1);
            Assert.AreEqual(0, w.enemies.Length);
            DefenceRules.Add(w, "cannonboat", 5);
            DefenceRules.Add(w, "cannonboat", 100);
            Assert.AreEqual(60, w.enemies[0].count);
            DefenceRules.Add(w, "cannonboat", -60);
            Assert.AreEqual(0, w.enemies.Length);
        }

        [Test]
        public void ForSave_KeepsOnlyWindowsWithShips()
        {
            var save = DefenceRules.ForSave(DefenceRules.Normalize(Sample()));
            Assert.AreEqual(2, save.windows.Length);
            Assert.AreEqual(1, save.windows[0].minute);
            Assert.AreEqual(3, save.windows[1].minute);
            Assert.AreEqual(2, save.guardian.minute);
        }

        [Test]
        public void MinuteAt_OneMinuteWindows()
        {
            Assert.AreEqual(1, DefenceRules.MinuteAt(0));
            Assert.AreEqual(1, DefenceRules.MinuteAt(59.9));
            Assert.AreEqual(2, DefenceRules.MinuteAt(60));
            Assert.AreEqual(15, DefenceRules.MinuteAt(899));
            Assert.AreEqual(15, DefenceRules.MinuteAt(2000));
        }

        [Test]
        public void Arm_BuildsTheRaidSchedule_ForTheNextRunOnly()
        {
            Assert.IsFalse(RaidSchedule.Active);
            RaidHud.Arm(new RaidDefence { seed = 7, timeLimitSeconds = 960, windowSeconds = 60, design = Sample() }, true);
            Assert.IsFalse(RaidSchedule.Active, "requested, not running: the menu's voyage is not a raid yet");
            Assert.IsNotNull(RunRequest.PendingRaid);
            RunRequest.Consume(); // the raid voyage starts
            Assert.IsTrue(RaidSchedule.Active);
            Assert.IsTrue(RaidSchedule.Practice, "the practice flag rides with the plan");
            Assert.AreEqual(60f, RaidSchedule.GuardianSeconds);
            Assert.AreEqual(2, RaidSchedule.Entries.Count);
            Assert.AreEqual("deckhand_dinghy", RaidSchedule.Entries[0].Kind);
            Assert.AreEqual(0f, RaidSchedule.Entries[0].StartSecond);
            Assert.AreEqual(120f, RaidSchedule.Entries[1].StartSecond);
            Assert.AreEqual(4, RaidSchedule.Entries[1].Count);
            RunRequest.Consume(); // the next voyage
            Assert.IsFalse(RaidSchedule.Active, "a later voyage never inherits the raid");
        }

        /// <summary>13 Oct (review): a raid armed but never started (the run did not take it) cannot turn the next normal
        /// voyage into a raid, and the menu clears a waiting request.</summary>
        [Test]
        public void StaleRaid_NeverReachesALaterVoyage()
        {
            RaidHud.Arm(new RaidDefence { design = Sample() });
            RaidSchedule.Clear(); // back at the menu
            Assert.IsNull(RunRequest.PendingRaid);
            RunRequest.Consume();
            Assert.IsFalse(RaidSchedule.Active);
            RaidHud.Arm(new RaidDefence { design = Sample() });
            RunRequest.Consume(); // taken by the run it was armed for (here: not a raid run, e.g. a 1v1)
            RaidSchedule.Clear(); // that run's raid HUD did not attach: it clears
            RunRequest.Consume(); // the next normal voyage
            Assert.IsFalse(RaidSchedule.Active);
        }

        [Test]
        public void Problem_IsReadable()
        {
            Assert.AreEqual("Minute 3: over budget", DefenceRules.Problem(new DefenceProblem { minute = 3, code = "over_budget" }));
            Assert.AreEqual("bad guardian", DefenceRules.Problem(new DefenceProblem { minute = 0, message = "bad guardian" }));
        }
    }
}
