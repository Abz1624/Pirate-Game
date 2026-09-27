using System;
using HighSeas.Balance;
using HighSeas.Player;
using HighSeas.V3;
using HighSeas.World;
using RoguePirates.V4.Config;
using RoguePirates.V4.Events;
using RoguePirates.V4.Spawning;
using RoguePirates.V4.Stage;
using RoguePirates.V4.Weapons;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Legacy
{
    /// <summary>
    /// Builds the M7 stage content (chests, landmarks, Beacon, discovery, interaction, Barnacle King director) over the
    /// legacy adapters and puts each tick where V3's piece ticked: the stage manager in POIManager's scene slot (file ID
    /// taken by V4SceneSetup.ApplyM7), discovery and interaction on objects created where V3DiscoverySystem and
    /// V3InteractionController were created, one tick per marker (POIInteractable's place), the boss director first in
    /// the legacy world tick (V3RunBootstrap.Update).
    /// </summary>
    [DeletedInPhase(14, "V4 composition root; its frame slots go with the legacy frame order")]
    public sealed class LegacyStageWiring : IDisposable
    {
        public const string StageRole = "stage";

        readonly bool _usePlaceholderVisuals;
        readonly LegacyBossKillHook _killHook = new LegacyBossKillHook();
        readonly CombatEvents _events;
        readonly RunClock _clock;
        readonly Func<StageContentConfig> _config;
        bool _active = true;

        public StageContentModule Module { get; }
        public InteractionModel Interaction { get; }
        public StageView View { get; }
        public BossDirector Boss { get; }
        public LegacyEncounterTracker Encounters { get; } = new LegacyEncounterTracker();

        readonly LegacyEnemyWiring _enemyWorld;

        public LegacyStageWiring(RunContext context, ConfigSetRuntime config, IRunStatus runStatus, IPlayerDamageSink damageSink,
            IEncounterExecutor executor, Func<PlayerShipController> player, Func<IWeaponManager> weapons, bool usePlaceholderVisuals,
            LegacyEnemyWiring enemyWorld)
        {
            _enemyWorld = enemyWorld;
            _usePlaceholderVisuals = usePlaceholderVisuals;
            _events = context.Events;
            _clock = context.Clock;
            StageContentConfig cached = null;
            _config = () => cached != null ? cached : (cached = config?.Single<StageContentConfig>());
            var run = new LegacyStageRun();
            var toasts = new LegacyStageToasts();
            Module = new StageContentModule(_config, run, new LegacyStageShip(player), new LegacyStageEconomy(weapons),
                new LegacyStageRewards(), toasts, Encounters, executor)
            {
                Publish = PublishEvent
            };
            Interaction = new InteractionModel(Module, run, toasts);
            View = new StageView(Module, Interaction);

            var objective = new LegacyBossObjective();
            // M8: the boss and its shots are the V4 enemy world's (were LegacyBossControl / LegacyHostileProjectiles).
            Boss = new BossDirector(enemyWorld.Contracts, enemyWorld.Services.Shots, damageSink, new LegacyBossEffects(),
                runStatus, () => config?.Single<BossConfig>(), () => objective.Secured, () => objective.StageActive);

            context.Register<IEncounterTracker>(Encounters);
            context.Register<IStageContent>(View);
            context.Register<IInteractionPromptView>(Interaction);
            context.Register<IInteractionTouch>(Interaction);
            context.Register<IBossView>(Boss);
        }

        void PublishEvent(CombatEventKind kind, int a, int b, Vector3 position, string tag) =>
            _events?.Publish(new CombatEvent(kind, _clock != null ? _clock.TickIndex : 0, _clock != null ? _clock.SimTime : 0f, a, b,
                position.x, position.y, position.z, tag));

        /// <summary>
        /// Where V3 initialized POIManager (LegacyStageAdapter.InitializePoi): markers, then V3's barrels. PvP scenes keep
        /// V3's rule (POIManager off, no markers, no barrels); the boss director is not used there either.
        /// </summary>
        public void InitializePoi(StageLayout layout, bool isPvPScene)
        {
            _active = !isPvPScene;
            var markers = new LegacyPoiMarkers(_usePlaceholderVisuals);
            if (!isPvPScene) markers.ClearRootLikeV3();
            Module.Initialize(layout, markers, enabled: !isPvPScene);
            if (isPvPScene || layout == null) return;
            // V3 always added the barrel world; V4 places none by default (adopted decision), so it only adds it when
            // the config asks for barrels (the v3-parity preset does).
            var c = _config();
            if (c == null || c.explosiveBarrelCount > 0) _enemyWorld.TakeOverBarrels(V3ExplosiveBarrelWorld.Ensure(layout), layout);
        }

        /// <summary>V3DiscoverySystem.Ensure's place: its update object, created here, and the reset around the spawn.</summary>
        public void EnsureDiscovery(StageLayout layout)
        {
            Module.Discovery.Initialize(layout);
            var go = new GameObject("V4DiscoverySlot");
            var slot = go.AddComponent<LegacyUpdateSlot>();
            slot.role = "discovery";
            var run = new LegacyStageRun();
            slot.Tick = dt => Module.Discovery.Tick(!run.Exists || run.IsPaused, run.ElapsedSeconds);
        }

        /// <summary>V3InteractionController.Ensure's place: its update object under the run canvas (the prompt panel is a presenter).</summary>
        public void EnsureInteraction(Transform canvas)
        {
            var go = new GameObject("V4InteractionSlot");
            if (canvas != null) go.transform.SetParent(canvas, false);
            var slot = go.AddComponent<LegacyUpdateSlot>();
            slot.role = "interaction";
            slot.Tick = Interaction.Tick;
        }

        /// <summary>The scene slot in POIManager's place (V4SceneSetup.ApplyM7).</summary>
        public void BindManagerSlot()
        {
            foreach (var s in Object.FindObjectsByType<LegacyUpdateSlot>(FindObjectsSortMode.None))
                if (s.role == StageRole) { s.Tick = Module.TickManager; return; }
            Debug.LogWarning("V4Game: no stage LegacyUpdateSlot in the scene (run RoguePirates/V4/Setup M7); adding one, frame order differs from V3.");
            var host = GameObject.Find("Systems");
            var added = (host != null ? host : new GameObject("V4StageSlot")).AddComponent<LegacyUpdateSlot>();
            added.role = StageRole;
            added.Tick = Module.TickManager;
        }

        /// <summary>Listens for boss kills (V3's kill path notified POIManager just before TriggerBus.Kill).</summary>
        public void AttachKillHook() => _killHook.Attach(Module.OnBossKilled);

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

        public void Dispose() => _killHook.Dispose();
    }
}
