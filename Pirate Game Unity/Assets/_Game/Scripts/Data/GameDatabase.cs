using UnityEngine;

namespace HighSeas.Data
{
    [CreateAssetMenu(fileName = "GameDatabase", menuName = "HighSeas/Game Database")]
    public sealed class GameDatabase : ScriptableObject
    {
        public ShipDefinition[] ships;
        public WeaponDefinition[] weapons;
        public LogbookDefinition[] logbooks;
        public RelicDefinition[] relics;
        public EnemyDefinition[] enemies;

        public WeaponDefinition GetWeapon(string id)
        {
            if (weapons == null) return null;
            for (var i = 0; i < weapons.Length; i++)
                if (weapons[i] != null && weapons[i].id == id) return weapons[i];
            return null;
        }

        public LogbookDefinition GetLogbook(string id)
        {
            if (logbooks == null) return null;
            for (var i = 0; i < logbooks.Length; i++)
                if (logbooks[i] != null && logbooks[i].id == id) return logbooks[i];
            return null;
        }

        public RelicDefinition GetRelic(string id)
        {
            if (relics == null) return null;
            for (var i = 0; i < relics.Length; i++)
                if (relics[i] != null && relics[i].id == id) return relics[i];
            return null;
        }

        public EnemyDefinition GetEnemy(string id)
        {
            if (enemies == null) return null;
            for (var i = 0; i < enemies.Length; i++)
                if (enemies[i] != null && enemies[i].id == id) return enemies[i];
            return null;
        }
    }
}
