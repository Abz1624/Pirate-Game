using System;
using System.Collections.Generic;
using RoguePirates.V4.Config;
using RoguePirates.V4.Events;
using RoguePirates.V4.Spawning;
using RoguePirates.V4.Views;
using UnityEngine;

namespace RoguePirates.V4.Narrative
{
    /// <summary>What the narrative presenter reads: the latest bark and the latest tutorial prompt (a serial changes when a new one is due).</summary>
    public interface INarrativeView
    {
        int BarkSerial { get; }
        /// <summary>The config id of the latest bark (console, tests).</summary>
        string BarkId { get; }
        string BarkSpeaker { get; }
        string BarkLine { get; }
        float BarkSeconds { get; }
        int PromptSerial { get; }
        string PromptText { get; }
        float PromptSeconds { get; }
        /// <summary>The tutorial is running this run (not complete, switched on).</summary>
        bool TutorialActive { get; }
        int TutorialIndex { get; }
        int TutorialCount { get; }
    }

    /// <summary>Console control of the barks.</summary>
    public interface IBarkControl
    {
        /// <summary>Plays a bark now (ignores cooldowns); false if there is no such bark.</summary>
        bool Play(string barkId);
    }

    /// <summary>Console control and state of the tutorial.</summary>
    public interface ITutorialControl
    {
        bool Active { get; }
        int Index { get; }
        int Count { get; }
        TutorialStep Current { get; }
        IReadOnlyList<string> Skipped { get; }
        bool Complete(TutorialTrigger trigger);
        void ResetProgress();
    }

    /// <summary>Tutorial progress: how many steps are done (completed or skipped), kept across runs.</summary>
    public interface ITutorialStore
    {
        int Progress { get; set; }
    }

    /// <summary>The V4 save key for tutorial progress (never V3's onboarding key).</summary>
    public sealed class PlayerPrefsTutorialStore : ITutorialStore
    {
        public const string Key = "v4_tutorial_progress";
        public int Progress
        {
            get => PlayerPrefs.GetInt(Key, 0);
            set { PlayerPrefs.SetInt(Key, value); PlayerPrefs.Save(); }
        }
    }

    /// <summary>The run clock and pause state the narrative respects.</summary>
    internal static class NarrativeTime
    {
        public static float Now(IRunStatus run) => run != null && run.Exists ? run.ElapsedSeconds : 0f;
        public static bool Quiet(IRunStatus run) => run == null || !run.Exists || run.IsPaused || run.IsRunEnded;
    }

    /// <summary>
    /// Barks (M13): short character beats on events. Every rule in <see cref="NarrativeConfig.barks"/> is a trigger with
    /// a cooldown and a priority; the highest-priority due bark plays when the global cooldown allows. Nothing plays
    /// while the run is paused (due barks wait for the pause to end) or over, events that could not play within a few
    /// seconds of run time are dropped, and a quiet window follows the boss's defeat. It never pauses anything and draws only from its own seeded stream.
    /// </summary>
    public sealed class BarkDirector : IRunModule, IBarkControl
    {
        const float DueForSeconds = 3f, RunStartDelay = 3f;
        const int Salt = 0x0BA4C;

        readonly List<(BarkRule rule, float at)> _due = new List<(BarkRule, float)>();
        readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();
        readonly HashSet<string> _playedThisRun = new HashSet<string>();
        readonly List<float> _killTimes = new List<float>(); // the last minute of kills (streak windows are shorter)
        RunContext _context;
        CombatEvents _events;
        IRunStatus _run;
        Difficulty.IPowerInputs _power;
        IRunViews _views;
        NarrativeConfig _config;
        RoguePirates.V4.Foundation.Core.RunStream _rng = new RoguePirates.V4.Foundation.Core.RunStream(Salt); // V5 M9a (I9a.2)
        float _lastBark = float.NegativeInfinity, _lastKill, _bossDefeatedAt = float.NegativeInfinity;
        bool _runStartDone;
        int _gold;

