using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RoguePirates.Api.Data;
using RoguePirates.Api.Hackathon;
using RoguePirates.Api.Playtest;
using RoguePirates.Api.Solana;

namespace RoguePirates.Api.Endpoints;

/// <summary>Hackathon (devnet) endpoints: wallet sign-in, profile email, balances, the treasury, deposits, owner payouts.</summary>
public static class HackathonEndpoints
{
    /// <summary>One sign-in verification at a time (a nonce is used exactly once).</summary>
    static readonly SemaphoreSlim AuthLock = new(1, 1);

    public static void MapHackathonApi(this WebApplication app)
    {
        // ---- wallet sign-in ----
        app.MapPost("/api/auth/wallet/nonce", async (HttpContext ctx, WalletNonceRequest req, WalletAuthService auth) =>
            Results.Ok(await auth.IssueNonceAsync(req, ctx.Request.Headers.Origin.ToString())));

        app.MapPost("/api/auth/wallet/verify", async (HttpContext ctx, WalletVerifyRequest req, PlayerAuth players, WalletAuthService auth) =>
        {
            var current = await players.TryGetAsync(ctx);
            await AuthLock.WaitAsync();
            try
            {
                ctx.Response.Headers.CacheControl = "no-store";
                return Results.Ok(await auth.VerifyAsync(req, current));
            }
            finally { AuthLock.Release(); }
        });

        // V5 M9h: sign out everywhere (every wallet session; a guest profile gets a fresh token back)
        app.MapPost("/api/auth/sessions/revoke-all", async (HttpContext ctx, PlayerAuth players, WalletAuthService auth) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await auth.RevokeAllAsync(await players.RequireAsync(ctx)));
        });

        app.MapPut("/api/profile/email", async (HttpContext ctx, EmailRequest req, PlayerAuth players, WalletAuthService auth) =>
            Results.Ok(await auth.SetEmailAsync(await players.RequireAsync(ctx), req)));

        // ---- balances and the treasury ----
        app.MapGet("/api/wallet/balance", async (HttpContext ctx, string? wallet, PlayerAuth players, SolanaService sol) =>
        {
            var address = wallet;
            if (string.IsNullOrWhiteSpace(address))
            {
                var player = await players.RequireAsync(ctx);
                address = player.WalletAddress ?? throw new ApiException(400, "wallet_required", "This profile has no wallet; sign in with a wallet.");
            }
            var lamports = await sol.GetBalanceAsync(address.Trim());
            return Results.Ok(new { wallet = address.Trim(), lamports, sol = lamports / (double)SolanaService.LamportsPerSol, cluster = sol.Cluster });
        }).RequireRateLimiting(RpcReads.Policy);

        // V5 M9h: the balance is read from the chain at most every 30 s (TreasuryMonitor), and reports the low-float alarm
        app.MapGet("/api/treasury", async (SolanaService sol, TreasuryMonitor monitor) =>
        {
            if (!sol.TreasuryOn)
                return Results.Ok(new { enabled = false, address = (string?)null, lamports = (long?)null, sol = (double?)null, cluster = sol.Cluster,
                    floatLamports = (long?)null, low = (bool?)null, readAt = (DateTime?)null });
            var (lamports, readAt) = (await monitor.BalanceAsync())!.Value;
            return Results.Ok(new { enabled = true, address = sol.TreasuryAddress, lamports = (long?)lamports,
                sol = (double?)(lamports / (double)SolanaService.LamportsPerSol), cluster = sol.Cluster,
                floatLamports = (long?)monitor.FloatLamports, low = (bool?)monitor.IsLow(lamports), readAt = (DateTime?)readAt });
        });

        // ---- V5 M9h: the live-API switches as this server runs them (public, read-only; set on Railway) ----
        app.MapGet("/api/switches", (ApiSettings settings, IConfiguration config) => Results.Ok(new
        {
            wagersHumanEnabled = settings.WagersHumanEnabled,
            raidLootEnabled = settings.RaidLootEnabled,
            islandSaleCarriesRum = settings.IslandSaleCarriesRum,
            dailyRaceFixedSalt = settings.DailyRaceFixedSalt,
            weeklyRewardCheckedOnly = ApiSettings.Flag(config["WEEKLY_REWARD_CHECKED_ONLY"], true),
            weeklyRewardsAuto = ApiSettings.Flag(config["WEEKLY_REWARDS_AUTO"], true),
            botStakes = false,
            raidStakes = false,
            mintDailyBudget = settings.MintDailyBudget,
            sessionLifetimeDays = settings.SessionLifetimeDays,
            // V5 M9j: server-decided results
            relayHullEnforce = settings.RelayHullEnforce,
            relayHullMargin = settings.RelayHullMargin,
            raidPairLimitPerDay = settings.RaidPairLimitPerDay,
            raidRecordOnePerDay = settings.RaidRecordOnePerDay,
        }));

        // ---- deposits (generic; wagers, market and raids use the same check) ----
        app.MapPost("/api/deposits/verify", async (HttpContext ctx, DepositVerifyRequest req, PlayerAuth players, LedgerService ledger) =>
        {
            var player = await players.RequireAsync(ctx);
            // 13 Oct hotfix: wager, market and raid deposits are only recorded by their own endpoints (amount, memo, seat checks)
            var (deposit, already) = await ledger.VerifyClientDepositAsync(player, req.Signature, req.Purpose, req.RefId,
                req.MinLamports ?? 1, string.IsNullOrWhiteSpace(req.Memo) ? null : req.Memo.Trim());
            return Results.Ok(LedgerService.DepositView(deposit, already));
        });

        app.MapGet("/api/deposits/mine", async (HttpContext ctx, PlayerAuth players, GameDb db) =>
        {
            var player = await players.RequireAsync(ctx);
            var list = await db.SolanaDeposits.AsNoTracking().Where(d => d.PlayerId == player.Id)
                .OrderByDescending(d => d.Id).Take(50).ToListAsync();
            return Results.Ok(new { deposits = list.Select(d => LedgerService.DepositView(d, true)) });
        });

        app.MapGet("/api/payouts/mine", async (HttpContext ctx, PlayerAuth players, GameDb db, SolanaService sol) =>
        {
            var player = await players.RequireAsync(ctx);
            var list = await db.Payouts.AsNoTracking().Where(p => p.PlayerId == player.Id)
                .OrderByDescending(p => p.Id).Take(50).ToListAsync();
            return Results.Ok(new { payouts = list.Select(p => LedgerService.PayoutView(p, sol.Cluster)) });
        });

        // ---- owner tools (X-Admin-Key = ADMIN_KEY; 503 when unset) ----
        app.MapPost("/api/admin/payouts", async (HttpContext ctx, AdminPayoutRequest req, ApiSettings settings, GameDb db, LedgerService ledger, SolanaService sol) =>
        {
            RequireAdmin(ctx, settings);
            // 13 Oct hotfix: an Idempotency-Key header is required (the same key never pays twice) and the amount is capped
            var key = ctx.Request.Headers["Idempotency-Key"].ToString().Trim();
            if (key.Length is 0 or > 64)
                throw new ApiException(400, "idempotency_key_required", "Send an Idempotency-Key header (1-64 characters); the same key never pays twice.");
            if (req.Lamports > settings.AdminPayoutMaxLamports)
                throw new ApiException(400, "amount_too_high", $"An owner payout is at most {settings.AdminPayoutMaxLamports} lamports (ADMIN_PAYOUT_MAX_LAMPORTS).");
            var wallet = (req.Wallet ?? "").Trim();
            var reason = string.IsNullOrWhiteSpace(req.Reason) ? "admin" : req.Reason.Trim();
            if (reason.Length > 32) throw new ApiException(400, "invalid_reason", "reason is at most 32 characters.");
            var refId = "admin:" + key;
            if (refId.Length > 64) refId = refId[..64];
            var player = await db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.WalletAddress == wallet);
            var previous = await db.Payouts.AsNoTracking().Where(p => p.Reason == reason && p.RefId == refId && p.Status != Payout.Failed)
                .OrderByDescending(p => p.Id).FirstOrDefaultAsync();
            var payout = previous ?? await ledger.PayAsync(player?.Id, wallet, req.Lamports, reason, refId);
            return Results.Ok(LedgerService.PayoutView(payout, sol.Cluster));
        });

        // V5 M9h (C9h.2): the treasury's state for the owner: a fresh balance, the float and alarm, today's mints against
        // the budget, open and failed payouts, and the top-up rule
        app.MapGet("/api/admin/treasury", async (HttpContext ctx, ApiSettings settings, SolanaService sol, TreasuryMonitor monitor,
            IslandService islands, GameDb db) =>
        {
            RequireAdmin(ctx, settings);
            var balance = await monitor.BalanceAsync(fresh: true);
            var open = await db.Payouts.AsNoTracking().Where(p => p.Status != Payout.Sent)
                .GroupBy(p => p.Status).Select(g => new { status = g.Key, count = g.Count(), lamports = g.Sum(p => p.Lamports) }).ToListAsync();
            return Results.Ok(new
            {
                enabled = sol.TreasuryOn, address = sol.TreasuryAddress, cluster = sol.Cluster,
                lamports = balance?.lamports, sol = balance == null ? (double?)null : balance.Value.lamports / (double)SolanaService.LamportsPerSol,
                floatLamports = monitor.FloatLamports, low = balance == null ? (bool?)null : monitor.IsLow(balance.Value.lamports),
                lowSince = monitor.LowSince,
                mintsToday = await islands.MintsTodayAsync(), mintDailyBudget = settings.MintDailyBudget,
                pendingMints = await db.Islands.AsNoTracking().CountAsync(i => i.PendingMint != null),
                islandsNotMinted = await db.Islands.AsNoTracking().CountAsync(i => i.NftStatus != IslandService.NftMinted),
                payoutsNotSent = open,
                topUpRule = TreasuryMonitor.TopUpRule,
            });
        });

        // V5 M9h (C9h.2): every payout and Island NFT mint since a date (default 12 Oct 2026, the hackathon), with totals
        app.MapGet("/api/admin/report", async (HttpContext ctx, DateTime? since, ApiSettings settings, SolanaService sol, GameDb db) =>
        {
            RequireAdmin(ctx, settings);
            var from = DateTime.SpecifyKind((since ?? new DateTime(2026, 10, 12)).Date, DateTimeKind.Utc);
            var payouts = await db.Payouts.AsNoTracking().Where(p => p.CreatedAt >= from).OrderBy(p => p.Id).ToListAsync();
            var islandRows = await db.Islands.AsNoTracking()
                .Where(i => i.MintSig != null || i.NftMint != null || i.PendingMint != null || i.CreatedAt >= from)
                .Join(db.Players.AsNoTracking(), i => i.OwnerPlayerId, p => p.Id, (i, p) => new { i, wallet = p.WalletAddress })
                .ToListAsync();
            var mints = islandRows.Where(x => (x.i.MintSentAt ?? x.i.MintedAt ?? x.i.CreatedAt) >= from)
                .OrderBy(x => x.i.MintSentAt ?? x.i.CreatedAt)
                .Select(x => new
                {
                    islandId = x.i.Id, name = x.i.Name, ownerWallet = x.wallet, status = x.i.NftStatus, mint = x.i.NftMint,
                    pendingMint = x.i.PendingMint, signature = x.i.MintSig, error = x.i.MintError,
                    claimedAt = x.i.CreatedAt, sentAt = x.i.MintSentAt, mintedAt = x.i.MintedAt,
                    explorerUrl = x.i.MintSig == null ? null : sol.ExplorerTx(x.i.MintSig),
                }).ToList();
            var deposits = await db.SolanaDeposits.AsNoTracking().Where(d => d.CreatedAt >= from)
                .GroupBy(d => new { d.Purpose, d.Status })
                .Select(g => new { purpose = g.Key.Purpose, status = g.Key.Status, count = g.Count(), lamports = g.Sum(d => d.Lamports) }).ToListAsync();
            return Results.Ok(new
            {
                since = from, until = DateTime.UtcNow, cluster = sol.Cluster,
                payoutTotals = payouts.GroupBy(p => new { p.Reason, p.Status })
                    .Select(g => new { reason = g.Key.Reason, status = g.Key.Status, count = g.Count(), lamports = g.Sum(p => p.Lamports) })
                    .OrderBy(t => t.reason).ThenBy(t => t.status),
                payoutsSentLamports = payouts.Where(p => p.Status == Payout.Sent).Sum(p => p.Lamports),
                mintsMinted = mints.Count(m => m.status == IslandService.NftMinted),
                depositTotals = deposits.OrderBy(d => d.purpose).ThenBy(d => d.status),
                payouts = payouts.Select(p => new
                {
                    payoutId = p.Id, playerId = p.PlayerId, wallet = p.Wallet, lamports = p.Lamports, reason = p.Reason, refId = p.RefId,
                    status = p.Status, signature = p.Signature, error = p.Error, createdAt = p.CreatedAt, sentAt = p.SentAt,
                    explorerUrl = p.Signature == null ? null : sol.ExplorerTx(p.Signature),
                }),
                mints,
            });
        });

        app.MapPost("/api/admin/airdrop", async (HttpContext ctx, AdminAirdropRequest req, ApiSettings settings, SolanaService sol) =>
        {
            RequireAdmin(ctx, settings);
            var wallet = string.IsNullOrWhiteSpace(req.Wallet) ? sol.TreasuryAddress : req.Wallet.Trim();
            if (wallet == null || !SolanaService.IsValidAddress(wallet)) throw new ApiException(400, "invalid_wallet", "Not a Solana address.");
            var sig = await sol.RequestAirdropAsync(wallet, Math.Clamp(req.Lamports ?? SolanaService.LamportsPerSol, 1, 5 * SolanaService.LamportsPerSol));
            return Results.Ok(new { wallet, signature = sig });
        });
    }

    static void RequireAdmin(HttpContext ctx, ApiSettings settings)
    {
        if (settings.AdminKey == null) throw new ApiException(503, "admin_off", "ADMIN_KEY is not configured.");
        var given = ctx.Request.Headers["X-Admin-Key"].ToString();
        var a = Encoding.UTF8.GetBytes(given);
        var b = Encoding.UTF8.GetBytes(settings.AdminKey);
        if (a.Length != b.Length || !CryptographicOperations.FixedTimeEquals(a, b))
            throw new ApiException(403, "not_admin", "Wrong or missing X-Admin-Key.");
    }
}
