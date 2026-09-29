using System;
using System.Linq;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Config;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Ports
{
    /// <summary>
    /// The stage-profile context the shared V3 code still reads until M15 (RunManager.IsOvertime, ContentUnlocks' weapon
    /// and item checks, StagePoiPlacementOptions via StageBuilder's POI placement). M14: the profile is built from the V4
    /// configs alone — the V4-owned values from their configs, V3's values for the fields no V4 system owns from
    /// <see cref="StageContentConfig.sharedProfile"/> — instead of a clone of V3's asset with pass-through fields, and it is
    /// no longer validated against V3's encounter schedule (the V4 config validator checks V4's data). V4 systems read their
    /// own configs, not this profile. Replaces LegacyConfigRuntime's profile half and LegacyProfileAdapter.
    /// </summary>
    public sealed class StageProfilePort : IDisposable
    {
        /// <summary>A gold pickup's grant after the stage's gold rules and the gold-gain stat (V3 SaltwindStageEconomyLogic).</summary>
        public static int ApplyGoldGrant(int amount, float goldGain) =>
            RoguePirates.V4.Foundation.Balance.SaltwindStageEconomyLogic.ApplyGoldGrant(amount, RoguePirates.V4.Foundation.Balance.StageBalanceProfileContext.Export, goldGain);

        /// <summary>The stage profile's id, balance version and content hash (the run header), or null before it begins.</summary>
        public (string, string, string)? Identity()
        {
            var ex = RoguePirates.V4.Foundation.Balance.StageBalanceProfileContext.Export;
            return ex == null ? ((string, string, string)?)null : (ex.ProfileId, ex.BalanceVersion.ToString(), ex.ContentHash);
        }

        /// <summary>The results screen's footer (seed, time, balance version and hash of the stage profile V3 showed).</summary>
        public string ResultsFooter(int runSeed, float elapsedSeconds) =>
            RoguePirates.V4.Foundation.Balance.SaltwindRunResultsLogic.FormatProfileFooter(RoguePirates.V4.Foundation.Balance.StageBalanceProfileContext.Export, runSeed, elapsedSeconds);

        // Timeline phase ids and tag caps the V3 profile keys on (the name-keyed bridge, plan D3).
        const string EliteA = "elite_hammerhead", EliteB = "elite_kraken", SwarmA = "swarm_pulse_a", SwarmB = "swarm_pulse_b";
        const string CapRangedGunner = "ranged_gunner", CapAreaDenial = "area_denial", CapChargeWindup = "charge_windup";

        readonly ConfigSetRuntime _config;
        StageBalanceProfile _profile;

        public StageProfilePort(ConfigSetRuntime config) { _config = config; }

        /// <summary>The per-run profile (built on first use, kept current on config edits while the run is active).</summary>
        public StageBalanceProfile Profile => _profile != null ? _profile : _profile = Build();

        WaveTimelineConfig Timeline => _config?.Single<WaveTimelineConfig>();
        DifficultyCurveConfig Difficulty => _config?.Single<DifficultyCurveConfig>();
        StageContentConfig Stage => _config?.Single<StageContentConfig>();

        StageBalanceProfile Build()
        {
            var p = ScriptableObject.CreateInstance<StageBalanceProfile>();
            p.name = "V4StageProfile";
            p.hideFlags = HideFlags.DontSave;
            Fill(p);
            return p;
        }

        void Fill(StageBalanceProfile p)
        {
            var stage = Stage;
            if (stage != null)
            {
                FieldMirror.CopyFields(stage.sharedProfile, p);
                FieldMirror.CopyFields(stage.killGold, p);
            }
            if (Timeline != null && Difficulty != null) ApplyProfile(Timeline, Difficulty, p, stage);
        }

        /// <summary>Begins the context with the per-run profile (V4 runs are solo; the local-multiplayer scenes are V3's).</summary>
        public bool TryBegin(out string error)
        {
            error = null;
            StageBalanceProfileContext.EndRun();
            if (_config == null || Timeline == null || Difficulty == null) { error = "V4Game: the config set has no timeline/difficulty config."; return false; }
            StageBalanceProfileContext.BeginRun(Profile);
            _config.ConfigChanged += OnConfigChanged;
            return true;
        }

        /// <summary>A live edit: the profile is refilled and the snapshot rebuilt, replaying the chest purchase count so chest
        /// prices do not reset (readers of Authoring see the edited profile at once).</summary>
        void OnConfigChanged(ConfigChange change)
        {
            switch (_config.Get<V4ConfigAsset>(change.configId))
            {
                case WaveTimelineConfig _:
                case DifficultyCurveConfig _:
                case StageContentConfig _:
                    if (_profile == null || !StageBalanceProfileContext.IsActive || StageBalanceProfileContext.Authoring != _profile) return;
                    Fill(_profile);
                    var purchases = StageBalanceProfileContext.Runtime?.SuccessfulChestPurchases ?? 0;
                    StageBalanceProfileContext.BeginRun(_profile);
                    for (var i = 0; i < purchases; i++) StageBalanceProfileContext.Runtime.RegisterSuccessfulChestPurchase();
                    break;
            }
        }

        public void EndIfActive()
        {
            if (StageBalanceProfileContext.IsActive) AppRoot.EndActiveSaltwindRunContext();
        }

        public void Dispose()
        {
            if (_config != null) _config.ConfigChanged -= OnConfigChanged;
            if (_profile != null) { if (Application.isPlaying) Object.Destroy(_profile); else Object.DestroyImmediate(_profile); }
            _profile = null;
        }

        /// <summary>The stage profile fields StageContentConfig owns (placement counts, chest prices, landmark values).</summary>
        public static void ApplyStage(StageContentConfig s, StageBalanceProfile p)
        {
            p.beaconDirectionHintSeconds = s.beaconDirectionHintSeconds;
            p.beaconExactMarkerSeconds = s.beaconExactMarkerSeconds;
            p.repairWreckMaxHullFraction = s.repairWreckMaxHullFraction;
            p.powderCacheBonusGold = s.powderCacheBonusGold;
            p.signalBuoyOccupancySeconds = s.signalBuoyChargeSeconds;
            p.salvageChestCount = s.salvageChestCount;
            p.disableGoldTriggeredChestSpawn = s.disableGoldTriggeredChestSpawn;
            p.disableAutomaticRouteEvents = s.disableAutomaticRouteEvents;
            p.explosiveBarrelFuseSeconds = s.explosiveBarrelFuseSeconds;
            p.explosiveBarrelRadius = s.explosiveBarrelRadius;
            p.explosiveBarrelDamage = s.explosiveBarrelDamage;
            p.explosiveBarrelCount = s.explosiveBarrelCount;
            p.chestBasePrice = s.chestBasePrice;
            p.chestPriceGrowth = s.chestPriceGrowth;
            p.chestPriceIncrementBase = s.chestPriceIncrementBase;
        }

        /// <summary>The stage profile fields the timeline and difficulty configs own (and, with a stage config, StageContentConfig's).</summary>
        public static void ApplyProfile(WaveTimelineConfig t, DifficultyCurveConfig d, StageBalanceProfile p, StageContentConfig stage = null)
        {
            if (stage != null) ApplyStage(stage, p);
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
            if (t.Phase(SwarmB) is TimelinePhase sb) p.swarmStartSecondsB = sb.trigger.timeSeconds;
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
    }
}