        /// <summary>The selected Captain's config key (rook, brine, …), for the Captain's own lines.</summary>
        public Func<string> CaptainKey;

        public string Id => "barks";
        public int TickOrder => 950;
        public NarrativeView View;

        NarrativeConfig Config => _config ??= _context?.Config?.Single<NarrativeConfig>();

        public void Initialize(RunContext context)
        {
            _context = context;
            _events = context.Events;
            context.TryGet(out _run);
            context.TryGet(out _power);
            if (_events != null) _events.TickPublished += OnEvents;
        }

        public void BeginRun(in RunStart start)
        {
            _due.Clear();
            _lastPlayed.Clear();
            _playedThisRun.Clear();
            _killTimes.Clear();
            _rng = new RoguePirates.V4.Foundation.Core.RunStream(start.Seed ^ Salt);
            _lastBark = _bossDefeatedAt = float.NegativeInfinity;
            _lastKill = 0f;
            _runStartDone = false;
            _gold = 0;
        }

        public void EndRun(in RunEnd end)
        {
            if (_events != null) _events.TickPublished -= OnEvents;
        }

        void OnEvents(IReadOnlyList<CombatEvent> batch)
        {
            var c = Config;
            if (c == null || !c.barksEnabled) return;
            var now = NarrativeTime.Now(_run);
            for (var i = 0; i < batch.Count; i++)
            {
                var e = batch[i];
                switch (e.Kind)
                {
                    case CombatEventKind.Killed:
                        _lastKill = now;
                        _killTimes.Add(now);
                        while (_killTimes.Count > 0 && _killTimes[0] < now - 60f) _killTimes.RemoveAt(0); // times are in order
                        Due(c, BarkTrigger.KillStreak, now, 0f, null);
                        break;
                    case CombatEventKind.LevelUp: Due(c, BarkTrigger.LevelUp, now, 0f, null); break;
                    case CombatEventKind.ChestOpened: Due(c, BarkTrigger.ChestOpened, now, 0f, null); break;
                    case CombatEventKind.LandmarkUsed: Due(c, BarkTrigger.LandmarkUsed, now, 0f, null); break;
                    case CombatEventKind.BeaconState:
                        if (e.A >= 1) Due(c, BarkTrigger.BeaconHint, now, e.A, null);
                        if (e.A >= 3) Due(c, BarkTrigger.BeaconSpotted, now, e.A, null);
                        break;
                    case CombatEventKind.BossState:
                        if (e.A == 1) Due(c, BarkTrigger.BossSummoned, now, 0f, null);
                        if (e.A == 2) { _bossDefeatedAt = now; Due(c, BarkTrigger.BossDefeated, now, 0f, null); }
                        break;
                    case CombatEventKind.PhaseChanged:
                        if (e.B == 1) Due(c, BarkTrigger.PhaseStarted, now, 0f, e.Tag);
                        break;
                    case CombatEventKind.Warning: Due(c, BarkTrigger.PhaseWarning, now, 0f, e.Tag); break;
                    case CombatEventKind.PlayerDamaged:
                        if (_power != null && _power.MaxHull > 0f) Due(c, BarkTrigger.HullLow, now, _power.Hull / _power.MaxHull, null);
                        break;
                    case CombatEventKind.GoldChanged:
                        _gold = e.A;
                        Due(c, BarkTrigger.GoldAtLeast, now, _gold, null);
                        break;
                }
            }
        }

        /// <summary>Queues the rules of <paramref name="trigger"/> that this event satisfies.</summary>
        void Due(NarrativeConfig c, BarkTrigger trigger, float now, float value, string tag)
        {
            foreach (var rule in c.barks)
            {
                if (rule == null || !rule.enabled || rule.trigger != trigger) continue;
                if (!Matches(rule, value, tag, now)) continue;
                if (!IsDue(rule)) _due.Add((rule, now));
            }
        }

        bool IsDue(BarkRule rule)
        {
            for (var i = 0; i < _due.Count; i++) if (_due[i].rule == rule) return true;
            return false;
        }

