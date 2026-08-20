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
        [SerializeField] ShipPredictionReconciler reconciler;
        [SerializeField] float snapDistance = 8f;

        Renderer[] renderers;
        bool loggedReady;
        uint lastSnapshotTick;
        bool hasTarget;
        Rigidbody body;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (erClient == null)
                erClient = FindFirstObjectByType<ErClient>();
            if (reconciler == null)
                reconciler = GetComponent<ShipPredictionReconciler>();
            if (reconciler == null)
                reconciler = gameObject.AddComponent<ShipPredictionReconciler>();
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

            var remoteView = GetComponent<RemoteShipView>();
            if (remoteView != null)
                remoteView.enabled = true;
        }

        void OnMatchStarted()
        {
            loggedReady = false;
            lastSnapshotTick = 0;
            hasTarget = false;
            PrepareRigidbodyForNetworkDrive();

            if (erClient == null || !erClient.HasSecondarySession) return;

            var remoteView = GetComponent<RemoteShipView>();
            if (remoteView != null)
                remoteView.enabled = false;

            SetVisualsVisible(true);
            TrySyncSpawnSnapshot(erClient.LatestSnapshot);
            Debug.Log("[Coop] Local dual ship2 active — arrow keys, slot 1");
        }

        void TrySyncSpawnSnapshot(MatchSnapshot snap)
        {
            if (snap?.players == null || snap.players.Length <= SlotIndex) return;

            var p = snap.players[SlotIndex];
            reconciler.SyncToServerTick(snap.tick, p.x, p.z, p.heading);
            ApplyTransform(p.x, p.z, p.heading);
            hasTarget = true;
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

        void LateUpdate()
        {
            if (!IsActiveMode() || !hasTarget) return;

            SetVisualsVisible(true);

            var pos = reconciler.GetDisplayPosition();
            var rot = Quaternion.Euler(0f, reconciler.GetDisplayHeading(), 0f);
            transform.SetPositionAndRotation(pos, rot);
        }

        void FixedUpdate()
        {
            if (!IsActiveMode() || !hasTarget) return;

            var input = ReadInput();
            var advanced = reconciler.TryAdvanceSimulationTick(Time.fixedDeltaTime);
            if (advanced)
                reconciler.QueueInput(input);

            erClient.PublishLocalInput(SlotIndex, input.throttle, input.steer);

            if ((input.throttle != 0 || input.steer != 0) && !loggedReady)
            {
                Debug.Log("[Coop] Ship2 steering — arrows submit to slot 1");
                loggedReady = true;
            }
        }

        void OnSnapshot(MatchSnapshot snap)
        {
            if (!IsActiveMode() || snap?.players == null || snap.players.Length <= SlotIndex)
                return;

            var p = snap.players[SlotIndex];
            if (snap.tick < lastSnapshotTick) return;

            if (!hasTarget)
            {
                reconciler.SyncToServerTick(snap.tick, p.x, p.z, p.heading);
                ApplyTransform(p.x, p.z, p.heading);
                hasTarget = true;
                lastSnapshotTick = snap.tick;
                return;
            }

            if (snap.tick <= lastSnapshotTick) return;

            var newPos = new Vector3(p.x, 0f, p.z);
            if (Vector3.Distance(transform.position, newPos) > snapDistance)
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

            float throttle = 0f;
            float steer = 0f;
            if (kb.upArrowKey.isPressed) throttle += 1f;
            if (kb.downArrowKey.isPressed) throttle -= 1f;
            if (kb.leftArrowKey.isPressed) steer -= 1f;
            if (kb.rightArrowKey.isPressed) steer += 1f;

            input.throttle = (sbyte)Mathf.RoundToInt(Mathf.Clamp(throttle, -1f, 1f) * 127f);
            input.steer = (sbyte)Mathf.RoundToInt(Mathf.Clamp(steer, -1f, 1f) * 127f);
            return input;
        }
    }
}

