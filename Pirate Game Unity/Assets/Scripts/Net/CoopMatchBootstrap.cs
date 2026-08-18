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
            lobbyManager?.RefreshLobbyList();
        }
    }
}