        bool Matches(BarkRule rule, float value, string tag, float now)
        {
            switch (rule.trigger)
            {
                case BarkTrigger.KillStreak:
                    var from = now - Mathf.Max(0.1f, rule.windowSeconds);
                    var n = 0;
                    foreach (var t in _killTimes) if (t >= from) n++;
                    return n >= rule.value;
                case BarkTrigger.HullLow: return value > 0f && value <= rule.value;
                case BarkTrigger.GoldAtLeast: return value >= rule.value;
                case BarkTrigger.PhaseStarted:
                case BarkTrigger.PhaseWarning: return !string.IsNullOrEmpty(rule.tag) && rule.tag == tag; // an untagged phase bark never fires
                default: return true;
            }
        }

        public void Tick(in SimTick tick)
        {
            var c = Config;
            if (c == null || !c.barksEnabled) return;
            var now = NarrativeTime.Now(_run);
            // Never during a pause or after the run: due barks wait out a pause (the run clock stops, so they do not go
            // stale — a level-up bark plays when the cards close) and are dropped when the run ends.
            if (_run != null && _run.IsRunEnded) { _due.Clear(); return; }
            if (NarrativeTime.Quiet(_run)) return;
            if (!_runStartDone && now >= RunStartDelay) { _runStartDone = true; Due(c, BarkTrigger.RunStart, now, 0f, null); }
            for (var i = 0; i < c.barks.Count; i++)
            {
                var rule = c.barks[i];
                if (rule != null && rule.enabled && rule.trigger == BarkTrigger.Quiet && now - _lastKill >= rule.value && !IsDue(rule))
                    _due.Add((rule, now));
            }
            // Everyday barks expire after a few seconds; once-per-run milestones (boss, Beacon, elites) wait out the global cooldown.
            for (var i = _due.Count - 1; i >= 0; i--)
                if (now - _due[i].at > (_due[i].rule.oncePerRun ? c.globalCooldownSeconds + DueForSeconds : DueForSeconds)) _due.RemoveAt(i);
            if (_due.Count == 0 || now - _lastBark < c.globalCooldownSeconds) return;

            BarkRule best = null;
            for (var i = 0; i < _due.Count; i++)
            {
                var rule = _due[i].rule;
                if (!Playable(c, rule, now)) continue;
                if (best == null || rule.priority > best.priority) best = rule;
            }
            if (best == null) return;
            Play(best, now);
            for (var i = _due.Count - 1; i >= 0; i--) if (_due[i].rule == best) _due.RemoveAt(i);
            if (best.trigger == BarkTrigger.Quiet) _lastKill = now; // quiet barks count from the last bark too
        }

        bool Playable(NarrativeConfig c, BarkRule rule, float now)
        {
            if (rule.oncePerRun && _playedThisRun.Contains(rule.barkId)) return false;
            if (_lastPlayed.TryGetValue(rule.barkId, out var last) && now - last < rule.cooldownSeconds) return false;
            // The claim banner has the screen right after the boss dies (the boss-victory barks follow once it is over).
            if (now - _bossDefeatedAt < c.quietAfterBossSeconds) return false;
            return true;
        }

        void Play(BarkRule rule, float now)
        {
            _lastBark = now;
            _lastPlayed[rule.barkId] = now;
            _playedThisRun.Add(rule.barkId);
            var lines = rule.lines;
            if (rule.speaker == BarkSpeaker.Captain && rule.captainLines != null && CaptainKey != null)
            {
                var key = CaptainKey();
                for (var i = 0; i < rule.captainLines.Count; i++)
                    if (rule.captainLines[i] != null && rule.captainLines[i].captainKey == key && rule.captainLines[i].lines != null && rule.captainLines[i].lines.Length > 0)
                    { lines = rule.captainLines[i].lines; break; }
            }
            var line = lines != null && lines.Length > 0 ? lines[_rng.Next(lines.Length)] : string.Empty;
            View?.ShowBark(SpeakerName(rule.speaker), line, Config != null ? Config.barkSeconds : 3.5f, rule.barkId);
        }

