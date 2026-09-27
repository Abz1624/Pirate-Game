using System;
using HighSeas.Core;
using HighSeas.World;
using UnityEngine;

namespace RoguePirates.V4.Enemies
{
    /// <summary>
    /// Burn, Tarred, Breach and Curse on enemies (port of StatusController). Keyed by slot with 512 entries, as V3: a
    /// status applied to a slot freed in the same frame (Burn after a killing hit) is inherited by the next enemy
    /// spawned into it (V3 behaviour, kept for trace parity).
    /// </summary>
    public sealed class EnemyStatuses
    {
        readonly float[] _burnStacks = new float[512];
        readonly float[] _tarred = new float[512];
        readonly float[] _breach = new float[512];
        readonly float[] _curse = new float[512];
        readonly bool[] _burnVisualActive = new bool[512];

        /// <summary>After a status was applied (V3 raised TriggerBus.StatusApplied).</summary>
        public Action<StatusType> Applied;

        public void ApplyStatus(int enemyIndex, StatusType type, float strength)
        {
            if (enemyIndex < 0 || enemyIndex >= _burnStacks.Length) return;
            switch (type)
            {
                case StatusType.Burn:
                    _burnStacks[enemyIndex] = Mathf.Min(20f, _burnStacks[enemyIndex] + 1f);
                    break;
                case StatusType.Tarred:
                    _tarred[enemyIndex] = Mathf.Max(_tarred[enemyIndex], strength);
                    break;
                case StatusType.Breach:
                    _breach[enemyIndex] = Mathf.Min(5f, _breach[enemyIndex] + 1f);
                    break;
                case StatusType.Shock:
                    break;
                case StatusType.Curse:
                    _curse[enemyIndex] = Mathf.Min(0.6f, _curse[enemyIndex] + strength * 0.1f);
                    break;
            }
            Applied?.Invoke(type);
        }

        public float GetVulnerability(int enemyIndex) => 1f + _curse[enemyIndex];
        public float GetArmorReduction(int enemyIndex) => _breach[enemyIndex] * 4f;
        public float BurnStacks(int enemyIndex) => _burnStacks[enemyIndex];

        /// <param name="burnDamage">The burn tick through the hit path without hit feedback (V3 ApplyDamage(i, x, false, false)).</param>
        /// <param name="setBurnVisual">The model's burn flash on or off.</param>
        public void Tick(float dt, EnemyUnit[] agents, Action<int, float> burnDamage, Action<int, bool> setBurnVisual)
        {
            for (var i = 0; i < agents.Length; i++)
            {
                if (!agents[i].active)
                {
                    if (_burnStacks[i] > 0f || _tarred[i] > 0f || _breach[i] > 0f || _curse[i] > 0f)
                        ClearIndex(i);
                    continue;
                }

                if (_burnStacks[i] > 0f)
                {
                    burnDamage(i, _burnStacks[i] * 0.5f * dt);
                    _burnStacks[i] = Mathf.Max(0f, _burnStacks[i] - dt * 0.5f);
                }

                var burning = _burnStacks[i] > 0f;
                if (_burnVisualActive[i] != burning)
                {
                    _burnVisualActive[i] = burning;
                    setBurnVisual(i, burning);
                }

                _tarred[i] = Mathf.Max(0f, _tarred[i] - dt);
                _breach[i] = Mathf.Max(0f, _breach[i] - dt * 0.25f);
                _curse[i] = Mathf.Max(0f, _curse[i] - dt * 0.05f);
            }
        }

        public void ClearIndex(int index)
        {
            if (index < 0 || index >= _burnStacks.Length) return;
            _burnStacks[index] = 0f;
            _tarred[index] = 0f;
            _breach[index] = 0f;
            _curse[index] = 0f;
            _burnVisualActive[index] = false;
        }
    }

    public struct HostileShot
    {
        public bool active;
        public Vector3 position;
        public Vector3 velocity;
        public float life;
        public float damage;
        public float radius;
        public float gravity;
        public Transform view;
    }

    /// <summary>Hostile shot models and the impact splash (V3's ProjectileViewPool and JuiceVfxBroker until M14).</summary>
    public interface IHostileShotViews
    {
        Transform Acquire(Vector3 position, Color color, float visualRadius);
        void Release(Transform view);
        void Splash(Vector3 position);
    }

    /// <summary>The ship as hostile shots see it: V3 tested against the DamageReceiver's transform and IsAlive.</summary>
    public interface IHostileShotTarget
    {
        bool Exists { get; }
        bool IsAlive { get; }
        Vector3 HullPosition { get; }
        void TakeDamage(float amount, Vector3 source, bool isCrit);
    }

