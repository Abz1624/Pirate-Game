using System;
using UnityEngine;

namespace HighSeas.Core
{
    [Serializable]
    public struct StatModifier
    {
        public StatId stat;
        public float additive;
        public float multiplicative;

        public StatModifier(StatId stat, float additive = 0f, float multiplicative = 1f)
        {
            this.stat = stat;
            this.additive = additive;
            this.multiplicative = multiplicative;
        }
    }

    public static class GameConstants
    {
        public const float DefaultMoveSpeed = 6.2f;
        public const float DefaultHull = 100f;
        public const float DefaultPickupRadius = 2.25f;
        public const float ContactIFrame = 0.20f;
        public const float EnemyContactCooldown = 0.8f;
        public const float StageDurationSeconds = 600f;
        public const float StageBaseThreat = 2.6f;
        public const int DefaultWeaponSlots = 2;
        public const int DefaultLogbookSlots = 2;
        public const int MaxWeaponLevel = 12;
        public const int MaxLogbookLevel = 10;
        public const float CameraPitch = 52f;
        public const float CameraFov = 55f;
        public const float SmoothDampTime = 0.10f;
        public const float VisualYawLerp = 12f;
        public const float MapSizeMeters = 220f;
        public const int GridCells = 14;
        public const float CellSizeMeters = 16f;
        public const float SpawnAnnulusMin = 13f;
        public const float SpawnAnnulusMax = 22f;
        public const int SaveVersion = 1;

        public static readonly float[] RarityMultipliers = { 1f, 1.25f, 1.55f, 1.90f, 2.50f };
        public static readonly float[] RarityBaseWeights = { 70f, 22.5f, 6f, 1.35f, 0.15f };
    }

    public static class GameMath
    {
        public static float XPToNext(int level) =>
            Mathf.Round(12f + 5.5f * level + 1.35f * Mathf.Pow(level, 1.32f));

        public static float ChestCost(int paidChestIndex) =>
            Mathf.Round(25f + 34f * (paidChestIndex - 1) + 4.5f * Mathf.Pow(paidChestIndex - 1, 2));

        public static float ComputeThreatPerSecond(float elapsedSeconds, int tier, int stageIndex, int bountyLevel, bool overtime)
        {
            var minute = elapsedSeconds / 60f;
            var timeCurve = 1f + 0.11f * minute + 0.022f * minute * minute;
            var tierMult = tier switch { 1 => 1f, 2 => 1.35f, _ => 1.8f };
            var stageMult = stageIndex switch { 0 => 1f, 1 => 1.22f, _ => 1.48f };
            var bountyMult = 1f + 0.06f * bountyLevel;
            var overtimeMult = 1f;
            if (overtime)
            {
                var overtimeMinutes = (elapsedSeconds - GameConstants.StageDurationSeconds) / 60f;
                overtimeMult = 1.5f * Mathf.Pow(1.2f, overtimeMinutes);
            }
            else if (elapsedSeconds > GameConstants.StageDurationSeconds)
            {
                var overtimeMinutes = (elapsedSeconds - GameConstants.StageDurationSeconds) / 60f;
                overtimeMult = 1.5f * Mathf.Pow(1.2f, overtimeMinutes);
            }

            return GameConstants.StageBaseThreat * timeCurve * tierMult * stageMult * bountyMult * overtimeMult;
        }

        public static float ComputeDamage(
            float baseDamage,
            float weaponLevelDamage,
            float globalDamageBonus,
            float weaponDamageBonus,
            bool isCrit,
            float critDamage,
            bool isElite,
            float eliteDamageMult,
            float statusMult,
            float targetArmor)
        {
            var weaponRoll = baseDamage * weaponLevelDamage * (1f + globalDamageBonus) * weaponDamageBonus;
            if (isCrit) weaponRoll *= critDamage;
            if (isElite) weaponRoll *= eliteDamageMult;
            var raw = weaponRoll * statusMult;
            var armorMult = 100f / (100f + Mathf.Max(0f, targetArmor));
            return Mathf.Max(1f, raw * armorMult);
        }

        public static float EvadeChance(float evasion) =>
            Mathf.Min(0.75f, evasion / (evasion + 100f));

        public static float EnemyHP(float baseHp, float minute, int tier, int stageIndex)
        {
            var tierMult = tier switch { 1 => 1f, 2 => 1.45f, _ => 2.05f };
            var stageMult = stageIndex switch { 0 => 1f, 1 => 1.22f, _ => 1.5f };
            return baseHp * (1f + 0.09f * minute + 0.018f * minute * minute) * tierMult * stageMult;
        }

        public static float EnemyDamage(float baseDamage, float minute, int tier, int stageIndex)
        {
            var tierMult = tier switch { 1 => 1f, 2 => 1.25f, _ => 1.6f };
            var stageMult = stageIndex switch { 0 => 1f, 1 => 1.12f, _ => 1.28f };
            return baseDamage * (1f + 0.055f * minute) * tierMult * stageMult;
        }
    }
}
