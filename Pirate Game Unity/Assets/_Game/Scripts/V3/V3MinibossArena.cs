using HighSeas.Combat;
using HighSeas.Enemies;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Survivor-style miniboss moment: cull trash, pause ambient spawns until the boss agent dies.</summary>
    public static class V3MinibossArena
    {
        public static bool Active { get; private set; }
        public static bool BlocksAmbientSpawning => Active;
        public static int FocusAgentId { get; private set; } = -1;

        public static void ResetForRun()
        {
            Active = false;
            FocusAgentId = -1;
        }

        public static void Begin(EnemyWorld world, EncounterSpawner spawner, int focusAgentId, string displayName)
        {
            if (world == null || focusAgentId <= 0) return;
            if (!world.TryGetAgentByUnitId(focusAgentId, out _))
                return;

            if (Active)
            {
                if (FocusAgentId == focusAgentId) return;
                End();
            }

            FocusAgentId = focusAgentId;
            Active = true;
            var culled = world.CullAllExceptAgent(focusAgentId);
            if (!world.TryGetAgentByUnitId(focusAgentId, out _))
            {
                Active = false;
                FocusAgentId = -1;
                return;
            }

            spawner?.SetMinibossArenaActive(true);
            V3ToastHud.Instance?.Show($"MINI BOSS — {displayName}", 4f);
            RunCombatTelemetry.Instance?.Record("miniboss_arena_begin", new System.Collections.Generic.Dictionary<string, object>
            {
                ["agentId"] = focusAgentId,
                ["label"] = displayName,
                ["culled"] = culled
            });
        }

        public static void NotifyAgentKilled(int agentId)
        {
            if (!Active || agentId != FocusAgentId) return;
            End();
        }

        static void End()
        {
            Active = false;
            FocusAgentId = -1;
            EncounterSpawner.Instance?.SetMinibossArenaActive(false);
            V3ToastHud.Instance?.Show("Arena clear — enemies return!", 3f);
            RunCombatTelemetry.Instance?.Record("miniboss_arena_end", null);
        }
    }
}
