using HighSeas.Data;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HighSeas.Core
{
    public static class GameDatabaseLoader
    {
        public const string AssetPath = "Assets/_Game/Data/GameDatabase.asset";

        public static GameDatabase Load()
        {
#if UNITY_EDITOR
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(AssetPath);
#else
            var db = Resources.Load<GameDatabase>("GameDatabase");
#endif
            if (IsValid(db)) return db;
            Debug.LogWarning("GameDatabase missing or empty; using runtime fallback definitions.");
            return CreateRuntimeFallback();
        }

        public static bool IsValid(GameDatabase db)
        {
            return db != null
                   && db.ships != null && db.ships.Length > 0 && db.ships[0] != null
                   && db.weapons != null && db.weapons.Length > 0 && db.weapons[0] != null
                   && db.enemies != null && db.enemies.Length > 0 && db.enemies[0] != null;
        }

        static GameDatabase CreateRuntimeFallback()
        {
            var db = ScriptableObject.CreateInstance<GameDatabase>();
            db.ships = new[] { CreateShip() };
            db.weapons = CreateWeapons();
            db.logbooks = CreateLogbooks();
            db.relics = CreateRelics();
            db.enemies = CreateEnemies();
            return db;
        }

        static ShipDefinition CreateShip()
        {
            var ship = ScriptableObject.CreateInstance<ShipDefinition>();
            ship.id = "cutlass_sloop";
            ship.displayName = "Cutlass Sloop";
            ship.baseHull = 100f;
            ship.baseMoveSpeed = 6.2f;
            ship.startingWeaponId = "broadside_battery";
            return ship;
        }

        static WeaponDefinition[] CreateWeapons()
        {
            return new[]
            {
                MakeWeapon("deck_cannons", WeaponPattern.AutoProjectile, TargetingMode.Nearest),
                MakeWeapon("broadside_battery", WeaponPattern.FanVolley, TargetingMode.DensestCluster),
                MakeWeapon("powder_kegs", WeaponPattern.MineDrop, TargetingMode.PlayerTrail),
                MakeWeapon("lightning_mast", WeaponPattern.ChainLightning, TargetingMode.Nearest),
                MakeWeapon("flying_cutlasses", WeaponPattern.Orbit, TargetingMode.AreaAroundShip),
                MakeWeapon("flamethrower", WeaponPattern.TrailZone, TargetingMode.PlayerTrail)
            };
        }

        static WeaponDefinition MakeWeapon(string id, WeaponPattern pattern, TargetingMode targeting)
        {
            var w = ScriptableObject.CreateInstance<WeaponDefinition>();
            w.id = id;
            w.displayName = id.Replace('_', ' ');
            w.pattern = pattern;
            w.targeting = targeting;
            w.baseDamage = 10f;
            w.baseCooldown = 1.2f;
            w.validUpgradeAxes = new[] { UpgradeAxis.Damage, UpgradeAxis.Cooldown, UpgradeAxis.Quantity };
            return w;
        }

        static LogbookDefinition[] CreateLogbooks()
        {
            return new[]
            {
                MakeLogbook("gunnery_manual", StatId.Damage, 0.08f),
                MakeLogbook("powder_drill", StatId.AttackSpeed, 0.075f),
                MakeLogbook("oversized_shot", StatId.Size, 0.10f),
                MakeLogbook("extra_hands", StatId.Quantity, 1f),
                MakeLogbook("reinforced_hull", StatId.Hull, 20f),
                MakeLogbook("carpenters_notes", StatId.HullRegen, 0.35f),
                MakeLogbook("infamy_charter", StatId.XPGain, 0.08f),
                MakeLogbook("treasure_map", StatId.Luck, 0.07f)
            };
        }

        static LogbookDefinition MakeLogbook(string id, StatId stat, float perLevel)
        {
            var l = ScriptableObject.CreateInstance<LogbookDefinition>();
            l.id = id;
            l.displayName = id.Replace('_', ' ');
            l.primaryStat = stat;
            l.perLevelAdditive = perLevel;
            return l;
        }

        static RelicDefinition[] CreateRelics()
        {
            var r = ScriptableObject.CreateInstance<RelicDefinition>();
            r.id = "lucky_doubloon";
            r.displayName = "Lucky Doubloon";
            r.rarity = Rarity.Common;
            r.passiveModifiers = new[] { new StatModifier(StatId.Luck, 0.07f) };
            return new[] { r };
        }

        static EnemyDefinition[] CreateEnemies()
        {
            return new[]
            {
                MakeEnemy("cannonboat", EnemyCategory.Ship, 18f, 8f, 3.8f, 3, 1.2f, EnemyBehavior.DirectChaser),
                MakeEnemy("shark", EnemyCategory.Creature, 16f, 9f, 5f, 4, 1.1f, EnemyBehavior.DirectChaser),
                MakeEnemy("swordfish", EnemyCategory.Creature, 10f, 6f, 5.85f, 2, 0.9f, EnemyBehavior.FastSwarm),
                MakeEnemy("hammerhead", EnemyCategory.Creature, 14f, 7f, 5f, 3, 1.1f, EnemyBehavior.FlankingChaser),
                MakeEnemy("kraken_spawn", EnemyCategory.Creature, 28f, 11f, 2.5f, 6, 1.6f, EnemyBehavior.TankChaser),
                MakeEnemy("manta_ray", EnemyCategory.Creature, 22f, 8f, 4.5f, 4, 1.4f, EnemyBehavior.FlankingChaser),
                MakeEnemy("reef_crab_miniboss", EnemyCategory.Creature, 120f, 14f, 2f, 8, 2.2f, EnemyBehavior.MaintainRange, isMiniboss: true),
                MakeEnemy("barnacle_king", EnemyCategory.Creature, 800f, 18f, 2.5f, 9, 2.8f, EnemyBehavior.TankChaser, isBoss: true)
            };
        }

        static EnemyDefinition MakeEnemy(
            string id,
            EnemyCategory category,
            float hp,
            float dmg,
            float speed,
            int threat,
            float radius,
            EnemyBehavior behavior,
            bool isBoss = false,
            bool isMiniboss = false)
        {
            var e = ScriptableObject.CreateInstance<EnemyDefinition>();
            e.id = id;
            e.displayName = id.Replace('_', ' ');
            e.category = category;
            e.baseHP = hp;
            e.baseDamage = dmg;
            e.moveSpeed = speed;
            e.threatCost = threat;
            e.radius = radius;
            e.behavior = behavior;
            e.isBoss = isBoss;
            e.isMiniboss = isMiniboss;
            e.xpValue = isBoss ? 100 : isMiniboss ? 35 : threat + 1;
            e.goldValue = isBoss ? 150 : isMiniboss ? 50 : 1;
            e.placeholderColor = category == EnemyCategory.Ship
                ? new Color(0.75f, 0.45f, 0.2f)
                : new Color(0.2f, 0.65f, 0.85f);
            return e;
        }
    }
}
