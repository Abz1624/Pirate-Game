using System;
using UnityEngine;

namespace PirateGame.Net
{
    /// <summary>
    /// Boots ER client, lobby, and match view. Attach to a scene bootstrap object.
    /// </summary>
    public class CoopMatchBootstrap : MonoBehaviour
    {
        [SerializeField] BlockchainConfig config;
        [SerializeField] ErClient erClient;
        [SerializeField] LobbyManager lobbyManager;

        void Awake()
        {
            if (config == null)
                config = Resources.Load<BlockchainConfig>("BlockchainConfig");

            EnsureCameraBootstrap();

            // Older setups deactivated RemoteShip; inactive objects never run Awake/OnEnable.
            var remote = GameObject.Find("RemoteShip");
            if (remote != null && !remote.activeSelf)
                remote.SetActive(true);

            Debug.Log($"[Coop] Boot — networking scaffold present (use SoloRunBootstrap for local survivor mode).");
        }

        void EnsureCameraBootstrap()
        {
            var type = Type.GetType("PirateGame.Camera.CoopMatchCameraBootstrap, Assembly-CSharp");
            if (type != null && GetComponent(type) == null)
                gameObject.AddComponent(type);
        }

        void Start()
        {
            if (lobbyManager == null)
                lobbyManager = FindFirstObjectByType<LobbyManager>();
            if (erClient == null)
                erClient = FindFirstObjectByType<ErClient>();

            var wallet = EnsureWalletBootstrap();
            EnsureLobbyUi(wallet);
            lobbyManager?.RefreshLobbyList();
        }

        Component EnsureWalletBootstrap()
        {
            var walletType = Type.GetType("PirateGame.UI.WalletBootstrap, Assembly-CSharp");
            if (walletType == null)
                return null;

            var existing = GetComponent(walletType);
            if (existing != null)
                return existing;

            var wallet = gameObject.AddComponent(walletType);
            AssignIfPossible(wallet, "config", config);
            return wallet;
        }

        void EnsureLobbyUi(Component wallet)
        {
            var uiType = Type.GetType("PirateGame.UI.CoopLobbyUI, Assembly-CSharp");
            if (uiType == null)
            {
                Debug.LogWarning("[Coop] CoopLobbyUI not found — use keyboard shortcuts (U/O/P) or add CoopLobbyUI manually.");
                return;
            }

            if (GetComponent(uiType) != null)
                return;

            var ui = gameObject.AddComponent(uiType);
            AssignIfPossible(ui, "lobbyManager", lobbyManager);
            AssignIfPossible(ui, "erClient", erClient);
            AssignIfPossible(ui, "walletBootstrap", wallet);
            AssignIfPossible(ui, "config", config);
            Debug.Log("[Coop] CoopLobbyUI attached for wallet host/join.");
        }

        static void AssignIfPossible(Component target, string fieldName, UnityEngine.Object value)
        {
            if (target == null || value == null) return;
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            field?.SetValue(target, value);
        }
    }
}