using System.Collections.Generic;
using PirateGame.Combat;
using UnityEngine;

namespace PirateGame.Enemies
{
    /// <summary>
    /// Spawns enemies from a configurable list of models and health values.
    /// Drop on an empty GameObject in the scene and assign Enemy Definitions (or inline types).
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [System.Serializable]
        public class SpawnType
        {
            [Tooltip("Optional shared definition. When set, prefab/health/speed/weight below are ignored unless Override Definition is on.")]
            public EnemyDefinition definition;

            [Tooltip("If true, use the inline Prefab / Max Health / Move Speed / Weight on this row instead of the definition.")]
            public bool overrideDefinition;

            [Tooltip("Enemy model prefab. Required when Definition is empty or Override is on.")]
            public GameObject prefab;

            [Min(1f)]
            public float maxHealth = 30f;

            [Min(0f)]
            public float moveSpeed = 4f;

            [Min(0f)]
            public float spawnWeight = 1f;

            public bool TryResolve(out GameObject resolvedPrefab, out float health, out float speed, out float weight)
            {
                resolvedPrefab = null;
                health = 30f;
                speed = 4f;
                weight = 0f;

                var useInline = definition == null || overrideDefinition;
                if (useInline)
                {
                    resolvedPrefab = prefab;
                    health = maxHealth;
                    speed = moveSpeed;
                    weight = spawnWeight;
                }
                else
                {
                    resolvedPrefab = definition.prefab;
                    health = definition.maxHealth;
                    speed = definition.moveSpeed;
                    weight = definition.spawnWeight;
                }

                return resolvedPrefab != null && weight > 0f;
            }

            public string DisplayLabel
            {
                get
                {
                    if (!overrideDefinition && definition != null && !string.IsNullOrEmpty(definition.displayName))
                        return definition.displayName;
                    if (prefab != null)
                        return prefab.name;
                    if (definition != null)
                        return definition.name;
                    return "Empty";
                }
            }
        }

        [Header("Enemy Types")]
        [Tooltip("Add one row per enemy model. Assign a Definition asset and/or set Prefab + Max Health.")]
        [SerializeField] List<SpawnType> enemyTypes = new List<SpawnType>();

        [Header("Target")]
        [Tooltip("Player ship. Auto-found by Player tag if left empty.")]
        [SerializeField] Transform player;

        [Header("Timing")]
        [SerializeField] bool spawnOnStart = true;
        [SerializeField] bool autoSpawn = true;
        [SerializeField] float spawnInterval = 3f;
        [SerializeField] int enemiesPerSpawn = 1;
        [SerializeField] int maxAlive = 20;

        [Header("Placement")]
        [Tooltip("Optional fixed points. If empty, enemies spawn on a ring around the player (or this transform).")]
        [SerializeField] Transform[] spawnPoints;
        [SerializeField] float minSpawnRadius = 25f;
        [SerializeField] float maxSpawnRadius = 40f;
        [SerializeField] float spawnHeight = 0f;

        [Header("Scaling")]
        [Tooltip("Multiplies every spawned enemy's health (tide / difficulty).")]
        [SerializeField] float healthMultiplier = 1f;

        [Header("Debug")]
        [SerializeField] bool logSpawns;

        readonly List<GameObject> aliveEnemies = new List<GameObject>();
        float spawnTimer;

        public int AliveCount
        {
            get
            {
                PruneDead();
                return aliveEnemies.Count;
            }
        }

        public IReadOnlyList<SpawnType> EnemyTypes => enemyTypes;

        void Start()
        {
            ResolvePlayer();
            spawnTimer = spawnInterval;

            if (spawnOnStart)
                SpawnWave(enemiesPerSpawn);
        }

        void Update()
        {
            if (!autoSpawn) return;

            spawnTimer -= Time.deltaTime;
            if (spawnTimer > 0f) return;

            spawnTimer = spawnInterval;
            SpawnWave(enemiesPerSpawn);
        }

        public void SpawnWave(int count)
        {
            for (var i = 0; i < count; i++)
                SpawnRandom();
        }

        public GameObject SpawnRandom()
        {
            if (!TryPickType(out var type))
                return null;

            return Spawn(type);
        }

        public GameObject Spawn(SpawnType type)
        {
            if (type == null || !type.TryResolve(out var prefab, out var health, out var speed, out _))
            {
                Debug.LogWarning($"{nameof(EnemySpawner)} on '{name}' has an invalid enemy type.", this);
                return null;
            }

            PruneDead();
            if (aliveEnemies.Count >= maxAlive)
                return null;

            ResolvePlayer();
            var position = GetSpawnPosition();
            var rotation = GetSpawnRotation(position);

            var instance = Instantiate(prefab, position, rotation);
            ConfigureInstance(instance, health * Mathf.Max(0.01f, healthMultiplier), speed);
            aliveEnemies.Add(instance);

            if (logSpawns)
                Debug.Log($"Spawned {type.DisplayLabel} (HP {health * healthMultiplier:0.#}) at {position}", instance);

            return instance;
        }

