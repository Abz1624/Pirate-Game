using System;
using UnityEngine;

namespace RoguePirates.V4.Weapons
{
    /// <summary>Weapon combat telemetry (hits, emissions, concurrent targets, control time) for the run's telemetry export.</summary>
    public interface IWeaponTelemetry
    {
        void RecordHit(string weaponId, float damage, bool isCrit, bool isElite, bool isBoss);
        void RecordEmission(string weaponId);
        void RecordConcurrentTargets(string weaponId, int count);
        void RecordControl(string weaponId, float seconds);
    }

    /// <summary>
    /// The weapons' combat rules outside the behaviours (M14; was LegacyWeaponHooks): V3's proc budget (at most 12 triggered
    /// damage events per damage resolution, DamageProcPolicy — per run instead of static), the hit records (the results damage
    /// share listens; telemetry records them) and the Hellfire patches. V4 runs are solo: V3's ship-versus-ship hooks and its
    /// auto-fire playtest flag (a constant false) are gone.
    /// </summary>
    public sealed class WeaponCombatHooks : IWeaponCombatHooks
    {
        public const int MaxProcEventsPerResolution = 12;

        readonly IWeaponTelemetry _telemetry;
        readonly RoguePirates.V4.Enemies.HellfirePatches _hellfire;
        int _procEvents;

        public WeaponCombatHooks(IWeaponTelemetry telemetry, RoguePirates.V4.Enemies.HellfirePatches hellfire)
        {
            _telemetry = telemetry;
            _hellfire = hellfire;
        }

        /// <summary>M12: every recorded weapon hit (ledger id, damage) — the V4 results damage share listens.</summary>
        public event Action<string, float> HitRecorded;

        public void BeginDamageResolution() => _procEvents = 0;

        public bool TryRegisterProcEvent()
        {
            if (_procEvents >= MaxProcEventsPerResolution) return false;
            _procEvents++;
            return true;
        }

        public void RecordHit(string weaponId, float damage, bool isCrit, bool isElite, bool isBoss)
        {
            _telemetry?.RecordHit(weaponId, damage, isCrit, isElite, isBoss);
            HitRecorded?.Invoke(weaponId, damage);
        }

        public void RecordEmission(string weaponId) => _telemetry?.RecordEmission(weaponId);
        public void RecordConcurrentTargets(string weaponId, int count) => _telemetry?.RecordConcurrentTargets(weaponId, count);
        public void RecordControl(string weaponId, float seconds) => _telemetry?.RecordControl(weaponId, seconds);
        public void SpawnFlamePatch(Vector3 position, string weaponId) => _hellfire?.TrySpawnOnDirectFlameKill(position, weaponId);

        public void ApplyShipRadiusDamage(Vector3 center, float radius, float damage, string weaponId) { }

        public bool TryResolveShipAim(string weaponId, out Vector3 direction)
        {
            direction = Vector3.forward;
            return false;
        }

        public bool ShipVersusShip => false;
        public bool IsPvP => false;
        public bool AutoFireDisabled => false;
    }
}
