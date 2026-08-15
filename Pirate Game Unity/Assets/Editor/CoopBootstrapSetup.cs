using PirateGame.Net;
using PirateGame.Ship;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click local ER co-op wiring for Main / SampleScene.
/// Menu: Pirate Game → Co-op → Setup Local ER Bootstrap
/// </summary>
public static class CoopBootstrapSetup
{
    const string ConfigPath = "Assets/Resources/BlockchainConfig.asset";
    const string WslKeypairPath = @"\\wsl$\Ubuntu\home\shara\.config\solana\id.json";

    [MenuItem("Pirate Game/Co-op/Setup Local ER Bootstrap")]
    static void Setup()
    {
        var config = EnsureConfig();
        var root = EnsureRoot(config);
        WirePlayerShip(root.GetComponent<ErClient>());
        WireRemoteShip(root.GetComponent<ErClient>(), root.GetComponent<LobbyManager>());
        var remote = Object.FindFirstObjectByType<RemoteShipView>();
        if (remote != null)
            WireDualShipComponents(remote.gameObject);
        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);

        EditorUtility.DisplayDialog(
            "Local ER Bootstrap",
            "Created / updated CoopBootstrap in the open scene.\n\n" +
            "Components: ErClient, LobbyManager, CoopMatchBootstrap, CoopLocalHotkeys\n" +
            $"Payer keypair: {config.localPayerKeypairPath}\n" +
            "Mode: Local (8899 / 7799)\n\n" +
            "1. Save the scene (Ctrl+S)\n" +
            "2. Enter Play Mode\n" +
            "3. Press K for local 2-player (WASD + arrow keys), I for solo, or ParrelSync co-op\n" +
            "4. Steer with WASD — Console should show [ER] submit_input",
            "OK");
    }

    [MenuItem("Pirate Game/Co-op/Point BlockchainConfig At WSL Keypair")]
    static void PointAtWslKeypair()
    {
        var config = EnsureConfig();
        Undo.RecordObject(config, "Set WSL payer keypair");
        config.mode = BlockchainConfig.NetworkMode.Local;
        config.localPayerKeypairPath = WslKeypairPath;
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Selection.activeObject = config;
        EditorUtility.DisplayDialog(
            "BlockchainConfig",
            $"Mode = Local\nPayer = {WslKeypairPath}\n\n" +
            "Address should match: solana address (2vN2Czct…)",
            "OK");
    }

    static BlockchainConfig EnsureConfig()
    {
        var config = AssetDatabase.LoadAssetAtPath<BlockchainConfig>(ConfigPath);
        if (config == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            config = ScriptableObject.CreateInstance<BlockchainConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }

        Undo.RecordObject(config, "Configure BlockchainConfig for local ER");
        config.mode = BlockchainConfig.NetworkMode.Local;
        if (string.IsNullOrWhiteSpace(config.localPayerKeypairPath) ||
            config.localPayerKeypairPath.Contains("wsl$"))
            config.localPayerKeypairPath = WslKeypairPath;
        if (string.IsNullOrWhiteSpace(config.programId))
            config.programId = "EeWGiecEDWgBB3eUxgQfTLjQJcZnLRYsXvbCcYfxP6pC";
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        return config;
    }

    static GameObject EnsureRoot(BlockchainConfig config)
    {
        var existing = Object.FindFirstObjectByType<CoopMatchBootstrap>();
        GameObject root;
        if (existing != null)
        {
            root = existing.gameObject;
            Undo.RegisterFullObjectHierarchyUndo(root, "Update CoopBootstrap");
        }
        else
        {
            root = new GameObject("CoopBootstrap");
            Undo.RegisterCreatedObjectUndo(root, "Create CoopBootstrap");
        }

        var er = root.GetComponent<ErClient>() ?? Undo.AddComponent<ErClient>(root);
        var lobby = root.GetComponent<LobbyManager>() ?? Undo.AddComponent<LobbyManager>(root);
        var boot = root.GetComponent<CoopMatchBootstrap>() ?? Undo.AddComponent<CoopMatchBootstrap>(root);
        var hotkeys = root.GetComponent<CoopLocalHotkeys>() ?? Undo.AddComponent<CoopLocalHotkeys>(root);

        AssignSerialized(er, "config", config);
        AssignSerialized(lobby, "config", config);
        AssignSerialized(lobby, "erClient", er);
        AssignSerialized(boot, "config", config);
        AssignSerialized(boot, "erClient", er);
        AssignSerialized(boot, "lobbyManager", lobby);
        AssignSerialized(hotkeys, "lobbyManager", lobby);

        EditorUtility.SetDirty(root);
        return root;
    }

    static void WirePlayerShip(ErClient er)
    {
        var movement = Object.FindFirstObjectByType<ShipMovement>();
        if (movement == null)
        {
            Debug.LogWarning("[Coop] No ShipMovement in scene — add NetworkedShipController to the player ship manually.");
            return;
        }

        var ship = movement.gameObject;
        Undo.RegisterFullObjectHierarchyUndo(ship, "Wire networked ship");

        if (ship.GetComponent<ShipPredictionReconciler>() == null)
            Undo.AddComponent<ShipPredictionReconciler>(ship);

        var net = ship.GetComponent<NetworkedShipController>();
        if (net == null)
            net = Undo.AddComponent<NetworkedShipController>(ship);

        AssignSerialized(net, "erClient", er);

        // Avoid double-driving the rigidbody while ER inputs are live.
        movement.enabled = false;
        EditorUtility.SetDirty(ship);
        Debug.Log($"[Coop] Networked ship wired on {ship.name}; ShipMovement disabled.");
    }

    static void WireRemoteShip(ErClient er, LobbyManager lobby)
    {
        var existing = Object.FindFirstObjectByType<RemoteShipView>();
        if (existing != null)
        {
            AssignSerialized(existing, "erClient", er);
            AssignSerialized(existing, "lobbyManager", lobby);
            WireDualShipComponents(existing.gameObject);
            Debug.Log($"[Coop] Remote ship already present on {existing.name}");
            return;
        }

        var playerShip = GameObject.FindGameObjectWithTag("Player");
        if (playerShip == null)
        {
            var movement = Object.FindFirstObjectByType<ShipMovement>();
            playerShip = movement != null ? movement.gameObject : null;
        }
        if (playerShip == null)
        {
            Debug.LogWarning("[Coop] No player ship found — remote ship not created.");
            return;
        }

        var offset = playerShip.transform.right * 12f;
        var remote = Object.Instantiate(playerShip, playerShip.transform.position + offset, Quaternion.identity);
        remote.name = "RemoteShip";
        Undo.RegisterCreatedObjectUndo(remote, "Create remote ship");

        remote.tag = "Untagged";
        // NetworkedShipController requires ShipPredictionReconciler — remove in dependency order.
        foreach (var net in remote.GetComponentsInChildren<NetworkedShipController>(true))
            Undo.DestroyObjectImmediate(net);
        foreach (var reconciler in remote.GetComponentsInChildren<ShipPredictionReconciler>(true))
            Undo.DestroyObjectImmediate(reconciler);
        foreach (var movement in remote.GetComponentsInChildren<ShipMovement>(true))
            Undo.DestroyObjectImmediate(movement);

        var rb = remote.GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = true;

        var view = Undo.AddComponent<RemoteShipView>(remote);
        WireDualShipComponents(remote);
        AssignSerialized(view, "erClient", er);
        AssignSerialized(view, "lobbyManager", lobby);
        EditorUtility.SetDirty(remote);
        Debug.Log($"[Coop] Remote ship created at {remote.name} (follows other player's snapshot).");
    }

    [MenuItem("Pirate Game/Co-op/Add Remote Ship To Scene")]
    static void AddRemoteShipMenu()
    {
        var er = Object.FindFirstObjectByType<ErClient>();
        var lobby = Object.FindFirstObjectByType<LobbyManager>();
        if (er == null || lobby == null)
        {
            EditorUtility.DisplayDialog("Remote ship", "Run Setup Local ER Bootstrap first.", "OK");
            return;
        }
        WireRemoteShip(er, lobby);
    }

    static void WireDualShipComponents(GameObject remote)
    {
        foreach (var net in remote.GetComponentsInChildren<NetworkedShipController>(true))
            Undo.DestroyObjectImmediate(net);

        if (remote.GetComponent<LocalDualShipController>() == null)
            Undo.AddComponent<LocalDualShipController>(remote);
    }

    static void AssignSerialized(Object target, string fieldName, Object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogWarning($"[Coop] Missing field {target.GetType().Name}.{fieldName}");
            return;
        }
        prop.objectReferenceValue = value;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }
}
