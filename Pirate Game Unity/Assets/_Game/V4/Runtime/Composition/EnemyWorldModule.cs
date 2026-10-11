using System;
using System.Linq;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.Enemies;
using RoguePirates.V4.Foundation.Pickups;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Foundation.World;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Spawning;
using UnityEngine;
using Object = UnityEngine.Object;
using RoguePirates.V4.Ports;

namespace RoguePirates.V4.Composition
{
    /// <summary>
    /// M8: the V4 enemy world in the run, wired to what is still V3 (models, hit feedback, the kill path's legacy
    /// targets, pickups and powerups, the static encounter tracker, the frame slots). Replaces V3's EnemyWorld,
    /// StatusController and HostileProjectileWorld (and LegacyEnemyWorldPort, which ticked them). Each piece runs where
    /// V3's ran: the frame in the shell tick (V3RunBootstrap.Update's place), hellfire patches in a slot created where
    /// V3 ensured HellfirePatchWorld, barrels in a slot on V3's barrel object, telemetry sampling where
    /// CombatTelemetryDriver was added.
    /// </summary>
    public sealed class EnemyWorldModule
    {
        readonly ConfigSetRuntime _config;
        readonly Transform _systems;
        readonly RunPort _run;

        public EnemySimulation Simulation { get; }
        public EnemyCombatServices Services { get; }
        public EnemyStatuses Statuses { get; }
        public EnemyWorldContracts Contracts { get; }
        public HellfirePatches Hellfire { get; }
        /// <summary>M14: the run's enemy combat profiles.</summary>
        public EnemyCombatProfiles CombatProfiles { get; }
        public ShipPort Target { get; }
        public ExplosiveBarrels Barrels { get; private set; }
        /// <summary>M14: the V4 kill fan-out (was LegacyEnemyKillTargets).</summary>
        public KillFanout KillTargets { get; }
        /// <summary>M14: the run's toasts and the presenter's hit feedback (were V3ToastHud and LegacyEnemyFeedback).</summary>
        public RoguePirates.V4.Views.IToastSink Toasts;
        public Func<IEnemyHitFeedback> HitFeedback;
        /// <summary>M14.9b: the blast and enemy-shot presenters the presentation root registers (were V3's
        /// SaltwindCombatEffects, ProjectileViewPool and JuiceVfxBroker splash).</summary>
        public Func<IEnemyEffects> EffectsTarget;
        public Func<IHostileShotViews> ShotViewsTarget;
        /// <summary>M14.10: the enemy view presenter (was LegacyEnemyViewSink over V3's EnemyViewPool at /Systems).</summary>
        public Func<IEnemyViewSink> ViewsTarget;
        /// <summary>M14.14: the run's frame (ticks register here instead of legacy update slots).</summary>
        public FramePipeline Frame { get; set; }
        public HellfireEnvironment HellfireEnvironment { get; }
        /// <summary>M9: pickups and floor powerups tick in the frame where V3's did.</summary>
        public BuildModule Build { get; set; }
        /// <summary>R-40/R-49: burn ticks × this (Sea Witch's Eye, Ember's passive); 1 unless those switches are on.</summary>
        public Func<float> StatusDamage;
        /// <summary>R-40: the near-miss margin for hostile shots (0 = off).</summary>
        public Func<float> NearMissMargin;