    /// <summary>
    /// Enemy cannonballs and ranged shots (port of HostileProjectileWorld). 96 slots, lowest-free, same arc, water,
    /// island and hit rules. Also the boss director's shot sink.
    /// </summary>
    public sealed class HostileProjectiles : IHostileProjectileView, RoguePirates.V4.Stage.IHostileProjectileSink
    {
        const float PlayerHitRadius = 0.85f;
        const float MaxVerticalHitSeparation = 3f;
        const float WaterSurfaceY = 0.15f;

        static readonly Color HostileProjectileColor = new Color(0.42f, 0.22f, 0.1f);

        readonly HostileShot[] _pool = new HostileShot[96];
        readonly IHostileShotViews _views;
        int _active;

        public HostileProjectiles(IHostileShotViews views) { _views = views; }

        public int Capacity => _pool.Length;
        public int ActiveCount => _active;
        bool RoguePirates.V4.Stage.IHostileProjectileSink.Exists => true;
        bool RoguePirates.V4.Stage.IHostileProjectileSink.TrySpawn(Vector3 position, Vector3 direction, float speed, float damage, float life, float hitRadius, float visualRadius) =>
            TrySpawn(position, direction, speed, damage, life, hitRadius, visualRadius);
        public HostileShot[] Pool => _pool;

        public bool TryGet(int index, out HostileShotSnapshot shot)
        {
            shot = default;
            if (index < 0 || index >= _pool.Length || !_pool[index].active) return false;
            shot = new HostileShotSnapshot { position = _pool[index].position, velocity = _pool[index].velocity };
            return true;
        }

        public bool TrySpawn(Vector3 position, Vector3 direction, float speed, float damage, float life, float hitRadius, float visualRadius = -1f)
        {
            if (_active >= _pool.Length) return false;

            var slot = -1;
            for (var i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].active)
                {
                    slot = i;
                    break;
                }
            }

            if (slot < 0) return false;

            var dir = direction;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return false;
            dir.Normalize();

            if (visualRadius < 0f)
                visualRadius = hitRadius;

            var velocity = dir * speed;
            velocity.y = Mathf.Clamp(speed * 0.3f, 1.6f, 3.2f);

            var view = _views?.Acquire(position, HostileProjectileColor, visualRadius);
            _pool[slot] = new HostileShot
            {
                active = true,
                position = position,
                velocity = velocity,
                life = life,
                damage = damage,
                radius = hitRadius,
                gravity = 7.5f,
                view = view
            };
            _active++;
            return true;
        }

        public void Tick(float dt, IHostileShotTarget player)
        {
            var target = player != null && player.Exists ? player : null;
            for (var i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].active) continue;

                ref var shot = ref _pool[i];
                shot.velocity.y -= shot.gravity * dt;
                shot.position += shot.velocity * dt;
                shot.life -= dt;

                if (shot.view != null)
                {
                    shot.view.position = shot.position;
                    if (shot.velocity.sqrMagnitude > 0.01f)
                        shot.view.rotation = Quaternion.LookRotation(shot.velocity.normalized, Vector3.up);
                }

                if (shot.position.y < WaterSurfaceY && shot.velocity.y < 0f)
                {
                    Despawn(i, shot.position);
                    continue;
                }

                if (IslandObstacles.IsAvailable && IslandObstacles.IsPositionBlocked(shot.position, shot.radius))
                {
                    Despawn(i, shot.position);
                    continue;
                }

                if (target != null && target.IsAlive && TryHitPlayer(ref shot, target.HullPosition))
                {
                    target.TakeDamage(shot.damage, shot.position, false);
                    Despawn(i, shot.position);
                    continue;
                }

                if (shot.life <= 0f)
                    Despawn(i, shot.position);
            }
        }

        static bool TryHitPlayer(ref HostileShot shot, Vector3 playerPos)
        {
            var flat = playerPos - shot.position;
            flat.y = 0f;
            var hitRadius = shot.radius + PlayerHitRadius;
            if (flat.sqrMagnitude > hitRadius * hitRadius)
                return false;

            var verticalSeparation = Mathf.Abs(shot.position.y - playerPos.y);
            return verticalSeparation <= MaxVerticalHitSeparation;
        }

        void Despawn(int index, Vector3 impactPosition)
        {
            if (!_pool[index].active) return;
            if (_pool[index].view != null)
                _views?.Release(_pool[index].view);
            _pool[index].active = false;
            _active--;
            _views?.Splash(impactPosition);
        }

        public void ClearAllActive()
        {
            for (var i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].active) continue;
                Despawn(i, _pool[i].position);
            }
        }
    }
}
