using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using static RoguePirates.V4.Telemetry.TelemetryJson;

namespace RoguePirates.V4.Telemetry
{
    /// <summary>V5 M9a (I9a.1): what identifies a run in its log (the seed and what the rules were).</summary>
    public sealed class RunLogHeader
    {
        public int Seed;
        /// <summary>local, requested or issued (<see cref="SeedSource"/>).</summary>
        public string SeedSource = "local";
        /// <summary>The server's run id for an issued seed (M10), else null.</summary>
        public string IssuedRunId;
        /// <summary>The client's run id (RunManager.RunId).</summary>
        public string RunId;
        public string ConfigHash, BalanceVersion, ContentHash, StageProfile, BuildId, Captain, StartingWeapon;
        public bool Editor, DevelopmentBuild;
    }

    /// <summary>V5 M9a (I9a.1): the run's running totals, read at each minute's end (cumulative since the run start).</summary>
    public struct RunLogCounters
    {
        public int Kills, Gold, Level, Pickups, Alive;
        public long Xp;
        public float Hull, MaxHull;
    }

    /// <summary>
    /// V5 M9a (I9a.1): the compact, versioned run log of one voyage (format <see cref="Format"/> version
    /// <see cref="Version"/>), uploaded with POST /api/runs and checked by the API (server/RoguePirates.Api/Playtest/
    /// RunLogRules.cs documents the same format). Per minute: spawns, kills (and per weapon, elite and boss), gold, XP, level,
    /// damage taken and dealt, healed, pickups, chests, points of interest, enemies alive and hull at the minute's end; plus
    /// the ship's inputs sampled every <see cref="InputEvery"/> s for disputes. Plain data, no engine calls: the
    /// <see cref="RunLogRecorder"/> feeds it from the run, tests feed it directly.
    ///
    /// Consistency by construction (the API checks it): the minute kills are deltas of the same kill counter the summary
    /// sends, so they sum to it; the last minute's level is the summary's level; per-weapon, elite and boss kills of a
    /// minute never exceed its kills (an excess, from a kill counted on either side of a minute's edge, carries over).
    /// Size: about 0.25 KB a minute plus about 30 bytes an input sample, so a 15-20 minute voyage is about 12-16 KB; a
    /// log over <see cref="MaxBytes"/> thins its input samples until it fits (the API takes at most 64 KB).
    /// </summary>
    public sealed class RunLogBuilder
    {
        public const string Format = "rp-runlog";
        public const int Version = 1;
        public const int MinuteSeconds = 60;
        public const float InputEvery = 5f;
        /// <summary>The client's own ceiling (the API's is 64 KB): input samples thin out above it.</summary>
        public const int MaxBytes = 48 * 1024;
        public const int MaxWeapons = 32;
        public static readonly string[] InputCols = { "t", "x", "z", "heading", "steer", "throttle", "boost" };

        sealed class Minute
        {
            public int spawns, kills, eliteKills, bossKills, gold, pickups, chests, pois, alive, level;
            public long xp;
            public float taken, dealt, healed, hull, maxHull;
            public int[] wk = Array.Empty<int>();
        }

        struct Input
        {
            public float t, x, z;
            public int heading, steer, throttle, boost;
        }

        readonly List<Minute> _minutes = new List<Minute>(32);
        readonly List<Input> _inputs = new List<Input>(256);
        readonly List<string> _weapons = new List<string>(8);
        readonly Dictionary<string, int> _weaponIndex = new Dictionary<string, int>();
        // per-weapon kills already written to closed minutes (the baseline the next minute's deltas start from)
        readonly List<int> _weaponLogged = new List<int>(8);
        readonly List<int> _weaponNow = new List<int>(8);
        readonly List<float> _weaponDamageNow = new List<float>(8);
        float _dealtLogged;

        RunLogCounters _last;
        Minute _current;
        int _pendingElite, _pendingBoss;
        float _nextInput;

        public RunLogHeader Header { get; private set; }
        public bool Begun { get; private set; }
        public bool Closed { get; private set; }
        public float Seconds { get; private set; }
        public string EndReason { get; private set; } = "quit";
        public float BossSummoned { get; set; } = -1f;
        public float BossDefeated { get; set; } = -1f;
        public int MinuteCount => _minutes.Count + (_current != null ? 1 : 0);
        public int InputCount => _inputs.Count;

