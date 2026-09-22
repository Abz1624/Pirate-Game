using System.Collections.Generic;
using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Encounters;
using HighSeas.POI;
using HighSeas.V3;
using HighSeas.VFX;
using HighSeas.World;
using UnityEngine;

namespace HighSeas.Enemies
{
    public struct EnemyAgent
    {
        public int id;
        public bool active;
        public Vector3 position;
        public Vector3 velocity;
        public float hp;
        public float maxHp;
        public float damage;
        public float moveSpeed;
        public float radius;
        public float contactCooldown;
        public float chargeTimer;
        public float attackCooldown;
        public float knockbackTimer;
        public Vector3 knockbackDir;
        public int definitionIndex;
        public EliteAffix affix;
        public bool isElite;
        public bool isBoss;
        public Transform view;
        public Transform healthBar;
        public string lastHitWeaponId;
        public bool swordfishSpringVisual;
        public SpawnProfile spawnProfile;
        public EnemySpawnRuntimeParams patternParams;
    }

    public sealed class SpatialHash
    {
        readonly Dictionary<long, List<int>> _cells = new();
        readonly float _cellSize;
        readonly List<int> _queryBuffer = new(64);

        public SpatialHash(float cellSize = 4f)
        {
            _cellSize = cellSize;
        }

        static long Key(int x, int z) => ((long)x << 32) | (uint)z;

        public void Clear() => _cells.Clear();

        public void Insert(int enemyId, Vector3 pos)
        {
            var key = Key(Mathf.FloorToInt(pos.x / _cellSize), Mathf.FloorToInt(pos.z / _cellSize));
            if (!_cells.TryGetValue(key, out var list))
            {
                list = new List<int>(8);
                _cells[key] = list;
            }
            list.Add(enemyId);
        }

        public List<int> Query(Vector3 center, float radius, int maxResults = 32)
        {
            _queryBuffer.Clear();
            var rCells = Mathf.CeilToInt(radius / _cellSize);
            var cx = Mathf.FloorToInt(center.x / _cellSize);
            var cz = Mathf.FloorToInt(center.z / _cellSize);
            for (var dx = -rCells; dx <= rCells; dx++)
            {
                for (var dz = -rCells; dz <= rCells; dz++)
                {
                    if (!_cells.TryGetValue(Key(cx + dx, cz + dz), out var list)) continue;
                    for (var i = 0; i < list.Count; i++)
                    {
                        if (_queryBuffer.Count >= maxResults) return _queryBuffer;
                        _queryBuffer.Add(list[i]);
                    }
                }
            }
            return _queryBuffer;
        }
    }

    public sealed class EnemyWorld : MonoBehaviour
    {
        [SerializeField] Transform enemyRoot;
        [SerializeField] Data.EnemyDefinition[] definitions;
        [SerializeField] bool collideWithIslands = true;
        [SerializeField] float islandAvoidanceProbeDistance = 10f;
        [SerializeField] float islandCollisionPadding = 0.35f;
        [SerializeField] float shipCollisionPadding = 0.55f;
        [SerializeField] float enemySeparationPadding = 0.2f;
        [SerializeField] DemoJuiceSettings juiceSettings;

        readonly SpatialHash _hash = new(4f);
        EnemyAgent[] _agents;
        EnemyViewPool _viewPool;
        int _activeCount;
        int _nextId = 1;
        float _experimentHpMultiplier = 1f;
        float _experimentDamageMultiplier = 1f;
        int _bossControlSlot = -1;
        bool _bossPauseMovement;
        bool _bossUseForcedVelocity;
        Vector3 _bossForcedVelocity;

        public int ActiveCount => _activeCount;
        public EnemyAgent[] Agents => _agents;
        public Data.EnemyDefinition[] Definitions => definitions;

        public void SetExperimentMultipliers(float hpMultiplier, float damageMultiplier)
        {
            _experimentHpMultiplier = Mathf.Max(0.1f, hpMultiplier);
            _experimentDamageMultiplier = Mathf.Max(0.1f, damageMultiplier);
        }

        public bool TryFindActiveBoss(out int slot, out EnemyAgent agent)
        {
            slot = -1;
            agent = default;
            if (_agents == null) return false;
            for (var i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].active || !_agents[i].isBoss) continue;
                slot = i;
                agent = _agents[i];
                return true;
            }

