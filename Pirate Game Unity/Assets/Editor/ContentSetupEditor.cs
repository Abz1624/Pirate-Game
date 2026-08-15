using System.Collections.Generic;
using System.IO;
using PirateGame.Combat;
using PirateGame.Enemies;
using PirateGame.Progression;
using UnityEditor;
using UnityEngine;

public static class ContentSetupEditor
{
    const string CardImageFolder = "Assets/Combat Card Images";
    const string EnemyGltfFolder = "Assets/3D Models/Enemies";
    const string ItemGltfFolder = "Assets/3D Models/Items";
    const string EnemyPrefabFolder = "Assets/Prefabs/Enemies";
    const string PickupPrefabFolder = "Assets/Prefabs/Items";
    const string DatabasePath = "Assets/Resources/UpgradeCardDatabase.asset";
    const string PickupCatalogPath = "Assets/Resources/PickupCatalog.asset";

    static readonly (string file, string name, UpgradeArcFamily family, UpgradeEffect effect, float magnitude)[] CardDefs =
    {
        ("Card_Art_Scatter_Shot.png", "Scatter Shot", UpgradeArcFamily.Broadside, UpgradeEffect.Pierce, 1f),
        ("Card_Art_Carronade.png", "Carronade", UpgradeArcFamily.Broadside, UpgradeEffect.DamagePercent, 0.2f),
        ("Card_Art_Double_Barrel.png", "Double Barrel", UpgradeArcFamily.Broadside, UpgradeEffect.CannonCount, 1f),
        ("Card_Art_Storm_of_Iron.png", "Storm of Iron", UpgradeArcFamily.Broadside, UpgradeEffect.FireRatePercent, 0.25f),
        ("Card_Art_Broadside.png", "Broadside", UpgradeArcFamily.Broadside, UpgradeEffect.DamagePercent, 0.15f),
        ("Card_Art_Long_9s.png", "Long Nines", UpgradeArcFamily.Broadside, UpgradeEffect.RangePercent, 0.2f),
        ("Card_Art_Bombardment.png", "Bombardment", UpgradeArcFamily.Deck, UpgradeEffect.FireRatePercent, 0.18f),
        ("Card_Art_Tail_Sweep.png", "Tail Sweep", UpgradeArcFamily.Deck, UpgradeEffect.DamagePercent, 0.12f),
        ("Card_Art_Reload.png", "Reload", UpgradeArcFamily.Passive, UpgradeEffect.FireRatePercent, 0.15f),
        ("Card_Art_Swift_Course.png", "Swift Course", UpgradeArcFamily.Passive, UpgradeEffect.TurnRatePercent, 0.15f),
        ("Card_Art_Fleetfoot.png", "Fleetfoot", UpgradeArcFamily.Passive, UpgradeEffect.MoveSpeedPercent, 0.12f),
        ("Card_Art_Fortitude.png", "Fortitude", UpgradeArcFamily.Passive, UpgradeEffect.MaxHealthPercent, 0.2f),
        ("Card_Art_Continuous_Repairs.png", "Continuous Repairs", UpgradeArcFamily.Passive, UpgradeEffect.HealthRegen, 2f),
        ("Card_Art_Iron_Fangs.png", "Iron Fangs", UpgradeArcFamily.Passive, UpgradeEffect.DamagePercent, 0.1f),
        ("Card_Art_Stack_The_Deck.png", "Stack the Deck", UpgradeArcFamily.Passive, UpgradeEffect.PickupRadiusPercent, 0.25f),
    };

    [MenuItem("Pirate Game/Content/Build All Solo Content")]
    static void BuildAll()
    {
        BuildUpgradeCardDatabase();
        BuildEnemyPrefabs();
        BuildPickupPrefabs();
        BuildPickupCatalog();
        WireEnemySpawner();
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Content", "Solo content built:\n• Upgrade cards\n• Enemy prefabs\n• Pickup prefabs\n• Spawner wired", "OK");
    }