        /// <summary>Starts the log (run time 0) with the run's counters at the start (kills 0, level 1, ...).</summary>
        public void Begin(RunLogHeader header, in RunLogCounters start)
        {
            Header = header ?? new RunLogHeader();
            _minutes.Clear(); _inputs.Clear(); _weapons.Clear(); _weaponIndex.Clear(); _weaponLogged.Clear(); _weaponNow.Clear(); _weaponDamageNow.Clear();
            _dealtLogged = 0f;
            _last = start;
            _pendingElite = _pendingBoss = 0;
            _current = new Minute();
            _nextInput = 0f;
            Seconds = 0f;
            EndReason = "quit";
            BossSummoned = BossDefeated = -1f;
            Begun = true;
            Closed = false;
        }

        // ---- facts as they happen (into the open minute) ----

        public void AddSpawn(int count = 1) { if (Open) _current.spawns += count; }
        public void AddDamageTaken(float amount) { if (Open && amount > 0f) _current.taken += amount; }
        public void AddHealed(float amount) { if (Open && amount > 0f) _current.healed += amount; }
        public void AddChest() { if (Open) _current.chests++; }
        public void AddPoi() { if (Open) _current.pois++; }

        /// <summary>A kill's kind (from the Killed event): elite and boss kills are counted here, all kills by the counter.</summary>
        public void AddKillKind(bool elite, bool boss)
        {
            if (!Open) return;
            if (boss) _pendingBoss++;
            else if (elite) _pendingElite++;
        }

        bool Open => Begun && !Closed;

        /// <summary>The weapon ledger's running totals (kills and damage per weapon since the run start).</summary>
        public void SetWeaponTotal(string weaponId, int kills, float damage)
        {
            if (!Open || string.IsNullOrEmpty(weaponId)) return;
            if (!_weaponIndex.TryGetValue(weaponId, out var i))
            {
                if (_weapons.Count >= MaxWeapons) return; // more weapons than the format holds: their kills stay in the total
                i = _weapons.Count;
                _weaponIndex[weaponId] = i;
                _weapons.Add(weaponId);
                _weaponLogged.Add(0);
                _weaponNow.Add(0);
                _weaponDamageNow.Add(0f);
            }
            _weaponNow[i] = kills;
            _weaponDamageNow[i] = damage;
        }

        /// <summary>Samples the ship's input when one is due (every <see cref="InputEvery"/> s of run time).</summary>
        public void SampleInput(float t, float x, float z, float headingDegrees, float steer, float throttle, bool boosting)
        {
            if (!Open || t < _nextInput) return;
            _nextInput = (float)(Math.Floor(t / InputEvery) + 1) * InputEvery;
            _inputs.Add(new Input
            {
                t = Round1(t), x = Round1(x), z = Round1(z),
                heading = (int)Math.Round(((headingDegrees % 360f) + 360f) % 360f) % 360,
                steer = (int)Math.Round(Clamp(steer, -1f, 1f) * 100f), throttle = (int)Math.Round(Clamp(throttle, -1f, 1f) * 100f),
                boost = boosting ? 1 : 0,
            });
        }

        /// <summary>Closes every minute that ended by run time <paramref name="t"/> with the counters now.</summary>
        public void Advance(float t, in RunLogCounters now)
        {
            if (!Open) return;
            if (t > Seconds) Seconds = t;
            while (t >= (_minutes.Count + 1) * MinuteSeconds) CloseMinute(now);
        }

        /// <summary>Ends the log: the open (partial) minute with the counters now. Later calls change nothing.</summary>
        public void Close(float t, string endReason, in RunLogCounters now)
        {
            if (!Open) return;
            Advance(t, now);
            Seconds = Math.Max(Seconds, t);
            // a run that ends exactly on a minute's edge has its last (empty) minute open: it stays, as the API allows
            CloseMinute(now, last: true);
            EndReason = string.IsNullOrEmpty(endReason) ? "quit" : endReason;
            Closed = true;
        }

