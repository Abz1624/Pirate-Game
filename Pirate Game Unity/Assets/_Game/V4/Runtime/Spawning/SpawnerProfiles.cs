using System;
using System.Collections.Generic;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.Enemies;
using RoguePirates.V4.Config;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Spawning
{
    /// <summary>
    /// The runtime spawn profile of every spawner config (M14; was LegacyConfigRuntime's per-run clones of V3's Resources
    /// schedule profiles with the V4 values written in). Each profile is built from its config alone — the special-
    /// behaviour modules are V4 data since M14 — and kept current on config edits. The profile type is the shared spawn
    /// data type the enemy simulation reads (moved with the other shared definition types in M15).
    /// </summary>
    public sealed class SpawnerProfiles : ISpawnerProfiles, IDisposable
    {
        readonly ConfigSetRuntime _config;
        readonly Dictionary<string, SpawnProfile> _bySpawner = new Dictionary<string, SpawnProfile>();
        readonly Dictionary<SpawnProfile, SpawnProfile> _eliteCopies = new Dictionary<SpawnProfile, SpawnProfile>();
        readonly List<Object> _created = new List<Object>();
        Func<string, EnemyDefinition> _enemy;

        public SpawnerProfiles(ConfigSetRuntime config) { _config = config; }

        WaveTimelineConfig Timeline => _config?.Single<WaveTimelineConfig>();

        /// <summary>Builds one profile per spawner config (run start, before any spawn).</summary>
        public void Build()
        {
            if (_config == null) return;
            foreach (var spawner in _config.All<EnemySpawnerConfig>())
            {
                var profile = Track(ScriptableObject.CreateInstance<SpawnProfile>());
                // The profile id is V3's (legacy code and traces key on it); V4-only spawners use their "v4_" id.
                profile.name = profile.profileId = string.IsNullOrEmpty(spawner.legacyProfileId) ? spawner.configId : spawner.legacyProfileId;
                _bySpawner[spawner.configId] = profile;
                Apply(spawner, profile);
            }
            foreach (var spawner in _config.All<EnemySpawnerConfig>()) ApplySummon(spawner);
            ApplyBandRates();
            _config.ConfigChanged += OnConfigChanged;
        }

        /// <summary>Points every profile at the run's copy of its anchor enemy (the per-run database, M2).</summary>
        public void BindEnemies(Func<string, EnemyDefinition> enemy)
        {
            _enemy = enemy;
            if (_config == null) return;
            foreach (var spawner in _config.All<EnemySpawnerConfig>()) ApplyAnchor(spawner);
        }

        public SpawnProfile For(EnemySpawnerConfig spawner) =>
            spawner != null && _bySpawner.TryGetValue(spawner.configId, out var profile) ? profile : null;

        /// <summary>
        /// M11: the elite copy of a profile for one adaptive-elite spawn, current at this moment (same movement, attacks and
        /// modules; elite settings from the director config). One reusable copy per source profile (M14: V3 instantiated and
        /// destroyed a copy per spawn); the agent is pointed back at the source profile after the spawn, so spawner caps
        /// count it.
        /// </summary>
        public SpawnProfile EliteCopy(SpawnProfile source, DirectorConfig d)
        {
            if (source == null || d == null) return source;
            if (!_eliteCopies.TryGetValue(source, out var copy))
            {
                copy = Track(ScriptableObject.CreateInstance<SpawnProfile>());
                copy.name = source.name + " (adaptive elite)";
                copy.elite = new SpawnEliteSettings();
                _eliteCopies[source] = copy;
            }
            copy.profileId = source.profileId;
            copy.enemy = source.enemy;
            copy.spawnMode = source.spawnMode;
            copy.groupLayout = source.groupLayout;
            copy.timing = source.timing;
            copy.placement = source.placement;
            copy.movement = source.movement;
            copy.attack = source.attack;
            copy.specialBehavior = source.specialBehavior;
            copy.formation = source.formation;
            copy.summon = source.summon;
            copy.tether = source.tether;
            copy.supportAura = source.supportAura;
            copy.phaseDash = source.phaseDash;
            copy.terrainAmbush = source.terrainAmbush;
            copy.adaptive = source.adaptive;
            var e = copy.elite;
            e.isElite = true;
            e.affix = (EliteAffix)(int)d.eliteAffix; // same order (tested)
            e.hpMultiplier = Mathf.Max(0.1f, d.eliteHpMultiplier);
            e.damageMultiplier = Mathf.Max(0.1f, d.eliteDamageMultiplier);
            e.sizeMultiplier = Mathf.Max(0.1f, d.eliteSizeMultiplier);
            e.speedMultiplier = source.elite != null ? source.elite.speedMultiplier : 1f;
            e.goldMultiplier = 1f;
            e.xpMultiplier = 1f;
            return copy;
        }

        /// <summary>The config's blocks onto the profile (every block, including the special-behaviour modules).</summary>
        public static void Apply(EnemySpawnerConfig c, SpawnProfile p)
        {
            p.spawnMode = (SpawnMode)(int)c.spawnMode;
            p.groupLayout = (SpawnGroupLayout)(int)c.groupLayout;
            FieldMirror.CopyFields(c.timing, p.timing);
            FieldMirror.CopyFields(c.placement, p.placement);
            FieldMirror.CopyFields(c.elite, p.elite);
            FieldMirror.CopyFields(c.movement, p.movement);
            FieldMirror.CopyFields(c.attack, p.attack);
            p.specialBehavior = (EnemySpecialBehavior)(int)c.specialBehavior;
            FieldMirror.CopyFields(c.formation, p.formation);
            FieldMirror.CopyFields(c.summon, p.summon);
            FieldMirror.CopyFields(c.tether, p.tether);
            FieldMirror.CopyFields(c.supportAura, p.supportAura);
            FieldMirror.CopyFields(c.phaseDash, p.phaseDash);
            FieldMirror.CopyFields(c.terrainAmbush, p.terrainAmbush);
            FieldMirror.CopyFields(c.adaptive, p.adaptive);
        }

        void ApplySummon(EnemySpawnerConfig spawner)
        {
            if (For(spawner) is SpawnProfile profile) profile.summon.minionProfile = For(spawner.summon.minionSpawner);
        }

        void ApplyAnchor(EnemySpawnerConfig spawner)
        {
            var profile = For(spawner);
            if (profile == null || spawner.anchor == null || _enemy == null) return;
            var def = _enemy(spawner.anchor.enemyId);
            if (def != null) profile.enemy = def;
        }

        /// <summary>A band phase's rate sets its spawner's interval (band rates win over spawner intervals).</summary>
        void ApplyBandRates()
        {
            var t = Timeline;
            if (t?.phases == null) return;
            foreach (var phase in t.phases)
            {
                if (phase.spawner == null || phase.phaseId == null || !phase.phaseId.StartsWith(PhaseIds.BandPrefix, StringComparison.Ordinal)) continue;
                var profile = For(phase.spawner);
                if (profile != null && phase.ratePerSecond > 0f) profile.timing.spawnIntervalSeconds = 1f / phase.ratePerSecond;
            }
        }

        void OnConfigChanged(ConfigChange change)
        {
            switch (_config.Get<V4ConfigAsset>(change.configId))
            {
                case EnemySpawnerConfig spawner:
                    if (For(spawner) is SpawnProfile profile) Apply(spawner, profile);
                    ApplySummon(spawner);
                    ApplyAnchor(spawner);
                    ApplyBandRates();
                    break;
                case WaveTimelineConfig _:
                    ApplyBandRates();
                    break;
            }
        }

        T Track<T>(T obj) where T : Object
        {
            obj.hideFlags = HideFlags.DontSave;
            _created.Add(obj);
            return obj;
        }

        public void Dispose()
        {
            if (_config != null) _config.ConfigChanged -= OnConfigChanged;
            foreach (var o in _created) if (o != null) { if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o); }
            _created.Clear();
            _bySpawner.Clear();
            _eliteCopies.Clear();
        }
    }
}
