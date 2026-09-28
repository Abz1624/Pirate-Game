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
        [Tooltip("Speaker names shown with barks (the chipmunk's name is still open).")] public string pennyName = "Penny", chipmunkName = "The chipmunk", bossName = "Barnacle King";

        public List<BarkRule> barks = DefaultBarks();
        public List<TutorialStep> tutorial = DefaultTutorial();

        public override void Validate(ConfigValidationReport report)
        {
            var ids = new HashSet<string>();
            foreach (var b in barks)
            {
                if (b == null) { report.Error(this, nameof(barks), "null bark"); continue; }
                if (string.IsNullOrWhiteSpace(b.barkId) || !ids.Add(b.barkId)) report.Error(this, nameof(barks), $"missing or duplicate bark id '{b.barkId}'");
                if (b.lines == null || b.lines.Length == 0) report.Error(this, nameof(barks), $"bark '{b.barkId}' has no lines");
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

        /// <summary>About thirty placeholder barks (GDD voice: Penny dry and tired, the chipmunk silent and shameless, the
        /// Barnacle King petty, the Captain's own catchphrase). Replace from the Narrative Bible.</summary>
        public static List<BarkRule> DefaultBarks()
        {
            var list = new List<BarkRule>
            {
                B("start_penny", BarkSpeaker.Penny, BarkTrigger.RunStart, 0f, 9999f, 5, "Try to bring the ship back this time.", "I have the repair bill ready. Just in case."),
                B("start_captain", BarkSpeaker.Captain, BarkTrigger.RunStart, 0f, 9999f, 4, "Exactly as planned."),
                B("streak_captain", BarkSpeaker.Captain, BarkTrigger.KillStreak, 8f, 45f, 3, "Again.", "Exactly as planned.", "More."),
                B("streak_penny", BarkSpeaker.Penny, BarkTrigger.KillStreak, 15f, 90f, 2, "That's a lot of wreckage. None of it is ours. Good."),
                B("level_penny", BarkSpeaker.Penny, BarkTrigger.LevelUp, 0f, 60f, 1, "Another upgrade. I'll pretend that was budgeted.", "Fine. Bolt it on."),
                B("level_captain", BarkSpeaker.Captain, BarkTrigger.LevelUp, 0f, 75f, 1, "I have an idea.", "Better."),
                B("chest_chipmunk", BarkSpeaker.Chipmunk, BarkTrigger.ChestOpened, 0f, 60f, 3, "*stuffs something shiny into its cheeks*", "*inspects the chest, then the Captain's pockets*"),
                B("chest_penny", BarkSpeaker.Penny, BarkTrigger.ChestOpened, 0f, 90f, 2, "Salvage. Legally, probably ours."),
                B("landmark_penny", BarkSpeaker.Penny, BarkTrigger.LandmarkUsed, 0f, 60f, 2, "Useful. Don't get used to useful."),
                B("landmark_chipmunk", BarkSpeaker.Chipmunk, BarkTrigger.LandmarkUsed, 0f, 90f, 1, "*pockets a bolt nobody will miss*"),
                B("beacon_hint_penny", BarkSpeaker.Penny, BarkTrigger.BeaconHint, 1f, 9999f, 4, "The Beacon's out there. So is whoever owns it."),
                B("beacon_spotted_penny", BarkSpeaker.Penny, BarkTrigger.BeaconSpotted, 3f, 9999f, 6, "There's the Treasury Beacon. The sea keeps receipts, Captain."),
                B("boss_summoned_king", BarkSpeaker.BarnacleKing, BarkTrigger.BossSummoned, 0f, 9999f, 8, "Your ship is improperly docked in MY waters.", "Unlicensed vessel! Unpaid tax! Leave, or be fined."),
                B("boss_summoned_penny", BarkSpeaker.Penny, BarkTrigger.BossSummoned, 0f, 9999f, 7, "That's the Barnacle King. He'll want paperwork."),
                B("boss_defeated_penny", BarkSpeaker.Penny, BarkTrigger.BossDefeated, 0f, 9999f, 9, "Please stop there. You won't, but please.", "That was expensive. Worth it, apparently."),
                B("boss_defeated_chipmunk", BarkSpeaker.Chipmunk, BarkTrigger.BossDefeated, 0f, 9999f, 8, "*claims one barnacle as a crown*"),
                B("elite_hammer_penny", BarkSpeaker.Penny, BarkTrigger.PhaseStarted, 0f, 9999f, 5, "That hammerhead looks personally offended."),
                B("elite_kraken_penny", BarkSpeaker.Penny, BarkTrigger.PhaseStarted, 0f, 9999f, 5, "Kraken spawn. I'm not paying for tentacle damage."),
                B("reef_crab_penny", BarkSpeaker.Penny, BarkTrigger.PhaseStarted, 0f, 9999f, 5, "Reef crab. Armoured. Unreasonably so."),
                B("overtime_penny", BarkSpeaker.Penny, BarkTrigger.PhaseStarted, 0f, 9999f, 6, "We could leave. We won't. Noted."),
                B("storm_penny", BarkSpeaker.Penny, BarkTrigger.PhaseWarning, 0f, 9999f, 6, "Storm's coming. I've seen your sailing. This will be expensive."),
                B("hull_low_penny", BarkSpeaker.Penny, BarkTrigger.HullLow, 0.3f, 45f, 7, "The hull is mostly holes now.", "Repairs are getting expensive. Sail better."),
                B("hull_low_captain", BarkSpeaker.Captain, BarkTrigger.HullLow, 0.15f, 60f, 6, "The ship looks fine.", "No."),
                B("gold_penny", BarkSpeaker.Penny, BarkTrigger.GoldAtLeast, 150f, 9999f, 3, "Good news: gold. Bad news: it's only good until we sink."),
                B("gold_chipmunk", BarkSpeaker.Chipmunk, BarkTrigger.GoldAtLeast, 300f, 9999f, 2, "*eyes the gold pile with professional interest*"),
                B("quiet_penny", BarkSpeaker.Penny, BarkTrigger.Quiet, 25f, 120f, 1, "Quiet. I don't like quiet.", "Nobody's shooting. Enjoy it."),
                B("quiet_chipmunk", BarkSpeaker.Chipmunk, BarkTrigger.Quiet, 40f, 150f, 1, "*naps in a coil of rope*"),
                B("streak_chipmunk", BarkSpeaker.Chipmunk, BarkTrigger.KillStreak, 20f, 120f, 1, "*applauds, then steals a coin from the applause*"),
                B("level_chipmunk", BarkSpeaker.Chipmunk, BarkTrigger.LevelUp, 0f, 150f, 0, "*tests the new part with its teeth*"),
                B("boss_summoned_captain", BarkSpeaker.Captain, BarkTrigger.BossSummoned, 0f, 9999f, 6, "Let it spread.", "I have an idea."),
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
            T("autofire_xp", TutorialTrigger.XpCollected, "Your guns fire on their own. Sink ships and sail over the XP they drop.", 120f),
            T("level_up", TutorialTrigger.LevelUp, "Level up: pick one card — a new weapon, an upgrade or a Logbook.", 150f),
            T("chest", TutorialTrigger.ChestOpened, "Salvage chests hold items. Sail up to one and hold E to open it.", 150f),
            T("landmark", TutorialTrigger.LandmarkUsed, "The minimap marks landmarks. Use one: a Repair Wreck, a Signal Buoy or a Treasure Chart.", 150f),
            T("beacon", TutorialTrigger.BossSummoned, "Find the Treasury Beacon (the minimap points to it) and hold E to call its guardian.", 360f),
            T("boss_telegraph", TutorialTrigger.BossTelegraph, "Marked water shows where the Barnacle King will strike. Sail out of it.", 120f),
            T("claim", TutorialTrigger.BossDefeated, "TERRITORY CLAIMED — SURVIVE AS LONG AS YOU CAN.", 0f, 6f, true),
            T("death_summary", TutorialTrigger.RunEnded, "Run gold is lost at sea. Rum is yours to keep: spend it on unlocks between voyages.", 0f, 8f, true),
        };
    }
}
