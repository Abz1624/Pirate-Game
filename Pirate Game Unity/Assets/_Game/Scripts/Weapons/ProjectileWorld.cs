using HighSeas.Combat;
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
        public int homingEnemyIndex;
        public Vector3 spawnDirection;
    }

    public sealed class ProjectileWorld : MonoBehaviour
    {
        public static ProjectileWorld Instance { get; private set; }

        const float MinGameplayHitRadius = 0.32f;
        const float HitSlop = 0.16f;
        const float MaxVerticalHitSeparation = 4f;
        const float HomingTurnRateDegPerSec = 20f;
        const float HomingMinAngleDeg = 8f;
        const float MaxHomingDeviationDeg = 10f;

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
            PlayerShipController firingShip = null,
            int homingEnemyIndex = -1)
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
            var normalizedDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
            _pool[slot] = new Projectile
            {
                active = true,
                position = pos,
                previousPosition = pos,
                velocity = normalizedDir * speed,
                damage = damage,
                life = life,
                radius = radius,
                pierceLeft = Mathf.Max(1, pierce + 1),
                isCrit = isCrit,
                weaponId = weaponId,
                firingShip = firingShip,
                view = view,
                homingEnemyIndex = ShouldApplyHoming(weaponId, homingEnemyIndex) ? homingEnemyIndex : -1,
                spawnDirection = normalizedDir
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
                ApplySubtleHoming(ref p, dt, enemies);
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
                        var agentPos = list[ei].position + list[ei].velocity * (dt * 0.5f);
                        if (!TryHitAgentAlongSegment(
                                segmentStart,
                                p.position,
                                p.radius,
                                agentPos,
                                list[ei].radius,
                                out _))
                            continue;

                        enemies.ApplyDamage(ei, p.damage, p.isCrit, weaponId: p.weaponId);
                        if (p.weaponId == "harpoon_gun")
                        {
                            var agent = list[ei];
                            var slow = agent.isBoss ? 0.125f : 0.25f;
                            StatusController.Instance?.ApplyStatus(ei, StatusType.Tarred, slow);
                            WeaponDamageLedger.RecordControl(p.weaponId, 1.2f);
                        }
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

        static bool ShouldApplyHoming(string weaponId, int enemyIndex)
        {
            if (enemyIndex < 0 || string.IsNullOrEmpty(weaponId)) return false;
            return weaponId is "swivel_guns" or "deck_cannons" or "long_nine";
        }

        static void ApplySubtleHoming(ref Projectile p, float dt, EnemyWorld enemies)
        {
            if (p.homingEnemyIndex < 0 || enemies == null) return;
            var agents = enemies.Agents;
            if (p.homingEnemyIndex >= agents.Length || !agents[p.homingEnemyIndex].active) return;

            var agent = agents[p.homingEnemyIndex];
            var target = agent.position;
            target.y = p.position.y;
            var toTarget = target - p.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.04f) return;

            var speed = p.velocity.magnitude;
            if (speed < 0.01f) return;

            var current = p.velocity;
            current.y = 0f;
            var desired = toTarget.normalized * speed;
            var alignment = Vector3.Dot(current.normalized, desired.normalized);
            if (alignment > 0.992f)
                return;

            var angleDeg = Mathf.Acos(Mathf.Clamp(alignment, -1f, 1f)) * Mathf.Rad2Deg;
            if (angleDeg < HomingMinAngleDeg)
                return;

            var turnScale = Mathf.Clamp01(angleDeg / 40f);
            var maxRadians = HomingTurnRateDegPerSec * Mathf.Deg2Rad * dt * turnScale;
            var steered = Vector3.RotateTowards(current, desired, maxRadians, 0f);
            steered.y = 0f;
            if (steered.sqrMagnitude < 0.01f) return;
            steered.Normalize();

            var spawnDir = p.spawnDirection;
            spawnDir.y = 0f;
            if (spawnDir.sqrMagnitude < 0.01f)
                spawnDir = current.normalized;
            else
                spawnDir.Normalize();

            var maxDevRad = MaxHomingDeviationDeg * Mathf.Deg2Rad;
            var clampedDir = Vector3.RotateTowards(spawnDir, steered, maxDevRad, 0f);
            if (Vector3.Angle(spawnDir, current.normalized) >= MaxHomingDeviationDeg - 0.5f)
                return;

            p.velocity = clampedDir * speed;
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
