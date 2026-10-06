using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace RoguePirates.Api.Playtest;

/// <summary>Keeps the open lobby WebSockets and pushes the full lobby state (JSON) to each of them.</summary>
public sealed class LobbyBroadcaster
{
    sealed class Connection
    {
        public required WebSocket Socket { get; init; }
        public Guid PlayerId { get; init; }
        public SemaphoreSlim SendLock { get; } = new(1, 1);
    }

    readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Connection>> _lobbies = new();
    readonly ILogger<LobbyBroadcaster> _log;

    public LobbyBroadcaster(ILogger<LobbyBroadcaster> log) => _log = log;

    public Guid Add(Guid lobbyId, Guid playerId, WebSocket socket)
    {
        var id = Guid.NewGuid();
        _lobbies.GetOrAdd(lobbyId, _ => new()).TryAdd(id, new Connection { Socket = socket, PlayerId = playerId });
        return id;
    }

    /// <summary>True while the player has at least one open socket on the lobby.</summary>
    public bool IsConnected(Guid lobbyId, Guid playerId) =>
        _lobbies.TryGetValue(lobbyId, out var set)
        && set.Values.Any(c => c.PlayerId == playerId && c.Socket.State == WebSocketState.Open);

    /// <summary>Sends a message to every socket the player has open on the lobby.</summary>
    public async Task SendToPlayerAsync(Guid lobbyId, Guid playerId, string json)
    {
        if (!_lobbies.TryGetValue(lobbyId, out var set)) return;
        await Task.WhenAll(set.Where(kv => kv.Value.PlayerId == playerId)
            .Select(kv => SendAsync(lobbyId, kv.Key, kv.Value, json, close: false)));
    }

    /// <summary>Closes every socket of the lobby (end of the rematch window).</summary>
    public async Task CloseAllAsync(Guid lobbyId)
    {
        if (!_lobbies.TryGetValue(lobbyId, out var set)) return;
        await Task.WhenAll(set.Select(async kv =>
        {
            await kv.Value.SendLock.WaitAsync();
            try
            {
                if (kv.Value.Socket.State == WebSocketState.Open)
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await kv.Value.Socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "lobby ended", cts.Token);
                }
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "Lobby socket close failed");
            }
            finally
            {
                kv.Value.SendLock.Release();
                Remove(lobbyId, kv.Key);
            }
        }));
    }

    public void Remove(Guid lobbyId, Guid connectionId)
    {
        if (_lobbies.TryGetValue(lobbyId, out var set))
        {
            set.TryRemove(connectionId, out _);
            if (set.IsEmpty) _lobbies.TryRemove(lobbyId, out _);
        }
    }

    public int Count(Guid lobbyId) => _lobbies.TryGetValue(lobbyId, out var set) ? set.Count : 0;

    public Task SendToAsync(Guid lobbyId, Guid connectionId, string json) =>
        _lobbies.TryGetValue(lobbyId, out var set) && set.TryGetValue(connectionId, out var c)
            ? SendAsync(lobbyId, connectionId, c, json, close: false)
            : Task.CompletedTask;

    /// <summary>Pushes the state to every socket of the lobby; closes them afterwards when the lobby has ended.</summary>
    public async Task PublishAsync(Guid lobbyId, string json, bool close)
    {
        if (!_lobbies.TryGetValue(lobbyId, out var set)) return;
        await Task.WhenAll(set.Select(kv => SendAsync(lobbyId, kv.Key, kv.Value, json, close)));
    }

    async Task SendAsync(Guid lobbyId, Guid connectionId, Connection c, string json, bool close)
    {
        await c.SendLock.WaitAsync();
        try
        {
            if (c.Socket.State != WebSocketState.Open) { Remove(lobbyId, connectionId); return; }
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await c.Socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, cts.Token);
            if (close)
                await c.Socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "lobby ended", cts.Token);
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "Lobby socket send failed; dropping it");
            Remove(lobbyId, connectionId);
        }
        finally
        {
            c.SendLock.Release();
        }
    }
}
