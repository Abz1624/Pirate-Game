using System.Collections.Generic;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Enemies;
using UnityEngine;

namespace RoguePirates.V4.Spawning
{
    /// <summary>
    /// Hackathon (12 Oct): an island raid's designed defence. While a schedule is set, the next voyage's spawn controller
    /// runs it instead of the timeline (no ambient bands, minute waves, hordes, focus phases, overtime or storm): each
    /// entry's enemies spawn spread over its window, through the same spawn executor (caps, placement, off-screen rules),
    /// and the stage summons the guardian boss at <see cref="GuardianSeconds"/> (in place of the auto-boss time). The
    /// raid HUD sets it before the voyage and clears it when the raid ends; normal voyages never see it.
    /// </summary>
    public static class RaidSchedule
    {
        public sealed class Entry
        {
            public float StartSecond;
            public string Kind;
            public int Count;
        }

        static List<Entry> _entries;

        public static bool Active => _entries != null;
        public static IReadOnlyList<Entry> Entries => _entries;
        public static float GuardianSeconds { get; private set; }
        public static float WindowSeconds { get; private set; } = 60f;
        /// <summary>Enemies the schedule spawned in the current voyage (HUD, tests).</summary>
        public static int Spawned { get; internal set; }

        public static void Set(IEnumerable<Entry> entries, float guardianSeconds, float windowSeconds = 60f)
        {
            _entries = new List<Entry>();
            if (entries != null)
                foreach (var e in entries)
                    if (e != null && e.Count > 0 && !string.IsNullOrEmpty(e.Kind)) _entries.Add(e);
            GuardianSeconds = Mathf.Max(1f, guardianSeconds);
            WindowSeconds = Mathf.Max(5f, windowSeconds);
            Spawned = 0;
        }

        public static void Clear()
        {
            _entries = null;
            Spawned = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Clear();
    }

    /// <summary>Runs <see cref="RaidSchedule"/> inside the spawn controller (one per voyage).</summary>
    internal sealed class RaidSpawner
    {
        readonly SpawnServices _s;
        readonly SpawnExecutor _executor;
        readonly List<RaidSchedule.Entry> _entries = new List<RaidSchedule.Entry>();
        int[] _done = new int[0];
        readonly Dictionary<string, (SpawnProfile profile, string enemyId, float hp)> _profiles = new Dictionary<string, (SpawnProfile, string, float)>();
        SpawnProfile _generic;
        int _instanceId;

        public RaidSpawner(SpawnServices services, SpawnExecutor executor)
        {
            _s = services;
            _executor = executor;
        }

        public void BeginRun()
        {
            _entries.Clear();
            _profiles.Clear();
            _generic = null;
            if (RaidSchedule.Entries != null) _entries.AddRange(RaidSchedule.Entries);
            _done = new int[_entries.Count];
            _instanceId = 0;
            RaidSchedule.Spawned = 0;
        }

        /// <summary>Each entry's enemies spawn evenly over 90% of its window; what the caps or placement held back
        /// keeps trying on later frames.</summary>
        public void Tick(float now)
        {
            var spread = RaidSchedule.WindowSeconds * 0.9f;
            var budget = 6; // spawns per frame
            for (var i = 0; i < _entries.Count && budget > 0; i++)
            {
                var e = _entries[i];
                if (now < e.StartSecond || _done[i] >= e.Count) continue;
                var due = Mathf.Min(e.Count, Mathf.FloorToInt(e.Count * (now - e.StartSecond) / spread) + 1);
                while (_done[i] < due && budget > 0)
                {
                    budget--;
                    if (!TrySpawn(e.Kind)) { budget = 0; break; }
                    _done[i]++;
                    RaidSchedule.Spawned++;
                }
            }
        }

        bool TrySpawn(string kind)
        {
            var (profile, enemyId, hp) = Resolve(kind);
            if (profile == null) return true; // unknown kind: skip it rather than retry forever
            if (_instanceId == 0) _instanceId = _executor.NextInstanceId();
            var leader = -1;
            var heavy = kind.StartsWith("elite_") || kind.Contains("miniboss");
            return _executor.TryBurst(profile, 1, heavy, hp, 1f, _instanceId, ref leader, null, ambient: !heavy, enemyId: enemyId) > 0;
        }

        (SpawnProfile, string, float) Resolve(string kind)
        {
            if (_profiles.TryGetValue(kind, out var known)) return known;
            var result = (default(SpawnProfile), (string)null, 1f);
            var timeline = _s.Timeline;
            if (_generic == null && timeline != null)
                foreach (var p in timeline.phases)
                    if (p != null && p.spawner != null && AmbientSpawner.IsAmbient(p)) { _generic = _s.Profiles.For(p.spawner); if (_generic != null) break; }
            if (kind.StartsWith("elite_"))
            {
                var key = kind.EndsWith("_spawn") ? kind.Substring(0, kind.Length - "_spawn".Length) : kind;
                var spawner = FindSpawner(timeline, key);
                var elite = spawner != null ? _s.Profiles.For(spawner) : null;
                result = elite != null ? (elite, null, 1f) : (_generic, kind.Substring("elite_".Length), 3f);
            }
            else result = (_generic, kind, 1f);
            if (result.Item1 != null && result.Item2 != null && _s.Sink.FindDefinition(result.Item2) < 0) result = (null, null, 1f);
            _profiles[kind] = result;
            return result;
        }

        static EnemySpawnerConfig FindSpawner(WaveTimelineConfig timeline, string key)
        {
            if (timeline == null) return null;
            foreach (var p in timeline.phases)
                if (p != null && p.spawner != null && (p.spawner.name.EndsWith(key) || (p.spawner.legacyProfileId ?? string.Empty).EndsWith(key))) return p.spawner;
            return null;
        }
    }
}
