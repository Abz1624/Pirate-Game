using UnityEngine;
using UnityEngine.InputSystem;
using PirateGame.Net;

namespace PirateGame.Ship
{
    /// <summary>
    /// Local ship: client-side fixed-point prediction + on-chain reconciliation.
    /// </summary>
    public class NetworkedShipController : MonoBehaviour
    {
        [SerializeField] ErClient erClient;
        [SerializeField] LobbyManager lobbyManager;
        [SerializeField] ShipPredictionReconciler reconciler;
        [SerializeField] float snapDistance = 8f;

        byte localSlot;
        uint lastSnapshotTick;
        bool hasSnapshot;
        Rigidbody body;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            EnsureLobby();
            if (erClient == null)
                erClient = FindFirstObjectByType<ErClient>();
            if (reconciler == null)
                reconciler = GetComponent<ShipPredictionReconciler>();
            if (reconciler == null)
                reconciler = gameObject.AddComponent<ShipPredictionReconciler>();

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
            hasSnapshot = false;
            PrepareRigidbodyForNetworkDrive();
            TrySyncSpawnSnapshot(erClient?.LatestSnapshot);
        }

        void TrySyncSpawnSnapshot(MatchSnapshot snap)
        {
            if (!SnapshotDecoder.TryGetPlayerBySlot(snap, localSlot, out var p)) return;

            reconciler.SyncToServerTick(snap.tick, p.x, p.z, p.heading);
            ApplyTransform(p.x, p.z, p.heading);
            hasSnapshot = true;
            lastSnapshotTick = snap.tick;
        }

        void PrepareRigidbodyForNetworkDrive()
        {
            if (body == null) return;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
        }

        void FixedUpdate()
        {
            if (erClient == null || !erClient.MatchLive || !hasSnapshot) return;

            var input = ReadInput(erClient.HasSecondarySession);
            var advanced = reconciler.TryAdvanceSimulationTick(Time.fixedDeltaTime);
            if (advanced)
                reconciler.QueueInput(input);

            erClient.SubmitInput(input.throttle / 127f, input.steer / 127f, 0, force: advanced);
        }

        void LateUpdate()
        {
            if (erClient == null || !erClient.MatchLive || !hasSnapshot) return;

            var pos = reconciler.GetDisplayPosition();
            var rot = Quaternion.Euler(0f, reconciler.GetDisplayHeading(), 0f);
            transform.SetPositionAndRotation(pos, rot);
        }

        void OnSnapshot(MatchSnapshot snap)
        {
            if (!SnapshotDecoder.TryGetPlayerBySlot(snap, localSlot, out var p)) return;
            if (snap.tick < lastSnapshotTick) return;

            if (!hasSnapshot)
            {
                reconciler.SyncToServerTick(snap.tick, p.x, p.z, p.heading);
                ApplyTransform(p.x, p.z, p.heading);
                hasSnapshot = true;
                lastSnapshotTick = snap.tick;
                return;
            }

            if (snap.tick <= lastSnapshotTick) return;

            if (Vector3.Distance(transform.position, new Vector3(p.x, 0f, p.z)) > snapDistance)
            {
                reconciler.SyncToServerTick(snap.tick, p.x, p.z, p.heading);
                ApplyTransform(p.x, p.z, p.heading);
            }
            else
            {
                reconciler.Reconcile(snap.tick, p.x, p.z, p.heading);
            }

            lastSnapshotTick = snap.tick;
        }

        void ApplyTransform(short x, short z, ushort heading)
        {
            var pos = new Vector3(x, 0f, z);
            var rot = Quaternion.Euler(0f, heading / 1024f * 360f, 0f);
            transform.SetPositionAndRotation(pos, rot);
        }

        static ShipPredictor.ShipInput ReadInput(bool dualMode)
        {
            var input = new ShipPredictor.ShipInput();
            float throttle = 0f;
            float steer = 0f;

            if (Gamepad.current != null)
            {
                var stick = Gamepad.current.leftStick.ReadValue();
                throttle += stick.y;
                steer += stick.x;
            }
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed) throttle += 1f;
                if (Keyboard.current.sKey.isPressed) throttle -= 1f;
                if (Keyboard.current.aKey.isPressed) steer -= 1f;
                if (Keyboard.current.dKey.isPressed) steer += 1f;

                if (!dualMode)
                {
                    if (Keyboard.current.upArrowKey.isPressed) throttle += 1f;
                    if (Keyboard.current.downArrowKey.isPressed) throttle -= 1f;
                    if (Keyboard.current.leftArrowKey.isPressed) steer -= 1f;
                    if (Keyboard.current.rightArrowKey.isPressed) steer += 1f;
                }
            }

            input.throttle = (sbyte)Mathf.RoundToInt(Mathf.Clamp(throttle, -1f, 1f) * 127f);
            input.steer = (sbyte)Mathf.RoundToInt(Mathf.Clamp(steer, -1f, 1f) * 127f);
            return input;
        }
    }
}

