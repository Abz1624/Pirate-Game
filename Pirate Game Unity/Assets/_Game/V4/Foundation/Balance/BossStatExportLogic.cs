using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.Simulation.Balance;

namespace RoguePirates.V4.Foundation.Balance
{
    /// <summary>Captures the Saltwind solo finale boss for reproducible profile content hashing.</summary>
    public static class BossStatExportLogic
    {

        /// <summary>The stage boss's stats (M16: its id comes from the stage config, was V3's constant).</summary>
        public static StageBossStatSnapshot SnapshotSaltwindBoss(string bossId, GameDatabase database = null)
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
