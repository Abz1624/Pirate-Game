using UnityEngine;

namespace RoguePirates.V5.Net
{
    /// <summary>V5 M9b: closes the server's tick-time window, after the run's frame pipeline (order 10).</summary>
    [DefaultExecutionOrder(11)]
    [AddComponentMenu("")]
    public sealed class ServerWorkEnd : MonoBehaviour
    {
        NetServer _server;

        void Update()
        {
            if (_server == null) _server = GetComponent<NetServer>();
            if (_server != null && _server.Manager != null && _server.Manager.IsServer) _server.EndWork();
        }
    }
}
