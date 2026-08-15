using PirateGame.Combat;
using PirateGame.Progression;
using PirateGame.Ship;
using UnityEditor;
using UnityEngine;
using TopDownCameraFollow = PirateGame.Camera.TopDownCameraFollow;

public static class PlayerShipBuilder
{
    public const string DefaultShipGltfPath = "Assets/3D Models/Ships/item_4x8_pirategalleonred.gltf";
    public const string PlayerShipPrefabPath = "Assets/Prefabs/Solo/PlayerShip.prefab";
    public const string CannonBallPrefabPath = "Assets/Prefabs/CannonBall.prefab";
    public const float DefaultShipScale = 0.05f;

    public static GameObject EnsurePlayerShipInScene(Vector3 position)
    {
        var existing = GameObject.FindGameObjectWithTag("Player");
        if (existing != null)
        {
            ConfigurePlayerShip(existing);
            existing.transform.position = position;
            return existing;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerShipPrefabPath);
        GameObject ship;
        if (prefab != null)
        {
            ship = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            ship.transform.position = position;
        }
        else
        {
            ship = CreateShipFromGltf(position);
            SavePlayerPrefab(ship);
        }

        ConfigurePlayerShip(ship);
        return ship;
    }

    public static GameObject CreateShipFromGltf(Vector3 position)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultShipGltfPath);
        if (model == null)
        {
            Debug.LogError($"Missing ship model at {DefaultShipGltfPath}");
            return null;
        }

        var ship = Object.Instantiate(model);
        ship.name = "PlayerShip";
        ship.tag = "Player";
        ship.transform.position = position;
        ship.transform.localScale = Vector3.one * DefaultShipScale;
        return ship;
    }

    public static void SavePlayerPrefab(GameObject shipInstance)
    {
        EnsureFolder("Assets/Prefabs/Solo");
        ConfigurePlayerShip(shipInstance);
        PrefabUtility.SaveAsPrefabAsset(shipInstance, PlayerShipPrefabPath);
    }

    public static void ConfigurePlayerShip(GameObject shipRoot)
    {
        if (shipRoot == null) return;

        Undo.RegisterFullObjectHierarchyUndo(shipRoot, "Configure Player Ship");
        shipRoot.tag = "Player";

        var body = shipRoot.GetComponent<Rigidbody>();
        if (body == null)
            body = Undo.AddComponent<Rigidbody>(shipRoot);

        body.useGravity = false;
        body.isKinematic = false;
        body.constraints = RigidbodyConstraints.FreezePositionY |
                           RigidbodyConstraints.FreezeRotationX |
                           RigidbodyConstraints.FreezeRotationZ;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var hullCollider = shipRoot.GetComponent<CapsuleCollider>();
        if (hullCollider == null)
            hullCollider = Undo.AddComponent<CapsuleCollider>(shipRoot);

        hullCollider.center = Vector3.zero;
        hullCollider.radius = 3.5f;
        hullCollider.height = 14f;
        hullCollider.direction = 2;

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

        EnsureMount(mountsParent, "Cannon_Port_Fore", new Vector3(-6f, 0f, 5f), Quaternion.Euler(0f, -90f, 0f));
        EnsureMount(mountsParent, "Cannon_Starboard_Fore", new Vector3(6f, 0f, 5f), Quaternion.Euler(0f, 90f, 0f));
        EnsureMount(mountsParent, "Cannon_Port_Mid", new Vector3(-6f, 0f, 0f), Quaternion.Euler(0f, -90f, 0f));
        EnsureMount(mountsParent, "Cannon_Starboard_Mid", new Vector3(6f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
        EnsureMount(mountsParent, "Cannon_Port_Aft", new Vector3(-6f, 0f, -5f), Quaternion.Euler(0f, -90f, 0f));
        EnsureMount(mountsParent, "Cannon_Starboard_Aft", new Vector3(6f, 0f, -5f), Quaternion.Euler(0f, 90f, 0f));
        EnsureMount(mountsParent, "Cannon_Port_Rear", new Vector3(-6f, 0f, -9f), Quaternion.Euler(0f, -90f, 0f));
        EnsureMount(mountsParent, "Cannon_Starboard_Rear", new Vector3(6f, 0f, -9f), Quaternion.Euler(0f, 90f, 0f));

        var mountNames = new[]
        {
            "Cannon_Port_Fore", "Cannon_Starboard_Fore",
            "Cannon_Port_Mid", "Cannon_Starboard_Mid",
            "Cannon_Port_Aft", "Cannon_Starboard_Aft",
            "Cannon_Port_Rear", "Cannon_Starboard_Rear"
        };
        var mounts = new Transform[mountNames.Length];
        for (var i = 0; i < mountNames.Length; i++)
            mounts[i] = mountsParent.Find(mountNames[i]);

        var projectilePrefab = AssetDatabase.LoadAssetAtPath<CannonProjectile>(CannonBallPrefabPath);
        var serialized = new SerializedObject(autoFire);
        serialized.FindProperty("cannonMounts").arraySize = mounts.Length;
        for (var i = 0; i < mounts.Length; i++)
            serialized.FindProperty("cannonMounts").GetArrayElementAtIndex(i).objectReferenceValue = mounts[i];

        serialized.FindProperty("activeCannonCount").intValue = 2;
        serialized.FindProperty("projectilePrefab").objectReferenceValue = projectilePrefab;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        if (shipRoot.GetComponent<Health>() == null)
        {
            var hp = Undo.AddComponent<Health>(shipRoot);
            hp.Initialize(100f);
        }

        if (shipRoot.GetComponent<PlayerRunProgress>() == null)
            Undo.AddComponent<PlayerRunProgress>(shipRoot);

        if (shipRoot.GetComponent<PickupMagnet>() == null)
            Undo.AddComponent<PickupMagnet>(shipRoot);
    }

    public static void SetupCamera(Transform ship)
    {
        var cam = UnityEngine.Camera.main;
        if (cam == null) return;

        var follow = cam.GetComponent<TopDownCameraFollow>();
        if (follow == null)
            follow = Undo.AddComponent<TopDownCameraFollow>(cam.gameObject);

        follow.target = ship;
        follow.offset = new Vector3(0f, 18f, 12f);
        follow.followShipRotation = false;
        follow.snapOnStart = true;
        follow.rotationMode = TopDownCameraFollow.RotationMode.LookAtTarget;
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
