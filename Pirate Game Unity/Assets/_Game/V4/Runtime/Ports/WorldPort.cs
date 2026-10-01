using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Foundation.Progression;
using RoguePirates.V4.Foundation.World;
using UnityEngine;
using RoguePirates.V4.Composition;

namespace RoguePirates.V4.Ports
{
    /// <summary>
    /// The world as the shared V3 code builds it (M14.14; was LegacyWorldAdapter plus the world and playfeel pieces of
    /// LegacyPresentationAdapter and RunBootstrapSequence): the stage builder and its island layout, the player's safe spawn,
    /// the bounds' storm region and edge rings, V3's environmental hazards switched off, the run context shared V3 code
    /// reads, the home seed, and the ship's boost, camera zoom and day-night components. All of it is shared V3
    /// infrastructure the plan keeps until M15.
    /// </summary>
    public sealed class WorldPort
    {
        readonly StageBuilder _stageBuilder;
        readonly IslandLayoutDefinition _islandLayout;

        public WorldPort(StageBuilder stageBuilder, IslandLayoutDefinition islandLayout)
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
            // Demo: the touch stick only in the mobile app (it is authored inactive, so Find cannot see it: search the scene).
            var joystick = FindInactive("JoystickBG");
            var wanted = RoguePirates.V4.Foundation.Platform.VirtualJoystick.TouchControlsWanted;
            if (joystick != null && joystick.activeSelf != wanted) joystick.SetActive(wanted);
            var systems = GameObject.Find("Systems");
            if (systems != null && systems.GetComponent<PlayerShipVisualBootstrap>() == null)
                systems.AddComponent<PlayerShipVisualBootstrap>();
        }

        static GameObject FindInactive(string name)
        {
            foreach (var t in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == name) return t.gameObject;
            return null;
        }

        /// <summary>V3 turned off its wind, currents and whirlpools for Saltwind (M15: that code is gone; a leftover
        /// hazard root in a scene is still removed).</summary>
        public static void DisableEnvironmentalHazards()
        {
            var root = GameObject.Find("EnvironmentHazards");
            if (root != null) Object.Destroy(root);
        }

        /// <summary>
        /// Seed for the run, as V3 resolves it: explicit request, else a random seed on Retry (V3RunBootstrap.RestartRun),
        /// else the locked home seed / random first-run seed (HomeSeed).
        /// </summary>
        public static int ResolveSeed(int requestedSeed, bool retry) =>
            requestedSeed != 0 ? requestedSeed : retry ? Random.Range(1, int.MaxValue) : HomeSeed.ResolveRunSeed();

        public static bool HomeSeedLocked => HomeSeed.HasLockedHomeSeed;
        public static void LockHomeSeed(int runSeed) => HomeSeed.TryLockFromRun(runSeed);

        /// <summary>V3RunBootstrap.RestartRun's side effects before the retry reload.</summary>
        public static void NotifyRetryStarting()
        {
            RoguePirates.V4.Foundation.Balance.SaltwindRunResetLogic.NotifyRetryStarting();
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
            _stageBuilder.ConfigureSaltwindLayout(ResolveIslandLayout());
            _stageBuilder.SetProceduralIslandSettings(ProceduralIslandSettings.Default.Clamped());
            _stageBuilder.SetIslandDensityMultiplier(1f);
        }

        // V5 M3e: the islands were static-batched here once; measured on the same map it saved ~40-55 of ~500 batches for
        // +25 MB of combined meshes (413k vertices) on a build already over its memory budget, so it was taken out
        // (plan/v5/phase-03e-performance/PLAN.md, D25); the SRP Batcher batches the islands.
        public StageLayout Build(int seed) => _stageBuilder != null ? _stageBuilder.Build(seed, 0) : null;

        /// <summary>The storm region reset, hazards off (the storm ring and walls are V4 presenters).</summary>
        public static void ResetWorldVisuals()
        {
            PlayBounds.ResetStormSafeRegion();
            DisableEnvironmentalHazards();
        }

