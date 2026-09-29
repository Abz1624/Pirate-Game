using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Player;
using HighSeas.V3;
using HighSeas.World;
using UnityEngine;
using RoguePirates.V4.Composition;

namespace RoguePirates.V4.Legacy
{
    /// <summary>
    /// The world as the shared V3 code builds it (M14.14; was LegacyWorldAdapter plus the world and playfeel pieces of
    /// LegacyPresentationAdapter and RunBootstrapSequence): the stage builder and its island layout, the player's safe spawn,
    /// the bounds' storm region and edge rings, V3's environmental hazards switched off, the run context shared V3 code
    /// reads, the home seed, and the ship's boost, camera zoom and day-night components. All of it is shared V3
    /// infrastructure the plan keeps until M15.
    /// </summary>
    [DeletedInPhase(15, "StageBuilder, the island layout, PlayBounds, the run context, the home seed and the ship's playfeel components are shared infrastructure until M15 (plan §4.2)")]
    public sealed class LegacyWorldPort
    {
        readonly StageBuilder _stageBuilder;
        readonly IslandLayoutDefinition _islandLayout;

        public LegacyWorldPort(StageBuilder stageBuilder, IslandLayoutDefinition islandLayout)
        {
            _stageBuilder = stageBuilder != null ? stageBuilder : Object.FindFirstObjectByType<StageBuilder>();
            _islandLayout = islandLayout;
        }

        public StageBuilder StageBuilder => _stageBuilder;

        // ---- run start ----------------------------------------------------------------------------------------------

        /// <summary>V3RunBootstrap.Awake's world half: hazards off, the touch joystick on, the ship visual bootstrap.</summary>
        public static void PrepareScene()
        {
            DisableEnvironmentalHazards();
            var joystick = GameObject.Find("JoystickBG");
            if (joystick != null && !joystick.activeSelf) joystick.SetActive(true);
            var systems = GameObject.Find("Systems");
            if (systems != null && systems.GetComponent<PlayerShipVisualBootstrap>() == null)
                systems.AddComponent<PlayerShipVisualBootstrap>();
        }

