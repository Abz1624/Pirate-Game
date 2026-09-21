using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.Player;
using HighSeas.UI;
using HighSeas.V3.MultiShip;
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
        bool _ambientSpawnWasEnabled = true;
        bool _spawnOverrideActive;

        WeaponController _weapons;
        LevelSystem _levels;
        EncounterSpawner _encounterSpawner;
        EnemyWorld _enemyWorld;
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
            _encounterSpawner = FindFirstObjectByType<EncounterSpawner>();
            _enemyWorld = FindFirstObjectByType<EnemyWorld>();
            _player = FindFirstObjectByType<PlayerShipController>();
            if (_encounterSpawner != null)
                _ambientSpawnWasEnabled = _encounterSpawner.AmbientScheduleEnabled;
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

            if (keyboard.kKey.wasPressedThisFrame)
                GrantCoOpPartyRandomWeapons();

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                DespawnOldestEnemy();
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
            if (V3RunSession.IsPvP && V3PvPWeaponLoadout.IsExcludedFromPvP(id))
            {
                CycleWeapon(delta);
                return;
            }

            _weapons.SetSoloWeapon(id);
            _levels?.SetPlaytestSoloWeapon(id);
            CombatExperimentContext.ShowAimVisuals = true;
            V3ToastHud.Instance?.Show(
                $"Weapon test [{_weaponIndex + 1}/{pool.Length}]\n{V3WeaponPlaytestCatalog.GetDisplayLabel(id)}\n[ / ] or , / . to cycle · Shift+E spawn targets · F8 help",
                3.5f);
        }

        void SpawnTargetCluster()
        {
            var spawner = EncounterSpawner.Instance ?? FindFirstObjectByType<EncounterSpawner>();
            var profile = spawner?.GetScheduleProfile("route_cannon");
            if (spawner == null || profile == null)
            {
                V3ToastHud.Instance?.Show("Missing route_cannon profile on EncounterSpawnSchedule.", 2.5f);
                return;
            }

            spawner.TriggerProfile(profile);
            V3ToastHud.Instance?.Show("Triggered target profile (Shift+E)", 2f);
        }

        void RefillHull()
        {
            if (_player?.Damage == null) return;
            _player.Damage.Heal(_player.Damage.MaxHP);
            V3ToastHud.Instance?.Show("Hull refilled (Shift+R)", 1.5f);
        }

        void DespawnOldestEnemy()
        {
            if (_enemyWorld == null) _enemyWorld = FindFirstObjectByType<EnemyWorld>();
            if (_enemyWorld == null) return;

            if (!_enemyWorld.TryDespawnOldestActive(out var unitId))
            {
                V3ToastHud.Instance?.Show("No active enemies to remove (1)", 1.5f);
                return;
            }

            var label = unitId >= 0 ? $"Removed oldest enemy (spawn id {unitId})" : "Removed oldest enemy";
            V3ToastHud.Instance?.Show($"{label} · 1", 1.5f);
        }

        void ToggleAutoSpawn()
        {
            if (_encounterSpawner == null) _encounterSpawner = FindFirstObjectByType<EncounterSpawner>();
            if (_encounterSpawner == null) return;
            _spawnOverrideActive = !_spawnOverrideActive;
            _encounterSpawner.SetAmbientScheduleEnabled(
                _spawnOverrideActive ? _ambientSpawnWasEnabled : false);
            V3ToastHud.Instance?.Show(
                _encounterSpawner.AmbientScheduleEnabled
                    ? "Auto spawn ON (Shift+N)"
                    : "Auto spawn OFF — use Shift+E for targets",
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

        void GrantCoOpPartyRandomWeapons()
        {
            if (!V3RunSession.IsCoOp)
            {
                V3ToastHud.Instance?.Show("K — co-op only: random 6 weapons for all player ships.", 2.5f);
                return;
            }

            var database = GameDatabaseLoader.Load();
            var armed = V3MultiShipWeaponLoadouts.RollAndApplyRandomLoadoutsToAllShips(database);
            V3ToastHud.Instance?.Show(
                armed > 0
                    ? $"Co-op loadout (K): {armed} ships armed with 6 random weapons each."
                    : "No player ships found to arm.",
                4f);
        }

        void GrantDemoGunneryLoadout()
        {
            if (V3RunSession.IsPvP)
            {
                V3ToastHud.Instance?.Show("Y — gunnery loadout disabled in PvP (mortar off; duel uses Broadside Lv3).", 3f);
                return;
            }

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
                "1 — remove oldest spawned enemy\n" +
                "I — grant next interaction item\n" +
                "O — grant logbook rank\n" +
                "B — summon treasury boss\n" +
                "Y — gunnery loadout (6 weapons)\n" +
                "K — co-op: random 6 weapons on all ships\n" +
                "G — scatter random rarity weapon cards (pop-in)\n" +
                "H — auto-scroll 50+ weapon cards (R→L)\n" +
                "Esc — close card demos\n" +
                "V — toggle aim overlays\n" +
                "F9 — spawn director debug panel",
                8f);
        }
    }
}
