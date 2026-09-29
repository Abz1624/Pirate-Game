using System;
using System.Collections.Generic;
using RoguePirates.V4.Views;
using RoguePirates.V4.Weapons;

namespace RoguePirates.V4.Difficulty
{
    /// <summary>The difficulty director's inputs (M14; was LegacyPowerInputs): the weapon slots, the build's modifiers and the
    /// ship's hull.</summary>
    public sealed class PowerInputs : IPowerInputs
    {
        static readonly IReadOnlyList<WeaponSlot> None = new WeaponSlot[0];
        readonly Func<IWeaponManager> _weapons;
        readonly Func<IWeaponModifiers> _modifiers;
        readonly Func<float> _incoming;
        readonly IShipView _ship;

        public PowerInputs(Func<IWeaponManager> weapons, Func<IWeaponModifiers> modifiers, Func<float> incomingDamageMultiplier, IShipView ship)
        {
            _weapons = weapons;
            _modifiers = modifiers;
            _incoming = incomingDamageMultiplier;
            _ship = ship;
        }

        public IReadOnlyList<WeaponSlot> Weapons => _weapons()?.Slots ?? None;
        public IWeaponModifiers Modifiers => _modifiers();
        public float Hull => _ship != null ? _ship.Hp : 0f;
        public float MaxHull => _ship != null ? _ship.MaxHp : 0f;
        public float IncomingDamageMultiplier => _incoming();
    }
}
