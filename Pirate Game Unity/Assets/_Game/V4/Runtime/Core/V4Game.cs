using System;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Foundation.World;
using RoguePirates.Simulation;
using RoguePirates.V4.Config;
using RoguePirates.V4.Events;
using RoguePirates.V4.Ports;
using RoguePirates.V4.Spawning;
using UnityEngine;
using UnityEngine.SceneManagement;
using RoguePirates.V4.Composition;

namespace RoguePirates.V4
{
    /// <summary>
    /// V4 composition root. Builds the run context and module list and connects them. Systems that have no V4 module
    /// yet run through <see cref="RunBootstrapSequence"/> (same order and slot as V3RunBootstrap, so M0 runs match V3).
    /// It must not grow gameplay logic of its own: new behavior goes into modules; calls into the shared run, ship and world go through Ports/.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class V4Game : MonoBehaviour
    {
        [SerializeField] GameDatabase database;
        [SerializeField] StageBuilder stageBuilder;
        [SerializeField] PlayerShipController player;
        [SerializeField] IslandLayoutDefinition islandLayout;
        [SerializeField] bool usePlaceholderPoiVisuals = true;
        [Header("Config (M1)")]
        [Tooltip("The config set this run copies (SaltwindDefault).")]
        [SerializeField] ConfigSet configSet;
        [Tooltip("Module Lab scene: invulnerable ship, no ambient spawns or beats; enemies are spawned from the console.")]
        [SerializeField] bool moduleLab;
        [Tooltip("M8: the enemy world's scene values (were serialized on V3's EnemyWorld component in the V4 scenes).")]
        [SerializeField] Enemies.EnemyWorldSettings enemyWorld = new Enemies.EnemyWorldSettings();

        /// <summary>Raised once per run after it starts; DevTools hooks the Tuning Console here (event, not state).</summary>
        public static event Action<V4Game> RunStarted;

        ConfigSetRuntime _config;
        Weapons.WeaponKillGold _killGold;
        RunClock _clock;
        CombatEvents _events;
        RunContext _context;
        ModuleRegistry _modules;
        RunBootstrapSequence _shell;
        RunEventPublisher _publisher;
        RunPort _runPort;
        ShipPort _shipPort;
        SpawnController _spawn;
        FramePipeline _frame;
        /// <summary>M14.14: the run's frame steps.</summary>
        public FramePipeline Frame => _frame;
        WeaponModule _weapons;
        Telemetry.ResultsTelemetry _resultsTelemetry;
        StageModule _stage;
        EnemyWorldModule _enemies;
        BuildModule _build;
        Difficulty.DifficultyDirector _director;
        Rewards.RewardModule _rewards;
        Narrative.BarkDirector _barks;
        Narrative.TutorialDirector _tutorial;
        Rewards.DamageShareTracker _damageShare;
        SpawnerProfiles _spawnerProfiles;
        Telemetry.RunTelemetry _telemetry;
        Views.ToastForwarder _toasts;
        RunLifecycle _lifecycle;
        int _seed;
        bool _running;
        bool _ended;
        bool _objectiveSecured;

        public RunContext Context => _context;
        public ConfigSetRuntime Config => _config;
        public ModuleRegistry Modules => _modules;
        public CombatEvents Events => _events;
        public RunBootstrapSequence Shell => _shell;
        /// <summary>M9: the build and progression modules (console sections, trace pilot).</summary>
        public BuildModule Build => _build;
        /// <summary>The stage module (tests reach the chest economy through it).</summary>
        public Composition.StageModule Stage => _stage;
        public bool IsRunning => _running;
        /// <summary>M14: the run lifecycle (death flow, damage record).</summary>
        public RunLifecycle Lifecycle => _lifecycle;
        /// <summary>M14: the run's telemetry (port of V3's RunCombatTelemetry).</summary>
        public Telemetry.RunTelemetry Telemetry => _telemetry;
        public bool IsModuleLab => moduleLab;

        /// <summary>Playtest (3 Oct, owner): the run's mode (<see cref="RunRequest.Mode"/>): a voyage, or a 1v1 match.</summary>
        public RunMode Mode { get; private set; }
        public bool IsPvp => Mode == RunMode.Pvp;
        /// <summary>The 1v1 match (null in a voyage).</summary>
        public RoguePirates.V4.Pvp.PvpMatchController PvpMatch => _pvp;
        RoguePirates.V4.Pvp.PvpMatchController _pvp;
        PlayerDamageService _damageSink;

        void Awake()
        {
            Mode = moduleLab ? RunMode.Voyage : RunRequest.ConsumeMode();
            var pvp = IsPvp;
            _config = configSet != null ? new ConfigSetRuntime(configSet) : null;
            ApplyRequestedPreset();
            // V5 M8z (7 Oct): the weapon switch-off list is config (the run's, with its preset).
            if (_config != null) RoguePirates.V4.Foundation.Combat.WeaponFeatureFlags.Register(_config.Single<UnlockConfig>()?.disabledWeaponIds);
            var run = _config?.Single<RunConfig>();
            _clock = run != null ? new RunClock(run.tickRate, run.maxStepsPerFrame) : new RunClock();
            _events = new CombatEvents();
            _context = new RunContext(_clock, _events, _config);
            if (_config != null) _config.ConfigChanged += OnConfigChanged;
            _modules = new ModuleRegistry();
            // Each phase registers its module here and removes the legacy piece it replaces.
            // M3: the spawn controller (replaces the legacy ambient spawner and the V3 spawn directors).
            _spawn = new SpawnController { LabMode = moduleLab, PvpMode = pvp };
            _modules.Add(_spawn);
            // M10: the difficulty director (measures the player's power, logs Pressure). M11: Adaptive by default; the levers
            // bend ambient spawning only.
            _director = new Difficulty.DifficultyDirector();
            _modules.Add(_director);
            _context.Register<Difficulty.IPressureView>(_director);
            _context.Register<Difficulty.IDifficultyLevers>(_director); // M11: the adaptive levers (ambient spawns read them)
            // M12: Rum — rows listening to events, settled once per run id at death (replaces the V3 Marks tracker).
            _context.Register<Rewards.IRumBank>(new Rewards.SaveRumBank(() => AppRoot.Instance != null ? AppRoot.Instance.Save : null));
            _rewards = new Rewards.RewardModule
            {
                LabMode = moduleLab || pvp, // no Rum in a 1v1
                RunTime = () => RunManager.Instance != null ? RunManager.Instance.ElapsedSeconds : 0f,
                RunGold = () => RunManager.Instance != null ? RunManager.Instance.Gold : 0,
            };
            _modules.Add(_rewards);
            _context.Register<Rewards.IRumView>(_rewards);
            // M13: barks and the First Conquest tutorial (replace V3OnboardingDirector); presentation reads the narrative view.
            var narrative = new Narrative.NarrativeView();
            _barks = new Narrative.BarkDirector { View = narrative, CaptainKey = () => _build?.Captain?.Config?.captainKey };
            _tutorial = new Narrative.TutorialDirector { View = narrative, LabMode = moduleLab || pvp };
            _modules.Add(_barks);
            _modules.Add(_tutorial);
            _context.Register<Narrative.INarrativeView>(narrative);
            _context.Register<Narrative.IBarkControl>(_barks);
            _context.Register<Narrative.ITutorialControl>(_tutorial);
            _context.Register<ISpawnController>(_spawn);
            // Contracts the V4 modules use, implemented over legacy systems until those systems are ported.
            // M14: the shared run and ship (kept until M15) behind two ports; every V4 reader goes through them.
            var runState = new RunPort
            {
                Timeline = () => _config?.Single<WaveTimelineConfig>(),
                EndedByDeathSource = () => _rewards != null && _rewards.EndedByDeath,
            };
            var ship = new ShipPort(() => _shell != null && _shell.Player != null ? _shell.Player : player);
            // M14: the run's telemetry (port of V3's static RunCombatTelemetry + WeaponDamageLedger, same export) and the
            // run lifecycle (start tracking, damage record, death flow; was LegacyRunLifecycle).
            var experiment = _config?.Single<ExperimentConfig>();
            _telemetry = new Telemetry.RunTelemetry(experiment != null ? experiment.legacyProfileId : null, experiment != null ? experiment.weaponSlotCap : 6,
                experiment == null || experiment.telemetryEnabled, () => runState.ElapsedSeconds, () => runState.RunSeed)
            {
                GoldEarned = () => runState.GrossGoldEarned,
                GoldSpent = () => runState.GrossGoldSpent,
            };
            // M14: the run's one toast sink (was V3ToastHud's static instance); the toast presenter plugs in when it binds.
            _toasts = new Views.ToastForwarder();
            _lifecycle = new RunLifecycle(runState, _telemetry, _rewards)
            {
                Toast = _toasts.Show,
                SetShipMovement = ship.SetMovementEnabled,
            };
            var encounters = new SpawnRateModifiers(runState, () => _config?.Single<WaveTimelineConfig>());
            // M8: the V4 enemy world (replaces EnemyWorld, StatusController and HostileProjectileWorld).
            var encounterTracker = new EncounterTracker(); // M14: per run (was V3's static RunEncounterTracker)
            _enemies = new EnemyWorldModule(_config, enemyWorld, ship, runState, encounterTracker);
            _context.Register<IEnemySpawnSink>(_enemies.Contracts);
            _context.Register<Enemies.IEnemyWorldView>(_enemies.Contracts);
            _context.Register<Enemies.IEnemyDevControl>(_enemies.Contracts);
            _context.Register<Enemies.IHostileProjectileView>(_enemies.Services.Shots);
            _context.Register<Enemies.IHazardView>(_enemies.Services.Hazards);
            _context.Register<ISpawnPlacement>(new SpawnPlacementPort(() => _shell != null ? _shell.Player : null));
            _context.Register<IRunRandom>(runState);
            _context.Register<IRunStatus>(runState);
            _context.Register<ISpawnFeedback>(new SpawnFeedback(_toasts, _telemetry)); // M14 (was LegacySpawnFeedback)
            var playBounds = new PlayBoundsPort();
            _context.Register<IStormArena>(playBounds);
            _context.Register<Views.IWorldView>(playBounds);
            var damageSink = new PlayerDamageService(ship); // M14.14 (was LegacyPlayerDamageSink)
            _damageSink = damageSink;
            // V5: island impacts cost hull through the build's incoming multiplier.
            if (player != null) player.IslandImpactDamage += (at, amount) => damageSink.Impact(amount, at);
            _context.Register<IPlayerDamageSink>(damageSink);
            _context.Register<ISpawnRateSource>(encounters);
            // M14.14: one frame driver runs every run tick in the captured order (were legacy update slots).
            _frame = new FramePipeline { Clock = () => runState.ElapsedSeconds };
            var driver = GetComponent<V4FrameDriver>();
            if (driver == null) driver = gameObject.AddComponent<V4FrameDriver>();
            driver.Pipeline = _frame;
            _enemies.Frame = _frame;
            _shell = new RunBootstrapSequence(this, database, stageBuilder, player, islandLayout, usePlaceholderPoiVisuals, _config, _frame, _spawn, encounters);
            // M14: spawn profiles built from the spawner configs alone (were per-run clones of V3's schedule profiles).
            _spawnerProfiles = new SpawnerProfiles(_config);
            _spawnerProfiles.Build();
            _shell.Spawners = _spawnerProfiles;
            _context.Register<ISpawnerProfiles>(_spawnerProfiles);
            _shell.Enemies = _enemies;
            _enemies.Toasts = _toasts;
            _enemies.HitFeedback = () => _context.TryGet<Enemies.IEnemyHitFeedback>(out var feedback) ? feedback : null;
            // M14.9b: blasts and enemy shot models reach the presenters registered in the context (forwarders until then).
            _context.Register<Enemies.IEnemyEffects>(_enemies.Services.Effects);
            _context.Register<Enemies.IBurningPatchView>(_enemies.Hellfire);
            _enemies.EffectsTarget = () => _context.TryGet<Enemies.IEnemyEffects>(out var effects) ? effects : null;
            _enemies.ShotViewsTarget = () => _context.TryGet<Enemies.IHostileShotViews>(out var shots) ? shots : null;
            _enemies.ViewsTarget = () => _context.TryGet<Enemies.IEnemyViewSink>(out var enemyViews) ? enemyViews : null; // M14.10
            _shell.Lifecycle = _lifecycle;
            _shell.Toasts = _toasts;
            _shell.RunPort = runState;
            _enemies.Telemetry = _telemetry;
            _enemies.KillTargets.WeaponKill = _telemetry.RecordKill;
            _killGold = new Weapons.WeaponKillGold(() => _config);
            _enemies.KillTargets.WeaponKillGold = _killGold.Bonus;
            // M9: level-ups, Logbooks, items, the Captain, floor powerups and pickups (replace LevelSystem, the weapon
            // record, PickupWorld, the V3 Captain/item/powerup components and the switched-off EncounterSpawner).
            _build = new BuildModule(_config, () => _shell != null ? _shell.Player : null, () => _shell?.Database);
            _build.Frame = _frame;
            _shell.Build = _build;
            _build.PickupViewsTarget = () => _context.TryGet<Progression.IPickupViews>(out var pickups) ? pickups : null;
            _build.PowerupViewsTarget = () => _context.TryGet<Progression.IPowerupViews>(out var powerups) ? powerups : null;
            _build.OfferPresenterTarget = () => _context.TryGet<Progression.IOfferPresenter>(out var cards) ? cards : null; // M14.11
            _build.Toasts = _toasts;
            _build.Telemetry = _telemetry;
            _enemies.Build = _build;
            _enemies.KillTargets.SpawnKillDrops = _build.SpawnKillDrops;
            _enemies.KillTargets.RollPowerup = _build.RollPowerup;
            _enemies.KillTargets.ItemKill = _build.NotifyItemKill;
            _enemies.Services.PlayerIncomingDamage = _build.IncomingDamageMultiplier;
            ship.IncomingDamage = _build.IncomingDamageMultiplier;
            damageSink.IncomingDamage = _build.IncomingDamageMultiplier;
            damageSink.RecordDamage = _lifecycle.RecordDamage;
            damageSink.TookSteadyDamage = _build.Items.NotifyPlayerTookDamage;
            encounters.DeadCalm = () => _build.Powerups.SpawnRateMultiplier;
            encounters.SpawnDensity = _build.Items.SpawnDensityMultiplier; // R-40: Cursed Compass
            _enemies.Contracts.ControlledTargetMultiplier = _build.Items.ControlledTargetDamageMultiplier; // R-40: Leviathan's Tooth
            _enemies.StatusDamage = _build.StatusDamageMultiplier; // R-40/R-49: burn ticks
            _enemies.NearMissMargin = _build.NearMissMargin; // R-40: near-miss dodges
            _enemies.Services.Shots.NearMiss += _build.NotifyNearMiss;
            runState.Level = () => _build.Progression.Level;
            _context.Register<Progression.IItemInventory>(_build.Items);
            // M5: the Weapon Manager (replaces WeaponController's firing and ProjectileWorld). M9: it reads the V4 loadout.
            _weapons = new WeaponModule(_shell, _context, _config, runState, ship, _events, _clock, _enemies, _build, _telemetry);
            _weapons.Frame = _frame;
            _shell.Weapons = _weapons;
            _build.Weapons = _weapons.Manager;
            _build.Enemies = _enemies.Contracts;
            _context.Register<Difficulty.IPowerInputs>(new Difficulty.PowerInputs(() => _weapons?.Manager, () => _build.WeaponModifiers, _build.IncomingDamageMultiplier, ship));
            _enemies.HellfireEnvironment.Calculator = () => _weapons.Manager.Combat.Calculator;
            _enemies.HellfireEnvironment.HasHellfire = _build.HasHellfire;
            _enemies.HellfireEnvironment.StatusDamage = _build.StatusDamageMultiplier;
            _context.Register<RoguePirates.V4.Weapons.IWeaponManager>(_weapons.Manager);
            // M6: the read-only views presentation reads (over the legacy hubs until their modules exist).
            // M7: stage content (chests, landmarks, Beacon, discovery, interaction, boss director) replaces POIManager.
            _stage = new StageModule(_context, _config, runState, ship, damageSink, _spawn, () => _shell.Player, () => _weapons.Manager, usePlaceholderPoiVisuals, _enemies, encounterTracker, _toasts);
            _stage.Frame = _frame;
            _stage.PvpMode = pvp; // Playtest (3 Oct, owner): no points of interest, barrels or boss in a 1v1
            _enemies.KillTargets.BossKilled = _stage.Module.OnBossKilled; // V3 told POIManager right before TriggerBus.Kill
            _shell.Stage = _stage;
            _stage.Economy.AddItem = _build.Items.TryAddItem;
            _stage.Economy.HasItem = _build.Items.HasItem; // V5 M3: the evolution-item chance
            _stage.Economy.WeaponUnlocked = Ports.ContentPort.IsWeaponUnlocked; // V5 M4: evolution items of locked weapons stay out of chests
            // V5 M5: bought items, Skeleton Key, Smuggler's Ledger, Black Spot, the Plunderer Logbook.
            _stage.Economy.ItemUnlocked = id => Composition.BuildModule.IsBought(_config, id);
            _stage.Economy.FreeChance = _build.Items.FreeChestChance;
            _stage.Economy.PriceMultiplier = _build.Items.ChestPriceMultiplier;
            _stage.Economy.RarityBoost = _build.Items.ChestRarityBoost;
            _enemies.KillTargets.EliteRewardMultiplier = _build.Items.EliteRewardMultiplier;
            _enemies.KillTargets.BossGoldMultiplier = () => RunManager.Instance != null ? RunManager.Instance.Stats.Get(StatId.DoubloonGain) : 1f;
            _stage.Rewards.Telemetry = _telemetry;
            var unlocks = new Rewards.UnlockService(() => AppRoot.Instance != null ? AppRoot.Instance.Save : null, () => _config); // M12
            _damageShare = new Rewards.DamageShareTracker(); // M12: results damage share from the weapon hit records
            if (_weapons.Hooks != null) _weapons.Hooks.HitRecorded += _damageShare.Record;
            // M14.11: V4 views (was LegacyViewFactory over the legacy hubs); the kept run manager's facts through the run port.
            var views = Views.RunViewsFactory.Create(new Views.RunViewSources
            {
                Run = runState,
                Ship = ship,
                Toasts = _toasts,
                Commands = new Views.HudCommands(ship.RequestBoost, pvp ? (System.Action)LeavePvp : RestartRun, pvp ? (System.Action)LeavePvp : GoToMenu, unlocks, () => Views.HudCommands.TogglePause(RunManager.Instance)),
                Enemies = _enemies.Contracts,
                Weapons = _weapons.Manager,
                Captain = _build.Captain,
                ActivesOn = _build.ActivesEnabled,
                ActiveKeyHint = _build.ActiveKeyHint,
                Progression = _build.Progression,
                Logbook = _build.Logbook,
                Items = _build.Items,
                Item = _build.ItemConfigFor,
                WeaponSlotCap = () => _config?.Single<ExperimentConfig>()?.weaponSlotCap ?? 6,
                Config = _config,
                Rum = _rewards,
                Unlocks = unlocks,
                Damage = _damageShare,
                ObjectiveSecured = () => runState.ObjectiveSecured,
                ResultsFooter = () => _shell.StageProfile.ResultsFooter(runState.RunSeed, runState.ElapsedSeconds),
                GoldEarned = () => runState.GrossGoldEarned,
                GoldSpent = () => runState.GrossGoldSpent,
                GoldRemaining = () => runState.Gold,
                Stats = () => RunManager.Instance != null ? RunManager.Instance.Stats : null, // V5 M8z (7 Oct): the level-up cards
            });
            views.Stage = _stage.View;
            _context.Register<Views.IRunViews>(views);
            _resultsTelemetry = new Telemetry.ResultsTelemetry(_telemetry, _rewards, () => runState.ObjectiveSecured);
            _shell.Rewards = _rewards;
            // M14: native event producers (was LegacyEventFeed republishing legacy events).
            _publisher = new RunEventPublisher(_events, _clock);
            _enemies.KillTargets.Killed = _publisher.Killed; // M12: Killed events carry the unit id and run-clock time
            _enemies.Simulation.HitPublished = _publisher.Hit;
            damageSink.SteadyDamageReported += _publisher.SteadyDamage;
            _runPort = runState;
            _shipPort = ship;
            // V5 M8z (6 Oct): tests run with DemoMode.Off; they don't write session files.
            if (!moduleLab && !pvp && !RoguePirates.V4.Foundation.Core.DemoMode.Off) AddSessionRecorder(runState, ship, damageSink);
            _shell.Awake();
        }

        Telemetry.SessionRecorder _session;
        /// <summary>V5 M8z (7 Oct): the voyage's session recorder (null when it records none: PvP, the Module Lab, DemoMode
        /// off), for the playtest-preset tests.</summary>
        public Telemetry.SessionRecorder Session => _session;

        /// <summary>
        /// Playtest (6 Oct, owner: "let me play a session, then use that data to verify if the simulations are accurate"):
        /// the voyage's session file for plan/playtest/montecarlo/compare_session.mjs. The damage source tag rides on the
        /// enemy simulation, the hostile shots and the stage damage so PlayerDamaged events say who hit the ship.
        /// </summary>
        void AddSessionRecorder(RunPort runState, ShipPort ship, PlayerDamageService damageSink)
        {
            var source = new PlayerDamageSource();
            ship.DamageSource = source;
            damageSink.DamageSource = source;
            _enemies.Services.DamageSource = source;
            if (_enemies.Services.Shots != null) _enemies.Services.Shots.DamageSource = source;
            var progression = _build.Progression;
            _session = new Telemetry.SessionRecorder
            {
                RunTime = () => runState.ElapsedSeconds,
                RunExists = () => runState.Exists,
                Level = () => progression.Level,
                Xp = () => progression.CurrentXp,
                XpToNext = () => progression.XpToNext,
                Kills = () => runState.Kills,
                Gold = () => runState.Gold,
                HullKnown = () => ship.HasDamageReceiver,
                Hull = () => ship.Hp,
                MaxHull = () => ship.MaxHp,
                SunkNow = () => (ship.HasDamageReceiver && ship.Hp <= 0f) || runState.EndedByDeath,
                RunEnded = () => runState.IsRunEnded,
                ObjectiveSecured = () => runState.ObjectiveSecured,
                Captain = () => _build.Captain?.Config != null ? _build.Captain.Config.captainKey : null,
                StartingWeapon = () => progression.OwnedWeapons.Count > 0 ? progression.OwnedWeapons[0] : null,
                ConfigHash = () => _config?.Hash,
                StageProfile = () => _lifecycle.StageProfile?.Invoke(),
                IsMiniboss = _enemies.Contracts.DefinitionIsMiniboss,
                Telemetry = _telemetry,
            };
            _telemetry.PoiUsed = _session.OnPoiUsed;
            _telemetry.ItemAcquired = _session.OnItemAcquired;
            progression.OfferChosen += _session.OnOfferChosen;
            progression.Evolved += _session.OnEvolved;
            _modules.Add(_session);
        }

        void Start()
        {
            var request = RunRequest.Consume();
            var seed = RunBootstrapSequence.ResolveSeed(request.seed, request.retry);
            _seed = seed;

            _modules.InitializeAll(_context);
            // Playtest (4 Oct, review): a 1v1 run without its match goes back to the menu instead of hanging.
            if (IsPvp && !PreparePvp()) { Debug.LogWarning("V4Game: no 1v1 match to start; back to the menu."); GoToMenu(); return; }
            // Playtest (4 Oct): a voyage started while still seated in a 1v1 lobby leaves it (a match starting there could
            // not be played from the voyage and would be lost on time).
            if (!IsPvp && !moduleLab && Online.PvpSession.Exists && Online.PvpSession.Instance.InLobby) _ = Online.PvpSession.Instance.Leave();
            if (!_shell.Start(seed, request.retry)) return;
            if (_pvp != null)
            {
                _pvp.Begin(_shipPort, _damageSink, _build, _enemies.Simulation, _toasts);
                _pvp.AttachWeapons(_weapons.Effects, () => _context.TryGet<RoguePirates.V4.Weapons.IWeaponEffects>(out var fx) ? fx : null, _config);
            }

            _runPort.AttachEvents(_publisher);
            _shipPort.AttachEvents(_publisher);
            _publisher.AttachProgression(_build.Progression);
            _build.AttachShip(_shell.Player);
            _runPort.StateChanged += _resultsTelemetry.OnRunState; // M14.11 (was LegacyResultsTelemetry on the run manager)
            if (_session != null) _runPort.StateChanged += _session.OnRunState; // Playtest (6 Oct, owner): saved when the results show
            if (moduleLab) _shell.EnterModuleLab();
            _killGold?.ResetForRun();
            _modules.BeginRunAll(new RunStart(seed, request.retry));
            _running = true;
            RunStarted?.Invoke(this);
        }

        void Update()
        {
            if (!_running) return;
            // Legacy systems tick exactly like V3RunBootstrap.Update (variable dt) until their modules replace them.
            _shell.Tick(Time.deltaTime);

            var run = RunManager.Instance;
            SyncPause(run);
            if (run != null && !run.IsRunEnded)
            {
                var steps = _clock.Advance(Time.deltaTime);
                for (var i = 0; i < steps; i++)
                {
                    _modules.TickAll(_clock.NextTick());
                    _events.EndTick(); // one batch per simulation tick
                }
            }

            if (run == null) return;
            _objectiveSecured = run.ObjectiveSecured;
            if (!_ended && run.IsRunEnded) EndRun();
        }

        void LateUpdate()
        {
            if (!_running) return;
            // (M14.11: the level-up presenter hides its cards on Skip; the end-of-frame R-83 check is gone.)

            // Legacy producers publish outside V4 ticks (their own Update, or while paused); deliver those with the
            // current tick stamp so readers see them this frame instead of waiting for the next tick.
            if (_events.Pending.Count > 0) _events.EndTick();
        }

        /// <summary>The V4 clock mirrors the legacy pause (one source of truth until V4 owns pausing).</summary>
        void SyncPause(RunManager run)
        {
            _clock.Pause.Remove(SimPauseReason.Manual | SimPauseReason.Choice);
            if (run == null || !run.IsPaused) return;
            _clock.Pause.Add(run.PauseReason == PauseReason.UserPause ? SimPauseReason.Manual : SimPauseReason.Choice);
        }

        void EndRun()
        {
            _ended = true;
            if (!moduleLab && !IsPvp) _build?.RecordRunGold(); // lifetime gold (Richie's unlock)
            if (!moduleLab && !IsPvp) RecordRunScore();
            _modules.EndRunAll(new RunEnd(_clock.SimTime, _objectiveSecured));
        }

        /// <summary>M11: the leaderboard record (kills beside average/peak Pressure, director version, config hash). Only runs
        /// that ended (death), not runs left mid-way.</summary>
        void RecordRunScore()
        {
            var run = RunManager.Instance;
            if (_director == null || run == null) return;
            try
            {
                var record = _director.CreateRecord(_seed, run.KillCount, run.ElapsedSeconds, _config?.Hash, _config != null ? _config.ChangeLog.Count : 0);
                RunRecords.Add(record);
            }
            catch (System.Exception e) { Debug.LogException(e); } // a record failure never blocks the run end
        }

        /// <summary>
        /// Playtest (3 Oct, owner): a 1v1 run: the match controller places the ship at its seat, the draft is the starting
        /// build (first Captain for both), level-ups apply by priority without cards, a sinking is the match's. The rest
        /// of the PvE switches were set in Awake (spawns, stage content, Rum, tutorial).
        /// </summary>
        bool PreparePvp()
        {
            _pvp = gameObject.AddComponent<RoguePirates.V4.Pvp.PvpMatchController>();
            if (!_pvp.Prepare(Online.PvpSession.Instance)) return false;
            var draft = _pvp.MyDraft;
            _build.PvpWeapons = draft?.weapons ?? System.Array.Empty<string>();
            _build.PvpLogbooks = draft?.logbooks ?? System.Array.Empty<string>();
            _build.Progression.AutoLevelUp = _pvp.AutoLevel;
            _shell.PlaceShip = _pvp.PlaceShip;
            _lifecycle.PvpSunk = _pvp.OnSunk;
            _context.Register<RoguePirates.V4.Pvp.IPvpMatchView>(_pvp);
            return true;
        }

        /// <summary>M11: where run records go (tests swap it).</summary>
        public Difficulty.IRunRecordStore RunRecords { get; set; } = new Difficulty.PlayerPrefsRunRecordStore();

        void OnDestroy()
        {
            // Fallback for runs left before they ended (restart, menu); values captured while RunManager existed.
            if (_running && !_ended)
            {
                _ended = true;
                if (!moduleLab && !IsPvp) _build?.RecordRunGold(); // a run left mid-way (restart, menu) still earned its gold
                _modules.EndRunAll(new RunEnd(_clock.SimTime, _objectiveSecured));
            }

            _publisher?.Detach();
            _runPort?.DetachEvents();
            _shipPort?.DetachEvents();
            if (_runPort != null && _resultsTelemetry != null) _runPort.StateChanged -= _resultsTelemetry.OnRunState;
            if (_session != null)
            {
                if (_runPort != null) _runPort.StateChanged -= _session.OnRunState;
                _build.Progression.OfferChosen -= _session.OnOfferChosen;
                _build.Progression.Evolved -= _session.OnEvolved;
                _session.Detach();
            }
            _damageShare?.Dispose();
            _spawnerProfiles?.Dispose();
            _stage?.Dispose();
            _enemies?.Dispose();
            _weapons?.Dispose();
            _build?.Dispose();
            _shell?.Unwire();
            _shell?.OnDestroy();
            if (_config != null) _config.ConfigChanged -= OnConfigChanged;
            _config?.Dispose();
        }

        /// <summary>A preset requested for this run (console "restart with preset") applies before anything reads config.</summary>
        void ApplyRequestedPreset()
        {
            var json = RunRequest.ConsumePreset();
            if (_config == null || string.IsNullOrEmpty(json)) return;
            var errors = new System.Collections.Generic.List<string>();
            if (!PresetCodec.Apply(_config, PresetCodec.FromJson(json), 0f, errors))
                Debug.LogWarning("V4Game: preset applied with errors:\n" + string.Join("\n", errors));
        }

        /// <summary>Every live config edit is also a ConfigChanged fact on the event stream (telemetry, console log).</summary>
        void OnConfigChanged(ConfigChange change)
        {
            _enemies?.OnConfigChanged();
            _events.Publish(new CombatEvent(CombatEventKind.ConfigChanged, _clock.TickIndex, _clock.SimTime, 0, 0, 0f, 0f, 0f, change.configId + "." + change.path));
        }

        void OnApplicationPause(bool paused) => _shell?.OnApplicationPause(paused);

        public static void RestartRun()
        {
            RunBootstrapSequence.NotifyRetryStarting();
            RunRequest.MarkRetry();
            SceneManager.LoadScene(CurrentRunScene());
        }

        public static void RestartRunWithSeed(int seed)
        {
            RunRequest.RequestSeed(seed);
            SceneManager.LoadScene(CurrentRunScene());
        }

        /// <summary>Restarts reload the run scene they came from (the Module Lab stays the lab).</summary>
        static string CurrentRunScene() =>
            SceneManager.GetActiveScene().name == V4SceneNames.ModuleLab ? V4SceneNames.ModuleLab : V4SceneNames.SaltwindRun;

        public static void GoToMenu() => SceneManager.LoadScene(V4SceneNames.MainMenu);

        /// <summary>Playtest (3 Oct, owner, round 7): a 1v1's Retry and Main Menu leave the match and its lobby, so nothing of
        /// it follows the player into the next voyage.</summary>
        void LeavePvp()
        {
            if (_pvp != null) { _pvp.MainMenu(); return; }
            if (Online.PvpSession.Exists) Online.PvpSession.Instance.ReturnToMenu(true);
            else GoToMenu();
        }
    }
}
