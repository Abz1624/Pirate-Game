using System;
using System.Linq;
using RoguePirates.V4.Config;
using RoguePirates.V4.Spawning;
using UnityEngine;

namespace RoguePirates.V4.Enemies
{
    /// <summary>
    /// What the Hellfire patches need (M14; was LegacyHellfireEnvironment): the run gate, the tick damage roll and the
    /// evolution check. The roll is the V4 damage calculator (V3 DamageSystem.RollDamage, base 4 with the Flamethrower's
    /// id, on the combat stream) and the check reads the V4 progression's evolutions; both are bound once the modules exist.
    /// </summary>
    public sealed class HellfireEnvironment : IHellfireEnvironment
    {
        readonly IRunStatus _run;
        readonly Func<ConfigSetRuntime> _config;
        string _damageWeaponId;

        public HellfireEnvironment(IRunStatus run, Func<ConfigSetRuntime> config)
        {
            _run = run;
            _config = config;
        }

        /// <summary>The weapon id V3's tick roll used: the base weapon of the evolution that leaves patches (the Flamethrower).</summary>
        public string DamageWeaponId => _damageWeaponId ??= _config?.Invoke()?.All<WeaponEvolutionConfig>()
            .FirstOrDefault(e => e.effect == ConfigEnums.EvolutionEffect.WideConeWithPatches)?.baseWeapon?.weaponId ?? string.Empty;

        public Func<RoguePirates.V4.Weapons.DamageCalculator> Calculator;
        public Func<string, bool> HasHellfire;
        /// <summary>R-40/R-49: the patch tick × this (status damage); 1 unless the switches are on.</summary>
        public Func<float> StatusDamage;

        public bool RunActive => _run != null && _run.Exists && !_run.IsPaused && !_run.IsRunEnded;
        public float RollTickDamage(bool targetIsElite) =>
            Calculator().RollAmount(4f, 1f, targetIsElite, 0f, 0f, DamageWeaponId) * (StatusDamage != null ? StatusDamage() : 1f);
        public bool IsHellfireWeapon(string weaponId) => HasHellfire != null && HasHellfire(weaponId);

        /// <summary>No effect: V3's burning-oil prefab rendered with a broken material (R-104); the patches show as ground
        /// rings instead (M8 playtest).</summary>
        public void OilDropped(Vector3 position, float radius) { }
    }
}
