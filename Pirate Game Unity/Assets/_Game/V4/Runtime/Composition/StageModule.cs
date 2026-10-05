using System;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Foundation.World;
using RoguePirates.V4.Config;
using RoguePirates.V4.Events;
using RoguePirates.V4.Spawning;
using RoguePirates.V4.Stage;
using RoguePirates.V4.Weapons;
using UnityEngine;
using Object = UnityEngine.Object;
using RoguePirates.V4.Ports;

namespace RoguePirates.V4.Composition
{
    /// <summary>
    /// The stage content (M7; M14.14 a V4 module, was LegacyStageWiring): chests, landmarks, Beacon, discovery,
    /// interaction and the Barnacle King director, with their ticks in the frame pipeline (the Stage step runs the per-POI
    /// ticks, then the stage manager; Discovery and Interaction steps) and the boss director first in the run's enemy
    /// tick. The markers are the marker presenter's (M14.12), drawn from the module's marker state.
    /// </summary>
    public sealed class StageModule : IDisposable
    {
        /// <summary>M12: the run adapter the stage module ends runs through (Rum is set on it).</summary>
        public RunPort StageRun { get; private set; }

        /// <summary>M14.14: the run's frame (ticks register here instead of legacy update slots).</summary>
        public FramePipeline Frame { get; set; }

        readonly bool _usePlaceholderVisuals;
        readonly CombatEvents _events;
        readonly RunClock _clock;
        readonly Func<StageContentConfig> _config;
        bool _active = true;

        public StageContentModule Module { get; }
        public InteractionModel Interaction { get; }
        public StageView View { get; }
        public BossDirector Boss { get; }
        /// <summary>M14: the run's V4 encounter tracker (was the static RunEncounterTracker).</summary>
        public EncounterTracker Encounters { get; }

        readonly EnemyWorldModule _enemyWorld;

        /// <summary>Chest prices, loot and items (M9: items go to the V4 item module; M14: the V4 chest economy).</summary>
        public ChestEconomy Economy { get; }
        /// <summary>Telemetry, the home seed and the chest trigger (M14: telemetry is the V4 module's).</summary>
        public StageRewards Rewards { get; } = new StageRewards { LockSeed = WorldPort.LockHomeSeed }; // M14.12

        public StageModule(RunContext context, ConfigSetRuntime config, RunPort runStatus, ShipPort ship, IPlayerDamageSink damageSink,
            IEncounterExecutor executor, Func<PlayerShipController> player, Func<IWeaponManager> weapons, bool usePlaceholderVisuals,
            EnemyWorldModule enemyWorld, EncounterTracker encounters, IStageToasts toasts)
        {
            Encounters = encounters;
            _enemyWorld = enemyWorld;
            _usePlaceholderVisuals = usePlaceholderVisuals;
            _events = context.Events;
            _clock = context.Clock;
            StageContentConfig cached = null;
            _config = () => cached != null ? cached : (cached = config?.Single<StageContentConfig>());
            var run = runStatus;
            StageRun = run;
            Economy = new ChestEconomy(() => config, run, run, () => RoguePirates.V4.Foundation.Core.AppRoot.Instance != null ? RoguePirates.V4.Foundation.Core.AppRoot.Instance.Save : null, weapons);
            Module = new StageContentModule(_config, run, ship, Economy,
                Rewards, toasts, Encounters, executor)
            {
                Publish = PublishEvent
            };
            Module.IsFirstConquest = () => !ContentPort.FirstConquestDone; // playtest round 1: banner
            Interaction = new InteractionModel(Module, run, toasts);
            View = new StageView(Module, Interaction);

            // M8: the boss and its shots are the V4 enemy world's (were LegacyBossControl / LegacyHostileProjectiles).
            Boss = new BossDirector(enemyWorld.Contracts, enemyWorld.Services.Shots, damageSink, new BossEffects(enemyWorld.Services.Effects, toasts),
                runStatus, () => config?.Single<BossConfig>(), () => Encounters.BossObjectiveSecured, () => true); // M14: V4 runs always have a stage

            context.Register<IEncounterTracker>(Encounters);
            context.Register<IStageContent>(View);
            context.Register<IInteractionPromptView>(Interaction);
            context.Register<IInteractionTouch>(Interaction);
            context.Register<IBossView>(Boss);
            context.Register<IPoiMarkerView>(Module); // M14.12: the marker presenter
        }

