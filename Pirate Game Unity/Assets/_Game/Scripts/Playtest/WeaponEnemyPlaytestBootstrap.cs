using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.Meta;
using HighSeas.Pickups;
using HighSeas.Player;
using HighSeas.UI;
using HighSeas.POI;
using HighSeas.V3;
using HighSeas.V3.MultiShip;
using HighSeas.Weapons;
using HighSeas.World;
using UnityEngine;

namespace HighSeas.Playtest
{
    /// <summary>
    /// Lightweight V3-style run boot for Test_WeaponEnemy (no Saltwind profile / POI pressure).
    /// Disables legacy <see cref="RunBootstrap"/> and <see cref="V3RunBootstrap"/> on the same object.
    /// </summary>
    [DefaultExecutionOrder(-110)]
    public sealed class WeaponEnemyPlaytestBootstrap : MonoBehaviour
    {
        [SerializeField] GameDatabase database;
        [SerializeField] StageBuilder stageBuilder;
        [SerializeField] PlayerShipController player;
        [SerializeField] IslandLayoutDefinition islandLayout;
        [SerializeField] int runSeed = 12345;

        StageLayout _layout;

        void Awake()
        {
            WeaponEnemyPlaytestSession.Active = true;
            V3RunContext.IsActive = true;
            AppRoot.EnsureInitialized();

            foreach (var legacy in FindObjectsByType<RunBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                legacy.enabled = false;
            foreach (var v3 in FindObjectsByType<V3RunBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                v3.enabled = false;

            if (database == null || !GameDatabaseLoader.IsValid(database))
                database = GameDatabaseLoader.Load();

            var systems = GameObject.Find("Systems");
            if (systems != null && systems.GetComponent<PlayerShipVisualBootstrap>() == null)
                systems.AddComponent<PlayerShipVisualBootstrap>();

            var joystick = GameObject.Find("JoystickBG");
            if (joystick != null && !joystick.activeSelf)
                joystick.SetActive(true);

            var poi = FindFirstObjectByType<POIManager>();
            if (poi != null)
                poi.enabled = false;

            DisableAutomaticEnemySpawning();
        }

        static void DisableAutomaticEnemySpawning()
        {
            foreach (var spawner in FindObjectsByType<EncounterSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                spawner.EnterManualPlaytestMode();

            foreach (var balance in FindObjectsByType<StageBalanceProfileHost>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                balance.enabled = false;
        }

        void OnDestroy()
        {
            if (player != null && player.Damage != null)
                player.Damage.SetPlaytestInvulnerable(false);
            WeaponEnemyPlaytestSession.Active = false;
        }

        void Start()
        {
            if (database == null)
            {
                Debug.LogError("WeaponEnemyPlaytestBootstrap: GameDatabase missing.");
                return;
            }

            if (RunManager.Instance == null)
                gameObject.AddComponent<RunManager>();

            if (stageBuilder == null)
                stageBuilder = FindFirstObjectByType<StageBuilder>();

            ConfigureStageBuilder();

            var experimentProfile = CombatExperimentContext.Active;
            CombatWeaponCatalog.ApplyToDatabase(database, experimentProfile);
            CombatSpecialistEnemyCatalog.ApplyToDatabase(database, experimentProfile);
            WeaponDamageLedger.Reset();
            WeaponEvolutionRuntime.Reset();

            RunManager.Instance.BeginRun(runSeed != 0 ? runSeed : GameConstants.DemoRunSeed);
            _layout = stageBuilder != null ? stageBuilder.Build(runSeed, 0) : null;
            V3RunContext.Layout = _layout;

            FindFirstObjectByType<EnemyWorld>()?.ResetAllAgentsForPromo();
            FindFirstObjectByType<PickupWorld>()?.ClearAllActive();
            FindFirstObjectByType<ProjectileWorld>()?.ClearAllActive();
            FindFirstObjectByType<HostileProjectileWorld>()?.ClearAllActive();

            var enemyWorld = FindFirstObjectByType<EnemyWorld>();
            enemyWorld?.SetDefinitions(database.enemies);
            enemyWorld?.SetCollideWithIslands(false);

            if (player == null)
                player = FindFirstObjectByType<PlayerShipController>();

            if (player != null && _layout != null)
            {
                player.SetModelForwardIsNegativeZ(false);
                var layoutDef = ResolveIslandLayout();
                PlacePlayerAtSafeSpawn(player, layoutDef, runSeed);
                player.SetMovementEnabled(true);
            }

            var weapons = V3MultiShipWeaponLoadouts.FindLocalHumanWeaponController()
                ?? FindFirstObjectByType<WeaponController>();
            if (player != null)
                weapons?.BindOwner(player);
            weapons?.ApplyCombatExperiment(experimentProfile);
            weapons?.Initialize(database, "swivel_guns");

            EnsureLevelSystem();
            LevelSystem.Instance?.ConfigureV3();
            LevelSystem.Instance?.ApplyCombatExperiment(experimentProfile);
            LevelSystem.Instance?.PrepareForNewRun(database, "swivel_guns");
            LevelSystem.Instance?.SyncWeaponControllerFromOwnedLoadout(weapons);

            var spawner = FindFirstObjectByType<EncounterSpawner>();
            spawner?.EnterManualPlaytestMode();

            if (FindFirstObjectByType<HostileProjectileWorld>() == null)
            {
                var systems = GameObject.Find("Systems");
                systems?.AddComponent<HostileProjectileWorld>();
            }

            if (player != null)
            {
                player.InitializeFromRun(RunManager.Instance.Stats.MaxHull);
                player.Damage?.SetPlaytestInvulnerable(true);
            }

            RunManager.Instance.EnterPlaying();

            WeaponTargetingVisualizer.Ensure();
        }

        void Update()
        {
            if (!WeaponEnemyPlaytestSession.Active) return;
            if (RunManager.Instance == null || RunManager.Instance.IsPaused) return;
            if (RunManager.Instance.IsRunEnded) return;

            var dt = Time.deltaTime;
            var playerPos = player != null ? player.Position : Vector3.zero;
            FindFirstObjectByType<EnemyWorld>()?.Tick(dt, playerPos, player?.Damage);
            FindFirstObjectByType<ProjectileWorld>()?.Tick(dt, FindFirstObjectByType<EnemyWorld>());
            FindFirstObjectByType<HostileProjectileWorld>()?.Tick(dt, player?.Damage);
            FindFirstObjectByType<PickupWorld>()?.Tick(dt, playerPos, RunManager.Instance.Stats.PickupRadius);
            FindFirstObjectByType<StatusController>()?.Tick(dt, FindFirstObjectByType<EnemyWorld>());
        }

        static void EnsureLevelSystem()
        {
            if (LevelSystem.Instance != null) return;
            var systems = GameObject.Find("Systems");
            if (systems == null) return;
            if (systems.GetComponent<LevelSystem>() == null)
                systems.AddComponent<LevelSystem>();
        }

        void ConfigureStageBuilder()
        {
            if (stageBuilder == null) return;
            stageBuilder.ConfigureV3Layout(ResolveIslandLayout());
            var islandSettings = ProceduralIslandSettings.Default.Clamped();
            stageBuilder.SetProceduralIslandSettings(islandSettings);
            stageBuilder.SetIslandDensityMultiplier(1f);
        }

        IslandLayoutDefinition ResolveIslandLayout()
        {
            if (islandLayout != null && islandLayout.HasIslands)
                return islandLayout;

            var fromResources = Resources.Load<IslandLayoutDefinition>("V3SaltwindIslandLayout");
            if (fromResources != null && fromResources.HasIslands)
                return fromResources;

            return IslandLayoutDefinition.LoadAssignedOrDefault(null);
        }

        void PlacePlayerAtSafeSpawn(PlayerShipController ship, IslandLayoutDefinition layoutDef, int seed)
        {
            const float hullRadius = 1.15f;
            Physics.SyncTransforms();
            var spawn = V3SpawnAnchorValidator.ResolveRuntimeSpawn(_layout, layoutDef, seed, hullRadius);
            ship.SetWorldPosition(spawn);
        }
    }
}
