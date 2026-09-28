using System;
using System.Collections.Generic;
using RoguePirates.V4.Config;
using RoguePirates.V4.Events;
using UnityEngine;

namespace RoguePirates.V4.Rewards
{
    /// <summary>Where settled Rum goes (the shared save: <c>marks</c> field, one receipt per run id).</summary>
    public interface IRumBank
    {
        /// <summary>Adds <paramref name="rum"/> once for <paramref name="runId"/>; false if already settled (or no save).</summary>
        bool TrySettle(string runId, int rum);
        int Treasury { get; }
    }

    /// <summary>One row's result on the results screen.</summary>
    public struct RumRowResult
    {
        public string RowId, Label;
        public RumSource Source;
        public int Credits, Rum;
    }

    /// <summary>Read-only Rum state for the results view, the console and traces.</summary>
    public interface IRumView
    {
        string CurrencyName { get; }
        /// <summary>Rum earned so far this run (live), per enabled row.</summary>
        IReadOnlyList<RumRowResult> Live { get; }
        bool Settled { get; }
        /// <summary>True if the settlement paid (false: already paid for this run id, or no save).</summary>
        bool SettlementApplied { get; }
        /// <summary>The settled breakdown (valid once <see cref="Settled"/>).</summary>
        IReadOnlyList<RumRowResult> SettledRows { get; }
        int SettledTotal { get; }
        bool EndedByDeath { get; }
        int Treasury { get; }
        /// <summary>Rum of one source in the settled breakdown (the trace's V3-shaped "marks" line).</summary>
        int SettledRumOf(RumSource source);
    }

    /// <summary>
    /// Rum (M12): every row of <see cref="RewardConfig"/> listens to V4 combat events — Killed (with the run-clock time of
    /// the kill), LandmarkUsed, BossState, PhaseChanged (scheduled-elite spawns carry the unit id), ChestOpened — and the
    /// run is settled once, at death, through <see cref="IRumBank"/> (never RunManager.FinalizeRunOnDeath). With the default
    /// rows it pays exactly V3's Marks: minutes are closed by the run clock at settlement, the minute in progress never
    /// counts, and an overtime minute is paid only once the next overtime minute has begun. Settlement also reads events
    /// published earlier in the dying frame that have not been delivered yet (a boss secured at death still counts).
    /// </summary>
    public sealed class RewardModule : IRunModule, IRumView
    {
        readonly Dictionary<int, int> _killsPerMinute = new Dictionary<int, int>();
        readonly Dictionary<int, int> _killsPerOvertimeMinute = new Dictionary<int, int>();
        readonly HashSet<int> _landmarks = new HashSet<int>();
        readonly Dictionary<int, int> _scheduledElites = new Dictionary<int, int>(); // unit id → scheduled-elite index
        readonly HashSet<int> _elitesSunk = new HashSet<int>(); // scheduled-elite indices
        readonly List<RumRowResult> _live = new List<RumRowResult>();
        readonly List<RumRowResult> _settled = new List<RumRowResult>();
        RunContext _context;
        CombatEvents _events;
        IRumBank _bank;
        RewardConfig _config;
        WaveTimelineConfig _timeline;
        bool _boss;
        int _chests;

        public string Id => "rewards";
        public int TickOrder => 900; // after everything that produces events

        /// <summary>Module Lab: nothing is ever settled.</summary>
        public bool LabMode { get; set; }

        RewardConfig Config => _config ??= _context?.Config?.Single<RewardConfig>();
        WaveTimelineConfig Timeline => _timeline ??= _context?.Config?.Single<WaveTimelineConfig>();

        public string CurrencyName => Config != null ? Config.currencyName : "Rum";
        public bool Settled { get; private set; }
        public bool SettlementApplied { get; private set; }
        public bool EndedByDeath { get; private set; }
        public int SettledTotal { get; private set; }
        public IReadOnlyList<RumRowResult> SettledRows => _settled;
        public int Treasury => _bank != null ? _bank.Treasury : 0;

        /// <summary>Live rows at the current run time (no pending events).</summary>
        public IReadOnlyList<RumRowResult> Live
        {
            get
            {
                Compute(_live, CurrentTime, null);
                return _live;
            }
        }

        /// <summary>The run clock (RunManager's elapsed time: the console's jump moves it; minutes close on it).</summary>
        public Func<float> RunTime;
        float CurrentTime => RunTime != null ? RunTime() : 0f;

        public void Initialize(RunContext context)
        {
            _context = context;
            _events = context.Events;
            context.TryGet(out _bank);
            if (_events != null) _events.TickPublished += OnEvents;
        }

        public void BeginRun(in RunStart start)
        {
            _killsPerMinute.Clear();
            _killsPerOvertimeMinute.Clear();
            _landmarks.Clear();
            _scheduledElites.Clear();
            _elitesSunk.Clear();
            _settled.Clear();
            _boss = false;
            _chests = 0;
            Settled = SettlementApplied = EndedByDeath = false;
            SettledTotal = 0;
        }

        public void Tick(in SimTick tick) { }

        public void EndRun(in RunEnd end)
        {
            if (_events != null) _events.TickPublished -= OnEvents;
        }

        void OnEvents(IReadOnlyList<CombatEvent> batch)
        {
            if (Settled) return; // the settled breakdown is final (pending events were read at settlement)
            for (var i = 0; i < batch.Count; i++) Apply(batch[i], _killsPerMinute, _killsPerOvertimeMinute, _landmarks, _scheduledElites, _elitesSunk, ref _boss, ref _chests);
        }

