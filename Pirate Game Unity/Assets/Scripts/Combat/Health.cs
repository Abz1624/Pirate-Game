using UnityEngine;

namespace PirateGame.Combat
{
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] float maxHealth = 30f;
        [SerializeField] bool destroyOnDeath = true;

        float currentHealth;
        float maxHealthMultiplier = 1f;

        public event System.Action<Health> Died;
        public event System.Action<Health> Changed;

        public bool IsAlive => currentHealth > 0f;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth * maxHealthMultiplier;

        public float MaxHealthMultiplier
        {
            get => maxHealthMultiplier;
            set
            {
                maxHealthMultiplier = Mathf.Max(0.1f, value);
                currentHealth = Mathf.Min(currentHealth, MaxHealth);
            }
        }

        void Awake()
        {
            currentHealth = MaxHealth;
        }

        /// <summary>
        /// Sets max and current health. Call after spawn to apply definition / wave multipliers.
        /// </summary>
        public void Initialize(float newMaxHealth)
        {
            maxHealth = Mathf.Max(1f, newMaxHealth);
            currentHealth = MaxHealth;
            Changed?.Invoke(this);
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            currentHealth = Mathf.Min(MaxHealth, currentHealth + amount);
            Changed?.Invoke(this);
        }

        public void TakeDamage(float amount, GameObject source)
        {
            if (!IsAlive) return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            Changed?.Invoke(this);
            if (!IsAlive)
            {
                Died?.Invoke(this);
                if (destroyOnDeath)
                    Destroy(gameObject);
            }
        }
    }
}
