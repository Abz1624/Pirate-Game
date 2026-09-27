using HighSeas.Core;
using HighSeas.Enemies;
using UnityEngine;

namespace RoguePirates.V4.Enemies
{
    /// <summary>One enemy slot. Port of V3's EnemyAgent, field for field (same names, so the port reads like V3).</summary>
    public struct EnemyUnit
    {
        public int id;
        public bool active;
        public Vector3 position;
        public Vector3 velocity;
        public float hp;
        public float maxHp;
        public float damage;
        public float moveSpeed;
        public float radius;
        public float contactCooldown;
        public float chargeTimer;
        public float attackCooldown;
        public float knockbackTimer;
        public Vector3 knockbackDir;
        public int definitionIndex;
        public EliteAffix affix;
        public bool isElite;
        public bool isBoss;
        public Transform view;
        public Transform healthBar;
        public string lastHitWeaponId;
        public bool swordfishSpringVisual;
        public SpawnProfile spawnProfile;
        public EnemySpawnRuntimeParams patternParams;
    }

    /// <summary>Per-enemy combat state (port of V3's SaltwindCombatAgentState).</summary>
    public sealed class CombatAgentState
    {
        public string enemyId;
        public HighSeas.Combat.EnemyCombatPhase phase = HighSeas.Combat.EnemyCombatPhase.Approach;
        public float phaseTimer;
        public int attackSequence;
        public int attackId;
        public Vector3 committedDirection;
        public Vector3 committedPosition;
        public float orbitSign = 1f;
        public int chargePassCount;
        public bool contactDisabled;
        public string lastDeferReason;
    }

    /// <summary>
    /// Combat state by unit id, per run (V3 kept it in a static dictionary that survived scene reloads). Port of
    /// SaltwindCombatAgentStateStore; V3's "powder_barge starts with a 2 s phase timer" is the archetype's
    /// <c>world.initialCombatPhaseTimer</c>.
    /// </summary>
    public sealed class CombatStateStore
    {
        readonly System.Collections.Generic.Dictionary<int, CombatAgentState> _states = new System.Collections.Generic.Dictionary<int, CombatAgentState>();

        public CombatAgentState GetOrCreate(int unitId, string enemyId, float initialPhaseTimer)
        {
            if (!_states.TryGetValue(unitId, out var state))
            {
                state = new CombatAgentState
                {
                    enemyId = enemyId,
                    orbitSign = (unitId & 1) == 0 ? 1f : -1f,
                    phaseTimer = initialPhaseTimer
                };
                _states[unitId] = state;
            }
            return state;
        }

        public bool TryGet(int unitId, out CombatAgentState state) => _states.TryGetValue(unitId, out state);
        public void Clear(int unitId) => _states.Remove(unitId);
        public void ClearAll() => _states.Clear();
        public int Count => _states.Count;
    }
}
