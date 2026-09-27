using System;
using System.Collections.Generic;
using System.Reflection;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.Meta;
using HighSeas.Player;
using HighSeas.V3;
using HighSeas.V3.MultiShip;
using HighSeas.VFX;
using HighSeas.Weapons;
using RoguePirates.V4.Weapons;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Legacy
{

    /// <summary>The player ship for weapons.</summary>
    [DeletedInPhase(14, "V4 ship snapshot")]
    public sealed class LegacyWeaponShip : IShipState
    {
        readonly Func<PlayerShipController> _ship;
        public LegacyWeaponShip(Func<PlayerShipController> ship) { _ship = ship; }

        PlayerShipController Ship => _ship?.Invoke();
        public bool Exists => Ship != null;
        public Vector3 Position => Ship.Position;
        public Vector3 Velocity => Ship.Velocity;
        public Vector3 MovementForward => Ship.MovementForward;
        public Vector3 TransformPoint(Vector3 local) => Ship.transform.TransformPoint(local);
    }

    /// <summary>The run's combat random stream (RunManager.Rng, seeded per run).</summary>
    [DeletedInPhase(14, "V4 run random")]
    public sealed class LegacyCombatRandom : ICombatRandom
    {
        public float CombatFloat() => RunManager.Instance.Rng.CombatFloat();
    }

    /// <summary>Proc budget, the damage ledger, flame patches, and the ship-versus-ship hooks (inert in solo).</summary>
    [DeletedInPhase(12, "V4 kill path (M8), rewards and telemetry (M12)")]
    public sealed class LegacyWeaponHooks : IWeaponCombatHooks
    {
        readonly Func<PlayerShipController> _ship;
        readonly Func<string, WeaponDefinition> _definition;
        readonly RoguePirates.V4.Enemies.HellfirePatches _hellfire;

        public LegacyWeaponHooks(Func<PlayerShipController> ship, Func<string, WeaponDefinition> definition, RoguePirates.V4.Enemies.HellfirePatches hellfire)
        {
            _ship = ship;
            _definition = definition;
            _hellfire = hellfire;
        }

        public void BeginDamageResolution() => CombatProcRuntime.BeginWeaponDamageResolution();
        public bool TryRegisterProcEvent() => DamageProcPolicy.TryRegisterProcEvent();
        public void RecordHit(string weaponId, float damage, bool isCrit, bool isElite, bool isBoss) => WeaponDamageLedger.RecordHit(weaponId, damage, isCrit, isElite, isBoss);
        public void RecordEmission(string weaponId) => WeaponDamageLedger.RecordEmission(weaponId);
        public void RecordConcurrentTargets(string weaponId, int count) => WeaponDamageLedger.RecordConcurrentTargets(weaponId, count);
        public void RecordControl(string weaponId, float seconds) => WeaponDamageLedger.RecordControl(weaponId, seconds);
        // M8: the V4 hellfire patches (V3's HellfirePatchWorld read V3's EnemyWorld, which left the V4 scenes).
        public void SpawnFlamePatch(Vector3 position, string weaponId) => _hellfire?.TrySpawnOnDirectFlameKill(position, weaponId);

        public void ApplyShipRadiusDamage(Vector3 center, float radius, float damage, string weaponId) =>
            V3ShipCombatRegistry.ApplyRadiusDamage(_ship?.Invoke(), center, radius, damage, false, weaponId);

        public bool TryResolveShipAim(string weaponId, out Vector3 direction)
        {
            direction = Vector3.forward;
            var def = _definition?.Invoke(weaponId);
            var ship = _ship?.Invoke();
            if (def == null || ship == null) return false;
            return V3ShipCombatRegistry.TryResolveAim(ship, def, out _, out direction);
        }

        public bool ShipVersusShip => V3RunSession.AllowShipVsShipCombat;
        public bool IsPvP => V3RunSession.IsPvP;
        public bool AutoFireDisabled => V3PlaytestFlags.ShouldDisableShipAutoFire && V3RunContext.IsActive;
    }
}
