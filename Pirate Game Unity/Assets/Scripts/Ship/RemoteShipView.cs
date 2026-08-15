using UnityEngine;
using PirateGame.Net;

namespace PirateGame.Ship
{
    /// <summary>
    /// Displays another player's ship from ER snapshot data (no local input).
    /// Stays active so Awake/OnEnable run; renderers are hidden until a remote player appears.
    /// </summary>
    public class RemoteShipView : MonoBehaviour
    {
        [SerializeField] ErClient erClient;
        [SerializeField] LobbyManager lobbyManager;
        [SerializeField] float positionSmoothTime = 0.08f;
        [SerializeField] float rotationSmoothTime = 0.08f;
        [SerializeField] float snapDistance = 6f;

        byte localSlot;
        Vector3 targetPosition;
        float targetHeadingDeg;
        Vector3 positionVelocity;
        float rotationVelocity;
        bool hasTarget;
        bool loggedRemote;
        bool loggedEmpty;
        Renderer[] renderers;

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            SetVisualsVisible(false);
            EnsureDualComponents();
            EnsureReferences();
        }

        void EnsureDualComponents()
        {
            foreach (var net in GetComponentsInChildren<NetworkedShipController>(true))
                Destroy(net);

            if (GetComponent<LocalDualShipController>() == null)
                gameObject.AddComponent<LocalDualShipController>();
        }

        void OnEnable()
        {
            EnsureReferences();
            if (lobbyManager != null)
                lobbyManager.OnMatchStarted += OnMatchStarted;
            if (erClient != null)
                erClient.OnSnapshotReceived += OnSnapshot;
        }

        void OnDisable()
        {
            if (lobbyManager != null)
                lobbyManager.OnMatchStarted -= OnMatchStarted;
            if (erClient != null)
                erClient.OnSnapshotReceived -= OnSnapshot;
        }

        void EnsureReferences()
        {
            if (lobbyManager == null)
                lobbyManager = FindFirstObjectByType<LobbyManager>();
            if (erClient == null)
                erClient = FindFirstObjectByType<ErClient>();
            RefreshLocalSlot();
        }

        async void OnMatchStarted()
        {
            loggedRemote = false;
            loggedEmpty = false;
            RefreshLocalSlot();
            if (erClient != null)
            {
                await erClient.RefreshMatchAndSnapshotAsync();
                OnSnapshot(erClient.LatestSnapshot);
            }
        }

        void RefreshLocalSlot()
        {
            if (lobbyManager != null)
                localSlot = lobbyManager.LocalPlayerSlot;
        }

        void OnSnapshot(MatchSnapshot snap)
        {
            if (erClient != null && erClient.HasSecondarySession)
                return;

            RefreshLocalSlot();
            var playerCount = snap?.players?.Length ?? 0;
            if (playerCount == 0)
            {
                if (!loggedEmpty && erClient != null && erClient.MatchLive)
                {
                    Debug.Log($"[RemoteShip] waiting for snapshot players (localSlot={localSlot})");
                    loggedEmpty = true;
                }
                return;
            }

            loggedEmpty = false;
            for (var i = 0; i < playerCount; i++)
            {
                if (i == localSlot) continue;

                var p = snap.players[i];
                if (!SnapshotDecoder.IsPlayerSlotPopulated(p)) continue;

                var newPos = new Vector3(p.x, 0f, p.z);
                var newHeading = p.heading / 1024f * 360f;

                if (!hasTarget || Vector3.Distance(transform.position, newPos) > snapDistance)
                {
                    transform.SetPositionAndRotation(
                        newPos,
                        Quaternion.Euler(0f, newHeading, 0f));
                    positionVelocity = Vector3.zero;
                    rotationVelocity = 0f;
                }

                targetPosition = newPos;
                targetHeadingDeg = newHeading;
                hasTarget = true;

                if (!loggedRemote)
                {
                    Debug.Log(
                        $"[RemoteShip] Tracking index {i} slot={p.slot} at ({p.x},{p.z}) localSlot={localSlot} players={playerCount} tick={snap.tick}");
                    loggedRemote = true;
                }

                SetVisualsVisible(true);
                return;
            }

            hasTarget = false;
            SetVisualsVisible(false);
        }

        void SetVisualsVisible(bool visible)
        {
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r != null)
                    r.enabled = visible;
            }
        }

        void Update()
        {
            if (!hasTarget) return;

            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref positionVelocity,
                positionSmoothTime);

            var currentY = transform.eulerAngles.y;
            var smoothedY = Mathf.SmoothDampAngle(
                currentY,
                targetHeadingDeg,
                ref rotationVelocity,
                rotationSmoothTime);
            transform.rotation = Quaternion.Euler(0f, smoothedY, 0f);
        }
    }
}
