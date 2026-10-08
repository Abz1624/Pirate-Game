using UnityEngine;

namespace RoguePirates.V5.Net
{
    /// <summary>V5 M9b: opens the server's tick-time window, before the run's composition root (order -100).</summary>
    [DefaultExecutionOrder(-101)]
    [AddComponentMenu("")]
    public sealed class ServerWorkBegin : MonoBehaviour
    {
        NetServer _server;

        void Update()
        {
            if (_server == null) _server = GetComponent<NetServer>();
            if (_server != null && _server.Manager != null && _server.Manager.IsServer) _server.BeginWork();
        }
    }
}
