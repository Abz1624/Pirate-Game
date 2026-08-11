using UnityEngine;
using UnityEngine.InputSystem;
using PirateGame.Net;

namespace PirateGame.Ship
{
    /// <summary>
    /// Networked ship control: local prediction + ER input submit.
    /// Replaces direct physics on the player ship prefab.
    /// </summary>
    [RequireComponent(typeof(ShipPredictionReconciler))]
    public class NetworkedShipController : MonoBehaviour
    {
        [SerializeField] ErClient erClient;
        ShipPredictionReconciler reconciler;
        byte localSlot;

        void Awake()
        {
            reconciler = GetComponent<ShipPredictionReconciler>();
            if (erClient == null)
                erClient = FindFirstObjectByType<ErClient>();
            if (erClient != null)
                erClient.OnSnapshotReceived += OnSnapshot;
        }

        void OnDestroy()
        {
            if (erClient != null)
                erClient.OnSnapshotReceived -= OnSnapshot;
        }

        void FixedUpdate()
        {
            var input = ReadInput();
            reconciler.ApplyInput(input, Time.fixedDeltaTime);
            erClient?.SubmitInput(input.throttle, input.steer, 0);

            transform.position = reconciler.GetDisplayPosition();
            transform.rotation = Quaternion.Euler(0f, reconciler.GetDisplayHeading(), 0f);
        }

        void OnSnapshot(MatchSnapshot snap)
        {
            foreach (var p in snap.players)
            {
                if (p.slot != localSlot) continue;
                reconciler.Reconcile(new Vector3(p.x, 0f, p.z), p.heading / 1024f * 360f);
                break;
            }
        }

        static ShipPredictor.ShipInput ReadInput()
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
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                    input.throttle += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                    input.throttle -= 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                    input.steer -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                    input.steer += 1f;
            }
            input.throttle = Mathf.Clamp(input.throttle, -1f, 1f);
            input.steer = Mathf.Clamp(input.steer, -1f, 1f);
            return input;
        }
    }
}
