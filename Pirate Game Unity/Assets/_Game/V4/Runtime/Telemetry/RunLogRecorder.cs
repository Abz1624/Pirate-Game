using System;
using System.Collections.Generic;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Events;
using RoguePirates.V4.Foundation.Core;
using UnityEngine;

namespace RoguePirates.V4.Telemetry
{
    /// <summary>V5 M9a (I9a.1): a closed run log and the summary numbers it was closed with (the upload sends both).</summary>
    public sealed class RunLogResult
    {
        public string Json;
        public int Kills, Level, Bytes;
        public float Seconds;
        public int Seed;
        public string ConfigHash;
    }

    /// <summary>
    /// V5 M9a (I9a.1): where the results screen's upload gets the run log (null when the run keeps none). Read only: the
    /// runtime closes the log at the run's end (<see cref="RunLogRecorder"/>); presentation never changes run state.
    /// </summary>
    public interface IRunLogSource
    {
        /// <summary>The log the run's end closed, with its totals; null while the run is still going or when there is no
        /// usable log (no seed or config hash).</summary>
        RunLogResult Closed { get; }
    }

    /// <summary>
    /// V5 M9a (I9a.1): records every voyage's run log (<see cref="RunLogBuilder"/>), whatever the playtest switches (the
    /// <see cref="SessionRecorder"/> is the playtest's larger file; this is the compact log the API stores). Reads the run's
    /// counters through delegates and the combat events (spawns, kinds of kills, damage taken, chests, points of interest);
    /// per-weapon kills and damage come from the <see cref="RunTelemetry"/> ledger. Closed by the runtime at the run's end:
    /// on the tick batch that carries the run-ended state (PlayerDead or RunComplete, before any presenter reads that batch,
    /// since this module subscribes at initialization), and again by <see cref="EndRun"/> (a no-op once closed). The upload
    /// reads <see cref="Closed"/> and takes its summary from it, so the uploaded summary and the log agree.
    /// </summary>
    public sealed class RunLogRecorder : IRunModule, IRunLogSource
    {
        /// <summary>Hull gained in one tick while max hull stayed the same is healing.</summary>
        const float HealEpsilon = 0.0001f;

        public string Id => "run-log";
        public int TickOrder => 905; // after everything it reads (the session recorder is 900)

        // ---- sources (the composition root sets them) ----
        public Func<float> RunTime;
        public Func<int> Seed, Kills, Gold, Level, Pickups;
        public Func<long> Xp;
        public Func<float> Hull, MaxHull;
        public Func<bool> HullKnown, SunkNow, RunEnded, ObjectiveSecured;
        public Func<string> ConfigHash, RunId, Captain, StartingWeapon, BuildId;
        public Func<(string id, string version, string hash)?> StageProfile;
        public Func<RunRequest.SeedOrigin> SeedOrigin;
        /// <summary>The ship's position, heading (degrees), steer, throttle and boost (the input samples).</summary>
        public Func<Vector3> ShipPosition, ShipForward;
        public Func<Vector2> SteerThrottle;
        public Func<bool> Boosting;
        public RunTelemetry Telemetry;

        readonly RunLogBuilder _log = new RunLogBuilder();
        CombatEvents _events;
        IEnemyWorldView _enemies;
        bool _began, _sunk, _bossAlive;
        float _prevHull = -1f, _prevMax = -1f;
        RunLogResult _result;
        bool _closed;

        public RunLogBuilder Log => _log;
        public RunLogResult Result => _result;
        public RunLogResult Closed => _closed ? _result : null;

        public void Initialize(RunContext context)
        {
            _events = context.Events;
            context.TryGet(out _enemies);
            if (_events != null) _events.TickPublished += OnEvents;
        }

        public void BeginRun(in RunStart start)
        {
            _began = true;
            _sunk = _bossAlive = false;
            _prevHull = _prevMax = -1f;
            _result = null;
            _closed = false;
            var origin = SeedOrigin != null ? SeedOrigin() : default;
            var profile = Safe(() => StageProfile?.Invoke(), null);
            var seed = Safe(() => Seed != null ? Seed() : 0, 0);
            _log.Begin(new RunLogHeader
            {
                Seed = seed != 0 ? seed : start.Seed,
                SeedSource = origin.Source.ToString().ToLowerInvariant(),
                IssuedRunId = origin.IssuedRunId,
                RunId = Safe(() => RunId?.Invoke(), null),
                ConfigHash = Safe(() => ConfigHash?.Invoke(), null),
                BalanceVersion = profile?.version,
                ContentHash = profile?.hash,
                StageProfile = profile?.id,
                BuildId = Safe(() => BuildId?.Invoke(), null),
                Captain = Safe(() => Captain?.Invoke(), null),
                StartingWeapon = Safe(() => StartingWeapon?.Invoke(), null),
                Editor = Application.isEditor,
                DevelopmentBuild = Debug.isDebugBuild,
            }, Counters());
        }

        public void Tick(in SimTick tick)
        {
            if (!_began || _log.Closed) return;
            var t = Now;
            TrackHeal();
            if (ShipPosition != null)
            {
                var p = ShipPosition();
                var f = ShipForward != null ? ShipForward() : Vector3.forward;
                var st = SteerThrottle != null ? SteerThrottle() : Vector2.zero;
                _log.SampleInput(t, p.x, p.z, Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, st.x, st.y, Boosting != null && Boosting());
            }
            if (t >= (_log.MinuteCount) * RunLogBuilder.MinuteSeconds)
            {
                ReadWeapons();
                _log.Advance(t, Counters());
            }
        }