        /// <summary>V3 turned off its wind, currents, whirlpools and their visuals for Saltwind.</summary>
        public static void DisableEnvironmentalHazards()
        {
            EnvironmentalHazardsDisabled.Set(true);
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

        /// <summary>
        /// Seed for the run, as V3 resolves it: explicit request, else a random seed on Retry (V3RunBootstrap.RestartRun),
        /// else the locked home seed / random first-run seed (V3HomeSeed).
        /// </summary>
        public static int ResolveSeed(int requestedSeed, bool retry) =>
            requestedSeed != 0 ? requestedSeed : retry ? Random.Range(1, int.MaxValue) : V3HomeSeed.ResolveRunSeed();

        public static bool HomeSeedLocked => V3HomeSeed.HasLockedHomeSeed;
        public static void LockHomeSeed(int runSeed) => V3HomeSeed.TryLockFromRun(runSeed);

        /// <summary>V3RunBootstrap.RestartRun's side effects before the retry reload.</summary>
        public static void NotifyRetryStarting()
        {
            HighSeas.Balance.SaltwindRunResetLogic.NotifyRetryStarting();
            HighSeas.Save.RunSnapshotService.Clear();
        }

        // ---- the stage ----------------------------------------------------------------------------------------------

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

        /// <summary>The storm region reset, V3's edge rings off, hazards off (the storm ring and walls are V4 presenters).</summary>
        public static void ResetWorldVisuals()
        {
            V3StormEdgeVisual.DisableWorldRings();
            PlayBounds.ResetStormSafeRegion();
            DisableEnvironmentalHazards();
        }

        /// <summary>The run context shared V3 code reads (the layout for POI placement, the Captain for the HUD art).</summary>
        public static void SetRunContext(StageLayout layout)
        {
            V3RunContext.Layout = layout;
            V3RunContext.CaptainId = (V3CaptainId)RoguePirates.V4.Progression.CaptainChoice.LegacyId;
        }

        public static void MarkRunContextActive() => V3RunContext.IsActive = true;

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

        /// <summary>V3's barrel markers (the parity preset only: the V4 enemy world runs the barrels, V3's update stays off).</summary>
        public static void EnsureBarrelMarkers(StageLayout layout)
        {
            var legacy = V3ExplosiveBarrelWorld.Ensure(layout);
            if (legacy != null) legacy.enabled = false;
        }

        // ---- playfeel -----------------------------------------------------------------------------------------------

        /// <summary>The day-night cycle for the run's seed, and the ship's boost and boost camera zoom.</summary>
        public static void EnsurePlayfeel(PlayerShipController ship, int seed)
        {
            DisableEnvironmentalHazards();
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
    }

    /// <summary>
    /// V3's content unlock state and playtest flags as V4 reads them (M14.14): the First Conquest record, weapon unlocks,
    /// the saved Captain, the build's playtest switches and the menu's app/experiment setup. Shared state kept until M15.
    /// </summary>
    [DeletedInPhase(15, "V3ContentUnlocks, V3PlaytestFlags, V3CaptainSelection and CombatExperimentContext are shared progression state until M15")]
    public static class LegacyContentPort
    {
        public static bool FirstConquestDone => V3ContentUnlocks.HasCompletedFirstConquest;
        /// <summary>The First Conquest key is saved (V3 ResolveStartingWeapon's test), or V3's unlock-all flag is on.</summary>
        public static bool ConqueredForStartingWeapon => V3PlaytestFlags.UnlockAllContent || PlayerPrefs.HasKey(V3ContentUnlocks.FirstConquestKey);
        public static bool SaltwindDebugUnlockAll => V3PlaytestFlags.SaltwindDebugUnlockAll;
        public static bool IsWeaponUnlocked(string weaponId) => V3ContentUnlocks.IsWeaponUnlocked(weaponId);
        /// <summary>V3 LevelOfferGenerator.IsWeaponOfferEligible: the weapon's feature flag and, in V3-context runs, its unlock.</summary>
        public static bool IsWeaponOfferEligible(string weaponId) =>
            HighSeas.Combat.WeaponFeatureFlags.IsWeaponEnabled(weaponId) && (!V3RunContext.IsActive || V3ContentUnlocks.IsWeaponUnlocked(weaponId));
        /// <summary>V3's rule: in V3 runs a weapon offer also needs its unlock.</summary>
        public static bool RunContextActive => V3RunContext.IsActive;
        public static bool PlaytestHotkeys => V3PlaytestFlags.EnableRunPlaytestHotkeys;
        /// <summary>V3's playtest switch that opens every Captain.</summary>
        public static bool BypassProgressionGates => V3PlaytestFlags.BypassProgressionGates;

        /// <summary>The Captain saved by the menu (V3's selection PlayerPref), by V3 id.</summary>
        public static int SelectedCaptainId
        {
            get => (int)V3CaptainSelection.Selected;
            set => V3CaptainSelection.Selected = (V3CaptainId)value;
        }

        /// <summary>V3MainMenuController.Start: leave any run context, initialize the app, the menu's combat experiment.</summary>
        public static void PrepareMenu()
        {
            AppRoot.EndActiveSaltwindRunContext();
            AppRoot.EnsureInitialized();
            HighSeas.Combat.CombatExperimentContext.SetActiveProfileId(HighSeas.Combat.CombatExperimentContext.SelectedProfileId);
        }

        /// <summary>V3's Set Sail: the full combat experiment for the run.</summary>
        public static void BeginSaltwindExperiment() =>
            HighSeas.Combat.CombatExperimentContext.SetActiveProfileId(HighSeas.Combat.CombatExperimentProfile.FullCombatV2Id);
    }
}
