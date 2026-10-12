using RoguePirates.Api.Hackathon;
using RoguePirates.Api.Playtest;

namespace RoguePirates.Api.Endpoints;

public sealed record ClaimIslandRequest(string? Name);
public sealed record BuildRequest(string? Kind);
public sealed record RaidStartRequest(Guid IslandId, long? StakeLamports);
/// <summary><c>RunLog</c> is accepted and ignored for now (no raid run checks in the demo).</summary>
public sealed record RaidResultRequest(bool Won, bool? Partial, int Seconds, System.Text.Json.JsonElement? RunLog = null);
public sealed record SignatureRequest(string? Signature);
public sealed record MarketListRequest(Guid IslandId, long PriceLamports, string? Signature);

/// <summary>Hackathon (devnet): islands, RUM, raids and the island NFT marketplace. See server/README.md.</summary>
public static class IslandEndpoints
{
    public static void MapIslandApi(this WebApplication app)
    {
        // ---- islands ----
        app.MapPost("/api/islands/claim", async (HttpContext ctx, ClaimIslandRequest req, PlayerAuth auth, IslandService islands) =>
            Results.Ok(await islands.ClaimAsync(await auth.RequireAsync(ctx), req.Name, islands.PublicBase(ctx))));

        app.MapGet("/api/islands/mine", async (HttpContext ctx, PlayerAuth auth, IslandService islands) =>
            Results.Ok(await islands.MineAsync(await auth.RequireAsync(ctx), islands.PublicBase(ctx))));

        app.MapGet("/api/islands", async (HttpContext ctx, PlayerAuth auth, IslandService islands) =>
            Results.Ok(await islands.WorldAsync((await auth.TryGetAsync(ctx))?.Id, islands.PublicBase(ctx))));

        app.MapGet("/api/islands/{id:guid}", async (HttpContext ctx, Guid id, PlayerAuth auth, IslandService islands) =>
            Results.Ok(await islands.GetAsync(id, (await auth.TryGetAsync(ctx))?.Id, islands.PublicBase(ctx))));

        app.MapGet("/api/islands/{id:guid}/metadata.json", async (HttpContext ctx, Guid id, IslandService islands) =>
            Results.Ok(await islands.MetadataAsync(id, islands.PublicBase(ctx))));

        app.MapPost("/api/islands/{id:guid}/mint", async (HttpContext ctx, Guid id, PlayerAuth auth, IslandService islands) =>
            Results.Ok(await islands.RetryMintAsync(await auth.RequireAsync(ctx), id, islands.PublicBase(ctx))));

        app.MapPost("/api/islands/{id:guid}/collect", async (HttpContext ctx, Guid id, PlayerAuth auth, IslandService islands) =>
            Results.Ok(await islands.CollectAsync(await auth.RequireAsync(ctx), id, islands.PublicBase(ctx))));

        app.MapPost("/api/islands/{id:guid}/build", async (HttpContext ctx, Guid id, BuildRequest req, PlayerAuth auth, IslandService islands) =>
            Results.Ok(await islands.BuildAsync(await auth.RequireAsync(ctx), id, req.Kind, islands.PublicBase(ctx))));

        app.MapGet("/static/islands/{slot:int}.png", (int slot) =>
            slot < 0 || slot >= IslandRules.Slots.Count
                ? Results.NotFound()
                : Results.File(IslandRules.IslandImagePng(slot), "image/png"));

        // ---- raids ----
        app.MapPost("/api/raids/start", async (HttpContext ctx, RaidStartRequest req, PlayerAuth auth, RaidService raids) =>
            Results.Ok(await raids.StartAsync(await auth.RequireAsync(ctx), req.IslandId, req.StakeLamports)));

        // Raid stakes are off (SOL wagers are only in 1v1): 410 raid_stakes_off.
        app.MapPost("/api/raids/{id:guid}/wager", async (HttpContext ctx, Guid id, PlayerAuth auth) =>
        {
            await auth.RequireAsync(ctx);
            return Results.Ok(await RaidService.FundWagerAsync());
        });

        app.MapPost("/api/raids/{id:guid}/result", async (HttpContext ctx, Guid id, RaidResultRequest req, PlayerAuth auth, RaidService raids) =>
            Results.Ok(await raids.ResultAsync(await auth.RequireAsync(ctx), id, req.Won, req.Partial ?? false, req.Seconds)));

        app.MapPost("/api/raids/{id:guid}/settle", async (HttpContext ctx, Guid id, PlayerAuth auth, RaidService raids) =>
            Results.Ok(await raids.SettleAsync(await auth.RequireAsync(ctx), id)));

        app.MapGet("/api/raids/mine", async (HttpContext ctx, PlayerAuth auth, RaidService raids) =>
            Results.Ok(await raids.MineAsync(await auth.RequireAsync(ctx))));

        // ---- marketplace ----
        app.MapGet("/api/market", async (MarketService market) => Results.Ok(await market.ActiveAsync()));

        app.MapGet("/api/market/mine", async (HttpContext ctx, PlayerAuth auth, MarketService market) =>
            Results.Ok(await market.MineAsync(await auth.RequireAsync(ctx))));

        app.MapGet("/api/market/{id:guid}", async (Guid id, MarketService market) => Results.Ok(await market.GetAsync(id)));

        app.MapPost("/api/market/list", async (HttpContext ctx, MarketListRequest req, PlayerAuth auth, MarketService market) =>
            Results.Ok(await market.ListAsync(await auth.RequireAsync(ctx), req.IslandId, req.PriceLamports, req.Signature)));

        app.MapPost("/api/market/{id:guid}/buy", async (HttpContext ctx, Guid id, SignatureRequest req, PlayerAuth auth, MarketService market) =>
            Results.Ok(await market.BuyAsync(await auth.RequireAsync(ctx), id, req.Signature)));

        app.MapPost("/api/market/{id:guid}/complete", async (HttpContext ctx, Guid id, PlayerAuth auth, MarketService market) =>
            Results.Ok(await market.CompleteAsync(await auth.RequireAsync(ctx), id)));

        app.MapPost("/api/market/{id:guid}/cancel", async (HttpContext ctx, Guid id, PlayerAuth auth, MarketService market) =>
            Results.Ok(await market.CancelAsync(await auth.RequireAsync(ctx), id)));
    }
}
