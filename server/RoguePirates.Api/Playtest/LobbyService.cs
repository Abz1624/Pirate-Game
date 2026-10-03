using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RoguePirates.Api.Data;

namespace RoguePirates.Api.Playtest;

/// <summary>1v1 lobbies: create, join by code, seat status and drafts, ready -> starting, expiry, WebSocket push.</summary>
public sealed class LobbyService
{
    public const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"; // no 0/O, 1/I/L
    public const int CodeLength = 6;
    public const int DraftSize = 6;
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(15);

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    readonly GameDb _db;
    readonly IClock _clock;
    readonly ApiSettings _settings;
    readonly LobbyBroadcaster _broadcaster;

    public LobbyService(GameDb db, IClock clock, ApiSettings settings, LobbyBroadcaster broadcaster)
    {
        _db = db;
        _clock = clock;
        _settings = settings;
        _broadcaster = broadcaster;
    }

    static string NewCode()
    {
        Span<char> c = stackalloc char[CodeLength];
        for (var i = 0; i < CodeLength; i++) c[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        return new string(c);
    }

    public string InviteUrl(string code) => _settings.PlayUrl + (_settings.PlayUrl.Contains('?') ? "&" : "?") + "join=" + code;

    public async Task<object> CreateAsync(Player host, bool isPublic)
    {
        await GameLocks.Lobby.WaitAsync();
        try
        {
            string code;
            var attempts = 0;
            do
            {
                code = NewCode();
                if (++attempts > 20) throw new ApiException(503, "no_code", "Could not allocate a lobby code; try again.");
            } while (await _db.Lobbies.AnyAsync(l => l.Code == code));

            var now = _clock.UtcNow;
            var lobby = new Lobby
            {
                Id = Guid.NewGuid(), Code = code, IsPublic = isPublic, Status = LobbyStates.Waiting, HostPlayerId = host.Id,
                HostStatus = SeatStates.Joined, CreatedAt = now, UpdatedAt = now, LastActivityAt = now,
            };
            _db.Lobbies.Add(lobby);
            await _db.SaveChangesAsync();
            return await BuildStateAsync(lobby);
        }
        finally
        {
            GameLocks.Lobby.Release();
        }
    }

    public async Task<object> ListPublicAsync()
    {
        await ExpireIdleAsync();
        var rows = await (from l in _db.Lobbies
                          join p in _db.Players on l.HostPlayerId equals p.Id
                          where l.IsPublic && l.Status == LobbyStates.Waiting
                          orderby l.CreatedAt descending
                          select new { l.Id, l.Code, l.CreatedAt, l.LastActivityAt, HostId = p.Id, p.Name, p.Rating })
            .Take(50)
            .ToListAsync();
        return new
        {
            lobbies = rows.Select(r => new
            {
                lobbyId = r.Id, code = r.Code, inviteUrl = InviteUrl(r.Code), createdAt = r.CreatedAt,
                expiresAt = r.LastActivityAt + IdleTimeout,
                host = new { playerId = r.HostId, name = r.Name, tag = PlayerTags.For(r.HostId), rating = r.Rating },
            }),
        };
    }

    public async Task<object> JoinAsync(Player player, string? rawCode)
    {
        var code = (rawCode ?? "").Trim().ToUpperInvariant();
        if (code.Length != CodeLength || code.Any(ch => !CodeAlphabet.Contains(ch)))
            throw new ApiException(400, "invalid_code", "Lobby codes are 6 characters.");

        await GameLocks.Lobby.WaitAsync();
        try
        {
            var lobby = await _db.Lobbies.FirstOrDefaultAsync(l => l.Code == code)
                        ?? throw new ApiException(404, "lobby_not_found", "No lobby with that code.");
            await ExpireIfIdleAsync(lobby);
            if (lobby.GuestPlayerId == player.Id && !LobbyStates.IsTerminal(lobby.Status))
                return await BuildStateAsync(lobby); // already seated: idempotent
            if (lobby.HostPlayerId == player.Id)
                throw new ApiException(409, "own_lobby", "You are the host of this lobby.");
            if (LobbyStates.IsTerminal(lobby.Status))
                throw new ApiException(409, "lobby_closed", "This lobby is closed.");
            if (lobby.Status != LobbyStates.Waiting || lobby.GuestPlayerId != null)
                throw new ApiException(409, "lobby_full", "This lobby is full.");

            lobby.GuestPlayerId = player.Id;
            lobby.GuestStatus = SeatStates.Joined;
            lobby.GuestDraftJson = null;
            lobby.Status = LobbyStates.Full;
            return await SaveAndPublishAsync(lobby);
        }
        finally
        {
            GameLocks.Lobby.Release();
        }
    }

    public async Task<object> GetAsync(Guid lobbyId, Player player)
    {
        await GameLocks.Lobby.WaitAsync();
        try
        {
            var lobby = await LoadForSeatAsync(lobbyId, player);
            await ExpireIfIdleAsync(lobby);
            return await BuildStateAsync(lobby);
        }
        finally
        {
            GameLocks.Lobby.Release();
        }
    }

    public async Task<object> UpdateStatusAsync(Guid lobbyId, Player player, LobbyStatusRequest req)
    {
        var status = (req.Status ?? "").Trim().ToLowerInvariant();
        if (!SeatStates.All.Contains(status))
            throw new ApiException(400, "invalid_status", "status must be one of: " + string.Join(", ", SeatStates.All) + ".");
        var draftJson = req.Draft == null ? null : ValidateDraft(req.Draft);

        await GameLocks.Lobby.WaitAsync();
        try
        {
            var lobby = await LoadForSeatAsync(lobbyId, player);
            await ExpireIfIdleAsync(lobby);
            if (LobbyStates.IsTerminal(lobby.Status))
                throw new ApiException(409, "lobby_closed", "This lobby has ended.");

            var isHost = lobby.HostPlayerId == player.Id;
            var seatStatus = isHost ? lobby.HostStatus : lobby.GuestStatus;
            if (seatStatus == SeatStates.Left && status != SeatStates.Left)
                throw new ApiException(409, "seat_left", "You left this lobby.");
            var preStart = lobby.Status is LobbyStates.Waiting or LobbyStates.Full;

            if (status == SeatStates.Left)
            {
                if (isHost)
                {
                    lobby.HostStatus = SeatStates.Left;
                    lobby.Status = LobbyStates.Closed;
                }
                else if (preStart)
                {
                    // the guest seat opens again
                    lobby.GuestPlayerId = null;
                    lobby.GuestStatus = null;
                    lobby.GuestDraftJson = null;
                    lobby.Status = LobbyStates.Waiting;
                }
                else
                {
                    lobby.GuestStatus = SeatStates.Left;
                }
                var state = await SaveAndPublishAsync(lobby);
                return state;
            }

            if (status == SeatStates.InMatch)
            {
                if (lobby.Status is not (LobbyStates.Starting or LobbyStates.InMatch))
                    throw new ApiException(409, "not_started", "The match has not started.");
                SetSeat(lobby, isHost, SeatStates.InMatch, null);
                var other = isHost ? lobby.GuestStatus : lobby.HostStatus;
                if (other == SeatStates.InMatch) lobby.Status = LobbyStates.InMatch;
                return await SaveAndPublishAsync(lobby);
            }

            // joined, drafting, ready
            if (!preStart)
                throw new ApiException(409, "already_started", "The match is already starting.");
            var existingDraft = isHost ? lobby.HostDraftJson : lobby.GuestDraftJson;
            if (status == SeatStates.Ready && draftJson == null && existingDraft == null)
                throw new ApiException(400, "draft_required", "Send your draft (6 weapons, 6 logbooks) before you are ready.");
            SetSeat(lobby, isHost, status, draftJson);

            if (lobby.Status == LobbyStates.Full && lobby.HostStatus == SeatStates.Ready && lobby.GuestStatus == SeatStates.Ready)
                StartMatch(lobby);

            return await SaveAndPublishAsync(lobby);
        }
        finally
        {
            GameLocks.Lobby.Release();
        }
    }

    static void SetSeat(Lobby lobby, bool isHost, string status, string? draftJson)
    {
        if (isHost)
        {
            lobby.HostStatus = status;
            if (draftJson != null) lobby.HostDraftJson = draftJson;
        }
        else
        {
            lobby.GuestStatus = status;
            if (draftJson != null) lobby.GuestDraftJson = draftJson;
        }
    }

    void StartMatch(Lobby lobby)
    {
        var match = new Match
        {
            Id = Guid.NewGuid(), LobbyId = lobby.Id, HostPlayerId = lobby.HostPlayerId, GuestPlayerId = lobby.GuestPlayerId!.Value,
            Seed = RandomNumberGenerator.GetInt32(int.MaxValue), Status = MatchStates.Pending, CreatedAt = _clock.UtcNow,
        };
        _db.Matches.Add(match);
        lobby.Status = LobbyStates.Starting;
        lobby.MatchId = match.Id;
        lobby.Seed = match.Seed;
        lobby.MatchServer = _settings.MatchServerUrl;
    }

    public static string ValidateDraft(DraftDto draft)
    {
        static List<string> Check(List<string>? ids, string field)
        {
            if (ids == null || ids.Count != DraftSize)
                throw new ApiException(400, "invalid_draft", $"draft.{field} must list exactly {DraftSize} ids in priority order.");
            var cleaned = ids.Select(i => (i ?? "").Trim()).ToList();
            if (cleaned.Any(i => i.Length == 0 || i.Length > 64))
                throw new ApiException(400, "invalid_draft", $"draft.{field} ids must be non-empty (max 64 characters).");
            if (cleaned.Distinct(StringComparer.Ordinal).Count() != DraftSize)
                throw new ApiException(400, "invalid_draft", $"draft.{field} ids must be distinct.");
            return cleaned;
        }

        var weapons = Check(draft.Weapons, "weapons");
        var logbooks = Check(draft.Logbooks, "logbooks");
        return JsonSerializer.Serialize(new DraftDto(weapons, logbooks), Json);
    }

    async Task<Lobby> LoadForSeatAsync(Guid lobbyId, Player player)
    {
        var lobby = await _db.Lobbies.FirstOrDefaultAsync(l => l.Id == lobbyId)
                    ?? throw new ApiException(404, "lobby_not_found", "No such lobby.");
        if (lobby.HostPlayerId != player.Id && lobby.GuestPlayerId != player.Id)
            throw new ApiException(403, "not_in_lobby", "You are not seated in this lobby.");
        return lobby;
    }

    async Task ExpireIfIdleAsync(Lobby lobby)
    {
        if (lobby.Status is LobbyStates.Waiting or LobbyStates.Full && lobby.LastActivityAt + IdleTimeout <= _clock.UtcNow)
        {
            lobby.Status = LobbyStates.Expired;
            lobby.UpdatedAt = _clock.UtcNow;
            await _db.SaveChangesAsync();
            await PublishAsync(lobby);
        }
    }

    /// <summary>Expires every waiting or full lobby idle for 15 minutes (called by the sweeper and before listings).</summary>
    public async Task<int> ExpireIdleAsync()
    {
        var cutoff = _clock.UtcNow - IdleTimeout;
        await GameLocks.Lobby.WaitAsync();
        try
        {
            var stale = await _db.Lobbies
                .Where(l => (l.Status == LobbyStates.Waiting || l.Status == LobbyStates.Full) && l.LastActivityAt <= cutoff)
                .ToListAsync();
            foreach (var lobby in stale) await ExpireIfIdleAsync(lobby);
            return stale.Count;
        }
        finally
        {
            GameLocks.Lobby.Release();
        }
    }

    async Task<object> SaveAndPublishAsync(Lobby lobby)
    {
        var now = _clock.UtcNow;
        lobby.UpdatedAt = now;
        lobby.LastActivityAt = now;
        await _db.SaveChangesAsync();
        return await PublishAsync(lobby);
    }

    /// <summary>Builds the state and pushes it to the lobby's sockets. Caller holds <see cref="GameLocks.Lobby"/>.</summary>
    public async Task<object> PublishAsync(Lobby lobby)
    {
        var state = await BuildStateAsync(lobby);
        await _broadcaster.PublishAsync(lobby.Id, JsonSerializer.Serialize(state, Json), LobbyStates.IsTerminal(lobby.Status));
        return state;
    }

    /// <summary>Registers a WebSocket and sends it the current state, under the lobby lock so pushes stay in order.</summary>
    public async Task<Guid> AttachAsync(Guid lobbyId, Player player, WebSocket socket)
    {
        await GameLocks.Lobby.WaitAsync();
        try
        {
            var lobby = await LoadForSeatAsync(lobbyId, player);
            await ExpireIfIdleAsync(lobby);
            var connection = _broadcaster.Add(lobbyId, socket);
            var state = await BuildStateAsync(lobby);
            await _broadcaster.SendToAsync(lobbyId, connection, JsonSerializer.Serialize(state, Json));
            return connection;
        }
        finally
        {
            GameLocks.Lobby.Release();
        }
    }

    /// <summary>Checks access before the WebSocket handshake (404/403 as HTTP errors).</summary>
    public async Task EnsureSeatAsync(Guid lobbyId, Player player) => await LoadForSeatAsync(lobbyId, player);

    public async Task<object> BuildStateAsync(Lobby lobby)
    {
        var ids = new List<Guid> { lobby.HostPlayerId };
        if (lobby.GuestPlayerId is { } g) ids.Add(g);
        var players = await _db.Players.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        // Drafts stay hidden from the other seat until the match starts; then both are shared for the match host.
        var revealDrafts = lobby.Status is LobbyStates.Starting or LobbyStates.InMatch or LobbyStates.Finished;
        var started = revealDrafts;

        object Seat(string seat, Guid id, string? status, string? draftJson)
        {
            players.TryGetValue(id, out var p);
            return new
            {
                seat, playerId = id, name = p?.Name, tag = PlayerTags.For(id), rating = p?.Rating,
                status, ready = status is SeatStates.Ready || (started && status == SeatStates.InMatch),
                hasDraft = draftJson != null,
                draft = revealDrafts && draftJson != null ? JsonSerializer.Deserialize<DraftDto>(draftJson, Json) : null,
            };
        }

        var seats = new List<object> { Seat("host", lobby.HostPlayerId, lobby.HostStatus, lobby.HostDraftJson) };
        if (lobby.GuestPlayerId is { } guestId) seats.Add(Seat("guest", guestId, lobby.GuestStatus, lobby.GuestDraftJson));

        return new
        {
            lobbyId = lobby.Id, code = lobby.Code, inviteUrl = InviteUrl(lobby.Code), @public = lobby.IsPublic,
            status = lobby.Status, hostPlayerId = lobby.HostPlayerId, guestPlayerId = lobby.GuestPlayerId,
            createdAt = lobby.CreatedAt, updatedAt = lobby.UpdatedAt,
            expiresAt = lobby.Status is LobbyStates.Waiting or LobbyStates.Full ? lobby.LastActivityAt + IdleTimeout : (DateTime?)null,
            seats,
            match = lobby.MatchId == null ? null : new
            {
                matchId = lobby.MatchId, seed = lobby.Seed, matchServer = lobby.MatchServer,
                // no match server configured: the host's client hosts the match
                hostMode = string.IsNullOrEmpty(lobby.MatchServer) ? "client_host" : "dedicated",
                hostPlayerId = lobby.HostPlayerId,
            },
        };
    }
}
