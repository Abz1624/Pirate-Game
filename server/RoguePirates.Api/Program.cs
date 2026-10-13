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
// V5 M9a (I9a.3): the run checks (shadow mode): the embedded rules, the checker, and the startup re-scoring pass.
builder.Services.AddSingleton(_ => RoguePirates.Api.Plausibility.PlausibilityRuleSet.LoadEmbedded());
builder.Services.AddSingleton<RoguePirates.Api.Plausibility.PlausibilityService>();
builder.Services.AddHostedService<RoguePirates.Api.Plausibility.PlausibilityRescorer>();
// V5 M9a (review): run logs older than RUNLOG_RETENTION_DAYS (default 56) are deleted unless the run was flagged or its log rejected.
builder.Services.AddHostedService<RunLogRetention>();
builder.Services.AddScoped<LobbyService>();
builder.Services.AddScoped<MatchService>();
builder.Services.AddSingleton<LobbyBroadcaster>();
builder.Services.AddSingleton<MatchRelay>();
builder.Services.AddHostedService<PlaytestSweeper>();
builder.Services.AddHostedService<ArenaTicker>();
// Hackathon (devnet): Solana RPC (replaceable in tests), the Solana service (treasury from TREASURY_SECRET_KEY), wallet sign-in, deposits and payouts.
builder.Services.AddHttpClient("solana", c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddSingleton<RoguePirates.Api.Solana.ISolanaRpc>(sp => new RoguePirates.Api.Solana.HttpSolanaRpc(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("solana"), sp.GetRequiredService<ApiSettings>().SolanaRpcUrl));
builder.Services.AddSingleton<RoguePirates.Api.Solana.SolanaService>();
builder.Services.AddScoped<RoguePirates.Api.Solana.WalletAuthService>();
builder.Services.AddScoped<RoguePirates.Api.Solana.LedgerService>();
// Hackathon: 1v1 SOL wagers (+ sweeper), weekly leaderboard rewards (+ job), the daily race.
builder.Services.AddScoped<RoguePirates.Api.Hackathon.WagerService>();
builder.Services.AddSingleton<RoguePirates.Api.Hackathon.BackgroundSettlements>();
builder.Services.AddHostedService<RoguePirates.Api.Hackathon.WagerSweeper>();
builder.Services.AddScoped<RoguePirates.Api.Hackathon.RewardService>();
builder.Services.AddHostedService<RoguePirates.Api.Hackathon.WeeklyRewardJob>();
builder.Services.AddScoped<RoguePirates.Api.Hackathon.DailyRaceService>();
// Hackathon: islands (RUM, buildings, Island NFTs), raids and the island NFT marketplace (Endpoints/IslandEndpoints.cs).
builder.Services.AddScoped<RoguePirates.Api.Hackathon.IslandService>();
builder.Services.AddScoped<RoguePirates.Api.Hackathon.RaidService>();
builder.Services.AddScoped<RoguePirates.Api.Hackathon.DefenceService>();
builder.Services.AddScoped<RoguePirates.Api.Hackathon.MarketService>();
builder.Services.AddScoped<RoguePirates.Api.Hackathon.DemoResetService>();

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

// Existing V5 placeholders (in-memory; replaced in M10). The unauthenticated stub endpoints (/accounts/guest, /runs/lease,
// /ledger/collect) and their services were retired in M9a.
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
    catch (RoguePirates.Api.Hackathon.DefenceInvalidException ex) when (!ctx.Response.HasStarted)
    {
        ctx.Response.StatusCode = 400;
        await ctx.Response.WriteAsJsonAsync(new
        {
            error = "invalid_defence", message = ex.Message,
            errors = ex.Errors.Select(e => new { minute = e.Minute, field = e.Field, code = e.Code, message = e.Message }),
        });
    }
});

// V5 M9a (review): an endpoint's body-size limit (RequestSizeLimitAttribute metadata; POST /api/runs) answered as a 413
// with a JSON error when the declared length is over it. Kestrel also enforces the limit while reading (chunked bodies);
// in-memory test servers do not, so the declared-length check here is what the tests see.
app.Use(async (ctx, next) =>
{
    if (ctx.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.Http.Metadata.IRequestSizeLimitMetadata>()?.MaxRequestBodySize is long max)
    {
        if (ctx.Request.ContentLength > max)
        {
            ctx.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await ctx.Response.WriteAsJsonAsync(new { error = "body_too_large", message = $"The request body is larger than {max} bytes." });
            return;
        }
        var size = ctx.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
        if (size is { IsReadOnly: false }) size.MaxRequestBodySize = max;
    }
    await next();
});

app.MapPlaytestApi();
app.MapHackathonApi();
app.MapWagerApi();
app.MapIslandApi();

app.Run();

public partial class Program { }