            return false;
        }

        public void SetBossAttackControl(int slot, bool pauseMovement, Vector3 forcedVelocity, bool useForced)
        {
            _bossControlSlot = slot;
            _bossPauseMovement = pauseMovement;
            _bossForcedVelocity = forcedVelocity;
            _bossUseForcedVelocity = useForced;
        }

        public void ClearBossAttackControl()
        {
            _bossControlSlot = -1;
            _bossPauseMovement = false;
            _bossUseForcedVelocity = false;
            _bossForcedVelocity = Vector3.zero;
        }

        void Awake()
        {
            var cap = Platform.QualityProfile.Current.enemyCap;
            _agents = new EnemyAgent[cap];
            _viewPool = new EnemyViewPool(enemyRoot != null ? enemyRoot : transform);
            if (juiceSettings == null)
                juiceSettings = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
        }

        public void SetDefinitions(Data.EnemyDefinition[] defs)
        {
            definitions = defs;
            if (defs == null) return;
            for (var i = 0; i < defs.Length; i++)
                EnemyViewPool.StripNavMeshFromPrefab(defs[i]?.visualPrefab);
        }

        public int CountActiveByDefinition(int definitionIndex)
        {
            var count = 0;
            for (var i = 0; i < _agents.Length; i++)
            {
                if (_agents[i].active && _agents[i].definitionIndex == definitionIndex)
                    count++;
            }
            return count;
        }

        public int Spawn(
            int definitionIndex,
            Vector3 position,
            EliteAffix affix = EliteAffix.None,
            float speedMultiplier = 1f,
            float hpMultiplier = 1f,
            float damageMultiplier = 1f)
        {
            if (definitions == null || definitionIndex < 0 || definitionIndex >= definitions.Length) return -1;
            var def = definitions[definitionIndex];
            var slot = -1;
            for (var i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].active)
                {
                    slot = i;
                    break;
                }
            }
            if (slot < 0) return -1;

            position = PlayBounds.Clamp(position, def.radius + 1f);
            position.y = def.visualHeightOffset;

            var minute = RunManager.Instance != null ? RunManager.Instance.ElapsedSeconds / 60f : 0f;
            var tier = RunManager.Instance?.Tier ?? 1;
            var stage = RunManager.Instance?.StageIndex ?? 0;
            var hp = GameMath.EnemyHP(def.baseHP, minute, tier, stage);
            var dmg = GameMath.EnemyDamage(def.baseDamage, minute, tier, stage);
            if (affix == EliteAffix.Armored) hp *= 1.8f;
            if (affix == EliteAffix.Swift) hp *= 0.9f;
            if (affix == EliteAffix.Rich) hp *= 1.25f;
            hp *= Mathf.Max(0.1f, hpMultiplier) * _experimentHpMultiplier;
            dmg *= Mathf.Max(0.1f, damageMultiplier) * _experimentDamageMultiplier;

            var v3Scale = V3.V3RunContext.IsActive ? V3.V3RunContext.EnemyVisualScale : 1f;
            var visualScale = def.visualScale * v3Scale * (affix != EliteAffix.None ? 1.38f : 1f);
            var enemyId = _nextId;
            var view = CreateView(def, position, visualScale, affix != EliteAffix.None, enemyId);

            var baseSpeed = def.moveSpeed * Mathf.Max(0.1f, speedMultiplier);
            if (affix == EliteAffix.Swift) baseSpeed *= 1.35f;

            _agents[slot] = new EnemyAgent
            {
                id = _nextId++,
                active = true,
                position = position,
                hp = hp,
                maxHp = hp,
                damage = dmg,
                moveSpeed = baseSpeed,
                radius = def.radius,
                definitionIndex = definitionIndex,
                affix = affix,
                isElite = affix != EliteAffix.None,
                isBoss = def.isBoss,
                chargeTimer = def.id == "swordfish" ? 0f : 2f,
                attackCooldown = GetInitialAttackCooldown(def),
                swordfishSpringVisual = false,
                view = view
            };

            if (collideWithIslands && IslandObstacles.IsAvailable)
            {
                var collisionRadius = GetCollisionRadius(def);
                var height = IslandObstacles.GetCollisionHeight(collisionRadius);
                _agents[slot].position = IslandObstacles.Depenetrate(position, collisionRadius, height);
                if (view != null)
                    view.position = _agents[slot].position;
            }

            _activeCount++;
            AttachHealthBar(slot, def, view, affix != EliteAffix.None);
            return slot;
        }

        public int SpawnFromProfile(
            int definitionIndex,
            Vector3 position,
            SpawnProfile profile,
            int profileInstanceId,
            FormationRole formationRole,
            int formationSlot,
            int formationLeaderAgentId,
            float hpMultiplier = 1f,
            float damageMultiplier = 1f)
        {
            if (profile == null)
                return -1;

            var elite = profile.elite;
            var affix = elite.isElite ? elite.affix : EliteAffix.None;
            if (elite.isElite && affix == EliteAffix.None)
                affix = EliteAffix.Rich;

            var speedMult = profile.movement.speedMultiplier * (elite.isElite ? elite.speedMultiplier : 1f);
            var slot = Spawn(
                definitionIndex,
                position,
                affix,
                speedMult,
                hpMultiplier * (elite.isElite ? elite.hpMultiplier : 1f),
                damageMultiplier * (elite.isElite ? elite.damageMultiplier : 1f));

            if (slot < 0)
                return -1;

            var leaderUnitId = formationLeaderAgentId;
            if (formationRole == FormationRole.Leader)
                leaderUnitId = _agents[slot].id;

            _agents[slot].spawnProfile = profile;
            _agents[slot].patternParams = SpawnProfileResolver.Resolve(
                profile, profileInstanceId, formationRole, formationSlot, leaderUnitId);
            if (_agents[slot].patternParams.attackCooldown > 0f)
                _agents[slot].attackCooldown = _agents[slot].patternParams.attackCooldown * 0.5f;
            if (_agents[slot].patternParams.movementPattern == EnemyMovementPattern.TelegraphedCharge)
            {
                var chargeCooldown = _agents[slot].patternParams.chargeCooldown;
                _agents[slot].chargeTimer = chargeCooldown > 0f ? chargeCooldown : 2f;
            }

            if (elite.isElite && elite.sizeMultiplier > 0.01f && _agents[slot].view != null)
                _agents[slot].view.localScale *= elite.sizeMultiplier;

            return slot;
        }

        public bool TryGetAgentByUnitId(int unitId, out int agentIndex)
        {
            for (var i = 0; i < _agents.Length; i++)
            {
                if (_agents[i].active && _agents[i].id == unitId)
                {
                    agentIndex = i;
                    return true;
                }
            }

            agentIndex = -1;
            return false;
        }

        /// <summary>Scheduled pressure-beat elite: 6× HP, 1.25× damage (combat spec §9).</summary>
        public int SpawnPressureBeat(int definitionIndex, Vector3 position) =>
            Spawn(definitionIndex, position, EliteAffix.Rich, 1f, 6f, 1.25f);

        /// <summary>Orients active enemy visuals toward a world point (promo shots, paused tableau).</summary>
        public void SnapAllActiveViewsToward(Vector3 worldTarget)
        {
            if (definitions == null) return;

            for (var i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].active || _agents[i].view == null) continue;
                var def = definitions[_agents[i].definitionIndex];
                var faceDirection = worldTarget - _agents[i].position;
                faceDirection.y = 0f;
                if (faceDirection.sqrMagnitude < 0.0001f) continue;

                var facing = Quaternion.LookRotation(faceDirection.normalized, Vector3.up);
                if (def != null && Mathf.Abs(def.visualYawOffset) > 0.01f)
                    facing *= Quaternion.Euler(0f, def.visualYawOffset, 0f);
                _agents[i].view.rotation = facing;
            }
        }

        void AttachHealthBar(int slot, Data.EnemyDefinition def, Transform view, bool elite)
        {
            if (juiceSettings?.enemyHealthBarPrefab == null || view == null) return;

            var barGo = Object.Instantiate(juiceSettings.enemyHealthBarPrefab);
            var bar = barGo.GetComponent<WorldHealthBar>();
            if (bar == null)
                bar = barGo.AddComponent<WorldHealthBar>();
            bar.Bind(view, juiceSettings.enemyHealthBarHeight);
            bar.SetFill(1f);
            _agents[slot].healthBar = barGo.transform;
        }

        Transform CreateView(Data.EnemyDefinition def, Vector3 position, float scale, bool elite, int animVariationSeed)
        {
            if (def.visualPrefab != null)
            {
                var yaw = Quaternion.Euler(0f, def.visualYawOffset, 0f);
                var view = _viewPool.Acquire(def.visualPrefab, position, yaw, scale);
                EnemyViewAnimation.ApplySpawnVariation(view, animVariationSeed);
                return view;
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = string.IsNullOrEmpty(def.displayName) ? def.id : def.displayName;
            go.transform.SetParent(enemyRoot != null ? enemyRoot : transform, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * (def.radius * 2f);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
                rend.material.color = elite ? Color.magenta : def.placeholderColor;
            return go.transform;
        }

        public void DespawnAllActive()
        {
            if (_agents == null) return;
            for (var i = 0; i < _agents.Length; i++)
            {
                if (_agents[i].active)
                    Despawn(i);
            }
        }

        /// <summary>Removes the active enemy with the lowest spawn id (earliest spawned).</summary>
        public bool TryDespawnOldestActive(out int despawnedUnitId)
        {
            despawnedUnitId = -1;
            var oldestSlot = -1;
            var oldestId = int.MaxValue;
            for (var i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].active) continue;
                if (_agents[i].id >= oldestId) continue;
                oldestId = _agents[i].id;
                oldestSlot = i;
            }

            if (oldestSlot < 0) return false;
            despawnedUnitId = oldestId;
            Despawn(oldestSlot);
            return true;
        }

        public void Despawn(int index)
        {
            if (index < 0 || index >= _agents.Length || !_agents[index].active) return;

            var despawnProfile = _agents[index].spawnProfile;
            var def = definitions[_agents[index].definitionIndex];
            EnemyBurnFlash.SetBurning(_agents[index].view, false);
            if (_agents[index].view != null)
            {
                if (def.visualPrefab != null)
                    _viewPool.Release(_agents[index].view, def.visualPrefab);
                else
                    Destroy(_agents[index].view.gameObject);
            }

            if (_agents[index].healthBar != null)
            {
                Destroy(_agents[index].healthBar.gameObject);
                _agents[index].healthBar = null;
            }

            var releasedAgentId = _agents[index].id;
            _agents[index].active = false;
            _activeCount--;
            Combat.StatusController.Instance?.ClearIndex(index);
            RunEncounterTracker.ReleaseAgentMapping(releasedAgentId);
            EncounterSpawner.Instance?.NotifyAgentDespawned(despawnProfile);
        }

        public void Tick(float dt, Vector3 playerPos, Combat.DamageReceiver playerDamage)
        {
            var playerVelocity = ResolvePlayerVelocity(playerDamage);
            _hash.Clear();
            for (var i = 0; i < _agents.Length; i++)
            {
                if (_agents[i].active) _hash.Insert(i, _agents[i].position);
            }

            for (var i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].active) continue;
                ref var e = ref _agents[i];
                var def = definitions[e.definitionIndex];
                EnemySpecialBehaviorSystem.TickPreMovement(this, i, ref e, def, dt, playerPos, playerDamage);
                var desiredVelocity = TickBehavior(i, ref e, def, dt, playerPos, playerVelocity);
                if (e.spawnProfile != null)
                {
                    EnemySpecialBehaviorSystem.TickPostMovement(this, ref e, ref e.patternParams, dt, playerPos);
                    desiredVelocity = e.velocity;
                }

                MoveAgent(ref e, dt, desiredVelocity, def);
                e.position.y = def.visualHeightOffset;
                UpdateView(ref e, def, dt, playerPos);

                var dist = HorizontalDistance(e.position, playerPos);
                TickRangedAttack(ref e, def, dt, playerPos, dist, playerDamage);
                var playerRadius = GetPlayerContactRadius(playerDamage);
                var contactRange = e.radius + playerRadius;
                if (def.category == EnemyCategory.Creature && !def.isBoss && !def.isMiniboss)
                    contactRange = GetMinimumCreatureHullDistance(playerRadius, e.radius);

                if (dist < contactRange && e.contactCooldown <= 0f)
                {
                    var contactEnabled = e.spawnProfile == null || e.patternParams.contactDamageEnabled;
                    if (contactEnabled)
                    {
                        playerDamage?.TakeDamage(e.damage, e.position, false);
                        e.contactCooldown = GameConstants.EnemyContactCooldown;
                        TryApplyCreatureContactKnockback(ref e, def, playerPos);
                        PushCreatureOffHull(ref e, playerPos, playerRadius);
                    }
                }
                if (e.contactCooldown > 0f) e.contactCooldown -= dt;
            }

            ResolvePlayerHullSeparation(playerPos, playerDamage);
            ResolveAgentOverlaps();
        }

        void ResolvePlayerHullSeparation(Vector3 playerPos, Combat.DamageReceiver playerDamage)
        {
            if (playerDamage == null) return;

            var playerRadius = GetPlayerContactRadius(playerDamage);
            for (var i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].active) continue;
                ref var agent = ref _agents[i];
                var def = definitions[agent.definitionIndex];
                if (def.category != EnemyCategory.Creature || def.isBoss || def.isMiniboss)
                    continue;

                var padding = GameConstants.CreatureHullSeparationPadding;
                var required = playerRadius + agent.radius + padding;
                var offset = agent.position - playerPos;
                offset.y = 0f;
                var dist = offset.magnitude;
                if (dist >= required)
                    continue;

                if (dist < 0.001f)
                    offset = new Vector3(agent.id % 2 == 0 ? 1f : -1f, 0f, 0.35f);

                agent.position = playerPos + offset.normalized * required;
                var toPlayer = playerPos - agent.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.01f)
                {
                    var approach = Vector3.Dot(agent.velocity, toPlayer.normalized);
                    if (approach > 0f)
                        agent.velocity -= toPlayer.normalized * approach;
                }

                if (agent.view != null)
                    agent.view.position = agent.position;
            }
        }

        static float GetMinimumCreatureHullDistance(float playerRadius, float enemyRadius) =>
            playerRadius + enemyRadius + GameConstants.CreatureHullSeparationPadding;

        static Vector3 ResolvePlayerVelocity(Combat.DamageReceiver playerDamage)
        {
            if (playerDamage == null) return Vector3.zero;
            var ship = playerDamage.GetComponent<Player.PlayerShipController>();
            if (ship == null) return Vector3.zero;
            var velocity = ship.Velocity;
            velocity.y = 0f;
            return velocity;
        }

        static void TryApplyCreatureContactKnockback(ref EnemyAgent agent, Data.EnemyDefinition def, Vector3 playerPos)
        {
            if (def.category != EnemyCategory.Creature || def.isBoss || def.isMiniboss)
                return;

            var away = agent.position - playerPos;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
                away = new Vector3(agent.id % 2 == 0 ? 1f : -1f, 0f, 0.6f);
            agent.knockbackDir = away.normalized;
            agent.knockbackTimer = GameConstants.CreatureKnockbackSeconds;
        }

        static void PushCreatureOffHull(ref EnemyAgent agent, Vector3 playerPos, float playerRadius)
        {
            var required = GetMinimumCreatureHullDistance(playerRadius, agent.radius);
            var offset = agent.position - playerPos;
            offset.y = 0f;
            if (offset.sqrMagnitude < 0.0001f)
                offset = agent.knockbackDir.sqrMagnitude > 0.01f ? agent.knockbackDir : Vector3.forward;
            agent.position = playerPos + offset.normalized * required;
        }

        static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static float GetPlayerContactRadius(Combat.DamageReceiver playerDamage)
        {
            if (playerDamage == null) return 1.15f;
            var ship = playerDamage.GetComponent<Player.PlayerShipController>();
            return ship != null ? ship.IslandCollisionRadius : 1.15f;
        }

        float GetCollisionRadius(Data.EnemyDefinition def)
        {
            var padding = def.category == EnemyCategory.Ship ? shipCollisionPadding : islandCollisionPadding;
            return def.radius + padding;
        }

        public float GetCollisionRadiusForAgent(in EnemyAgent agent)
        {
            if (definitions == null || agent.definitionIndex < 0 || agent.definitionIndex >= definitions.Length)
                return agent.radius;
            return GetCollisionRadius(definitions[agent.definitionIndex]);
        }

        public void EnsureAgentCapacity(int minimum)
        {
            minimum = Mathf.Max(64, minimum);
            if (_agents != null && _agents.Length >= minimum)
                return;

            var resized = new EnemyAgent[minimum];
            if (_agents != null)
            {
                var copy = Mathf.Min(_agents.Length, resized.Length);
                for (var i = 0; i < copy; i++)
                    resized[i] = _agents[i];
            }

            _agents = resized;
        }

        public void ResetAllAgentsForPromo()
        {
            for (var i = 0; i < _agents.Length; i++)
            {
                if (_agents[i].active)
                    Despawn(i);
            }

            _activeCount = 0;
            _viewPool?.DestroyAllPooledInstances();
        }

        public void ResolveSpawnOverlapsOnly(int passes = 3)
        {
            for (var pass = 0; pass < passes; pass++)
                ResolveAgentOverlaps();
        }

        /// <summary>Push agents apart and off islands after promo / debug spawns (no simulation tick).</summary>
        public void ResolveSpawnLayout(int overlapPasses = 4)
        {
            for (var pass = 0; pass < overlapPasses; pass++)
            {
                ResolveAgentOverlaps();
                if (!collideWithIslands || !IslandObstacles.IsAvailable)
                    continue;

                for (var i = 0; i < _agents.Length; i++)
                {
                    if (!_agents[i].active) continue;
                    ref var e = ref _agents[i];
                    var def = definitions[e.definitionIndex];
                    var collisionRadius = GetCollisionRadius(def);
                    var height = IslandObstacles.GetCollisionHeight(collisionRadius);
                    e.position = IslandObstacles.Depenetrate(e.position, collisionRadius, height);
                    if (e.view != null)
                        e.view.position = e.position;
                }
            }
        }

        void ResolveAgentOverlaps()
        {
            for (var pass = 0; pass < 2; pass++)
            {
                for (var i = 0; i < _agents.Length; i++)
                {
                    if (!_agents[i].active) continue;
                    ref var self = ref _agents[i];

                    for (var j = i + 1; j < _agents.Length; j++)
                    {
                        if (!_agents[j].active) continue;
                        ref var other = ref _agents[j];

                        var delta = self.position - other.position;
                        delta.y = 0f;
                        var minDist = self.radius + other.radius + enemySeparationPadding;
                        var dist = delta.magnitude;
                        if (dist >= minDist)
                            continue;

                        Vector3 push;
                        if (dist > 0.001f)
                            push = delta.normalized * ((minDist - dist) * 0.5f);
                        else
                            push = new Vector3((self.id % 2 == 0 ? 1f : -1f) * minDist * 0.5f, 0f, 0f);

                        self.position += push;
                        other.position -= push;
                    }
                }
            }
        }

        void MoveAgent(ref EnemyAgent e, float dt, Vector3 desiredVelocity, Data.EnemyDefinition def)
        {
            var delta = e.velocity * dt;
            if (!collideWithIslands || !IslandObstacles.IsAvailable)
            {
                e.position += delta;
                return;
            }

            var collisionRadius = GetCollisionRadius(def);
            var preferredDirection = desiredVelocity;
            preferredDirection.y = 0f;

            e.velocity = IslandObstacles.ApplyAvoidanceSteering(
                e.position,
                e.velocity,
                preferredDirection,
                collisionRadius,
                islandAvoidanceProbeDistance,
                e.id);

            delta = e.velocity * dt;
            var height = IslandObstacles.GetCollisionHeight(collisionRadius);
            e.position = IslandObstacles.ResolveCapsuleMovement(
                e.position,
                delta,
                collisionRadius,
                height,
                ref e.velocity,
                preferredDirection);

            e.position = IslandObstacles.Depenetrate(e.position, collisionRadius, height);
        }

        void UpdateView(ref EnemyAgent e, Data.EnemyDefinition def, float dt, Vector3 playerPos)
        {
            if (e.view == null) return;

            e.view.position = e.position;

            if (def.id == "swordfish")
                EnemyViewAnimation.TickSwordfish(e.view, ref e.swordfishSpringVisual, e.chargeTimer > 0f);

            Vector3 faceDirection;
            if (e.velocity.sqrMagnitude >= 0.01f)
                faceDirection = e.velocity;
            else if (def.behavior == EnemyBehavior.TelegraphedCharge)
            {
                faceDirection = playerPos - e.position;
                faceDirection.y = 0f;
                if (faceDirection.sqrMagnitude < 0.01f)
                    return;
            }
            else
            {
                return;
            }

            var facing = Quaternion.LookRotation(faceDirection.normalized, Vector3.up);
            if (Mathf.Abs(def.visualYawOffset) > 0.01f)
                facing *= Quaternion.Euler(0f, def.visualYawOffset, 0f);

            e.view.rotation = Quaternion.Slerp(e.view.rotation, facing, 1f - Mathf.Exp(-12f * dt));
        }

        Vector3 TickBehavior(int agentIndex, ref EnemyAgent e, Data.EnemyDefinition def, float dt, Vector3 playerPos, Vector3 playerVelocity)
        {
            if (e.isBoss && agentIndex == _bossControlSlot)
            {
                if (_bossPauseMovement)
                {
                    e.velocity = Vector3.zero;
                    return Vector3.zero;
                }

                if (_bossUseForcedVelocity)
                {
                    e.velocity = _bossForcedVelocity;
                    return _bossForcedVelocity;
                }
            }

            if (e.knockbackTimer > 0f)
            {
                e.knockbackTimer -= dt;
                var fade = Mathf.Clamp01(e.knockbackTimer / GameConstants.CreatureKnockbackSeconds);
                return e.knockbackDir * (e.moveSpeed * (1.6f + 0.9f * fade));
            }

            var toPlayer = playerPos - e.position;
            toPlayer.y = 0f;
            var dist = toPlayer.magnitude;
            var dir = dist > 0.01f ? toPlayer / dist : Vector3.forward;
            var usesCreatureIntercept = def.category == EnemyCategory.Creature && !def.isBoss && !def.isMiniboss;
            var chaseDir = usesCreatureIntercept
                ? GetCreatureInterceptDirection(e, playerPos, playerVelocity, dist, dir)
                : GetEncircleChaseDirection(e.id, e.position, playerPos, dist, dir);

            if (e.spawnProfile != null)
            {
                Vector3 patternVel;
                if (def.behavior == EnemyBehavior.DiveLunge)
                {
                    patternVel = TickSwordfishBehavior(ref e, chaseDir, dir, dist, dt, playerPos, playerVelocity);
                }
                else
                {
                    patternVel = EnemyPatternMovement.ComputeVelocity(
                        e,
                        e.patternParams,
                        chaseDir,
                        dir,
                        dist,
                        dt,
                        ref e.chargeTimer);
                }

                var patternIntent = patternVel;
                patternVel = ApplySeparation(agentIndex, patternVel, ref e, patternIntent);
                e.velocity = Vector3.ClampMagnitude(patternVel, e.moveSpeed * 1.5f);
                return patternIntent;
            }

            if (def.id == "swordfish")
                return TickSwordfishBehavior(ref e, chaseDir, dir, dist, dt, playerPos, playerVelocity);

            Vector3 behaviorVelocity;

            switch (def.behavior)
            {
                case EnemyBehavior.DirectChaser:
                case EnemyBehavior.TankChaser:
                case EnemyBehavior.DiveLunge:
                    behaviorVelocity = chaseDir * e.moveSpeed;
                    break;
                case EnemyBehavior.FlankingChaser:
                {
                    var aimDir = usesCreatureIntercept ? chaseDir : dir;
                    var flankSign = (e.id & 1) == 0 ? 1f : -1f;
                    var flank = Vector3.Cross(Vector3.up, aimDir) * flankSign;
                    behaviorVelocity = (aimDir + flank * 0.35f).normalized * e.moveSpeed;
                    break;
                }
                case EnemyBehavior.TelegraphedCharge:
                    e.chargeTimer -= dt;
                    if (e.chargeTimer <= 0f)
                        behaviorVelocity = dir * (e.moveSpeed * 2.5f);
                    else if (e.chargeTimer > 1.5f)
                        behaviorVelocity = dir * (e.moveSpeed * 0.2f);
                    else
                        behaviorVelocity = Vector3.zero;
                    if (e.chargeTimer <= -0.35f)
                        e.chargeTimer = 2f;
                    break;
                case EnemyBehavior.MaintainRange:
                case EnemyBehavior.FormationRanged:
                {
                    if (def.category == EnemyCategory.Ship && e.attackCooldown > 0f)
                    {
                        behaviorVelocity = Vector3.zero;
                        break;
                    }

                    var preferred = def.preferredRange > 0f ? def.preferredRange : 10f;
                    if (dist > preferred + 3f)
                        behaviorVelocity = dir * (e.moveSpeed * 0.65f);
                    else if (dist < preferred - 2f)
                    {
                        var strafeSign = (e.id & 1) == 0 ? 1f : -1f;
                        var tangent = Vector3.Cross(Vector3.up, dir) * strafeSign;
                        behaviorVelocity = tangent * (e.moveSpeed * 0.35f);
                    }
                    else
                        behaviorVelocity = Vector3.zero;
                    break;
                }
                case EnemyBehavior.SuicideApproach:
                    behaviorVelocity = chaseDir * (e.moveSpeed * 1.4f);
                    break;
                case EnemyBehavior.StrafeBurst:
                {
                    var tangent = Vector3.Cross(Vector3.up, dir);
                    behaviorVelocity = tangent * e.moveSpeed + dir * (e.moveSpeed * 0.25f);
                    e.attackCooldown -= dt;
                    break;
                }
                case EnemyBehavior.FastSwarm:
                    behaviorVelocity = chaseDir * (e.moveSpeed * 1.2f);
                    break;
                case EnemyBehavior.DriftPulse:
                    behaviorVelocity = chaseDir * e.moveSpeed;
                    e.attackCooldown -= dt;
                    break;
                case EnemyBehavior.StationaryTurret:
                    behaviorVelocity = Vector3.zero;
                    break;
                default:
                    behaviorVelocity = chaseDir * e.moveSpeed;
                    break;
            }

            var intent = behaviorVelocity;
            behaviorVelocity = ApplySeparation(agentIndex, behaviorVelocity, ref e, intent);
            var maxSpeed = e.moveSpeed * 1.5f;
            if (e.knockbackTimer > 0f)
                maxSpeed = e.moveSpeed * 2.5f;
            else if (def.id == "swordfish" && behaviorVelocity.sqrMagnitude > e.moveSpeed * e.moveSpeed * 2.1f)
                maxSpeed = e.moveSpeed * 2.6f;
            e.velocity = Vector3.ClampMagnitude(behaviorVelocity, maxSpeed);
            return intent;
        }

        static Vector3 GetCreatureInterceptDirection(
            in EnemyAgent agent,
            Vector3 playerPos,
            Vector3 playerVelocity,
            float distToPlayer,
            Vector3 directDir)
        {
            playerVelocity.y = 0f;
            var playerSpeed = playerVelocity.magnitude;
            var leadTime = playerSpeed > 0.35f
                ? Mathf.Clamp(distToPlayer / Mathf.Max(1f, agent.moveSpeed + playerSpeed * 0.65f), 0.12f, 2.2f)
                : 0f;

            var toEnemy = agent.position - playerPos;
            toEnemy.y = 0f;
            if (playerSpeed > 0.35f && toEnemy.sqrMagnitude > 0.01f)
            {
                var forward = playerVelocity / playerSpeed;
                var alignment = Vector3.Dot(toEnemy.normalized, forward);
                if (alignment > -0.35f)
                    leadTime *= Mathf.Lerp(1.08f, 1.42f, Mathf.Clamp01(alignment));
            }

            var predicted = playerPos + playerVelocity * leadTime;
            var toPredicted = predicted - agent.position;
            toPredicted.y = 0f;
            if (toPredicted.sqrMagnitude < 0.01f)
                return GetEncircleChaseDirection(agent.id, agent.position, playerPos, distToPlayer, directDir);

            var interceptDir = toPredicted.normalized;
            var encircleDir = GetEncircleChaseDirection(agent.id, agent.position, playerPos, distToPlayer, directDir);
            var interceptWeight = distToPlayer > 16f ? 0.38f : distToPlayer > 8f ? 0.65f : 0.9f;
            if (playerSpeed > 0.75f && toEnemy.sqrMagnitude > 0.01f)
            {
                var alignment = Vector3.Dot(toEnemy.normalized, playerVelocity.normalized);
                if (alignment > -0.25f)
                    interceptWeight = Mathf.Min(0.96f, interceptWeight + 0.12f);
            }

            return Vector3.Slerp(encircleDir, interceptDir, interceptWeight).normalized;
        }

        static Vector3 TickSwordfishBehavior(
            ref EnemyAgent agent,
            Vector3 cruiseDir,
            Vector3 directDir,
            float dist,
            float dt,
            Vector3 playerPos,
            Vector3 playerVelocity)
        {
            const float springRange = 9.5f;
            const float springDuration = 0.42f;
            const float springRetriggerCooldown = 1.35f;
            const float springSpeedMult = 2.45f;
            const float cruiseMult = 1.05f;

            if (agent.chargeTimer > 0f)
            {
                agent.chargeTimer -= dt;
                if (agent.chargeTimer <= 0f)
                    agent.chargeTimer = -springRetriggerCooldown;

                var springDir = GetCreatureInterceptDirection(agent, playerPos, playerVelocity, dist, directDir);
                return springDir * (agent.moveSpeed * springSpeedMult);
            }

            if (agent.chargeTimer < 0f)
            {
                agent.chargeTimer += dt;
                if (agent.chargeTimer > 0f)
                    agent.chargeTimer = 0f;
                return cruiseDir * (agent.moveSpeed * cruiseMult);
            }

            if (dist <= springRange)
            {
                agent.chargeTimer = springDuration;
                var springDir = GetCreatureInterceptDirection(agent, playerPos, playerVelocity, dist, directDir);
                return springDir * (agent.moveSpeed * springSpeedMult);
            }

            return cruiseDir * (agent.moveSpeed * cruiseMult);
        }

        /// <summary>Spread melee swarms around the player instead of stacking in a rear conga line.</summary>
        static Vector3 GetEncircleChaseDirection(int agentId, Vector3 agentPos, Vector3 playerPos, float distToPlayer, Vector3 directDir)
        {
            if (distToPlayer < 0.01f)
                return Vector3.forward;

            var ringRadius = Mathf.Clamp(distToPlayer * 0.42f, 3.5f, 9f);
            var slotAngle = agentId * 2.39996323f;
            var slotOffset = new Vector3(Mathf.Cos(slotAngle), 0f, Mathf.Sin(slotAngle)) * ringRadius;
            var slotTarget = playerPos + slotOffset;
            var toSlot = slotTarget - agentPos;
            toSlot.y = 0f;

            if (toSlot.sqrMagnitude < 0.01f)
                return directDir;

            var encircleDir = toSlot.normalized;
            var encircleWeight = distToPlayer > 14f ? 0.72f : distToPlayer > 7f ? 0.55f : 0.28f;
            return Vector3.Slerp(directDir, encircleDir, encircleWeight).normalized;
        }

        void TickRangedAttack(
            ref EnemyAgent e,
            Data.EnemyDefinition def,
            float dt,
            Vector3 playerPos,
            float dist,
            DamageReceiver playerDamage)
        {
            if (e.spawnProfile != null)
            {
                var attackPattern = e.patternParams.attackPattern;
                if (attackPattern == EnemyAttackPattern.Contact || attackPattern == EnemyAttackPattern.None)
                    return;

                e.attackCooldown -= dt;
                if (e.attackCooldown > 0f) return;

                var preferred = e.patternParams.attackRange > 0f
                    ? e.patternParams.attackRange
                    : def.preferredRange > 0f ? def.preferredRange : 12f;
                var minRange = preferred * 0.7f;
                var maxRange = preferred * 1.25f;
                if (dist < minRange || dist > maxRange)
                    return;

                var dir = playerPos - e.position;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.01f) return;

                if (IslandObstacles.IsAvailable)
                {
                    var origin = e.position + Vector3.up * 0.5f;
                    var target = playerPos + Vector3.up * 0.5f;
                    if (Physics.Linecast(origin, target, IslandObstacles.Mask, QueryTriggerInteraction.Ignore))
                        return;
                }

                var hostiles = HostileProjectileWorld.Instance;
                if (hostiles == null) return;

                dir.Normalize();
                var muzzle = e.position + dir * Mathf.Max(e.radius + 1.1f, 1.6f) + Vector3.up * 0.55f;
                GetHostileShotProfile(def, out var speed, out var life, out var hitRadius, out var visualRadius);
                if (!hostiles.TrySpawn(muzzle, dir, speed, e.damage, life, hitRadius, visualRadius))
                    return;

                e.attackCooldown = e.patternParams.attackCooldown > 0f
                    ? e.patternParams.attackCooldown
                    : GetAttackRecovery(def);
                return;
            }
            else
            {
            if (def.category != EnemyCategory.Ship &&
                def.behavior != EnemyBehavior.MaintainRange &&
                def.behavior != EnemyBehavior.FormationRanged)
                return;

            e.attackCooldown -= dt;
            if (e.attackCooldown > 0f) return;

            var preferred = def.preferredRange > 0f ? def.preferredRange : 12f;
            var minRange = def.id == "cannonboat" ? preferred * 0.8f : preferred * 0.7f;
            var maxRange = def.id == "cannonboat" ? preferred * 1.15f : preferred * 1.25f;
            if (dist < minRange || dist > maxRange)
                return;

            var dir = playerPos - e.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;

            if (IslandObstacles.IsAvailable)
            {
                var origin = e.position + Vector3.up * 0.5f;
                var target = playerPos + Vector3.up * 0.5f;
                if (Physics.Linecast(origin, target, IslandObstacles.Mask, QueryTriggerInteraction.Ignore))
                    return;
            }

            var hostiles = HostileProjectileWorld.Instance;
            if (hostiles == null) return;

            dir.Normalize();
            var muzzle = e.position + dir * Mathf.Max(e.radius + 1.1f, 1.6f) + Vector3.up * 0.55f;
            GetHostileShotProfile(def, out var speed, out var life, out var hitRadius, out var visualRadius);
            if (!hostiles.TrySpawn(muzzle, dir, speed, e.damage, life, hitRadius, visualRadius))
                return;

            e.attackCooldown = GetAttackRecovery(def);

            if (def.id == "manta_ray" || def.id == "manta")
            {
                var flank = Vector3.Cross(Vector3.up, dir);
                hostiles.TrySpawn(muzzle, (dir + flank * 0.55f).normalized, speed, e.damage * 0.85f, life, hitRadius, visualRadius);
                hostiles.TrySpawn(muzzle, (dir - flank * 0.55f).normalized, speed, e.damage * 0.85f, life, hitRadius, visualRadius);
            }
            }
        }

        static float GetInitialAttackCooldown(Data.EnemyDefinition def)
        {
            if (def.category != EnemyCategory.Ship &&
                def.behavior != EnemyBehavior.MaintainRange &&
                def.behavior != EnemyBehavior.FormationRanged)
                return 0f;

            return def.id switch
            {
                "cannonboat" => 1.1f,
                "drowned_harrier" => 0.9f,
                _ => def.attackCooldown > 0f ? def.attackCooldown * 0.5f : 1f
            };
        }

        static float GetAttackRecovery(Data.EnemyDefinition def)
        {
            if (def.id == "cannonboat")
                return 2f;

            var recovery = def.attackCooldown > 0f ? def.attackCooldown : 2.4f;
            if (def.category == EnemyCategory.Ship)
                recovery = Mathf.Max(recovery, 2.5f);
            return recovery;
        }

        static void GetHostileShotProfile(
            Data.EnemyDefinition def,
            out float speed,
            out float life,
            out float hitRadius,
            out float visualRadius)
        {
            switch (def.id)
            {
                case "drowned_harrier":
                    speed = 10f;
                    life = 1.45f;
                    hitRadius = 0.24f;
                    visualRadius = 0.14f;
                    return;
                case "cannonboat":
                    speed = 9f;
                    life = 1.55f;
                    hitRadius = 0.38f;
                    visualRadius = 0.16f;
                    return;
            }

            if (def.category == EnemyCategory.Ship)
            {
                speed = 9f;
                life = 1.35f;
                hitRadius = 0.22f;
                visualRadius = 0.13f;
                return;
            }

            speed = 9f;
            life = 1.3f;
            hitRadius = 0.22f;
            visualRadius = 0.13f;
        }

        Vector3 ApplySeparation(int agentIndex, Vector3 velocity, ref EnemyAgent self, Vector3 preferredDirection)
        {
            var nearby = _hash.Query(self.position, self.radius + 1.5f, 8);
            var push = Vector3.zero;
            var count = 0;
            for (var i = 0; i < nearby.Count; i++)
            {
                var index = nearby[i];
                if (index == agentIndex || !_agents[index].active) continue;
                var delta = self.position - _agents[index].position;
                delta.y = 0f;
                var dist = delta.magnitude;
                if (dist < 0.01f || dist > self.radius + _agents[index].radius + 1.5f) continue;
                push += delta.normalized * (1f - dist / (self.radius + _agents[index].radius + 1.5f));
                count++;
            }

            if (count == 0) return velocity;
            push /= count;

            var separated = velocity + push * 3f;
            if (preferredDirection.sqrMagnitude < 0.01f)
                return separated;

            preferredDirection.y = 0f;
            preferredDirection.Normalize();
            var forwardAmount = Vector3.Dot(separated, preferredDirection);
            if (forwardAmount >= 0f)
                return separated;

            var slide = Vector3.ProjectOnPlane(separated, preferredDirection);
            return slide.sqrMagnitude > 0.01f ? slide : velocity;
        }

        public bool TryGetNearest(Vector3 from, float range, out int index, bool preferElite = false)
        {
            index = FindNearest(from, range, preferElite);
            if (index >= 0 || !preferElite) return index >= 0;
            index = FindNearest(from, range, false);
            return index >= 0;
        }

        int FindNearest(Vector3 from, float range, bool eliteOnly)
        {
            _hash.Clear();
            for (var i = 0; i < _agents.Length; i++)
                if (_agents[i].active) _hash.Insert(i, _agents[i].position);

            var best = -1;
            var bestDist = range * range;
            var candidates = _hash.Query(from, range, 64);
            for (var qi = 0; qi < candidates.Count; qi++)
            {
                var i = candidates[qi];
                if (!_agents[i].active) continue;
                if (eliteOnly && !_agents[i].isElite) continue;
                var d = (_agents[i].position - from).sqrMagnitude;
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }
            return best;
        }

        public void SetBurnVisual(int index, bool burning) =>
            EnemyBurnFlash.SetBurning(index >= 0 && index < _agents.Length ? _agents[index].view : null, burning);

        public void ApplyDamage(int index, float damage, bool isCrit, bool showHitFeedback = true, string weaponId = null)
        {
            if (index < 0 || index >= _agents.Length || !_agents[index].active) return;
            ref var e = ref _agents[index];
            if (!string.IsNullOrEmpty(weaponId))
                e.lastHitWeaponId = weaponId;
            e.hp -= damage;

            if (e.healthBar != null)
            {
                var bar = e.healthBar.GetComponent<WorldHealthBar>();
                bar?.SetFill(e.maxHp > 0f ? e.hp / e.maxHp : 0f);
            }

            if (showHitFeedback && e.view != null && weaponId != "powder_kegs")
                JuiceVfxBroker.Instance?.PlayDamageHighlight(e.view);

            Meta.TriggerBus.Instance?.RaiseHit(damage, isCrit, e.position, weaponId);
            if (showHitFeedback && damage >= 0.5f)
                FloatingDamageNumbers.Instance?.ShowEnemyDamage(e.position, damage, isCrit);

            if (e.hp <= 0f)
            {
                var def = definitions[e.definitionIndex];
                RunManager.Instance?.RegisterKill(e.isBoss);
                var export = StageBalanceProfileContext.Export;
                var gold = def.goldValue;
                if (StageBalanceProfileContext.IsActive && export != null)
                {
                    gold = SaltwindStageEconomyLogic.ResolveKillGoldDrop(
                        export,
                        def,
                        RunManager.Instance.Rng,
                        e.affix == EliteAffix.Rich,
                        e.isElite,
                        e.isBoss);
                }

                Pickups.PickupWorld.Instance?.SpawnFromEnemy(
                    e.position,
                    def.xpValue,
                    gold,
                    !StageBalanceProfileContext.IsActive && e.affix == EliteAffix.Rich);
                V3FloorPowerupWorld.Instance?.TryRollOnKill(e.position, e.isBoss, e.isElite);
                RunEncounterTracker.RegisterAgentKill(e.id);
                if (StageBalanceProfileContext.IsActive && def != null)
                    RunMarksTracker.TryCreditScheduledEliteKill(def.id);
                if (e.isBoss && RunEncounterTracker.BossObjectiveSecured)
                    Object.FindFirstObjectByType<POIManager>()?.NotifyBossDefeated();
                Meta.TriggerBus.Instance?.RaiseKill(e.isElite, e.isBoss, e.position, e.lastHitWeaponId);
                V3.V3RunItemEffects.Instance?.NotifyKill();
                if (!string.IsNullOrEmpty(e.lastHitWeaponId))
                    WeaponDamageLedger.RecordKill(e.lastHitWeaponId);
                Despawn(index);
            }
        }
    }
}