        public EnemyWorldModule(ConfigSetRuntime config, EnemyWorldSettings settings, ShipPort ship, RunPort run, EncounterTracker encounters)
        {
            _config = config;
            var systems = GameObject.Find("Systems");
            _systems = systems != null ? systems.transform : null;
            Target = ship;
            _run = run;
            KillTargets = new KillFanout
            {
                RegisterKill = run.RegisterKill,
                RunTime = () => run.ElapsedSeconds,
                KillGold = () => _config?.Single<StageContentConfig>()?.killGold,
                Random = run,
                Encounters = encounters,
            };
            HellfireEnvironment = new HellfireEnvironment(run, () => _config);
            var effects = new RoguePirates.V4.Views.EnemyEffectsForwarder(() => EffectsTarget?.Invoke());
            // M14: per-run combat profiles from the archetype/variant configs and the V4 spawn-parameter resolver (were V3's
            // static registry, overridden from config and restored afterwards, and V3's resolver).
            CombatProfiles = new EnemyCombatProfiles(config);
            var spawnParams = new EnemySpawnParamResolver(CombatProfiles, config);
            // V3's EnemyWorld had no enemy root, so its models sat under its own object, /Systems (EnemyViewPool.Acquire
            // switches that root off and on around each new model).
            Simulation = new EnemySimulation(RoguePirates.V4.Foundation.Platform.QualityProfile.Current.enemyCap, run,
                new EnemyViewForwarder(() => ViewsTarget?.Invoke()), spawnParams, settings);
            Statuses = new EnemyStatuses();
            var s = new EnemyCombatServices
            {
                Simulation = Simulation,
                Hazards = new HazardTelegraphs(Target, effects),
                // M14: the timeline's tag caps (were read back from the stage-profile export, which copied them).
                Budget = new EncounterBudget(
                    () => _config?.Single<WaveTimelineConfig>()?.Cap(TagCapIds.ChargeWindup)?.max ?? 2,
                    () => _config?.Single<WaveTimelineConfig>()?.Cap(TagCapIds.AreaDenial)?.max ?? 2),
                Shots = new HostileProjectiles(new RoguePirates.V4.Views.HostileShotViewsForwarder(() => ShotViewsTarget?.Invoke())),
                Effects = effects,
                SpawnParams = spawnParams,
                // V3 queued minions on its switched-off EncounterSpawner (counted, never spawned; no V3 data summons).
                // The spawner left the V4 scenes in M9; the queueing is kept as accepted so the minion count behaves as V3.
                Summon = (profile, count) => true,
            };
            s.Behaviors = new EnemyCombatBehaviors(s);
            s.Specialists = new EnemySpecialistCombat(s);
            s.ReefCrab = new EnemyReefCrabCombat(s);
            s.SpecialBehaviors = new EnemySpecialBehaviors(s);
            Services = s;
            Contracts = new EnemyWorldContracts(Simulation, Statuses);
            Hellfire = new HellfirePatches(Simulation, Statuses, HellfireEnvironment);
            DifficultyCurveConfig curve = null; // cached: edits change the run's copy in place
            Simulation.Curve = () => curve ??= _config?.Single<DifficultyCurveConfig>(); // M10
            Simulation.WeaponRules = () => _config?.Single<WeaponRulesConfig>(); // V5 M4: slow and knockback caps
            Simulation.LeashConfig = () => _config?.Single<RunConfig>(); // V5 (owner, 10 Oct, D28): the leash's view and margin

            // V3 Despawn after the slot is freed: statuses by slot, the encounter mapping (the legacy spawner's count left with it in M9).
            Simulation.Despawned += (slot, unitId, profile) =>
            {
                Statuses.ClearIndex(slot);
                encounters.ReleaseUnit(unitId);
            };
            Simulation.SuppressHitHighlight = SuppressesHitHighlight;
            Simulation.Bind(s, Target, new EnemyFeedbackForwarder(() => HitFeedback?.Invoke()), KillTargets); // M14 (was LegacyEnemyFeedback)
        }

        /// <summary>V3 skipped the damage highlight for Powder Kegs hits (by id); now the weapon's <c>visual.noHitHighlight</c>.</summary>
        System.Collections.Generic.HashSet<string> _noHighlight;

