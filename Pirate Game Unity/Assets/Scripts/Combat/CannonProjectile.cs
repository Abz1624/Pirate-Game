using UnityEngine;

namespace PirateGame.Combat
{
    [RequireComponent(typeof(Rigidbody))]
    public class CannonProjectile : MonoBehaviour
    {
        [SerializeField] float lifetime = 4f;
        [SerializeField] float hitRadius = 0.45f;

        float damage;
        float maxRange;
        int pierceRemaining;
        GameObject source;
        Vector3 spawnPosition;
        Vector3 lastPosition;
        Rigidbody body;
        readonly Collider[] overlapBuffer = new Collider[12];

        public void Initialize(Vector3 velocity, float damageAmount, float range, int pierce, GameObject firedBy)
        {
            damage = damageAmount;
            maxRange = range;
            pierceRemaining = pierce;
            source = firedBy;
            spawnPosition = transform.position;
            lastPosition = transform.position;

            body ??= GetComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = velocity;

            Destroy(gameObject, lifetime);
        }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        void Update()
        {
            if (Vector3.Distance(spawnPosition, transform.position) >= maxRange)
                Destroy(gameObject);
        }

        void FixedUpdate()
        {
            var current = transform.position;
            var delta = current - lastPosition;
            var distance = delta.magnitude;
            if (distance > 0.001f)
            {
                if (Physics.SphereCast(lastPosition, hitRadius, delta.normalized, out var hit, distance, ~0, QueryTriggerInteraction.Collide))
                    TryDamage(hit.collider);

                var count = Physics.OverlapSphereNonAlloc(current, hitRadius, overlapBuffer, ~0, QueryTriggerInteraction.Collide);
                for (var i = 0; i < count; i++)
                    TryDamage(overlapBuffer[i]);
            }

            lastPosition = current;
        }

        void OnTriggerEnter(Collider other) => TryDamage(other);

        void TryDamage(Collider other)
        {
            if (other == null) return;
            if (source != null && other.transform.IsChildOf(source.transform))
                return;

            var damageable = other.GetComponentInParent<Health>();
            if (damageable == null || !damageable.IsAlive)
                return;

            damageable.TakeDamage(damage, source);

            pierceRemaining--;
            if (pierceRemaining < 0)
                Destroy(gameObject);
        }
    }
}
