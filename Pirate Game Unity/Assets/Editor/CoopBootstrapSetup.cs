using PirateGame.Net;
using PirateGame.Ship;
using PirateGame.UI;
using PirateGame.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One-click local ER co-op wiring for Main / SampleScene.
/// Menu: Pirate Game → Co-op → Setup Local ER Bootstrap
/// </summary>
public static class CoopBootstrapSetup
{
    const string ConfigPath = "Assets/Resources/BlockchainConfig.asset";
    const string SolanaTestScenePath = "Assets/Scenes/Solana Test Scene.unity";
    const string WslKeypairPath = @"\\wsl$\Ubuntu\home\shara\.config\solana\id.json";

    [MenuItem("Pirate Game/Co-op/Setup Solana Test Scene")]
    static void SetupSolanaTestScene()
    {
        if (!System.IO.File.Exists(SolanaTestScenePath))
        {
            EditorUtility.DisplayDialog("Solana Test", $"Scene not found: {SolanaTestScenePath}", "OK");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != SolanaTestScenePath)
            EditorSceneManager.OpenScene(SolanaTestScenePath);

        EnsureSceneInBuildSettings(SolanaTestScenePath);
        SetupCore();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        EditorUtility.DisplayDialog(
            "Solana Test Scene",
            "Solana Test Scene is wired for on-chain MagicBlock ER playtest.\n\n" +
            "Prerequisites (WSL):\n" +
            "  cd onchain && ./scripts/start-local-stack.sh\n" +
            "  (validator :8899, ER :7799 — both should be running)\n\n" +
            "In Unity:\n" +
            "  1. Save scene (Ctrl+S)\n" +
            "  2. Press Play\n" +
            "  3. Connect wallet → Host Room or Join Room\n" +
            "  4. Dev shortcuts still work: K/I/U/O/P when lobby UI is hidden\n\n" +
            "Solo.unity is offline-only (no Solana).",
            "OK");
    }

    [MenuItem("Pirate Game/Co-op/Setup Local ER Bootstrap")]
    static void Setup()
    {
        var config = SetupCore();
        EditorUtility.DisplayDialog(
            "Local ER Bootstrap",
            "Created / updated CoopBootstrap in the open scene.\n\n" +
            "Components: ErClient, LobbyManager, CoopMatchBootstrap, CoopLocalHotkeys, WalletBootstrap, CoopLobbyUI, PvPMatchHud, PvPDraftUI\n" +
            $"Payer keypair: {config.localPayerKeypairPath}\n" +
            "Mode: Local (8899 / 7799)\n\n" +
            "1. Save the scene (Ctrl+S)\n" +
            "2. Enter Play Mode\n" +
            "3. Press K for local 2-player (WASD + arrow keys), I for solo, or ParrelSync co-op\n" +
            "4. Steer with WASD — Console should show [ER] submit_input",
            "OK");
    }

    static BlockchainConfig SetupCore()
    {
        var config = EnsureConfig();
        var root = EnsureRoot(config);
        WirePlayerShip(root.GetComponent<ErClient>());
        WireRemoteShip(root.GetComponent<ErClient>(), root.GetComponent<LobbyManager>());
        EnsurePvPViews(root);
        var remote = Object.FindFirstObjectByType<RemoteShipView>();
        if (remote != null)
            WireDualShipComponents(remote.gameObject);
        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        return config;
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
        var wallet = root.GetComponent<WalletBootstrap>() ?? Undo.AddComponent<WalletBootstrap>(root);
        var lobbyUi = root.GetComponent<CoopLobbyUI>() ?? Undo.AddComponent<CoopLobbyUI>(root);

        AssignSerialized(er, "config", config);
        AssignSerialized(lobby, "config", config);
        AssignSerialized(lobby, "erClient", er);
        AssignSerialized(boot, "config", config);
        AssignSerialized(boot, "erClient", er);
        AssignSerialized(boot, "lobbyManager", lobby);
        AssignSerialized(hotkeys, "lobbyManager", lobby);
        AssignSerialized(wallet, "config", config);
        AssignSerialized(lobbyUi, "lobbyManager", lobby);
        AssignSerialized(lobbyUi, "erClient", er);
        AssignSerialized(lobbyUi, "walletBootstrap", wallet);
        AssignSerialized(lobbyUi, "config", config);

        EditorUtility.SetDirty(root);
        return root;
    }

    static void EnsurePvPViews(GameObject root)
    {
        var hud = root.GetComponent<PvPMatchHud>() ?? Undo.AddComponent<PvPMatchHud>(root);
        var draft = root.GetComponent<PvPDraftUI>() ?? Undo.AddComponent<PvPDraftUI>(root);
        var viewsGo = GameObject.Find("MatchViews");
        if (viewsGo == null)
        {
            viewsGo = new GameObject("MatchViews");
            Undo.RegisterCreatedObjectUndo(viewsGo, "Create MatchViews");
        }

        var buffer = viewsGo.GetComponent<SnapshotBuffer>() ?? Undo.AddComponent<SnapshotBuffer>(viewsGo);
        var interp = viewsGo.GetComponent<SnapshotInterpolator>() ?? Undo.AddComponent<SnapshotInterpolator>(viewsGo);
        var pool = viewsGo.GetComponent<EntityViewPool>() ?? Undo.AddComponent<EntityViewPool>(viewsGo);
        var pump = viewsGo.GetComponent<GameEventPump>() ?? Undo.AddComponent<GameEventPump>(viewsGo);
        var matchView = viewsGo.GetComponent<MatchViewController>() ?? Undo.AddComponent<MatchViewController>(viewsGo);

        AssignSerialized(interp, "buffer", buffer);
        AssignSerialized(matchView, "erClient", root.GetComponent<ErClient>());
        AssignSerialized(matchView, "lobbyManager", root.GetComponent<LobbyManager>());
        AssignSerialized(matchView, "interpolator", interp);
        AssignSerialized(matchView, "entityPool", pool);
        AssignSerialized(matchView, "eventPump", pump);
        AssignSerialized(hud, "erClient", root.GetComponent<ErClient>());
        AssignSerialized(hud, "lobbyManager", root.GetComponent<LobbyManager>());
        AssignSerialized(draft, "erClient", root.GetComponent<ErClient>());
        AssignSerialized(draft, "lobbyManager", root.GetComponent<LobbyManager>());
        EditorUtility.SetDirty(viewsGo);
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
        AssignSerialized(net, "lobbyManager", Object.FindFirstObjectByType<LobbyManager>());

        // Avoid double-driving the rigidbody while ER inputs are live.
        movement.enabled = false;
        var rb = ship.GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = true;
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

        foreach (var cannon in remote.GetComponentsInChildren<PirateGame.Combat.CannonAutoFire>(true))
            cannon.enabled = false;
        foreach (var magnet in remote.GetComponentsInChildren<PirateGame.Progression.PickupMagnet>(true))
            magnet.enabled = false;

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
        if (remote.GetComponent<ShipPredictionReconciler>() == null)
            Undo.AddComponent<ShipPredictionReconciler>(remote);

        var er = Object.FindFirstObjectByType<ErClient>();
        var dual = remote.GetComponent<LocalDualShipController>();
        if (dual != null && er != null)
            AssignSerialized(dual, "erClient", er);
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

    static void EnsureSceneInBuildSettings(string scenePath)
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        for (var i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path != scenePath) continue;
            if (!scenes[i].enabled)
            {
                scenes[i] = new EditorBuildSettingsScene(scenePath, true);
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            return;
        }

        scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