        bool SuppressesHitHighlight(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId) || _config == null) return false;
            if (_noHighlight == null)
            {
                _noHighlight = new System.Collections.Generic.HashSet<string>();
                foreach (var weapon in _config.All<WeaponConfig>())
                    if (weapon.visual != null && weapon.visual.noHitHighlight) _noHighlight.Add(weapon.weaponId);
            }
            return _noHighlight.Contains(weaponId);
        }

        /// <summary>Config edits (the console) can change the weapons' highlight flag.</summary>
        public void OnConfigChanged() => _noHighlight = null;

        /// <summary>The run's scene is going away.</summary>
        public void Dispose()
        {
            CombatProfiles?.Dispose();
        }

        /// <summary>LegacyEnemyWorldPort.ResetAndDefine: the run's definitions (with their id rules) and clear transient worlds.</summary>
        public void ResetAndDefine(GameDatabase database)
        {
            Simulation.DespawnAllActive(); // V3 ResetAllAgentsForPromo (nothing alive at a run start)
            Simulation.SeedRun(_run.RunSeed); // V5 M9a (I9a.2): after RunManager.BeginRun (RunBootstrapSequence)
            ClearTransientWorlds();
            Simulation.SetDefinitions(database.enemies, EnemySimulation.BuildRules(_config, database.enemies));
        }

        public void ClearTransientWorlds()
        {
            Build?.Pickups.ClearAll();
            Services.Shots.ClearAllActive();
        }

        /// <summary>
        /// V3RunBootstrap.Update's enemy part, same calls and order as LegacyEnemyWorldPort.Tick: boss director, enemies,
        /// player projectiles, hostile shots, pickups and floor powerups (V4 modules since M9), statuses.
        /// </summary>
        // V5 M3e: profiler markers for the enemy world's frame.
        static readonly Unity.Profiling.ProfilerMarker BossMarker = new Unity.Profiling.ProfilerMarker("V4.Enemies.Boss"),
            SimMarker = new Unity.Profiling.ProfilerMarker("V4.Enemies.Simulation"),
            ProjectilesMarker = new Unity.Profiling.ProfilerMarker("V4.Enemies.PlayerProjectiles"),
            ShotsMarker = new Unity.Profiling.ProfilerMarker("V4.Enemies.Shots"),
            BuildMarker = new Unity.Profiling.ProfilerMarker("V4.Enemies.BuildWorld"),
            StatusMarker = new Unity.Profiling.ProfilerMarker("V4.Enemies.Statuses");

        public void TickFrame(float dt, PlayerShipController player, Action<float> playerProjectiles, Action<float> bossDirector)
        {
            var playerPos = player != null ? player.Position : Vector3.zero;
            using (BossMarker.Auto()) bossDirector?.Invoke(dt);
            using (SimMarker.Auto()) Simulation.Tick(dt, playerPos, player?.Damage);
            // V4 player projectiles tick here since M5 (V3's ProjectileWorld, which V4 no longer fills, needed V3's EnemyWorld).
            using (ProjectilesMarker.Auto()) playerProjectiles?.Invoke(dt);
            Services.Shots.NearMissMargin = NearMissMargin != null ? NearMissMargin() : 0f;
            using (ShotsMarker.Auto()) Services.Shots.Tick(dt, player != null ? Target : null);
            using (BuildMarker.Auto()) Build?.TickWorld(dt, playerPos); // Magnetic Tide opens the barrels when its switch is on (R-103)
            _statusScale = StatusDamage != null ? StatusDamage() : 1f;
            // V5 M3e: the two callbacks are cached (two closures a frame before).
            if (_applyStatusDamage == null)
            {
                _applyStatusDamage = (i, damage) => Simulation.ApplyDamage(i, damage * _statusScale, false, false);
                _setBurnVisual = (i, burning) => Simulation.SetBurnVisual(i, burning);
            }
            using (StatusMarker.Auto()) Statuses.Tick(dt, Simulation.Agents, _applyStatusDamage, _setBurnVisual);
        }

        float _statusScale = 1f;
        Action<int, float> _applyStatusDamage;
        Action<int, bool> _setBurnVisual;

        /// <summary>The R-06 fix's release, where LegacyAreaDenialRelease ran (after the frame, with the run clock).</summary>
        public void TickAreaDenialRelease(float runSeconds) =>
            Services.Budget.TickAreaDenialRelease(runSeconds, _config?.Single<WaveTimelineConfig>()?.areaDenialReleaseSeconds ?? 0f);

        /// <summary>Where V3 ensured HellfirePatchWorld: the V4 patches in the frame's Hellfire step.</summary>
        public void CreateHellfireSlot() => Frame?.Register(FrameStep.Hellfire, Hellfire.Tick);

        /// <summary>
        /// The explosive barrels (only when the stage config places them, none does by default; the retired v3-parity preset did, V5 M8z (7 Oct, review)): V3's barrel world
        /// keeps its markers with its update off, and the V4 barrels run in the frame's Barrels step against V4 enemies.
        /// </summary>
        public void TakeOverBarrels(StageLayout layout)
        {
            Barrels = new ExplosiveBarrels(Simulation, Target, new BarrelEnvironment(_run, () => _config?.Single<StageContentConfig>(),
                (text, seconds) => Toasts?.Show(text, seconds)));
            Barrels.ResetForLayout(layout?.explosiveBarrelPositions);
            Frame?.Register(FrameStep.Barrels, Barrels.Tick);
        }

        /// <summary>M14: the run's V4 telemetry (was V3's static RunCombatTelemetry).</summary>
        public RoguePirates.V4.Telemetry.RunTelemetry Telemetry;

        /// <summary>
        /// CombatTelemetryDriver's work, in the frame's Telemetry step: movement and performance samples for the run
        /// telemetry, counting the V4 world's enemies.
        /// </summary>
        public void CreateTelemetrySlot(Func<int> projectileCount)
        {
            PlayerShipController player = null;
            Frame?.Register(FrameStep.Telemetry, dt =>
            {
                if (RunManager.Instance == null || RunManager.Instance.IsPaused) return;
                if (player == null) player = Object.FindFirstObjectByType<PlayerShipController>();
                if (player == null) return;
                var heading = Mathf.Repeat(player.SimRotation.eulerAngles.y, 360f); // V5 M9b close: the simulated pose
                Telemetry?.TickMovement(player.Position, heading, dt);
                Telemetry?.SamplePerformance(projectileCount != null ? projectileCount() : 0, Simulation.ActiveCount);
            });
        }
    }
}
