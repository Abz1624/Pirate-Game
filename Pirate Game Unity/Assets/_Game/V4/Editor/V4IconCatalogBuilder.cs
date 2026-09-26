using System.Collections.Generic;
using System.Linq;
using HighSeas.V3;
using RoguePirates.V4.Config;
using RoguePirates.V4.Presentation.Hud;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// Builds the HUD icon catalog (M6) from the files V3RunHudIconResolver looks up in the Editor (weapons by display
    /// name under Assets/UI/Weapons, items by file name under Assets/UI/Item Icons), so the V4 loadout HUD shows the same
    /// icons in player builds too. Idempotent; rerun after adding icons or content.
    /// Fix (R-90): V3 mapped only two apostrophe names to their files ("Gunner_s Gloves", "Sea Witch_s Eye"), so
    /// Admiral's Ledger, Captain's Bell and Dragon's Breath Nozzle showed no icon although their files exist; the
    /// builder falls back to the same apostrophe-to-underscore file name for every item.
    /// </summary>
    public static class V4IconCatalogBuilder
    {
        public const string CatalogPath = "Assets/_Game/V4/Resources/V4IconCatalog.asset";
        const string WeaponConfigFolder = "Assets/_Game/V4/Data/Weapons";
        const string ItemFolder = "Assets/UI/Item Icons/"; // V3RunHudIconResolver.ItemFolder

        [MenuItem("RoguePirates/V4/Build Icon Catalog")]
        static void BuildFromMenu() => Debug.Log(Build());

        public static string Build()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<IconCatalog>(CatalogPath);
            if (catalog == null)
            {
                var folder = System.IO.Path.GetDirectoryName(CatalogPath)?.Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_Game/V4", "Resources");
                catalog = ScriptableObject.CreateInstance<IconCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            // The run database as V3's HUD saw it: the asset plus the runtime weapon catalog (its display names pick the files).
            var source = V3ConfigMigrator.LoadCleanDatabase();
            var database = Object.Instantiate(source);
            database.weapons = source.weapons.Select(w => w != null ? Object.Instantiate(w) : null).ToArray();
            HighSeas.Combat.CombatWeaponCatalog.ApplyToDatabase(database, HighSeas.Combat.CombatExperimentContext.Active);
            catalog.weapons.Clear();
            var missing = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:WeaponConfig", new[] { WeaponConfigFolder }))
            {
                var config = AssetDatabase.LoadAssetAtPath<WeaponConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (config == null || string.IsNullOrEmpty(config.weaponId)) continue;
                var def = database != null ? database.GetWeapon(config.weaponId) : null;
                var sprite = V3RunHudIconResolver.GetWeaponIcon(config.weaponId, def);
                if (sprite == null) missing.Add(config.weaponId);
                catalog.weapons.Add(new IconCatalog.Entry { id = config.weaponId, sprite = sprite });
            }
            foreach (var w in database.weapons) if (w != null) Object.DestroyImmediate(w);
            Object.DestroyImmediate(database);

            catalog.items.Clear();
            var itemIds = V3ItemCatalog.ChestPool.Select(e => e.id)
                .Concat(V3ItemCatalog.InteractionItemIds)
                .Concat(V3ItemCatalog.PlaytestItemOrder)
                .Distinct()
                .OrderBy(id => id, System.StringComparer.Ordinal);
            foreach (var id in itemIds)
            {
                var sprite = V3RunHudIconResolver.GetItemIcon(id);
                if (sprite == null)
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ItemFolder + V3ItemCatalog.GetDisplayName(id).Replace('\'', '_') + ".png");
                if (sprite == null) missing.Add("item:" + id);
                catalog.items.Add(new IconCatalog.Entry { id = id, sprite = sprite });
            }

            catalog.weapons.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return $"Icon catalog: {catalog.weapons.Count} weapons, {catalog.items.Count} items" +
                   (missing.Count > 0 ? "; no icon for " + string.Join(", ", missing) : "");
        }
    }
}
