using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Debugging;
using HighSeas.Encounters;
using HighSeas.Enemies;
using HighSeas.Pickups;
using HighSeas.Player;
using HighSeas.POI;
using HighSeas.UI;
using HighSeas.V3;
using HighSeas.V3.MultiShip;
using HighSeas.VFX;
using HighSeas.Audio;
using HighSeas.Weapons;
using HighSeas.World;
using UnityEngine;

namespace RoguePirates.V4.Legacy
{
    // Each adapter wraps one legacy area that V4 still depends on. The attribute names the phase that deletes it;
    // DeletedInPhaseTests fails if an adapter outlives that phase. Method bodies are ported from V3RunBootstrap
    // with the call order unchanged, so M0 runs are identical to V3 runs.

    /// <summary>Stage balance profile context (V3 asset in M0; runtime profile built from V4 config from M1).</summary>
    [DeletedInPhase(14, "Profile context readers are gone after switch-over")]
    public sealed class LegacyProfileAdapter
    {
        /// <summary>
        /// Begins the stage profile context with the per-run profile built from V4 config (M1; replaces the scene's
        /// StageBalanceProfileHost). Validated with the same rules the V3 resolver applies.
        /// </summary>
        public bool TryBegin(StageBalanceProfile runtimeProfile, out string error)
        {
            error = null;
            StageBalanceProfileContext.EndRun();
            if (V3RunSession.IsLocalMultiplayer) return true;
            if (runtimeProfile == null) { error = "V4Game: no stage profile (config set or legacy base profile missing)."; return false; }
            var validation = StageBalanceProfileValidator.Validate(runtimeProfile);
            if (!validation.IsValid) { error = validation.ErrorMessage; return false; }
            StageBalanceProfileContext.BeginRun(runtimeProfile);
            return true;
        }

        /// <summary>
        /// Rebuilds the profile snapshot after a live config edit. Readers of Authoring see the edited runtime profile
        /// at once; the snapshot is rebuilt and the chest purchase count replayed so chest prices do not reset.
        /// </summary>
        public void Rebuild(StageBalanceProfile runtimeProfile)
        {
            if (!StageBalanceProfileContext.IsActive) return;
            var purchases = StageBalanceProfileContext.Runtime?.SuccessfulChestPurchases ?? 0;
            StageBalanceProfileContext.BeginRun(runtimeProfile);
            for (var i = 0; i < purchases; i++) StageBalanceProfileContext.Runtime.RegisterSuccessfulChestPurchase();
        }

        public void EndIfActive()
        {
            if (StageBalanceProfileContext.IsActive)
                AppRoot.EndActiveSaltwindRunContext();
        }
    }

    /// <summary>Game database load and the runtime catalogs. M0 applies them exactly as V3 does.</summary>
    [DeletedInPhase(9, "Enemies (M2), weapons (M5) and logbooks (M9) come from V4 configs")]
    public sealed class LegacyDatabaseAdapter
    {
        public GameDatabase Database { get; private set; }

        public void Load(GameDatabase serialized)
        {
            Database = serialized;
            if (Database == null || !GameDatabaseLoader.IsValid(Database))
                Database = GameDatabaseLoader.Load();
        }

        public void ApplyCatalogs(CombatExperimentProfile experiment)
        {
            CombatWeaponCatalog.ApplyToDatabase(Database, experiment);
            CombatSpecialistEnemyCatalog.ApplyToDatabase(Database, experiment);
        }
    }

    /// <summary>World generation, player spawn placement, bounds and V3 run context.</summary>
    [DeletedInPhase(14, "Becomes WorldModule over the shared StageBuilder")]
    public sealed class LegacyWorldAdapter
    {
        readonly StageBuilder _stageBuilder;
        readonly IslandLayoutDefinition _islandLayout;

        public LegacyWorldAdapter(StageBuilder stageBuilder, IslandLayoutDefinition islandLayout)
        {
            _stageBuilder = stageBuilder != null ? stageBuilder : Object.FindFirstObjectByType<StageBuilder>();
            _islandLayout = islandLayout;
        }

        public StageBuilder StageBuilder => _stageBuilder;

