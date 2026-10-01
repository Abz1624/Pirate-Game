using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    /// <summary>Who says a bark. The chipmunk is silent: its lines are action captions.</summary>
    public enum BarkSpeaker { Penny = 0, Chipmunk = 1, BarnacleKing = 2, Captain = 3 }

    /// <summary>What sets a bark off (V4 combat events or run state; no enemy or item ids in code).</summary>
    public enum BarkTrigger
    {
        RunStart = 0,          // a few seconds into the run
        KillStreak = 1,        // value kills within windowSeconds
        LevelUp = 2,
        ChestOpened = 3,
        LandmarkUsed = 4,
        BeaconHint = 5,        // BeaconState ≥ 1 (direction or marker)
        BeaconSpotted = 6,     // BeaconState 3
        BossSummoned = 7,      // BossState 1
        BossDefeated = 8,      // BossState 2
        PhaseStarted = 9,      // PhaseChanged start with Tag == tag (e.g. an elite beat, overtime)
        PhaseWarning = 10,     // Warning with Tag == tag (e.g. the storm)
        HullLow = 11,          // hull fraction at or below value after a hit
        GoldAtLeast = 12,      // run gold reaches value
        Quiet = 13,            // no kill for value seconds
    }

    /// <summary>A Captain's own lines for a bark (the selected Captain speaks in their voice).</summary>
    [Serializable]
    public sealed class CaptainLines
    {
        [Tooltip("CaptainConfig.captainKey (rook, brine, richie, flint, ember, roxie).")] public string captainKey = string.Empty;
        public string[] lines = Array.Empty<string>();
    }

    [Serializable]
    public sealed class BarkRule
    {
        public string barkId = "bark";
        public bool enabled = true;
        public BarkSpeaker speaker;
        public BarkTrigger trigger;
        [Tooltip("Trigger value (kills, hull fraction, gold, seconds).")] public float value;
        [Tooltip("KillStreak window (s).")] public float windowSeconds = 6f;
        [Tooltip("PhaseStarted / PhaseWarning: the phase id (an untagged phase bark never fires).")] public string tag = string.Empty;
        [Tooltip("One is picked at random (the narrative's own stream).")] public string[] lines = Array.Empty<string>();
        [Tooltip("Captain barks: each Captain's own lines (else the lines above).")] public List<CaptainLines> captainLines = new List<CaptainLines>();
        [Tooltip("Seconds before this bark can play again.")] public float cooldownSeconds = 60f;
        [Tooltip("Higher wins when several are due at once.")] public int priority;
        public bool oncePerRun;
    }

    /// <summary>What completes a tutorial step. Steps complete strictly in order: a trigger only counts for the current step.</summary>
    public enum TutorialTrigger
    {
        ShipMoved = 0,         // sailing faster than 2 m/s for a moment
        XpCollected = 1,       // any XP (or a level)
        LevelUp = 2,
        ChestOpened = 3,
        LandmarkUsed = 4,
        BossSummoned = 5,      // the Beacon activated
        BossTelegraph = 6,     // the boss telegraphs an attack
        BossDefeated = 7,
        RunEnded = 8,
    }

    [Serializable]
    public sealed class TutorialStep
    {
        public string stepId = "step";
        [TextArea] public string prompt = string.Empty;
        public TutorialTrigger completesOn;
        [Tooltip("The prompt stays this long (it is shown again, briefly, while the step waits).")] public float promptSeconds = 6f;
        [Tooltip("Skipped (not completed) after this long as the current step, so the tutorial never stalls (0 = never).")] public float skipAfterSeconds;
        [Tooltip("Show the prompt when the step completes (the claim banner, the death summary) instead of when it becomes current.")] public bool showOnComplete;
        [Tooltip("Completes without showing its prompt (the claim banner is the stage's first-conquest toast).")] public bool silent;
    }

    /// <summary>
    /// Barks and the First Conquest tutorial (M13, gameplay doc §11b). Barks are short character beats on events, with
    /// per-bark and global cooldowns; they never pause or block combat and stay quiet while the run is paused. The
    /// tutorial is nine neutral prompts in strict order, progress kept under a V4 save key. The lines here are
    /// placeholders in the GDD's voice until the Narrative Bible is the source.
    /// </summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Narrative", fileName = "Narrative")]
    public sealed class NarrativeConfig : V4ConfigAsset
    {
        public bool barksEnabled = true;
        public bool tutorialEnabled = true;
        [Tooltip("At least this long between any two barks.")] public float globalCooldownSeconds = 10f;
        [Tooltip("How long a bark stays on screen.")] public float barkSeconds = 3.5f;
        [Tooltip("No barks for this long after the boss dies (the claim banner has the screen).")] public float quietAfterBossSeconds = 6f;
        [Tooltip("Speaker names shown with barks (the chipmunk's name is still open).")] public string pennyName = "Penny", chipmunkName = "Chippy", bossName = "Barnacle King";

        public List<BarkRule> barks = DefaultBarks();
        public List<TutorialStep> tutorial = DefaultTutorial();

        public override void Validate(ConfigValidationReport report)
        {
            var ids = new HashSet<string>();
            foreach (var b in barks)
            {
                if (b == null) { report.Error(this, nameof(barks), "null bark"); continue; }
                if (string.IsNullOrWhiteSpace(b.barkId) || !ids.Add(b.barkId)) report.Error(this, nameof(barks), $"missing or duplicate bark id '{b.barkId}'");
                var hasCaptainLines = b.captainLines != null && b.captainLines.Exists(c => c != null && c.lines != null && c.lines.Length > 0);
                if ((b.lines == null || b.lines.Length == 0) && !hasCaptainLines) report.Error(this, nameof(barks), $"bark '{b.barkId}' has no lines");
                if (b.cooldownSeconds < 0f) report.Error(this, nameof(barks), $"bark '{b.barkId}': negative cooldown");
            }
            ids.Clear();
            foreach (var s in tutorial)
            {
                if (s == null) { report.Error(this, nameof(tutorial), "null step"); continue; }
                if (string.IsNullOrWhiteSpace(s.stepId) || !ids.Add(s.stepId)) report.Error(this, nameof(tutorial), $"missing or duplicate step id '{s.stepId}'");
                if (string.IsNullOrWhiteSpace(s.prompt)) report.Error(this, nameof(tutorial), $"step '{s.stepId}' has no prompt");
            }
        }

        static BarkRule B(string id, BarkSpeaker who, BarkTrigger when, float value, float cooldown, int priority, params string[] lines) =>
            new BarkRule { barkId = id, speaker = who, trigger = when, value = value, cooldownSeconds = cooldown, priority = priority, lines = lines };

        /// <summary>A Captain bark: the six Captains' own lines (keys: rook, brine = Hank, richie, flint = Sparky, ember, roxie).</summary>
        static BarkRule C(string id, BarkTrigger when, float value, float cooldown, int priority,
            string[] rook, string[] hank, string[] richie, string[] sparky, string[] ember, string[] roxie)
        {
            var rule = B(id, BarkSpeaker.Captain, when, value, cooldown, priority, rook);
            void Add(string key, string[] lines) => rule.captainLines.Add(new CaptainLines { captainKey = key, lines = lines });
            Add("rook", rook); Add("brine", hank); Add("richie", richie); Add("flint", sparky); Add("ember", ember); Add("roxie", roxie);
            return rule;
        }

        static string[] L(params string[] lines) => lines;

        /// <summary>
        /// The barks, from the Narrative Bible v1.1 (Rum for Gold where it means the meta currency): Penny practical, dry and
        /// increasingly tired; Chippy silent — action captions — and unable to resist anything shiny; the Barnacle King a
        /// petty landlord treating piracy as a property dispute; each Captain in their own voice. Short, no pirate dialect,
        /// no one explaining game systems.
        /// </summary>
        public static List<BarkRule> DefaultBarks()
        {
            var list = new List<BarkRule>
            {
                B("start_penny", BarkSpeaker.Penny, BarkTrigger.RunStart, 0f, 9999f, 5, "Try to bring the ship back this time.", "The repair bill's already open. Just in case.", "Get rich. Come home. In that order."),
                C("start_captain", BarkTrigger.RunStart, 0f, 9999f, 4,
                    L("This one's going to be a story.", "Exactly as planned."), L("Ship's sound. Let's go.", "Nobody's running today."),
                    L("Let's go make some money.", "Every coin comes home with me."), L("I have an idea.", "Brought extra powder. Don't ask."),
                    L("Let's see what catches.", "Nice and slow."), L("Let's find something fast.", "Closer is better.")),
                C("streak_captain", BarkTrigger.KillStreak, 8f, 45f, 3,
                    L("Exactly as planned.", "They'll write songs about this."), L("Still floating.", "Next."),
                    L("That's profit sinking. Theirs."), L("That was a small one.", "Bigger next time."),
                    L("Let it spread.", "There. Much better."), L("Again.", "Closer next time.")),
                B("streak_penny", BarkSpeaker.Penny, BarkTrigger.KillStreak, 15f, 90f, 2, "That's a lot of wreckage. None of it ours, for once."),
                B("level_penny", BarkSpeaker.Penny, BarkTrigger.LevelUp, 0f, 60f, 1, "Another upgrade. I'll pretend it was budgeted.", "Fine. Bolt it on."),
                C("level_captain", BarkTrigger.LevelUp, 0f, 75f, 1,
                    L("Even better than planned."), L("Sturdier. Good."), L("An investment. Technically."),
                    L("Ooh. That's going to be loud."), L("Needs another minute."), L("Faster. Good.")),
                B("chest_chippy", BarkSpeaker.Chipmunk, BarkTrigger.ChestOpened, 0f, 60f, 3, "*Chippy's cheeks are suddenly very full*", "*Chippy inspects the chest, then the Captain's pockets*"),
                B("chest_penny", BarkSpeaker.Penny, BarkTrigger.ChestOpened, 0f, 90f, 2, "Salvage. Legally, probably ours."),
                B("landmark_penny", BarkSpeaker.Penny, BarkTrigger.LandmarkUsed, 0f, 60f, 2, "Useful. Don't get used to useful."),
                B("landmark_chippy", BarkSpeaker.Chipmunk, BarkTrigger.LandmarkUsed, 0f, 90f, 1, "*Chippy pockets a compass part. Nobody saw.*"),
                B("beacon_hint_penny", BarkSpeaker.Penny, BarkTrigger.BeaconHint, 1f, 9999f, 4, "The Beacon's out there. So is whoever thinks he owns it."),
                B("beacon_spotted_penny", BarkSpeaker.Penny, BarkTrigger.BeaconSpotted, 3f, 9999f, 6, "There's the Treasury Beacon. The sea keeps receipts, Captain."),
                B("boss_summoned_king", BarkSpeaker.BarnacleKing, BarkTrigger.BossSummoned, 0f, 9999f, 8, "Your ship is improperly docked.", "That treasure is unpaid tax.", "Everything in Saltwind is MY property."),
                B("boss_summoned_penny", BarkSpeaker.Penny, BarkTrigger.BossSummoned, 0f, 9999f, 7, "That's the Barnacle King. He'll want paperwork."),
                C("boss_summoned_captain", BarkTrigger.BossSummoned, 0f, 9999f, 6,
                    L("We meet again. Probably."), L("Not moving."), L("He wants tax? From me?"),
                    L("I have an idea."), L("He'll catch."), L("Now it's interesting.")),
                B("boss_defeated_penny", BarkSpeaker.Penny, BarkTrigger.BossDefeated, 0f, 9999f, 9, "We're done. Come home.", "Objective secured. The sensible thing now is to stop."),
                B("boss_defeated_chippy", BarkSpeaker.Chipmunk, BarkTrigger.BossDefeated, 0f, 9999f, 8, "*Chippy claims the Barnacle King's crown. It is shiny.*"),
                C("boss_defeated_captain", BarkTrigger.BossDefeated, 0f, 9999f, 7,
                    L("Exactly as planned. Now, about that chest..."), L("Told you. No dodging needed."), L("Now we collect."),
                    L("Can I keep the crown? For science."), L("There. Much better."), L("Again.")),
                B("elite_hammer_penny", BarkSpeaker.Penny, BarkTrigger.PhaseStarted, 0f, 9999f, 5, "That hammerhead looks personally offended."),
                B("elite_kraken_penny", BarkSpeaker.Penny, BarkTrigger.PhaseStarted, 0f, 9999f, 5, "Kraken spawn. I'm not paying for tentacle damage."),
                B("reef_crab_penny", BarkSpeaker.Penny, BarkTrigger.PhaseStarted, 0f, 9999f, 5, "A reef crab. Armoured. Unreasonably so."),
                B("overtime_penny", BarkSpeaker.Penny, BarkTrigger.PhaseStarted, 0f, 9999f, 6, "We already won. We could leave. We won't. Noted."),
                B("storm_penny", BarkSpeaker.Penny, BarkTrigger.PhaseWarning, 0f, 9999f, 6, "Storm's coming. I've seen your sailing. This will be expensive."),
                B("hull_low_penny", BarkSpeaker.Penny, BarkTrigger.HullLow, 0.3f, 45f, 7, "The hull is mostly holes now.", "Repairs are getting expensive."),
                C("hull_low_captain", BarkTrigger.HullLow, 0.15f, 60f, 6,
                    L("All part of the plan."), L("No."), L("The ship looks fine."),
                    L("Structural integrity is overrated."), L("Just a little singed."), L("Closer.")),
                B("gold_penny", BarkSpeaker.Penny, BarkTrigger.GoldAtLeast, 150f, 9999f, 3, "Good news: Gold. Bad news: repairs."),
                B("gold_chippy", BarkSpeaker.Chipmunk, BarkTrigger.GoldAtLeast, 300f, 9999f, 2, "*Chippy eyes the Gold with professional interest*"),
                B("quiet_penny", BarkSpeaker.Penny, BarkTrigger.Quiet, 25f, 120f, 1, "Quiet. I don't like quiet."),
                B("quiet_chippy", BarkSpeaker.Chipmunk, BarkTrigger.Quiet, 40f, 150f, 1, "*Chippy naps in a coil of rope*"),
                B("streak_chippy", BarkSpeaker.Chipmunk, BarkTrigger.KillStreak, 20f, 120f, 1, "*Chippy applauds, then pockets a coin from the applause*"),
                B("level_chippy", BarkSpeaker.Chipmunk, BarkTrigger.LevelUp, 0f, 150f, 0, "*Chippy tests the new part with its teeth*"),
            };
            // Phase-tagged barks get their timeline phase ids from the generator (no content ids in schema code).
            foreach (var b in list) if (b.cooldownSeconds >= 9999f) b.oncePerRun = true;
            return list;
        }

        static TutorialStep T(string id, TutorialTrigger when, string prompt, float skipAfter = 0f, float seconds = 6f, bool onComplete = false) =>
            new TutorialStep { stepId = id, completesOn = when, prompt = prompt, skipAfterSeconds = skipAfter, promptSeconds = seconds, showOnComplete = onComplete };

        /// <summary>The nine First Conquest prompts (neutral UI text: characters never explain systems).</summary>
        public static List<TutorialStep> DefaultTutorial() => new List<TutorialStep>
        {
            T("move", TutorialTrigger.ShipMoved, "Sail with WASD or the left stick."),
            T("autofire_xp", TutorialTrigger.XpCollected, "Your guns fire on their own. Sink ships and sail over the XP they drop."),
            T("level_up", TutorialTrigger.LevelUp, "Level up: pick one card: a new weapon, an upgrade or a Logbook."),
            T("chest", TutorialTrigger.ChestOpened, "Salvage chests hold items. Sail up to one and hold E to open it.", 150f),
            T("landmark", TutorialTrigger.LandmarkUsed, "The minimap marks landmarks. Use one: a Repair Wreck, a Signal Buoy or a Treasure Chart.", 150f),
            T("beacon", TutorialTrigger.BossSummoned, "Find the Treasury Beacon (the minimap points to it) and hold E to call its guardian."),
            T("boss_telegraph", TutorialTrigger.BossTelegraph, "Marked water shows where the Barnacle King will strike. Sail out of it."),
            new TutorialStep { stepId = "claim", completesOn = TutorialTrigger.BossDefeated, prompt = "TERRITORY CLAIMED: SURVIVE AS LONG AS YOU CAN.", promptSeconds = 6f, showOnComplete = true, silent = true },
            T("death_summary", TutorialTrigger.RunEnded, "Leftover gold is banked as Rum. Rum is yours to keep: spend it on unlocks between voyages.", 0f, 8f, true),
        };
    }
}
