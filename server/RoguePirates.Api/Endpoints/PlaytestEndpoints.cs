using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using Microsoft.EntityFrameworkCore;
using RoguePirates.Api.Data;
using RoguePirates.Api.Playtest;

namespace RoguePirates.Api.Endpoints;

public static class PlaytestEndpoints
{
    public static void MapPlaytestApi(this WebApplication app)
    {
        app.MapGet("/health", async (GameDb db, ApiSettings settings) =>
        {
            var ok = await db.Database.CanConnectAsync();
            return ok
                ? Results.Ok(new { ok = true, db = settings.DbKind })
                : Results.Json(new { ok = false, db = settings.DbKind }, statusCode: 503);
        });

        // ---- profiles ----
        app.MapPost("/api/profiles", async (CreateProfileRequest req, GameDb db, IClock clock) =>
        {
            var name = NameRules.Validate(req.Name);
            var token = Tokens.NewToken();
            var player = new Player { Id = Guid.NewGuid(), Name = name, TokenHash = Tokens.Hash(token), CreatedAt = clock.UtcNow, Rating = Elo.Start };
            db.Players.Add(player);
            await db.SaveChangesAsync();
            return Results.Json(new { playerId = player.Id, token, name = player.Name, tag = PlayerTags.For(player.Id) }, statusCode: 201);
        });

        app.MapGet("/api/profiles/me", async (HttpContext ctx, PlayerAuth auth) => Results.Ok(ProfileView(await auth.RequireAsync(ctx))));

        app.MapPatch("/api/profiles/me", async (HttpContext ctx, RenameProfileRequest req, PlayerAuth auth, GameDb db) =>
        {
            var player = await auth.RequireAsync(ctx);
            player.Name = NameRules.Validate(req.Name);
            await db.SaveChangesAsync();
            return Results.Ok(ProfileView(player));
        });

        app.MapGet("/api/profiles/me/stats", async (HttpContext ctx, PlayerAuth auth, RunService runs) =>
            Results.Ok(await runs.StatsAsync(await auth.RequireAsync(ctx))));

        // ---- runs and weekly leaderboard ----
        // V5 M9a (review): the body is capped (413 above it); a log that fails the structural checks inside it is accepted.
        app.MapPost("/api/runs", async (HttpContext ctx, SubmitRunRequest req, PlayerAuth auth, RunService runs) =>
            Results.Ok(await runs.SubmitAsync(await auth.RequireAsync(ctx), req)))
            .WithMetadata(new RequestSizeLimitAttribute(RunLogRules.MaxRequestBodyBytes));

        app.MapGet("/api/leaderboard/weekly", async (HttpContext ctx, string? mapId, int? limit, PlayerAuth auth, RunService runs, IClock clock) =>
            Results.Ok(await runs.LeaderboardAsync(WeekMath.WeekStart(clock.UtcNow), mapId, limit, await auth.TryGetAsync(ctx))));

        app.MapGet("/api/leaderboard/weekly/previous", async (HttpContext ctx, string? mapId, int? limit, PlayerAuth auth, RunService runs, IClock clock) =>
            Results.Ok(await runs.LeaderboardAsync(WeekMath.WeekStart(clock.UtcNow).AddDays(-7), mapId, limit, await auth.TryGetAsync(ctx))));

        // ---- lobbies ----
        app.MapPost("/api/lobbies", async (HttpContext ctx, CreateLobbyRequest? req, PlayerAuth auth, LobbyService lobbies) =>
            Results.Json(await lobbies.CreateAsync(await auth.RequireAsync(ctx), req?.Public ?? false), statusCode: 201));

        app.MapGet("/api/lobbies/public", async (LobbyService lobbies) => Results.Ok(await lobbies.ListPublicAsync()));

        // V5 M9b (I9b.1 part 2a): a signed one-time join ticket for a server-run voyage on the Netcode match server
        // (contract: MatchTickets, plan/v5/phase-09b-netcode/README.md). 503 while RP_MATCH_TICKET_SECRET is unset.
        app.MapPost("/api/lobbies/voyage/ticket", async (HttpContext ctx, PlayerAuth auth, ApiSettings settings, IClock clock) =>
        {
            var player = await auth.RequireAsync(ctx);
            var key = MatchTickets.KeyFromSecret(settings.MatchTicketSecret);
            if (key == null)
                return Results.Json(new { error = "match_tickets_off", message = "The match server is not configured." }, statusCode: 503);
            var iat = new DateTimeOffset(DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc)).ToUnixTimeSeconds();
            var exp = iat + settings.MatchTicketLifetimeSeconds;
            var matchId = settings.VoyageMatchId ?? "v-" + MatchTickets.NewTicketId();
            var ticket = MatchTickets.Issue(key, new MatchTickets.Payload(1, MatchTickets.NewTicketId(), player.Id.ToString(), matchId,
                MatchTickets.VoyageMode, MatchTickets.Audience, iat, exp));
            ctx.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { ticket, matchServerUrl = settings.VoyageServerUrl, matchId, mode = MatchTickets.VoyageMode, expiresAt = exp });
        });

        app.MapPost("/api/lobbies/join", async (HttpContext ctx, JoinLobbyRequest req, PlayerAuth auth, LobbyService lobbies) =>
            Results.Ok(await lobbies.JoinAsync(await auth.RequireAsync(ctx), req.Code)));

        app.MapGet("/api/lobbies/{id:guid}", async (HttpContext ctx, Guid id, PlayerAuth auth, LobbyService lobbies) =>
            Results.Ok(await lobbies.GetAsync(id, await auth.RequireAsync(ctx))));

        app.MapPut("/api/lobbies/{id:guid}/status", async (HttpContext ctx, Guid id, LobbyStatusRequest req, PlayerAuth auth, LobbyService lobbies) =>
            Results.Ok(await lobbies.UpdateStatusAsync(id, await auth.RequireAsync(ctx), req)));

        // WebSocket: pushes the full lobby state (same JSON as GET /api/lobbies/{id}) on connect and on every change.
        // Browsers cannot set headers on WebSockets, so the token comes in the query string (kept out of request logs).
        // Practice bot in seat 1 (host only, lobby waiting); the host's client simulates the bot's ship.
        app.MapPost("/api/lobbies/{id:guid}/bot", async (HttpContext ctx, Guid id, PlayerAuth auth, LobbyService lobbies) =>
            Results.Ok(await lobbies.AddBotAsync(id, await auth.RequireAsync(ctx))));

        // Rematch after a refereed match (same as the WebSocket message {"t":"rematch"}).
        app.MapPost("/api/lobbies/{id:guid}/rematch", async (HttpContext ctx, Guid id, PlayerAuth auth, MatchRelay relay) =>
            Results.Ok(await relay.RequestRematchAsync(id, (await auth.RequireAsync(ctx)).Id)));

        app.Map("/ws/lobbies/{id:guid}", async (HttpContext ctx, Guid id, PlayerAuth auth, LobbyService lobbies, LobbyBroadcaster broadcaster, MatchRelay relay, ApiSettings settings) =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
                return Results.Json(new { error = "websocket_required", message = "Connect with a WebSocket." }, statusCode: 400);
            var origin = ctx.Request.Headers.Origin.ToString();
            if (!string.IsNullOrEmpty(origin) && !settings.IsOriginAllowed(origin))
                return Results.Json(new { error = "origin_not_allowed", message = "Origin not allowed." }, statusCode: 403);
            var player = await auth.FromTokenAsync(ctx.Request.Query["token"].ToString());
            if (player == null)
                return Results.Json(new { error = "unauthorized", message = "Missing or unknown token." }, statusCode: 401);
            await lobbies.EnsureSeatAsync(id, player);

            using var socket = await ctx.WebSockets.AcceptWebSocketAsync();
            var connection = await lobbies.AttachAsync(id, player, socket);
            try
            {
                var buffer = new byte[1024];
                var message = new MemoryStream();
                var oversized = false;
                while (socket.State == WebSocketState.Open)
                {
                    var msg = await socket.ReceiveAsync(buffer, ctx.RequestAborted);
                    if (msg.MessageType == WebSocketMessageType.Close)
                    {
                        if (socket.State == WebSocketState.CloseReceived)
                            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                        break;
                    }
                    if (message.Length + msg.Count > ArenaRules.MaxMessageBytes) oversized = true;
                    else message.Write(buffer, 0, msg.Count);
                    if (!msg.EndOfMessage) continue;

                    var text = System.Text.Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
                    message.SetLength(0);
                    if (oversized)
                    {
                        oversized = false; // dropped: over 4 KB
                        continue;
                    }
                    // match messages ({"t": ...}) go to the relay; anything else (e.g. "ping") asks for the lobby state again
                    if (await relay.HandleAsync(id, player.Id, text)) continue;
                    // fresh scope: this request's DbContext would hand back the entity it tracked at connect time
                    using var scope = ctx.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateScope();
                    var state = await scope.ServiceProvider.GetRequiredService<LobbyService>().GetAsync(id, player);
                    await broadcaster.SendToAsync(id, connection, LobbyService.SocketJson(state));
                }
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException) { }
            finally
            {
                broadcaster.Remove(id, connection);
            }
            return Results.Empty;
        });

        // ---- matches ----
        app.MapPost("/api/matches/{matchId:guid}/result", async (HttpContext ctx, Guid matchId, MatchResultRequest req, PlayerAuth auth, MatchService matches) =>
            Results.Ok(await matches.SubmitAsync(matchId, await auth.RequireAsync(ctx), req)));

        app.MapGet("/api/matches/{matchId:guid}", async (Guid matchId, MatchService matches) => Results.Ok(await matches.GetAsync(matchId)));
    }

    static object ProfileView(Player p) => new
    {
        playerId = p.Id, name = p.Name, tag = PlayerTags.For(p.Id), rating = p.Rating, wins = p.Wins, losses = p.Losses,
        createdAt = p.CreatedAt,
    };
}
