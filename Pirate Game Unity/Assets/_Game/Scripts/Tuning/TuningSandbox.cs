using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.Pickups;
using HighSeas.Player;
using HighSeas.UI;
using HighSeas.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HighSeas.Tuning
{
    [DefaultExecutionOrder(50)]
    public sealed class TuningSandbox : MonoBehaviour
    {
        [SerializeField] bool disableAutoSpawn = true;
        [SerializeField] int spawnRingDistance = 12;
        [SerializeField] int enemiesPerSpawn = 3;

        static readonly string[] WeaponIds =
        {
            "deck_cannons",
            "broadside_battery",
            "burning_oil"
        };

        GameDatabase _database;
        WeaponController _weapons;
        EnemyWorld _enemyWorld;
        LevelSystem _levels;
        PlayerShipController _player;
        SpawnDirector _spawnDirector;
        int _enemyIndex;

        public bool HelpVisible { get; set; } = true;
        public string StatusMessage { get; private set; } = "Tuning sandbox ready.";

        void Start()
        {
            _database = GameDatabaseLoader.Load();
            _weapons = FindFirstObjectByType<WeaponController>();
            _enemyWorld = FindFirstObjectByType<EnemyWorld>();
            _levels = LevelSystem.Instance;
            _player = FindFirstObjectByType<PlayerShipController>();
            _spawnDirector = FindFirstObjectByType<SpawnDirector>();

            if (disableAutoSpawn && _spawnDirector != null)
                _spawnDirector.enabled = false;

            if (_enemyWorld?.Definitions != null && _enemyWorld.Definitions.Length > 0)
            {
                for (var i = 0; i < _enemyWorld.Definitions.Length; i++)
                {
                    if (_enemyWorld.Definitions[i] != null && _enemyWorld.Definitions[i].id == "cannonboat")
                    {
                        _enemyIndex = i;
                        break;
                    }
                }
            }

            SetStatus("Tuning sandbox active. Press H for hotkeys.");
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.hKey.wasPressedThisFrame)
            {
                HelpVisible = !HelpVisible;
                return;
            }

            if (keyboard.f1Key.wasPressedThisFrame) EquipWeapon(0, keyboard.shiftKey.isPressed);
            if (keyboard.f2Key.wasPressedThisFrame) EquipWeapon(1, keyboard.shiftKey.isPressed);
            if (keyboard.f3Key.wasPressedThisFrame) EquipWeapon(2, keyboard.shiftKey.isPressed);

            if (keyboard.lKey.wasPressedThisFrame) ForceLevelUp();
            if (keyboard.eKey.wasPressedThisFrame) SpawnEnemyCluster();
            if (keyboard.nKey.wasPressedThisFrame || keyboard.rightBracketKey.wasPressedThisFrame) CycleEnemy(1);
            if (keyboard.pKey.wasPressedThisFrame || keyboard.leftBracketKey.wasPressedThisFrame) CycleEnemy(-1);
            if (keyboard.xKey.wasPressedThisFrame) ClearEnemies();
            if (keyboard.gKey.wasPressedThisFrame) SpawnXpPickup();
            if (keyboard.rKey.wasPressedThisFrame) RefillHull();
            if (keyboard.minusKey.wasPressedThisFrame) AdjustFanSpread(-2f);
            if (keyboard.equalsKey.wasPressedThisFrame) AdjustFanSpread(2f);
        }

        void EquipWeapon(int weaponSlot, bool addSecondSlot)
        {
            if (_weapons == null || weaponSlot < 0 || weaponSlot >= WeaponIds.Length) return;
            var id = WeaponIds[weaponSlot];
            if (addSecondSlot)
            {
                if (_weapons.FindWeaponIndex(id) < 0)
                    _weapons.EquipWeapon(id);
                SetStatus($"Added weapon: {id}");
                return;
            }

            _weapons.Initialize(_database, id);
            SetStatus($"Equipped: {id}");
        }

        void ForceLevelUp()
        {
            if (_levels == null) return;
            var xp = Mathf.CeilToInt(_levels.XPToNext - _levels.CurrentXP);
            _levels.AddXP(Mathf.Max(1, xp));
            SetStatus("Forced level-up.");
        }

        void SpawnEnemyCluster()
        {
            if (_enemyWorld == null || _player == null) return;
            var def = CurrentEnemy();
            for (var i = 0; i < enemiesPerSpawn; i++)
            {
                var angle = i * (360f / Mathf.Max(1, enemiesPerSpawn)) * Mathf.Deg2Rad;
                var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spawnRingDistance;
                _enemyWorld.Spawn(_enemyIndex, _player.Position + offset);
            }
            SetStatus($"Spawned {enemiesPerSpawn}x {def?.displayName ?? "enemy"}.");
        }

        void CycleEnemy(int delta)
        {
            if (_enemyWorld?.Definitions == null || _enemyWorld.Definitions.Length == 0) return;
            _enemyIndex = (_enemyIndex + delta + _enemyWorld.Definitions.Length) % _enemyWorld.Definitions.Length;
            while (_enemyWorld.Definitions[_enemyIndex] == null)
                _enemyIndex = (_enemyIndex + delta + _enemyWorld.Definitions.Length) % _enemyWorld.Definitions.Length;
            SetStatus($"Enemy target: {CurrentEnemy().displayName}");
        }

        void ClearEnemies()
        {
            if (_enemyWorld == null) return;
            var agents = _enemyWorld.Agents;
            var cleared = 0;
            for (var i = 0; i < agents.Length; i++)
            {
                if (!agents[i].active) continue;
                _enemyWorld.Despawn(i);
                cleared++;
            }
            SetStatus($"Cleared {cleared} enemies.");
        }

        void SpawnXpPickup()
        {
            if (_player == null) return;
            PickupWorld.Instance?.Spawn(PickupType.Infamy, 25, _player.Position + Vector3.right * 2f);
            SetStatus("Spawned XP crystal.");
        }

        void RefillHull()
        {
            if (_player?.Damage == null) return;
            _player.Damage.Heal(_player.Damage.MaxHP);
            SetStatus("Hull refilled.");
        }

        void AdjustFanSpread(float delta)
        {
            var def = _database?.GetWeapon("broadside_battery");
            if (def == null || def.pattern != WeaponPattern.FanVolley)
            {
                SetStatus("Equip Broadside Battery (F2) to tune spread.");
                return;
            }

            def.fanSpreadDegrees = Mathf.Clamp(def.fanSpreadDegrees + delta, 4f, 90f);
            SetStatus($"Broadside spread: {def.fanSpreadDegrees:0}° (-/+ to adjust)");
        }

        public float? BroadsideSpreadDegrees()
        {
            var def = _database?.GetWeapon("broadside_battery");
            return def != null && def.pattern == WeaponPattern.FanVolley ? def.fanSpreadDegrees : null;
        }

        EnemyDefinition CurrentEnemy()
        {
            if (_enemyWorld?.Definitions == null || _enemyWorld.Definitions.Length == 0) return null;
            return _enemyWorld.Definitions[Mathf.Clamp(_enemyIndex, 0, _enemyWorld.Definitions.Length - 1)];
        }

        public int ActiveEnemyCount()
        {
            if (_enemyWorld == null) return 0;
            var count = 0;
            var agents = _enemyWorld.Agents;
            for (var i = 0; i < agents.Length; i++)
                if (agents[i].active) count++;
            return count;
        }

        public string CurrentEnemyLabel() => CurrentEnemy() != null ? CurrentEnemy().displayName : "none";

        public string EquippedWeaponsLabel()
        {
            if (_weapons == null || _weapons.Weapons.Count == 0) return "none";
            var text = _weapons.Weapons[0].def.displayName + " Lv" + _weapons.Weapons[0].level;
            if (_weapons.Weapons.Count > 1)
                text += " | " + _weapons.Weapons[1].def.displayName + " Lv" + _weapons.Weapons[1].level;
            return text;
        }

        void SetStatus(string message) => StatusMessage = message;
    }
}