    [MenuItem("Pirate Game/Content/Build Upgrade Card Database")]
    static void BuildUpgradeCardDatabase()
    {
        EnsureFolder("Assets/Resources");
        var db = AssetDatabase.LoadAssetAtPath<UpgradeCardDatabase>(DatabasePath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<UpgradeCardDatabase>();
            AssetDatabase.CreateAsset(db, DatabasePath);
        }

        db.cards = new List<UpgradeCardData>();
        foreach (var def in CardDefs)
        {
            var path = $"{CardImageFolder}/{def.file}";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                {
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null && importer.textureType != TextureImporterType.Sprite)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        importer.spriteImportMode = SpriteImportMode.Single;
                        importer.SaveAndReimport();
                        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    }
                }
            }

            db.cards.Add(new UpgradeCardData
            {
                id = Path.GetFileNameWithoutExtension(def.file),
                displayName = def.name,
                family = def.family,
                effect = def.effect,
                magnitude = def.magnitude,
                icon = sprite
            });
        }

        EditorUtility.SetDirty(db);
    }

    [MenuItem("Pirate Game/Content/Build Enemy Prefabs")]
    static void BuildEnemyPrefabs()
    {
        EnsureFolder(EnemyPrefabFolder);
        foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { EnemyGltfFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".gltf")) continue;
            CreateEnemyPrefab(path);
        }
    }

    [MenuItem("Pirate Game/Content/Build Pickup Prefabs")]
    static void BuildPickupPrefabs()
    {
        EnsureFolder(PickupPrefabFolder);
        foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { ItemGltfFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".gltf")) continue;
            CreatePickupPrefab(path);
        }
    }

    static void CreateEnemyPrefab(string gltfPath)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(gltfPath);
        if (model == null) return;

        var name = Path.GetFileNameWithoutExtension(gltfPath);
        var prefabPath = $"{EnemyPrefabFolder}/{name}.prefab";
        var scale = name.StartsWith("boss_") ? 0.02f : 0.015f;
        var health = name.StartsWith("boss_") ? 400f : 30f;
        var speed = name.StartsWith("boss_") ? 1.5f : 2f;

        var instance = Object.Instantiate(model);
        instance.name = name;
        instance.tag = "Enemy";
        instance.transform.localScale = Vector3.one * scale;

        if (instance.GetComponent<Health>() == null) instance.AddComponent<Health>();
        if (instance.GetComponent<EnemySetup>() == null) instance.AddComponent<EnemySetup>();
        if (instance.GetComponent<EnemyChase>() == null) instance.AddComponent<EnemyChase>();
        if (instance.GetComponent<EnemyCombatBehavior>() == null) instance.AddComponent<EnemyCombatBehavior>();
        if (instance.GetComponent<EnemyContactDamage>() == null) instance.AddComponent<EnemyContactDamage>();

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null)
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        else
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);

        Object.DestroyImmediate(instance);
    }

    static void CreatePickupPrefab(string gltfPath)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(gltfPath);
        if (model == null) return;

        var name = Path.GetFileNameWithoutExtension(gltfPath);
        var prefabPath = $"{PickupPrefabFolder}/{name}.prefab";

        var instance = Object.Instantiate(model);
        instance.name = name;
        instance.transform.localScale = Vector3.one * 0.1f;

        var col = instance.GetComponent<Collider>();
        if (col == null)
        {
            var sphere = instance.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 0.35f;
        }
        else col.isTrigger = true;

        if (instance.GetComponent<PickupItem>() == null)
            instance.AddComponent<PickupItem>();

        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        Object.DestroyImmediate(instance);
    }

    static void BuildPickupCatalog()
    {
        EnsureFolder("Assets/Resources");
        var catalog = AssetDatabase.LoadAssetAtPath<PickupCatalog>(PickupCatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<PickupCatalog>();
            AssetDatabase.CreateAsset(catalog, PickupCatalogPath);
        }

        var prefabs = new List<GameObject>();
        foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { PickupPrefabFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && prefab.GetComponent<PickupItem>() != null)
                prefabs.Add(prefab);
        }

        catalog.prefabs = prefabs.ToArray();
        EditorUtility.SetDirty(catalog);
    }

    static void WireEnemySpawner()
    {
        WireEnemySpawnerIfPresent();
    }

    public static void WireEnemySpawnerIfPresent()
    {
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        if (spawner == null) return;

        var so = new SerializedObject(spawner);
        var types = so.FindProperty("enemyTypes");
        var bosses = so.FindProperty("bossTypes");

        var mobPrefabs = new List<GameObject>();
        var bossPrefabs = new List<GameObject>();
        foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { EnemyPrefabFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            if (prefab.name.StartsWith("boss_"))
                bossPrefabs.Add(prefab);
            else
                mobPrefabs.Add(prefab);
        }

        types.arraySize = mobPrefabs.Count;
        for (var i = 0; i < mobPrefabs.Count; i++)
        {
            var row = types.GetArrayElementAtIndex(i);
            row.FindPropertyRelative("overrideDefinition").boolValue = true;
            row.FindPropertyRelative("prefab").objectReferenceValue = mobPrefabs[i];
            row.FindPropertyRelative("maxHealth").floatValue = 30f;
            row.FindPropertyRelative("moveSpeed").floatValue = 2f;
            row.FindPropertyRelative("spawnWeight").floatValue = 1f;
        }

        bosses.arraySize = bossPrefabs.Count;
        for (var i = 0; i < bossPrefabs.Count; i++)
        {
            var row = bosses.GetArrayElementAtIndex(i);
            row.FindPropertyRelative("overrideDefinition").boolValue = true;
            row.FindPropertyRelative("prefab").objectReferenceValue = bossPrefabs[i];
            row.FindPropertyRelative("maxHealth").floatValue = 400f;
            row.FindPropertyRelative("moveSpeed").floatValue = 1.5f;
            row.FindPropertyRelative("spawnWeight").floatValue = 1f;
        }

        var pickups = so.FindProperty("pickupPrefabs");
        var pickupPrefabs = new List<GameObject>();
        foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { PickupPrefabFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && prefab.GetComponent<PickupItem>() != null)
                pickupPrefabs.Add(prefab);
        }

        pickups.arraySize = pickupPrefabs.Count;
        for (var i = 0; i < pickupPrefabs.Count; i++)
            pickups.GetArrayElementAtIndex(i).objectReferenceValue = pickupPrefabs[i];

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(spawner);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
