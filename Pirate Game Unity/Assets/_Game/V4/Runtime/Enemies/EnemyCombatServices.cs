using System;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Enemies;
using UnityEngine;

namespace RoguePirates.V4.Enemies
{
    /// <summary>
    /// The per-run pieces the ported combat code shares (V3 reached them through statics and singletons): the enemy
    /// simulation, hazards, the encounter budget, hostile shots, effects, and the specialist/crab/special-behaviour
    /// modules.
    /// </summary>
    public sealed class EnemyCombatServices
    {
        public EnemySimulation Simulation;
        public HazardTelegraphs Hazards;
        public EncounterBudget Budget;
        public HostileProjectiles Shots;
        public IEnemyEffects Effects;
        public IEnemySpawnParams SpawnParams;
        public EnemyCombatBehaviors Behaviors;
        public EnemySpecialistCombat Specialists;
        public EnemyReefCrabCombat ReefCrab;
        public EnemySpecialBehaviors SpecialBehaviors;
        /// <summary>Queue minions for a summoner (V3: EncounterSpawner.TriggerProfile, switched off since M7). False = none.</summary>
        public Func<SpawnProfile, int, bool> Summon;
        /// <summary>V3's adaptive signals (RunCombatSignals, never created in V3, R-12): null.</summary>
        public RunCombatSignals AdaptiveSignals;

        /// <summary>
        /// M9: the build's incoming-damage multiplier (Brace!, Reinforced Keel). V3's DamageReceiver.TakeDamage read it
        /// from the Captain and item singletons; V4 applies it on every enemy hit before the receiver's own checks.
        /// </summary>
        public Func<float> PlayerIncomingDamage;

        /// <summary>Playtest (6 Oct, owner): who is hitting the ship; the simulation sets it to each enemy's id while that
        /// enemy acts (contact, specials, the shots it fires), for the session recorder. Null without a recorder.</summary>
        public RoguePirates.V4.Events.PlayerDamageSource DamageSource;

        /// <summary>An enemy hit on the ship: DamageReceiver.TakeDamage with the build's incoming multiplier.</summary>
        public void HitPlayer(DamageReceiver receiver, float amount, Vector3 source)
        {
            if (receiver == null) return;
            receiver.TakeDamage(amount * (PlayerIncomingDamage?.Invoke() ?? 1f), source, false);
        }

        public bool CombatEnabled => SpawnParams != null && SpawnParams.CombatEnabled;

        public bool TrySummon(SpawnProfile profile, int count) => Summon != null && Summon(profile, count);

        /// <summary>V3 SaltwindCombatAgentStateStore.GetOrCreate(unitId, def.id).</summary>
        public CombatAgentState State(int unitId, EnemyDefinition def)
        {
            var states = Simulation.CombatStates;
            if (states.TryGet(unitId, out var state)) return state;
            var index = Simulation.Definitions != null ? Array.IndexOf(Simulation.Definitions, def) : -1;
            return states.GetOrCreate(unitId, def != null ? def.id : null, Simulation.RulesFor(index).initialCombatPhaseTimer);
        }
    }
}
