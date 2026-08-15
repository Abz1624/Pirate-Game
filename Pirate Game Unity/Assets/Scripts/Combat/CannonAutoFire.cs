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

    public partial class CannonAutoFire : MonoBehaviour
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
        [SerializeField] float range = 10f;
        [SerializeField] int pierce = 0;
        [Tooltip("When enabled, only adjusts shot pitch (Y) for enemies already in the mount's forward cone. Horizontal bearing always follows the ship.")]
        [SerializeField] bool verticalAutoAim = true;
        [Tooltip("Half-angle of the forward fire cone used for vertical auto-aim.")]
        [SerializeField] float fireConeHalfAngle = 22f;
        [Tooltip("When not auto-aiming, each mount fires along its local forward axis.")]
        [SerializeField] bool useMountForwardWhenNotAiming = true;

        [Header("Targeting")]
        [SerializeField] LayerMask enemyLayers = ~0;
        [SerializeField] string enemyTag = "Enemy";
        [Tooltip("Shared Y plane for shots and hit detection (match enemy spawn height).")]
        [SerializeField] float combatPlaneY = 0.25f;

        float cooldown;
        int roundRobinIndex;

        float damageMultiplier = 1f;
        float fireRateMultiplier = 1f;
        float rangeMultiplier = 1f;
        int pierceBonus;

        public float DamageMultiplier
        {
            get => damageMultiplier;
            set => damageMultiplier = Mathf.Max(0.1f, value);
        }

        public float FireRateMultiplier
        {
            get => fireRateMultiplier;
            set => fireRateMultiplier = Mathf.Max(0.1f, value);
        }

        public float RangeMultiplier
        {
            get => rangeMultiplier;
            set => rangeMultiplier = Mathf.Max(0.1f, value);
        }

        public int PierceBonus
        {
            get => pierceBonus;
            set => pierceBonus = Mathf.Max(0, value);
        }

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

            var interval = 1f / (fireRate * fireRateMultiplier);
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

            var spawnPosition = mount.position;
            spawnPosition.y = combatPlaneY;

            var projectile = Instantiate(
                projectilePrefab,
                spawnPosition,
                Quaternion.LookRotation(direction, Vector3.up));

            projectile.Initialize(direction * projectileSpeed, damage * damageMultiplier, range * rangeMultiplier, pierce + pierceBonus, gameObject);
            return true;
        }

        bool TryGetFireDirection(Transform mount, out Vector3 direction)
        {
            direction = Vector3.zero;

            if (!TryGetHorizontalMountForward(mount, out var flatForward))
            {
                if (!useMountForwardWhenNotAiming)
                    return false;

                flatForward = mount.forward;
                flatForward.y = 0f;
                if (flatForward.sqrMagnitude < 0.0001f)
                    return false;

                flatForward.Normalize();
            }

            direction = flatForward;

            if (!verticalAutoAim)
                return true;

            var origin = mount.position;
            origin.y = combatPlaneY;

            if (!TryFindEnemyInFireCone(origin, flatForward, out var aimPoint))
                return true;

            var offset = aimPoint - origin;
            var horizontalDistance = new Vector2(offset.x, offset.z).magnitude;
            if (horizontalDistance < 0.05f)
                return true;

            direction = new Vector3(flatForward.x, offset.y / horizontalDistance, flatForward.z).normalized;
            return true;
        }

        bool TryGetHorizontalMountForward(Transform mount, out Vector3 flatForward)
        {
            flatForward = mount.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.0001f)
                return false;

            flatForward.Normalize();
            return true;
        }

        bool TryFindEnemyInFireCone(Vector3 origin, Vector3 flatForward, out Vector3 aimPoint)
        {
            aimPoint = Vector3.zero;
            var maxRange = range * rangeMultiplier;
            var maxRangeSq = maxRange * maxRange;
            var cosThreshold = Mathf.Cos(fireConeHalfAngle * Mathf.Deg2Rad);
            var bestDistanceSq = float.MaxValue;
            var found = false;

            if (!string.IsNullOrEmpty(enemyTag))
            {
                var taggedEnemies = GameObject.FindGameObjectsWithTag(enemyTag);
                foreach (var enemy in taggedEnemies)
                {
                    if (!IsEnemyInFireCone(enemy, origin, flatForward, cosThreshold, maxRangeSq, out var distSq))
                        continue;

                    if (distSq >= bestDistanceSq) continue;

                    bestDistanceSq = distSq;
                    aimPoint = GetAimPoint(enemy);
                    found = true;
                }
            }

            var hits = Physics.OverlapSphere(origin, maxRange, enemyLayers, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (!IsEnemyInFireCone(hit.gameObject, origin, flatForward, cosThreshold, maxRangeSq, out var distSq))
                    continue;

                if (distSq >= bestDistanceSq) continue;

                bestDistanceSq = distSq;
                aimPoint = GetAimPoint(hit.gameObject);
                found = true;
            }

            return found;
        }

        static bool IsEnemyInFireCone(
            GameObject target,
            Vector3 origin,
            Vector3 flatForward,
            float cosThreshold,
            float maxRangeSq,
            out float distanceSq)
        {
            distanceSq = float.MaxValue;
            if (!IsValidTarget(target)) return false;

            var toEnemy = target.transform.position - origin;
            toEnemy.y = 0f;
            distanceSq = toEnemy.sqrMagnitude;
            if (distanceSq > maxRangeSq || distanceSq < 0.0001f)
                return false;

            var alignment = Vector3.Dot(toEnemy.normalized, flatForward);
            return alignment >= cosThreshold;
        }

        static Vector3 GetAimPoint(GameObject target)
        {
            var collider = target.GetComponentInParent<Collider>();
            if (collider != null)
                return collider.bounds.center;

            return target.transform.position;
        }

        static bool IsValidTarget(GameObject target)
        {
            var damageable = target.GetComponentInParent<Health>();
            return damageable != null && damageable.IsAlive;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, range);
        }
    }
}
