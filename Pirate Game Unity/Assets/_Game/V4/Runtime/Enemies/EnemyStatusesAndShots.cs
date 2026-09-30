using System;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.World;
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
        readonly float[] _controlled = new float[512]; // R-40: seconds an enemy counts as controlled (V4 only; nothing in V3 reads it)

        /// <summary>R-40: how long a Tarred enemy counts as controlled (the harpoon's control seconds per hit, 1.2 s).</summary>
        public float ControlledSeconds = 1.2f;
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
                    _controlled[enemyIndex] = Mathf.Max(_controlled[enemyIndex], ControlledSeconds);
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
        /// <summary>
        /// R-40: the enemy is controlled — hit by a harpoon's tar within <see cref="ControlledSeconds"/>. (The tar value is
        /// its strength, 0.25, not a duration, so it cannot serve as the window.) The harpoon hit itself lands before its tar.
        /// </summary>
        public bool IsControlled(int enemyIndex) => enemyIndex >= 0 && enemyIndex < _controlled.Length && _controlled[enemyIndex] > 0f;

        /// <param name="burnDamage">The burn tick through the hit path without hit feedback (V3 ApplyDamage(i, x, false, false)).</param>
        /// <param name="setBurnVisual">The model's burn flash on or off.</param>
        public void Tick(float dt, EnemyUnit[] agents, Action<int, float> burnDamage, Action<int, bool> setBurnVisual)
        {
            for (var i = 0; i < agents.Length; i++)
            {
                if (!agents[i].active)
                {
                    if (_burnStacks[i] > 0f || _tarred[i] > 0f || _breach[i] > 0f || _curse[i] > 0f || _controlled[i] > 0f)
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
                _controlled[i] = Mathf.Max(0f, _controlled[i] - dt);
                _breach[i] = Mathf.Max(0f, _breach[i] - dt * 0.25f);
                _curse[i] = Mathf.Max(0f, _curse[i] - dt * 0.05f);
            }
        }

        public void ClearIndex(int index)
        {
            if (index < 0 || index >= _burnStacks.Length) return;
            _burnStacks[index] = 0f;
            _tarred[index] = 0f;
            _controlled[index] = 0f;
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
        /// <summary>R-40: passed within the near-miss margin of the ship without hitting.</summary>
        public bool closePass;
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

        const float DefaultShotGravity = 7.5f;
        const float MinShotGravity = 1.5f;
        const float MaxShotFlightSeconds = 3f;

        /// <summary>
        /// Where to aim so a shot at <paramref name="shotSpeed"/> meets a ship holding its course: the target's position
        /// plus its velocity times the flight time (refined twice, capped at <paramref name="maxLead"/> seconds).
        /// </summary>
        public static Vector3 PredictAimPoint(Vector3 from, Vector3 targetPos, Vector3 targetVelocity, float shotSpeed, float maxLead = 1.5f)
        {
            targetVelocity.y = 0f;
            if (shotSpeed <= 0.01f || targetVelocity.sqrMagnitude < 0.04f)
                return targetPos;

            var aim = targetPos;
            for (var pass = 0; pass < 2; pass++)
            {
                var dx = aim.x - from.x;
                var dz = aim.z - from.z;
                var lead = Mathf.Min(maxLead, Mathf.Sqrt(dx * dx + dz * dz) / shotSpeed);
                aim = targetPos + targetVelocity * lead;
            }

            return aim;
        }

        /// <param name="targetDistance">
        /// Horizontal distance to the aim point (below 0 = V3's fixed arc). With it the arc is shaped so the shot comes back
        /// to muzzle height at that distance: the old fixed arc splashed down after about 7.6 m, well short of the 12-22 m
        /// firing ranges.
        /// </param>
        public bool TrySpawn(Vector3 position, Vector3 direction, float speed, float damage, float life, float hitRadius, float visualRadius = -1f, float targetDistance = -1f)
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
            var gravity = DefaultShotGravity;
            if (targetDistance > 0f && speed > 0.01f)
            {
                // Flight time to the aim point; keep the usual launch angle and lighten gravity so it lands there, or on
                // short shots keep gravity and lower the launch.
                var flight = Mathf.Clamp(targetDistance / speed, 0.15f, MaxShotFlightSeconds);
                gravity = 2f * velocity.y / flight;
                if (gravity > DefaultShotGravity)
                {
                    gravity = DefaultShotGravity;
                    velocity.y = Mathf.Max(0.6f, gravity * flight * 0.5f);
                }
                else if (gravity < MinShotGravity)
                {
                    gravity = MinShotGravity;
                    velocity.y = gravity * flight * 0.5f;
                }

                life = Mathf.Max(life, flight + 0.35f);
            }

            var view = _views?.Acquire(position, HostileProjectileColor, visualRadius);
            _pool[slot] = new HostileShot
            {
                active = true,
                position = position,
                velocity = velocity,
                life = life,
                damage = damage,
                radius = hitRadius,
                gravity = gravity,
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
                    DespawnMissed(i, shot.position);
                    continue;
                }

                if (IslandObstacles.IsAvailable && IslandObstacles.IsPositionBlocked(shot.position, shot.radius))
                {
                    DespawnMissed(i, shot.position);
                    continue;
                }

                if (target != null && target.IsAlive && TryHitPlayer(ref shot, target.HullPosition))
                {
                    target.TakeDamage(shot.damage, shot.position, false);
                    Despawn(i, shot.position);
                    continue;
                }

                if (NearMissMargin > 0f && target != null && target.IsAlive && !shot.closePass && IsClosePass(ref shot, target.HullPosition))
                    shot.closePass = true;

                if (shot.life <= 0f)
                    DespawnMissed(i, shot.position);
            }
        }

        /// <summary>
        /// R-40 fix (0 = off, V3): a hostile shot that passes within this many metres of the ship's hit circle and leaves
        /// without hitting is a near-miss dodge (Ghost Lantern's crit, Roxie's refund; V3 had the hooks but no detection).
        /// </summary>
        public float NearMissMargin;

        /// <summary>A shot dodged: it passed close and left without hitting.</summary>
        public event Action NearMiss;

        bool IsClosePass(ref HostileShot shot, Vector3 playerPos)
        {
            var flat = playerPos - shot.position;
            flat.y = 0f;
            var near = shot.radius + PlayerHitRadius + NearMissMargin;
            return flat.sqrMagnitude <= near * near && Mathf.Abs(shot.position.y - playerPos.y) <= MaxVerticalHitSeparation;
        }

        void DespawnMissed(int index, Vector3 impactPosition)
        {
            var dodged = _pool[index].closePass;
            Despawn(index, impactPosition);
            if (dodged) NearMiss?.Invoke();
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
