namespace HighSeas.Combat
{
    /// <summary>Runtime proc-chain guard used by weapon triggered damage (Codex R10).</summary>
    public static class CombatProcRuntime
    {
        public static void BeginWeaponDamageResolution() => DamageProcPolicy.BeginDamageResolution();

        public static bool TryApplyTriggeredEnemyDamage(
            Enemies.EnemyWorld world,
            int enemyIndex,
            float damage,
            string weaponId,
            bool isCrit = false)
        {
            if (world == null || enemyIndex < 0) return false;
            if (!DamageProcPolicy.TryRegisterProcEvent()) return false;
            world.ApplyDamage(enemyIndex, damage, isCrit, weaponId: weaponId);
            return true;
        }
    }
}
