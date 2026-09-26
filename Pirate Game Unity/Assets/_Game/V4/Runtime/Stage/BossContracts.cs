using RoguePirates.V4.Config;
using UnityEngine;

namespace RoguePirates.V4.Stage
{
    /// <summary>The active boss as the boss director reads it each frame (a copy; V3 read the enemy world's agent struct).</summary>
    public struct BossSnapshot
    {
        public int Slot;
        public Vector3 Position;
        public float Hp, MaxHp, MoveSpeed, Damage, Radius;
    }

    /// <summary>
    /// What the boss director needs from the enemy world: the active boss and its movement override (pause during
    /// telegraphs and rests, a forced velocity during a charge). M7: the legacy EnemyWorld; M8: the V4 enemy world.
    /// </summary>
    public interface IBossControl
    {
        /// <summary>False while there is no enemy world (V3's director did nothing then, not even a reset).</summary>
        bool Exists { get; }
        /// <summary>The first active boss (V3: the first active agent flagged boss, by slot).</summary>
        bool TryFindActiveBoss(out BossSnapshot boss);
        void SetAttackControl(int slot, bool pauseMovement, Vector3 forcedVelocity, bool useForced);
        void ClearAttackControl();
    }

    /// <summary>Hostile projectiles (V3 HostileProjectileWorld.TrySpawn, same parameters).</summary>
    public interface IHostileProjectileSink
    {
        /// <summary>False when there is no hostile projectile world (V3 skipped the whole volley then).</summary>
        bool Exists { get; }
        bool TrySpawn(Vector3 position, Vector3 direction, float speed, float damage, float life, float hitRadius, float visualRadius);
    }

    /// <summary>Boss side effects that are still legacy presentation (explosion effect, toasts; presenters by M14).</summary>
    public interface IBossEffects
    {
        void Explosion(Vector3 position, float radius);
        void Toast(string text, float seconds);
    }

    public enum BossPhase { Recovery = 0, Telegraph = 1, Execute = 2 }

    /// <summary>What presentation reads about the boss's attack pattern (read-only).</summary>
    public interface IBossView
    {
        /// <summary>A boss is being driven (found and the fight not yet secured).</summary>
        bool Active { get; }
        BossAttackKind CurrentAttack { get; }
        BossPhase Phase { get; }
        float PhaseTimer { get; }
        /// <summary>Health phase 1-3 (0 before the first tick).</summary>
        int ScriptPhase { get; }
        bool Enraged { get; }
        /// <summary>Where the ship was when the current attack began telegraphing.</summary>
        Vector3 MarkedPoint { get; }
        /// <summary>
        /// The marked circle is shown (telegraph of a bombardment or slam). Updated only when the director runs past
        /// its gates, as V3's circle was: it stays as it was while the run is paused.
        /// </summary>
        bool MarkedTelegraphVisible { get; }
        Vector3 MarkedTelegraphCenter { get; }
        float MarkedTelegraphRadius { get; }
    }
}
