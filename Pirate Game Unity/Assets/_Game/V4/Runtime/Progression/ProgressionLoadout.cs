using System;
using System.Collections.Generic;
using RoguePirates.V4.Config;
using RoguePirates.V4.Weapons;
using UnityEngine;

namespace RoguePirates.V4.Progression
{
    /// <summary>One equipped weapon and its upgrades (V3 WeaponController.WeaponInstance's level-up fields).</summary>
    public sealed class LoadoutWeapon
    {
        public WeaponConfig Config;
        public int Level = 1;
        public float DamageBonus = 1f;
        public int QuantityBonus;
        public float CooldownMult = 1f, SpeedMult = 1f, SizeMult = 1f, DurationMult = 1f, KnockbackMult = 1f;
        public readonly Dictionary<ConfigEnums.UpgradeAxis, float> Accumulated = new Dictionary<ConfigEnums.UpgradeAxis, float>();
        public string Id => Config != null ? Config.weaponId : null;
    }

    /// <summary>
    /// The run's weapons (M9): what V3 kept on the switched-off WeaponController record — equip order, levels and
    /// upgrade values (EquipWeapon, ApplyUpgradePackage and ApplyAxisValue ported). The weapon module reads it through
    /// <see cref="IWeaponLoadout"/>.
    /// </summary>
    public sealed class ProgressionLoadout : IWeaponLoadout
    {
        readonly Func<string, WeaponConfig> _config;
        readonly Func<string, string> _evolution;
        readonly List<LoadoutWeapon> _weapons = new List<LoadoutWeapon>();
        int _maxSlots = 1;

        /// <param name="config">Weapon config by id.</param>
        /// <param name="evolutionOf">The evolution a weapon has taken (null = none).</param>
        public ProgressionLoadout(Func<string, WeaponConfig> config, Func<string, string> evolutionOf)
        {
            _config = config;
            _evolution = evolutionOf;
        }

        public IReadOnlyList<LoadoutWeapon> Weapons => _weapons;
        public int MaxSlots => _maxSlots;
        /// <summary>A new weapon's damage bonus (V3 pacing startingDamageMultiplier).</summary>
        public float NewWeaponDamageBonus = 1f;

        /// <summary>V3 EnsureWeaponSlotCapacity: never shrinks.</summary>
        public void EnsureSlotCapacity(int slots) => _maxSlots = Mathf.Max(_maxSlots, Mathf.Max(1, slots));

        public void Clear() => _weapons.Clear();

        /// <summary>Dev tools: these weapons in this order; ones already carried keep their state, new ones start fresh.</summary>
        public void Rebuild(IReadOnlyList<string> weaponIds)
        {
            var kept = new List<LoadoutWeapon>(_weapons);
            _weapons.Clear();
            foreach (var id in weaponIds)
            {
                if (_weapons.Count >= _maxSlots) break;
                var old = kept.Find(w => w.Id == id);
                if (old != null) _weapons.Add(old);
                else Equip(id);
            }
        }

        /// <summary>V3 EquipWeapon: fails when full or the weapon is unknown.</summary>
        public bool Equip(string weaponId)
        {
            if (_weapons.Count >= _maxSlots) return false;
            var config = _config(weaponId);
            if (config == null) return false;
            _weapons.Add(new LoadoutWeapon { Config = config, Level = 1, DamageBonus = NewWeaponDamageBonus });
            return true;
        }

        public int IndexOf(string weaponId)
        {
            for (var i = 0; i < _weapons.Count; i++) if (_weapons[i].Id == weaponId) return i;
            return -1;
        }

        /// <summary>V3 ApplyUpgradePackage: nothing at max level; else every line, then level + 1.</summary>
        public void ApplyUpgradePackage(int index, UpgradeLine[] lines)
        {
            if (index < 0 || index >= _weapons.Count || lines == null) return;
            var w = _weapons[index];
            if (w.Level >= w.Config.maxLevel) return;
            foreach (var line in lines) ApplyAxisValue(w, line.Axis, line.Value);
            w.Level++;
        }

        static void ApplyAxisValue(LoadoutWeapon w, ConfigEnums.UpgradeAxis axis, float value)
        {
            if (value <= 0f) return;
            w.Accumulated.TryGetValue(axis, out var accumulated);
            var cfg = Array.Find(w.Config.upgradeAxes ?? Array.Empty<WeaponUpgradeAxis>(), a => a.axis == axis);
            if (cfg != null && cfg.maxAccumulated > 0f)
            {
                var room = Mathf.Max(0f, cfg.maxAccumulated - accumulated);
                value = Mathf.Min(value, room);
                if (value <= 0f) return;
            }
            w.Accumulated[axis] = accumulated + value;
            switch (axis)
            {
                case ConfigEnums.UpgradeAxis.Damage: w.DamageBonus *= 1f + value; break;
                case ConfigEnums.UpgradeAxis.Cooldown: w.CooldownMult *= 1f - Mathf.Clamp01(value); break;
                case ConfigEnums.UpgradeAxis.Quantity: w.QuantityBonus += Mathf.Max(1, Mathf.RoundToInt(value)); break;
                case ConfigEnums.UpgradeAxis.Speed: w.SpeedMult *= 1f + value; break;
                case ConfigEnums.UpgradeAxis.Size: w.SizeMult *= 1f + value; break;
                case ConfigEnums.UpgradeAxis.Duration: w.DurationMult *= 1f + value; break;
                case ConfigEnums.UpgradeAxis.Knockback: w.KnockbackMult *= 1f + value; break;
            }
        }

        // ---- IWeaponLoadout ------------------------------------------------------------------------------------------

        public int Count => _weapons.Count;

        public LoadoutEntry Get(int index)
        {
            var w = _weapons[index];
            return new LoadoutEntry
            {
                Key = w, WeaponId = w.Id, EvolutionId = _evolution?.Invoke(w.Id), Level = w.Level,
                DamageBonus = w.DamageBonus, QuantityBonus = w.QuantityBonus, CooldownMult = w.CooldownMult,
                SpeedMult = w.SpeedMult, SizeMult = w.SizeMult, DurationMult = w.DurationMult, KnockbackMult = w.KnockbackMult,
            };
        }

        /// <summary>Firing state has no reader outside the weapon module once the V3 record is gone.</summary>
        public void Mirror(int index, in LoadoutMirror state) { }
    }
}
