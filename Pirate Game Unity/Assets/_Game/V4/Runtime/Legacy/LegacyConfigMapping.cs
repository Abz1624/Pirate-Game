using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HighSeas.Balance;
using HighSeas.Enemies;
using RoguePirates.V4.Config;
using UnityEngine;

namespace RoguePirates.V4.Legacy
{
    /// <summary>
    /// Maps V4 config values onto the legacy V3 objects (stage profile, encounter schedule, spawn profiles) and back.
    /// The same mapping serves the migrator (V3 → config) and the runtime adapter (config → per-run V3 clones), so
    /// the two cannot drift. Legacy content ids appear here by design (plan D3): this is the name-keyed bridge.
    /// </summary>
    [DeletedInPhase(14, "Legacy profile and schedule are gone after switch-over")]
    public static class LegacyConfigMapping
    {
        // ---- Phase ids the legacy profile keys on ---------------------------------------------------------
        public const string EliteA = "elite_hammerhead", EliteB = "elite_kraken", SwarmA = "swarm_pulse_a", SwarmB = "swarm_pulse_b";
        public const string Storm = "storm", Overtime = "overtime", Boss = "boss", PowderCache = "powder_cache", ReefCrab = "reef_crab";
        public const string BandPrefix = "band_";
        public const string CapRangedGunner = "ranged_gunner", CapAreaDenial = "area_denial", CapChargeWindup = "charge_windup";

        /// <summary>StageBalanceProfile fields whose value comes from a V4 config (owner in the comment).</summary>
        public static readonly string[] OwnedProfileFields =
        {
            // WaveTimelineConfig
            "graceSeconds", "stageDeadlineSeconds", "stormStartSeconds", "stormWarningLeadSeconds",
            "ambientRatePerSecond", "ambientBandDurationSeconds", "maxActiveEnemies", "reservedEncounterSlots",
            "bossAmbientRateMultiplier", "scheduledEliteSecondsA", "scheduledEliteSecondsB", "swarmStartSecondsA",
            "swarmStartSecondsB", "swarmDurationSeconds", "eventWarningSeconds", "maxSimultaneousCannonboats",
            "maxSimultaneousAreaDenial", "maxSimultaneousChargeWindups", "minChargeWarningSeconds",
            "minExplosiveWarningSeconds", "spawnRetryIntervalSeconds", "requiredEncounterTimeoutSeconds",
            "swarmIntensityMultiplier",
            "overtimeInitialSpawnIntervalSeconds", "overtimeIntervalFloorSeconds", "overtimeIntervalDecayPerStep",
            "overtimePressureStepSeconds", "pursuerWeightFirstTwoMinutes", "harrierWeightFirstTwoMinutes",
            "pursuerWeightAfterTwoMinutes", "harrierWeightAfterTwoMinutes", "stormSafeRegionStartFraction",
            "stormSafeRegionEndFraction", "stormContractionStartSeconds", "stormContractionEndSeconds",
            "stormOutsideDamagePerSecond",
            // DifficultyCurveConfig
            "overtimeHpMultiplierBase", "overtimeHpMultiplierExponentPerMinute", "overtimeDamageBonusPerMinute",
            // Set by the adapter to the per-run schedule clone
            "encounterSchedule",
        };

