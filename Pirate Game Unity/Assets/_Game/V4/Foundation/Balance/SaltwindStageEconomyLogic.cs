using System.Collections.Generic;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.UI;
using RoguePirates.Simulation.Balance;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Balance
{
    public enum SaltwindKillRewardClass
    {
        Ordinary,
        Specialist,
        ScheduledElite,
        Boss
    }

    /// <summary>Run gold, chest purchase, and kill rewards for solo Saltwind (Step 08 / C08).</summary>
    public static class SaltwindStageEconomyLogic
    {
        public const int ChestPriceFixtureCount = 5;

        public static SaltwindKillRewardClass ClassifyKillReward(EnemyDefinition def, bool isElite, bool isBoss)
        {
            if (def == null) return SaltwindKillRewardClass.Ordinary;
            if (isBoss || def.isBoss) return SaltwindKillRewardClass.Boss;
            if (def.isMiniboss || (isElite && def.threatCost >= 3))
                return SaltwindKillRewardClass.ScheduledElite;
            if (IsSpecialist(def)) return SaltwindKillRewardClass.Specialist;
            return SaltwindKillRewardClass.Ordinary;
        }

        public static int ResolveKillGoldDrop(
            StageBalanceProfileExport export,
            EnemyDefinition def,
            RunRng rng,
            bool richAffix,
            bool isElite,
            bool isBoss)
        {
            if (export?.Rewards == null || def == null || rng == null) return 0;

            var cls = ClassifyKillReward(def, isElite, isBoss);
            var amount = cls switch
            {
                SaltwindKillRewardClass.Ordinary => rng.DirectorFloat() < export.Rewards.OrdinaryEnemyGoldChance
                    ? export.Rewards.OrdinaryEnemyGoldAmount
                    : 0,
                SaltwindKillRewardClass.Specialist => export.Rewards.SpecialistEnemyGoldAmount,
                SaltwindKillRewardClass.ScheduledElite => export.Rewards.ScheduledEliteGoldAmount,
                SaltwindKillRewardClass.Boss => def.goldValue,
                _ => 0
            };

            if (richAffix && amount > 0)
                amount *= 2;
            return amount;
        }

        public static bool ShouldApplyGoldGainStat(StageBalanceProfileExport export) => export == null;

        public static int ApplyGoldGrant(int rawValue, StageBalanceProfileExport export, float goldGainStat) =>
            ShouldApplyGoldGainStat(export)
                ? Mathf.Max(0, Mathf.RoundToInt(rawValue * goldGainStat))
                : rawValue;

        public static int MergePickupValues(int existing, int incoming) => existing + incoming;

        public static int SumPickupValues(int[] values)
        {
            var sum = 0;
            if (values == null) return 0;
            for (var i = 0; i < values.Length; i++)
                sum += values[i];
            return sum;
        }

        static bool IsSpecialist(EnemyDefinition def)
        {
            if (def.tags != null)
            {
                for (var i = 0; i < def.tags.Length; i++)
                {
                    if (def.tags[i] == "specialist") return true;
                }
            }

            return def.threatCost >= 3 && !def.isBoss && !def.isMiniboss;
        }
    }
}
