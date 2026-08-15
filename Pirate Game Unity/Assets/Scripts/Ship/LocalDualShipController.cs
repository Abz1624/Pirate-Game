using UnityEngine;
using UnityEngine.InputSystem;
using PirateGame.Net;

namespace PirateGame.Ship
{
    /// <summary>
    /// Local dual mode: arrow keys drive slot 1; display follows on-chain snapshot.
    /// </summary>
    public class LocalDualShipController : MonoBehaviour
    {
        const byte SlotIndex = 1;

        [SerializeField] ErClient erClient;
        [SerializeField] RemoteShipView remoteView;
        [SerializeField] float positionSmoothTime = 0.08f;
        [SerializeField] float rotationSmoothTime = 0.08f;
        [SerializeField] float snapDistance = 8f;

        Renderer[] renderers;
        bool loggedReady;
        uint lastSnapshotTick;
        Vector3 targetPosition;
        float targetHeadingDeg;
        Vector3 positionVelocity;
        float rotationVelocity;
        bool hasTarget;

        void Awake()
        {
            if (erClient == null)
                erClient = FindFirstObjectByType<ErClient>();
            if (remoteView == null)
                remoteView = GetComponent<RemoteShipView>();
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        void OnEnable()
        {
            var lobby = FindFirstObjectByType<LobbyManager>();
            if (lobby != null)
                lobby.OnMatchStarted += OnMatchStarted;
            if (erClient != null)
                erClient.OnSnapshotReceived += OnSnapshot;
        }

        void OnDisable()
        {
            var lobby = FindFirstObjectByType<LobbyManager>();
            if (lobby != null)
                lobby.OnMatchStarted -= OnMatchStarted;
            if (erClient != null)
                erClient.OnSnapshotReceived -= OnSnapshot;
            if (remoteView != null)
                remoteView.enabled = true;
        }

        void OnMatchStarted()
        {
            loggedReady = false;
            lastSnapshotTick = 0;
            hasTarget = false;
            positionVelocity = Vector3.zero;
            rotationVelocity = 0f;

            if (!IsActiveMode()) return;

            if (remoteView != null)
                remoteView.enabled = false;

            var spawn = new Vector3(-2f, 0f, 0f);
            transform.SetPositionAndRotation(spawn, Quaternion.identity);
            targetPosition = spawn;
            SetVisualsVisible(true);
            Debug.Log($"[Coop] Local dual ship2 active at {spawn} — arrow keys, slot 1");
        }

        void Update()
        {
            var active = IsActiveMode();
            if (remoteView != null && active && remoteView.enabled)
                remoteView.enabled = false;

            if (!active)
            {
                if (remoteView != null && erClient != null && erClient.MatchLive)
                    remoteView.enabled = true;
                return;
            }

            SetVisualsVisible(true);

            if (!hasTarget) return;

            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref positionVelocity,
                positionSmoothTime);

            var smoothedY = Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                targetHeadingDeg,
                ref rotationVelocity,
                rotationSmoothTime);
            transform.rotation = Quaternion.Euler(0f, smoothedY, 0f);
        }

        void FixedUpdate()
        {
            if (!IsActiveMode()) return;

            var input = ReadInput();
            if (Mathf.Abs(input.throttle) > 0.01f || Mathf.Abs(input.steer) > 0.01f)
            {
                erClient.SubmitSecondaryInput(input.throttle, input.steer);
                if (!loggedReady)
                {
                    Debug.Log("[Coop] Ship2 steering — arrows submit to slot 1");
                    loggedReady = true;
                }
            }
        }

        void OnSnapshot(MatchSnapshot snap)
        {
            if (!IsActiveMode() || snap?.players == null || snap.players.Length <= SlotIndex)
                return;

            var p = snap.players[SlotIndex];
            if (!SnapshotDecoder.IsPlayerSlotPopulated(p)) return;
            if (snap.tick < lastSnapshotTick) return;

            var newPos = new Vector3(p.x, 0f, p.z);
            var newHeading = p.heading / 1024f * 360f;

            if (!hasTarget || snap.tick > lastSnapshotTick + 1 ||
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
            hasTarget = true;
            lastSnapshotTick = snap.tick;
        }

        bool IsActiveMode()
        {
            return erClient != null && erClient.MatchLive && erClient.HasSecondarySession;
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

        static ShipPredictor.ShipInput ReadInput()
        {
            var input = new ShipPredictor.ShipInput();
            var kb = Keyboard.current;
            if (kb == null) return input;

            if (kb.upArrowKey.isPressed)
                input.throttle += 1f;
            if (kb.downArrowKey.isPressed)
                input.throttle -= 1f;
            if (kb.leftArrowKey.isPressed)
                input.steer -= 1f;
            if (kb.rightArrowKey.isPressed)
                input.steer += 1f;

            input.throttle = Mathf.Clamp(input.throttle, -1f, 1f);
            input.steer = Mathf.Clamp(input.steer, -1f, 1f);
            return input;
        }
    }
}
