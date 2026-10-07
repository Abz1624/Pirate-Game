using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RoguePirates.Api.Data;
using RoguePirates.Api.Endpoints;
using RoguePirates.Api.Playtest;

var builder = WebApplication.CreateBuilder(args);

// Railway sets PORT; default 8080 locally. (Ignored by the in-memory test server.)
var port = Environment.GetEnvironmentVariable("PORT");
builder.WebHost.UseUrls($"http://0.0.0.0:{(string.IsNullOrWhiteSpace(port) ? "8080" : port.Trim())}");

// Settings are read when first resolved, so test hosts can override configuration.
builder.Services.AddSingleton(sp => ApiSettings.From(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<IClock, SystemClock>();
// One context per provider (each has its own migrations, see server/MIGRATIONS.md); services ask for GameDb and get
// the one the settings pick. Only the active provider's context is ever resolved.
builder.Services.AddDbContext<PostgresGameDb>((sp, options) =>
    options.UseNpgsql(ApiSettings.PostgresConnectionString(sp.GetRequiredService<ApiSettings>().DatabaseUrl!)));
builder.Services.AddDbContext<SqliteGameDb>((sp, options) =>
    options.UseSqlite(new SqliteConnectionStringBuilder { DataSource = sp.GetRequiredService<ApiSettings>().SqlitePath, Pooling = true }.ToString()));
builder.Services.AddScoped<GameDb>(sp => sp.GetRequiredService<ApiSettings>().UsePostgres
    ? sp.GetRequiredService<PostgresGameDb>()
    : sp.GetRequiredService<SqliteGameDb>());
builder.Services.AddScoped<PlayerAuth>();
builder.Services.AddScoped<RunService>();
builder.Services.AddScoped<LobbyService>();
builder.Services.AddScoped<MatchService>();
builder.Services.AddSingleton<LobbyBroadcaster>();
builder.Services.AddSingleton<MatchRelay>();
builder.Services.AddHostedService<PlaytestSweeper>();
builder.Services.AddHostedService<ArenaTicker>();

builder.Services.AddCors();
builder.Services.AddOptions<CorsOptions>().Configure<IServiceProvider>((o, sp) =>
{
    var settings = sp.GetRequiredService<ApiSettings>();
    o.AddDefaultPolicy(p => p.SetIsOriginAllowed(settings.IsOriginAllowed).AllowAnyHeader().AllowAnyMethod());
});

// Per-IP fixed window on every write (POST/PUT/PATCH/DELETE); reads and WebSockets are not limited.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        if (HttpMethods.IsGet(ctx.Request.Method) || HttpMethods.IsHead(ctx.Request.Method) || HttpMethods.IsOptions(ctx.Request.Method))
            return RateLimitPartition.GetNoLimiter("read");
        var limit = ctx.RequestServices.GetRequiredService<ApiSettings>().RateLimitPerMinute;
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
        });
    });
    o.OnRejected = async (context, ct) =>
        await context.HttpContext.Response.WriteAsJsonAsync(new { error = "rate_limited", message = "Too many requests; slow down." }, ct);
});

// Behind Railway's proxy: take the client IP from the last X-Forwarded-For hop (the one the proxy added).
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
    o.ForwardLimit = 1;
});

// Existing V5 placeholders (in-memory; replaced in M10).
builder.Services.AddSingleton<RoguePirates.Api.Services.RunLeaseService>();
builder.Services.AddSingleton<RoguePirates.Api.Services.LedgerService>();
builder.Services.AddSingleton<RoguePirates.Api.Services.CrewService>();
builder.Services.AddSingleton<RoguePirates.Api.Services.EventStandingService>();
builder.Services.AddSingleton<RoguePirates.Api.Services.PrizeGrantService>();
builder.Services.AddSingleton<RoguePirates.Api.Services.DeploymentService>();

var app = builder.Build();

// Bring the schema up to date on startup with EF Core migrations: an empty database gets everything, a database the
// old EnsureCreated built is baselined first (tables kept), then any pending migration is applied. See MIGRATIONS.md.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GameDb>();
    var settings = scope.ServiceProvider.GetRequiredService<ApiSettings>();
    DatabaseMigrator.Migrate(db, app.Logger);
    if (!settings.UsePostgres) db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    app.Logger.LogInformation("Rogue Pirates API using {Db}", settings.DbKind);
}

app.UseForwardedHeaders();
app.UseCors();
app.UseRateLimiter();
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    catch (ApiException ex) when (!ctx.Response.HasStarted)
    {
        ctx.Response.StatusCode = ex.Status;
        await ctx.Response.WriteAsJsonAsync(new { error = ex.Code, message = ex.Message });
    }
});

app.MapPlaytestApi();

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

public partial class Program { }
