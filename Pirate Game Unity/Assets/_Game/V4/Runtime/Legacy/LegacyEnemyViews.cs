using System;
using System.Reflection;
using HighSeas.Combat;
using HighSeas.Enemies;
using RoguePirates.V4.Enemies;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Legacy
{
    /// <summary>
    /// The read-only enemy views and dev actions over the legacy enemy world (M8 step 0, so the trace recorder, the
    /// trace pilot and the console read one contract before the world itself is swapped). Also used by DevTools in V3
    /// scenes (the V3 golden recordings), where there is no V4 run context.
    /// </summary>
    [DeletedInPhase(15, "V3 retirement: kept for the V3-scene golden recordings")]
    public sealed class LegacyEnemyWorldView : IEnemyWorldView, IEnemyDevControl
    {
        EnemyWorld _world;

        EnemyWorld World
        {
            get
            {
                if (_world == null) _world = Object.FindFirstObjectByType<EnemyWorld>();
                return _world;
            }
        }

        public bool Exists => World != null && World.Agents != null;
        public int Capacity => Exists ? World.Agents.Length : 0;
        public int ActiveCount => World != null ? World.ActiveCount : 0;

        public bool TryGet(int slot, out EnemySnapshot enemy)
        {
            enemy = default;
            if (!Exists) return false;
            var agents = World.Agents;
            if (slot < 0 || slot >= agents.Length || !agents[slot].active) return false;
            var a = agents[slot];
            var defs = World.Definitions;
            var def = defs != null && a.definitionIndex >= 0 && a.definitionIndex < defs.Length ? defs[a.definitionIndex] : null;
            enemy = new EnemySnapshot
            {
                slot = slot,
                unitId = a.id,
                definitionIndex = a.definitionIndex,
                enemyId = def != null ? def.id : "?",
                position = a.position,
                velocity = a.velocity,
                radius = a.radius,
                hp = a.hp,
                maxHp = a.maxHp,
                isElite = a.isElite,
                isBoss = a.isBoss
            };
            return true;
        }

        public bool TryFindByUnitId(int unitId, out int slot)
        {
            slot = -1;
            return World != null && World.TryGetAgentByUnitId(unitId, out slot);
        }

        public void Kill(int slot, string weaponId)
        {
            if (!Exists || slot < 0 || slot >= World.Agents.Length) return;
            var agent = World.Agents[slot];
            if (!agent.active) return;
            World.ApplyDamage(slot, Mathf.Max(agent.hp, agent.maxHp) + 500f, false, weaponId: weaponId);
        }

        public void SetHealthFraction(int slot, float fraction)
        {
            if (!Exists || slot < 0 || slot >= World.Agents.Length) return;
            var agents = World.Agents;
            var agent = agents[slot];
            if (!agent.active) return;
            agent.hp = Mathf.Clamp(agent.maxHp * fraction, 1f, agent.maxHp);
            agents[slot] = agent;
        }

        public void DespawnAll() => World?.DespawnAllActive();
    }

    /// <summary>Hostile projectiles in the legacy pool (private, read by reflection: V3 code is not edited, D1).</summary>
    [DeletedInPhase(15, "V3 retirement: kept for the V3-scene golden recordings")]
    public sealed class LegacyHostileProjectileView : IHostileProjectileView
    {
        static readonly FieldInfo Pool = typeof(HostileProjectileWorld).GetField("_pool", BindingFlags.Instance | BindingFlags.NonPublic);

        static HostileProjectile[] Shots => HostileProjectileWorld.Instance != null ? Pool?.GetValue(HostileProjectileWorld.Instance) as HostileProjectile[] : null;

        public int Capacity => Shots?.Length ?? 0;

        public bool TryGet(int index, out HostileShotSnapshot shot)
        {
            shot = default;
            var pool = Shots;
            if (pool == null || index < 0 || index >= pool.Length || !pool[index].active) return false;
            shot = new HostileShotSnapshot { position = pool[index].position, velocity = pool[index].velocity };
            return true;
        }
    }

    /// <summary>The legacy static hazard slots (private nested type, read by reflection).</summary>
    [DeletedInPhase(15, "V3 retirement: kept for the V3-scene golden recordings")]
    public sealed class LegacyHazardView : IHazardView
    {
        static readonly FieldInfo Slots = typeof(SaltwindHazardTelegraph).GetField("Slots", BindingFlags.Static | BindingFlags.NonPublic);
        static FieldInfo _active, _center, _radius, _timer, _armed;

        static Array All => Slots?.GetValue(null) as Array;

        public int Capacity => All?.Length ?? 0;

        public bool TryGet(int index, out HazardSnapshot hazard)
        {
            hazard = default;
            var slots = All;
            if (slots == null || index < 0 || index >= slots.Length) return false;
            var slot = slots.GetValue(index);
            if (slot == null) return false;
            if (_active == null)
            {
                var type = slot.GetType();
                _active = type.GetField("active");
                _center = type.GetField("center");
                _radius = type.GetField("radius");
                _timer = type.GetField("timer");
                _armed = type.GetField("armed");
            }
            if (!(bool)_active.GetValue(slot)) return false;
            hazard = new HazardSnapshot
            {
                center = (Vector3)_center.GetValue(slot),
                radius = (float)_radius.GetValue(slot),
                timer = (float)_timer.GetValue(slot),
                armed = (bool)_armed.GetValue(slot)
            };
            return true;
        }
    }
}