        /// <summary>StageBalanceProfile fields copied unchanged from the V3 asset until the phase that owns them.</summary>
        public static readonly Dictionary<string, int> PassThroughProfileFields = new Dictionary<string, int>
        {
            ["profileId"] = 14, ["schemaVersion"] = 14, ["balanceVersion"] = 14, ["enforceCompleteScheduleReferences"] = 14,
            ["beaconDirectionHintSeconds"] = 7, ["beaconExactMarkerSeconds"] = 7,
            ["ordinaryEnemyGoldChance"] = 12, ["ordinaryEnemyGoldAmount"] = 12, ["specialistEnemyGoldAmount"] = 12, ["scheduledEliteGoldAmount"] = 12,
            ["randomPowerupChancePerKill"] = 9, ["randomPowerupMinIntervalSeconds"] = 9, ["repairSuppliesMaxHullFraction"] = 9,
            ["powderRushAttackSpeedBonus"] = 9, ["powderRushDurationSeconds"] = 9, ["deadCalmSlowFraction"] = 9, ["deadCalmDurationSeconds"] = 9,
            ["repairWreckMaxHullFraction"] = 7, ["powderCacheBonusGold"] = 7, ["signalBuoyOccupancySeconds"] = 7,
            ["salvageChestCount"] = 7, ["disableGoldTriggeredChestSpawn"] = 7, ["disableAutomaticRouteEvents"] = 7,
            ["explosiveBarrelFuseSeconds"] = 7, ["explosiveBarrelRadius"] = 7, ["explosiveBarrelDamage"] = 7, ["explosiveBarrelCount"] = 7,
            ["weaponSlotCap"] = 9, ["logbookSlotCap"] = 9, ["levelUpChoiceCount"] = 9, ["levelZeroToOneXpCost"] = 9,
            ["xpCurveMultiplier"] = 9, ["xpCurveExponent"] = 9, ["newWeaponOfferProtectionLevels"] = 9,
            ["chestBasePrice"] = 7, ["chestPriceGrowth"] = 7, ["chestPriceIncrementBase"] = 7,
            ["marksPerQualifyingMinute"] = 12, ["marksPerQualifyingMinuteMinKills"] = 12, ["marksPerSpecialPoi"] = 12,
            ["marksPerScheduledElite"] = 12, ["marksBossVictory"] = 12, ["marksPerOvertimeMinute"] = 12, ["marksOvertimeCap"] = 12,
            ["runGoldExpiresAtRunEnd"] = 12, ["bankRunGoldToDoubloons"] = 12,
            ["starterWeaponIds"] = 9, ["weaponUnlocks"] = 12, ["captainUnlocks"] = 12,
        };

        // ---- Profile ------------------------------------------------------------------------------------

        /// <summary>Writes the V4-owned fields into a (runtime copy of the) V3 stage profile.</summary>
        public static void ApplyProfile(WaveTimelineConfig t, DifficultyCurveConfig d, StageBalanceProfile p)
        {
            p.graceSeconds = t.graceSeconds;
            p.stageDeadlineSeconds = t.stageDeadlineSeconds;
            var storm = t.phases.FirstOrDefault(x => x.isStorm);
            if (storm != null)
            {
                p.stormStartSeconds = storm.trigger.timeSeconds;
                p.stormWarningLeadSeconds = storm.warning.leadSeconds;
            }
            p.ambientRatePerSecond = (float[])t.profileAmbientRatePerSecond.Clone();
            p.ambientBandDurationSeconds = t.ambientBandDurationSeconds;
            p.maxActiveEnemies = t.maxActiveEnemies;
            p.reservedEncounterSlots = t.reservedEncounterSlots;
            p.bossAmbientRateMultiplier = t.bossAmbientRateMultiplier;
            p.eventWarningSeconds = t.eventWarningSeconds;
            p.minChargeWarningSeconds = t.minChargeWarningSeconds;
            p.minExplosiveWarningSeconds = t.minExplosiveWarningSeconds;
            p.spawnRetryIntervalSeconds = t.spawnRetryIntervalSeconds;
            p.requiredEncounterTimeoutSeconds = t.requiredEncounterTimeoutSeconds;
            if (t.Phase(EliteA) is TimelinePhase ea) p.scheduledEliteSecondsA = ea.trigger.timeSeconds;
            if (t.Phase(EliteB) is TimelinePhase eb) p.scheduledEliteSecondsB = eb.trigger.timeSeconds;
            if (t.Phase(SwarmA) is TimelinePhase sa)
            {
                p.swarmStartSecondsA = sa.trigger.timeSeconds;
                p.swarmDurationSeconds = sa.duration.seconds; // V3 has one duration and one intensity for both pulses
                p.swarmIntensityMultiplier = sa.rateMultiplier;
            }
            if (t.Phase(SwarmB) is TimelinePhase sb)
            {
                p.swarmStartSecondsB = sb.trigger.timeSeconds;
                if (t.Phase(SwarmA) is TimelinePhase a && (!Mathf.Approximately(a.duration.seconds, sb.duration.seconds) || !Mathf.Approximately(a.rateMultiplier, sb.rateMultiplier)))
                    Debug.LogWarning("V4 config: V3 uses one swarm duration and intensity for both pulses, so the second pulse's own values are ignored until M3.");
            }
            if (t.Cap(CapRangedGunner) is TagCap c1) p.maxSimultaneousCannonboats = c1.max;
            if (t.Cap(CapAreaDenial) is TagCap c2) p.maxSimultaneousAreaDenial = c2.max;
            if (t.Cap(CapChargeWindup) is TagCap c3) p.maxSimultaneousChargeWindups = c3.max;

            var o = t.overtime;
            p.overtimeInitialSpawnIntervalSeconds = o.initialSpawnIntervalSeconds;
            p.overtimeIntervalFloorSeconds = o.intervalFloorSeconds;
            p.overtimeIntervalDecayPerStep = o.intervalDecayPerStep;
            p.overtimePressureStepSeconds = o.pressureStepSeconds;
            p.pursuerWeightFirstTwoMinutes = o.pursuerWeightFirstTwoMinutes;
            p.harrierWeightFirstTwoMinutes = o.harrierWeightFirstTwoMinutes;
            p.pursuerWeightAfterTwoMinutes = o.pursuerWeightAfterTwoMinutes;
            p.harrierWeightAfterTwoMinutes = o.harrierWeightAfterTwoMinutes;
            var s = t.storm;
            p.stormSafeRegionStartFraction = s.safeRegionStartFraction;
            p.stormSafeRegionEndFraction = s.safeRegionEndFraction;
            p.stormContractionStartSeconds = s.contractionStartSeconds;
            p.stormContractionEndSeconds = s.contractionEndSeconds;
            p.stormOutsideDamagePerSecond = s.outsideDamagePerSecond;

            p.overtimeHpMultiplierBase = d.overtimeHpMultiplierBase;
            p.overtimeHpMultiplierExponentPerMinute = d.overtimeHpMultiplierExponentPerMinute;
            p.overtimeDamageBonusPerMinute = d.overtimeDamageBonusPerMinute;
        }

