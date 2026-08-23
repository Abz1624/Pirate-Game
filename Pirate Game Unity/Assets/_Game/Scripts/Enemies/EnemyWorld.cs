using System.Collections.Generic;
using HighSeas.Core;
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
        public int definitionIndex;
        public EliteAffix affix;
        public bool isElite;
        public bool isBoss;
        public Transform view;
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
        [SerializeField] float islandAvoidanceProbeDistance = 6f;
        [SerializeField] float islandCollisionPadding = 0.35f;
        [SerializeField] float shipCollisionPadding = 0.55f;
        [SerializeField] float enemySeparationPadding = 0.2f;

        readonly SpatialHash _hash = new(4f);
        EnemyAgent[] _agents;
        EnemyViewPool _viewPool;
        int _activeCount;
        int _nextId = 1;

        public int ActiveCount => _activeCount;
        public EnemyAgent[] Agents => _agents;
        public Data.EnemyDefinition[] Definitions => definitions;

        void Awake()
        {
            var cap = Platform.QualityProfile.Current.enemyCap;
            _agents = new EnemyAgent[cap];
            _viewPool = new EnemyViewPool(enemyRoot != null ? enemyRoot : transform);
        }

        public void SetDefinitions(Data.EnemyDefinition[] defs) => definitions = defs;

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

        public int Spawn(int definitionIndex, Vector3 position, EliteAffix affix = EliteAffix.None, float speedMultiplier = 1f)
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

            var minute = RunManager.Instance != null ? RunManager.Instance.ElapsedSeconds / 60f : 0f;
            var tier = RunManager.Instance?.Tier ?? 1;
            var stage = RunManager.Instance?.StageIndex ?? 0;
            var hp = GameMath.EnemyHP(def.baseHP, minute, tier, stage);
            var dmg = GameMath.EnemyDamage(def.baseDamage, minute, tier, stage);
            if (affix == EliteAffix.Armored) hp *= 1.8f;
            if (affix == EliteAffix.Swift) hp *= 0.9f;
            if (affix == EliteAffix.Rich) hp *= 1.25f;

            var visualScale = def.visualScale * (affix != EliteAffix.None ? 1.12f : 1f);
            var view = CreateView(def, position, visualScale, affix != EliteAffix.None);

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
                chargeTimer = 2f,
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
            return slot;
        }

        Transform CreateView(Data.EnemyDefinition def, Vector3 position, float scale, bool elite)
        {
            if (def.visualPrefab != null)
            {
                var yaw = Quaternion.Euler(0f, def.visualYawOffset, 0f);
                return _viewPool.Acquire(def.visualPrefab, position, yaw, scale);
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

        public void Despawn(int index)
        {
            if (index < 0 || index >= _agents.Length || !_agents[index].active) return;

            var def = definitions[_agents[index].definitionIndex];
            if (_agents[index].view != null)
            {
                if (def.visualPrefab != null)
                    _viewPool.Release(_agents[index].view, def.visualPrefab);
                else
                    Destroy(_agents[index].view.gameObject);
            }

            _agents[index].active = false;
            _activeCount--;
        }

        public void Tick(float dt, Vector3 playerPos, Combat.DamageReceiver playerDamage)
        {
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
                var desiredVelocity = TickBehavior(i, ref e, def, dt, playerPos);
                MoveAgent(ref e, dt, desiredVelocity, def);
                UpdateView(ref e, def, dt, playerPos);

                var dist = Vector3.Distance(e.position, playerPos);
                if (dist < e.radius + 1f && e.contactCooldown <= 0f)
                {
                    playerDamage?.TakeDamage(e.damage, e.position, false);
                    e.contactCooldown = GameConstants.EnemyContactCooldown;
                }
                if (e.contactCooldown > 0f) e.contactCooldown -= dt;
            }

            ResolveAgentOverlaps();
        }

        float GetCollisionRadius(Data.EnemyDefinition def)
        {
            var padding = def.category == EnemyCategory.Ship ? shipCollisionPadding : islandCollisionPadding;
            return def.radius + padding;
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
                islandAvoidanceProbeDistance);

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

        Vector3 TickBehavior(int agentIndex, ref EnemyAgent e, Data.EnemyDefinition def, float dt, Vector3 playerPos)
        {
            var toPlayer = playerPos - e.position;
            toPlayer.y = 0f;
            var dist = toPlayer.magnitude;
            var dir = dist > 0.01f ? toPlayer / dist : Vector3.forward;
            Vector3 behaviorVelocity;

            switch (def.behavior)
            {
                case EnemyBehavior.DirectChaser:
                case EnemyBehavior.TankChaser:
                case EnemyBehavior.DiveLunge:
                    behaviorVelocity = dir * e.moveSpeed;
                    break;
                case EnemyBehavior.FlankingChaser:
                {
                    var flankSign = (e.id & 1) == 0 ? 1f : -1f;
                    var flank = Vector3.Cross(Vector3.up, dir) * flankSign;
                    behaviorVelocity = (dir + flank * 0.35f).normalized * e.moveSpeed;
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
                    if (dist < def.preferredRange) behaviorVelocity = -dir * e.moveSpeed;
                    else if (dist > def.preferredRange + 2f) behaviorVelocity = dir * e.moveSpeed;
                    else behaviorVelocity = Vector3.zero;
                    break;
                case EnemyBehavior.SuicideApproach:
                    behaviorVelocity = dir * (e.moveSpeed * 1.4f);
                    break;
                case EnemyBehavior.StrafeBurst:
                {
                    var tangent = Vector3.Cross(Vector3.up, dir);
                    behaviorVelocity = tangent * e.moveSpeed + dir * (e.moveSpeed * 0.25f);
                    e.attackCooldown -= dt;
                    break;
                }
                case EnemyBehavior.FastSwarm:
                    behaviorVelocity = dir * (e.moveSpeed * 1.2f);
                    break;
                case EnemyBehavior.DriftPulse:
                    behaviorVelocity = dir * (e.moveSpeed * 0.45f);
                    e.attackCooldown -= dt;
                    break;
                case EnemyBehavior.StationaryTurret:
                    behaviorVelocity = Vector3.zero;
                    break;
                default:
                    behaviorVelocity = dir * e.moveSpeed;
                    break;
            }

            var intent = behaviorVelocity;
            behaviorVelocity = ApplySeparation(agentIndex, behaviorVelocity, ref e, intent);
            e.velocity = Vector3.ClampMagnitude(behaviorVelocity, e.moveSpeed * 1.5f);
            return intent;
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

        public void ApplyDamage(int index, float damage, bool isCrit)
        {
            if (index < 0 || index >= _agents.Length || !_agents[index].active) return;
            ref var e = ref _agents[index];
            e.hp -= damage;
            if (e.hp <= 0f)
            {
                var def = definitions[e.definitionIndex];
                RunManager.Instance?.RegisterKill(e.isBoss);
                Pickups.PickupWorld.Instance?.SpawnFromEnemy(e.position, def.xpValue, def.goldValue, e.affix == EliteAffix.Rich);
                Meta.TriggerBus.Instance?.RaiseKill(e.isElite, e.isBoss, e.position);
                Despawn(index);
            }
        }
    }
}
