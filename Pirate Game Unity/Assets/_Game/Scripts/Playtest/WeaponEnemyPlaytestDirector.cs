using System.Collections;
using System.Collections.Generic;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.Player;
using HighSeas.UI;
using HighSeas.V3;
using HighSeas.V3.MultiShip;
using HighSeas.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HighSeas.Playtest
{
    /// <summary>
    /// Weapon/enemy sandbox hotkeys. Run boot is handled by <see cref="WeaponEnemyPlaytestBootstrap"/>.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class WeaponEnemyPlaytestDirector : MonoBehaviour
    {
        const string SceneName = "Test_WeaponEnemy";

        [SerializeField] float spawnDistance = 14f;
        [SerializeField] bool clearEnemiesBeforeSpawn;
        [SerializeField] bool includeBossesInEnemyCycle;

        readonly List<int> _enemyDefinitionIndices = new();
        int _weaponIndex;
        int _enemyIndex;
        string _statusMessage = "";
        float _statusUntil;
        bool _ready;

        WeaponController _weapons;
        LevelSystem _levels;
        EnemyWorld _enemyWorld;
        PlayerShipController _player;

        void Awake()
        {
            var legacyHud = GameObject.Find("TuningSandboxHud");
            if (legacyHud != null)
                legacyHud.SetActive(false);

            if (!IsWeaponEnemyPlaytestScene())
                return;

            DisableLegacyRunBootstraps();
            DisableAutomaticEnemySpawning();
            EnsurePlaytestBootstrap();
            FixPlayerModelForwardForV3Ship();
        }

        static bool IsWeaponEnemyPlaytestScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            return scene.name == SceneName
                || (!string.IsNullOrEmpty(scene.path) && scene.path.Contains("Test_WeaponEnemy"));
        }

        static void DisableLegacyRunBootstraps()
        {
            foreach (var legacy in FindObjectsByType<RunBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                legacy.enabled = false;
            foreach (var v3 in FindObjectsByType<V3RunBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                v3.enabled = false;
        }

        static void DisableAutomaticEnemySpawning()
        {
            foreach (var spawner in FindObjectsByType<EncounterSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                spawner.EnterManualPlaytestMode();

            foreach (var balance in FindObjectsByType<HighSeas.Balance.StageBalanceProfileHost>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                balance.enabled = false;
        }

        void EnsurePlaytestBootstrap()
        {
            if (GetComponent<WeaponEnemyPlaytestBootstrap>() != null)
                return;
            gameObject.AddComponent<WeaponEnemyPlaytestBootstrap>();
        }

        static void FixPlayerModelForwardForV3Ship()
        {
            var player = FindFirstObjectByType<PlayerShipController>();
            if (player == null) return;
            player.SetModelForwardIsNegativeZ(false);
        }

        IEnumerator Start()
        {
            if (!IsWeaponEnemyPlaytestScene())
                yield break;

            var waitSeconds = 0f;
            while (!WeaponEnemyPlaytestSession.Active && waitSeconds < 5f)
            {
                waitSeconds += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!WeaponEnemyPlaytestSession.Active)
            {
                EnsurePlaytestBootstrap();
                yield return null;
            }

            yield return null;
            RefreshReferences();
            RebuildEnemyList();
            EquipWeaponAtIndex(_weaponIndex);
            _ready = _enemyDefinitionIndices.Count > 0 && _weapons != null && _player != null;
            ShowStatus(
                _ready
                    ? "Weapon/Enemy playtest\n[ / ] weapon · ; / ' enemy · P spawn · \\ clear · O aim · F1 help"
                    : "Playtest not ready — check bootstrap + database.",
                6f);
        }

        void RefreshReferences()
        {
            _weapons = V3MultiShipWeaponLoadouts.FindLocalHumanWeaponController()
                ?? FindFirstObjectByType<WeaponController>();
            _levels = LevelSystem.Instance;
            _enemyWorld = FindFirstObjectByType<EnemyWorld>();
            _player = FindFirstObjectByType<PlayerShipController>();
        }

        void Update()
        {
            if (!IsWeaponEnemyPlaytestScene() || !WeaponEnemyPlaytestSession.Active) return;
            if (RunManager.Instance != null && RunManager.Instance.IsPaused) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.backslashKey.wasPressedThisFrame)
            {
                ClearAllEnemies();
                return;
            }

            if (!_ready) return;

            if (keyboard.f1Key.wasPressedThisFrame)
            {
                ShowHelp();
                return;
            }

            if (keyboard.pKey.wasPressedThisFrame)
            {
                var bossesOnly = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                SpawnSelectedEnemy(bossesOnly);
            }

            if (keyboard.leftBracketKey.wasPressedThisFrame)
                CycleWeapon(-1);
            else if (keyboard.rightBracketKey.wasPressedThisFrame)
                CycleWeapon(1);

            if (keyboard.semicolonKey.wasPressedThisFrame)
                CycleEnemy(-1);
            else if (keyboard.quoteKey.wasPressedThisFrame)
                CycleEnemy(1);
        }

        void RebuildEnemyList()
        {
            if (_enemyWorld?.Definitions == null)
            {
                _enemyDefinitionIndices.Clear();
                return;
            }

            V3SaltwindEnemyPlaytestCatalog.CollectCycleDefinitionIndices(
                _enemyWorld.Definitions,
                includeBossesInEnemyCycle,
                _enemyDefinitionIndices);
            _enemyIndex = Mathf.Clamp(_enemyIndex, 0, Mathf.Max(0, _enemyDefinitionIndices.Count - 1));
        }

        void CycleWeapon(int delta)
        {
            var pool = V3WeaponPlaytestCatalog.All;
            if (pool.Length == 0 || _weapons == null)
            {
                ShowStatus("No WeaponController or weapon pool.", 2.5f);
                return;
            }

            _weaponIndex = (_weaponIndex + delta) % pool.Length;
            if (_weaponIndex < 0) _weaponIndex += pool.Length;
            EquipWeaponAtIndex(_weaponIndex);
        }

        void EquipWeaponAtIndex(int index)
        {
            var pool = V3WeaponPlaytestCatalog.All;
            if (pool.Length == 0 || _weapons == null) return;

            index = Mathf.Clamp(index, 0, pool.Length - 1);
            _weaponIndex = index;
            var id = pool[_weaponIndex];
            _weapons.SetSoloWeapon(id);
            if (_weapons.Weapons.Count == 0)
            {
                ShowStatus($"Failed to equip '{id}'.", 3f);
                return;
            }

            _levels?.SetPlaytestSoloWeapon(id);
            CombatExperimentContext.ShowAimVisuals = true;
            ShowStatus(
                $"Weapon [{_weaponIndex + 1}/{pool.Length}]\n{V3WeaponPlaytestCatalog.GetDisplayLabel(id)}",
                3f);
        }

        void CycleEnemy(int delta)
        {
            if (_enemyDefinitionIndices.Count == 0)
            {
                ShowStatus("No sandbox enemies (enable Include Bosses or check database).", 2.5f);
                return;
            }

            _enemyIndex = (_enemyIndex + delta) % _enemyDefinitionIndices.Count;
            if (_enemyIndex < 0) _enemyIndex += _enemyDefinitionIndices.Count;
            ShowCurrentEnemyToast();
        }

        void ShowCurrentEnemyToast()
        {
            var def = GetSelectedEnemyDefinition();
            if (def == null) return;
            ShowStatus(
                $"Enemy [{_enemyIndex + 1}/{_enemyDefinitionIndices.Count}]\n{def.displayName} ({def.id})\nP = spawn one",
                3.5f);
        }

        EnemyDefinition GetSelectedEnemyDefinition()
        {
            if (_enemyWorld?.Definitions == null || _enemyDefinitionIndices.Count == 0) return null;
            var idx = _enemyDefinitionIndices[_enemyIndex];
            return idx >= 0 && idx < _enemyWorld.Definitions.Length
                ? _enemyWorld.Definitions[idx]
                : null;
        }

        void SpawnSelectedEnemy(bool spawnBossFromDatabase)
        {
            RefreshReferences();
            RebuildEnemyList();

            if (_enemyWorld == null || _player == null)
            {
                ShowStatus("Missing EnemyWorld or player.", 2.5f);
                return;
            }

            if (clearEnemiesBeforeSpawn)
                _enemyWorld.DespawnAllActive();

            int defIndex;
            EnemyDefinition def;
            if (spawnBossFromDatabase)
            {
                defIndex = ResolveDefinitionIndex("barnacle_king");
                if (defIndex < 0)
                {
                    ShowStatus("barnacle_king not found in database.", 2.5f);
                    return;
                }

                def = _enemyWorld.Definitions[defIndex];
            }
            else
            {
                if (_enemyDefinitionIndices.Count == 0)
                {
                    ShowStatus("No enemies in cycle.", 2.5f);
                    return;
                }

                defIndex = _enemyDefinitionIndices[_enemyIndex];
                def = _enemyWorld.Definitions[defIndex];
            }

            var center = _player.Position;
            center.y = 0f;
            var forward = _player.MovementForward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;
            forward.Normalize();
            var pos = center + forward * spawnDistance;
            pos.y = 0f;

            var slot = _enemyWorld.Spawn(defIndex, pos);
            ShowStatus(
                slot >= 0
                    ? $"Spawned 1 × {def.displayName}"
                    : $"Spawn failed ({def.displayName}) — enemy cap?",
                3.5f);
        }

        static int ResolveDefinitionIndex(EnemyWorld world, string enemyId)
        {
            if (world?.Definitions == null) return -1;
            var defs = world.Definitions;
            for (var i = 0; i < defs.Length; i++)
            {
                if (defs[i] != null && defs[i].id == enemyId)
                    return i;
            }

            return -1;
        }

        int ResolveDefinitionIndex(string enemyId) => ResolveDefinitionIndex(_enemyWorld, enemyId);

        void ClearAllEnemies()
        {
            RefreshReferences();
            if (_enemyWorld == null)
            {
                ShowStatus("No EnemyWorld.", 2f);
                return;
            }

            var before = _enemyWorld.ActiveCount;
            _enemyWorld.DespawnAllActive();
            ShowStatus(
                before > 0 ? $"Cleared {before} enemies (\\)" : "No active enemies (\\)",
                2.5f);
        }

        void ShowHelp()
        {
            ShowStatus(
                "Weapon + Enemy playtest (V3 ship/camera)\n" +
                "[  ]  cycle weapon\n" +
                ";  '  cycle enemy (no bosses by default)\n" +
                "P  spawn one ahead of ship · Shift+P Barnacle King\n" +
                "\\  clear all spawned enemies · O / V aim overlays\n" +
                "Hull damage disabled in this scene\n" +
                "F1  help",
                8f);
        }

        void ShowStatus(string message, float durationSeconds)
        {
            _statusMessage = message;
            _statusUntil = Time.unscaledTime + durationSeconds;
        }

        void OnGUI()
        {
            var pool = V3WeaponPlaytestCatalog.All;
            var weaponLabel = pool.Length > 0 && _weaponIndex >= 0 && _weaponIndex < pool.Length
                ? V3WeaponPlaytestCatalog.GetDisplayLabel(pool[_weaponIndex])
                : "—";
            var enemyDef = GetSelectedEnemyDefinition();
            var enemyLabel = enemyDef != null ? $"{enemyDef.displayName} ({enemyDef.id})" : "—";

            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.Box(new Rect(12f, Screen.height - 132f, 440f, 120f), GUIContent.none);
            GUI.color = Color.white;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true
            };
            GUI.Label(
                new Rect(22f, Screen.height - 122f, 420f, 72f),
                $"PLAYTEST  ·  Weapon: {weaponLabel}\n" +
                $"Enemy: {enemyLabel}\n" +
                "[ ] weapon   ; ' enemy   P spawn   \\ clear   O aim   Shift+P king",
                style);

            if (Time.unscaledTime <= _statusUntil && !string.IsNullOrEmpty(_statusMessage))
            {
                GUI.color = new Color(0f, 0f, 0f, 0.65f);
                GUI.Box(new Rect(Screen.width * 0.5f - 210f, 24f, 420f, 96f), GUIContent.none);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width * 0.5f - 200f, 32f, 400f, 84f), _statusMessage, style);
            }
        }
    }
}
