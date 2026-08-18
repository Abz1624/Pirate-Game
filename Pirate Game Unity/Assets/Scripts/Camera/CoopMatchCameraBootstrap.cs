using PirateGame.Net;
using UnityEngine;

namespace PirateGame.Camera
{
    /// <summary>
    /// Attaches top-down camera follow when a coop match starts.
    /// Lives outside PirateGame.Net.asmdef so it can reference TopDownCameraFollow.
    /// </summary>
    public class CoopMatchCameraBootstrap : MonoBehaviour
    {
        [SerializeField] LobbyManager lobbyManager;
        [SerializeField] ErClient erClient;

        TopDownCameraFollow follow;

        void Awake()
        {
            if (lobbyManager == null)
                lobbyManager = FindFirstObjectByType<LobbyManager>();
            if (erClient == null)
                erClient = FindFirstObjectByType<ErClient>();
        }

        void OnEnable()
        {
            if (lobbyManager != null)
                lobbyManager.OnMatchStarted += OnMatchStarted;
        }

        void OnDisable()
        {
            if (lobbyManager != null)
                lobbyManager.OnMatchStarted -= OnMatchStarted;
            if (erClient != null)
                erClient.OnSnapshotReceived -= OnSnapshotForCamera;
        }

        void OnMatchStarted()
        {
            var cam = UnityEngine.Camera.main;
            if (cam == null) return;

            follow = cam.GetComponent<TopDownCameraFollow>();
            if (follow == null)
                follow = cam.gameObject.AddComponent<TopDownCameraFollow>();

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                follow.SetTarget(player.transform);

            if (erClient != null)
            {
                erClient.OnSnapshotReceived -= OnSnapshotForCamera;
                erClient.OnSnapshotReceived += OnSnapshotForCamera;
            }

            SnapCameraIfReady();
        }

        void OnSnapshotForCamera(MatchSnapshot snap)
        {
            if (snap?.players == null || snap.players.Length == 0) return;
            SnapCameraIfReady();
            if (erClient != null)
                erClient.OnSnapshotReceived -= OnSnapshotForCamera;
        }

        void SnapCameraIfReady()
        {
            if (follow == null) return;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                follow.SetTarget(player.transform);
            follow.SnapToTarget();
        }
    }
}

