using System.Security.Cryptography;
using System.Text;
using RoguePirates.Api.Hackathon;
using RoguePirates.Api.Playtest;

namespace RoguePirates.Api.Endpoints;

/// <summary>Hackathon (devnet): 1v1 SOL wagers, weekly leaderboard rewards and the daily race.</summary>
public static class WagerEndpoints
{
    public static void MapWagerApi(this WebApplication app)
    {
        // ---- 1v1 wagers (the stake can also be set at POST /api/lobbies {"stakeLamports"}) ----
        app.MapPost("/api/wagers/{lobbyId:guid}", async (HttpContext ctx, Guid lobbyId, WagerStakeRequest req, PlayerAuth auth, WagerService wagers) =>
            Results.Ok(await wagers.SetStakeAsync(await auth.RequireAsync(ctx), lobbyId, req.StakeLamports)));

        app.MapPost("/api/wagers/{lobbyId:guid}/deposit", async (HttpContext ctx, Guid lobbyId, WagerDepositRequest req, PlayerAuth auth, WagerService wagers) =>
            Results.Ok(await wagers.DepositAsync(await auth.RequireAsync(ctx), lobbyId, req.Signature)));

        app.MapGet("/api/wagers/mine", async (HttpContext ctx, PlayerAuth auth, WagerService wagers) =>
            Results.Ok(await wagers.MineAsync(await auth.RequireAsync(ctx))));

        app.MapGet("/api/wagers/{lobbyId:guid}", async (Guid lobbyId, WagerService wagers) => Results.Ok(await wagers.GetAsync(lobbyId)));

        // ---- weekly leaderboard rewards ----
        app.MapGet("/api/rewards/weekly", async (DateTime? weekStart, string? mapId, RewardService rewards) =>
            Results.Ok(await rewards.GetAsync(weekStart, mapId)));

        // ---- daily race ----
        app.MapGet("/api/race/daily", async (HttpContext ctx, PlayerAuth auth, DailyRaceService race) =>
            Results.Ok(await race.TodayAsync(await auth.TryGetAsync(ctx))));

        app.MapPost("/api/race/daily/entry", async (HttpContext ctx, DailyRaceEntryRequest req, PlayerAuth auth, DailyRaceService race) =>
            Results.Ok(await race.SubmitAsync(await auth.RequireAsync(ctx), req)))
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(RunLogRules.MaxRequestBodyBytes));

        app.MapGet("/api/race/daily/board", async (HttpContext ctx, string? date, int? limit, PlayerAuth auth, DailyRaceService race) =>
            Results.Ok(await race.BoardAsync(date, limit, await auth.TryGetAsync(ctx))));

        // ---- owner tools (X-Admin-Key) ----
        app.MapPost("/api/admin/rewards/weekly", async (HttpContext ctx, WeeklyRewardsRequest? req, ApiSettings settings, RewardService rewards) =>
        {
            RequireAdmin(ctx, settings);
            return Results.Ok(await rewards.PayWeekAsync(req?.WeekStart, req?.MapId, req?.DryRun ?? false));
        });

        app.MapPost("/api/admin/wagers/sweep", async (HttpContext ctx, ApiSettings settings, WagerService wagers) =>
        {
            RequireAdmin(ctx, settings);
            return Results.Ok(new { wagers = await wagers.SweepAsync() });
        });

        // owner-only: removes test wallets' data (runs, bests, race entries, raids, islands, closed listings) and,
        // optionally, one week's WeeklyRewards rows before a demo. dryRun defaults to true (counts only).
        app.MapPost("/api/admin/demo/reset", async (HttpContext ctx, DemoResetRequest? req, ApiSettings settings, DemoResetService reset) =>
        {
            RequireAdmin(ctx, settings);
            return Results.Ok(await reset.ResetAsync(req ?? new DemoResetRequest(null, null, null, null)));
        });
    }

    static void RequireAdmin(HttpContext ctx, ApiSettings settings)
    {
        if (settings.AdminKey == null) throw new ApiException(503, "admin_off", "ADMIN_KEY is not configured.");
        var a = Encoding.UTF8.GetBytes(ctx.Request.Headers["X-Admin-Key"].ToString());
        var b = Encoding.UTF8.GetBytes(settings.AdminKey);
        if (a.Length != b.Length || !CryptographicOperations.FixedTimeEquals(a, b))
            throw new ApiException(403, "not_admin", "Wrong or missing X-Admin-Key.");
    }
}