        /// <summary>Migrator: reads the V4-owned fields out of the V3 profile (phases must already exist).</summary>
        public static void ReadProfile(StageBalanceProfile p, WaveTimelineConfig t, DifficultyCurveConfig d)
        {
            t.graceSeconds = p.graceSeconds;
            t.stageDeadlineSeconds = p.stageDeadlineSeconds;
            foreach (var overtime in t.phases.Where(x => x.isOvertime)) overtime.trigger.timeSeconds = p.stageDeadlineSeconds;
            var storm = t.phases.FirstOrDefault(x => x.isStorm);
            if (storm != null) { storm.trigger.timeSeconds = p.stormStartSeconds; storm.warning.leadSeconds = p.stormWarningLeadSeconds; }
            t.profileAmbientRatePerSecond = (float[])p.ambientRatePerSecond.Clone();
            t.ambientBandDurationSeconds = p.ambientBandDurationSeconds;
            t.maxActiveEnemies = p.maxActiveEnemies;
            t.reservedEncounterSlots = p.reservedEncounterSlots;
            t.bossAmbientRateMultiplier = p.bossAmbientRateMultiplier;
            t.eventWarningSeconds = p.eventWarningSeconds;
            t.minChargeWarningSeconds = p.minChargeWarningSeconds;
            t.minExplosiveWarningSeconds = p.minExplosiveWarningSeconds;
            t.spawnRetryIntervalSeconds = p.spawnRetryIntervalSeconds;
            t.requiredEncounterTimeoutSeconds = p.requiredEncounterTimeoutSeconds;
            if (t.Phase(EliteA) is TimelinePhase ea) { ea.trigger.timeSeconds = p.scheduledEliteSecondsA; ea.warning.leadSeconds = p.eventWarningSeconds; }
            if (t.Phase(EliteB) is TimelinePhase eb) { eb.trigger.timeSeconds = p.scheduledEliteSecondsB; eb.warning.leadSeconds = p.eventWarningSeconds; }
            foreach (var (id, start) in new[] { (SwarmA, p.swarmStartSecondsA), (SwarmB, p.swarmStartSecondsB) })
            {
                if (!(t.Phase(id) is TimelinePhase swarm)) continue;
                swarm.trigger.timeSeconds = start;
                swarm.duration.kind = PhaseDurationKind.Seconds;
                swarm.duration.seconds = p.swarmDurationSeconds;
                swarm.rateMultiplier = p.swarmIntensityMultiplier;
                swarm.warning.leadSeconds = p.eventWarningSeconds;
            }
            SetCap(t, CapRangedGunner, p.maxSimultaneousCannonboats, TagCapScope.Any);
            SetCap(t, CapAreaDenial, p.maxSimultaneousAreaDenial, TagCapScope.SpecialistPool);
            SetCap(t, CapChargeWindup, p.maxSimultaneousChargeWindups, TagCapScope.SpecialistPool);

            var o = t.overtime;
            o.initialSpawnIntervalSeconds = p.overtimeInitialSpawnIntervalSeconds;
            o.intervalFloorSeconds = p.overtimeIntervalFloorSeconds;
            o.intervalDecayPerStep = p.overtimeIntervalDecayPerStep;
            o.pressureStepSeconds = p.overtimePressureStepSeconds;
            o.pursuerWeightFirstTwoMinutes = p.pursuerWeightFirstTwoMinutes;
            o.harrierWeightFirstTwoMinutes = p.harrierWeightFirstTwoMinutes;
            o.pursuerWeightAfterTwoMinutes = p.pursuerWeightAfterTwoMinutes;
            o.harrierWeightAfterTwoMinutes = p.harrierWeightAfterTwoMinutes;
            var s = t.storm;
            s.safeRegionStartFraction = p.stormSafeRegionStartFraction;
            s.safeRegionEndFraction = p.stormSafeRegionEndFraction;
            s.contractionStartSeconds = p.stormContractionStartSeconds;
            s.contractionEndSeconds = p.stormContractionEndSeconds;
            s.outsideDamagePerSecond = p.stormOutsideDamagePerSecond;

            d.overtimeHpMultiplierBase = p.overtimeHpMultiplierBase;
            d.overtimeHpMultiplierExponentPerMinute = p.overtimeHpMultiplierExponentPerMinute;
            d.overtimeDamageBonusPerMinute = p.overtimeDamageBonusPerMinute;
        }

