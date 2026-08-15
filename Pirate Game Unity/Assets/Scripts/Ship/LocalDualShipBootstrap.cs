using UnityEngine;
using PirateGame.Net;

namespace PirateGame.Ship
{
    /// <summary>
    /// Ensures RemoteShip has dual-mode components (runs in default assembly, not Net).
    /// </summary>
    public class LocalDualShipBootstrap : MonoBehaviour
    {
        void Awake()
        {
            foreach (var net in GetComponentsInChildren<NetworkedShipController>(true))
                Destroy(net);

            if (GetComponent<ShipPredictionReconciler>() == null)
                gameObject.AddComponent<ShipPredictionReconciler>();
            if (GetComponent<LocalDualShipController>() == null)
                gameObject.AddComponent<LocalDualShipController>();
        }
    }
}
