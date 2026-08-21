using System;
using System.Collections.Generic;
using HighSeas.Core;
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
            var rSq = radius * radius;
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

        readonly SpatialHash _hash = new(4f);
        EnemyAgent[] _agents;
        int _activeCount;
        int _nextId = 1;
        readonly List<int> _scratch = new(32);

        public int ActiveCount => _activeCount;
        public EnemyAgent[] Agents => _agents;
        public Data.EnemyDefinition[] Definitions => definitions;

        void Awake()
        {
            var cap = Platform.QualityProfile.Current.enemyCap;
            _agents = new EnemyAgent[cap];
        }

        public void SetDefinitions(Data.EnemyDefinition[] defs) => definitions = defs;

        public int Spawn(int definitionIndex, Vector3 position, EliteAffix affix = EliteAffix.None)
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
            if (affix == EliteAffix.Armored) { hp *= 1.8f; }
            if (affix == EliteAffix.Swift) { hp *= 0.9f; }
            if (affix == EliteAffix.Rich) hp *= 1.25f;

            var view = CreateView(def, affix != EliteAffix.None);
            view.position = position;

            _agents[slot] = new EnemyAgent
            {
                id = _nextId++,
                active = true,
                position = position,
                hp = hp,
                maxHp = hp,
                damage = dmg,
                moveSpeed = def.moveSpeed,
                radius = def.radius,
                definitionIndex = definitionIndex,
                affix = affix,
                isElite = affix != EliteAffix.None,
                isBoss = def.isBoss,
                view = view
            };
            _activeCount++;
            return slot;
        }

        Transform CreateView(Data.EnemyDefinition def, bool elite)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = def.id;
            go.transform.SetParent(enemyRoot != null ? enemyRoot : transform, false);
            go.transform.localScale = Vector3.one * (def.radius * 2f);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = elite ? Color.magenta : def.placeholderColor;
            }
            return go.transform;
        }

        public void Despawn(int index)
        {
            if (index < 0 || index >= _agents.Length || !_agents[index].active) return;
            if (_agents[index].view != null) Destroy(_agents[index].view.gameObject);
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
                TickBehavior(ref e, def, dt, playerPos);
                e.position += e.velocity * dt;
                if (e.view != null) e.view.position = e.position;

                var dist = Vector3.Distance(e.position, playerPos);
                if (dist < e.radius + 1f && e.contactCooldown <= 0f)
                {
                    playerDamage?.TakeDamage(e.damage, e.position, false);
                    e.contactCooldown = GameConstants.EnemyContactCooldown;
                }
                if (e.contactCooldown > 0f) e.contactCooldown -= dt;
            }
        }

        void TickBehavior(ref EnemyAgent e, Data.EnemyDefinition def, float dt, Vector3 playerPos)
        {
            var toPlayer = playerPos - e.position;
            toPlayer.y = 0f;
            var dist = toPlayer.magnitude;
            var dir = dist > 0.01f ? toPlayer / dist : Vector3.forward;

            switch (def.behavior)
            {
                case EnemyBehavior.DirectChaser:
                case EnemyBehavior.TankChaser:
                    e.velocity = dir * e.moveSpeed;
                    break;
                case EnemyBehavior.FlankingChaser:
                {
                    var flank = Vector3.Cross(Vector3.up, dir);
                    e.velocity = (dir + flank * 0.35f).normalized * e.moveSpeed;
                    break;
                }
                case EnemyBehavior.TelegraphedCharge:
                    e.chargeTimer -= dt;
                    if (e.chargeTimer <= 0f)
                    {
                        e.velocity = dir * (e.moveSpeed * 2.5f);
                        e.chargeTimer = 2f;
                    }
                    else if (e.chargeTimer > 1.5f)
                    {
                        e.velocity = dir * (e.moveSpeed * 0.2f);
                    }
                    break;
                case EnemyBehavior.MaintainRange:
                case EnemyBehavior.FormationRanged:
                    if (dist < def.preferredRange) e.velocity = -dir * e.moveSpeed;
                    else if (dist > def.preferredRange + 2f) e.velocity = dir * e.moveSpeed;
                    else e.velocity = Vector3.zero;
                    break;
                case EnemyBehavior.SuicideApproach:
                    e.velocity = dir * (e.moveSpeed * 1.4f);
                    break;
                case EnemyBehavior.StrafeBurst:
                {
                    var tangent = Vector3.Cross(Vector3.up, dir);
                    e.velocity = tangent * e.moveSpeed;
                    e.attackCooldown -= dt;
                    break;
                }
                case EnemyBehavior.FastSwarm:
                    e.velocity = dir * (e.moveSpeed * 1.2f);
                    break;
                default:
                    e.velocity = dir * e.moveSpeed;
                    break;
            }

            e.velocity = Vector3.ClampMagnitude(e.velocity, e.moveSpeed * 1.5f);
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
                Meta.TriggerBus.Instance?.RaiseKill(e.isElite, e.isBoss);
                Despawn(index);
            }
        }
    }
}
