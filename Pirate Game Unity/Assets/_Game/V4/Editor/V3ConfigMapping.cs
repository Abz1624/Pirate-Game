using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HighSeas.Balance;
using HighSeas.Enemies;
using RoguePirates.V4.Config;
using UnityEngine;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// The V3 → config mapping the migrator uses (M14: moved from Runtime/Legacy with the runtime clones it also served;
    /// the runtime builds its spawn profiles in SpawnerProfiles and the shared stage profile in LegacyStageProfilePort, and
    /// the config → V3 methods here forward to those, so the directions cannot drift). Legacy content ids appear here by
    /// design (plan D3): this is the name-keyed bridge.
    /// </summary>
    public static class V3ConfigMapping
    {
        // ---- Phase ids the legacy profile keys on ---------------------------------------------------------
        public const string EliteA = "elite_hammerhead", EliteB = "elite_kraken", SwarmA = "swarm_pulse_a", SwarmB = "swarm_pulse_b";
        public const string Storm = "storm", Overtime = "overtime", Boss = "boss", PowderCache = "powder_cache", ReefCrab = "reef_crab";
        public const string BandPrefix = PhaseIds.BandPrefix;
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
            // StageContentConfig (M7)
            "beaconDirectionHintSeconds", "beaconExactMarkerSeconds", "repairWreckMaxHullFraction", "powderCacheBonusGold",
            "signalBuoyOccupancySeconds", "salvageChestCount", "disableGoldTriggeredChestSpawn", "disableAutomaticRouteEvents",
            "explosiveBarrelFuseSeconds", "explosiveBarrelRadius", "explosiveBarrelDamage", "explosiveBarrelCount",
            "chestBasePrice", "chestPriceGrowth", "chestPriceIncrementBase",
            // Set by the adapter to the per-run schedule clone
            "encounterSchedule",
        };

        /// <summary>StageBalanceProfile fields copied unchanged from the V3 asset until the phase that owns them.</summary>
        public static readonly Dictionary<string, int> PassThroughProfileFields = new Dictionary<string, int>
        {
            ["profileId"] = 14, ["schemaVersion"] = 14, ["balanceVersion"] = 14, ["enforceCompleteScheduleReferences"] = 14,
            ["ordinaryEnemyGoldChance"] = 12, ["ordinaryEnemyGoldAmount"] = 12, ["specialistEnemyGoldAmount"] = 12, ["scheduledEliteGoldAmount"] = 12,
            ["randomPowerupChancePerKill"] = 9, ["randomPowerupMinIntervalSeconds"] = 9, ["repairSuppliesMaxHullFraction"] = 9,
            ["powderRushAttackSpeedBonus"] = 9, ["powderRushDurationSeconds"] = 9, ["deadCalmSlowFraction"] = 9, ["deadCalmDurationSeconds"] = 9,
            ["weaponSlotCap"] = 9, ["logbookSlotCap"] = 9, ["levelUpChoiceCount"] = 9, ["levelZeroToOneXpCost"] = 9,
            ["xpCurveMultiplier"] = 9, ["xpCurveExponent"] = 9, ["newWeaponOfferProtectionLevels"] = 9,
            ["marksPerQualifyingMinute"] = 12, ["marksPerQualifyingMinuteMinKills"] = 12, ["marksPerSpecialPoi"] = 12,
            ["marksPerScheduledElite"] = 12, ["marksBossVictory"] = 12, ["marksPerOvertimeMinute"] = 12, ["marksOvertimeCap"] = 12,
            ["runGoldExpiresAtRunEnd"] = 12, ["bankRunGoldToDoubloons"] = 12,
            ["starterWeaponIds"] = 9, ["weaponUnlocks"] = 12, ["captainUnlocks"] = 12,
        };

        // ---- Profile ------------------------------------------------------------------------------------

        /// <summary>The stage profile fields StageContentConfig owns (the runtime port's mapping).</summary>
        public static void ApplyStage(StageContentConfig s, StageBalanceProfile p) => RoguePirates.V4.Legacy.LegacyStageProfilePort.ApplyStage(s, p);

        /// <summary>The stage profile fields the V4 configs own (the runtime port's mapping).</summary>
        public static void ApplyProfile(WaveTimelineConfig t, DifficultyCurveConfig d, StageBalanceProfile p, StageContentConfig stage = null) =>
            RoguePirates.V4.Legacy.LegacyStageProfilePort.ApplyProfile(t, d, p, stage);

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

        /// <summary>A spawner config onto a spawn profile (the runtime SpawnerProfiles mapping).</summary>
        public static void ApplySpawner(EnemySpawnerConfig c, SpawnProfile p) => RoguePirates.V4.Spawning.SpawnerProfiles.Apply(c, p);

        /// <summary>M14: reads the special-behaviour modules from a V3 profile (the migrator's half).</summary>
        public static void ReadModules(SpawnProfile p, EnemySpawnerConfig c)
        {
            CopyFields(p.formation, c.formation);
            CopyFields(p.summon, c.summon);
            CopyFields(p.tether, c.tether);
            CopyFields(p.supportAura, c.supportAura);
            CopyFields(p.phaseDash, c.phaseDash);
            CopyFields(p.terrainAmbush, c.terrainAmbush);
            CopyFields(p.adaptive, c.adaptive);
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
            ReadModules(p, c);
        }

        public static List<string> CopyFields(object source, object destination) => FieldMirror.CopyFields(source, destination);
    }
}
