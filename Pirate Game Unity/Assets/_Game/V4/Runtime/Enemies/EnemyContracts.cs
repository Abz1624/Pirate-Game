using UnityEngine;

namespace RoguePirates.V4.Enemies
{
    /// <summary>One enemy as the read-only views see it.</summary>
    public struct EnemySnapshot
    {
        public int slot;
        public int unitId;
        public int definitionIndex;
        /// <summary>Content id of the enemy's definition ("?" when the definition is missing).</summary>
        public string enemyId;
        public Vector3 position;
        public Vector3 velocity;
        public float radius;
        public float hp;
        public float maxHp;
        public bool isElite;
        public bool isBoss;
    }

    /// <summary>
    /// Read-only access to the enemies, slot by slot in the simulation's own slot order (the trace recorder and the
    /// trace pilot depend on that order). M8 step 0: over the legacy enemy world; then the V4 enemy world.
    /// </summary>
    public interface IEnemyWorldView
    {
        bool Exists { get; }
        int Capacity { get; }
        int ActiveCount { get; }
        /// <summary>False for an empty slot or an index out of range.</summary>
        bool TryGet(int slot, out EnemySnapshot enemy);
        bool TryFindByUnitId(int unitId, out int slot);
    }

    /// <summary>Development actions on enemies (Tuning Console, trace runner). Never used by gameplay code.</summary>
    public interface IEnemyDevControl
    {
        /// <summary>A lethal hit through the normal weapon hit path (max(hp, maxHp) + 500 damage, no crit).</summary>
        void Kill(int slot, string weaponId);
        /// <summary>Sets health to a fraction of maximum (clamped to at least 1).</summary>
        void SetHealthFraction(int slot, float fraction);
        void DespawnAll();
    }

    public struct HostileShotSnapshot
    {
        public Vector3 position;
        public Vector3 velocity;
    }

    /// <summary>Enemy projectiles in flight, in pool order.</summary>
    public interface IHostileProjectileView
    {
        int Capacity { get; }
        bool TryGet(int index, out HostileShotSnapshot shot);
    }

    public enum HazardKind { Explosion, NetSlow, Mine }

    public struct HazardSnapshot
    {
        public HazardKind kind;
        public Vector3 center;
        public float radius;
        public float timer;
        public bool armed;
    }

    /// <summary>Telegraphed hazard circles (Kraken traps, mines, nets), in slot order.</summary>
    public interface IHazardView
    {
        int Capacity { get; }
        bool TryGet(int index, out HazardSnapshot hazard);
    }
}
