using System;
using HighSeas.Core;
using UnityEngine;

namespace HighSeas.Combat
{
    public interface IDamageable
    {
        bool IsAlive { get; }
        float CurrentHP { get; }
        float MaxHP { get; }
        float Armor { get; }
        bool IsElite { get; }
        void TakeDamage(float amount, Vector3 hitPoint, bool isCrit);
    }

    public sealed class DamageReceiver : MonoBehaviour, IDamageable
    {
        [SerializeField] float maxHP = 100f;
        [SerializeField] float armor;
        [SerializeField] bool isElite;

        float _currentHP;
        float _iframeUntil;

        public bool IsAlive => _currentHP > 0f;
        public float CurrentHP => _currentHP;
        public float MaxHP => maxHP;
        public float Armor => armor;
        public bool IsElite => isElite;

        public event Action<float, bool> Damaged;
        public event Action Died;

        void Awake()
        {
            _currentHP = maxHP;
        }

        public void Initialize(float hull, float armorValue = 0f, bool elite = false)
        {
            maxHP = hull;
            armor = armorValue;
            isElite = elite;
            _currentHP = maxHP;
        }

        public void TakeDamage(float amount, Vector3 hitPoint, bool isCrit)
        {
            if (!IsAlive) return;
            if (Time.time < _iframeUntil) return;
            _iframeUntil = Time.time + GameConstants.ContactIFrame;
            _currentHP = Mathf.Max(0f, _currentHP - amount);
            Damaged?.Invoke(amount, isCrit);
            if (_currentHP <= 0f) Died?.Invoke();
        }

        public void Heal(float amount)
        {
            _currentHP = Mathf.Min(maxHP, _currentHP + amount);
        }
    }
}
