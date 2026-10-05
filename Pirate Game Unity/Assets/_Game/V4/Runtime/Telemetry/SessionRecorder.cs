using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Events;
using RoguePirates.V4.Foundation.Core;
using UnityEngine;

namespace RoguePirates.V4.Telemetry
{
    /// <summary>
    /// Playtest (6 Oct, owner: "let me play a session, then use that data to verify if the simulations are accurate"): one
    /// voyage as the Monte Carlo model (plan/playtest/montecarlo) sees it. Samples every <see cref="SampleSeconds"/> of run
    /// time (level, XP, kills, gold, hull, enemies, elites alive, theoretical DPS, Pressure), events (picks, chests, points of
    /// interest, items, heals, level-ups, the Beacon and the boss), every elite, miniboss and boss with its spawn and kill
    /// time, damage taken by source (the PlayerDamaged tag) and the weapon ledger; saved once at the run's end (sunk, quit or
    /// the results screen) through <see cref="SessionFile"/>. Voyages only (not the 1v1, not the Module Lab). Reads events,
    /// views and the run's facts through delegates; per tick it only reads numbers (no allocation outside its records).
    /// </summary>
    public sealed class SessionRecorder : IRunModule
    {
        public const float SampleSeconds = 10f;
        /// <summary>Hull gained in one tick at or above this is a heal event; below it is regeneration (summed).</summary>
        const float HealEventMin = 2f;

        struct Sample
        {
            public float t, xp, hull, maxHull, dps, offense, pressure, damageTaken, healed;
            public int level, xpToNext, kills, gold, enemies, elitesAlive;
        }

        struct Ev
        {
            public float t, value;
            public string type, id, kind;
            public int n;
        }

        sealed class Focus
        {
            public int unitId;
            public string id, kind;
            public float spawn, kill = -1f, despawn = -1f, maxHp;
            public bool adaptive;
        }

        sealed class Source
        {
            public float damage;
            public int hits;
        }

        public string Id => "session-recorder";
        public int TickOrder => 900; // after everything it reads

        // ---- sources (the composition root sets them) ----
        public Func<float> RunTime;
        public Func<int> Level, XpToNext, Kills, Gold;
        public Func<float> Xp, Hull, MaxHull;
        public Func<bool> RunExists, HullKnown, SunkNow, RunEnded, ObjectiveSecured;
        public Func<string> Captain, StartingWeapon, ConfigHash;
        public Func<(string id, string version, string hash)?> StageProfile;
        /// <summary>Is this enemy definition (index) a miniboss (the snapshot only knows elite and boss).</summary>
        public Func<int, bool> IsMiniboss;
        /// <summary>The weapon ledger (damage, kills, hits per weapon).</summary>
        public RunTelemetry Telemetry;
        /// <summary>Where the file goes (tests swap it). Default: <see cref="SessionFile.Save"/>.</summary>
        public Action<string, string> Save = SessionFile.Save;

        CombatEvents _events;
        IEnemyWorldView _enemies;
        Difficulty.IPressureView _pressure;
        Difficulty.IDifficultyLevers _levers;

        readonly List<Sample> _samples = new List<Sample>(256);
        readonly List<Ev> _log = new List<Ev>(512);
        readonly List<Focus> _focus = new List<Focus>(64);
        readonly Dictionary<string, Source> _damage = new Dictionary<string, Source>();
        float _nextSample, _damageTotal, _healTotal, _healPickups, _regen, _bossSummoned = -1f, _bossDefeated = -1f;
        float _prevHull = -1f, _prevMax = -1f, _lastT;
        int _lastLevel, _lastKills, _lastGold;
        float _lastHull, _lastMaxHull;
        bool _bossAlive, _sunk, _began, _saved;
        string _startedAt, _captain, _weapon, _hash, _profileId, _balanceVersion, _contentHash;
        int _seed;

        public bool Saved => _saved;
        public string LastJson { get; private set; }

        public void Initialize(RunContext context)
        {
            _events = context.Events;
            context.TryGet(out _enemies);
            context.TryGet(out _pressure);
            context.TryGet(out _levers);
            if (_events != null) _events.TickPublished += OnEvents;
        }

