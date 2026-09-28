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
    [DeletedInPhase(15, "The V4 enemy world and card texts still read the shared GameDatabase definition types (moved in M15)")]
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


    /// <summary>
    /// Run-state statics, telemetry, the damage ledger and the death flow. M12: no Marks — Rum is the V4 reward module's,
    /// settled at death between PlayerDead and RunComplete (never RunManager.FinalizeRunOnDeath, which banked Doubloons).
    /// </summary>
    [DeletedInPhase(14, "V4 run lifecycle and telemetry")]
    public sealed class LegacyRunLifecycle
    {
        public static void TeardownTransientRunState(System.Action clearTransientWorlds)
        {
            RunEncounterTracker.ResetForNewRun();
            RunMarksTracker.Reset();
            SaltwindRunSettlement.ClearLastResults();
            clearTransientWorlds?.Invoke();
        }

        public static void ResetLedgers()
        {
            WeaponDamageLedger.Reset();
            // (V3 also cleared WeaponEvolutionRuntime here; since M9 the evolutions are the V4 progression module's, reset per run.)
        }

        public static void CreateTelemetry(CombatExperimentProfile experiment) => _ = new RunCombatTelemetry(experiment);

        public static void BeginRunTracking()
        {
            SaltwindCombatEncounterBudget.ResetForRun();
            SaltwindCombatAgentStateStore.ClearAll();
            SaltwindHazardTelegraph.ClearAll();
            // M12: the V3 Marks tracker is never started (it stays inert); Rum is the V4 reward module's.
            SaltwindRunSettlement.ClearLastResults();
            if (StageBalanceProfileContext.IsActive && StageBalanceProfileContext.Export != null)
            {
                var ex = StageBalanceProfileContext.Export;
                RunCombatTelemetry.Instance?.RecordStageProfileStart(ex.ProfileId, ex.BalanceVersion.ToString(), ex.ContentHash, RunManager.Instance.RunId);
            }
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

        /// <summary>V3RunBootstrap.OnPlayerDied (solo branch; the PvP branch is kept for parity). M12: Rum is settled by
        /// <paramref name="rewards"/> between PlayerDead and RunComplete, as V3 settled Marks inside FinalizeRunOnDeath.</summary>
        public static void OnPlayerDied(PlayerShipController player, System.Action trySecureIfBossDead, System.Action cancelPendingLevelUps,
            RoguePirates.V4.Rewards.RewardModule rewards)
        {
            cancelPendingLevelUps?.Invoke();
            if (player != null) player.SetMovementEnabled(false);
            if (V3RunSession.IsPvP)
            {
                RunManager.Instance.SetState(RunState.PlayerDead);
                RunManager.Instance.FinalizeRunOnDeath();
                V3ToastHud.Instance?.Show("You were sunk — the rival wins.", 4f);
                return;
            }

            trySecureIfBossDead?.Invoke();
            ApplyConquestUnlockIfSecured();
            var run = RunManager.Instance;
            if (run.State == RunState.RunComplete) return; // V3: FinalizeRunOnDeath ran once
            run.SetState(RunState.PlayerDead);
            var rum = rewards != null ? rewards.Settle(run.RunId, true) : 0;
            if (rewards != null && rewards.SettlementApplied)
                RunCombatTelemetry.Instance?.RecordMarksSettlement(rum, true, rum,
                    rewards.SettledRumOf(RoguePirates.V4.Config.RumSource.QualifyingMinute), rewards.SettledRumOf(RoguePirates.V4.Config.RumSource.SpecialLandmark),
                    rewards.SettledRumOf(RoguePirates.V4.Config.RumSource.ScheduledElite), rewards.SettledRumOf(RoguePirates.V4.Config.RumSource.BossVictory),
                    rewards.SettledRumOf(RoguePirates.V4.Config.RumSource.OvertimeMinute));
            run.SetState(RunState.RunComplete); // gold is run-only: nothing is banked (R-59)
            V3OnboardingDirector.RecordRunCompleted();
            RunCombatTelemetry.Instance?.RecordPlayerDeath(run.ObjectiveSecured);
            V3ToastHud.Instance?.Show("Run ended.", 4f);
            if (rewards != null && rewards.SettlementApplied)
                V3ToastHud.Instance?.Show($"{rewards.CurrencyName} settled: +{rum}", 4f);
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
        public static void HideLevelUpCardsLeftAfterSkip(int pendingLevelUps)
        {
            // Cheap checks first, and the modal cached: LevelUpModal.Instance re-wires its layout on every access.
            var run = RunManager.Instance;
            if (run == null || run.IsPaused) return;
            if (pendingLevelUps > 0) return;
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
                // M7: the B-key boss summon is a dev tool (Tuning Console, dev builds only; register R-55).
            }

            // M8: EnemyIdentityDebugLabels read V3's EnemyWorld, which left the V4 scenes (debug labels: a DevTools port later).
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
        public static Canvas EnsureRunUi(StageLayout layout, LegacyStageWiring stage)
        {
            EnsureEventSystem();
            var canvas = FindRunCanvas();
            canvas.sortingOrder = 20;
            UiCanvasConfigurator.Configure(canvas);
            var legacyRunEnd = canvas.GetComponentInChildren<RunEndScreen>(true);
            if (legacyRunEnd != null) legacyRunEnd.enabled = false;

            var hud = canvas.GetComponent<RunHud>();
            if (hud == null) hud = canvas.gameObject.AddComponent<RunHud>();
            hud.RebuildUiLayout();
            hud.ForceRefresh();
            V3PlayerInfoHudArtSettings.Reload();

            EnsureLevelUpModal(canvas);
            stage?.EnsureDiscovery(layout);        // M7: V4 discovery in V3DiscoverySystem's place
            V3ToastHud.Ensure(canvas.transform);
            stage?.EnsureInteraction(canvas.transform); // M7: V4 interaction in V3InteractionController's place

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
                // SaltwindCombatEffects (the enemy world's hazard and blast effects) still calls its blast effect; the
                // Hellfire patches no longer play its broken burning oil (R-104, M8 playtest). Its Update drew orbit
                // blades and the flame mount from the loadout record, which V4 now draws.
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
