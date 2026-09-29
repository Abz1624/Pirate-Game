using System;
using System.Collections.Generic;
using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.Simulation.Balance;

namespace RoguePirates.V4.Foundation.Balance
{
    public readonly struct SaltwindXpAdvanceResult
    {
        public int Level { get; }
        public float CurrentXp { get; }
        public int PendingLevelUps { get; }

        public SaltwindXpAdvanceResult(int level, float currentXp, int pendingLevelUps)
        {
            Level = level;
            CurrentXp = currentXp;
            PendingLevelUps = pendingLevelUps;
        }
    }

    /// <summary>Solo Saltwind progression rules (Step 07 / C07).</summary>
    public static class SaltwindStageProgressionLogic
    {
        public const int DefaultProtectionLevelsCount = 4;
        static readonly int[] DefaultProtectionLevels = { 4, 8, 12, 16 };

        public static float XpToNext(StageBalanceProfileExport export, int currentLevel) =>
            StageBalanceProfileExportCodec.XpToNextLevel(export, currentLevel);

        public static SaltwindXpAdvanceResult ApplyXpGrant(
            StageBalanceProfileExport export,
            int level,
            float currentXp,
            int grant)
        {
            if (export == null) throw new ArgumentNullException(nameof(export));
            if (grant <= 0) return new SaltwindXpAdvanceResult(level, currentXp, 0);

            var pending = 0;
            var remaining = (float)grant;
            while (remaining > 0.001f)
            {
                var need = XpToNext(export, level);
                var room = need - currentXp;
                if (remaining < room)
                {
                    currentXp += remaining;
                    remaining = 0f;
                    break;
                }

                remaining -= room;
                currentXp = 0f;
                level++;
                pending++;
            }

            return new SaltwindXpAdvanceResult(level, currentXp, pending);
        }

        public static int GetNewWeaponPityTargetCount(int playerLevel, int[] protectionLevels)
        {
            protectionLevels ??= DefaultProtectionLevels;
            var target = 0;
            for (var i = 0; i < protectionLevels.Length; i++)
            {
                if (playerLevel >= protectionLevels[i])
                    target = i + 3;
            }

            return target;
        }

        public static bool IsWeaponInOfferPool(StageBalanceProfileExport export, string weaponId)
        {
            if (export == null || string.IsNullOrEmpty(weaponId)) return false;
            if (!WeaponFeatureFlags.IsWeaponEnabled(weaponId)) return false;

            if (export.StarterWeaponIds != null)
            {
                for (var i = 0; i < export.StarterWeaponIds.Count; i++)
                {
                    if (export.StarterWeaponIds[i] == weaponId)
                        return true;
                }
            }

            return false;
        }

        public static int SimulateBoundedProcCascade(int branchFactor, int maxEvents)
        {
            if (branchFactor <= 0 || maxEvents <= 0) return 0;
            var processed = 0;
            var pending = 1;
            while (pending > 0 && processed < maxEvents)
            {
                var take = Math.Min(pending, maxEvents - processed);
                processed += take;
                if (processed >= maxEvents) break;
                pending = take * branchFactor;
            }

            return processed;
        }
    }
}
