using System.Collections.Generic;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Combat
{
    /// <summary>Registers specialist enemies (doc §8.4) into the runtime database when Director V2 is active.</summary>
    public static class CombatSpecialistEnemyCatalog
    {
        public static void ApplyToDatabase(GameDatabase database, CombatExperimentProfile profile)
        {
            if (database?.enemies == null || profile == null)
                return;

            var list = new List<EnemyDefinition>(database.enemies);
            RegisterAlias(list, "needle_sloop", "cutthroat_skiff", EnemyBehavior.FlankingChaser, 2.0f, 14f, 5.5f);
            RegisterAlias(list, "ram_raider", "rammer_raft", EnemyBehavior.TelegraphedCharge, 3.0f, 22f, 7f);
            RegisterAlias(list, "bomb_ketch", "bomb_boat", EnemyBehavior.MaintainRange, 5.0f, 28f, 3.2f);
            RegisterAlias(list, "net_cutter", "gunner_cog", EnemyBehavior.FormationRanged, 4.0f, 18f, 4.5f);
            RegisterAlias(list, "repair_tender", "boarding_barge", EnemyBehavior.MaintainRange, 6.0f, 24f, 2.8f);
            RegisterAlias(list, "powder_barge", "boarding_barge", EnemyBehavior.SuicideApproach, 4.0f, 40f, 2.2f);
            RegisterAlias(list, "shield_frigate", "gunner_cog", EnemyBehavior.TankChaser, 7.0f, 45f, 3f);
            RegisterAlias(list, "treasure_raider", "deckhand_dinghy", EnemyBehavior.FastSwarm, 4.0f, 16f, 6.5f);

            TuneBaselineRole(database, "manta_ray", EnemyBehavior.StrafeBurst, 2.3f);
            TuneBaselineRole(database, "reef_crab_miniboss", EnemyBehavior.TelegraphedCharge, 3f);

            database.enemies = list.ToArray();
        }

        static void RegisterAlias(
            List<EnemyDefinition> list,
            string newId,
            string templateId,
            EnemyBehavior behavior,
            float threatCost,
            float hp,
            float speed)
        {
            if (FindById(list, newId) != null) return;
            var template = FindById(list, templateId);
            if (template == null) return;

            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.id = newId;
            def.displayName = newId.Replace('_', ' ');
            def.category = template.category;
            def.behavior = behavior;
            def.baseHP = hp;
            def.baseDamage = template.baseDamage;
            def.moveSpeed = speed;
            def.radius = template.radius;
            def.threatCost = Mathf.RoundToInt(threatCost);
            def.xpValue = template.xpValue;
            def.goldValue = template.goldValue;
            def.preferredRange = template.preferredRange;
            def.attackCooldown = template.attackCooldown;
            def.placeholderColor = template.placeholderColor;
            def.tags = new[] { "specialist" };
            list.Add(def);
        }

        static void TuneBaselineRole(GameDatabase database, string id, EnemyBehavior behavior, float threatCost)
        {
            var def = database.GetEnemy(id);
            if (def == null) return;
            def.behavior = behavior;
            def.threatCost = Mathf.RoundToInt(threatCost);
        }

        static EnemyDefinition FindById(List<EnemyDefinition> list, string id)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].id == id) return list[i];
            }
            return null;
        }
    }
}