        public static void MarkRunContextActive() => ActiveRun.IsActive = true;

        public void PlacePlayerAtSafeSpawn(PlayerShipController ship, StageLayout layout, int seed)
        {
            const float hullRadius = 1.15f;
            Physics.SyncTransforms();
            var spawn = SpawnAnchorValidator.ResolveRuntimeSpawn(layout, ResolveIslandLayout(), seed, hullRadius);
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
            var boost = ship.GetComponent<RoguePirates.V4.Foundation.PlayerSystems.ShipBoostController>();
            if (boost == null) boost = ship.gameObject.AddComponent<RoguePirates.V4.Foundation.PlayerSystems.ShipBoostController>();
            boost.Bind(ship);
            if (ship.GetComponent<RoguePirates.V4.Foundation.PlayerSystems.BoostCameraZoom>() == null)
                ship.gameObject.AddComponent<RoguePirates.V4.Foundation.PlayerSystems.BoostCameraZoom>();
        }
    }

    /// <summary>
    /// V3's content unlock state and playtest flags as V4 reads them (M14.14): the First Conquest record, weapon unlocks,
    /// the saved Captain, the build's playtest switches and the menu's app/experiment setup. Shared state kept until M15.
    /// </summary>
    public static class ContentPort
    {
        public static bool FirstConquestDone => ContentUnlocks.HasCompletedFirstConquest;
        /// <summary>The First Conquest key is saved (V3 ResolveStartingWeapon's test), or V3's unlock-all flag is on.</summary>
        public static bool ConqueredForStartingWeapon => PlaytestFlags.UnlockAllContent || PlayerPrefs.HasKey(ContentUnlocks.FirstConquestKey);
        public static bool SaltwindDebugUnlockAll => PlaytestFlags.SaltwindDebugUnlockAll;
        public static bool IsWeaponUnlocked(string weaponId) => ContentUnlocks.IsWeaponUnlocked(weaponId);
        /// <summary>V3 LevelOfferGenerator.IsWeaponOfferEligible: the weapon's feature flag and, in V3-context runs, its unlock.</summary>
        public static bool IsWeaponOfferEligible(string weaponId) =>
            RoguePirates.V4.Foundation.Combat.WeaponFeatureFlags.IsWeaponEnabled(weaponId) && (!RunContextActive || ContentUnlocks.IsWeaponUnlocked(weaponId));
        /// <summary>A run is active (the Foundation flag, V3RunContext.IsActive until M15). V3's rule: in runs a weapon offer
        /// also needs its unlock.</summary>
        public static bool RunContextActive => ActiveRun.IsActive;
        public static bool PlaytestHotkeys => PlaytestFlags.EnableRunPlaytestHotkeys;
        /// <summary>V3's playtest switch that opens every Captain.</summary>
        public static bool BypassProgressionGates => PlaytestFlags.BypassProgressionGates;

        /// <summary>The Captain saved by the menu (V3's selection PlayerPref), by V3 id.</summary>
        public static int SelectedCaptainId
        {
            get => (int)CaptainSelection.Selected;
            set => CaptainSelection.Selected = (CaptainId)value;
        }

        /// <summary>V3MainMenuController.Start: leave any run context, initialize the app, the menu's combat experiment.</summary>
        public static void PrepareMenu()
        {
            AppRoot.EndActiveSaltwindRunContext();
            AppRoot.EnsureInitialized();
            RoguePirates.V4.Foundation.Combat.CombatExperimentContext.SetActiveProfileId(RoguePirates.V4.Foundation.Combat.CombatExperimentContext.SelectedProfileId);
        }

        /// <summary>V3's Set Sail: the full combat experiment for the run.</summary>
        public static void BeginSaltwindExperiment() =>
            RoguePirates.V4.Foundation.Combat.CombatExperimentContext.SetActiveProfileId(RoguePirates.V4.Foundation.Combat.CombatExperimentProfile.FullCombatV2Id);
    }
}
