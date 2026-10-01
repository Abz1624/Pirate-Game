using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    public enum PhaseTriggerKind { Time, Event, AfterPhaseCleared, Condition }
    public enum PhaseConditionKind { PlayerLevelAtLeast, KillsAtLeast, PressureAtLeast }
    public enum PhaseDurationKind { UntilNext, Seconds, UntilCleared }
    public enum SpawnBudgetUnit { Spawns, Threat }
    public enum TagCapScope { Any, SpecialistPool }
    public enum FocusPlacement { SpawnerProfile, RingAroundPlayer }
    public enum BossConflict { SkipPermanently, WaitUntilBossEnds }
    public enum StormDamageMode
    {
        [Tooltip("Steady damage every frame, not blocked by and not granting the ship's hit immunity (R-57 decision).")] Steady,
        [Tooltip("V3: a hit every frame through the ship's 0.2 s hit immunity (about 0.7 damage/s, and it blocks enemy hits).")] PerFrameHit
    }

    [Serializable]
    public sealed class PhaseTrigger
    {
        public PhaseTriggerKind kind = PhaseTriggerKind.Time;
        [Tooltip("Run time in seconds (Time trigger). Live until the phase starts; ambient bands run in time order, each until the next band starts. Overtime starts at the stage deadline instead.")]
        public float timeSeconds;
        [Tooltip("CombatEvents kind name (Event trigger).")] public string eventName = string.Empty;
        [Tooltip("Phase id that must be cleared first (AfterPhaseCleared).")] public string afterPhaseId = string.Empty;
        public PhaseConditionKind conditionKind = PhaseConditionKind.PlayerLevelAtLeast;
        public float conditionValue;
    }

    [Serializable]
    public sealed class PhaseDuration
    {
        public PhaseDurationKind kind = PhaseDurationKind.UntilNext;
        public float seconds;
    }

    [Serializable]
    public sealed class PhaseWarning
    {
        [Tooltip("Seconds before the trigger; 0 = no warning.")] public float leadSeconds;
        [Tooltip("{label} is replaced with the phase label, {lead} with the lead seconds.")] public string template = "WARNING: {label} incoming!";
    }

    [Serializable]
    public sealed class PoolEntry
    {
        [Tooltip("Legacy enemy id; specialist variants get archetype assets in M2.")] public string enemyId = string.Empty;
        public EnemyArchetypeConfig archetype;
        [Tooltip("Set for specialist variants (database aliases).")] public EnemyVariantConfig variant;
        public float weight = 1f;
    }

    [Serializable]
    public sealed class SpawnPool
    {
        public string poolId = "ordinary";
        [Tooltip("Chance a spawn uses this pool (ordinary share; the specialist pool gets the rest).")]
        [Range(0f, 1f)] public float share = 1f;
        public List<PoolEntry> entries = new List<PoolEntry>();
    }

    [Serializable]
    public sealed class TagCap
    {
        [Tooltip("Concurrency tag on archetypes, e.g. ranged_gunner, area_denial, charge_windup.")]
        public string tag = string.Empty;
        public int max = 2;
        public TagCapScope scope = TagCapScope.Any;
    }

    /// <summary>
    /// A scripted spawn of one enemy when the phase starts (the Reef Crab miniboss, scheduled elites). With the phase's
    /// pauseAmbient on, ambient spawns stop until that enemy dies (the miniboss arena).
    /// </summary>
    [Serializable]
    public sealed class FocusSpawn
    {
        public bool enabled;
        [Tooltip("SpawnerProfile: the phase's spawner places its anchor enemy (with ordinary-time scaling). RingAroundPlayer: this archetype at ringDistance, at a random angle from the loot stream.")]
        public FocusPlacement placement = FocusPlacement.SpawnerProfile;
        [Tooltip("Enemy for RingAroundPlayer placement.")] public EnemyArchetypeConfig archetype;
        [Tooltip("SpawnerProfile placement: spawn this variant instead of the spawner's anchor enemy (R-03: the scheduled elites spawn their elite variant, with its own combat profile).")]
        public EnemyVariantConfig variant;
        public float ringDistance = 22f;
        [Tooltip("RingAroundPlayer only (spawner placement uses the spawner's elite settings).")] public ConfigEnums.EliteAffix affix = ConfigEnums.EliteAffix.None;
        public float speedMultiplier = 1f, hpMultiplier = 1f, damageMultiplier = 1f;
        [Tooltip("During the boss fight: skip for good (V3 elites) or wait until it ends (V3 Reef Crab).")]
        public BossConflict bossConflict = BossConflict.SkipPermanently;
        [Tooltip("Keep retrying this long after the trigger when no slot or position is free; 0 = until it spawns.")]
        public float retryWindowSeconds = 20f;
        [Tooltip("Index of the scheduled elite this is, for Rum (-1 = not a scheduled elite).")] public int scheduledEliteIndex = -1;
        [Tooltip("Do not spawn while another arena is running.")] public bool waitForOtherArena;
        [Tooltip("Shown when it spawns; {label} = phase label. Empty = no toast.")] public string arrivalToast = "{label} has arrived!";
    }

    [Serializable]
    public sealed class TimelinePhase
    {
        [ConfigIdentity] public string phaseId = "phase";
        public string label = "Phase";
        public PhaseTrigger trigger = new PhaseTrigger();
        public PhaseDuration duration = new PhaseDuration();
        [Tooltip("Spawner this phase drives (ambient bands, beats, encounters).")]
        public EnemySpawnerConfig spawner;
        [Tooltip("Second spawner (overtime harrier).")] public EnemySpawnerConfig alternateSpawner;
        [Tooltip("Ambient spawns per second while the phase is active (0 = not an ambient phase).")]
        public float ratePerSecond;
        [Tooltip("Spawns: ratePerSecond is spawns per second. Threat: threat cost per second (an enemy costing 3 waits three times as long).")]
        public SpawnBudgetUnit budgetUnit = SpawnBudgetUnit.Spawns;
        [Tooltip("Ambient composition: the first pool is the base (its share = chance a spawn stays in it); later pools share the rest. Tag caps and tier unlocks filter picks.")]
        public List<SpawnPool> pools = new List<SpawnPool>();
        [Tooltip("With a focus spawn: ambient spawns stop until the focus enemy dies.")] public bool pauseAmbient;
        [Tooltip("Multiplier on the ambient spawn rate while active (swarm pulses, with a Seconds duration).")]
        public float rateMultiplier = 1f;
        [Tooltip("Ambient: enemies per spawn (0 = the spawner's own count).")] public int burstCount;
        public PhaseWarning warning = new PhaseWarning();
        public FocusSpawn focus = new FocusSpawn();
        public bool isBoss, isOvertime, isStorm;
    }

    [Serializable]
    public sealed class OvertimeSettings
    {
        public float initialSpawnIntervalSeconds = 1.4f;
        public float intervalFloorSeconds = 0.35f;
        public float intervalDecayPerStep = 0.85f;
        public float pressureStepSeconds = 30f;
        public float pursuerWeightFirstTwoMinutes = 0.8f;
        [Tooltip("Stored for the profile hash; V3 rolls harrier as 1 - pursuer.")] public float harrierWeightFirstTwoMinutes = 0.2f;
        public float pursuerWeightAfterTwoMinutes = 0.6f;
        [Tooltip("Stored for the profile hash; V3 rolls harrier as 1 - pursuer.")] public float harrierWeightAfterTwoMinutes = 0.4f;
        public string startToast = "DROWNED PURSUIT: spectral fleet incoming!";
        [Tooltip("Extra HP and damage multiplier for overtime spawns, evaluated at each spawn: this at the deadline...")] public float spawnStatRampStart = 1f;
        [Tooltip("...rising linearly to this after spawnStatRampSeconds (then held). 1 and 1 = V3 (no ramp).")] public float spawnStatRampEnd = 2f;
        [Tooltip("Seconds after the deadline to reach spawnStatRampEnd.")] public float spawnStatRampSeconds = 300f;
    }

    [Serializable]
    public sealed class StormSettings
    {
        [Tooltip("Storm start and warning lead are the storm phase's trigger time and warning lead.")]
        public float safeRegionStartFraction = 1f;
        public float safeRegionEndFraction = 0.4f;
        public float contractionStartSeconds = 720f;
        public float contractionEndSeconds = 900f;
        public float outsideDamagePerSecond = 8f;
        public StormDamageMode damageMode = StormDamageMode.Steady;
        public string startToast = "DREAD TIDE: stay inside the safe water!";
        public string edgeToast = "STORM EDGE: return to open water!";
        [Tooltip("M15 playtest (off = V3: nothing happens past the red edge wall before the storm): steady damage while the ship is outside the edge wall, at any time. During the storm the storm's own edge damage applies instead.")]
        public bool edgeWallDamage;
        [Tooltip("Damage per second just outside the edge wall; it grows 8 % per metre further out, like the storm's.")]
        public float edgeWallDamagePerSecond = 8f;
        public string edgeWallToast = "OFF THE MAP: turn back to open water!";
    }

    public enum HordeFormation
    {
        [Tooltip("A closing circle around the ship.")] Ring,
        [Tooltip("A wall side by side, across the ship's path, from one direction.")] Line,
        [Tooltip("A tight pack from one side.")] Cluster
    }

    /// <summary>
    /// V5 M1: one minute of the enemy flow (Vampire Survivors-style). While it is current, a minimum number of enemies is
    /// kept alive (topped up on every interval tick) and the ambient band's rate is multiplied. The floor curve is jagged on
    /// purpose (tension and release), not a ramp.
    /// </summary>
    [Serializable]
    public sealed class MinuteWave
    {
        [Tooltip("Run time (s) this wave starts; it lasts until the next wave's start or the stage deadline.")] public float startSeconds;
        [Tooltip("Minimum enemies alive, as a share of the ambient cap (the device cap minus reserved encounter slots).")]
        [Range(0f, 1f)] public float minAliveShare;
        [Tooltip("Multiplier on the current ambient band's spawn rate while this wave is current.")] public float rateMultiplier = 1f;
        [Tooltip("Seconds between floor top-up checks (Vampire Survivors: 1.0 early, down to 0.25).")] public float topUpIntervalSeconds = 1f;
        [Tooltip("At most this many enemies per top-up tick.")] public int maxTopUpPerTick = 4;
    }

    /// <summary>V5 M1: a telegraphed horde: many weak, fast enemies at once in a formation, away from the boss fight.</summary>
    [Serializable]
    public sealed class HordeEvent
    {
        [ConfigIdentity] public string hordeId = "horde";
        public string label = "Horde";
        public float timeSeconds;
        public HordeFormation formation = HordeFormation.Ring;
        [Tooltip("The enemy the horde is made of (weak fodder reads best).")] public EnemyArchetypeConfig archetype;
        public int count = 16;
        [Tooltip("Distance from the ship where the formation appears.")] public float distance = 20f;
        [Tooltip("Spacing between enemies (Line, Cluster).")] public float spacing = 1.8f;
        public float speedMultiplier = 1.3f;
        public float hpMultiplier = 0.7f;
        public float damageMultiplier = 1f;
        [Tooltip("Warning toast this long before it arrives (0 = none).")] public float warningLeadSeconds = 3f;
        public string warningToast = "HORDE INCOMING!";
    }

    /// <summary>V5 M1: tension and release. After big moments the ambient flow backs off for a while.</summary>
    [Serializable]
    public sealed class ReleaseSettings
    {
        [Tooltip("Ambient rate multiplier during a release window.")] public float rateMultiplier = 0.35f;
        [Tooltip("Floor multiplier during a release window.")] public float floorMultiplier = 0.4f;
        [Tooltip("Release after a miniboss or scheduled elite (arena) is sunk, seconds.")] public float afterArenaSeconds = 20f;
        [Tooltip("Release after the Barnacle King is sunk, seconds.")] public float afterBossKillSeconds = 25f;
        [Tooltip("A horde's release starts this long after it arrives...")] public float hordeReleaseDelaySeconds = 20f;
        [Tooltip("...and lasts this long.")] public float afterHordeSeconds = 15f;
        [Tooltip("Floor multiplier while the Barnacle King fight is on (the fight is the focus; Vampire Survivors drops to ~10 on boss minutes).")]
        public float bossFightFloorMultiplier = 0.3f;
        [Tooltip("A lull (release rate and floor) this long before each horde, so the crowd thins and the horde has room.")]
        public float preHordeLullSeconds = 10f;
    }

    [Serializable]
    public sealed class FlowSettings
    {
        [Tooltip("V5 M1 enemy flow (minute waves, floor, hordes, release). Off = V4 bands only (the v3-parity preset turns it off).")]
        public bool enabled;
        [Tooltip("V5 M3d (owner's sketch, 1 Oct 2026): the floor and the rate multiplier follow a straight line from each wave to the next, so the waves are keyframes of a peak-and-valley curve. Off = each wave holds until the next (M1).")]
        public bool smoothWaves;
        public List<MinuteWave> waves = new List<MinuteWave>();
        public List<HordeEvent> hordes = new List<HordeEvent>();
        public ReleaseSettings release = new ReleaseSettings();

        /// <summary>
        /// The floor share and the rate multiplier at <paramref name="seconds"/>: the current wave's, or with
        /// <see cref="smoothWaves"/> on, a straight line towards the next wave's (V5 M3d). False before the first wave.
        /// </summary>
        public bool Sample(float seconds, out float floorShare, out float rateMultiplier)
        {
            floorShare = 0f;
            rateMultiplier = 1f;
            var current = WaveAt(seconds, out _);
            if (current == null) return false;
            floorShare = current.minAliveShare;
            rateMultiplier = current.rateMultiplier;
            if (!smoothWaves) return true;
            MinuteWave next = null;
            for (var i = 0; i < waves.Count; i++)
            {
                var w = waves[i];
                if (w.startSeconds <= current.startSeconds) continue;
                if (next == null || w.startSeconds < next.startSeconds) next = w;
            }
            if (next == null) return true;
            var t = Mathf.Clamp01((seconds - current.startSeconds) / Mathf.Max(0.001f, next.startSeconds - current.startSeconds));
            floorShare = Mathf.Lerp(current.minAliveShare, next.minAliveShare, t);
            rateMultiplier = Mathf.Lerp(current.rateMultiplier, next.rateMultiplier, t);
            return true;
        }

        /// <summary>The wave current at <paramref name="seconds"/> (waves sorted by start time), or null.</summary>
        public MinuteWave WaveAt(float seconds, out int index)
        {
            index = -1;
            MinuteWave best = null;
            for (var i = 0; i < waves.Count; i++)
            {
                var w = waves[i];
                if (w.startSeconds > seconds) continue;
                if (best == null || w.startSeconds >= best.startSeconds) { best = w; index = i; }
            }
            return best;
        }
    }

    /// <summary>
    /// The Saltwind run schedule: stage limits, ambient bands, scheduled beats, overtime and storm. In M1 its values
    /// reach the legacy profile (hash, POI and chest readers); from M3 the V4 spawn controller runs the phases.
    /// </summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Wave Timeline", fileName = "WaveTimeline")]
    public sealed class WaveTimelineConfig : V4ConfigAsset
    {
        [Header("Stage")]
        public float stageDeadlineSeconds = 600f;
        [Tooltip("No ambient spawns before this run time.")] public float graceSeconds = 2f;
        public int maxActiveEnemies = 80;
        [Tooltip("Slots kept free for Beacon and encounter spawns (ambient cap = max - reserved).")] public int reservedEncounterSlots = 8;
        [Tooltip("V3 (on): a band's spawner overrides the attack timing (charge wind-up, cooldown, range) of its anchor enemy, so the same-looking enemy behaves differently by band. V5 M3d (owner, 1 Oct 2026): off, an enemy always attacks the way its own combat profile says.")]
        public bool spawnerCombatOverrides = true;
        public float globalMinSpawnGapSeconds = 0.35f;
        public float spawnRetryIntervalSeconds = 0.35f;
        [Tooltip("A queued encounter (Beacon boss, Powder Cache) fails if it can't spawn within this many seconds.")] public float requiredEncounterTimeoutSeconds = 10f;
        [Tooltip("V3: the encounter queue waited during miniboss arenas (and could time out). V4 default off (an intended difference since M3); the v3-parity preset turns it on.")]
        public bool encountersWaitDuringArenas;
        public float bossAmbientRateMultiplier = 0.5f;
        public float eventWarningSeconds = 5f;
        [Tooltip("Stored for the profile hash; the live band length comes from the phase triggers.")] public float ambientBandDurationSeconds = 120f;
        [Tooltip("Stored for the stage profile hash only. The live band rates are the phases' ratePerSecond (register R-01).")]
        public float[] profileAmbientRatePerSecond = { 0.5f, 1.2f, 1.8f, 2.4f, 3f };
        [Tooltip("Stored for the profile hash; V3 does not read these.")] public float minChargeWarningSeconds = 1f, minExplosiveWarningSeconds = 1.2f;

        [Header("Phases")]
        public List<TimelinePhase> phases = new List<TimelinePhase>();

        [Header("Concurrency")]
        public List<TagCap> tagCaps = new List<TagCap>();
        [Tooltip("An area-denial cast (Kraken traps) frees its concurrency slot after this many seconds. 0 = never, as in V3 (register R-06: after two casts per run no Kraken lays traps again).")]
        public float areaDenialReleaseSeconds = 2f;

        [Header("Overtime and storm")]
        public OvertimeSettings overtime = new OvertimeSettings();
        public StormSettings storm = new StormSettings();

        [Header("Enemy flow (V5 M1)")]
        public FlowSettings flow = new FlowSettings();

        // V5 M3e: plain loops (called per spawner run per frame; the LINQ forms allocated a closure and an enumerator).
        public TimelinePhase Phase(string id)
        {
            if (phases == null) return null;
            for (var i = 0; i < phases.Count; i++) if (phases[i].phaseId == id) return phases[i];
            return null;
        }

        public TagCap Cap(string tag)
        {
            if (tagCaps == null) return null;
            for (var i = 0; i < tagCaps.Count; i++) if (tagCaps[i].tag == tag) return tagCaps[i];
            return null;
        }

        public override void Validate(ConfigValidationReport report)
        {
            report.Positive(this, nameof(stageDeadlineSeconds), stageDeadlineSeconds);
            report.NotNegative(this, nameof(graceSeconds), graceSeconds);
            if (maxActiveEnemies <= reservedEncounterSlots)
                report.Error(this, nameof(reservedEncounterSlots), $"reserved slots ({reservedEncounterSlots}) must be below max active enemies ({maxActiveEnemies})");
            report.Positive(this, nameof(globalMinSpawnGapSeconds), globalMinSpawnGapSeconds);
            var ids = new HashSet<string>();
            for (var i = 0; i < phases.Count; i++)
            {
                var p = phases[i];
                var at = $"phases[{i}]";
                if (string.IsNullOrWhiteSpace(p.phaseId)) report.Error(this, at + ".phaseId", "missing phase id");
                else if (!ids.Add(p.phaseId)) report.Error(this, at + ".phaseId", $"duplicate phase id '{p.phaseId}'");
                report.NotNegative(this, at + ".trigger.timeSeconds", p.trigger.timeSeconds);
                report.NotNegative(this, at + ".ratePerSecond", p.ratePerSecond);
                report.NotNegative(this, at + ".warning.leadSeconds", p.warning.leadSeconds);
                if (p.rateMultiplier <= 0f) report.Error(this, at + ".rateMultiplier", "must be greater than 0");
                if (p.burstCount > 0 && p.spawner != null && p.spawner.timing.maxActiveFromProfile > 0 && p.spawner.timing.maxActiveFromProfile < p.burstCount)
                    report.Error(this, at + ".burstCount", $"burst {p.burstCount} is larger than the spawner cap {p.spawner.timing.maxActiveFromProfile}");
                for (var j = 0; j < p.pools.Count; j++)
                    report.WeightsNotZero(this, $"{at}.pools[{j}].entries", p.pools[j].entries.Select(e => e.weight));
            }
            ValidateTriggerCycles(report);
            foreach (var cap in tagCaps) if (cap.max < 0) report.Error(this, "tagCaps", $"{cap.tag}: max must not be negative");
            report.Positive(this, "overtime.intervalFloorSeconds", overtime.intervalFloorSeconds);
            report.Positive(this, "overtime.pressureStepSeconds", overtime.pressureStepSeconds);
            if (storm.contractionEndSeconds < storm.contractionStartSeconds)
                report.Error(this, "storm.contractionEndSeconds", "contraction ends before it starts");
            foreach (var p in phases.Where(p => p.isOvertime && p.trigger.kind == PhaseTriggerKind.Time))
                if (!Mathf.Approximately(p.trigger.timeSeconds, stageDeadlineSeconds))
                    report.Error(this, "phases", $"overtime phase '{p.phaseId}' starts at {p.trigger.timeSeconds}s but the stage deadline is {stageDeadlineSeconds}s");
            if (phases.Count(p => p.isStorm) > 1) report.Error(this, "phases", "more than one storm phase");
            ValidateFlow(report);
            for (var i = 0; i < phases.Count; i++)
            {
                var p = phases[i];
                var at = $"phases[{i}]";
                if (p.ratePerSecond > 0f && p.spawner == null) report.Error(this, at + ".spawner", "an ambient phase (ratePerSecond > 0) needs a spawner");
                if (p.isOvertime && (p.spawner == null || p.alternateSpawner == null)) report.Error(this, at, "overtime needs a spawner and an alternate spawner");
                if (!p.focus.enabled) continue;
                if (p.focus.placement == FocusPlacement.SpawnerProfile && p.spawner == null) report.Error(this, at + ".spawner", "a focus spawn with SpawnerProfile placement needs a spawner");
                if (p.focus.placement == FocusPlacement.RingAroundPlayer && p.focus.archetype == null) report.Error(this, at + ".focus.archetype", "a ring focus spawn needs an archetype");
                if (p.focus.retryWindowSeconds < 0f) report.Error(this, at + ".focus.retryWindowSeconds", "must not be negative");
            }
        }

        void ValidateFlow(ConfigValidationReport report)
        {
            if (flow == null) return;
            for (var i = 0; i < flow.waves.Count; i++)
            {
                var w = flow.waves[i];
                var at = $"flow.waves[{i}]";
                report.NotNegative(this, at + ".startSeconds", w.startSeconds);
                if (w.minAliveShare < 0f || w.minAliveShare > 1f) report.Error(this, at + ".minAliveShare", "must be 0-1");
                if (w.rateMultiplier < 0f) report.Error(this, at + ".rateMultiplier", "must not be negative");
                report.Positive(this, at + ".topUpIntervalSeconds", w.topUpIntervalSeconds);
                if (w.maxTopUpPerTick < 1) report.Error(this, at + ".maxTopUpPerTick", "must be at least 1");
            }
            var ids = new HashSet<string>();
            for (var i = 0; i < flow.hordes.Count; i++)
            {
                var h = flow.hordes[i];
                var at = $"flow.hordes[{i}]";
                if (string.IsNullOrWhiteSpace(h.hordeId)) report.Error(this, at + ".hordeId", "missing horde id");
                else if (!ids.Add(h.hordeId)) report.Error(this, at + ".hordeId", $"duplicate horde id '{h.hordeId}'");
                if (h.archetype == null) report.Error(this, at + ".archetype", "a horde needs an archetype");
                if (h.count < 1) report.Error(this, at + ".count", "must be at least 1");
                report.Positive(this, at + ".distance", h.distance);
                report.NotNegative(this, at + ".timeSeconds", h.timeSeconds);
            }
            var r = flow.release;
            if (r != null)
            {
                if (r.rateMultiplier < 0f || r.floorMultiplier < 0f || r.bossFightFloorMultiplier < 0f)
                    report.Error(this, "flow.release", "multipliers must not be negative");
                report.NotNegative(this, "flow.release.afterArenaSeconds", r.afterArenaSeconds);
                report.NotNegative(this, "flow.release.afterBossKillSeconds", r.afterBossKillSeconds);
                report.NotNegative(this, "flow.release.afterHordeSeconds", r.afterHordeSeconds);
            }
        }

        /// <summary>AfterPhaseCleared triggers must not form a loop (A waits for B, B waits for A).</summary>
        void ValidateTriggerCycles(ConfigValidationReport report)
        {
            var after = phases.Where(p => p.trigger.kind == PhaseTriggerKind.AfterPhaseCleared)
                              .ToDictionary(p => p.phaseId, p => p.trigger.afterPhaseId);
            foreach (var start in after.Keys)
            {
                var seen = new HashSet<string> { start };
                var current = start;
                while (after.TryGetValue(current, out var next) && !string.IsNullOrEmpty(next))
                {
                    if (!seen.Add(next)) { report.Error(this, "phases", $"trigger cycle through '{start}'"); break; }
                    current = next;
                }
            }
        }
    }
}
