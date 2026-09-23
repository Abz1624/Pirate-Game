using System;

namespace RoguePirates.Simulation.Balance
{
    /// <summary>Maps export boss snapshot + deadline onto Balance Lab scenarios.</summary>
    public static class SaltwindLabBossParityLogic
    {
        public static void ConfigureScenario(
            BalanceLabScenario scenario,
            StageBalanceProfileExport export,
            BalanceSkillPreset skill)
        {
            if (scenario == null || export == null) return;

            var boss = export.BossStatSnapshot;
            if (boss != null && !string.IsNullOrEmpty(boss.BossId))
            {
                scenario.BossId = boss.BossId;
                var bossHull = Math.Max(1f, boss.BaseHull);
                bossHull *= SaltwindLabMatrixCalibration.BossHullMultiplierForSkill(
                    skill,
                    scenario.SaltwindUnlockPool,
                    scenario.SaltwindBuildPolicy);
                scenario.BossBaseHull = bossHull;
                scenario.BossBaseDamage = Math.Max(0.1f, boss.BaseDamage);
                scenario.BossBaseMoveSpeed = Math.Max(0.1f, boss.MoveSpeed);
                scenario.BossAttackCooldown = Math.Max(0.1f, boss.AttackCooldownSeconds);
                scenario.BossPreferredRange = Math.Max(0f, boss.PreferredRange);
            }

            scenario.BossSpawnSeconds = SaltwindLabRuntimeParityLogic.QuoteStageDeadline(export);
            scenario.BossBehavior = BalanceEnemyBehavior.Tank;
            scenario.DurationSeconds = scenario.BossSpawnSeconds + 240f;
            scenario.PlayerHullMultiplier = SaltwindLabMatrixCalibration.PlayerHullMultiplierForSkill(
                skill,
                scenario.SaltwindUnlockPool,
                scenario.SaltwindBuildPolicy);
            scenario.InputPolicy = SaltwindLabMatrixCalibration.InputPolicyForSkill(skill);
        }
    }
}
