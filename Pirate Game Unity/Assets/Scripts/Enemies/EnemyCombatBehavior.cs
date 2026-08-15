using PirateGame.Combat;
using UnityEngine;

namespace PirateGame.Enemies
{
    /// <summary>
    /// Name-driven attack patterns for mob enemies and bosses.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyCombatBehavior : MonoBehaviour
    {
        [SerializeField] EnemyBehaviorKind behavior = EnemyBehaviorKind.Chaser;
        [SerializeField] float attackRange = 12f;
        [SerializeField] float attackCooldown = 2.5f;
        [SerializeField] float attackDamage = 8f;
        [SerializeField] float projectileSpeed = 14f;
        [SerializeField] bool logAttacks;

        Transform target;
        float cooldown;
        float chargeTimer;
        bool charging;

        public EnemyBehaviorKind Behavior
        {
            get => behavior;
            set => behavior = value;
        }

        public void ConfigureFromName(string modelName, bool isBoss)
        {
            behavior = isBoss ? EnemyBehaviorKind.Boss : EnemyBehaviorRegistry.Resolve(modelName);
            if (isBoss)
            {
                attackDamage *= 2.5f;
                attackCooldown *= 0.7f;
                attackRange *= 1.4f;
            }
        }

        public void SetTarget(Transform player) => target = player;

        void Update()
        {
            if (target == null) return;

            cooldown -= Time.deltaTime;
            var flat = target.position - transform.position;
            flat.y = 0f;
            var dist = flat.magnitude;
            if (dist > attackRange * 1.5f && behavior != EnemyBehaviorKind.LureSlow)
                return;

            switch (behavior)
            {
                case EnemyBehaviorKind.Charger:
                    UpdateCharger(flat, dist);
                    break;
                case EnemyBehaviorKind.Ranged:
                    if (cooldown <= 0f && dist <= attackRange)
                        FireBolt(flat.normalized);
                    break;
                case EnemyBehaviorKind.AoePulse:
                    if (cooldown <= 0f && dist <= attackRange * 0.85f)
                        PulseAoe();
                    break;
                case EnemyBehaviorKind.LureSlow:
                    if (cooldown <= 0f && dist <= attackRange * 0.6f)
                        TrySlowPlayer();
                    break;
                case EnemyBehaviorKind.Boss:
                    if (cooldown <= 0f)
                    {
                        if (dist <= attackRange * 0.7f)
                            PulseAoe(attackDamage * 1.5f, attackRange * 1.2f);
                        else
                            FireBolt(flat.normalized, attackDamage * 1.2f);
                    }
                    break;
            }
        }

        void UpdateCharger(Vector3 flat, float dist)
        {
            if (charging)
            {
                transform.position += flat.normalized * (projectileSpeed * 1.6f * Time.deltaTime);
                chargeTimer -= Time.deltaTime;
                if (chargeTimer <= 0f)
                    charging = false;
                return;
            }

            if (cooldown <= 0f && dist <= attackRange && dist > 3f)
            {
                charging = true;
                chargeTimer = 0.45f;
                cooldown = attackCooldown;
            }
        }

        void FireBolt(Vector3 direction, float? damageOverride = null)
        {
            cooldown = attackCooldown;
            var bolt = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bolt.transform.position = transform.position + Vector3.up * 0.5f;
            bolt.transform.localScale = Vector3.one * 0.35f;
            Destroy(bolt.GetComponent<Collider>());
            var col = bolt.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.5f;

            var rb = bolt.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.linearVelocity = direction * projectileSpeed;

            var proj = bolt.AddComponent<EnemyBolt>();
            proj.Initialize(damageOverride ?? attackDamage, target.gameObject, 6f);

            if (logAttacks)
                Debug.Log($"[Enemy] {name} ranged shot");
        }

        void PulseAoe(float? damageOverride = null, float? radiusOverride = null)
        {
            cooldown = attackCooldown * 1.2f;
            var radius = radiusOverride ?? attackRange * 0.75f;
            var hits = Physics.OverlapSphere(transform.position, radius);
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Player")) continue;
                var hp = hit.GetComponentInParent<Health>();
                if (hp != null && hp.IsAlive)
                    hp.TakeDamage(damageOverride ?? attackDamage, gameObject);
            }

            if (logAttacks)
                Debug.Log($"[Enemy] {name} AOE pulse");
        }

        void TrySlowPlayer()
        {
            cooldown = attackCooldown;
            var ship = target.GetComponent<Ship.ShipMovement>();
            if (ship != null)
                ship.ApplySlow(0.65f, 1.5f);
        }
    }
}
