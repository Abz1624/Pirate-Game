using System;

namespace RoguePirates.Simulation
{
    public static class SimMath
    {
        public static float XPToNext(int level)
        {
            return (float)Math.Round(12d + 5.5d * level + 1.35d * Math.Pow(level, 1.32d), MidpointRounding.AwayFromZero);
        }

        public static float ChestCost(int paidChestIndex) =>
            (float)Math.Round(25d + 34d * (paidChestIndex - 1) + 4.5d * Math.Pow(paidChestIndex - 1, 2), MidpointRounding.AwayFromZero);

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
                var overtimeMinutes = (elapsedSeconds - SimConstants.StageDurationSeconds) / 60f;
                overtimeMult = 1.5f * MathF.Pow(1.2f, overtimeMinutes);
            }

            return SimConstants.StageBaseThreat * timeCurve * tierMult * stageMult * bountyMult * overtimeMult;
        }

        public static SimFixed ComputeDamage(
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
            var armorMult = 100f / (100f + MathF.Max(0f, targetArmor));
            var final = MathF.Max(1f, raw * armorMult);
            return SimFixed.FromFloat(final);
        }

        public static SimFixed EnemyHP(float baseHp, float minute, int tier, int stageIndex)
        {
            var tierMult = tier switch { 1 => 1f, 2 => 1.45f, _ => 2.05f };
            var stageMult = stageIndex switch { 0 => 1f, 1 => 1.22f, _ => 1.5f };
            var hp = baseHp * (1f + 0.09f * minute + 0.018f * minute * minute) * tierMult * stageMult;
            return SimFixed.FromFloat(hp);
        }

        /// <summary>Doc §7.3 Director V2 spawn rate (enemies/sec), capped at 16.</summary>
        public static float ComputeV3SpawnRate(float minute, float densityMultiplier, bool directorV2Enabled)
        {
            var x = MathF.Min(minute, 10f);
            var rate = (2.5f + 0.55f * x + 0.025f * x * x) * MathF.Max(0.1f, densityMultiplier);
            rate = MathF.Min(16f, rate);
            if (!directorV2Enabled)
                rate *= 0.42f;
            return rate;
        }
    }
}
