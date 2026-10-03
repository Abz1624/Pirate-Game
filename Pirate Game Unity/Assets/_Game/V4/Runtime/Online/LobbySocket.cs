using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace RoguePirates.V4.Online
{
    /// <summary>What happened on a lobby socket (handed out on the main thread by <see cref="ILobbySocket.Poll"/>).</summary>
    public enum SocketEventKind { Opened, Message, Closed }

    public struct SocketEvent
    {
        public SocketEventKind Kind;
        /// <summary>The message text, or the close reason.</summary>
        public string Text;
    }

    /// <summary>
    /// Playtest (3 Oct, owner): the lobby WebSocket behind one interface: the browser's WebSocket through a .jslib on
    /// WebGL (System.Net.WebSockets does not run there), ClientWebSocket in the Editor and on Android. Events are queued
    /// and handed out by <see cref="Poll"/> on the main thread; sends never block.
    /// </summary>
    public interface ILobbySocket : IDisposable
    {
        bool IsOpen { get; }
        void Connect(string url);
        void Send(string text);
        void Close();
        bool Poll(out SocketEvent e);
    }

    public static class LobbySockets
    {
        /// <summary>The socket for this platform.</summary>
        public static ILobbySocket Create()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new WebGlLobbySocket();
#else
            return new ClientLobbySocket();
#endif
        }

        /// <summary>ws(s):// of an http(s):// API base.</summary>
        public static string WsBase(string httpBase)
        {
            if (string.IsNullOrEmpty(httpBase)) return null;
            if (httpBase.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return "wss://" + httpBase.Substring(8);
            if (httpBase.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) return "ws://" + httpBase.Substring(7);
            return httpBase;
        }

        /// <summary>Copies text to the clipboard (the browser's clipboard on WebGL).</summary>
        public static void CopyToClipboard(string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGlLobbySocket.Copy(text ?? string.Empty);
#else
            GUIUtility.systemCopyBuffer = text ?? string.Empty;
#endif
        }

        /// <summary>WebGL: removes ?join= from the address bar once the code was read (a reload does not join again).</summary>
        public static void DropJoinFromUrl()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGlLobbySocket.DropJoin();
#endif
        }
    }

    /// <summary>ClientWebSocket (Editor, Android, desktop): a receive loop on the thread pool, one send at a time.</summary>
    public sealed class ClientLobbySocket : ILobbySocket
    {
        readonly ConcurrentQueue<SocketEvent> _events = new ConcurrentQueue<SocketEvent>();
        readonly ConcurrentQueue<string> _outbox = new ConcurrentQueue<string>();
        readonly SemaphoreSlim _sendSignal = new SemaphoreSlim(0);
        System.Net.WebSockets.ClientWebSocket _ws;
        CancellationTokenSource _cts;
        volatile bool _open;

        public bool IsOpen => _open;

        public void Connect(string url)
        {
            Close();
            _cts = new CancellationTokenSource();
            _ws = new System.Net.WebSockets.ClientWebSocket();
            var ws = _ws;
            var token = _cts.Token;
            Task.Run(() => Run(ws, url, token));
        }

        async Task Run(System.Net.WebSockets.ClientWebSocket ws, string url, CancellationToken token)
        {
            try
            {
                await ws.ConnectAsync(new Uri(url), token);
                _open = true;
                _events.Enqueue(new SocketEvent { Kind = SocketEventKind.Opened });
                _ = Task.Run(() => SendLoop(ws, token));
                var buffer = new byte[16 * 1024];
                var text = new StringBuilder();
                while (!token.IsCancellationRequested && ws.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    var r = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (r.MessageType == System.Net.WebSockets.WebSocketMessageType.Close) break;
                    text.Append(Encoding.UTF8.GetString(buffer, 0, r.Count));
                    if (!r.EndOfMessage) continue;
                    _events.Enqueue(new SocketEvent { Kind = SocketEventKind.Message, Text = text.ToString() });
                    text.Clear();
                }
                var reason = ws.CloseStatus.HasValue ? ((int)ws.CloseStatus.Value) + " " + ws.CloseStatusDescription : "closed";
                _open = false;
                _events.Enqueue(new SocketEvent { Kind = SocketEventKind.Closed, Text = reason });
            }
            catch (Exception e)
            {
                _open = false;
                if (!token.IsCancellationRequested) _events.Enqueue(new SocketEvent { Kind = SocketEventKind.Closed, Text = e.Message });
            }
        }

        async Task SendLoop(System.Net.WebSockets.ClientWebSocket ws, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await _sendSignal.WaitAsync(token);
                    while (_outbox.TryDequeue(out var text))
                    {
                        if (ws.State != System.Net.WebSockets.WebSocketState.Open) return;
                        var bytes = Encoding.UTF8.GetBytes(text);
                        await ws.SendAsync(new ArraySegment<byte>(bytes), System.Net.WebSockets.WebSocketMessageType.Text, true, token);
                    }
                }
            }
            catch (Exception) { /* closed or cancelled: the receive loop reports it */ }
        }

        public void Send(string text)
        {
            if (!_open || string.IsNullOrEmpty(text)) return;
            _outbox.Enqueue(text);
            _sendSignal.Release();
        }

        public void Close()
        {
            _open = false;
            var ws = _ws;
            var cts = _cts;
            _ws = null;
            _cts = null;
            while (_outbox.TryDequeue(out _)) { }
            if (ws == null) return;
            try
            {
                if (ws.State == System.Net.WebSockets.WebSocketState.Open)
                    _ = ws.CloseOutputAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None)
                        .ContinueWith(_ => { try { cts?.Cancel(); ws.Dispose(); } catch (Exception) { } });
                else { cts?.Cancel(); ws.Dispose(); }
            }
            catch (Exception) { }
        }

        public bool Poll(out SocketEvent e) => _events.TryDequeue(out e);

        public void Dispose() => Close();
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    /// <summary>The browser's WebSocket through RoguePiratesPvp.jslib (polled each frame).</summary>
    public sealed class WebGlLobbySocket : ILobbySocket
    {
        [DllImport("__Internal")] static extern int RPPvp_SocketOpen(string url);
        [DllImport("__Internal")] static extern int RPPvp_SocketSend(int id, string text);
        [DllImport("__Internal")] static extern void RPPvp_SocketClose(int id);
        [DllImport("__Internal")] static extern string RPPvp_SocketPoll(int id);
        [DllImport("__Internal")] static extern void RPPvp_CopyText(string text);
        [DllImport("__Internal")] static extern void RPPvp_DropJoinFromUrl();

        int _id;
        bool _open;

        public bool IsOpen => _open;

        public void Connect(string url)
        {
            Close();
            _id = RPPvp_SocketOpen(url);
        }

        public void Send(string text)
        {
            if (_id == 0 || !_open || string.IsNullOrEmpty(text)) return;
            RPPvp_SocketSend(_id, text);
        }

        public void Close()
        {
            if (_id != 0) RPPvp_SocketClose(_id);
            _id = 0;
            _open = false;
        }

        public bool Poll(out SocketEvent e)
        {
            e = default;
            if (_id == 0) return false;
            var raw = RPPvp_SocketPoll(_id);
            if (string.IsNullOrEmpty(raw)) return false;
            switch (raw[0])
            {
                case 'O': _open = true; e.Kind = SocketEventKind.Opened; break;
                case 'M': e.Kind = SocketEventKind.Message; e.Text = raw.Substring(1); break;
                default: _open = false; e.Kind = SocketEventKind.Closed; e.Text = raw.Substring(1); break;
            }
            return true;
        }

        public void Dispose() => Close();

        public static void Copy(string text) => RPPvp_CopyText(text);
        public static void DropJoin() => RPPvp_DropJoinFromUrl();
    }
#endif
}
