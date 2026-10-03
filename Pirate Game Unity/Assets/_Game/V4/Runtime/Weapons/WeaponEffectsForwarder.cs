using System;
using UnityEngine;

namespace RoguePirates.V4.Weapons
{
    /// <summary>
    /// The weapon module's effects sink (M6). The weapon module is built before presentation exists (and player
    /// projectiles keep their sink from construction), so this forwards each call to the presenter registered for
    /// <see cref="IWeaponEffects"/> at that moment, if any. Without one (tests, batch runs, after the presenter is
    /// destroyed) effects are skipped and handles are -1, which the behaviors already treat as "no view".
    /// </summary>
    public sealed class WeaponEffectsForwarder : IWeaponEffects
    {
        readonly Func<IWeaponEffects> _target;

        public WeaponEffectsForwarder(Func<IWeaponEffects> target) { _target = target; }

        /// <summary>Playtest (3 Oct, owner, round 7: "in pvp i couldn't see other player's weapons, attacks"): the 1v1 match
        /// mirrors the visible weapon effects to the opponent (kind, weapon id, point, direction, radius or range).</summary>
        public Action<char, string, Vector3, Vector3, float> Mirror;

        IWeaponEffects T
        {
            get
            {
                var target = _target?.Invoke();
                if (ReferenceEquals(target, this)) return null;
                // A destroyed presenter still exists as a C# object; Unity's null check says it is gone.
                if (target is UnityEngine.Object unityObject && unityObject == null) return null;
                return target;
            }
        }

        public void Fired(string weaponId, Vector3 origin, Vector3 direction) { T?.Fired(weaponId, origin, direction); Mirror?.Invoke('f', weaponId, origin, direction, 0f); }
        public void Aimed(string weaponId, Vector3 aimDirection, Vector3 targetPosition, bool hasTarget) => T?.Aimed(weaponId, aimDirection, targetPosition, hasTarget);
        public void Beam(Vector3 origin, Vector3 direction, float range, float halfWidth) { T?.Beam(origin, direction, range, halfWidth); Mirror?.Invoke('b', null, origin, direction, range); }
        public void SideSalvo(string weaponId, Vector3 shipPosition, Vector3 sideDirection) { T?.SideSalvo(weaponId, shipPosition, sideDirection); Mirror?.Invoke('s', weaponId, shipPosition, sideDirection, 0f); }
        public int MineDropped(Vector3 position) => T?.MineDropped(position) ?? -1;
        public void MineRemoved(int handle) => T?.MineRemoved(handle);
        public void MineSunk(int handle, float seconds) => T?.MineSunk(handle, seconds);
        public void Explosion(Vector3 position, float radius) { T?.Explosion(position, radius); Mirror?.Invoke('e', null, position, Vector3.zero, radius); }
        public void LightningStrike(Vector3 position, float radius) { T?.LightningStrike(position, radius); Mirror?.Invoke('l', null, position, Vector3.zero, radius); }
        public void FlameBurst(Vector3 origin, Vector3 aim) => T?.FlameBurst(origin, aim);
        public void AreaTelegraph(Vector3 position, float radius) { T?.AreaTelegraph(position, radius); Mirror?.Invoke('m', null, position, Vector3.zero, radius); }
        public void TimedRing(Vector3 position, float radius, float seconds) => T?.TimedRing(position, radius, seconds);
        public void AreaImpact(Vector3 position, float radius) { T?.AreaImpact(position, radius); Mirror?.Invoke('a', null, position, Vector3.zero, radius); }
        public void AuraPulse(Vector3 center, float radius) => T?.AuraPulse(center, radius);
        public void WeaponPulse(string weaponId, Vector3 center, float radius) { T?.WeaponPulse(weaponId, center, radius); Mirror?.Invoke('p', weaponId, center, Vector3.zero, radius); }
        public void BurningPatch(Vector3 position, float radius) => T?.BurningPatch(position, radius);
        public void WaterSplash(Vector3 position) => T?.WaterSplash(position);
        public int SkiffSpawned(Vector3 position, Vector3 forward) => T?.SkiffSpawned(position, forward) ?? -1;
        public int SummonSpawned(string weaponId, Vector3 position, Vector3 forward) => T?.SummonSpawned(weaponId, position, forward) ?? -1;
        public void SkiffMoved(int handle, Vector3 position, Vector3 facing) => T?.SkiffMoved(handle, position, facing);
        public void SkiffRemoved(int handle) => T?.SkiffRemoved(handle);
    }
}
