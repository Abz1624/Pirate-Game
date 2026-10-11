using System.Collections.Generic;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Progression;
using RoguePirates.V4.Weapons;
using UnityEngine;

namespace RoguePirates.V5.Net
{
    /// <summary>
    /// V5 M9c S0b (load probe only): what the server's run would have to mirror as effects, recorded from the run's own
    /// presentation sinks (the views the headless server has none of): the real weapon behaviours of the probe ship's build
    /// fire through <see cref="IWeaponEffects"/> and <see cref="IProjectileViews"/>, the enemies through
    /// <see cref="IHostileShotViews"/> and <see cref="IEnemyEffects"/>, kills drop through <see cref="IPickupViews"/> and
    /// <see cref="IPowerupViews"/>. Every call is kept as a raw event with the server tick; <see cref="NetLoadProbe"/>
    /// turns them into the prototype <c>fx</c> encoding's items (salvos as one event, projectiles dead-reckoned). It
    /// returns what the headless forwarders return without a view (null pickups, hostile shots and powerups, so the run
    /// behaves as on the M9b server), except handles for projectiles and summons so their moves and ends are seen
    /// (view-only calls).
    /// </summary>
    public sealed class NetFxRecorder : IWeaponEffects, IProjectileViews, IHostileShotViews, IEnemyEffects, IPickupViews, IPowerupViews
    {
        public enum Kind : byte
        {
            ProjectileSpawn = 1, ProjectileTurn, ProjectileEnd, Fired, Directional, Effect, TimedRing,
            SummonSpawn, SummonMove, SummonEnd, HostileShot, HostileSplash, EnemyExplosion, Pickup, Powerup,
        }

        public struct Raw
        {
            public Kind kind;
            public uint tick;
            /// <summary>0: the probe ship (replayed by the ghosts), 255: the world (enemies).</summary>
            public byte source;
            public byte weapon;
            /// <summary>Ship events: offset from the probe ship when recorded; world events: the world position.</summary>
            public Vector3 offset;
            public int handle;
        }

        public const byte World = 255;

        /// <summary>The newest raw events (oldest first), at most <see cref="KeepTicks"/> old.</summary>
        public readonly List<Raw> Events = new List<Raw>(8192);
        public const uint KeepTicks = 300;
        /// <summary>Calls by kind since the start (the per-second rates in the probe's report).</summary>
        public readonly Dictionary<string, long> Calls = new Dictionary<string, long>();
        public long AimedCalls, ProjectileMoves;

        readonly System.Func<uint> _tick;
        readonly System.Func<Vector3> _ship;
        readonly Dictionary<string, byte> _weapons = new Dictionary<string, byte>();
        public readonly List<string> WeaponNames = new List<string>();
        readonly Dictionary<int, (byte weapon, byte heading, float turnAt)> _projectiles = new Dictionary<int, (byte, byte, float)>();
        int _nextHandle = 1;
        int _nextSummon = 1;

        public NetFxRecorder(System.Func<uint> tick, System.Func<Vector3> shipPosition)
        {
            _tick = tick;
            _ship = shipPosition;
        }

        public void Trim()
        {
            var keepFrom = _tick() > KeepTicks ? _tick() - KeepTicks : 0;
            var n = 0;
            while (n < Events.Count && Events[n].tick < keepFrom) n++;
            if (n > 0) Events.RemoveRange(0, n);
        }

        byte Weapon(string id)
        {
            id ??= "?";
            if (_weapons.TryGetValue(id, out var b)) return b;
            b = (byte)Mathf.Min(254, _weapons.Count);
            _weapons[id] = b;
            WeaponNames.Add(id);
            return b;
        }

        void Count(string name) { Calls.TryGetValue(name, out var n); Calls[name] = n + 1; }

        void Ship(Kind kind, string weaponId, Vector3 position, int handle = 0, string call = null)
        {
            Count(call ?? kind.ToString());
            Events.Add(new Raw { kind = kind, tick = _tick(), source = 0, weapon = Weapon(weaponId), offset = position - _ship(), handle = handle });
        }

        void WorldEvent(Kind kind, Vector3 position, string call)
        {
            Count(call);
            Events.Add(new Raw { kind = kind, tick = _tick(), source = World, offset = position });
        }

        // ---- IProjectileViews ------------------------------------------------------------------------------------------

        public int Acquire(string weaponId, Vector3 position, Color color, float radius)
        {
            var h = _nextHandle++;
            _projectiles[h] = (Weapon(weaponId), 0, -1f);
            Ship(Kind.ProjectileSpawn, weaponId, position, h, "projectile");
            return h;
        }

        public void Move(int handle, Vector3 position, Vector3 velocity)
        {
            ProjectileMoves++;
            if (!_projectiles.TryGetValue(handle, out var p)) return;
            var heading = NetCodec.HeadingByte(velocity);
            var now = Time.realtimeSinceStartup;
            if (p.turnAt < 0f) { _projectiles[handle] = (p.weapon, heading, now); return; } // the first move: the salvo's heading
            var diff = Mathf.Abs((sbyte)(heading - p.heading));
            // Dead reckoning: a correction only when the heading moved over ~5.6 degrees, at most 10 a second.
            if (diff <= 4 || now - p.turnAt < 0.1f) return;
            _projectiles[handle] = (p.weapon, heading, now);
            Count("projectileTurn");
            Events.Add(new Raw { kind = Kind.ProjectileTurn, tick = _tick(), source = 0, weapon = p.weapon, offset = position - _ship(), handle = handle });
        }

