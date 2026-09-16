var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<RoguePirates.Api.Services.RunLeaseService>();
builder.Services.AddSingleton<RoguePirates.Api.Services.LedgerService>();
builder.Services.AddSingleton<RoguePirates.Api.Services.CrewService>();
builder.Services.AddSingleton<RoguePirates.Api.Services.EventStandingService>();
builder.Services.AddSingleton<RoguePirates.Api.Services.PrizeGrantService>();
builder.Services.AddSingleton<RoguePirates.Api.Services.DeploymentService>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", schema = 1 }));

app.MapPost("/accounts/guest", (RoguePirates.Api.Services.RunLeaseService leases) =>
{
    var accountId = leases.CreateGuestAccount();
    return Results.Ok(new { accountId });
});

app.MapPost("/runs/lease", (RoguePirates.Api.Services.RunLeaseService leases, RunLeaseRequest req) =>
{
    var result = leases.AcquireLease(req.AccountId, req.IdempotencyKey);
    return result.Success ? Results.Ok(result) : Results.Conflict(result);
});

app.MapPost("/ledger/collect", (RoguePirates.Api.Services.LedgerService ledger, LedgerCollectRequest req) =>
{
    var ok = ledger.TryCollectProduction(req.AccountId, req.BuildingId, req.IdempotencyKey);
    return ok ? Results.Ok(new { collected = true }) : Results.Conflict(new { collected = false });
});

app.Run();

public sealed record RunLeaseRequest(string AccountId, string IdempotencyKey);
public sealed record LedgerCollectRequest(string AccountId, string BuildingId, string IdempotencyKey);
