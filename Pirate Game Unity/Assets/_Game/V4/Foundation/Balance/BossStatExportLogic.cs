using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.Simulation.Balance;

namespace RoguePirates.V4.Foundation.Balance
{
    /// <summary>Captures the Saltwind solo finale boss for reproducible profile content hashing.</summary>
    public static class BossStatExportLogic
    {
        public const string DefaultSaltwindBossId = "barnacle_king";

        public static StageBossStatSnapshot SnapshotSaltwindBoss(
            GameDatabase database = null,
            string bossId = DefaultSaltwindBossId)
        {
            database ??= GameDatabaseLoader.Load();
            var def = database != null ? database.GetEnemy(bossId) : null;
            if (def == null)
            {
                return new StageBossStatSnapshot { BossId = bossId };
            }

            return new StageBossStatSnapshot
            {
                BossId = def.id,
                BaseHull = def.baseHP,
                BaseDamage = def.baseDamage,
                MoveSpeed = def.moveSpeed,
                AttackCooldownSeconds = def.attackCooldown > 0f ? def.attackCooldown : 2.8f,
                PreferredRange = def.preferredRange > 0f ? def.preferredRange : 10f
            };
        }
    }
}
