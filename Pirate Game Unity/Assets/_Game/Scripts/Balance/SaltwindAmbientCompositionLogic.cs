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
        // Cannonboat caps apply to ordinary-pool picks (Codex F3).

        static readonly float[] OrdinaryShareByBand = { 1f, 0.8f, 0.7f, 0.65f, 0.6f };

        static readonly string[][] OrdinaryFamilies =
        {
            new[] { "swordfish", "deckhand_dinghy" },
            new[] { "swordfish", "deckhand_dinghy", "cutthroat_skiff", "hammerhead" },
            new[] { "hammerhead", "cutthroat_skiff", "shark", "cannonboat" },
            new[] { "manta_ray", "cannonboat", "hammerhead" },
            new[] { "manta_ray", "cannonboat", "hammerhead" }
        };

        static readonly string[][] SpecialistFamilies =
        {
            Array.Empty<string>(),
            new[] { "needle_sloop", "ram_raider" },
            new[] { "bomb_boat", "rammer_raft" },
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
            out string enemyId,
            float elapsedSeconds = 0f,
            int playerLevel = 0)
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
                if (TryPickFromPool(
                        SpecialistFamilies[bandIndex], rng, countActiveByEnemyId, export, true,
                        elapsedSeconds, playerLevel, out enemyId))
                    return true;
            }

            if (TryPickFromPool(
                    OrdinaryFamilies[bandIndex], rng, countActiveByEnemyId, export, false,
                    elapsedSeconds, playerLevel, out enemyId))
                return true;

            if (OrdinaryFamilies[bandIndex].Length > 0)
            {
                var fallback = OrdinaryFamilies[bandIndex][0];
                if (IsWithinCap(fallback, countActiveByEnemyId(fallback), export, false) &&
                    EnemyTierLogic.CanSpawnEnemyId(fallback, elapsedSeconds, playerLevel))
                {
                    enemyId = fallback;
                    return true;
                }
            }

            return false;
        }

        static bool TryPickFromPool(
            string[] pool,
            RunRng rng,
            Func<string, int> countActiveByEnemyId,
            StageBalanceProfileExport export,
            bool specialistPool,
            float elapsedSeconds,
            int playerLevel,
            out string enemyId)
        {
            enemyId = null;
            if (pool == null || pool.Length == 0)
                return false;

            var start = rng.DirectorInt(0, pool.Length);
            for (var offset = 0; offset < pool.Length; offset++)
            {
                var candidate = pool[(start + offset) % pool.Length];
                if (!EnemyTierLogic.CanSpawnEnemyId(candidate, elapsedSeconds, playerLevel))
                    continue;
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
            if (export?.Spawn == null)
                return true;

            if (enemyId == "cannonboat")
                return activeCount < export.Spawn.MaxSimultaneousCannonboats;
            if (!specialistPool)
                return true;

            if (enemyId == "kraken_spawn")
                return activeCount < export.Spawn.MaxSimultaneousAreaDenial;
            if (enemyId == "ram_raider")
                return activeCount < export.Spawn.MaxSimultaneousChargeWindups;
            return true;
        }
    }
}
