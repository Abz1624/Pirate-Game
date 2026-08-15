using PirateGame.Combat;
using PirateGame.Progression;
using UnityEngine;

namespace PirateGame.Enemies
{
    /// <summary>
    /// Ramming contact — damages the player and destroys the enemy without loot.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyContactDamage : MonoBehaviour
    {
        [SerializeField] float contactRadius = 2.2f;
        [SerializeField] float damageToPlayer = 12f;
        [SerializeField] bool isBoss;

        Transform target;
        Health selfHealth;
        bool hasRammed;

        public void Configure(Transform player, bool boss)
        {
            target = player;
            isBoss = boss;
            contactRadius = boss ? 3.5f : 2.2f;
            damageToPlayer = boss ? 28f : 12f;
        }

        void Awake()
        {
            selfHealth = GetComponent<Health>();
            isBoss = name.ToLowerInvariant().StartsWith("boss_");
        }

        void Update()
        {
            if (hasRammed || target == null || selfHealth == null || !selfHealth.IsAlive)
                return;

            var offset = target.position - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > contactRadius * contactRadius)
                return;

            Ram();
        }

        void Ram()
        {
            hasRammed = true;

            var playerHealth = target.GetComponentInParent<Health>();
            playerHealth?.TakeDamage(damageToPlayer, gameObject);

            GetComponent<EnemyLootDropper>()?.SuppressLoot();
            selfHealth.TakeDamage(selfHealth.MaxHealth + 1f, gameObject);
        }
    }
}
