using RoguePirates.V4.Foundation.Data;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RoguePirates.V4.Foundation.Core
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
            // M16: V3's built-in stub definitions (keyed by content id) are gone; the shipped assets are required.
            Debug.LogError("GameDatabase missing or empty (" + AssetPath + " / Resources/GameDatabase).");
            return null;
        }

        public static bool IsValid(GameDatabase db)
        {
            return db != null
                   && db.ships != null && db.ships.Length > 0 && db.ships[0] != null
                   && db.weapons != null && db.weapons.Length > 0 && db.weapons[0] != null
                   && db.enemies != null && db.enemies.Length > 0 && db.enemies[0] != null;
        }
    }
}
