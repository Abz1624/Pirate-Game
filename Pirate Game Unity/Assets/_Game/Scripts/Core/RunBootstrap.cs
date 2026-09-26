using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.Meta;
using HighSeas.Pickups;
using HighSeas.Player;
using HighSeas.POI;
using HighSeas.UI;
using HighSeas.Weapons;
using HighSeas.VFX;
using HighSeas.World;
using UnityEngine;

namespace HighSeas.Core
{
    [DefaultExecutionOrder(-100)]
    public sealed class RunBootstrap : MonoBehaviour
    {
        [SerializeField] GameDatabase database;
        [SerializeField] StageBuilder stageBuilder;
        [SerializeField] PlayerShipController player;
        [SerializeField] int runSeed = GameConstants.DemoRunSeed;

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
            if (systems == null)
                return;

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
                Debug.LogError("RunBootstrap: GameDatabase is missing. Run HighSeas/Bootstrap Project in the editor.");
                return;
            }

            if (RunManager.Instance == null)
            {
                gameObject.AddComponent<RunManager>();
            }

            if (stageBuilder == null)
                stageBuilder = FindFirstObjectByType<StageBuilder>();

            if (stageBuilder == null)
            {
                var systems = GameObject.Find("Systems");
                if (systems != null)
                    stageBuilder = systems.GetComponent<StageBuilder>();
            }

            var seed = runSeed != 0 ? runSeed : GameConstants.DemoRunSeed;
            RunManager.Instance.BeginRun(seed);
            _layout = stageBuilder != null ? stageBuilder.Build(seed, 0) : null;
            if (_layout == null)
                Debug.LogWarning("RunBootstrap: no StageBuilder found — islands and POIs were not generated.");

            var poi = FindFirstObjectByType<POIManager>();
            poi?.Initialize(_layout, database);

            var enemyWorld = FindFirstObjectByType<EnemyWorld>();
            enemyWorld?.SetDefinitions(database.enemies);

            var startingWeapon = database.ships[0].startingWeaponId;
            var weapons = FindFirstObjectByType<WeaponController>();
            weapons?.Initialize(database, startingWeapon);

            LevelSystem.Instance?.Initialize(database, startingWeapon);

            if (player == null)
                player = FindFirstObjectByType<PlayerShipController>();

            if (player != null && _layout != null)
                player.transform.position = _layout.playerSpawn;

            player?.InitializeFromRun(RunManager.Instance.Stats.MaxHull);
            EnsurePlayerHealthBar(player);

            if (player != null)
            {
                player.Damage.Died += OnPlayerDied;
                if (TriggerBus.Instance != null)
                    player.Damage.Damaged += OnPlayerDamaged;
            }

            EnsureCombatVfx();

            RunManager.Instance.EnterPlaying();
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

        void EnsurePlayerHealthBar(PlayerShipController player)
        {
            if (player == null) return;
            if (player.GetComponent<PlayerHealthBarBinder>() == null)
                player.gameObject.AddComponent<PlayerHealthBarBinder>();
        }

        void EnsureCombatVfx()
        {
            var systems = GameObject.Find("Systems");
            if (systems == null) return;

            if (systems.GetComponent<JuiceVfxBroker>() == null)
                systems.AddComponent<JuiceVfxBroker>();
            if (systems.GetComponent<CombatVfxController>() == null)
                systems.AddComponent<CombatVfxController>();
            if (systems.GetComponent<FloatingDamageNumbers>() == null)
                systems.AddComponent<FloatingDamageNumbers>();
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

        void OnPlayerDied()
        {
            RunManager.Instance.SetState(RunState.PlayerDead);
            RunManager.Instance.CompleteRun(false);
        }
    }
}