        string SpeakerName(BarkSpeaker speaker)
        {
            var c = Config;
            switch (speaker)
            {
                case BarkSpeaker.Penny: return c != null ? c.pennyName : "Penny";
                case BarkSpeaker.Chipmunk: return c != null ? c.chipmunkName : "The chipmunk";
                case BarkSpeaker.BarnacleKing: return c != null ? c.bossName : "Barnacle King";
                default:
                    if (_views == null) _context?.TryGet(out _views);
                    var title = _views?.Captain?.DisplayTitle;
                    return string.IsNullOrEmpty(title) ? "Captain" : title;
            }
        }

        /// <summary>Console: play a bark now (ignores cooldowns and the pause rule).</summary>
        public bool Play(string barkId)
        {
            var rule = Config?.barks.Find(b => b != null && b.barkId == barkId);
            if (rule == null) return false;
            Play(rule, NarrativeTime.Now(_run));
            return true;
        }
    }

    /// <summary>
    /// The First Conquest tutorial (M13, replaces V3OnboardingDirector): nine prompts in strict order — only the current
    /// step's trigger counts — with progress kept under a V4 save key, so a later run continues where the last one
    /// stopped. A step is skipped after its time limit so the tutorial never stalls (a skipped step is not shown again).
    /// When the player gets ahead — the Beacon activated or the boss sunk before those steps are current — the steps in
    /// between are skipped and the step that happened completes (the claim banner shows on the real first conquest); a
    /// step whose trigger already happened this run completes as soon as it becomes current. Prompts are neutral text
    /// (characters never explain systems) and never pause the run.
    /// </summary>
    public sealed class TutorialDirector : IRunModule, ITutorialControl
    {
        const float ReshowEverySeconds = 30f, MoveSpeedSqr = 4f, MoveHoldSeconds = 0.5f;

        RunContext _context;
        CombatEvents _events;
        IRunStatus _run;
        IRunViews _views;
        Stage.IBossView _boss;
        NarrativeConfig _config;
        float _enteredAt, _lastShown, _movingFor, _lastNow;
        bool _active, _levelUpPending;
        readonly HashSet<TutorialTrigger> _seenThisRun = new HashSet<TutorialTrigger>();

        public string Id => "tutorial";
        public int TickOrder => 960;
        public NarrativeView View;
        public ITutorialStore Store = new PlayerPrefsTutorialStore();
        /// <summary>Module Lab: no tutorial.</summary>
        public bool LabMode { get; set; }
        /// <summary>Steps skipped this run (console).</summary>
        public readonly List<string> SkippedThisRun = new List<string>();
        public IReadOnlyList<string> Skipped => SkippedThisRun;

        NarrativeConfig Config => _config ??= _context?.Config?.Single<NarrativeConfig>();
        public int Count => Config != null ? Config.tutorial.Count : 0;
        public int Index => Store != null ? Mathf.Clamp(Store.Progress, 0, Count) : Count;
        public bool Active => _active && Index < Count;
        public TutorialStep Current => Active ? Config.tutorial[Index] : null;

        public void Initialize(RunContext context)
        {
            _context = context;
            _events = context.Events;
            context.TryGet(out _run);
            if (_events != null) _events.TickPublished += OnEvents;
        }

        public void BeginRun(in RunStart start)
        {
            SkippedThisRun.Clear();
            _seenThisRun.Clear();
            _levelUpPending = false;
            var c = Config;
            _active = !LabMode && c != null && c.tutorialEnabled && Index < Count;
            _lastNow = 0f;
            _movingFor = 0f;
            if (_active) Enter(0f);
            PublishState();
        }

        public void EndRun(in RunEnd end)
        {
            // Only a real end (death, or a victory that ends the run): leaving mid-run (restart, menu) is not one.
            if (_run != null && _run.IsRunEnded) Complete(TutorialTrigger.RunEnded);
            if (_events != null) _events.TickPublished -= OnEvents;
        }

