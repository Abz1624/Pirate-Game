using UnityEngine;

namespace RoguePirates.V5.Net
{
    /// <summary>
    /// V5 M9b playtest fixes (A): draws the net client's ship between its last two net ticks every frame
    /// (<see cref="NetClient.DrawShip"/>). Its LateUpdate runs before the camera's follow (execution order 0), so the camera
    /// follows the drawn ship, not the 30 Hz stepped one.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public sealed class NetShipDraw : MonoBehaviour
    {
        NetClient _client;

        void LateUpdate()
        {
            if (_client == null) _client = GetComponent<NetClient>();
            if (_client != null) _client.DrawShip();
        }
    }
}
