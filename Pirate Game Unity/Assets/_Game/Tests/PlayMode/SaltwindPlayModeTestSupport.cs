using System;
using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Encounters;
using HighSeas.Enemies;
using HighSeas.POI;
using HighSeas.World;
using UnityEngine;

namespace HighSeas.Tests
{
    /// <summary>Shared PlayMode setup for Saltwind lifecycle / spawner integration (Codex R10).</summary>
    public sealed class SaltwindPlayModeTestSupport
    {
        readonly GameObject _root;
        bool _disposed;

        public RunManager RunManager { get; }
        public EncounterSpawner Spawner { get; }
        public EnemyWorld EnemyWorld { get; }
        public EnemyDefinition TestEnemyDefinition { get; }
        public Transform PlayerTransform { get; }

        SaltwindPlayModeTestSupport(
            GameObject root,
            RunManager runManager,
            EncounterSpawner spawner,
            EnemyWorld enemyWorld,
            EnemyDefinition testEnemyDefinition,
            Transform playerTransform)
        {
            _root = root;
            RunManager = runManager;
            Spawner = spawner;
            EnemyWorld = enemyWorld;
            TestEnemyDefinition = testEnemyDefinition;
            PlayerTransform = playerTransform;
        }

        public static SaltwindPlayModeTestSupport Create(int seed = 4242)
        {
            AppRoot.EnsureInitialized();
            StageBalanceProfileContext.BeginRun(StageBalanceProfile.CreateBuiltInSaltwindDefaults());
            RunEncounterTracker.ResetForTests();
            RunMarksTracker.Reset();
            SaltwindRunSettlement.ClearLastResults();

            var root = new GameObject("SaltwindPlayModeHarness");

            var rm = root.AddComponent<RunManager>();
            rm.BeginRun(seed);
            rm.EnterPlaying();

            var playerGo = new GameObject("PlayerAnchor");
            playerGo.transform.SetParent(root.transform, false);
            playerGo.transform.position = Vector3.zero;

            var camGo = new GameObject("SpawnCam");
            camGo.transform.SetParent(root.transform, false);
            camGo.transform.position = new Vector3(0f, 25f, -12f);
            camGo.transform.LookAt(Vector3.zero);
            var cam = camGo.AddComponent<Camera>();

            var enemyGo = new GameObject("EnemyWorld");
            enemyGo.transform.SetParent(root.transform, false);
            var enemyWorld = enemyGo.AddComponent<EnemyWorld>();
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.id = "playmode_shark";
            def.displayName = "PlayMode Shark";
            def.baseHP = 10f;
            def.baseDamage = 1f;
            def.moveSpeed = 3f;
            def.radius = 0.6f;
            def.goldValue = 1;
            enemyWorld.SetDefinitions(new[] { def });

            var spawnerGo = new GameObject("EncounterSpawner");
            spawnerGo.transform.SetParent(root.transform, false);
            var spawner = spawnerGo.AddComponent<EncounterSpawner>();
            spawner.ConfigureRuntimeReferences(enemyWorld, playerGo.transform, cam);
            spawner.ResetForNewRun();

            return new SaltwindPlayModeTestSupport(root, rm, spawner, enemyWorld, def, playerGo.transform);
        }

        public SpawnProfile CreateEncounterProfile(int spawnCount = 1)
        {
            var profile = ScriptableObject.CreateInstance<SpawnProfile>();
            profile.enemy = TestEnemyDefinition;
            profile.spawnMode = SpawnMode.Burst;
            profile.timing.spawnCount = spawnCount;
            profile.placement.distanceMin = 8f;
            profile.placement.distanceMax = 14f;
            profile.placement.fallbackPolicy = SpawnFallbackPolicy.RetryLater;
            profile.placement.requireOutsidePlayerFov = false;
            return profile;
        }

        public POIManager CreatePoiManager()
        {
            var db = GameDatabaseLoader.Load();
            var layout = new StageLayout { playerSpawn = Vector3.zero, bossBeacon = Vector3.forward * 20f };
            var go = new GameObject("POIManager");
            go.transform.SetParent(_root.transform, false);
            var poi = go.AddComponent<POIManager>();
            poi.SetV3Options(continueAfterBossVictory: true, usePlaceholderVisuals: true, useV3ChestPricing: true);
            poi.BindEncounterSpawner(Spawner);
            poi.Initialize(layout, db);
            return poi;
        }

        public DamageReceiver CreatePlayerDamageReceiver(float hull = 50f)
        {
            var go = new GameObject("PlayerDamage");
            go.transform.SetParent(_root.transform, false);
            var dmg = go.AddComponent<DamageReceiver>();
            dmg.Initialize(hull, 0f);
            return dmg;
        }

        public void FillActiveEnemiesToCount(int targetActive)
        {
            targetActive = Mathf.Min(targetActive, Spawner.GetEnemyCapForDebug());
            while (EnemyWorld.ActiveCount < targetActive)
            {
                var offset = EnemyWorld.ActiveCount * 1.1f;
                var slot = EnemyWorld.Spawn(0, new Vector3(40f + offset, 0f, 40f + offset));
                if (slot < 0)
                    break;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(_root);
            else
                UnityEngine.Object.DestroyImmediate(_root);
        }
    }
}
