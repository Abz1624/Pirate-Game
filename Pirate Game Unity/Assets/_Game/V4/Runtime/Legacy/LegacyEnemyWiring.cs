using System;
using System.Linq;
using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Encounters;
using HighSeas.Enemies;
using HighSeas.Pickups;
using HighSeas.Player;
using HighSeas.V3;
using HighSeas.Weapons;
using HighSeas.World;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Legacy
{
    /// <summary>
    /// M8: the V4 enemy world in the run, wired to what is still V3 (models, hit feedback, the kill path's legacy
    /// targets, pickups and powerups, the static encounter tracker, the frame slots). Replaces V3's EnemyWorld,
    /// StatusController and HostileProjectileWorld (and LegacyEnemyWorldPort, which ticked them). Each piece runs where
    /// V3's ran: the frame in the shell tick (V3RunBootstrap.Update's place), hellfire patches in a slot created where
    /// V3 ensured HellfirePatchWorld, barrels in a slot on V3's barrel object, telemetry sampling where
    /// CombatTelemetryDriver was added.
    /// </summary>
    [DeletedInPhase(14, "V4 composition root owns the enemy world")]
    public sealed class LegacyEnemyWiring
    {
        readonly ConfigSetRuntime _config;
        readonly Transform _systems;

        public EnemySimulation Simulation { get; }
        public EnemyCombatServices Services { get; }
        public EnemyStatuses Statuses { get; }
        public EnemyWorldContracts Contracts { get; }
        public HellfirePatches Hellfire { get; }
        public LegacyEnemyTarget Target { get; }
        public ExplosiveBarrels Barrels { get; private set; }
        public LegacyEnemyKillTargets KillTargets { get; } = new LegacyEnemyKillTargets();
        public LegacyHellfireEnvironment HellfireEnvironment { get; } = new LegacyHellfireEnvironment();
        /// <summary>M9: pickups and floor powerups tick in the frame where V3's did.</summary>
        public LegacyBuildWiring Build { get; set; }
        /// <summary>R-40/R-49: burn ticks × this (Sea Witch's Eye, Ember's passive); 1 unless those switches are on.</summary>
        public Func<float> StatusDamage;
        /// <summary>R-40: the near-miss margin for hostile shots (0 = off).</summary>
        public Func<float> NearMissMargin;

        public LegacyEnemyWiring(ConfigSetRuntime config, EnemyWorldSettings settings, Func<PlayerShipController> player)
        {
            _config = config;
            var systems = GameObject.Find("Systems");
            _systems = systems != null ? systems.transform : null;
            Target = new LegacyEnemyTarget(player);
            var effects = new LegacyEnemyEffects();
            var spawnParams = new LegacyEnemySpawnParams();
            // V3's EnemyWorld had no enemy root, so its models sat under its own object, /Systems (EnemyViewPool.Acquire
            // switches that root off and on around each new model).
            Simulation = new EnemySimulation(HighSeas.Platform.QualityProfile.Current.enemyCap, new LegacyEnemyRunState(),
                new LegacyEnemyViewSink(_systems), spawnParams, settings);
            Statuses = new EnemyStatuses();
            var s = new EnemyCombatServices
            {
                Simulation = Simulation,
                Hazards = new HazardTelegraphs(Target, effects),
                Budget = new EncounterBudget(
                    () => StageBalanceProfileContext.Export?.Spawn?.MaxSimultaneousChargeWindups ?? 2,
                    () => StageBalanceProfileContext.Export?.Spawn?.MaxSimultaneousAreaDenial ?? 2),
                // V3's HostileProjectileWorld was added to /Systems; its shot models were children of it.
                Shots = new HostileProjectiles(new LegacyHostileShotViews(_systems)),
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

            // V3 Despawn after the slot is freed: statuses by slot, the encounter mapping (the legacy spawner's count left with it in M9).
            Simulation.Despawned += (slot, unitId, profile) =>
            {
                Statuses.ClearIndex(slot);
                RunEncounterTracker.ReleaseAgentMapping(unitId);
            };
            Statuses.Applied += type => HighSeas.Meta.TriggerBus.Instance?.RaiseStatusApplied(type);
            Simulation.SuppressHitHighlight = SuppressesHitHighlight;
            Simulation.Bind(s, Target, new LegacyEnemyFeedback(), KillTargets);
            LegacyHazardTelegraphView.Source = s.Hazards;
            LegacyHazardTelegraphView.Patches = Hellfire; // M8 playtest: a ground ring per burning patch
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

        /// <summary>The run's scene is going away: the hazard rings stop reading this world.</summary>
        public void Dispose()
        {
            if (LegacyHazardTelegraphView.Source == Services.Hazards) LegacyHazardTelegraphView.Source = null;
            if (LegacyHazardTelegraphView.Patches == Hellfire) LegacyHazardTelegraphView.Patches = null;
        }

        /// <summary>LegacyEnemyWorldPort.ResetAndDefine: the run's definitions (with their id rules) and clear transient worlds.</summary>
        public void ResetAndDefine(GameDatabase database)
        {
            Simulation.DespawnAllActive(); // V3 ResetAllAgentsForPromo (nothing alive at a run start)
            ClearTransientWorlds();
            Simulation.SetDefinitions(database.enemies, EnemySimulation.BuildRules(_config, database.enemies));
        }

        public void ClearTransientWorlds()
        {
            Build?.Pickups.ClearAll();
            Object.FindFirstObjectByType<ProjectileWorld>()?.ClearAllActive();
            Services.Shots.ClearAllActive();
        }

        /// <summary>
        /// V3RunBootstrap.Update's enemy part, same calls and order as LegacyEnemyWorldPort.Tick: boss director, enemies,
        /// player projectiles, hostile shots, pickups and floor powerups (V4 modules since M9), statuses.
        /// </summary>
        public void TickFrame(float dt, PlayerShipController player, Action<float> playerProjectiles, Action<float> bossDirector)
        {
            var playerPos = player != null ? player.Position : Vector3.zero;
            bossDirector?.Invoke(dt);
            Simulation.Tick(dt, playerPos, player?.Damage);
            // V4 player projectiles tick here since M5 (V3's ProjectileWorld, which V4 no longer fills, needed V3's EnemyWorld).
            playerProjectiles?.Invoke(dt);
            Services.Shots.NearMissMargin = NearMissMargin != null ? NearMissMargin() : 0f;
            Services.Shots.Tick(dt, player != null ? Target : null);
            Build?.TickWorld(dt, playerPos); // Magnetic Tide opens the barrels when its switch is on (R-103)
            var status = StatusDamage != null ? StatusDamage() : 1f;
            Statuses.Tick(dt, Simulation.Agents, (i, damage) => Simulation.ApplyDamage(i, damage * status, false, false), (i, burning) => Simulation.SetBurnVisual(i, burning));
        }

        /// <summary>The R-06 fix's release, where LegacyAreaDenialRelease ran (after the frame, with the run clock).</summary>
        public void TickAreaDenialRelease(float runSeconds) =>
            Services.Budget.TickAreaDenialRelease(runSeconds, _config?.Single<WaveTimelineConfig>()?.areaDenialReleaseSeconds ?? 0f);

        /// <summary>Where V3 ensured HellfirePatchWorld (a component added to /Systems): a slot ticking the V4 patches.</summary>
        public void CreateHellfireSlot()
        {
            if (_systems == null) return;
            var slot = _systems.gameObject.AddComponent<LegacyUpdateSlot>();
            slot.role = "hellfire";
            slot.Tick = Hellfire.Tick;
        }

        /// <summary>
        /// V3's barrel world stays for its markers (they bob with Unity's global random state, R-95); its update moves to
        /// a slot on the same object, which damages V4 enemies.
        /// </summary>
        public void TakeOverBarrels(V3ExplosiveBarrelWorld legacy, StageLayout layout)
        {
            if (legacy == null) return;
            legacy.enabled = false;
            Barrels = new ExplosiveBarrels(Simulation, Target, new LegacyBarrelEnvironment());
            Barrels.ResetForLayout(layout?.explosiveBarrelPositions);
            var slot = legacy.gameObject.AddComponent<LegacyUpdateSlot>();
            slot.role = "barrels";
            slot.Tick = Barrels.Tick;
        }

        /// <summary>
        /// CombatTelemetryDriver's place (added to /Systems at run start): movement and performance samples for the run
        /// telemetry. V3's driver counted the legacy enemy world's enemies; this counts the V4 world's.
        /// </summary>
        public void CreateTelemetrySlot(Func<int> projectileCount)
        {
            if (_systems == null) return;
            var slot = _systems.gameObject.AddComponent<LegacyUpdateSlot>();
            slot.role = "telemetry";
            PlayerShipController player = null;
            slot.Tick = dt =>
            {
                if (RunManager.Instance == null || RunManager.Instance.IsPaused) return;
                if (player == null) player = Object.FindFirstObjectByType<PlayerShipController>();
                if (player == null) return;
                var heading = Mathf.Repeat(player.transform.eulerAngles.y, 360f);
                RunCombatTelemetry.Instance?.TickMovement(player.Position, heading, dt);
                RunCombatTelemetry.Instance?.SamplePerformance(projectileCount != null ? projectileCount() : 0, Simulation.ActiveCount);
            };
        }
    }
}