        public static void DisableEnvironmentalHazards()
        {
            EnvironmentalHazardsDisabled.Set(true);
            foreach (var juice in Object.FindObjectsByType<PlayerJuiceController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                juice.DisableSeaFoamTrail();
            foreach (var wind in Object.FindObjectsByType<WindGustDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(wind);
            foreach (var visual in Object.FindObjectsByType<EnvironmentHazardVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(visual.gameObject);
            foreach (var current in Object.FindObjectsByType<OceanCurrentZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(current.gameObject);
            foreach (var pool in Object.FindObjectsByType<WhirlpoolHazard>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(pool.gameObject);
            var root = GameObject.Find("EnvironmentHazards");
            if (root != null) Object.Destroy(root);
        }

        public static void EnsureTouchJoystickActive()
        {
            var joystick = GameObject.Find("JoystickBG");
            if (joystick != null && !joystick.activeSelf) joystick.SetActive(true);
        }

        public static void EnsureGameplayBootstrapComponents()
        {
            var systems = GameObject.Find("Systems");
            if (systems == null) return;
            if (systems.GetComponent<PlayerShipVisualBootstrap>() == null)
                systems.AddComponent<PlayerShipVisualBootstrap>();
        }

        public IslandLayoutDefinition ResolveIslandLayout()
        {
            if (_islandLayout != null && _islandLayout.HasIslands) return _islandLayout;
            var fromResources = Resources.Load<IslandLayoutDefinition>("V3SaltwindIslandLayout");
            if (fromResources != null && fromResources.HasIslands) return fromResources;
            return IslandLayoutDefinition.LoadAssignedOrDefault(null);
        }

        public void ConfigureStageBuilder()
        {
            if (_stageBuilder == null) return;
            _stageBuilder.ConfigureV3Layout(ResolveIslandLayout());
            _stageBuilder.SetProceduralIslandSettings(ProceduralIslandSettings.Default.Clamped());
            _stageBuilder.SetIslandDensityMultiplier(1f);
        }

        public StageLayout Build(int seed) => _stageBuilder != null ? _stageBuilder.Build(seed, 0) : null;

        public static void ResetWorldVisuals()
        {
            V3StormEdgeVisual.DisableWorldRings();
            PlayBounds.ResetStormSafeRegion();
            DisableEnvironmentalHazards();
            V3SaltwindStormRingVisual.Disable();
            V3PlayBoundsWallVisual.Ensure();
        }

        public static void SetRunContext(StageLayout layout)
        {
            V3RunContext.Layout = layout;
            V3RunContext.CaptainId = V3CaptainSelection.Selected;
        }

        public void PlacePlayerAtSafeSpawn(PlayerShipController ship, StageLayout layout, int seed)
        {
            const float hullRadius = 1.15f;
            Physics.SyncTransforms();
            var spawn = V3SpawnAnchorValidator.ResolveRuntimeSpawn(layout, ResolveIslandLayout(), seed, hullRadius);
            if (IslandObstacles.IsAvailable)
            {
                var height = IslandObstacles.GetCollisionHeight(hullRadius);
                for (var i = 0; i < 8; i++)
                {
                    if (!IslandObstacles.IsPositionBlocked(spawn, hullRadius)) break;
                    spawn = IslandObstacles.Depenetrate(spawn, hullRadius, height);
                }
            }

            ship.SetWorldPosition(spawn);
        }
    }

    /// <summary>Legacy enemy, projectile, pickup and status worlds: reset, definitions and the per-frame tick.</summary>
    [DeletedInPhase(8, "V4 Enemy World")]
    public sealed class LegacyEnemyWorldPort
    {
        public static void ClearTransientWorlds()
        {
            Object.FindFirstObjectByType<PickupWorld>()?.ClearAllActive();
            Object.FindFirstObjectByType<ProjectileWorld>()?.ClearAllActive();
            Object.FindFirstObjectByType<HostileProjectileWorld>()?.ClearAllActive();
        }

        public static EnemyWorld ResetAndDefine(GameDatabase database)
        {
            Object.FindFirstObjectByType<EnemyWorld>()?.ResetAllAgentsForPromo();
            ClearTransientWorlds();
            var enemyWorld = Object.FindFirstObjectByType<EnemyWorld>();
            enemyWorld?.SetDefinitions(database.enemies);
            return enemyWorld;
        }

        public static void EnsureHostileProjectiles()
        {
            if (Object.FindFirstObjectByType<HostileProjectileWorld>() != null) return;
            var systems = GameObject.Find("Systems");
            if (systems == null) return;
            systems.AddComponent<HostileProjectileWorld>();
        }

        /// <summary>Same calls, same order as V3RunBootstrap.Update.</summary>
        public static void Tick(float dt, PlayerShipController player, System.Action<float> playerProjectiles = null)
        {
            var playerPos = player != null ? player.Position : Vector3.zero;
            Object.FindFirstObjectByType<BarnacleKingBossAttackDirector>()?.Tick(dt);
            Object.FindFirstObjectByType<EnemyWorld>()?.Tick(dt, playerPos, player?.Damage);
            // M5: V4 player projectiles tick here, where V3 ticked ProjectileWorld (which V4 no longer fills).
            if (playerProjectiles != null) playerProjectiles(dt);
            else Object.FindFirstObjectByType<ProjectileWorld>()?.Tick(dt, Object.FindFirstObjectByType<EnemyWorld>());
            Object.FindFirstObjectByType<HostileProjectileWorld>()?.Tick(dt, player?.Damage);
            Object.FindFirstObjectByType<PickupWorld>()?.Tick(dt, playerPos, RunManager.Instance.Stats.PickupRadius);
            V3FloorPowerupWorld.Instance?.Tick(dt, playerPos);
            Object.FindFirstObjectByType<StatusController>()?.Tick(dt, Object.FindFirstObjectByType<EnemyWorld>());
        }
    }

    /// <summary>Captains, items, powerups, hellfire patches and LevelSystem.</summary>
    [DeletedInPhase(9, "Build & Progression modules")]
    public sealed class LegacyBuildAdapter
    {
        public static void ResetPowerupsAndItems()
        {
            V3FloorPowerupWorld.Ensure();
            V3FloorPowerupWorld.Instance?.ResetForRun();
            V3RunItemInventory.Instance?.ClearForNewRun();
            V3RunItemEffects.Instance?.ResetForNewRun();
        }

        public static void EnsureCaptain(V3CaptainId captainId, PlayerShipController ship)
        {
            var systems = GameObject.Find("Systems");
            if (systems != null)
            {
                var controller = systems.GetComponent<V3CaptainController>();
                if (controller == null) controller = systems.AddComponent<V3CaptainController>();
                controller.Initialize(captainId, ship);
            }

            systems = GameObject.Find("Systems");
            if (systems != null)
            {
                var active = systems.GetComponent<V3CaptainActiveController>();
                if (active == null) active = systems.AddComponent<V3CaptainActiveController>();
                active.Initialize(captainId);
                HellfirePatchWorld.Ensure();
            }

            V3CaptainActiveController.Instance?.ResetForNewRun();
        }

        public static string ResolveStartingWeapon(V3CaptainProfile profile)
        {
            if (StageBalanceProfileContext.IsActive && !V3PlaytestFlags.SaltwindDebugUnlockAll)
                return "swivel_guns";
            if (!V3PlaytestFlags.UnlockAllContent && !PlayerPrefs.HasKey(V3ContentUnlocks.FirstConquestKey))
                return "swivel_guns";
            var starter = profile.StartingWeaponId;
            return string.IsNullOrEmpty(starter) ? "swivel_guns" : starter;
        }

        public static void ApplyStageBalanceProgression() => LevelSystem.Instance?.ApplyStageBalanceProgression();

        /// <summary>Ensure and configure LevelSystem. The weapon-record sync that follows is done by V4LegacyBridge.</summary>
        public static void PrepareLevelSystem(GameDatabase database, CombatExperimentProfile experiment, string startingWeapon)
        {
            if (LevelSystem.Instance == null)
            {
                var systems = GameObject.Find("Systems");
                if (systems != null && systems.GetComponent<LevelSystem>() == null)
                    systems.AddComponent<LevelSystem>();
            }

            LevelSystem.Instance?.ConfigureV3();
            LevelSystem.Instance?.ApplyCombatExperiment(experiment);
            LevelSystem.Instance?.PrepareForNewRun(database, startingWeapon);
        }

        public static void EnsureItemSystems()
        {
            V3RunItemInventory.Ensure();
            V3RunItemEffects.Ensure();
        }

        public static void CancelPendingLevelUps()
        {
            LevelSystem.Instance?.CancelPendingLevelUps();
            LevelUpModal.Instance?.Hide();
        }
    }

    /// <summary>POIManager, barrels, boss attack director and storm edge damage.</summary>
    [DeletedInPhase(7, "StageContent and BossDirector")]
    public sealed class LegacyStageAdapter
    {
        public static void InitializePoi(StageLayout layout, GameDatabase database, bool usePlaceholderVisuals, bool isPvPScene)
        {
            var poi = Object.FindFirstObjectByType<POIManager>();
            if (poi == null) return;
            if (isPvPScene) { poi.enabled = false; return; }
            poi.enabled = true;
            poi.SetV3Options(continueAfterBossVictory: true, usePlaceholderVisuals: usePlaceholderVisuals, useV3ChestPricing: true);
            poi.Initialize(layout, database);
            if (layout != null) V3ExplosiveBarrelWorld.Ensure(layout);
        }

        public static void ResetBossDirector(EnemyWorld enemyWorld, PlayerShipController ship) =>
            BarnacleKingBossAttackDirector.Ensure(enemyWorld, ship).ResetForRun();

        /// <summary>
        /// Module Lab: POIs, hold-to-interact and the B-key summon are off, so chests, the powder-cache ambush and the
        /// Beacon boss cannot spawn anything (a boss summon runs the spawner's trigger queue even while it is stopped).
        /// </summary>
        public static void EnterModuleLab()
        {
            foreach (var poi in Object.FindObjectsByType<POIManager>(FindObjectsSortMode.None)) poi.enabled = false;
            foreach (var interaction in Object.FindObjectsByType<V3InteractionController>(FindObjectsSortMode.None)) interaction.enabled = false;
            foreach (var hotkey in Object.FindObjectsByType<V3BossSummonHotkey>(FindObjectsSortMode.None)) hotkey.enabled = false;
        }

        public static void TrySecureIfBossDead() => Object.FindFirstObjectByType<POIManager>()?.TrySecureIfBossDead();
    }

    /// <summary>Marks tracking, settlement, telemetry, damage ledger and the death finalize path.</summary>
    [DeletedInPhase(12, "RewardModule")]
    public sealed class LegacyRewardsAdapter
    {
        public static void TeardownTransientRunState()
        {
            RunEncounterTracker.ResetForNewRun();
            RunMarksTracker.Reset();
            SaltwindRunSettlement.ClearLastResults();
            LegacyEnemyWorldPort.ClearTransientWorlds();
        }

        public static void ResetLedgers()
        {
            WeaponDamageLedger.Reset();
            WeaponEvolutionRuntime.Reset();
        }

        public static void CreateTelemetry(CombatExperimentProfile experiment) => _ = new RunCombatTelemetry(experiment);

        public static void BeginRunTracking()
        {
            SaltwindCombatEncounterBudget.ResetForRun();
            SaltwindCombatAgentStateStore.ClearAll();
            SaltwindHazardTelegraph.ClearAll();
            RunMarksTracker.BeginRun(RunManager.Instance.RunId);
            SaltwindRunSettlement.ClearLastResults();
            if (StageBalanceProfileContext.IsActive && StageBalanceProfileContext.Export != null)
            {
                var ex = StageBalanceProfileContext.Export;
                RunCombatTelemetry.Instance?.RecordStageProfileStart(ex.ProfileId, ex.BalanceVersion.ToString(), ex.ContentHash, RunManager.Instance.RunId);
            }
        }

        public static void EnsureTelemetryDriver()
        {
            var systems = GameObject.Find("Systems");
            if (systems == null) return;
            if (systems.GetComponent<CombatTelemetryDriver>() == null)
                systems.AddComponent<CombatTelemetryDriver>();
        }

        public static void ApplyConquestUnlockIfSecured()
        {
            if (RunManager.Instance != null && RunManager.Instance.ObjectiveSecured)
                PlayerPrefs.SetInt(V3ContentUnlocks.FirstConquestKey, 1);
        }

        public static void RecordDamage(float amount)
        {
            HighSeas.Meta.TriggerBus.Instance?.RaiseDamageTaken(amount);
            RunCombatTelemetry.Instance?.RecordDamageTaken(amount);
        }

        /// <summary>V3RunBootstrap.OnPlayerDied (solo branch; the PvP branch is kept for parity).</summary>
        public static void OnPlayerDied(PlayerShipController player)
        {
            LevelSystem.Instance?.CancelPendingLevelUps();
            if (player != null) player.SetMovementEnabled(false);
            if (V3RunSession.IsPvP)
            {
                RunManager.Instance.SetState(RunState.PlayerDead);
                RunManager.Instance.FinalizeRunOnDeath();
                V3ToastHud.Instance?.Show("You were sunk — the rival wins.", 4f);
                return;
            }

            LegacyStageAdapter.TrySecureIfBossDead();
            ApplyConquestUnlockIfSecured();
            RunManager.Instance.SetState(RunState.PlayerDead);
            RunManager.Instance.FinalizeRunOnDeath();
            V3OnboardingDirector.RecordRunCompleted();
            RunCombatTelemetry.Instance?.RecordPlayerDeath(RunManager.Instance.ObjectiveSecured);
            V3ToastHud.Instance?.Show("Run ended — gold banked.", 4f);
            if (StageBalanceProfileContext.IsActive && SaltwindRunSettlement.LastSettlementApplied)
                V3ToastHud.Instance?.Show($"Marks settled: +{SaltwindRunSettlement.LastMarksAwarded}", 4f);
        }
    }

    /// <summary>V3 UI bootstrap, VFX, playfeel, debug tools and toasts.</summary>
    [DeletedInPhase(14, "All presentation is V4 after switch-over")]
    public sealed class LegacyPresentationAdapter
    {
        static readonly System.Reflection.FieldInfo LevelUpPanelField =
            typeof(LevelUpModal).GetField("panel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        // Cached for the per-frame skip check (the modal only; its panel is rebuilt) (Unity's null check drops them when the scene unloads).
        static LevelUpModal _levelUpModal;
        static int _nextModalLookupFrame;

        /// <summary>
        /// V3 bug (register R-83): skipping the last pending level-up resumes the run but never hides the card panel,
        /// so the cards stay on screen (and 1/2/3 still pick from them). Every way V3 shows the cards also pauses the
        /// run for the level-up in the same call, so cards still showing while the run is unpaused with nothing
        /// pending are stale: hide them. Picking a card already hides them, so in practice only Skip is affected.
        /// </summary>
        public static void HideLevelUpCardsLeftAfterSkip()
        {
            // Cheap checks first, and the modal cached: LevelUpModal.Instance re-wires its layout on every access.
            var run = RunManager.Instance;
            if (run == null || run.IsPaused) return;
            if (LevelSystem.Instance != null && LevelSystem.Instance.PendingLevelUps > 0) return;
            if (_levelUpModal == null)
            {
                if (Time.frameCount < _nextModalLookupFrame) return;
                // The modal V3 shows (it may pick among several). V3 can replace it while building its UI, so a
                // destroyed one is looked up again at once; only a failed lookup waits 30 frames.
                _levelUpModal = LevelUpModal.Instance;
                if (_levelUpModal == null) { _nextModalLookupFrame = Time.frameCount + 30; return; }
            }
            // Read the panel each time: the modal rebuilds it (NeedsModernRebuild), so a cached panel goes stale.
            var panel = LevelUpPanelField?.GetValue(_levelUpModal) as GameObject;
            if (panel != null && panel.activeSelf) _levelUpModal.Hide();
        }

        public static void EnsureDevAndDebugTools(bool firstRun)
        {
            if (firstRun)
            {
                V3RunPlaytestHotkeys.Ensure();
                // SpawnDirectorDebugUI (F9) is not created from M3: it rewrote the legacy spawner's base rate every
                // frame (register R-71), and the legacy spawner no longer runs ambient spawns.
                if (V3PlaytestFlags.EnableRunPlaytestHotkeys) WeaponCardAnimationDemo.Ensure();
                V3BossSummonHotkey.Ensure();
            }

            EnemyIdentityDebugLabels.Ensure();
            // M6: WeaponTargetingVisualizer is replaced by the V4 aim overlay (same O/V/backslash toggle).
        }

        /// <summary>
        /// The run UI (M6). V3RunUiBootstrap.EnsureAll is no longer called: it adds V3AuthoredGameplayHud (which adds the
        /// bottom loadout HUD), a V3RunResultsScreen, the V3 minimap and beacon guide, and even a LevelSystem, which the
        /// V4 presenters and later phases replace. What it did for the kept legacy pieces is reproduced here in its
        /// order: event system, run canvas setup, legacy RunHud and level-up modal layout, discovery, toasts,
        /// interaction prompts, and hiding the placeholder duplicates. The V4 presenters are installed by the
        /// presentation root when the run starts.
        /// </summary>
        public static Canvas EnsureRunUi(StageLayout layout)
        {
            EnsureEventSystem();
            var canvas = FindRunCanvas();
            canvas.sortingOrder = 20;
            UiCanvasConfigurator.Configure(canvas);
            var legacyRunEnd = canvas.GetComponentInChildren<RunEndScreen>(true);
            if (legacyRunEnd != null) legacyRunEnd.enabled = false;
            EnsureLevelSystem();

            var hud = canvas.GetComponent<RunHud>();
            if (hud == null) hud = canvas.gameObject.AddComponent<RunHud>();
            hud.RebuildUiLayout();
            hud.ForceRefresh();
            V3PlayerInfoHudArtSettings.Reload();

            EnsureLevelUpModal(canvas);
            V3DiscoverySystem.Ensure(layout);
            V3ToastHud.Ensure(canvas.transform);
            V3InteractionController.Ensure(canvas.transform);

            if (HasAuthoredHudCanvas())
            {
                // V3 hid these placeholder duplicates once the authored HUD was found (the V4 HUD presenter binds it now).
                HidePlaceholders<V3ObjectiveHud>(canvas.transform);
                HidePlaceholders<V3RunLoadoutHud>(canvas.transform);
                HidePlaceholders<V3RunCaptainActiveHud>(canvas.transform);
                HidePlaceholders<V3RunStatsHud>(canvas.transform);
                HidePlaceholders<V3RunTimerHud>(canvas.transform);
                HidePlaceholders<V3PlayerInfoHud>(canvas.transform);
            }
            else
            {
                // No authored HUD canvas: V3's fallback HUDs, as EnsureAll did (no V4 scene is built this way today).
                V3ObjectiveHud.Ensure(canvas.transform);
                V3RunLoadoutHud.Ensure(canvas.transform);
                V3RunCaptainActiveHud.Ensure(canvas.transform);
                V3RunStatsHud.Ensure(canvas.transform);
                V3RunTimerHud.Ensure(canvas.transform);
                V3PlayerInfoHud.Ensure(canvas.transform);
            }

            var hudRoot = canvas.transform.Find(RunHudUiFactory.RootName);
            var left = hudRoot != null ? hudRoot.Find("LeftHudPanel") : null;
            if (left != null) left.gameObject.SetActive(false);
            foreach (var strip in canvas.GetComponentsInChildren<WeaponLevelStripHud>(true)) strip.gameObject.SetActive(false);

            hud.ForceRefresh();
            return canvas;
        }

        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        /// <summary>V3RunUiBootstrap.EnsureRunCanvas: /RunCanvas, else the RunHud's canvas, else any canvas, else a new one.</summary>
        public static Canvas FindRunCanvas()
        {
            var runCanvasGo = GameObject.Find("RunCanvas");
            if (runCanvasGo != null && runCanvasGo.TryGetComponent(out Canvas runCanvas)) return runCanvas;
            var runHud = Object.FindFirstObjectByType<RunHud>();
            var hudCanvas = runHud != null ? runHud.GetComponentInParent<Canvas>() : null;
            if (hudCanvas != null) return hudCanvas;
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null) return canvas;
            var go = new GameObject("RunCanvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            return canvas;
        }

        /// <summary>V3 added a LevelSystem to /Systems when none existed (the scene has one); dropped when M9 replaces it.</summary>
        static void EnsureLevelSystem()
        {
            if (LevelSystem.Instance != null) return;
            var systems = GameObject.Find("Systems");
            if (systems != null && systems.GetComponent<LevelSystem>() == null) systems.AddComponent<LevelSystem>();
        }

        static void EnsureLevelUpModal(Canvas canvas)
        {
            var modal = canvas.GetComponentInChildren<LevelUpModal>(true);
            if (modal == null)
            {
                var panel = new GameObject("LevelUpPanel", typeof(RectTransform));
                panel.transform.SetParent(canvas.transform, false);
                var rt = panel.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                modal = panel.AddComponent<LevelUpModal>();
            }
            modal.RebuildUiLayout();
            modal.gameObject.SetActive(false);
        }

        /// <summary>V3AuthoredGameplayHud.EnsureInScene's test: a canvas outside the legacy RunHud with Weapon_Slot and Profile.</summary>
        static bool HasAuthoredHudCanvas()
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas.GetComponentInParent<RunHud>() != null) continue;
                if (canvas.transform.Find("Weapon_Slot") != null && canvas.transform.Find("Profile") != null) return true;
            }
            return false;
        }

        static void HidePlaceholders<T>(Transform canvas) where T : Component
        {
            foreach (var component in canvas.GetComponentsInChildren<T>(true))
                if (component.gameObject.name.StartsWith("PLACEHOLDER_")) component.gameObject.SetActive(false);
        }

        public static void EnsurePlayerHealthBar(PlayerShipController ship)
        {
            if (ship == null) return;
            if (ship.GetComponent<PlayerHealthBarBinder>() == null)
                ship.gameObject.AddComponent<PlayerHealthBarBinder>();
        }

        public static void EnsureCombatVfx()
        {
            var systems = GameObject.Find("Systems");
            if (systems == null) return;
            if (systems.GetComponent<JuiceVfxBroker>() == null) systems.AddComponent<JuiceVfxBroker>();
            if (systems.GetComponent<CombatVfxController>() == null) systems.AddComponent<CombatVfxController>();
            var damageNumbers = systems.GetComponent<FloatingDamageNumbers>();
            if (damageNumbers == null) damageNumbers = systems.AddComponent<FloatingDamageNumbers>();
            damageNumbers.Rebind();
            if (systems.GetComponent<WeaponJuiceController>() == null) systems.AddComponent<WeaponJuiceController>();
            if (systems.GetComponent<PlayerJuiceController>() == null) systems.AddComponent<PlayerJuiceController>();

            var vfxRoot = systems.transform.Find("VFXRoot");
            if (vfxRoot == null)
            {
                var go = new GameObject("VFXRoot");
                go.transform.SetParent(systems.transform, false);
                vfxRoot = go.transform;
            }

            var settings = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
            var broker = systems.GetComponent<JuiceVfxBroker>();
            if (broker != null) broker.Configure(settings, vfxRoot);
            var juice = systems.GetComponent<WeaponJuiceController>();
            if (juice != null)
            {
                juice.Configure(settings, vfxRoot);
                // M6: the V4 weapon presenter draws the weapon effects. The juice controller stays (disabled) only because
                // HellfirePatchWorld and SaltwindCombatEffects still call its burning-oil and blast effects (M8); its
                // Update drew orbit blades and the flame mount from the loadout record, which V4 now draws.
                juice.enabled = false;
            }
            if (systems.GetComponent<CameraShakeService>() == null) systems.AddComponent<CameraShakeService>();
            if (systems.GetComponent<SfxService>() == null) systems.AddComponent<SfxService>();
            // M6: V3SpawnWarningHud is not added; the spawn warning presenter shows those toasts.
            LegacyHazardTelegraphView.Ensure(); // V4 addition: V3 never draws pending ground hazards (Kraken traps)
        }

        public static void EnsurePlayfeelSystems(PlayerShipController ship, int seed)
        {
            V3OnboardingDirector.Ensure()?.ResetForRun();
            LegacyWorldAdapter.DisableEnvironmentalHazards();

            var systems = GameObject.Find("Systems");
            if (systems != null)
            {
                var dayNight = systems.GetComponent<DayNightDirector>();
                if (dayNight == null) dayNight = systems.AddComponent<DayNightDirector>();
                dayNight.BeginRun(seed);
            }

            if (ship == null) return;
            var boost = ship.GetComponent<HighSeas.PlayerSystems.ShipBoostController>();
            if (boost == null) boost = ship.gameObject.AddComponent<HighSeas.PlayerSystems.ShipBoostController>();
            boost.Bind(ship);
            if (ship.GetComponent<HighSeas.PlayerSystems.BoostCameraZoom>() == null)
                ship.gameObject.AddComponent<HighSeas.PlayerSystems.BoostCameraZoom>();
        }

        public static void ShowStartToasts(bool firstRun, int seed)
        {
            if (firstRun && V3PlaytestFlags.EnableRunPlaytestHotkeys)
                V3ToastHud.Instance?.Show("Playtest: F8 hotkeys · G weapon card scatter · H card scroll", 5f);

            var seedLabel = firstRun && V3HomeSeed.HasLockedHomeSeed ? "locked" : "random";
            V3ToastHud.Instance?.Show(
                firstRun
                    ? $"Saltwind voyage ({seedLabel} seed {seed}).\nFind salvage chests as you bank gold, secure the Beacon."
                    : $"New voyage (seed {seed}).\nIslands, chests, and spawn rerolled.",
                firstRun ? 6f : 5f);
        }
    }
}
