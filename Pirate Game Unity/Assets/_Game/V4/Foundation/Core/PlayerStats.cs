using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Core
{
    public sealed class PlayerStats
    {
        readonly Dictionary<StatId, float> _base = new();
        readonly List<StatModifier> _modifiers = new();

        public event Action<StatId> StatChanged;

        public PlayerStats()
        {
            SetBase(StatId.Hull, GameConstants.DefaultHull);
            SetBase(StatId.MoveSpeed, GameConstants.DefaultMoveSpeed);
            SetBase(StatId.PickupRadius, GameConstants.DefaultPickupRadius);
            SetBase(StatId.Damage, 1f);
            SetBase(StatId.AttackSpeed, 1f);
            SetBase(StatId.CritChance, 0.05f);
            SetBase(StatId.CritDamage, 1.5f);
            SetBase(StatId.Size, 1f);
            SetBase(StatId.Duration, 1f);
            SetBase(StatId.ProjectileSpeed, 1f);
            SetBase(StatId.Knockback, 1f);
            SetBase(StatId.XPGain, 1f);
            SetBase(StatId.GoldGain, 1f);
            SetBase(StatId.DoubloonGain, 1f);
            SetBase(StatId.EliteDamage, 1f);
            SetBase(StatId.Luck, 0f);
        }

        public void SetBase(StatId stat, float value)
        {
            _base[stat] = value;
            StatChanged?.Invoke(stat);
        }

        public void AddModifier(StatModifier mod) => _modifiers.Add(mod);

        public bool RemoveModifier(StatModifier mod) => _modifiers.Remove(mod);

        public void ClearModifiers() => _modifiers.Clear();

        /// <summary>The stat's base value (before modifiers); 0 when unset.</summary>
        public float GetBase(StatId stat) => _base.TryGetValue(stat, out var v) ? v : 0f;

        public float Get(StatId stat)
        {
            _base.TryGetValue(stat, out var baseVal);
            var additive = 0f;
            var multiplicative = 1f;
            for (var i = 0; i < _modifiers.Count; i++)
            {
                var m = _modifiers[i];
                if (m.stat != stat) continue;
                additive += m.additive;
                multiplicative *= m.multiplicative;
            }

            return (baseVal + additive) * multiplicative;
        }

        public float MaxHull => Get(StatId.Hull);
        public float MoveSpeed => Get(StatId.MoveSpeed);
        public float PickupRadius => Get(StatId.PickupRadius);
        public float GlobalDamage => Get(StatId.Damage);
        public float AttackSpeed => Get(StatId.AttackSpeed);
        public float CritChance => Mathf.Min(1f, Get(StatId.CritChance));
        public float CritDamage => Get(StatId.CritDamage);
        public float Size => Get(StatId.Size);
        public float Duration => Get(StatId.Duration);
        public float ProjectileSpeed => Get(StatId.ProjectileSpeed);
        public float Knockback => Get(StatId.Knockback);
        public float Luck => Get(StatId.Luck);
        public float XPGain => Get(StatId.XPGain);
        public float GoldGain => Get(StatId.GoldGain);
        public float EliteDamage => Get(StatId.EliteDamage);
    }
}
