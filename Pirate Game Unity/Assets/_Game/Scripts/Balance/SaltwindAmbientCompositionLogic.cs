using System;
using HighSeas.Core;
using RoguePirates.Simulation.Balance;

namespace HighSeas.Balance
{
    /// <summary>
    /// Five-band ambient roster + ordinary/specialist mix for Saltwind solo (Step 04).
    /// Schedule entries keep one anchor enemy; runtime picks from this table when the profile is active.
    /// </summary>
    public static class SaltwindAmbientCompositionLogic
    {
        public const int BandCount = 5;

        static readonly float[] OrdinaryShareByBand = { 1f, 0.8f, 0.7f, 0.65f, 0.6f };

        static readonly string[][] OrdinaryFamilies =
        {
            new[] { "shark", "swordfish" },
            new[] { "cannonboat", "hammerhead" },
            new[] { "manta_ray", "hammerhead", "cannonboat" },
            new[] { "manta_ray", "cannonboat", "hammerhead" },
            new[] { "manta_ray", "cannonboat", "hammerhead" }
        };

        static readonly string[][] SpecialistFamilies =
        {
            Array.Empty<string>(),
            new[] { "needle_sloop", "ram_raider" },
            new[] { "bomb_boat", "boarding_barge" },
            new[] { "bomb_boat", "boarding_barge", "kraken_spawn" },
            new[] { "kraken_spawn", "bomb_boat", "boarding_barge" }
        };

        public static bool TryGetAmbientBandIndex(string profileId, out int bandIndex)
        {
            bandIndex = -1;
            if (string.IsNullOrEmpty(profileId) || !profileId.StartsWith("saltwind_ambient_band_", StringComparison.Ordinal))
                return false;
            var suffix = profileId.Substring("saltwind_ambient_band_".Length);
            if (!int.TryParse(suffix, out bandIndex))
                return false;
            return bandIndex >= 0 && bandIndex < BandCount;
        }

        public static float GetOrdinaryShare(int bandIndex)
        {
            if (bandIndex < 0 || bandIndex >= BandCount)
                return 1f;
            return OrdinaryShareByBand[bandIndex];
        }

        public static bool TryPickEnemyId(
            int bandIndex,
            RunRng rng,
            Func<string, int> countActiveByEnemyId,
            StageBalanceProfileExport export,
            out string enemyId)
        {
            enemyId = null;
            if (bandIndex < 0 || bandIndex >= BandCount || rng == null)
                return false;

            countActiveByEnemyId ??= _ => 0;
            var ordinaryShare = GetOrdinaryShare(bandIndex);
            var rollSpecialist = SpecialistFamilies[bandIndex].Length > 0 &&
                                 rng.DirectorFloat() > ordinaryShare;

            if (rollSpecialist)
            {
                if (TryPickFromPool(SpecialistFamilies[bandIndex], rng, countActiveByEnemyId, export, true, out enemyId))
                    return true;
            }

            if (TryPickFromPool(OrdinaryFamilies[bandIndex], rng, countActiveByEnemyId, export, false, out enemyId))
                return true;

            if (OrdinaryFamilies[bandIndex].Length > 0)
            {
                enemyId = OrdinaryFamilies[bandIndex][0];
                return true;
            }

            return false;
        }

        static bool TryPickFromPool(
            string[] pool,
            RunRng rng,
            Func<string, int> countActiveByEnemyId,
            StageBalanceProfileExport export,
            bool specialistPool,
            out string enemyId)
        {
            enemyId = null;
            if (pool == null || pool.Length == 0)
                return false;

            var start = rng.DirectorInt(0, pool.Length);
            for (var offset = 0; offset < pool.Length; offset++)
            {
                var candidate = pool[(start + offset) % pool.Length];
                if (!IsWithinCap(candidate, countActiveByEnemyId(candidate), export, specialistPool))
                    continue;
                enemyId = candidate;
                return true;
            }

            return false;
        }

        static bool IsWithinCap(
            string enemyId,
            int activeCount,
            StageBalanceProfileExport export,
            bool specialistPool)
        {
            if (export?.Spawn == null || !specialistPool)
                return true;

            if (enemyId == "cannonboat")
                return activeCount < export.Spawn.MaxSimultaneousCannonboats;
            if (enemyId == "kraken_spawn")
                return activeCount < export.Spawn.MaxSimultaneousAreaDenial;
            if (enemyId == "ram_raider")
                return activeCount < export.Spawn.MaxSimultaneousChargeWindups;
            return true;
        }
    }
}