        public void BeginRun(in RunStart start)
        {
            _samples.Clear(); _log.Clear(); _focus.Clear(); _damage.Clear();
            _nextSample = SampleSeconds;
            _damageTotal = _healTotal = _healPickups = _regen = 0f;
            _bossSummoned = _bossDefeated = -1f;
            _prevHull = _prevMax = -1f;
            _bossAlive = _sunk = _saved = false;
            _began = true;
            _seed = start.Seed;
            _startedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            _captain = Captain?.Invoke() ?? "unknown";
            _weapon = StartingWeapon?.Invoke() ?? "unknown";
            _hash = ConfigHash?.Invoke() ?? string.Empty;
            var profile = StageProfile?.Invoke();
            _profileId = profile?.id ?? string.Empty;
            _balanceVersion = profile?.version ?? string.Empty;
            _contentHash = profile?.hash ?? string.Empty;
            Read();
            AddSample(0f);
        }

        public void Tick(in SimTick tick)
        {
            if (!_began || _saved) return;
            Read();
            if (HullKnown != null && HullKnown())
            {
                // Heals: hull up in a tick while max hull did not change (a Hull Logbook or item raises both).
                if (_prevHull >= 0f && Mathf.Abs(_lastMaxHull - _prevMax) < 0.001f)
                {
                    var gain = _lastHull - _prevHull;
                    if (gain > 0.0001f)
                    {
                        _healTotal += gain;
                        if (gain >= HealEventMin) { _healPickups += gain; Log("heal", null, null, 0, gain); }
                        else _regen += gain;
                    }
                }
                _prevHull = _lastHull;
                _prevMax = _lastMaxHull;
            }
            if (_lastT >= _nextSample)
            {
                AddSample(_nextSample);
                _nextSample += SampleSeconds;
            }
        }

        public void EndRun(in RunEnd end) => Finish();

        /// <summary>The run manager's state changes: the results screen shows at RunComplete (saved then).</summary>
        public void OnRunState(RunState state)
        {
            if (state == RunState.PlayerDead) _sunk = true;
            if (state == RunState.RunComplete) Finish();
        }

        // ---- hooks (the composition root wires them) ----

        public void OnOfferChosen(Progression.Offer offer) =>
            Log("pick", offer.Id, offer.Kind.ToString(), Level != null ? Level() : _lastLevel, offer.Rarity);

        public void OnEvolved(string weaponId, string evolutionId) => Log("evolve", weaponId, evolutionId, 0, 0f);
        public void OnItemAcquired(string itemId) => Log("item", itemId, null, 0, 0f);
        public void OnPoiUsed(string type) => Log("poi", type, null, 0, 0f);

        void Log(string type, string id, string kind, int n, float value)
        {
            if (!_began || _saved) return;
            _log.Add(new Ev { t = Now, type = type, id = id, kind = kind, n = n, value = value });
        }

        float Now => RunTime != null ? RunTime() : _lastT;

        void Read()
        {
            _lastT = Now;
            if (Level != null) _lastLevel = Level();
            if (Kills != null) _lastKills = Kills();
            if (Gold != null) _lastGold = Gold();
            if (HullKnown != null && HullKnown())
            {
                _lastHull = Hull != null ? Hull() : 0f;
                _lastMaxHull = MaxHull != null ? MaxHull() : 0f;
            }
        }

        void AddSample(float t)
        {
            var elites = 0;
            for (var i = 0; i < _focus.Count; i++)
            {
                var f = _focus[i];
                if (f.kill >= 0f || f.despawn >= 0f) continue;
                if (_enemies != null && !_enemies.TryFindByUnitId(f.unitId, out _)) { f.despawn = _lastT; continue; }
                if (f.kind == "elite") elites++;
            }
            _samples.Add(new Sample
            {
                t = t, level = _lastLevel, xp = Xp != null ? Xp() : 0f, xpToNext = XpToNext != null ? XpToNext() : 0, kills = _lastKills, gold = _lastGold,
                hull = _lastHull, maxHull = _lastMaxHull, enemies = _enemies != null ? _enemies.ActiveCount : 0, elitesAlive = elites,
                damageTaken = _damageTotal, healed = _healTotal, dps = _levers != null ? _levers.PlayerDps : 0f, offense = _pressure != null ? _pressure.Offense : 0f, pressure = _pressure != null ? _pressure.Pressure : 0f,
            });
        }

