using UnityEngine;

namespace PirateGame.Combat
{
    [RequireComponent(typeof(Rigidbody))]
    public class CannonProjectile : MonoBehaviour
    {
        [SerializeField] float lifetime = 4f;

        float damage;
        float maxRange;
        int pierceRemaining;
        GameObject source;
        Vector3 spawnPosition;
        Rigidbody body;

        public void Initialize(Vector3 velocity, float damageAmount, float range, int pierce, GameObject firedBy)
        {
            damage = damageAmount;
            maxRange = range;
            pierceRemaining = pierce;
            source = firedBy;
            spawnPosition = transform.position;

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

        void OnTriggerEnter(Collider other)
        {
            if (source != null && other.transform.IsChildOf(source.transform))
                return;

            var damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.IsAlive)
                return;

            damageable.TakeDamage(damage, source);

            pierceRemaining--;
            if (pierceRemaining < 0)
                Destroy(gameObject);
        }
    }
}
