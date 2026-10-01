using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Events;
using RoguePirates.V4.Narrative;
using RoguePirates.V4.Spawning;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>M13: barks (cooldowns, priority, quiet rules) and the tutorial (strict order, skips, saved progress).</summary>
    [Category("V4")]
    public sealed class V4NarrativeM13Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        const float Dt = 1f / 60f;

        static ConfigSetRuntime Default() => new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));

        sealed class Run : IRunStatus
        {
            public float Time;
            public bool Paused, Ended;
            public bool Exists => true;
            public float ElapsedSeconds => Time;
            public bool IsPaused => Paused;
            public bool IsRunEnded => Ended;
            public bool IsGameplayActive => !Paused && !Ended;
            public bool IsBossFight => false;
            public int PlayerLevel => 0;
            public int Kills => 0;
        }

        sealed class Store : ITutorialStore { public int Progress { get; set; } }

        sealed class Power : RoguePirates.V4.Difficulty.IPowerInputs
        {
            public float HullValue = 100f;
            public System.Collections.Generic.IReadOnlyList<RoguePirates.V4.Weapons.WeaponSlot> Weapons => null;
            public RoguePirates.V4.Weapons.IWeaponModifiers Modifiers => null;
            public float Hull => HullValue;
            public float MaxHull => 100f;
            public float IncomingDamageMultiplier => 1f;
        }

        sealed class Rig
        {
            public CombatEvents Events = new CombatEvents();
            public Run Run = new Run();
            public NarrativeView View = new NarrativeView();
            public BarkDirector Barks;
            public TutorialDirector Tutorial;
            public Store Store = new Store();
            public Power Power = new Power();
            int _tick;

            public void Publish(CombatEventKind kind, int a = 0, int b = 0, string tag = null) =>
                Events.Publish(new CombatEvent(kind, _tick, Run.Time, a, b, 0f, 0f, 0f, tag));

            /// <summary>Advances the run clock, ticking both directors and delivering events each tick. While the run is
            /// paused or over the clock stands still, so the same span is covered in ticks instead (bounded loop).</summary>
            public void Advance(float seconds)
            {
                var ticks = Mathf.Max(1, Mathf.CeilToInt(seconds / Dt));
                for (var i = 0; i < ticks; i++)
                {
                    Events.EndTick();
                    var t = new SimTick(_tick, Dt, Run.Time);
                    Barks.Tick(t);
                    Tutorial.Tick(t);
                    _tick++;
                    if (!Run.Paused && !Run.Ended) Run.Time += Dt;
                }
            }
        }

        static Rig Make(ConfigSetRuntime set, int progress = 0, int seed = 1)
        {
            var r = new Rig();
            r.Store.Progress = progress;
            var context = new RunContext(new RunClock(), r.Events, set);
            context.Register<IRunStatus>(r.Run);
            context.Register<RoguePirates.V4.Difficulty.IPowerInputs>(r.Power);
            r.Barks = new BarkDirector { View = r.View };
            r.Tutorial = new TutorialDirector { View = r.View, Store = r.Store };
            r.Barks.Initialize(context);
            r.Tutorial.Initialize(context);
            r.Barks.BeginRun(new RunStart(seed, false));
            r.Tutorial.BeginRun(new RunStart(seed, false));
            return r;
        }

        [Test]
        public void Config_HasAboutThirtyBarks_AllSpeakers_AndNineTutorialSteps()
        {
            using var set = Default();
            var c = set.Single<NarrativeConfig>();
            Assert.GreaterOrEqual(c.barks.Count, 28);
            foreach (BarkSpeaker s in System.Enum.GetValues(typeof(BarkSpeaker))) Assert.IsTrue(c.barks.Any(b => b.speaker == s), s.ToString());
            Assert.IsTrue(c.barks.All(b => b.lines.Length > 0 && b.lines.All(l => !string.IsNullOrWhiteSpace(l))));
            Assert.AreEqual(9, c.tutorial.Count);
            Assert.AreEqual("TERRITORY CLAIMED: SURVIVE AS LONG AS YOU CAN.", c.tutorial[7].prompt);
            Assert.AreEqual(TutorialTrigger.RunEnded, c.tutorial[8].completesOn);
            StringAssert.Contains("Rum", c.tutorial[8].prompt);
            Assert.IsTrue(c.barks.Where(b => b.speaker == BarkSpeaker.Chipmunk).All(b => b.lines.All(l => l.StartsWith("*Chippy"))), "Chippy is silent: action captions");
            Assert.AreEqual("Chippy", c.chipmunkName);
            // Every Captain bark has each of the six Captains' own lines (from the Narrative Bible).
            foreach (var b in c.barks.Where(x => x.speaker == BarkSpeaker.Captain))
                foreach (var key in new[] { "rook", "brine", "richie", "flint", "ember", "roxie" })
                    Assert.IsTrue(b.captainLines.Any(x => x.captainKey == key && x.lines.Length > 0), $"{b.barkId}: {key}");
        }

        [Test]
        public void Tutorial_StepsCompleteOnlyInOrder_AndProgressIsSaved()
        {
            using var set = Default();
            var r = Make(set);
            Assert.IsTrue(r.Tutorial.Active);
            Assert.AreEqual(1, r.View.PromptSerial, "step 1's prompt shows at once");
            StringAssert.Contains("Sail", r.View.PromptText);

            // Events for later steps do not complete anything yet (they are remembered for when their step comes).
            r.Publish(CombatEventKind.ChestOpened);
            r.Advance(1f);
            Assert.AreEqual(0, r.Tutorial.Index, "out of order: nothing completes");
            Assert.IsFalse(r.Tutorial.Complete(TutorialTrigger.LevelUp), "only the current step's trigger counts");

            Assert.IsTrue(r.Tutorial.Complete(TutorialTrigger.ShipMoved));
            Assert.AreEqual(1, r.Store.Progress, "saved");
            Assert.IsFalse(r.Tutorial.Complete(TutorialTrigger.ShipMoved), "a step completes once");
            Assert.IsTrue(r.Tutorial.Complete(TutorialTrigger.XpCollected));

            // Level-up: the prompt shows over the cards, and the step completes when they close (the run resumes).
            r.Run.Paused = true;
            r.Publish(CombatEventKind.LevelUp, 1);
            r.Publish(CombatEventKind.OffersShown, 1);
            var shownBefore = r.View.PromptSerial;
            r.Advance(1f);
            StringAssert.Contains("pick one card", r.View.PromptText, "shown over the cards");
            Assert.Greater(r.View.PromptSerial, shownBefore);
            Assert.AreEqual(2, r.Tutorial.Index, "not while the cards are open");
            r.Run.Paused = false;
            r.Advance(0.1f);
            // The chest was opened earlier this run: its step completes as soon as it is current.
            Assert.AreEqual(4, r.Tutorial.Index, "level-up done, and the chest step (already seen) completed");
            r.Publish(CombatEventKind.LandmarkUsed, 4);
            r.Advance(0.1f);
            Assert.AreEqual(5, r.Tutorial.Index);
            r.Publish(CombatEventKind.BossState, 1);
            r.Advance(0.1f);
            Assert.IsTrue(r.Tutorial.Complete(TutorialTrigger.BossTelegraph));

            var before = r.View.PromptSerial;
            r.Advance(1f);
            Assert.AreEqual(before, r.View.PromptSerial, "no claim prompt before the boss dies");
            r.Publish(CombatEventKind.BossState, 2);
            r.Advance(0.1f);
            Assert.AreEqual(8, r.Tutorial.Index, "the claim step completed (silently: the banner is the stage's first-conquest toast)");

            r.Tutorial.EndRun(new RunEnd(100f, true)); // left mid-run (restart, menu): not a death
            Assert.AreEqual(8, r.Tutorial.Index, "leaving is not the death summary");
            r.Run.Ended = true;
            r.Tutorial.EndRun(new RunEnd(100f, true));
            StringAssert.Contains("Rum", r.View.PromptText, "the death summary");
            Assert.AreEqual(9, r.Store.Progress);
            Assert.IsFalse(r.Tutorial.Active, "done");

            Assert.IsFalse(Make(set, r.Store.Progress).Tutorial.Active, "a finished tutorial never shows again");
            var resumed = Make(set, 3);
            Assert.AreEqual(3, resumed.Tutorial.Index, "a later run continues where the last stopped");
            StringAssert.Contains("chest", resumed.View.PromptText);
        }

        /// <summary>Review fix: a player who activates the Beacon or sinks the boss early is caught up, not nagged.</summary>
        [Test]
        public void Tutorial_CatchesUp_WhenTheBossComesEarly()
        {
            using var set = Default();
            var r = Make(set, 3); // on the chest step
            r.Publish(CombatEventKind.BossState, 1);
            r.Advance(0.1f);
            Assert.AreEqual(6, r.Tutorial.Index, "chest and landmark skipped, the Beacon step completed");
            CollectionAssert.AreEqual(new[] { "chest", "landmark" }, r.Tutorial.Skipped);
            r.Publish(CombatEventKind.BossState, 2);
            r.Advance(0.1f);
            Assert.AreEqual(8, r.Tutorial.Index, "telegraph skipped, the claim completed");
            Assert.IsTrue(r.Tutorial.Current == null || r.Tutorial.Current.stepId == "death_summary", "caught up to the death summary");
        }

        [Test]
        public void Tutorial_SkipsAStalledStep_WithoutCompletingIt_AndIsOffInTheLab()
        {
            using var set = Default();
            var core = Make(set, 1); // XP step: core, never skipped (playtest round 1)
            core.Advance(400f);
            Assert.AreEqual(1, core.Tutorial.Index, "core steps wait for the player");

            var r = Make(set, 3); // chest step: optional, skipped after 150 s
            r.Advance(155f);
            Assert.AreEqual(4, r.Tutorial.Index);
            CollectionAssert.Contains(r.Tutorial.SkippedThisRun, "chest");

            var lab = new TutorialDirector { View = new NarrativeView(), Store = new Store(), LabMode = true };
            var context = new RunContext(new RunClock(), new CombatEvents(), set);
            lab.Initialize(context);
            lab.BeginRun(new RunStart(1, false));
            Assert.IsFalse(lab.Active);
        }

        [Test]
        public void Barks_RespectCooldowns_Priority_AndStayQuietWhilePaused()
        {
            using var set = Default();
            var r = Make(set, 99);
            r.Advance(3.2f);
            Assert.AreEqual(1, r.View.BarkSerial, "the run-start bark");
            Assert.AreEqual("Penny", r.View.BarkSpeaker, "priority: Penny's start line (5) beats the Captain's (4)");

            // Global cooldown: a level-up 2 s later waits; its window (3 s) passes, so it is dropped.
            r.Publish(CombatEventKind.LevelUp, 2);
            r.Advance(4f);
            Assert.AreEqual(1, r.View.BarkSerial, "global cooldown");

            // Paused (e.g. the level-up cards): nothing plays; the due bark waits and plays once the pause ends.
            // (Twenty quiet seconds first, with an odd kill so the "quiet" barks stay silent: every cooldown has passed.)
            for (var i = 0; i < 10; i++) { r.Publish(CombatEventKind.Killed, 500 + i); r.Advance(2f); }
            var serial = r.View.BarkSerial;
            r.Run.Paused = true;
            r.Publish(CombatEventKind.LevelUp, 3);
            r.Advance(5f);
            Assert.AreEqual(serial, r.View.BarkSerial, "paused: quiet");
            r.Run.Paused = false;
            r.Advance(0.5f);
            Assert.AreEqual(serial + 1, r.View.BarkSerial, "the level-up bark plays when the cards close");
            r.Run.Ended = true;
            r.Publish(CombatEventKind.ChestOpened);
            r.Advance(0.5f);
            Assert.AreEqual(serial + 1, r.View.BarkSerial, "nothing after the run ends");
            r.Run.Ended = false;
            r.Advance(12f);

            // A boss victory plays its bark (a milestone: it waits out the global cooldown if another bark just played)
            // and silences the others for a while.
            r.Publish(CombatEventKind.BossState, 2);
            r.Advance(11f);
            StringAssert.StartsWith("boss_defeated_", r.View.BarkId, "the boss-victory bark (Penny's or the chipmunk's) played after the quiet window");
        }

        [Test]
        public void Barks_KillStreak_PerBarkCooldown_OncePerRun_Replay_AndConsolePlay()
        {
            using var set = Default();
            List<string> Barks(int seed)
            {
                var r = Make(set, 99, seed);
                var ids = new List<string>();
                var seen = 0;
                for (var s2 = 0; s2 < 60; s2++)
                {
                    for (var k = 0; k < 3; k++) r.Publish(CombatEventKind.Killed, 100 + s2 * 3 + k);
                    r.Advance(1f);
                    if (r.View.BarkSerial != seen) { seen = r.View.BarkSerial; ids.Add($"{r.Run.Time:0}:{r.View.BarkId}:{r.View.BarkLine}"); }
                }
                return ids;
            }
            var a2 = Barks(7);
            CollectionAssert.AreEqual(a2, Barks(7), "same seed, same barks");
            var streaks = a2.Where(x => x.Contains(":streak_captain:")).Select(x => float.Parse(x.Split(':')[0], System.Globalization.CultureInfo.InvariantCulture)).ToList();
            Assert.GreaterOrEqual(streaks.Count, 1, "a kill streak bark");
            for (var i = 1; i < streaks.Count; i++) Assert.GreaterOrEqual(streaks[i] - streaks[i - 1], 45f - 1f, "its own 45 s cooldown");
            Assert.AreEqual(1, a2.Count(x => x.Contains(":start_penny:")), "once per run");

            var once = Make(set, 99);
            Assert.IsTrue(once.Barks.Play("boss_summoned_king"));
            Assert.AreEqual("Barnacle King", once.View.BarkSpeaker);
            Assert.AreEqual("boss_summoned_king", once.View.BarkId);
            Assert.IsFalse(once.Barks.Play("no_such_bark"));
        }

        [Test]
        public void Barks_HullLow_AndPhaseTags()
        {
            using var set = Default();
            var r = Make(set, 99);
            var unit = 1000;
            void Wait(int seconds) { for (var i = 0; i < seconds / 2; i++) { r.Publish(CombatEventKind.Killed, unit++); r.Advance(2f); } } // an odd kill keeps "quiet" barks silent
            Wait(30); // past the run-start barks (the Captain's follows Penny's after the global cooldown)
            r.Power.HullValue = 25f;
            r.Publish(CombatEventKind.PlayerDamaged, 0, 0);
            r.Advance(0.2f);
            Assert.AreEqual("hull_low_penny", r.View.BarkId, "hull at 25 %");

            Wait(16);
            r.Publish(CombatEventKind.PhaseChanged, 0, 1, "band_1"); // an untagged-for-it phase
            r.Advance(0.2f);
            Assert.AreNotEqual("elite_hammer_penny", r.View.BarkId);
            r.Publish(CombatEventKind.PhaseChanged, 0, 1, set.Single<NarrativeConfig>().barks.First(b => b.barkId == "elite_hammer_penny").tag);
            r.Advance(0.2f);
            Assert.AreEqual("elite_hammer_penny", r.View.BarkId, "its own phase");

            var untagged = new BarkRule { barkId = "x", trigger = BarkTrigger.PhaseStarted, lines = new[] { "x" } };
            set.Single<NarrativeConfig>().barks.Add(untagged);
            try
            {
                Wait(16);
                r.Publish(CombatEventKind.PhaseChanged, 0, 1, "band_2");
                r.Advance(0.2f);
                Assert.AreNotEqual("x", r.View.BarkId, "an untagged phase bark never fires");
            }
            finally { set.Single<NarrativeConfig>().barks.Remove(untagged); }
        }

        /// <summary>The narrative never pauses or blocks anything, and V3's onboarding is gone from V4 runs.</summary>
        [Test]
        public void Narrative_NeverPauses_AndV3OnboardingIsRetired()
        {
            var narrative = Path.Combine(Application.dataPath, "_Game/V4/Runtime/Narrative");
            foreach (var f in Directory.GetFiles(narrative, "*.cs"))
            {
                var text = File.ReadAllText(f);
                Assert.IsFalse(text.Contains("SetPause") || text.Contains("PauseReason.") || text.Contains("SetState(") || text.Contains(".Publish("), f + ": never pauses, sets run state or publishes");
            }
            foreach (var f in Directory.GetFiles(Path.Combine(Application.dataPath, "_Game/V4"), "*.cs", SearchOption.AllDirectories))
                Assert.IsFalse(File.ReadAllText(f).Contains("V3OnboardingDirector."), f + ": V3 onboarding call");
        }
    }
}