        public void EndRun(in RunEnd end) => Close();

        /// <summary>The run manager's state changes (a sinking is remembered for the end reason).</summary>
        public void OnRunState(RunState state)
        {
            if (state == RunState.PlayerDead) _sunk = true;
        }

        /// <summary>Closes the log now (runtime only: the run's end, or a test standing in for it) and returns it with its
        /// totals; null when there is no usable log. Closing again returns the same result.</summary>
        public RunLogResult Close()
        {
            if (!_began) return null;
            if (_closed) return _result;
            _closed = true;
            var t = Now;
            TrackHeal();
            ReadWeapons();
            var c = Counters();
            var sunk = _sunk || (SunkNow != null && Safe(SunkNow, false));
            var reason = sunk ? "sunk" : RunEnded != null && Safe(RunEnded, false) && ObjectiveSecured != null && Safe(ObjectiveSecured, false) ? "victory" : "quit";
            _log.Close(t, reason, c);
            var header = _log.Header;
            if (header.Seed == 0 || string.IsNullOrEmpty(header.ConfigHash))
            {
                Debug.LogWarning("Run log: no seed or config hash; the run is uploaded without a log.");
                return null;
            }
            var json = _log.ToJson();
            _result = new RunLogResult
            {
                Json = json, Bytes = System.Text.Encoding.UTF8.GetByteCount(json), Kills = _log.TotalKills(), Level = _log.LastLevel,
                Seconds = _log.Seconds, Seed = header.Seed, ConfigHash = header.ConfigHash,
            };
            return _result;
        }

        float Now => RunTime != null ? Safe(RunTime, _log.Seconds) : _log.Seconds;

        RunLogCounters Counters() => new RunLogCounters
        {
            Kills = Safe(() => Kills != null ? Kills() : 0, 0),
            Gold = Safe(() => Gold != null ? Gold() : 0, 0),
            Level = Safe(() => Level != null ? Level() : 1, 1),
            Pickups = Safe(() => Pickups != null ? Pickups() : 0, 0),
            Xp = Safe(() => Xp != null ? Xp() : 0L, 0L),
            Alive = _enemies != null ? _enemies.ActiveCount : 0,
            Hull = HullKnown != null && Safe(HullKnown, false) && Hull != null ? Safe(Hull, 0f) : 0f,
            MaxHull = HullKnown != null && Safe(HullKnown, false) && MaxHull != null ? Safe(MaxHull, 0f) : 0f,
        };

        void ReadWeapons() => Telemetry?.WeaponTotals((id, damage, kills, hits) => _log.SetWeaponTotal(id, kills, damage));

        void TrackHeal()
        {
            if (HullKnown == null || !Safe(HullKnown, false) || Hull == null || MaxHull == null) return;
            var hull = Safe(Hull, 0f);
            var max = Safe(MaxHull, 0f);
            if (_prevHull >= 0f && Mathf.Abs(max - _prevMax) < 0.001f && hull - _prevHull > HealEpsilon) _log.AddHealed(hull - _prevHull);
            _prevHull = hull;
            _prevMax = max;
        }

        void OnEvents(IReadOnlyList<CombatEvent> batch)
        {
            if (!_began || _log.Closed) return;
            var ended = false;
            for (var i = 0; i < batch.Count; i++)
            {
                var e = batch[i];
                switch (e.Kind)
                {
                    case CombatEventKind.Spawned: _log.AddSpawn(); break;
                    case CombatEventKind.Killed: _log.AddKillKind((e.B & 1) != 0, (e.B & 2) != 0); break;
                    case CombatEventKind.PlayerDamaged: _log.AddDamageTaken(e.Z); break;
                    case CombatEventKind.ChestOpened: _log.AddChest(); break;
                    case CombatEventKind.LandmarkUsed: _log.AddPoi(); break;
                    case CombatEventKind.BossState:
                        if (e.A == 1 && !_bossAlive && _log.BossSummoned < 0f) { _log.BossSummoned = Now; _bossAlive = true; }
                        else if (e.A == 2 && _log.BossDefeated < 0f) { _log.BossDefeated = Now; _bossAlive = false; }
                        break;
                    case CombatEventKind.RunState:
                        if (e.A == (int)RunState.PlayerDead) _sunk = true;
                        if (e.A == (int)RunState.PlayerDead || e.A == (int)RunState.RunComplete) ended = true;
                        break;
                }
            }
            if (ended) Close(); // the whole batch counted first; the results screen reads this batch after us
        }

        /// <summary>The game build's id: the version, plus "+editor" in the Editor or the build GUID's first 12 characters.</summary>
        public static string BuildIdOfThisBuild()
        {
            var id = string.IsNullOrEmpty(Application.version) ? "0" : Application.version;
            if (Application.isEditor) return id + "+editor";
            var guid = Application.buildGUID;
            return string.IsNullOrEmpty(guid) ? id : id + "+" + (guid.Length > 12 ? guid.Substring(0, 12) : guid);
        }

        static T Safe<T>(Func<T> f, T fallback)
        {
            try { return f != null ? f() : fallback; } catch { return fallback; }
        }

        public void Detach()
        {
            if (_events != null) _events.TickPublished -= OnEvents;
            _events = null;
        }
    }
}
