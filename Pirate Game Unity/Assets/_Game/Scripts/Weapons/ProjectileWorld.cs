using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.Player;
using HighSeas.V3.MultiShip;
using HighSeas.VFX;
using UnityEngine;

namespace HighSeas.Weapons
{
    public struct Projectile
    {
        public bool active;
        public Vector3 position;
        public Vector3 previousPosition;
        public Vector3 velocity;
        public float damage;
        public float life;
        public float radius;
        public int pierceLeft;
        public bool isCrit;
        public string weaponId;
        public PlayerShipController firingShip;
        public Transform view;
    }

    public sealed class ProjectileWorld : MonoBehaviour
    {
        public static ProjectileWorld Instance { get; private set; }

        const float MinGameplayHitRadius = 0.32f;
        const float HitSlop = 0.1f;
        const float MaxVerticalHitSeparation = 4f;

        [SerializeField] Transform root;
        [SerializeField] DemoJuiceSettings juiceSettings;

        Projectile[] _pool;
        ProjectileViewPool _viewPool;
        int _active;

        public int ActiveProjectileCount => _active;

        void Awake()
        {
            Instance = this;
            _pool = new Projectile[Platform.QualityProfile.Current.projectileCap];
            if (juiceSettings == null)
                juiceSettings = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
            _viewPool = new ProjectileViewPool(root != null ? root : transform, juiceSettings);
        }

        public bool TrySpawn(
            Vector3 pos,
            Vector3 dir,
            float speed,
            float damage,
            float life,
            float radius,
            int pierce,
            bool isCrit,
            Color color,
            string weaponId = null,
            PlayerShipController firingShip = null)
        {
            if (_active >= _pool.Length) return false;
            var slot = -1;
            for (var i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].active) { slot = i; break; }
            }
            if (slot < 0) return false;

            ResolveProjectileVisual(weaponId, radius, out var prefab, out var scaleMult, out var visualRadius);
            var view = _viewPool.Acquire(pos, color, visualRadius, prefab, scaleMult);
            _pool[slot] = new Projectile
            {
                active = true,
                position = pos,
                previousPosition = pos,
                velocity = dir.normalized * speed,
                damage = damage,
                life = life,
                radius = radius,
                pierceLeft = Mathf.Max(1, pierce + 1),
                isCrit = isCrit,
                weaponId = weaponId,
                firingShip = firingShip,
                view = view
            };
            _active++;
            return true;
        }

        void ResolveProjectileVisual(string weaponId, float radius, out GameObject prefab, out float scaleMult, out float visualRadius)
        {
            prefab = null;
            scaleMult = 1f;
            visualRadius = radius;
            if (juiceSettings == null || string.IsNullOrEmpty(weaponId)) return;

            if (juiceSettings.TryGetWeaponVfx(weaponId, out var binding) && binding.projectilePrefab != null)
            {
                prefab = binding.projectilePrefab;
                scaleMult = binding.projectileScaleMult;
                return;
            }

            if (weaponId == "swivel_guns")
            {
                prefab = juiceSettings.smallProjectilePrefab != null
                    ? juiceSettings.smallProjectilePrefab
                    : juiceSettings.projectilePrefab;
                scaleMult = juiceSettings.swivelProjectileSizeMult;
                visualRadius = radius * 0.85f;
                return;
            }

            if (weaponId == "harpoon_gun" && juiceSettings.harpoonProjectilePrefab != null)
            {
                prefab = juiceSettings.harpoonProjectilePrefab;
                scaleMult = juiceSettings.harpoonProjectileScale;
            }
        }

        public void Tick(float dt, EnemyWorld enemies)
        {
            for (var i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].active) continue;
                ref var p = ref _pool[i];
                var segmentStart = p.position;
                p.position += p.velocity * dt;
                p.previousPosition = segmentStart;
                p.life -= dt;
                if (p.view != null)
                {
                    p.view.position = p.position;
                    if (p.velocity.sqrMagnitude > 0.01f)
                        p.view.rotation = Quaternion.LookRotation(p.velocity.normalized, Vector3.up);
                }

                if (enemies != null && p.pierceLeft > 0)
                {
                    var list = enemies.Agents;
                    for (var ei = 0; ei < list.Length; ei++)
                    {
                        if (!list[ei].active) continue;
                        if (!TryHitAgentAlongSegment(
                                segmentStart,
                                p.position,
                                p.radius,
                                list[ei].position,
                                list[ei].radius,
                                out _))
                            continue;

                        enemies.ApplyDamage(ei, p.damage, p.isCrit, weaponId: p.weaponId);
                        p.pierceLeft--;
                        if (p.pierceLeft <= 0)
                            break;
                    }
                }

                if (p.pierceLeft > 0 && V3RunSession.AllowShipVsShipCombat && p.firingShip != null)
                {
                    if (V3ShipCombatRegistry.TryProjectileHit(p.firingShip, p.position, p.radius, p.damage, p.isCrit))
                    {
                        p.pierceLeft--;
                        if (p.pierceLeft <= 0)
                            Despawn(i);
                    }
                }

                if (p.life <= 0f || p.pierceLeft <= 0)
                    Despawn(i);
            }
        }

        static bool TryHitAgentAlongSegment(
            Vector3 segmentStart,
            Vector3 segmentEnd,
            float projectileRadius,
            Vector3 agentPosition,
            float agentRadius,
            out float flatDistanceSqr)
        {
            flatDistanceSqr = float.MaxValue;
            var hitRadius = Mathf.Max(projectileRadius, MinGameplayHitRadius) + agentRadius + HitSlop;
            var closest = ClosestPointOnSegmentXZ(segmentStart, segmentEnd, agentPosition);
            var flat = agentPosition - closest;
            flat.y = 0f;
            flatDistanceSqr = flat.sqrMagnitude;
            if (flatDistanceSqr > hitRadius * hitRadius)
                return false;

            var verticalSeparation = Mathf.Abs(agentPosition.y - closest.y);
            return verticalSeparation <= MaxVerticalHitSeparation;
        }

        static Vector3 ClosestPointOnSegmentXZ(Vector3 a, Vector3 b, Vector3 point)
        {
            var ab = b - a;
            ab.y = 0f;
            if (ab.sqrMagnitude < 0.0001f)
                return a;

            var ap = point - a;
            ap.y = 0f;
            var t = Mathf.Clamp01(Vector3.Dot(ap, ab) / ab.sqrMagnitude);
            return a + ab * t;
        }

        void Despawn(int i)
        {
            if (!_pool[i].active) return;
            var position = _pool[i].position;
            var weaponId = _pool[i].weaponId;
            if (_pool[i].view != null)
                _viewPool.Release(_pool[i].view);
            _pool[i].active = false;
            _active--;
            if (ShouldPlayWaterSplash(weaponId))
                JuiceVfxBroker.Instance?.PlayWaterSplash(position);
        }

        static bool ShouldPlayWaterSplash(string weaponId) =>
            weaponId != "broadside_battery";

        public void ClearAllActive()
        {
            for (var i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].active) continue;
                Despawn(i);
            }
        }
    }
}
