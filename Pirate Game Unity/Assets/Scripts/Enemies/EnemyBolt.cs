using PirateGame.Combat;
using UnityEngine;

namespace PirateGame.Enemies
{
    public class EnemyBolt : MonoBehaviour
    {
        float damage;
        GameObject ignore;
        float lifetime;

        public void Initialize(float dmg, GameObject player, float lifeSeconds)
        {
            damage = dmg;
            ignore = player;
            lifetime = lifeSeconds;
            Destroy(gameObject, lifetime);
        }

        void OnTriggerEnter(Collider other)
        {
            if (ignore != null && other.transform.IsChildOf(ignore.transform))
                return;

            if (!other.CompareTag("Player")) return;

            var hp = other.GetComponentInParent<Health>();
            if (hp != null && hp.IsAlive)
                hp.TakeDamage(damage, gameObject);

            Destroy(gameObject);
        }
    }
}
