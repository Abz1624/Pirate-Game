using UnityEngine;

namespace PirateGame.Combat
{
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] float maxHealth = 30f;
        [SerializeField] bool destroyOnDeath = true;

        float currentHealth;

        public bool IsAlive => currentHealth > 0f;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;

        void Awake()
        {
            currentHealth = maxHealth;
        }

        /// <summary>
        /// Sets max and current health. Call after spawn to apply definition / wave multipliers.
        /// </summary>
        public void Initialize(float newMaxHealth)
        {
            maxHealth = Mathf.Max(1f, newMaxHealth);
            currentHealth = maxHealth;
        }

        public void TakeDamage(float amount, GameObject source)
        {
            if (!IsAlive) return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            if (!IsAlive && destroyOnDeath)
                Destroy(gameObject);
        }
    }
}
