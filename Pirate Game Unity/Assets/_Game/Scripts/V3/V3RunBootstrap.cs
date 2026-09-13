using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.Meta;
using HighSeas.Pickups;
using HighSeas.Player;
using HighSeas.POI;
using HighSeas.UI;
using HighSeas.VFX;
using HighSeas.Weapons;
using HighSeas.World;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>
    /// V3 run bootstrap: post-boss survival, 100% gold banking at death, tropical island layout.
    /// Ship controller and camera values remain scene-authored (copied from Run.unity).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class V3RunBootstrap : MonoBehaviour
    {
        [SerializeField] GameDatabase database;
        [SerializeField] StageBuilder stageBuilder;
        [SerializeField] PlayerShipController player;
        [SerializeField] IslandLayoutDefinition islandLayout;
        [SerializeField] int runSeed = GameConstants.DemoRunSeed;
        [SerializeField] bool usePlaceholderPoiVisuals = true;

        StageLayout _layout;

        void Awake()
        {
            AppRoot.EnsureInitialized();
            if (database == null || !GameDatabaseLoader.IsValid(database))
                database = GameDatabaseLoader.Load();
            DisableTouchJoystick();
            EnsureGameplayBootstrapComponents();
        }

        static void EnsureGameplayBootstrapComponents()
        {
            var systems = GameObject.Find("Systems");
            if (systems == null) return;
            if (systems.GetComponent<PlayerShipVisualBootstrap>() == null)
                systems.AddComponent<PlayerShipVisualBootstrap>();
        }

        static void DisableTouchJoystick()
        {
            var joystick = GameObject.Find("JoystickBG");
            if (joystick != null)
                joystick.SetActive(false);
        }

        void Start()
        {
            if (database == null)
            {
                Debug.LogError("V3RunBootstrap: GameDatabase is missing.");
                return;
            }

            if (RunManager.Instance == null)
                gameObject.AddComponent<RunManager>();

            if (stageBuilder == null)
                stageBuilder = FindFirstObjectByType<StageBuilder>();

            ConfigureStageBuilder();

            var seed = runSeed != 0 ? runSeed : GameConstants.DemoRunSeed;
            RunManager.Instance.BeginRun(seed);
            _layout = stageBuilder != null ? stageBuilder.Build(seed, 0) : null;
            V3RunContext.Layout = _layout;
            V3RunContext.CaptainId = V3CaptainSelection.Selected;

            var poi = FindFirstObjectByType<POIManager>();
            if (poi != null)
            {
                poi.SetV3Options(continueAfterBossVictory: true, usePlaceholderVisuals: usePlaceholderPoiVisuals, useV3ChestPricing: true);
                poi.Initialize(_layout, database);
            }

            var enemyWorld = FindFirstObjectByType<EnemyWorld>();
            enemyWorld?.SetDefinitions(database.enemies);

            if (player == null)
                player = FindFirstObjectByType<PlayerShipController>();

            var captainId = V3CaptainSelection.Selected;
            var captainProfile = V3CaptainProfile.Get(captainId);
            EnsureCaptainController(captainId, player);

            var startingWeapon = captainProfile.StartingWeaponId;
            var weapons = FindFirstObjectByType<WeaponController>();
            weapons?.Initialize(database, startingWeapon);

            LevelSystem.Instance?.Initialize(database, startingWeapon);
            V3RunUiBootstrap.EnsureAll(_layout);

            if (player != null && _layout != null)
                player.transform.position = _layout.playerSpawn;

            if (V3CaptainController.Instance == null)
                player?.InitializeFromRun(RunManager.Instance.Stats.MaxHull);
            EnsurePlayerHealthBar(player);

            if (player != null)
            {
                player.Damage.Died += OnPlayerDied;
                if (TriggerBus.Instance != null)
                    player.Damage.Damaged += OnPlayerDamaged;
            }

            EnsureCombatVfx();
            ConfigureSpawnDirector();
            EnsurePressureBeats(enemyWorld, player);
            RunManager.Instance.EnterPlaying();
            V3ToastHud.Instance?.Show(
                "V3 Saltwind — explore 12 chests + 4 shrines, secure the Beacon. Death banks all run gold.",
                7f);
        }

        void ConfigureSpawnDirector()
        {
            var director = FindFirstObjectByType<SpawnDirector>();
            director?.ConfigureV3Pacing(true);
        }

        void EnsureCaptainController(V3CaptainId captainId, PlayerShipController ship)
        {
            var systems = GameObject.Find("Systems");
            if (systems == null) return;

            var controller = systems.GetComponent<V3CaptainController>();
            if (controller == null)
                controller = systems.AddComponent<V3CaptainController>();
            controller.Initialize(captainId, ship);
        }

        void EnsurePressureBeats(EnemyWorld enemyWorld, PlayerShipController ship)
        {
            var systems = GameObject.Find("Systems");
            if (systems == null) return;

            var director = systems.GetComponent<SpawnDirector>();
            if (director == null)
                director = FindFirstObjectByType<SpawnDirector>();

            var beats = systems.GetComponent<V3PressureBeatDirector>();
            if (beats == null)
                beats = systems.AddComponent<V3PressureBeatDirector>();
            beats.Initialize(enemyWorld, director, ship != null ? ship.transform : null);
        }

        void ConfigureStageBuilder()
        {
            if (stageBuilder == null) return;

            var layout = ResolveIslandLayout();
            stageBuilder.ConfigureV3Layout(layout);
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

        void Update()
        {
            if (RunManager.Instance == null || RunManager.Instance.IsPaused) return;
            var dt = Time.deltaTime;
            var playerPos = player != null ? player.Position : Vector3.zero;
            FindFirstObjectByType<EnemyWorld>()?.Tick(dt, playerPos, player?.Damage);
            FindFirstObjectByType<ProjectileWorld>()?.Tick(dt, FindFirstObjectByType<EnemyWorld>());
            FindFirstObjectByType<PickupWorld>()?.Tick(dt, playerPos, RunManager.Instance.Stats.PickupRadius);
            FindFirstObjectByType<StatusController>()?.Tick(dt, FindFirstObjectByType<EnemyWorld>());
        }

        void OnPlayerDamaged(float amount, bool _) => TriggerBus.Instance?.RaiseDamageTaken(amount);

        void OnPlayerDied()
        {
            RunManager.Instance.SetState(RunState.PlayerDead);
            RunManager.Instance.FinalizeRunOnDeath();
            V3ToastHud.Instance?.Show("Run ended — gold banked at port.", 4f);
        }

        void EnsurePlayerHealthBar(PlayerShipController ship)
        {
            if (ship == null) return;
            if (ship.GetComponent<PlayerHealthBarBinder>() == null)
                ship.gameObject.AddComponent<PlayerHealthBarBinder>();
        }

        void EnsureCombatVfx()
        {
            var systems = GameObject.Find("Systems");
            if (systems == null) return;

            if (systems.GetComponent<JuiceVfxBroker>() == null)
                systems.AddComponent<JuiceVfxBroker>();
            if (systems.GetComponent<CombatVfxController>() == null)
                systems.AddComponent<CombatVfxController>();
            if (systems.GetComponent<WeaponJuiceController>() == null)
                systems.AddComponent<WeaponJuiceController>();
            if (systems.GetComponent<PlayerJuiceController>() == null)
                systems.AddComponent<PlayerJuiceController>();

            var vfxRoot = systems.transform.Find("VFXRoot");
            if (vfxRoot == null)
            {
                var go = new GameObject("VFXRoot");
                go.transform.SetParent(systems.transform, false);
                vfxRoot = go.transform;
            }

            var broker = systems.GetComponent<JuiceVfxBroker>();
            if (broker != null)
            {
                var settings = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
                broker.Configure(settings, vfxRoot);
            }
        }
    }
}
