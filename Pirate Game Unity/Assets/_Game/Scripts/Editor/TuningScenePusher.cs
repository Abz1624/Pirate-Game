#if UNITY_EDITOR
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.Pickups;
using HighSeas.POI;
using HighSeas.UI;
using HighSeas.VFX;
using HighSeas.Weapons;
using UnityEditor;
using UnityEngine;

namespace HighSeas.Editor
{
    static class TuningScenePusher
    {
        public static void PushIfPlaying()
        {
            if (!EditorApplication.isPlaying) return;

            var juice = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
            var pacing = Resources.Load<DemoPacingSettings>("DemoPacingSettings");
            var levelUp = Resources.Load<LevelUpSettings>("LevelUpSettings");
            var spawn = Resources.Load<SpawnDirectorSettings>("SpawnDirectorSettings");

            PushSettings(juice);
            PushPacing(pacing);
            PushLevelUp(levelUp);
            PushSpawn(spawn);
        }

        static void PushSettings(DemoJuiceSettings juice)
        {
            if (juice == null) return;

            foreach (var broker in FindAll<JuiceVfxBroker>())
                SetReference(broker, "settings", juice);

            foreach (var combat in FindAll<CombatVfxController>())
                SetReference(combat, "settings", juice);

            foreach (var weaponJuice in FindAll<WeaponJuiceController>())
                SetReference(weaponJuice, "settings", juice);

            foreach (var playerJuice in FindAll<PlayerJuiceController>())
                SetReference(playerJuice, "settings", juice);

            foreach (var pickups in FindAll<PickupWorld>())
                SetReference(pickups, "juiceSettings", juice);

            foreach (var projectiles in FindAll<ProjectileWorld>())
                SetReference(projectiles, "juiceSettings", juice);

            foreach (var enemies in FindAll<EnemyWorld>())
                SetReference(enemies, "juiceSettings", juice);

            foreach (var poi in FindAll<POIManager>())
                SetReference(poi, "juiceSettings", juice);

            foreach (var flash in FindAll<PlayerDamageFlash>())
                flash.Configure(juice);
        }

        static void PushPacing(DemoPacingSettings pacing)
        {
            if (pacing == null) return;

            foreach (var levels in FindAll<LevelSystem>())
                SetReference(levels, "demoPacing", pacing);

            foreach (var weapons in FindAll<WeaponController>())
                SetReference(weapons, "demoPacing", pacing);

            foreach (var pickups in FindAll<PickupWorld>())
                SetReference(pickups, "pacingSettings", pacing);

            foreach (var poi in FindAll<POIManager>())
                SetReference(poi, "pacingSettings", pacing);

            foreach (var director in FindAll<SpawnDirector>())
                SetPrivateField(director, "_pacingSettings", pacing);
        }

        static void PushLevelUp(LevelUpSettings levelUp)
        {
            if (levelUp == null) return;

            foreach (var levels in FindAll<LevelSystem>())
                SetReference(levels, "levelUpSettings", levelUp);
        }

        static void PushSpawn(SpawnDirectorSettings spawn)
        {
            if (spawn == null) return;

            foreach (var director in FindAll<SpawnDirector>())
                SetReference(director, "settings", spawn);
        }

        static T[] FindAll<T>() where T : Object
        {
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        static void SetReference(Object target, string propertyName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(propertyName);
            if (prop == null) return;
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetPrivateField(Object target, string fieldName, Object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(target, value);
        }
    }
}
#endif