        void OnEvents(IReadOnlyList<CombatEvent> batch)
        {
            if (!_began || _saved) return;
            for (var i = 0; i < batch.Count; i++)
            {
                var e = batch[i];
                switch (e.Kind)
                {
                    case CombatEventKind.Spawned: OnSpawned(e); break;
                    case CombatEventKind.Killed:
                        for (var f = 0; f < _focus.Count; f++)
                            if (_focus[f].unitId == e.A && _focus[f].kill < 0f) { _focus[f].kill = e.Z; break; }
                        break;
                    case CombatEventKind.PlayerDamaged:
                    {
                        var source = e.Tag ?? "other";
                        if (source == "stage_hit" && _bossAlive) source = "boss_director"; // the boss's slams and marks
                        if (!_damage.TryGetValue(source, out var s)) _damage[source] = s = new Source();
                        s.damage += e.Z;
                        s.hits++;
                        _damageTotal += e.Z;
                        break;
                    }
                    case CombatEventKind.LevelUp: Log("level", null, null, e.A, 0f); break;
                    case CombatEventKind.ChestOpened: Log("chest", null, null, e.A, 0f); break;
                    case CombatEventKind.LandmarkUsed: Log("landmark", e.Tag, null, e.A, 0f); break;
                    case CombatEventKind.BeaconState: Log("beacon", null, null, e.A, 0f); break;
                    case CombatEventKind.BossArriving: Log("boss_arriving", null, null, 0, e.Z); break;
                    case CombatEventKind.BossState:
                        if (e.A == 1 && _bossSummoned < 0f) { _bossSummoned = Now; _bossAlive = true; Log("boss_summoned", null, null, e.B, 0f); }
                        else if (e.A == 2 && _bossDefeated < 0f) { _bossDefeated = Now; _bossAlive = false; Log("boss_defeated", null, null, e.B, 0f); }
                        break;
                    case CombatEventKind.RunState:
                        if (e.A == (int)RunState.PlayerDead) _sunk = true;
                        break;
                }
            }
        }

        void OnSpawned(in CombatEvent e)
        {
            if (_enemies == null || !_enemies.TryFindByUnitId(e.A, out var slot) || !_enemies.TryGet(slot, out var snap)) return;
            var kind = snap.isBoss ? "boss" : IsMiniboss != null && IsMiniboss(e.B) ? "miniboss" : snap.isElite ? "elite" : null;
            if (kind == null) return;
            _focus.Add(new Focus
            {
                unitId = e.A, id = e.Tag ?? snap.enemyId, kind = kind, spawn = Now, maxHp = snap.maxHp,
                adaptive = (e.Flags & CombatEventFlags.AdaptiveElite) != 0,
            });
        }

        /// <summary>Saves once: the final sample, the end reason, the file.</summary>
        public void Finish()
        {
            if (!_began || _saved) return;
            if (RunExists == null || SafeBool(RunExists)) Read(); // on a quit the run manager may already be gone: keep the last values
            var sunk = _sunk || (SunkNow != null && SafeBool(SunkNow));
            var reason = sunk ? "sunk" : RunEnded != null && SafeBool(RunEnded) && ObjectiveSecured != null && SafeBool(ObjectiveSecured) ? "victory" : "quit";
            AddSample(_lastT);
            _saved = true;
            var json = ToJson(reason);
            LastJson = json;
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            try { Save?.Invoke($"session-{stamp}-{Clean(_captain)}.json", json); }
            catch (Exception ex) { Debug.LogException(ex); } // a failed save never blocks the run end
        }

        static bool SafeBool(Func<bool> f)
        {
            try { return f(); } catch { return false; }
        }

        static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "unknown";
            var sb = new StringBuilder(s.Length);
            foreach (var c in s) sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            return sb.ToString();
        }

        // ---- JSON ----

