using System;
using System.Collections.Generic;
using System.Linq;
using HighSeas.Balance;
using HighSeas.Data;
using HighSeas.Enemies;
using RoguePirates.V4.Config;
using RoguePirates.V4.Spawning;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Legacy
{
    /// <summary>
    /// Carries the V4 config into the legacy run (M1): a per-run copy of the V3 stage profile with the V4-owned fields
    /// written in (the rest pass through, see LegacyConfigMapping), and a per-run copy of the Resources encounter
    /// schedule whose spawn profiles are per-run copies too. Live config edits patch these copies, so no V3 asset is
    /// ever modified. Values V3 caches at the spawner reset (band start times, global spawn gap, spawner start delay)
    /// are written only when the run starts; to change them, restart with a preset (the console's "Restart with it").
    /// </summary>
    [DeletedInPhase(14, "Legacy profile and schedule are gone after switch-over")]
    public sealed class LegacyConfigRuntime : IDisposable, ISpawnerProfiles
    {
        readonly ConfigSetRuntime _config;
        readonly StageBalanceProfile _baseProfile;
        readonly Dictionary<SpawnProfile, SpawnProfile> _clones = new Dictionary<SpawnProfile, SpawnProfile>();
        readonly Dictionary<string, SpawnProfile> _cloneBySpawnerId = new Dictionary<string, SpawnProfile>();
        readonly List<Object> _created = new List<Object>();

        public StageBalanceProfile RuntimeProfile { get; private set; }
        public EncounterSpawnSchedule RuntimeSchedule { get; private set; }
        /// <summary>
        /// M3: the schedule the kept legacy EncounterSpawner gets: the same profile copies, no ambient entries (the V4
        /// spawn controller runs the ambient bands). The full schedule stays on the runtime profile for its export.
        /// </summary>
        public EncounterSpawnSchedule EncounterOnlySchedule { get; private set; }
        public event Action ProfileChanged;

        WaveTimelineConfig Timeline => _config.Single<WaveTimelineConfig>();
        DifficultyCurveConfig Difficulty => _config.Single<DifficultyCurveConfig>();
        StageContentConfig Stage => _config.Single<StageContentConfig>();

        public LegacyConfigRuntime(ConfigSetRuntime config, StageBalanceProfile baseProfile)
        {
            _config = config;
            _baseProfile = baseProfile;
        }

        /// <summary>Builds the schedule and profile copies. Returns false (with a reason) when inputs are missing.</summary>
        public bool Build(out string error)
        {
            error = null;
            if (_config == null || Timeline == null || Difficulty == null) { error = "config set has no timeline/difficulty"; return false; }
            if (_baseProfile == null) { error = "no legacy base profile assigned on V4Game"; return false; }
            var source = Resources.Load<EncounterSpawnSchedule>("EncounterSpawnSchedule"); // the copy the legacy spawner loads
            if (source == null) { error = "Resources/EncounterSpawnSchedule missing"; return false; }

            RuntimeSchedule = Track(Object.Instantiate(source));
            RuntimeSchedule.name = source.name;
            if (RuntimeSchedule.entries != null)
                foreach (var entry in RuntimeSchedule.entries) entry.profile = CloneOf(entry.profile);
            RuntimeSchedule.hammerheadBeat = CloneOf(source.hammerheadBeat);
            RuntimeSchedule.krakenBeat = CloneOf(source.krakenBeat);
            RuntimeSchedule.swarmPulse = CloneOf(source.swarmPulse);
            RuntimeSchedule.drownedPursuer = CloneOf(source.drownedPursuer);
            RuntimeSchedule.drownedHarrier = CloneOf(source.drownedHarrier);
            RuntimeSchedule.routeTreasureRaider = CloneOf(source.routeTreasureRaider);
            RuntimeSchedule.routeCursedRingSwordfish = CloneOf(source.routeCursedRingSwordfish);
            RuntimeSchedule.routeCursedRingCannon = CloneOf(source.routeCursedRingCannon);
            RuntimeSchedule.powderCacheAmbush = CloneOf(source.powderCacheAmbush);
            RuntimeSchedule.bossSummon = CloneOf(source.bossSummon);

            foreach (var spawner in _config.All<EnemySpawnerConfig>())
            {
                var clone = _clones.Values.FirstOrDefault(c => c.ResolvedId == spawner.legacyProfileId);
                if (clone == null)
                {
                    // A V4-only spawner (no V3 profile, e.g. the Powder Cache elites, M8 playtest): its own runtime profile,
                    // outside V3's schedule, filled from the config below and anchored at run start.
                    if (spawner.legacyProfileId == null || !spawner.legacyProfileId.StartsWith("v4_", StringComparison.Ordinal))
                        Debug.LogWarning($"V4: spawner {spawner.configId} matches no V3 spawn profile ('{spawner.legacyProfileId}'); it gets its own. V4-only spawners use a 'v4_' profile id.");
                    clone = Track(ScriptableObject.CreateInstance<SpawnProfile>());
                    clone.name = clone.profileId = string.IsNullOrEmpty(spawner.legacyProfileId) ? spawner.configId : spawner.legacyProfileId;
                }
                _cloneBySpawnerId[spawner.configId] = clone;
                LegacyConfigMapping.ApplySpawner(spawner, clone);
            }
            LegacyConfigMapping.ApplySchedule(Timeline, RuntimeSchedule, ProfileFor, atRunStart: true);

            EncounterOnlySchedule = Track(Object.Instantiate(RuntimeSchedule));
            EncounterOnlySchedule.name = source.name;
            EncounterOnlySchedule.entries = Array.Empty<ScheduledSpawnEntry>();

            RuntimeProfile = Track(Object.Instantiate(_baseProfile));
            RuntimeProfile.name = _baseProfile.name;
            LegacyConfigMapping.ApplyProfile(Timeline, Difficulty, RuntimeProfile, Stage);
            RuntimeProfile.encounterSchedule = RuntimeSchedule;

            _config.ConfigChanged += OnConfigChanged;
            return true;
        }

        /// <summary>
        /// Points every runtime spawn profile at the per-run enemy copy with the same id (M2), so every spawn path
        /// (beats, encounters, overtime, boss, route events) reads config values, not only the ambient pick.
        /// </summary>
        public void RetargetEnemies(GameDatabase runDatabase)
        {
            if (runDatabase == null) return;
            _runDatabase = runDatabase;
            foreach (var clone in _clones.Values)
            {
                if (clone == null || clone.enemy == null) continue;
                var copy = runDatabase.GetEnemy(clone.enemy.id);
                if (copy != null) clone.enemy = copy;
            }
            // A spawner's anchor archetype decides its enemy (R-05: overtime spawners anchor the Drowned enemies; the
            // other spawners' anchors are V3's profile enemies, so nothing else changes).
            foreach (var spawner in _config.All<EnemySpawnerConfig>()) ApplyAnchor(spawner);
        }

        GameDatabase _runDatabase;

        void ApplyAnchor(EnemySpawnerConfig spawner)
        {
            if (_runDatabase == null || spawner?.anchor == null || !_cloneBySpawnerId.TryGetValue(spawner.configId, out var clone)) return;
            var def = _runDatabase.GetEnemy(spawner.anchor.enemyId);
            if (def != null) clone.enemy = def;
        }

        /// <summary>The runtime spawn profile a spawner config drives (the V4 spawn controller spawns with it).</summary>
        public SpawnProfile For(EnemySpawnerConfig spawner) => ProfileFor(spawner);

        SpawnProfile ProfileFor(EnemySpawnerConfig spawner) =>
            spawner != null && _cloneBySpawnerId.TryGetValue(spawner.configId, out var clone) ? clone : null;

        SpawnProfile CloneOf(SpawnProfile source)
        {
            if (source == null) return null;
            if (_clones.TryGetValue(source, out var clone)) return clone;
            clone = Track(Object.Instantiate(source));
            clone.name = source.name; // the profile export keys schedule profiles by name
            return _clones[source] = clone;
        }

        T Track<T>(T obj) where T : Object
        {
            obj.hideFlags = HideFlags.DontSave;
            _created.Add(obj);
            return obj;
        }

        void OnConfigChanged(ConfigChange change)
        {
            var config = _config.Get<V4ConfigAsset>(change.configId);
            switch (config)
            {
                case EnemySpawnerConfig spawner:
                    if (_cloneBySpawnerId.TryGetValue(spawner.configId, out var clone)) LegacyConfigMapping.ApplySpawner(spawner, clone);
                    ApplyAnchor(spawner);
                    LegacyConfigMapping.ApplySchedule(Timeline, RuntimeSchedule, ProfileFor, atRunStart: false); // band rates win over spawner intervals
                    LegacyConfigMapping.ApplySchedule(Timeline, EncounterOnlySchedule, ProfileFor, atRunStart: false);
                    break;
                case WaveTimelineConfig _:
                case DifficultyCurveConfig _:
                    LegacyConfigMapping.ApplySchedule(Timeline, RuntimeSchedule, ProfileFor, atRunStart: false);
                    // The kept encounter executor reads grace and max-active from its own copy (boss/cache caps).
                    LegacyConfigMapping.ApplySchedule(Timeline, EncounterOnlySchedule, ProfileFor, atRunStart: false);
                    LegacyConfigMapping.ApplyProfile(Timeline, Difficulty, RuntimeProfile, Stage);
                    ProfileChanged?.Invoke();
                    break;
                case StageContentConfig stage:
                    // Chest prices and landmark values reach the rebuilt profile export; placement counts apply next run.
                    LegacyConfigMapping.ApplyStage(stage, RuntimeProfile);
                    ProfileChanged?.Invoke();
                    break;
            }
        }

        public void Dispose()
        {
            if (_config != null) _config.ConfigChanged -= OnConfigChanged;
            foreach (var o in _created) if (o != null) { if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o); }
            _created.Clear();
        }
    }
}
