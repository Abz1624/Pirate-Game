using System;
using System.Collections.Generic;
using HighSeas.Player;
using RoguePirates.V4.Difficulty;
using RoguePirates.V4.Weapons;

namespace RoguePirates.V4.Legacy
{
    /// <summary>The difficulty director's inputs (M10): the weapon slots, the build's modifiers and the kept ship's hull.</summary>
    [DeletedInPhase(14, "V4 ship snapshot")]
    public sealed class LegacyPowerInputs : IPowerInputs
    {
        readonly Func<IWeaponManager> _weapons;
        readonly LegacyBuildWiring _build;
        readonly Func<PlayerShipController> _ship;
        static readonly IReadOnlyList<WeaponSlot> None = new WeaponSlot[0];

        public LegacyPowerInputs(Func<IWeaponManager> weapons, LegacyBuildWiring build, Func<PlayerShipController> ship)
        {
            _weapons = weapons;
            _build = build;
            _ship = ship;
        }

        PlayerShipController Ship => _ship();
        public IReadOnlyList<WeaponSlot> Weapons => _weapons()?.Slots ?? None;
        public IWeaponModifiers Modifiers => _build.WeaponModifiers;
        public float Hull => Ship != null && Ship.Damage != null ? Ship.Damage.CurrentHP : 0f;
        public float MaxHull => Ship != null && Ship.Damage != null ? Ship.Damage.MaxHP : 0f;
        public float IncomingDamageMultiplier => _build.IncomingDamageMultiplier();
    }
}
