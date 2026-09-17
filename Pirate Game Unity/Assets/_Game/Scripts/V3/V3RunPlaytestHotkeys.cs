using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.Player;
using HighSeas.UI;
using HighSeas.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HighSeas.V3
{
    /// <summary>In-run playtest hotkeys: solo weapon cycle, targets, items, logbooks.</summary>
    [DefaultExecutionOrder(45)]
    public sealed class V3RunPlaytestHotkeys : MonoBehaviour
    {
        public static V3RunPlaytestHotkeys Instance { get; private set; }

        static readonly string[] DemoGunneryLoadout =
        {
            "powder_kegs",
            "lightning_mast",
            "mortar",
            "broadside_battery",
            "long_nine",
            "swivel_guns"
        };

        int _weaponIndex;
        int _itemIndex;
        int _logbookIndex;
        bool _spawnDirectorWasEnabled = true;
        bool _spawnOverrideActive;

        WeaponController _weapons;
        LevelSystem _levels;
        EnemyWorld _enemies;
        SpawnDirector _spawnDirector;
        PlayerShipController _player;

        public static V3RunPlaytestHotkeys Ensure()
        {
            if (!V3PlaytestFlags.EnableRunPlaytestHotkeys) return Instance;
            if (Instance != null) return Instance;
            var go = new GameObject("V3RunPlaytestHotkeys");
            Instance = go.AddComponent<V3RunPlaytestHotkeys>();
            return Instance;
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            _weapons = FindFirstObjectByType<WeaponController>();
            _levels = LevelSystem.Instance;
            _enemies = FindFirstObjectByType<EnemyWorld>();
            _spawnDirector = FindFirstObjectByType<SpawnDirector>();
            _player = FindFirstObjectByType<PlayerShipController>();
            if (_spawnDirector != null)
                _spawnDirectorWasEnabled = _spawnDirector.enabled;
        }

        void Update()
        {
            if (!V3PlaytestFlags.EnableRunPlaytestHotkeys || !V3RunContext.IsActive) return;
            if (RunManager.Instance == null || RunManager.Instance.IsRunEnded) return;
            if (RunManager.Instance.IsPaused) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f8Key.wasPressedThisFrame)
            {
                ShowHelpToast();
                return;
            }

            if (keyboard.leftBracketKey.wasPressedThisFrame || keyboard.commaKey.wasPressedThisFrame)
                CycleWeapon(-1);
            else if (keyboard.rightBracketKey.wasPressedThisFrame || keyboard.periodKey.wasPressedThisFrame)
                CycleWeapon(1);

            if (keyboard.eKey.wasPressedThisFrame && keyboard.leftShiftKey.isPressed)
                SpawnTargetCluster();
            if (keyboard.rKey.wasPressedThisFrame && keyboard.leftShiftKey.isPressed)
                RefillHull();
            if (keyboard.nKey.wasPressedThisFrame && keyboard.leftShiftKey.isPressed)
                ToggleAutoSpawn();

            if (keyboard.iKey.wasPressedThisFrame)
                GrantNextItem(1);
            if (keyboard.oKey.wasPressedThisFrame)
                GrantNextLogbook();

            if (keyboard.yKey.wasPressedThisFrame)
                GrantDemoGunneryLoadout();
        }

        void CycleWeapon(int delta)
        {
            if (_weapons == null) _weapons = FindFirstObjectByType<WeaponController>();
            if (_weapons == null) return;

            var pool = V3WeaponPlaytestCatalog.All;
            if (pool.Length == 0) return;

            _weaponIndex = (_weaponIndex + delta) % pool.Length;
            if (_weaponIndex < 0) _weaponIndex += pool.Length;

            var id = pool[_weaponIndex];
            _weapons.SetSoloWeapon(id);
            _levels?.SetPlaytestSoloWeapon(id);
            CombatExperimentContext.ShowAimVisuals = true;
            V3ToastHud.Instance?.Show(
                $"Weapon test [{_weaponIndex + 1}/{pool.Length}]\n{V3WeaponPlaytestCatalog.GetDisplayLabel(id)}\n[ / ] or , / . to cycle · Shift+E spawn targets · F8 help",
                3.5f);
        }

        void SpawnTargetCluster()
        {
            if (_enemies == null || _player == null) return;
            var defIndex = 0;
            if (_enemies.Definitions != null)
            {
                for (var i = 0; i < _enemies.Definitions.Length; i++)
                {
                    if (_enemies.Definitions[i]?.id == "cannonboat") { defIndex = i; break; }
                }
            }

            var origin = _player.Position;
            const int count = 6;
            const float radius = 14f;
            for (var i = 0; i < count; i++)
            {
                var angle = i / (float)count * Mathf.PI * 2f;
                var pos = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                _enemies.Spawn(defIndex, pos);
            }

            V3ToastHud.Instance?.Show("Spawned target ring (Shift+E)", 2f);
        }

        void RefillHull()
        {
            if (_player?.Damage == null) return;
            _player.Damage.Heal(_player.Damage.MaxHP);
            V3ToastHud.Instance?.Show("Hull refilled (Shift+R)", 1.5f);
        }

        void ToggleAutoSpawn()
        {
            if (_spawnDirector == null) _spawnDirector = FindFirstObjectByType<SpawnDirector>();
            if (_spawnDirector == null) return;
            _spawnOverrideActive = !_spawnOverrideActive;
            _spawnDirector.enabled = _spawnOverrideActive ? _spawnDirectorWasEnabled : false;
            V3ToastHud.Instance?.Show(
                _spawnDirector.enabled ? "Auto spawn ON (Shift+N)" : "Auto spawn OFF — use Shift+E for targets",
                2.5f);
        }

        void GrantNextItem(int delta)
        {
            var pool = V3ItemCatalog.InteractionItemIds;
            if (pool.Length == 0) return;
            _itemIndex = (_itemIndex + delta) % pool.Length;
            if (_itemIndex < 0) _itemIndex += pool.Length;
            var id = pool[_itemIndex];
            V3RunItemInventory.Instance?.TryAddItem(id);
        }

        void GrantNextLogbook()
        {
            if (_levels == null) _levels = LevelSystem.Instance;
            if (_levels == null) return;
            var pool = V3LogbookCatalog.AllIds;
            if (pool.Length == 0) return;
            _logbookIndex = (_logbookIndex + 1) % pool.Length;
            _levels.GrantPlaytestLogbookRank(pool[_logbookIndex]);
        }

        void GrantDemoGunneryLoadout()
        {
            if (_weapons == null) _weapons = FindFirstObjectByType<WeaponController>();
            if (_weapons == null) return;

            _weapons.SetWeaponLoadout(DemoGunneryLoadout);
            _levels?.SetPlaytestWeaponLoadout(DemoGunneryLoadout);
            CombatExperimentContext.ShowAimVisuals = true;
            V3ToastHud.Instance?.Show(
                "Gunnery loadout equipped (Y)\n" +
                "Powder Kegs · Lightning · Mortar · Broadside · Long Nine · Swivel Guns",
                4f);
        }

        static void ShowHelpToast()
        {
            V3ToastHud.Instance?.Show(
                "Playtest hotkeys\n" +
                "[ or ,  ·  ] or .  — cycle solo weapon (12 pool)\n" +
                "Shift+E — spawn enemy ring\n" +
                "Shift+R — refill hull\n" +
                "Shift+N — toggle auto spawn\n" +
                "I — grant next interaction item\n" +
                "O — grant logbook rank\n" +
                "B — summon treasury boss\n" +
                "Y — gunnery loadout (6 weapons)\n" +
                "V — toggle aim overlays",
                8f);
        }
    }
}