        void Apply(in CombatEvent e, Dictionary<int, int> minutes, Dictionary<int, int> overtime, HashSet<int> landmarks,
            Dictionary<int, int> elites, HashSet<int> sunk, ref bool boss, ref int chests)
        {
            switch (e.Kind)
            {
                case CombatEventKind.Killed:
                {
                    var t = e.Z; // run-clock time of the kill
                    var minute = (int)(t / 60f);
                    minutes[minute] = minutes.TryGetValue(minute, out var n) ? n + 1 : 1;
                    var deadline = Deadline;
                    if (t > deadline) // V3: RunManager.IsOvertime (strictly after the deadline)
                    {
                        var ot = (int)((t - deadline) / 60f);
                        overtime[ot] = overtime.TryGetValue(ot, out var m) ? m + 1 : 1;
                    }
                    if (e.A > 0 && elites.TryGetValue(e.A, out var index))
                    {
                        elites.Remove(e.A);
                        sunk.Add(index);
                    }
                    break;
                }
                case CombatEventKind.LandmarkUsed:
                    landmarks.Add(e.A);
                    break;
                case CombatEventKind.BossState:
                    if (e.A == 2) boss = true;
                    break;
                case CombatEventKind.ChestOpened:
                    chests++;
                    break;
                case CombatEventKind.PhaseChanged:
                    // A focus phase spawned its unit (A = unit id, B = 1): scheduled elites are registered by unit id.
                    if (e.B == 1 && e.A > 0 && !string.IsNullOrEmpty(e.Tag))
                    {
                        var phase = Timeline?.Phase(e.Tag);
                        var eliteIndex = phase?.focus != null ? phase.focus.scheduledEliteIndex : -1;
                        if (eliteIndex >= 0) elites[e.A] = eliteIndex;
                    }
                    break;
            }
        }

        float Deadline => Timeline != null ? Timeline.stageDeadlineSeconds : 600f;

        /// <summary>
        /// The run's Rum at <paramref name="time"/>: each enabled row's credits under its caps. <paramref name="pending"/>
        /// adds events not delivered yet (settlement in the dying frame).
        /// </summary>
        void Compute(List<RumRowResult> into, float time, IReadOnlyList<CombatEvent> pending)
        {
            into.Clear();
            var c = Config;
            if (c == null) return;
            var minutes = _killsPerMinute;
            var overtime = _killsPerOvertimeMinute;
            var landmarks = _landmarks;
            var elites = _scheduledElites;
            var sunk = _elitesSunk;
            var boss = _boss;
            var chests = _chests;
            if (pending != null && pending.Count > 0)
            {
                minutes = new Dictionary<int, int>(_killsPerMinute);
                overtime = new Dictionary<int, int>(_killsPerOvertimeMinute);
                landmarks = new HashSet<int>(_landmarks);
                elites = new Dictionary<int, int>(_scheduledElites);
                sunk = new HashSet<int>(_elitesSunk);
                for (var i = 0; i < pending.Count; i++) Apply(pending[i], minutes, overtime, landmarks, elites, sunk, ref boss, ref chests);
            }

            var deadline = Deadline;
            var closedMinutes = (int)(time / 60f); // minutes 0 .. closed-1 have ended
            var closedOvertime = time > deadline ? (int)((time - deadline) / 60f) : 0; // overtime minutes 0 .. closed-1 are paid
            foreach (var row in c.rows)
            {
                if (row == null || !row.enabled) continue;
                var candidates = 0;
                switch (row.source)
                {
                    case RumSource.QualifyingMinute:
                        for (var m = 0; m < closedMinutes; m++)
                            if ((m + 1) * 60f <= deadline && minutes.TryGetValue(m, out var k) && k >= row.minKills) candidates++;
                        break;
                    case RumSource.OvertimeMinute:
                        for (var m = 0; m < closedOvertime; m++)
                            if (overtime.TryGetValue(m, out var k) && k >= row.minKills) candidates++;
                        break;
                    case RumSource.SpecialLandmark: candidates = landmarks.Count; break;
                    case RumSource.ScheduledElite: candidates = sunk.Count; break;
                    case RumSource.BossVictory: candidates = boss ? 1 : 0; break;
                    case RumSource.ChestOpened: candidates = chests; break;
                }
                var credits = candidates;
                if (row.maxCredits > 0) credits = Mathf.Min(credits, row.maxCredits);
                if (row.maxRum > 0 && row.rum > 0) credits = Mathf.Min(credits, row.maxRum / row.rum);
                var rum = credits * Mathf.Max(0, row.rum);
                if (row.maxRum > 0) rum = Mathf.Min(rum, row.maxRum);
                into.Add(new RumRowResult { RowId = row.rowId, Label = row.label, Source = row.source, Credits = credits, Rum = rum });
            }
        }

        /// <summary>
        /// Settles the run once (at death): computes the breakdown at the run clock's time (including undelivered events)
        /// and pays it through the bank under <paramref name="runId"/>. Later calls return the first result and pay nothing.
        /// </summary>
        public int Settle(string runId, bool endedByDeath)
        {
            if (Settled) return SettledTotal;
            EndedByDeath = endedByDeath;
            if (LabMode) return 0;
            Compute(_settled, CurrentTime, _events?.Pending);
            var total = 0;
            foreach (var r in _settled) total += r.Rum;
            SettledTotal = total;
            EndedByDeath = endedByDeath;
            Settled = true;
            SettlementApplied = _bank != null && !string.IsNullOrEmpty(runId) && _bank.TrySettle(runId, total);
            return total;
        }

        public int SettledRumOf(RumSource source)
        {
            var sum = 0;
            foreach (var r in _settled) if (r.Source == source) sum += r.Rum;
            return sum;
        }
    }
}
