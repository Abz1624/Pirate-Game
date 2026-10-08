using System;
using Unity.Netcode.Transports.UTP;

namespace RoguePirates.V5.Net
{
    /// <summary>
    /// V5 M9b (I9b.1): where a client connects, from one URL. <c>ws://127.0.0.1:7777/</c> is a local server (plain
    /// WebSockets); <c>wss://match.example.com/</c> is the Railway service: Railway's edge ends TLS, so the browser
    /// connects with secure WebSockets (<c>WithSecureClientParameters(serverName)</c>, through
    /// <see cref="UnityTransport.SetClientSecrets"/>) while the server listens on plain WS on <c>$PORT</c>.
    /// Every client (WebGL included) and the server use WebSockets: the browser has nothing else.
    /// </summary>
    public readonly struct NetEndpoint
    {
        public readonly string Host;
        public readonly ushort Port;
        public readonly bool Secure;
        public readonly string Path;

        public NetEndpoint(string host, ushort port, bool secure, string path = "/")
        {
            Host = host;
            Port = port;
            Secure = secure;
            Path = string.IsNullOrEmpty(path) ? "/" : path;
        }

        public string Url => (Secure ? "wss://" : "ws://") + Host + ":" + Port + Path;
        public override string ToString() => Url;

        public static bool TryParse(string url, out NetEndpoint endpoint, out string error)
        {
            endpoint = default;
            error = null;
            if (string.IsNullOrWhiteSpace(url)) { error = "no address"; return false; }
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) { error = "not a URL: " + url; return false; }
            bool secure;
            if (uri.Scheme == "wss") secure = true;
            else if (uri.Scheme == "ws") secure = false;
            else { error = "use ws:// (local) or wss:// (Railway), not " + uri.Scheme; return false; }
            var port = uri.IsDefaultPort || uri.Port < 0 ? (secure ? 443 : 80) : uri.Port;
            if (port <= 0 || port > ushort.MaxValue) { error = "bad port " + port; return false; }
            endpoint = new NetEndpoint(uri.Host, (ushort)port, secure, uri.AbsolutePath);
            return true;
        }

        public static NetEndpoint Parse(string url) =>
            TryParse(url, out var endpoint, out var error) ? endpoint : throw new FormatException(error);

        /// <summary>A client transport for this endpoint: WebSockets always; TLS checked against <see cref="Host"/> on wss.</summary>
        public void ApplyClient(UnityTransport transport)
        {
            transport.UseWebSockets = true;
            transport.UseEncryption = Secure;
            transport.SetConnectionData(true, Host, Port);
            transport.ConnectionData.WebSocketPath = Path;
            if (Secure) transport.SetClientSecrets(Host);
        }

        /// <summary>
        /// A server transport: plain WebSockets on <paramref name="port"/> at <paramref name="listenAddress"/>
        /// (0.0.0.0 in a container; TLS is Railway's edge's job, so no certificate lives on the server).
        /// </summary>
        public static void ApplyServer(UnityTransport transport, ushort port, string listenAddress = "0.0.0.0")
        {
            transport.UseWebSockets = true;
            transport.UseEncryption = false;
            transport.SetConnectionData(true, "127.0.0.1", port, listenAddress);
        }

        /// <summary>The server's port: Railway's <c>$PORT</c>, else <paramref name="fallback"/>.</summary>
        public static ushort PortFromEnvironment(ushort fallback = 7777) =>
            ushort.TryParse(Environment.GetEnvironmentVariable("PORT"), out var port) && port > 0 ? port : fallback;
    }
}
