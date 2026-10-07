using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.Data;
using UnityEngine;

namespace HighSeas.Combat
{
    /// <summary>Runtime registration for the 12-weapon playtest pool and baseline weapon tuning.</summary>
    public static class CombatWeaponCatalog
    {
        public static void ApplyToDatabase(GameDatabase database, CombatExperimentProfile profile)
        {
            if (database?.weapons == null) return;
            TuneBaselineWeapons(database);
            if (profile == null || profile.weaponSlotCap <= 2) return;

            var list = new List<WeaponDefinition>(database.weapons);
            AddIfMissing(list, CreateLongNine());
            AddIfMissing(list, CreateSwivelGuns());
            AddIfMissing(list, CreateHarpoonGun());
            AddIfMissing(list, CreateMortar());
            AddIfMissing(list, CreateTidalAura());
            database.weapons = list.ToArray();
            SyncExperimentalWeapon(database, "mortar", CreateMortar());
            SyncExperimentalWeapon(database, "swivel_guns", CreateSwivelGuns());
            SyncExperimentalWeapon(database, "tidal_aura", CreateTidalAura());
        }

        static void SyncExperimentalWeapon(GameDatabase database, string id, WeaponDefinition template)
        {
            if (database == null || template == null) return;
            var weapon = database.GetWeapon(id);
            if (weapon == null) return;
            weapon.displayName = template.displayName;
            weapon.pattern = template.pattern;
            weapon.targeting = template.targeting;
            weapon.baseDamage = template.baseDamage;
            weapon.baseCooldown = template.baseCooldown;
            weapon.range = template.range;
            weapon.targetArcDegrees = template.targetArcDegrees;
            weapon.procCoefficient = template.procCoefficient;
            weapon.fanSpreadDegrees = template.fanSpreadDegrees;
            weapon.size = template.size;
            weapon.baseQuantity = template.baseQuantity;
            weapon.preferElite = template.preferElite;
            weapon.preferHighestHull = template.preferHighestHull;
            weapon.tags = template.tags;
        }

        static void AddIfMissing(List<WeaponDefinition> list, WeaponDefinition weapon)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].id == weapon.id) return;
            }
            list.Add(weapon);
        }

        static void TuneBaselineWeapons(GameDatabase database)
        {
            SetWeapon(database, "deck_cannons", TargetingMode.ForwardArc, 120f, 22f, 1f, preferElite: false);
            SetWeapon(database, "broadside_battery", TargetingMode.PortStarboard, 50f, 14f, 0.7f);
            SetWeapon(database, "powder_kegs", TargetingMode.AftDeploy, 0f, 12f, 0.8f);
            SetWeapon(database, "flying_cutlasses", TargetingMode.AreaAroundShip, 360f, 6f, 0.35f);
            SetWeapon(database, "lightning_mast", TargetingMode.Nearest, 360f, 16f, 0.5f);
            SetWeapon(database, "flamethrower", TargetingMode.ForwardCone, 52f, 9.5f, 0.2f);
            SetWeapon(database, "burning_oil", TargetingMode.ForwardCone, 52f, 9.5f, 0.2f);
        }

        static void SetWeapon(
            GameDatabase database,
            string id,
            TargetingMode targeting,
            float arc,
            float range,
            float proc,
            bool preferElite = false,
            bool preferHighestHull = false)
        {
            var weapon = database.GetWeapon(id);
            if (weapon == null) return;
            weapon.targeting = targeting;
            weapon.targetArcDegrees = arc;
            weapon.range = range;
            weapon.procCoefficient = proc;
            weapon.preferElite = preferElite;
            weapon.preferHighestHull = preferHighestHull;
        }

        static WeaponDefinition CreateLongNine() => Build(
            "long_nine", "Long Nine", WeaponPattern.AutoProjectile, TargetingMode.ForwardArc,
            damage: 28f, cooldown: 2.4f, range: 24f, arc: 40f, proc: 1f,
            tags: Tags("Gunnery", "Hunter"), preferHighestHull: true);

        static WeaponDefinition CreateSwivelGuns() => Build(
            "swivel_guns", "Swivel Guns", WeaponPattern.AutoProjectile, TargetingMode.Nearest,
            damage: 3.5f, cooldown: 0.72f, range: 13f, arc: 360f, proc: 0.35f,
            tags: Tags("Gunnery"), quantity: 1);

        static WeaponDefinition CreateGrapeshot() => Build(
            "grapeshot", "Grapeshot", WeaponPattern.FanVolley, TargetingMode.ForwardCone,
            damage: 7f, cooldown: 1.1f, range: 9f, arc: 100f, proc: 0.7f,
            tags: Tags("Gunnery", "CloseRange"), fanSpread: 100f);

        static WeaponDefinition CreateHarpoonGun() => Build(
            "harpoon_gun", "Harpoon Gun", WeaponPattern.AutoProjectile, TargetingMode.TetherTarget,
            damage: 16f, cooldown: 1.8f, range: 16f, arc: 75f, proc: 0.8f,
            tags: Tags("Hunter", "Control"), preferHighestHull: true);

        static WeaponDefinition CreateMortar() => Build(
            "mortar", "Mortar", WeaponPattern.PredictedAoE, TargetingMode.OmniClusterGround,
            damage: 22f, cooldown: 2.6f, range: 18f, arc: 360f, proc: 0.8f,
            tags: Tags("Explosive"), size: 4.25f);

        /// <summary>Aqua-teal ring around the ship; ticks damage on everything inside. Radius grows every level.</summary>
        static WeaponDefinition CreateTidalAura() => Build(
            "tidal_aura", "Tidal Aura", WeaponPattern.AuraPulse, TargetingMode.AreaAroundShip,
            damage: 4f, cooldown: 0.5f, range: 0f, arc: 360f, proc: 0.3f,
            tags: Tags("Control"), size: 1f);

        static WeaponDefinition CreateBoardingParty() => Build(
            "boarding_party", "Boarding Party", WeaponPattern.SummonChase, TargetingMode.NearestElite,
            damage: 12f, cooldown: 3.5f, range: 16f, arc: 360f, proc: 0.6f,
            tags: Tags("Crew"), preferElite: true);

        static string[] Tags(params string[] tags) => tags;

        static WeaponDefinition Build(
            string id,
            string displayName,
            WeaponPattern pattern,
            TargetingMode targeting,
            float damage,
            float cooldown,
            float range,
            float arc,
            float proc,
            string[] tags,
            float fanSpread = 50f,
            float size = 1f,
            int quantity = 1,
            bool preferElite = false,
            bool preferHighestHull = false)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.id = id;
            weapon.displayName = displayName;
            weapon.pattern = pattern;
            weapon.targeting = targeting;
            weapon.tags = tags;
            weapon.baseDamage = damage;
            weapon.baseCooldown = cooldown;
            weapon.range = range;
            weapon.targetArcDegrees = arc;
            weapon.procCoefficient = proc;
            weapon.fanSpreadDegrees = fanSpread;
            weapon.size = size;
            weapon.baseQuantity = quantity;
            weapon.preferElite = preferElite;
            weapon.preferHighestHull = preferHighestHull;
            weapon.projectileColor = Color.Lerp(Color.white, Color.cyan, 0.35f);
            return weapon;
        }
    }
}
