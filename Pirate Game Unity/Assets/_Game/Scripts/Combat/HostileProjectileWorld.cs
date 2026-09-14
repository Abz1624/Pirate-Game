using HighSeas.Data;
using HighSeas.VFX;
using HighSeas.Weapons;
using HighSeas.World;
using UnityEngine;

namespace HighSeas.Combat
{
    public struct HostileProjectile
    {
        public bool active;
        public Vector3 position;
        public Vector3 velocity;
        public float life;
        public float damage;
        public float radius;
        public Transform view;
    }

    /// <summary>Enemy cannonballs and ranged shots aimed at the player.</summary>
    public sealed class HostileProjectileWorld : MonoBehaviour
    {
        public static HostileProjectileWorld Instance { get; private set; }

        static readonly Color HostileProjectileColor = new(0.42f, 0.22f, 0.1f);

        [SerializeField] Transform root;
        HostileProjectile[] _pool;
        ProjectileViewPool _viewPool;
        int _active;

        void Awake()
        {
            Instance = this;
            _pool = new HostileProjectile[96];
            var juice = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
            _viewPool = new ProjectileViewPool(root != null ? root : transform, juice);
        }

        public bool TrySpawn(
            Vector3 position,
            Vector3 direction,
            float speed,
            float damage,
            float life,
            float hitRadius,
            float visualRadius = -1f)
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

            var view = _viewPool.Acquire(position, HostileProjectileColor, visualRadius);
            _pool[slot] = new HostileProjectile
            {
                active = true,
                position = position,
                velocity = dir * speed,
                life = life,
                damage = damage,
                radius = hitRadius,
                view = view
            };
            _active++;
            return true;
        }

        public void Tick(float dt, DamageReceiver player)
        {
            for (var i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].active) continue;

                ref var shot = ref _pool[i];
                shot.position += shot.velocity * dt;
                shot.life -= dt;

                if (shot.view != null)
                {
                    shot.view.position = shot.position;
                    if (shot.velocity.sqrMagnitude > 0.01f)
                        shot.view.rotation = Quaternion.LookRotation(shot.velocity.normalized, Vector3.up);
                }

                if (IslandObstacles.IsAvailable && IslandObstacles.IsPositionBlocked(shot.position, shot.radius))
                {
                    Despawn(i, shot.position);
                    continue;
                }

                if (player != null && player.IsAlive)
                {
                    var playerPos = player.transform.position;
                    var dist = Vector3.Distance(shot.position, playerPos);
                    if (dist <= shot.radius + 0.85f)
                    {
                        player.TakeDamage(shot.damage, shot.position, false);
                        Despawn(i, shot.position);
                        continue;
                    }
                }

                if (shot.life <= 0f)
                    Despawn(i, shot.position);
            }
        }

        void Despawn(int index, Vector3 impactPosition)
        {
            if (!_pool[index].active) return;
            if (_pool[index].view != null)
                _viewPool.Release(_pool[index].view);
            _pool[index].active = false;
            _active--;
            JuiceVfxBroker.Instance?.PlayWaterSplash(impactPosition);
        }
    }
}