        static void SetCap(WaveTimelineConfig t, string tag, int max, TagCapScope scope)
        {
            var cap = t.Cap(tag);
            if (cap == null) t.tagCaps.Add(cap = new TagCap { tag = tag });
            cap.max = max;
            cap.scope = scope;
        }

        /// <summary>Concurrency tags for the enemies V3's composition caps check by id (SaltwindAmbientCompositionLogic L128-147).</summary>
        public static readonly Dictionary<string, string[]> LegacyConcurrencyTags = new Dictionary<string, string[]>
        {
            ["cannonboat"] = new[] { CapRangedGunner },
            ["kraken_spawn"] = new[] { CapAreaDenial },
            ["ram_raider"] = new[] { CapChargeWindup }, // specialist alias; gets an archetype (variant) in M2
        };

        /// <summary>Template of each V3 specialist alias (CombatSpecialistEnemyCatalog.ApplyToDatabase L17-24).</summary>
        public static readonly Dictionary<string, string> SpecialistTemplates = new Dictionary<string, string>
        {
            ["needle_sloop"] = "cutthroat_skiff", ["ram_raider"] = "rammer_raft", ["bomb_ketch"] = "bomb_boat",
            ["net_cutter"] = "gunner_cog", ["repair_tender"] = "boarding_barge", ["powder_barge"] = "boarding_barge",
            ["shield_frigate"] = "gunner_cog", ["treasure_raider"] = "deckhand_dinghy",
        };

        /// <summary>V3 registry elite profiles and the enemy they belong to (EnemyCombatProfileRegistry L177-204).</summary>
        public static readonly (string eliteId, string baseId)[] RegistryElites = { ("elite_hammerhead", "hammerhead"), ("elite_kraken_spawn", "kraken_spawn") };

