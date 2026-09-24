using System.Collections.Generic;
using HighSeas.Combat;
using UnityEngine;

namespace HighSeas.Enemies
{
    public sealed class SaltwindCombatAgentState
    {
        public string enemyId;
        public EnemyCombatPhase phase = EnemyCombatPhase.Approach;
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

    public static class SaltwindCombatAgentStateStore
    {
        static readonly Dictionary<int, SaltwindCombatAgentState> States = new();

        public static SaltwindCombatAgentState GetOrCreate(int unitId, string enemyId)
        {
            if (!States.TryGetValue(unitId, out var state))
            {
                state = new SaltwindCombatAgentState
                {
                    enemyId = enemyId,
                    orbitSign = (unitId & 1) == 0 ? 1f : -1f,
                    phaseTimer = enemyId == "powder_barge" ? 2f : 0f
                };
                States[unitId] = state;
            }

            return state;
        }

        public static bool TryGet(int unitId, out SaltwindCombatAgentState state) =>
            States.TryGetValue(unitId, out state);

        public static void OnSpawn(int unitId, string enemyId) => GetOrCreate(unitId, enemyId);

        public static void ClearAgent(int unitId) => States.Remove(unitId);

        public static void ClearAll() => States.Clear();
    }
}
