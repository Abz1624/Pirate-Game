using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Encounters;
using HighSeas.Enemies;
using HighSeas.Playtest;
using HighSeas.V3;
using RoguePirates.V4.Stage;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Legacy
{
    /// <summary>The legacy EnemyWorld's boss lookup and boss movement override, as the boss director's enemy world.</summary>
    [DeletedInPhase(8, "V4 enemy world")]
    public sealed class LegacyBossControl : IBossControl
    {
        EnemyWorld _world;
        // V3 found the world with FindFirstObjectByType each frame; cached here (Unity's null check drops a destroyed one).
        EnemyWorld World => _world != null ? _world : (_world = Object.FindFirstObjectByType<EnemyWorld>());

        public bool Exists => World != null;

        public bool TryFindActiveBoss(out BossSnapshot boss)
        {
            boss = default;
            boss.Slot = -1;
            var world = World;
            if (world == null) return false;
            if (!world.TryFindActiveBoss(out var slot, out var agent)) return false;
            boss.Slot = slot;
            boss.Position = agent.position;
            boss.Hp = agent.hp;
            boss.MaxHp = agent.maxHp;
            boss.MoveSpeed = agent.moveSpeed;
            boss.Damage = agent.damage;
            boss.Radius = agent.radius;
            return true;
        }

        public void SetAttackControl(int slot, bool pauseMovement, Vector3 forcedVelocity, bool useForced)
        {
            var world = World;
            if (world != null) world.SetBossAttackControl(slot, pauseMovement, forcedVelocity, useForced);
        }

        public void ClearAttackControl()
        {
            var world = World;
            if (world != null) world.ClearBossAttackControl();
        }
    }

    /// <summary>HostileProjectileWorld.Instance as the boss director's projectile sink.</summary>
    [DeletedInPhase(8, "V4 enemy world (hostile projectiles)")]
    public sealed class LegacyHostileProjectiles : IHostileProjectileSink
    {
        public bool Exists => HostileProjectileWorld.Instance != null;

        public bool TrySpawn(Vector3 position, Vector3 direction, float speed, float damage, float life, float hitRadius, float visualRadius)
        {
            var world = HostileProjectileWorld.Instance;
            return world != null && world.TrySpawn(position, direction, speed, damage, life, hitRadius, visualRadius);
        }
    }

    /// <summary>The boss's explosion effect and toasts (legacy presentation until M14).</summary>
    [DeletedInPhase(8, "Boss effects move to V4 events and presenters with the V4 enemy world")]
    public sealed class LegacyBossEffects : IBossEffects
    {
        public void Explosion(Vector3 position, float radius) => SaltwindCombatEffects.PlayExplosion(position, radius);

        public void Toast(string text, float seconds)
        {
            var hud = V3ToastHud.Instance;
            if (hud != null) hud.Show(text, seconds);
        }
    }

    /// <summary>The boss director's gates that are still legacy state: the boss objective and V3's stage gate.</summary>
    [DeletedInPhase(8, "V4 encounter state")]
    public sealed class LegacyBossObjective
    {
        /// <summary>RunEncounterTracker.BossObjectiveSecured (the director stands down once the boss encounter is done).</summary>
        public bool Secured => RunEncounterTracker.BossObjectiveSecured;

        /// <summary>
        /// V3's first gate: stage balance profile active or the weapon/enemy playtest running. Always true in a solo V4
        /// run (the profile context is begun at run start); false in local multiplayer, where V4 does not begin it.
        /// </summary>
        public bool StageActive => StageBalanceProfileContext.IsActive || WeaponEnemyPlaytestSession.Active;
    }
}