        /// <summary>Enemies only V3's fallback database defines (register R-05).</summary>
        public static readonly string[] FallbackOnlyEnemies = { "drowned_pursuer", "drowned_harrier" };

        // ---- Encounter schedule -------------------------------------------------------------------------

        /// <summary>
        /// Writes the timeline's stage limits, band windows and band rates into the per-run schedule clone whose
        /// profile references are already per-run clones (<paramref name="profileFor"/> maps a spawner to its clone).
        /// </summary>
        /// <remarks>
        /// <paramref name="atRunStart"/> = true also writes the band start times and the global spawn gap. V3 caches
        /// those at the spawner reset (entry start, the global gap) while it reads each band's end live from the next
        /// entry, so changing them mid-run would make bands overlap or leave gaps. Live edits therefore skip them; they
        /// take effect when a run starts with them (restart with a preset).
        /// </remarks>
        public static void ApplySchedule(WaveTimelineConfig t, EncounterSpawnSchedule schedule, Func<EnemySpawnerConfig, SpawnProfile> profileFor, bool atRunStart)
        {
            schedule.gracePeriodSeconds = t.graceSeconds;
            schedule.maxActiveEnemies = t.maxActiveEnemies;
            if (atRunStart) schedule.globalMinSpawnGapSeconds = t.globalMinSpawnGapSeconds;
            foreach (var phase in t.phases)
            {
                if (phase.spawner == null || !phase.phaseId.StartsWith(BandPrefix)) continue;
                var profile = profileFor(phase.spawner);
                if (profile == null) continue;
                if (atRunStart)
                    foreach (var entry in schedule.entries ?? Array.Empty<ScheduledSpawnEntry>())
                        if (entry.profile == profile) entry.startTimeSeconds = phase.trigger.timeSeconds;
                if (phase.ratePerSecond > 0f) profile.timing.spawnIntervalSeconds = 1f / phase.ratePerSecond;
            }
        }

        // ---- Spawn profiles -----------------------------------------------------------------------------

        public static void ApplySpawner(EnemySpawnerConfig c, SpawnProfile p)
        {
            p.spawnMode = (SpawnMode)(int)c.spawnMode;
            p.groupLayout = (SpawnGroupLayout)(int)c.groupLayout;
            CopyFields(c.timing, p.timing);
            CopyFields(c.placement, p.placement);
            CopyFields(c.elite, p.elite);
            CopyFields(c.movement, p.movement);
            CopyFields(c.attack, p.attack);
        }

        public static void ReadSpawner(SpawnProfile p, EnemySpawnerConfig c)
        {
            c.legacyProfileId = p.ResolvedId;
            c.spawnMode = (ConfigEnums.SpawnMode)(int)p.spawnMode;
            c.groupLayout = (ConfigEnums.SpawnGroupLayout)(int)p.groupLayout;
            c.specialBehavior = (ConfigEnums.EnemySpecialBehavior)(int)p.specialBehavior;
            CopyFields(p.timing, c.timing);
            CopyFields(p.placement, c.placement);
            CopyFields(p.elite, c.elite);
            CopyFields(p.movement, c.movement);
            CopyFields(p.attack, c.attack);
        }

        /// <summary>
        /// Copies public instance fields with the same name (enums by value, arrays element-wise). Returns the
        /// destination fields that had no source, so tests can prove the mirrors are complete.
        /// </summary>
        public static List<string> CopyFields(object source, object destination)
        {
            var missing = new List<string>();
            var src = source.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public).ToDictionary(f => f.Name);
            foreach (var d in destination.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!src.TryGetValue(d.Name, out var s)) { missing.Add(d.Name); continue; }
                d.SetValue(destination, Convert(s.GetValue(source), d.FieldType));
            }
            return missing;
        }

        static object Convert(object value, Type target)
        {
            if (value == null) return null;
            if (target.IsEnum) return Enum.ToObject(target, System.Convert.ToInt32(value));
            if (target.IsArray && value is Array array)
            {
                var element = target.GetElementType();
                var result = Array.CreateInstance(element, array.Length);
                for (var i = 0; i < array.Length; i++) result.SetValue(Convert(array.GetValue(i), element), i);
                return result;
            }
            return value;
        }
    }
}
