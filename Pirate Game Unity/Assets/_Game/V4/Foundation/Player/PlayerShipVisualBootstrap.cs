using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Player
{
    /// <summary>
    /// Ensures the player ship has an artist visual in scenes that only have a bare PlayerShip controller.
    /// </summary>
    [DefaultExecutionOrder(-105)]
    public sealed class PlayerShipVisualBootstrap : MonoBehaviour
    {
        void Awake()
        {
            var player = FindFirstObjectByType<PlayerShipController>();
            if (player == null || HasShipVisual(player.transform))
                return;

            var database = GameDatabaseLoader.Load();
            if (database?.ships == null || database.ships.Length == 0)
                return;

            var shipDef = database.ships[0];
            if (shipDef?.visualPrefab == null)
                return;

            var instance = Instantiate(shipDef.visualPrefab, player.transform);
            instance.name = shipDef.visualPrefab.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            SanitizeVisual(instance);
            EnsureVisualPivot(player);
        }

        static bool HasShipVisual(Transform playerTransform)
        {
            for (var i = 0; i < playerTransform.childCount; i++)
            {
                if (playerTransform.GetChild(i).GetComponentInChildren<MeshRenderer>(true) != null)
                    return true;
            }

            return false;
        }

        static void EnsureVisualPivot(PlayerShipController player)
        {
            if (player.transform.Find("VisualTiltPivot") != null)
                return;

            Transform visualChild = null;
            for (var i = 0; i < player.transform.childCount; i++)
            {
                var child = player.transform.GetChild(i);
                if (child.name == "VisualTiltPivot") continue;
                if (child.GetComponentInChildren<MeshRenderer>(true) != null)
                {
                    visualChild = child;
                    break;
                }
            }

            if (visualChild == null)
                return;

            var pivot = new GameObject("VisualTiltPivot").transform;
            pivot.SetParent(player.transform, false);
            visualChild.SetParent(pivot, true);
        }

        static void SanitizeVisual(GameObject visualRoot)
        {
            foreach (var rb in visualRoot.GetComponentsInChildren<Rigidbody>(true))
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            foreach (var col in visualRoot.GetComponentsInChildren<Collider>(true))
                col.enabled = false;
        }
    }
}
