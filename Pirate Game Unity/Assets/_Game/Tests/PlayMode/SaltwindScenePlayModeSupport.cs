using System.Collections;
using HighSeas.Balance;
using HighSeas.Core;
using HighSeas.Encounters;
using HighSeas.Enemies;
using HighSeas.POI;
using HighSeas.V3;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HighSeas.Tests
{
    public sealed class BossSummonAttemptResult
    {
        public bool Summoned;
    }

    public sealed class BossAgentWaitResult
    {
        public int AgentIndex = -1;
    }

    static class SaltwindScenePlayModeSupport
    {
        public const float SceneReadyTimeoutSeconds = 25f;
        public const int BossSummonMaxAttempts = 45;
        public const float BossSpawnWaitSeconds = 8f;

        public static IEnumerator LoadSaltwindRunUntilPlaying()
        {
            yield return SceneManager.LoadSceneAsync(V3SceneNames.SaltwindRun, LoadSceneMode.Single);
            var deadline = Time.realtimeSinceStartup + SceneReadyTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (RunManager.Instance != null &&
                    RunManager.Instance.State == RunState.Playing &&
                    StageBalanceProfileContext.IsActive)
                    yield break;
                yield return null;
            }

            Assert.Fail("Saltwind run scene did not reach Playing with an active stage profile.");
        }

        public static IEnumerator TrySummonBossWithRetries(POIManager poi, BossSummonAttemptResult result)
        {
            result.Summoned = false;
            if (poi == null)
            {
                Assert.Fail("POIManager missing.");
                yield break;
            }

            for (var attempt = 0; attempt < BossSummonMaxAttempts; attempt++)
            {
                if (poi.BossDefeated)
                {
                    result.Summoned = true;
                    yield break;
                }

                if (poi.TrySummonBoss() && poi.BossSummoned)
                {
                    result.Summoned = true;
                    yield break;
                }

                yield return null;
            }
        }

        public static bool TryFindActiveBossAgent(EnemyWorld world, out int agentIndex, out int unitId)
        {
            agentIndex = -1;
            unitId = -1;
            if (world?.Agents == null) return false;
            for (var i = 0; i < world.Agents.Length; i++)
            {
                var agent = world.Agents[i];
                if (!agent.active || !agent.isBoss) continue;
                agentIndex = i;
                unitId = agent.id;
                return true;
            }

            return false;
        }

        public static IEnumerator WaitForActiveBossAgent(EnemyWorld world, BossAgentWaitResult result)
        {
            result.AgentIndex = -1;
            var deadline = Time.realtimeSinceStartup + BossSpawnWaitSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (TryFindActiveBossAgent(world, out var index, out _))
                {
                    result.AgentIndex = index;
                    yield break;
                }

                yield return null;
            }
        }

        /// <summary>Uses <see cref="EnemyWorld.ApplyDamage"/> — same path as weapon hits.</summary>
        public static void ApplyLethalWeaponDamage(EnemyWorld world, int agentIndex, string weaponId = "deck_cannons")
        {
            var agent = world.Agents[agentIndex];
            var damage = Mathf.Max(agent.hp, agent.maxHp) + 500f;
            world.ApplyDamage(agentIndex, damage, false, weaponId: weaponId);
        }
    }
}