        public GameObject Spawn(EnemyDefinition definition, Vector3? position = null)
        {
            if (definition == null || definition.prefab == null)
            {
                Debug.LogWarning($"{nameof(EnemySpawner)}: missing EnemyDefinition or prefab.", this);
                return null;
            }

            PruneDead();
            if (aliveEnemies.Count >= maxAlive)
                return null;

            ResolvePlayer();
            var pos = position ?? GetSpawnPosition();
            var rotation = GetSpawnRotation(pos);

            var instance = Instantiate(definition.prefab, pos, rotation);
            ConfigureInstance(instance, definition.maxHealth * Mathf.Max(0.01f, healthMultiplier), definition.moveSpeed);
            aliveEnemies.Add(instance);
            return instance;
        }

        public void ClearAliveEnemies()
        {
            PruneDead();
            for (var i = aliveEnemies.Count - 1; i >= 0; i--)
            {
                if (aliveEnemies[i] != null)
                    Destroy(aliveEnemies[i]);
            }

            aliveEnemies.Clear();
        }

        void ConfigureInstance(GameObject instance, float health, float speed)
        {
            if (!instance.CompareTag("Enemy"))
                instance.tag = "Enemy";

            var healthComponent = instance.GetComponent<Health>();
            if (healthComponent == null)
                healthComponent = instance.AddComponent<Health>();

            healthComponent.Initialize(health);

            if (instance.GetComponent<EnemySetup>() == null)
                instance.AddComponent<EnemySetup>();

            var chase = instance.GetComponent<EnemyChase>();
            if (chase == null)
                chase = instance.AddComponent<EnemyChase>();

            if (player != null)
                chase.Initialize(player, speed);
            else
                chase.MoveSpeed = speed;
        }

        bool TryPickType(out SpawnType picked)
        {
            picked = null;
            var totalWeight = 0f;
            for (var i = 0; i < enemyTypes.Count; i++)
            {
                if (enemyTypes[i] != null && enemyTypes[i].TryResolve(out _, out _, out _, out var weight))
                    totalWeight += weight;
            }

            if (totalWeight <= 0f)
            {
                if (enemyTypes.Count > 0)
                    Debug.LogWarning($"{nameof(EnemySpawner)} on '{name}' has no valid enemy types with prefabs and weight > 0.", this);
                return false;
            }

            var roll = Random.Range(0f, totalWeight);
            var cumulative = 0f;
            for (var i = 0; i < enemyTypes.Count; i++)
            {
                var type = enemyTypes[i];
                if (type == null || !type.TryResolve(out _, out _, out _, out var weight))
                    continue;

                cumulative += weight;
                if (roll <= cumulative)
                {
                    picked = type;
                    return true;
                }
            }

            picked = enemyTypes[enemyTypes.Count - 1];
            return picked != null;
        }

        Vector3 GetSpawnPosition()
        {
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                var point = spawnPoints[Random.Range(0, spawnPoints.Length)];
                if (point != null)
                {
                    var p = point.position;
                    p.y = spawnHeight;
                    return p;
                }
            }

            var origin = player != null ? player.position : transform.position;
            var angle = Random.Range(0f, Mathf.PI * 2f);
            var radius = Random.Range(minSpawnRadius, Mathf.Max(minSpawnRadius, maxSpawnRadius));
            return new Vector3(
                origin.x + Mathf.Cos(angle) * radius,
                spawnHeight,
                origin.z + Mathf.Sin(angle) * radius);
        }

        Quaternion GetSpawnRotation(Vector3 spawnPosition)
        {
            if (player == null)
                return Quaternion.identity;

            var toPlayer = player.position - spawnPosition;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.001f)
                return Quaternion.identity;

            return Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
        }

        void ResolvePlayer()
        {
            if (player != null) return;

            var playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                player = playerObject.transform;
        }

        void PruneDead()
        {
            for (var i = aliveEnemies.Count - 1; i >= 0; i--)
            {
                if (aliveEnemies[i] == null)
                    aliveEnemies.RemoveAt(i);
            }
        }

        void OnDrawGizmosSelected()
        {
            var origin = player != null ? player.position : transform.position;
            origin.y = spawnHeight;

            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(origin, minSpawnRadius);
            Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.6f);
            Gizmos.DrawWireSphere(origin, maxSpawnRadius);

            if (spawnPoints == null) return;
            Gizmos.color = Color.yellow;
            foreach (var point in spawnPoints)
            {
                if (point == null) continue;
                Gizmos.DrawSphere(point.position, 0.6f);
            }
        }
    }
}
