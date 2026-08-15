namespace PirateGame.Enemies
{
    public enum EnemyBehaviorKind
    {
        Chaser,
        Charger,
        Ranged,
        AoePulse,
        LureSlow,
        Boss
    }

    public static class EnemyBehaviorRegistry
    {
        public static EnemyBehaviorKind Resolve(string modelName)
        {
            if (string.IsNullOrEmpty(modelName))
                return EnemyBehaviorKind.Chaser;

            var key = modelName.ToLowerInvariant();

            if (key.StartsWith("boss_"))
                return EnemyBehaviorKind.Boss;

            if (key.Contains("hammerdead") || key.Contains("crasher") || key.Contains("shark") || key.Contains("harpy") || key.Contains("hippogriff"))
                return EnemyBehaviorKind.Charger;

            if (key.Contains("wyvern") || key.Contains("deep_one") || key.Contains("megasquito") || key.Contains("spout") || key.Contains("mecha"))
                return EnemyBehaviorKind.Ranged;

            if (key.Contains("charybdis") || key.Contains("blowfish") || key.Contains("jellyfish") || key.Contains("foam") || key.Contains("mist"))
                return EnemyBehaviorKind.AoePulse;

            if (key.Contains("anglerfish") || key.Contains("kelpling") || key.Contains("spirit"))
                return EnemyBehaviorKind.LureSlow;

            return EnemyBehaviorKind.Chaser;
        }
    }
}
