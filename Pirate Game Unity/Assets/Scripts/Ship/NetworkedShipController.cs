using UnityEngine;
using UnityEngine.InputSystem;
using PirateGame.Net;

namespace PirateGame.Ship
{
    /// <summary>
    /// Networked ship: submit input to ER, display smoothed on-chain snapshot position.
    /// </summary>
    public class NetworkedShipController : MonoBehaviour
    {
        [SerializeField] ErClient erClient;
        [SerializeField] LobbyManager lobbyManager;
        [SerializeField] float positionSmoothTime = 0.08f;
        [SerializeField] float rotationSmoothTime = 0.08f;
        [SerializeField] float snapDistance = 8f;

        byte localSlot;
        uint lastSnapshotTick;
        Vector3 targetPosition;
        float targetHeadingDeg;
        Vector3 positionVelocity;
        float rotationVelocity;
        bool hasTarget;

        void Awake()
        {
            EnsureLobby();
            if (erClient == null)
                erClient = FindFirstObjectByType<ErClient>();
            if (lobbyManager != null)
            {
                lobbyManager.OnMatchStarted += OnMatchStarted;
                localSlot = lobbyManager.LocalPlayerSlot;
            }
            if (erClient != null)
                erClient.OnSnapshotReceived += OnSnapshot;
        }

        void EnsureLobby()
        {
            if (lobbyManager == null)
                lobbyManager = FindFirstObjectByType<LobbyManager>();
        }

        void OnDestroy()
        {
            if (lobbyManager != null)
                lobbyManager.OnMatchStarted -= OnMatchStarted;
            if (erClient != null)
                erClient.OnSnapshotReceived -= OnSnapshot;
        }

        void OnMatchStarted()
        {
            EnsureLobby();
            if (lobbyManager != null)
                localSlot = lobbyManager.LocalPlayerSlot;
            lastSnapshotTick = 0;
            hasTarget = false;
            positionVelocity = Vector3.zero;
            rotationVelocity = 0f;
        }

        void FixedUpdate()
        {
            if (erClient == null || !erClient.MatchLive) return;

            var dualMode = erClient.HasSecondarySession;
            var input = ReadInput(dualMode);
            if (Mathf.Abs(input.throttle) > 0.01f || Mathf.Abs(input.steer) > 0.01f)
                erClient.SubmitInput(input.throttle, input.steer, 0);
        }

        void Update()
        {
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

        void OnSnapshot(MatchSnapshot snap)
        {
            if (snap?.players == null || snap.players.Length <= localSlot) return;

            var p = snap.players[localSlot];
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

        static ShipPredictor.ShipInput ReadInput(bool dualMode)
        {
            var input = new ShipPredictor.ShipInput();
            if (Gamepad.current != null)
            {
                var stick = Gamepad.current.leftStick.ReadValue();
                input.throttle += stick.y;
                input.steer += stick.x;
            }
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed)
                    input.throttle += 1f;
                if (Keyboard.current.sKey.isPressed)
                    input.throttle -= 1f;
                if (Keyboard.current.aKey.isPressed)
                    input.steer -= 1f;
                if (Keyboard.current.dKey.isPressed)
                    input.steer += 1f;

                if (!dualMode)
                {
                    if (Keyboard.current.upArrowKey.isPressed)
                        input.throttle += 1f;
                    if (Keyboard.current.downArrowKey.isPressed)
                        input.throttle -= 1f;
                    if (Keyboard.current.leftArrowKey.isPressed)
                        input.steer -= 1f;
                    if (Keyboard.current.rightArrowKey.isPressed)
                        input.steer += 1f;
                }
            }
            input.throttle = Mathf.Clamp(input.throttle, -1f, 1f);
            input.steer = Mathf.Clamp(input.steer, -1f, 1f);
            return input;
        }
    }
}
