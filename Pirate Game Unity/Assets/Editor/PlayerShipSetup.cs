using PirateGame.Combat;
using PirateGame.Enemies;
using PirateGame.Ship;
using UnityEditor;
using UnityEngine;
using TopDownCameraFollow = PirateGame.Camera.TopDownCameraFollow;

public static class PlayerShipSetup
{
    const string CannonBallPrefabPath = "Assets/Prefabs/CannonBall.prefab";
    const string EnemyDefinitionsFolder = "Assets/ScriptableObjects/Enemies";

    [MenuItem("Pirate Game/Combat/Setup Selected As Enemy")]
    static void SetupSelectedEnemy()
    {
        var target = Selection.activeGameObject;
        if (target == null)
        {
            EditorUtility.DisplayDialog("Setup Enemy", "Select an enemy object in the Hierarchy first.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(target, "Setup Enemy");
        target.tag = "Enemy";

        if (target.GetComponent<Health>() == null)
            Undo.AddComponent<Health>(target);

        if (target.GetComponent<EnemySetup>() == null)
            Undo.AddComponent<EnemySetup>(target);

        if (target.GetComponent<EnemyChase>() == null)
            Undo.AddComponent<EnemyChase>(target);

        EditorUtility.DisplayDialog(
            "Setup Enemy",
            "Enemy configured.\n\n" +
            "- Tagged Enemy (cannon targeting)\n" +
            "- Health + EnemySetup + EnemyChase\n" +
            "- Save as a Prefab, then create an Enemy Definition to set model + HP for the spawner",
            "OK");
    }

    [MenuItem("Pirate Game/Enemies/Create Enemy Spawner In Scene")]
    static void CreateEnemySpawner()
    {
        var existing = Object.FindFirstObjectByType<EnemySpawner>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorUtility.DisplayDialog(
                "Enemy Spawner",
                "An Enemy Spawner already exists in the scene. Select it to configure enemy models and health.",
                "OK");
            return;
        }

        var go = new GameObject("EnemySpawner");
        Undo.RegisterCreatedObjectUndo(go, "Create Enemy Spawner");
        Undo.AddComponent<EnemySpawner>(go);
        Selection.activeGameObject = go;

        EditorUtility.DisplayDialog(
            "Enemy Spawner",
            "Enemy Spawner created.\n\n" +
            "1. Create enemy prefabs (Setup Selected As Enemy)\n" +
            "2. Create definitions: Pirate Game → Enemies → Create Enemy Definition\n" +
            "3. Add those definitions to the Enemy Types list on this spawner\n" +
            "   (or enable Override Definition and set Prefab + Max Health inline)",
            "OK");
    }

    [MenuItem("Pirate Game/Enemies/Create Enemy Definition")]
    static void CreateEnemyDefinition()
    {
        EnsureFolder("Assets/ScriptableObjects");
        EnsureFolder(EnemyDefinitionsFolder);

        var asset = ScriptableObject.CreateInstance<EnemyDefinition>();
        var path = AssetDatabase.GenerateUniqueAssetPath($"{EnemyDefinitionsFolder}/EnemyDefinition.asset");
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(asset);
        Selection.activeObject = asset;

        EditorUtility.DisplayDialog(
            "Enemy Definition",
            "Created Enemy Definition.\n\n" +
            "Assign Prefab (enemy model) and Max Health, then add it to an Enemy Spawner.",
            "OK");
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

    [MenuItem("Pirate Game/Combat/Setup Selected Ship As Player")]
    static void SetupSelectedShip()
    {
        var shipRoot = Selection.activeGameObject;
        if (shipRoot == null)
        {
            EditorUtility.DisplayDialog("Setup Player Ship", "Select the ship root in the Hierarchy first.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(shipRoot, "Setup Player Ship");

        shipRoot.tag = "Player";

        var body = shipRoot.GetComponent<Rigidbody>();
        if (body == null)
            body = Undo.AddComponent<Rigidbody>(shipRoot);

        body.useGravity = false;
        body.isKinematic = false;
        body.constraints = RigidbodyConstraints.FreezePositionY |
                           RigidbodyConstraints.FreezeRotationX |
                           RigidbodyConstraints.FreezeRotationZ;

        if (shipRoot.GetComponent<ShipMovement>() == null)
            Undo.AddComponent<ShipMovement>(shipRoot);

        var autoFire = shipRoot.GetComponent<CannonAutoFire>();
        if (autoFire == null)
            autoFire = Undo.AddComponent<CannonAutoFire>(shipRoot);

        var mountsParent = shipRoot.transform.Find("CannonMounts");
        if (mountsParent == null)
        {
            var mountsGo = new GameObject("CannonMounts");
            Undo.RegisterCreatedObjectUndo(mountsGo, "Create Cannon Mounts");
            mountsGo.transform.SetParent(shipRoot.transform, false);
            mountsParent = mountsGo.transform;
        }

        EnsureMount(mountsParent, "Cannon_Port", new Vector3(-6f, 2f, 0f), Quaternion.Euler(0f, -90f, 0f));
        EnsureMount(mountsParent, "Cannon_Starboard", new Vector3(6f, 2f, 0f), Quaternion.Euler(0f, 90f, 0f));

        var mounts = new[]
        {
            mountsParent.Find("Cannon_Port"),
            mountsParent.Find("Cannon_Starboard")
        };

        var projectilePrefab = AssetDatabase.LoadAssetAtPath<CannonProjectile>(CannonBallPrefabPath);
        var serialized = new SerializedObject(autoFire);
        serialized.FindProperty("cannonMounts").arraySize = mounts.Length;
        for (var i = 0; i < mounts.Length; i++)
            serialized.FindProperty("cannonMounts").GetArrayElementAtIndex(i).objectReferenceValue = mounts[i];

        serialized.FindProperty("activeCannonCount").intValue = mounts.Length;
        serialized.FindProperty("projectilePrefab").objectReferenceValue = projectilePrefab;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        SetupCamera(shipRoot.transform);

        EditorUtility.DisplayDialog(
            "Setup Player Ship",
            "Player ship configured.\n\n" +
            "- W/S or stick Y to throttle forward and reverse\n" +
            "- A/D or stick X to steer (turn without strafing)\n" +
            "- Cannons auto-fire at nearest Enemy-tagged target\n" +
            "- Drag Cannon_Port / Cannon_Starboard empties to reposition mounts",
            "OK");
    }

    static void EnsureMount(Transform parent, string name, Vector3 localPosition, Quaternion localRotation)
    {
        var existing = parent.Find(name);
        if (existing != null)
        {
            if (existing.GetComponent<CannonMount>() == null)
                Undo.AddComponent<CannonMount>(existing.gameObject);
            return;
        }

        var mount = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(mount, "Create Cannon Mount");
        mount.transform.SetParent(parent, false);
        mount.transform.localPosition = localPosition;
        mount.transform.localRotation = localRotation;
        Undo.AddComponent<CannonMount>(mount);
    }

    static void SetupCamera(Transform ship)
    {
        var cam = UnityEngine.Camera.main;
        if (cam == null) return;

        var follow = cam.GetComponent<TopDownCameraFollow>();
        if (follow == null)
            follow = Undo.AddComponent<TopDownCameraFollow>(cam.gameObject);

        follow.target = ship;
        follow.followShipRotation = true;
        follow.snapOnStart = true;
    }
}