        void CloseMinute(in RunLogCounters now, bool last = false)
        {
            var m = _current;
            m.kills = Math.Max(0, now.Kills - _last.Kills);
            m.gold = Math.Max(0, now.Gold - _last.Gold);
            m.xp = Math.Max(0L, now.Xp - _last.Xp);
            m.pickups = Math.Max(0, now.Pickups - _last.Pickups);
            m.level = Math.Max(1, now.Level);
            m.alive = Math.Max(0, now.Alive);
            m.hull = Math.Max(0f, now.Hull);
            m.maxHull = Math.Max(0f, now.MaxHull);

            // boss and elite kills inside the minute's kills; an excess waits for the next minute
            m.bossKills = Math.Min(_pendingBoss, m.kills);
            _pendingBoss -= m.bossKills;
            m.eliteKills = Math.Min(_pendingElite, m.kills - m.bossKills);
            _pendingElite -= m.eliteKills;
            if (last) _pendingBoss = _pendingElite = 0;

            // per-weapon kills: the ledger's deltas, at most the minute's kills (the rest waits for the next minute)
            m.wk = new int[_weapons.Count];
            var room = m.kills;
            for (var i = 0; i < _weapons.Count; i++)
            {
                var delta = Math.Max(0, _weaponNow[i] - _weaponLogged[i]);
                var take = Math.Min(delta, room);
                m.wk[i] = take;
                room -= take;
                _weaponLogged[i] += take;
            }
            var dealt = 0f;
            for (var i = 0; i < _weaponDamageNow.Count; i++) dealt += _weaponDamageNow[i];
            m.dealt = Math.Max(0f, dealt - _dealtLogged);
            _dealtLogged = Math.Max(_dealtLogged, dealt);

            _minutes.Add(m);
            _last = now;
            _current = last ? null : new Minute();
        }

        // ---- totals (tests, the submitter's own check) ----

        public int TotalKills()
        {
            var n = 0;
            foreach (var m in _minutes) n += m.kills;
            return n;
        }

        public int LastLevel => _minutes.Count > 0 ? _minutes[_minutes.Count - 1].level : 0;

        /// <summary>Kills of minute <paramref name="minute"/> by weapon <paramref name="weaponId"/> (0 when none).</summary>
        public int WeaponKills(int minute, string weaponId) =>
            minute >= 0 && minute < _minutes.Count && weaponId != null && _weaponIndex.TryGetValue(weaponId, out var i) && i < _minutes[minute].wk.Length
                ? _minutes[minute].wk[i] : 0;

        // ---- JSON ----

        /// <summary>The log as JSON, at most <paramref name="maxBytes"/> UTF-8 bytes when thinning the input samples can
        /// get it there (every second sample, then every fourth, ...).</summary>
        public string ToJson(int maxBytes = MaxBytes)
        {
            var stride = 1;
            while (true)
            {
                var json = Write(stride);
                if (Encoding.UTF8.GetByteCount(json) <= maxBytes || stride >= Math.Max(1, _inputs.Count)) return json;
                stride *= 2;
            }
        }

