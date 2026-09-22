using System.Collections.Generic;
using HighSeas.Data;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HighSeas.V3
{
    /// <summary>Resolves weapon/item sprites from Assets/UI (editor play) with optional Resources fallback.</summary>
    public static class V3RunHudIconResolver
    {
        const string WeaponFolder = "Assets/UI/Weapons/";
        const string ItemFolder = "Assets/UI/Item Icons/";

        static readonly Dictionary<string, Sprite> Cache = new();

        public static Sprite GetWeaponIcon(string weaponId, WeaponDefinition def = null)
        {
            if (string.IsNullOrEmpty(weaponId)) return null;
            if (Cache.TryGetValue(weaponId, out var cached)) return cached;

            var fileName = def != null && !string.IsNullOrEmpty(def.displayName)
                ? def.displayName
                : V3WeaponDisplayNames.GetWeaponName(weaponId);

            var sprite = LoadSprite(WeaponFolder + fileName + ".png")
                         ?? Resources.Load<Sprite>($"V3RunHudIcons/Weapons/{weaponId}");
            Cache[weaponId] = sprite;
            return sprite;
        }

        public static Sprite GetItemIcon(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            var key = "item:" + itemId;
            if (Cache.TryGetValue(key, out var cached)) return cached;

            var sprite = LoadSprite(ItemFolder + ItemIdToFileName(itemId) + ".png")
                         ?? LoadSprite(ItemFolder + V3ItemCatalog.GetDisplayName(itemId) + ".png")
                         ?? Resources.Load<Sprite>($"V3RunHudIcons/Items/{itemId}");
            Cache[key] = sprite;
            return sprite;
        }

        static string WeaponIdToDisplayName(string weaponId) => V3WeaponDisplayNames.GetWeaponName(weaponId);

        static string ItemIdToFileName(string itemId) => itemId switch
        {
            "gunners_gloves" => "Gunner_s Gloves",
            "sea_witchs_eye" => "Sea Witch_s Eye",
            _ => V3ItemCatalog.GetDisplayName(itemId)
        };

        static Sprite LoadSprite(string assetPath)
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
#else
            return null;
#endif
        }
    }
}