        string ToJson(string reason)
        {
            var sb = new StringBuilder(64 * 1024);
            sb.Append("{\"format\":\"rp-session-1\",\"header\":{");
            Str(sb, "startedAt", _startedAt, true);
            Str(sb, "endedAt", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
            Str(sb, "platform", Application.platform.ToString());
            Str(sb, "appVersion", Application.version);
            Str(sb, "unityVersion", Application.unityVersion);
            Bool(sb, "editor", Application.isEditor);
            Bool(sb, "developmentBuild", Debug.isDebugBuild);
            Str(sb, "captain", _captain);
            Str(sb, "startingWeapon", _weapon);
            Int(sb, "seed", _seed);
            Str(sb, "configHash", _hash);
            Str(sb, "stageProfile", _profileId);
            Str(sb, "balanceVersion", _balanceVersion);
            Str(sb, "contentHash", _contentHash);
            Str(sb, "endReason", reason);
            Num(sb, "endTime", _lastT);
            Num(sb, "bossSummoned", _bossSummoned);
            Num(sb, "bossDefeated", _bossDefeated);
            Int(sb, "finalLevel", _lastLevel);
            Int(sb, "finalKills", _lastKills);
            Num(sb, "sampleSeconds", SampleSeconds);
            sb.Append("},\"samples\":[");
            for (var i = 0; i < _samples.Count; i++)
            {
                var s = _samples[i];
                if (i > 0) sb.Append(',');
                sb.Append('{');
                Num(sb, "t", s.t, true); Int(sb, "level", s.level); Num(sb, "xp", s.xp); Int(sb, "xpToNext", s.xpToNext); Int(sb, "kills", s.kills);
                Int(sb, "gold", s.gold); Num(sb, "hull", s.hull); Num(sb, "maxHull", s.maxHull); Num(sb, "damageTaken", s.damageTaken); Num(sb, "healed", s.healed); Int(sb, "enemies", s.enemies);
                Int(sb, "elitesAlive", s.elitesAlive); Num(sb, "dps", s.dps); Num(sb, "offense", s.offense); Num(sb, "pressure", s.pressure);
                sb.Append('}');
            }
            sb.Append("],\"events\":[");
            for (var i = 0; i < _log.Count; i++)
            {
                var e = _log[i];
                if (i > 0) sb.Append(',');
                sb.Append('{');
                Num(sb, "t", e.t, true); Str(sb, "type", e.type);
                if (e.id != null) Str(sb, "id", e.id);
                if (e.kind != null) Str(sb, "kind", e.kind);
                if (e.n != 0) Int(sb, "n", e.n);
                if (e.value != 0f) Num(sb, "value", e.value);
                sb.Append('}');
            }
            sb.Append("],\"focus\":[");
            for (var i = 0; i < _focus.Count; i++)
            {
                var f = _focus[i];
                if (i > 0) sb.Append(',');
                sb.Append('{');
                Str(sb, "id", f.id, true); Str(sb, "kind", f.kind); Int(sb, "unitId", f.unitId); Num(sb, "spawn", f.spawn);
                Num(sb, "kill", f.kill); Num(sb, "despawn", f.despawn); Num(sb, "maxHp", f.maxHp); Bool(sb, "adaptive", f.adaptive);
                sb.Append('}');
            }
            sb.Append("],\"damageTaken\":{");
            Num(sb, "total", _damageTotal, true);
            sb.Append(",\"bySource\":{");
            var first = true;
            foreach (var kv in _damage)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(Esc(kv.Key)).Append("\":{");
                Num(sb, "damage", kv.Value.damage, true); Int(sb, "hits", kv.Value.hits);
                sb.Append('}');
            }
            sb.Append("}},\"heals\":{");
            Num(sb, "total", _healTotal, true); Num(sb, "pickupsAndRepairs", _healPickups); Num(sb, "regen", _regen);
            sb.Append("},\"weapons\":{");
            first = true;
            Telemetry?.WeaponTotals((id, damage, kills, hits) =>
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(Esc(id)).Append("\":{");
                Num(sb, "damage", damage, true); Int(sb, "kills", kills); Int(sb, "hits", hits);
                sb.Append('}');
            });
            sb.Append("}}");
            return sb.ToString();
        }

        static void Key(StringBuilder sb, string key, bool first)
        {
            if (!first) sb.Append(',');
            sb.Append('"').Append(key).Append("\":");
        }

        static void Str(StringBuilder sb, string key, string value, bool first = false)
        {
            Key(sb, key, first);
            if (value == null) sb.Append("null");
            else sb.Append('"').Append(Esc(value)).Append('"');
        }

        static void Num(StringBuilder sb, string key, float value, bool first = false)
        {
            Key(sb, key, first);
            if (float.IsNaN(value) || float.IsInfinity(value)) sb.Append("null");
            else sb.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        static void Int(StringBuilder sb, string key, int value, bool first = false)
        {
            Key(sb, key, first);
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        static void Bool(StringBuilder sb, string key, bool value, bool first = false)
        {
            Key(sb, key, first);
            sb.Append(value ? "true" : "false");
        }

        static string Esc(string s)
        {
            if (s.IndexOfAny(new[] { '"', '\\', '\n', '\r', '\t' }) < 0) return s;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        public void Detach()
        {
            if (_events != null) _events.TickPublished -= OnEvents;
            _events = null;
        }
    }
}
