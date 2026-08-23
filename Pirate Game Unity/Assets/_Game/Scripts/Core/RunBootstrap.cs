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
            EnsureWeaponStripHud();

            if (player == null)
                player = FindFirstObjectByType<PlayerShipController>();

            if (player != null && _layout != null)
                player.transform.position = _layout.playerSpawn;

            player?.InitializeFromRun(RunManager.Instance.Stats.MaxHull);

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

        void EnsureWeaponStripHud()
        {
            if (FindFirstObjectByType<WeaponLevelStripHud>() != null) return;
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            var strip = new GameObject("WeaponLevelStrip");
            strip.transform.SetParent(canvas.transform, false);
            strip.AddComponent<WeaponLevelStripHud>();
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
        }

        void OnPlayerDied()
        {
            RunManager.Instance.SetState(RunState.PlayerDead);
            RunManager.Instance.CompleteRun(false);
        }
    }
}
