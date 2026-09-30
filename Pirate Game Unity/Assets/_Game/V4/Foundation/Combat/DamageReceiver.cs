using System;
using RoguePirates.V4.Foundation.Core;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Combat
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
        [SerializeField] float maxHP = GameConstants.DefaultHull;
        [SerializeField] float armor;
        [SerializeField] bool isElite;

        float _currentHP;
        float _iframeUntil;
        bool _playtestInvulnerable;

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

        public void SetArmor(float armorValue) => armor = armorValue;

        /// <summary>When true, <see cref="TakeDamage"/> is ignored (weapon/enemy playtest scene only).</summary>
        public void SetPlaytestInvulnerable(bool invulnerable) => _playtestInvulnerable = invulnerable;

        /// <summary>Updates max HP without a free full heal (preserves current HP ratio by default).</summary>
        public void AdjustMaxHP(float newMax, bool preserveRatio = true)
        {
            newMax = Mathf.Max(1f, newMax);
            if (Mathf.Approximately(maxHP, newMax))
                return;

            var ratio = preserveRatio && maxHP > 0.01f ? _currentHP / maxHP : 1f;
            maxHP = newMax;
            _currentHP = preserveRatio
                ? Mathf.Min(maxHP, maxHP * ratio)
                : Mathf.Min(_currentHP, maxHP);
        }

        public void TakeDamage(float amount, Vector3 hitPoint, bool isCrit)
        {
            if (!IsAlive) return;
            if (_playtestInvulnerable) return;
            if (Time.time < _iframeUntil) return;
            _iframeUntil = Time.time + GameConstants.ContactIFrame;
            _currentHP = Mathf.Max(0f, _currentHP - amount);
            Damaged?.Invoke(amount, isCrit);
            if (_currentHP <= 0f) Died?.Invoke();
        }

        /// <summary>
        /// V5 island impact: a knock that neither checks nor starts the contact hit immunity (an enemy hit right after a
        /// bump still lands, and a bump right after an enemy hit still counts), but raises the usual Damaged and Died
        /// events so the hit feedback, the items and the run see it.
        /// </summary>
        public void TakeImpact(float amount, Vector3 hitPoint)
        {
            if (!IsAlive || _playtestInvulnerable || amount <= 0f) return;
            _currentHP = Mathf.Max(0f, _currentHP - amount);
            Damaged?.Invoke(amount, false);
            if (_currentHP <= 0f) Died?.Invoke();
        }

        public void Heal(float amount)
        {
            _currentHP = Mathf.Min(maxHP, _currentHP + amount);
        }
    }
}