        public void Release(int handle)
        {
            if (!_projectiles.Remove(handle)) return;
            Ship(Kind.ProjectileEnd, null, _ship(), handle, "projectileEnd");
        }

        // ---- IWeaponEffects --------------------------------------------------------------------------------------------

        public void Fired(string weaponId, Vector3 origin, Vector3 direction) => Ship(Kind.Fired, weaponId, origin, 0, "fired");
        public void Aimed(string weaponId, Vector3 aimDirection, Vector3 targetPosition, bool hasTarget) => AimedCalls++;
        public void Beam(Vector3 origin, Vector3 direction, float range, float halfWidth) => Ship(Kind.Directional, "beam", origin, 0, "beam");
        public void SideSalvo(string weaponId, Vector3 shipPosition, Vector3 sideDirection) => Ship(Kind.Fired, weaponId, shipPosition, 0, "sideSalvo");
        public int MineDropped(Vector3 position) { var h = _nextSummon++; Ship(Kind.SummonSpawn, "mine", position, h, "mine"); return h; }
        public void MineRemoved(int handle) => Ship(Kind.SummonEnd, "mine", _ship(), handle, "mineEnd");
        public void MineSunk(int handle, float seconds) => Ship(Kind.SummonEnd, "mine", _ship(), handle, "mineEnd");
        public void Explosion(Vector3 position, float radius) => Ship(Kind.Effect, "explosion", position, 0, "explosion");
        public void LightningStrike(Vector3 position, float radius) => Ship(Kind.Effect, "lightning", position, 0, "lightning");
        public void FlameBurst(Vector3 origin, Vector3 aim) => Ship(Kind.Directional, "flame", origin, 0, "flameBurst");
        public void AreaTelegraph(Vector3 position, float radius) => Ship(Kind.Effect, "telegraph", position, 0, "telegraph");
        public void TimedRing(Vector3 position, float radius, float seconds) => Ship(Kind.TimedRing, "ring", position, 0, "timedRing");
        public void AreaImpact(Vector3 position, float radius) => Ship(Kind.Effect, "impact", position, 0, "areaImpact");
        public void AuraPulse(Vector3 center, float radius) => Ship(Kind.Effect, "aura", center, 0, "auraPulse");
        public void WeaponPulse(string weaponId, Vector3 center, float radius) => Ship(Kind.Effect, weaponId, center, 0, "weaponPulse");
        public void BurningPatch(Vector3 position, float radius) => Ship(Kind.Effect, "patch", position, 0, "burningPatch");
        public void WaterSplash(Vector3 position) => Ship(Kind.Effect, "splash", position, 0, "waterSplash");
        public int SkiffSpawned(Vector3 position, Vector3 forward) { var h = _nextSummon++; Ship(Kind.SummonSpawn, "skiff", position, h, "summon"); return h; }
        public int SummonSpawned(string weaponId, Vector3 position, Vector3 forward) { var h = _nextSummon++; Ship(Kind.SummonSpawn, weaponId, position, h, "summon"); return h; }
        public void SkiffMoved(int handle, Vector3 position, Vector3 facing)
        {
            Count("summonMove");
            Events.Add(new Raw { kind = Kind.SummonMove, tick = _tick(), source = 0, offset = position - _ship(), handle = handle });
        }
        public void SkiffRemoved(int handle) => Ship(Kind.SummonEnd, "skiff", _ship(), handle, "summonEnd");

        // ---- Enemies, pickups, powerups (null views, as the headless forwarders return) --------------------------------

        Transform IHostileShotViews.Acquire(Vector3 position, Color color, float visualRadius) { WorldEvent(Kind.HostileShot, position, "hostileShot"); return null; }
        void IHostileShotViews.Release(Transform view) { }
        void IHostileShotViews.Splash(Vector3 position) => WorldEvent(Kind.HostileSplash, position, "hostileSplash");
        public void PlayExplosion(Vector3 position, float blastRadius) => WorldEvent(Kind.EnemyExplosion, position, "enemyExplosion");

        object IPickupViews.Acquire(PickupType type, Vector3 position, int value) { Ship(Kind.Pickup, type.ToString(), position, 0, "pickup"); return null; }
        Vector3 IPickupViews.AlignBarrel(object view) => view is Transform tr && tr != null ? tr.position : Vector3.zero;
        void IPickupViews.BeginHoming(object view, PickupType type) { }
        void IPickupViews.Update(object view, PickupType type, Vector3 position, bool homing, float dt) { }
        void IPickupViews.Release(object view, PickupType type, int value) { }

        object IPowerupViews.Create(PowerupKind kind, Vector3 position) { Ship(Kind.Powerup, kind.ToString(), position, 0, "powerup"); return null; }
        void IPowerupViews.Move(object view, Vector3 position) { }
        void IPowerupViews.Destroy(object view) { }
    }
}