        void Enter(float now)
        {
            _enteredAt = now;
            _lastShown = float.NegativeInfinity;
            var step = Current;
            if (step == null) return;
            if (_seenThisRun.Contains(step.completesOn) && step.completesOn != TutorialTrigger.LevelUp) { Complete(step.completesOn); return; }
            if (!step.showOnComplete) Show(step, now);
        }

        void Show(TutorialStep step, float now)
        {
            _lastShown = now;
            if (step.silent) return;
            // Playtest (5 Oct, review): without a Treasury Beacon (the boss rises on its own) the Beacon step waits silently.
            if (step.completesOn == TutorialTrigger.BossSummoned && (RoguePirates.V4.Foundation.Core.DemoMode.ChestsOnlyPointsOfInterest || RoguePirates.V4.Foundation.Core.DemoMode.AutoBossSeconds > 0f)) return;
            View?.ShowPrompt(step.prompt, step.promptSeconds);
        }

        void OnEvents(IReadOnlyList<CombatEvent> batch)
        {
            if (!Active) return;
            for (var i = 0; i < batch.Count; i++)
            {
                var e = batch[i];
                switch (e.Kind)
                {
                    case CombatEventKind.LevelUp:
                        _levelUpPending = true; // completes when the cards close (the run resumes)
                        break;
                    case CombatEventKind.OffersShown:
                        if (Current != null && Current.completesOn == TutorialTrigger.LevelUp) Show(Current, NarrativeTime.Now(_run)); // over the cards
                        break;
                    case CombatEventKind.ChestOpened: Seen(TutorialTrigger.ChestOpened); break;
                    case CombatEventKind.LandmarkUsed: Seen(TutorialTrigger.LandmarkUsed); break;
                    case CombatEventKind.BossState:
                        if (e.A == 1) CatchUp(TutorialTrigger.BossSummoned);
                        if (e.A == 2) CatchUp(TutorialTrigger.BossDefeated);
                        break;
                    case CombatEventKind.RunState:
                        if (e.A == (int)RoguePirates.V4.Foundation.Core.RunState.PlayerDead || e.A == (int)RoguePirates.V4.Foundation.Core.RunState.RunComplete) Complete(TutorialTrigger.RunEnded);
                        break;
                }
            }
        }

        /// <summary>Remembers that <paramref name="trigger"/> happened this run and completes the current step if it waits for it.</summary>
        void Seen(TutorialTrigger trigger)
        {
            _seenThisRun.Add(trigger);
            Complete(trigger);
        }

        /// <summary>A milestone the player reached early (the Beacon, the boss): skip the steps before it, then complete it.</summary>
        void CatchUp(TutorialTrigger trigger)
        {
            _seenThisRun.Add(trigger);
            var c = Config;
            if (c == null) return;
            var target = c.tutorial.FindIndex(s => s != null && s.completesOn == trigger);
            if (target < 0 || Index > target) return;
            var now = NarrativeTime.Now(_run);
            while (Active && Index < target)
            {
                SkippedThisRun.Add(Current.stepId);
                Store.Progress = Index + 1;
            }
            _enteredAt = now;
            Complete(trigger);
        }

        /// <summary>Completes the current step if <paramref name="trigger"/> is what it waits for (strict order).</summary>
        public bool Complete(TutorialTrigger trigger)
        {
            var step = Current;
            if (step == null || step.completesOn != trigger) return false;
            var now = NarrativeTime.Now(_run);
            if (step.showOnComplete) Show(step, now);
            Store.Progress = Index + 1;
            if (Active) Enter(now);
            PublishState();
            return true;
        }

