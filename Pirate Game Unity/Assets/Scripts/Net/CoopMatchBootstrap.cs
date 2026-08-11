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

            Debug.Log($"[Coop] Boot — mode={config?.mode} ER={config?.ErRpc}");
        }

        void Start()
        {
            lobbyManager?.RefreshLobbyList();
        }
    }
}
