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
        [SerializeField] Color remoteTint = new(0.55f, 0.78f, 1f, 1f);

        byte localSlot;
        byte trackedRemoteSlot = byte.MaxValue;
        Vector3 targetPosition;
        float targetHeadingDeg;
        Vector3 positionVelocity;
        float rotationVelocity;
        bool hasTarget;
        bool loggedRemote;
        bool loggedEmpty;
        uint lastTrackedTick;
        bool appliedTint;
        Renderer[] renderers;
        MaterialPropertyBlock tintBlock;

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            tintBlock = new MaterialPropertyBlock();
            SetVisualsVisible(false);
            EnsureDualComponents();
            EnsureReferences();
        }

        void EnsureDualComponents()
        {
            foreach (var net in GetComponentsInChildren<NetworkedShipController>(true))
                Destroy(net);

            var dual = GetComponent<LocalDualShipController>();
            if (dual == null)
                dual = gameObject.AddComponent<LocalDualShipController>();
            dual.enabled = false;
        }

        void OnEnable()
        {
            EnsureReferences();
            if (lobbyManager != null)
            {
                lobbyManager.OnMatchStarted += OnMatchStarted;
                lobbyManager.OnJoinedLobby += OnJoinedLobby;
            }
            if (erClient != null)
                erClient.OnSnapshotReceived += OnSnapshot;
        }

        void OnDisable()
        {
            if (lobbyManager != null)
            {
                lobbyManager.OnMatchStarted -= OnMatchStarted;
                lobbyManager.OnJoinedLobby -= OnJoinedLobby;
            }
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

        void OnJoinedLobby() => RefreshLocalSlot();

        async void OnMatchStarted()
        {
            loggedRemote = false;
            loggedEmpty = false;
            hasTarget = false;
            trackedRemoteSlot = byte.MaxValue;
            appliedTint = false;
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
            var expectedPlayers = erClient != null
                ? System.Math.Max((int)erClient.MatchPlayerCount, snap?.players?.Length ?? 0)
                : snap?.players?.Length ?? 0;

            if (snap?.players == null || snap.players.Length == 0)
            {
                if (!loggedEmpty && erClient != null && erClient.MatchLive)
                {
                    Debug.Log($"[RemoteShip] waiting for snapshot players (localSlot={localSlot})");
                    loggedEmpty = true;
                }
                return;
            }

            loggedEmpty = false;
            if (!TryFindRemotePlayer(snap, localSlot, (byte)expectedPlayers, out var remote))
            {
                hasTarget = false;
                SetVisualsVisible(false);
                return;
            }

            var newPos = new Vector3(remote.x, 0f, remote.z);
            var newHeading = remote.heading / 1024f * 360f;

            if (!hasTarget || trackedRemoteSlot != remote.slot ||
                Vector3.Distance(transform.position, newPos) > snapDistance)
            {
                transform.SetPositionAndRotation(
                    newPos,
                    Quaternion.Euler(0f, newHeading, 0f));
                positionVelocity = Vector3.zero;
                rotationVelocity = 0f;
            }

            targetPosition = newPos;
            targetHeadingDeg = newHeading;
            trackedRemoteSlot = remote.slot;
            hasTarget = true;

            if (!loggedRemote || snap.tick > lastTrackedTick)
            {
                Debug.Log(
                    $"[RemoteShip] Tracking slot={remote.slot} at ({remote.x},{remote.z}) " +
                    $"localSlot={localSlot} players={snap.players.Length} expected={expectedPlayers} tick={snap.tick}");
                loggedRemote = true;
                lastTrackedTick = snap.tick;
            }

            SetVisualsVisible(true);
        }

        static bool TryFindRemotePlayer(
            MatchSnapshot snap,
            byte localSlot,
            byte expectedPlayers,
            out SnapshotPlayerState remote)
        {
            remote = default;
            if (snap?.players == null) return false;

            for (byte i = 0; i < snap.players.Length; i++)
            {
                if (i == localSlot) continue;
                var p = snap.players[i];
                if (!SnapshotDecoder.IsRemotePlayerVisible(p, expectedPlayers)) continue;
                remote = p;
                remote.slot = i;
                return true;
            }

            return false;
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

            if (visible && !appliedTint)
            {
                ApplyRemoteTint();
                appliedTint = true;
            }
        }

        void ApplyRemoteTint()
        {
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(tintBlock);
                tintBlock.SetColor("_Color", remoteTint);
                tintBlock.SetColor("_BaseColor", remoteTint);
                r.SetPropertyBlock(tintBlock);
            }
        }

        void LateUpdate()
        {
            if (erClient != null && erClient.MatchLive && !hasTarget && erClient.LatestSnapshot != null)
                OnSnapshot(erClient.LatestSnapshot);

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