        public void Tick(in SimTick tick)
        {
            var step = Current;
            if (step == null) return;
            var now = NarrativeTime.Now(_run);
            var dt = Mathf.Max(0f, now - _lastNow);
            _lastNow = now;
            if (NarrativeTime.Quiet(_run)) return;
            if (_views == null) _context?.TryGet(out _views);
            if (_boss == null) _context?.TryGet(out _boss);
            if (_levelUpPending)
            {
                _levelUpPending = false;
                _seenThisRun.Add(TutorialTrigger.LevelUp);
                if (Complete(TutorialTrigger.LevelUp)) return; // the cards are closed: the run has resumed
                step = Current;
                if (step == null) return;
            }

            switch (step.completesOn)
            {
                case TutorialTrigger.ShipMoved:
                    var v = _views?.Ship != null ? _views.Ship.Velocity : Vector3.zero;
                    _movingFor = v.sqrMagnitude > MoveSpeedSqr ? _movingFor + dt : 0f;
                    if (_movingFor >= MoveHoldSeconds) { Complete(TutorialTrigger.ShipMoved); return; }
                    break;
                case TutorialTrigger.XpCollected:
                    var p = _views?.Progression;
                    if (p != null && (p.CurrentXp > 0f || p.Level > 0)) { Complete(TutorialTrigger.XpCollected); return; }
                    break;
                case TutorialTrigger.BossTelegraph:
                    if (_boss != null && _boss.Active && _boss.Phase == Stage.BossPhase.Telegraph) { Complete(TutorialTrigger.BossTelegraph); return; }
                    break;
            }

            // Playtest (5 Oct, review): a step that waits for a landmark the stage no longer has is skipped at once.
            if (step.completesOn == TutorialTrigger.LandmarkUsed && RoguePirates.V4.Foundation.Core.DemoMode.ChestsOnlyPointsOfInterest)
            {
                SkippedThisRun.Add(step.stepId);
                Store.Progress = Index + 1;
                if (Active) Enter(now);
                PublishState();
                return;
            }
            if (step.skipAfterSeconds > 0f && now - _enteredAt >= step.skipAfterSeconds)
            {
                SkippedThisRun.Add(step.stepId);
                Store.Progress = Index + 1;
                if (Active) Enter(now);
                PublishState();
                return;
            }
            if (!step.showOnComplete && now - _lastShown >= ReshowEverySeconds) Show(step, now);
        }

        /// <summary>Console: start the tutorial again from step one (this run too).</summary>
        public void ResetProgress()
        {
            Store.Progress = 0;
            SkippedThisRun.Clear();
            _seenThisRun.Clear();
            var c = Config;
            _active = !LabMode && c != null && c.tutorialEnabled;
            if (_active) Enter(NarrativeTime.Now(_run));
            PublishState();
        }

        void PublishState() => View?.SetTutorial(Active, Index, Count);
    }

    /// <summary>The narrative state presenters read (written by the two directors).</summary>
    public sealed class NarrativeView : INarrativeView
    {
        public int BarkSerial { get; private set; }
        public string BarkId { get; private set; } = string.Empty;
        public string BarkSpeaker { get; private set; } = string.Empty;
        public string BarkLine { get; private set; } = string.Empty;
        public float BarkSeconds { get; private set; }
        public int PromptSerial { get; private set; }
        public string PromptText { get; private set; } = string.Empty;
        public float PromptSeconds { get; private set; }
        public bool TutorialActive { get; private set; }
        public int TutorialIndex { get; private set; }
        public int TutorialCount { get; private set; }

        public void ShowBark(string speaker, string line, float seconds, string barkId = null)
        {
            BarkId = barkId ?? string.Empty;
            BarkSpeaker = speaker ?? string.Empty;
            BarkLine = line ?? string.Empty;
            BarkSeconds = seconds;
            BarkSerial++;
        }

        public void ShowPrompt(string text, float seconds)
        {
            PromptText = text ?? string.Empty;
            PromptSeconds = seconds;
            PromptSerial++;
        }

        public void SetTutorial(bool active, int index, int count)
        {
            TutorialActive = active;
            TutorialIndex = index;
            TutorialCount = count;
        }
    }
}
