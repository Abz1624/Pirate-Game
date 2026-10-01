using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.Enemies;
using UnityEngine;

namespace RoguePirates.V4.Enemies
{
    /// <summary>Run values the enemy simulation reads at spawn (V3 read RunManager and V3RunContext directly).</summary>
    public interface IEnemyRunState
    {
        bool Exists { get; }
        float ElapsedSeconds { get; }
        int Tier { get; }
        int StageIndex { get; }
        /// <summary>Global enemy model scale of the run (V3 solo: 0.88; it also scales the gameplay radius).</summary>
        float EnemyVisualScale { get; }
    }

    /// <summary>
    /// Enemy models and health bars. They stay V3's (<c>EnemyViewPool</c> rooted at /Systems, created at the same
    /// moments) until M14, because creating a view toggles /Systems and a new ship model keeps its island colliders
    /// until the end of the frame (R-81), which the simulation's island checks see.
    /// </summary>
    public interface IEnemyViewSink
    {
        /// <summary>Called when the run's definitions are set (V3 strips NavMesh components from each model prefab).</summary>
        void Prepare(EnemyDefinition[] definitions);
        /// <summary>Model scale for a definition without a model (V3 EnemyVisualFallbackResolver; it sets the radius).</summary>
        float FallbackVisualScale(EnemyDefinition def);
        Transform CreateView(EnemyDefinition def, Vector3 position, float scale, bool elite, int animationSeed);
        /// <summary>The health bar bound to a view, or null (no bar prefab or no view).</summary>
        Transform AttachHealthBar(EnemyDefinition def, Transform view, bool elite);
        /// <summary>Stops the burn flash, returns the model to its pool (or destroys a placeholder) and destroys the bar.</summary>
        void Release(EnemyDefinition def, Transform view, Transform healthBar);
        /// <summary>Swordfish-style spring pose while dashing (V3 EnemyViewAnimation.TickSwordfish).</summary>
        void TickSpring(Transform view, ref bool springing, bool isSpringing);
        /// <summary>Ship wake (wake and foam parts on while moving).</summary>
        void TickWake(Transform view, EnemyCategory category, Vector3 velocity);
        void SetBurning(Transform view, bool burning);
        void SetHealthFill(Transform healthBar, float fill);
    }

    /// <summary>
    /// Hit feedback that is V3 presentation until M14 (damage highlight, camera shake and hit sound, the TriggerBus hit
    /// event, damage numbers). Called at V3's points in V3's order: the damage numbers draw from Unity's global random
    /// state, which later feeds pickup offsets, so moving them changes the run.
    /// </summary>
    public interface IEnemyFeedback
    {
        void DamageHighlight(Transform view);
        void HitShakeAndSound(float damage);
        void RaiseHit(float damage, bool isCrit, Vector3 position, string weaponId);
        void DamageNumber(Vector3 position, float damage, bool isCrit);
    }

    /// <summary>
    /// Everything V3's kill path told other systems, in V3's order (kill count, the gold roll on the Director stream,
    /// pickups, floor powerup roll, encounter completion, scheduled-elite Marks, the miniboss arena, the TriggerBus kill
    /// event (which the stage content hears first for the boss), item kill counters, the weapon damage ledger). The
    /// despawn follows.
    /// </summary>
    public interface IEnemyKillTargets
    {
        void OnKilled(in EnemyUnit unit, EnemyDefinition def);
    }

    /// <summary>
    /// Movement and attack parameters for a spawn (V3 SpawnProfileResolver over the combat-profile registry, which V4
    /// fills from the archetype combat blocks). Kept on the shared resolver in M8; its own id rules stay legacy code.
    /// </summary>
    public interface IEnemySpawnParams
    {
        /// <summary>The Saltwind combat feature is on (the stage profile says so).</summary>
        bool CombatEnabled { get; }
        bool HasCombatProfile(EnemyDefinition def);
        /// <summary>M14: the run's combat profile of an enemy id (was V3's static registry).</summary>
        bool TryGetCombatProfile(string enemyId, out RoguePirates.V4.Foundation.Combat.EnemyCombatProfile profile);
        /// <summary>Combat on and the enemy has a combat profile (V3 EnemyCombatProfileRegistry.UsesSaltwindBehavior).</summary>
        bool UsesCombatBehavior(string enemyId);
        EnemySpawnRuntimeParams ResolveForDefinition(EnemyDefinition def);
        EnemySpawnRuntimeParams Resolve(SpawnProfile profile, EnemyDefinition def, int profileInstanceId, FormationRole role, int formationSlot, int leaderUnitId);
    }
}