        string Write(int stride)
        {
            var h = Header ?? new RunLogHeader();
            var sb = new StringBuilder(16 * 1024);
            sb.Append('{');
            Str(sb, "format", Format, true);
            Int(sb, "v", Version);
            Int(sb, "seed", h.Seed);
            Str(sb, "seedSource", Clip(h.SeedSource, 16) ?? "local");
            Str(sb, "issuedRunId", Clip(h.IssuedRunId, 128));
            Str(sb, "runId", Clip(h.RunId, 64));
            Str(sb, "configHash", Clip(h.ConfigHash, 128));
            Str(sb, "balanceVersion", Clip(h.BalanceVersion, 64));
            Str(sb, "contentHash", Clip(h.ContentHash, 128));
            Str(sb, "stageProfile", Clip(h.StageProfile, 128));
            Str(sb, "buildId", Clip(string.IsNullOrEmpty(h.BuildId) ? "unknown" : h.BuildId, 64));
            Str(sb, "captain", Clip(h.Captain, 128));
            Str(sb, "startingWeapon", Clip(h.StartingWeapon, 128));
            Bool(sb, "editor", h.Editor);
            Bool(sb, "developmentBuild", h.DevelopmentBuild);
            Str(sb, "end", EndReason);
            Key(sb, "seconds", false); Num(sb, Round1(Seconds), "0.#");
            Key(sb, "bossSummoned", false); Num(sb, BossSummoned < 0f ? -1f : Round1(BossSummoned), "0.#");
            Key(sb, "bossDefeated", false); Num(sb, BossDefeated < 0f ? -1f : Round1(BossDefeated), "0.#");
            Int(sb, "minuteSeconds", MinuteSeconds);
            sb.Append(",\"weapons\":[");
            for (var i = 0; i < _weapons.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(Esc(CleanId(_weapons[i]))).Append('"');
            }
            sb.Append("],\"minutes\":[");
            var minutes = _minutes.Count;
            for (var i = 0; i < minutes; i++)
            {
                var m = _minutes[i];
                if (i > 0) sb.Append(',');
                sb.Append('{');
                Int(sb, "m", i, true);
                Int(sb, "spawns", m.spawns); Int(sb, "kills", m.kills); Int(sb, "eliteKills", m.eliteKills); Int(sb, "bossKills", m.bossKills);
                Int(sb, "gold", m.gold); Long(sb, "xp", m.xp); Int(sb, "level", m.level);
                Key(sb, "taken", false); Num(sb, Round1(m.taken), "0.#");
                Key(sb, "dealt", false); Num(sb, Round1(m.dealt), "0.#");
                Key(sb, "healed", false); Num(sb, Round1(m.healed), "0.#");
                Int(sb, "pickups", m.pickups); Int(sb, "chests", m.chests); Int(sb, "pois", m.pois); Int(sb, "alive", m.alive);
                Key(sb, "hull", false); Num(sb, Round1(m.hull), "0.#");
                Key(sb, "maxHull", false); Num(sb, Round1(m.maxHull), "0.#");
                sb.Append(",\"wk\":[");
                for (var w = 0; w < _weapons.Count; w++)
                {
                    if (w > 0) sb.Append(',');
                    sb.Append(w < m.wk.Length ? m.wk[w].ToString(CultureInfo.InvariantCulture) : "0");
                }
                sb.Append("]}");
            }
            sb.Append(']');
            Key(sb, "inputEvery", false); Num(sb, InputEvery * stride, "0.#");
            sb.Append(",\"inputCols\":[");
            for (var i = 0; i < InputCols.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(InputCols[i]).Append('"');
            }
            sb.Append("],\"inputs\":[");
            var first = true;
            for (var i = 0; i < _inputs.Count; i += stride)
            {
                var s = _inputs[i];
                if (s.t > Seconds + 1f) break;
                if (!first) sb.Append(',');
                first = false;
                sb.Append('[');
                Num(sb, s.t, "0.#"); sb.Append(',');
                Num(sb, s.x, "0.#"); sb.Append(',');
                Num(sb, s.z, "0.#"); sb.Append(',');
                sb.Append(s.heading.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(s.steer.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(s.throttle.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(s.boost.ToString(CultureInfo.InvariantCulture));
                sb.Append(']');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>The minute part only (same seed and inputs give the same text: the determinism test compares it).</summary>
        public string MinutesJson()
        {
            var json = Write(int.MaxValue);
            var start = json.IndexOf(",\"weapons\":", StringComparison.Ordinal);
            var end = json.IndexOf(",\"inputEvery\":", StringComparison.Ordinal);
            return start >= 0 && end > start ? json.Substring(start + 1, end - start - 1) : json;
        }

        static float Round1(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0f : (float)Math.Round(v, 1, MidpointRounding.AwayFromZero);
        static float Clamp(float v, float min, float max) => float.IsNaN(v) ? 0f : v < min ? min : v > max ? max : v;
        static string Clip(string s, int max) => s == null ? null : s.Length <= max ? s : s.Substring(0, max);

        /// <summary>Weapon ids as the API takes them: 1-64 characters of A-Z, a-z, 0-9, _ . : -.</summary>
        static string CleanId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "unknown";
            var sb = new StringBuilder(Math.Min(id.Length, 64));
            foreach (var c in id)
            {
                if (sb.Length >= 64) break;
                sb.Append(c < 128 && (char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == ':' || c == '-') ? c : '_');
            }
            return sb.ToString();
        }
    }
}