        void PublishEvent(CombatEventKind kind, int a, int b, Vector3 position, string tag) =>
            _events?.Publish(new CombatEvent(kind, _clock != null ? _clock.TickIndex : 0, _clock != null ? _clock.SimTime : 0f, a, b,
                position.x, position.y, position.z, tag));

        /// <summary>
        /// Where V3 initialized POIManager: markers, then V3's barrels (only when the stage config places them).
        /// </summary>
        /// <summary>Playtest (3 Oct, owner): a 1v1 match: no chests, landmarks, Beacon, barrels or boss on the stage.</summary>
        public bool PvpMode { get; set; }

        public void InitializePoi(StageLayout layout)
        {
            if (PvpMode)
            {
                _active = false;
                layout?.pois?.Clear(); // the discovery and the minimap share the layout: they see none either
                Module.Initialize(layout, enabled: false, placeholderVisuals: _usePlaceholderVisuals);
                return;
            }
            _active = true;
            // Playtest (4-5 Oct, owner): no points of interest in a voyage except the salvage chests (the only item source):
            // no landmarks, Beacon or Powder Cache. The module stays on: the boss still rises on its own timer.
            layout?.pois?.RemoveAll(p => p.type != RoguePirates.V4.Foundation.Core.POIType.SalvageChest);
            // M14.12: the module keeps the marker state; the marker presenter draws it (was LegacyPoiMarkers, which also
            // cleared /Systems' children like V3's POIManager, R-98 — nothing V4 keeps there needs clearing).
            Module.Initialize(layout, enabled: true, placeholderVisuals: _usePlaceholderVisuals);
            if (layout == null) return;
            // V3 always added the barrel world; V4 places none by default (adopted decision), so it only adds it when
            // the config asks for barrels (the v3-parity preset does).
            var c = _config();
            if (c == null || c.explosiveBarrelCount > 0)
            {
                _enemyWorld.TakeOverBarrels(layout);
            }
        }

        /// <summary>V3DiscoverySystem.Ensure's place: its update object, created here, and the reset around the spawn.</summary>
        public void EnsureDiscovery(StageLayout layout)
        {
            Module.Discovery.Initialize(layout);
            var run = StageRun;
            Frame?.Register(FrameStep.Discovery, dt => Module.Discovery.Tick(!run.Exists || run.IsPaused, run.ElapsedSeconds));
        }

        /// <summary>V3InteractionController.Ensure's place: its update object under the run canvas (the prompt panel is a presenter).</summary>
        public void EnsureInteraction()
        {
            Frame?.Register(FrameStep.Interaction, Interaction.Tick);
        }

        /// <summary>The scene slot in POIManager's place (V4SceneSetup.ApplyM7).</summary>
        public void BindManagerSlot()
        {
            Frame?.Register(FrameStep.Stage, TickStage);
        }

        /// <summary>M14.12: the per-POI ticks (were each marker's slot), then POIManager.Update's.</summary>
        void TickStage(float dt)
        {
            Module.TickPois(dt);
            Module.TickManager(dt);
        }

        /// <summary>V3 BarnacleKingBossAttackDirector.Ensure(...).ResetForRun().</summary>
        public void ResetBoss()
        {
            if (_active) Boss.ResetForRun();
        }

        /// <summary>V3RunBootstrap.Update ticked the director first, before the enemy world.</summary>
        public void TickBoss(float dt)
        {
            if (_active) Boss.Tick(dt);
        }

        public void TrySecureIfBossDead() => Module.TrySecureIfBossDead();

        /// <summary>
        /// Module Lab: chests, landmarks, the Beacon and their prompts work as in a run (M8 playtest: they were off, as V3
        /// switched POIManager and the controller off in its test scene, so the lab's Beacon and chests did nothing).
        /// Nothing spawns by itself: the Powder Cache ambush and the boss come only from what you do. A boss victory in
        /// the lab is not saved (no home-seed lock or boss-victory Marks).
        /// </summary>
        public void EnterModuleLab() => Module.LabMode = true;

        public void Dispose() { }
    }
}
