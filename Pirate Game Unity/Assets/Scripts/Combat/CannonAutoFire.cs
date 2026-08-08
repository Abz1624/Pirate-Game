using UnityEngine;

namespace PirateGame.Combat
{
    public enum CannonFireMode
    {
        /// <summary>All active cannons fire together on each cooldown.</summary>
        Volley,
        /// <summary>One cannon fires per cooldown tick (survivor-style stream).</summary>
        RoundRobin
    }

    public class CannonAutoFire : MonoBehaviour
    {
        [Header("Cannons")]
        [Tooltip("Empty GameObjects (with optional CannonMount) placed on the hull.")]
        [SerializeField] Transform[] cannonMounts = System.Array.Empty<Transform>();
        [Tooltip("How many mounts are active. Clamped to cannonMounts.Length. Upgrade this at runtime.")]
        [SerializeField] int activeCannonCount = 2;
        [SerializeField] CannonFireMode fireMode = CannonFireMode.Volley;

        [Header("Firing")]
        [SerializeField] CannonProjectile projectilePrefab;
        [Tooltip("Shots per second across the whole battery (not per cannon).")]
        [SerializeField] float fireRate = 1.5f;
        [SerializeField] float projectileSpeed = 24f;
        [SerializeField] float damage = 12f;
        [SerializeField] float range = 30f;
        [SerializeField] int pierce = 0;
        [SerializeField] bool aimAtNearestEnemy = true;
        [Tooltip("When not auto-aiming, each mount fires along its local forward axis.")]
        [SerializeField] bool useMountForwardWhenNotAiming = true;

        [Header("Targeting")]
        [SerializeField] LayerMask enemyLayers = ~0;
        [SerializeField] string enemyTag = "Enemy";

        float cooldown;
        int roundRobinIndex;

        public int ActiveCannonCount
        {
            get => activeCannonCount;
            set => activeCannonCount = Mathf.Clamp(value, 0, cannonMounts.Length);
        }

        public float FireRate
        {
            get => fireRate;
            set => fireRate = Mathf.Max(0.05f, value);
        }

        public Transform[] CannonMounts => cannonMounts;

        void Update()
        {
            if (projectilePrefab == null || cannonMounts.Length == 0 || activeCannonCount <= 0)
                return;

            cooldown -= Time.deltaTime;
            if (cooldown > 0f)
                return;

            var interval = 1f / fireRate;
            var fired = false;

            if (fireMode == CannonFireMode.Volley)
            {
                for (var i = 0; i < activeCannonCount; i++)
                    fired |= TryFireFromMount(cannonMounts[i]);

                if (fired)
                    cooldown = interval;
            }
            else
            {
                for (var attempt = 0; attempt < activeCannonCount; attempt++)
                {
                    var mount = cannonMounts[roundRobinIndex % activeCannonCount];
                    roundRobinIndex = (roundRobinIndex + 1) % activeCannonCount;

                    if (TryFireFromMount(mount))
                    {
                        fired = true;
                        break;
                    }
                }

                if (fired)
                    cooldown = interval / Mathf.Max(1, activeCannonCount);
            }
        }

        bool TryFireFromMount(Transform mount)
        {
            if (mount == null) return false;

            var mountComponent = mount.GetComponent<CannonMount>();
            if (mountComponent != null && !mountComponent.Enabled)
                return false;

            if (!TryGetFireDirection(mount, out var direction))
                return false;

            var projectile = Instantiate(
                projectilePrefab,
                mount.position,
                Quaternion.LookRotation(direction, Vector3.up));

            projectile.Initialize(direction * projectileSpeed, damage, range, pierce, gameObject);
            return true;
        }

        bool TryGetFireDirection(Transform mount, out Vector3 direction)
        {
            direction = Vector3.zero;

            if (aimAtNearestEnemy && TryFindNearestEnemy(mount.position, out var enemyPosition))
            {
                direction = enemyPosition - mount.position;
                direction.y = 0f;
                if (direction.sqrMagnitude < 0.0001f)
                    return false;

                direction.Normalize();
                return true;
            }

            if (useMountForwardWhenNotAiming)
            {
                direction = mount.forward;
                direction.y = 0f;
                if (direction.sqrMagnitude < 0.0001f)
                    return false;

                direction.Normalize();
                return true;
            }

            return false;
        }

        bool TryFindNearestEnemy(Vector3 from, out Vector3 enemyPosition)
        {
            enemyPosition = Vector3.zero;
            var bestDistance = range * range;
            var found = false;

            if (!string.IsNullOrEmpty(enemyTag))
            {
                var taggedEnemies = GameObject.FindGameObjectsWithTag(enemyTag);
                foreach (var enemy in taggedEnemies)
                {
                    if (!IsValidTarget(enemy)) continue;
                    var offset = enemy.transform.position - from;
                    offset.y = 0f;
                    var distSq = offset.sqrMagnitude;
                    if (distSq > bestDistance) continue;

                    bestDistance = distSq;
                    enemyPosition = enemy.transform.position;
                    found = true;
                }
            }

            var hits = Physics.OverlapSphere(from, range, enemyLayers, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (!IsValidTarget(hit.gameObject)) continue;
                var offset = hit.transform.position - from;
                offset.y = 0f;
                var distSq = offset.sqrMagnitude;
                if (distSq > bestDistance) continue;

                bestDistance = distSq;
                enemyPosition = hit.transform.position;
                found = true;
            }

            return found;
        }

        static bool IsValidTarget(GameObject target)
        {
            var damageable = target.GetComponentInParent<IDamageable>();
            return damageable != null && damageable.IsAlive;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, range);
        }
    }
}
