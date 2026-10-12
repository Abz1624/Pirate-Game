using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RoguePirates.Api.Data;
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
        app.MapPost("/api/auth/wallet/nonce", async (WalletNonceRequest req, WalletAuthService auth) =>
            Results.Ok(await auth.IssueNonceAsync(req)));

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
        });

        app.MapGet("/api/treasury", async (SolanaService sol) =>
        {
            if (!sol.TreasuryOn)
                return Results.Ok(new { enabled = false, address = (string?)null, lamports = (long?)null, sol = (double?)null, cluster = sol.Cluster });
            var lamports = await sol.GetBalanceAsync(sol.TreasuryAddress!);
            return Results.Ok(new { enabled = true, address = sol.TreasuryAddress, lamports = (long?)lamports,
                sol = (double?)(lamports / (double)SolanaService.LamportsPerSol), cluster = sol.Cluster });
        });

        // ---- deposits (generic; wagers, market and raids use the same check) ----
        app.MapPost("/api/deposits/verify", async (HttpContext ctx, DepositVerifyRequest req, PlayerAuth players, LedgerService ledger) =>
        {
            var player = await players.RequireAsync(ctx);
            var (deposit, already) = await ledger.VerifyDepositAsync(player, req.Signature, req.Purpose, req.RefId,
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
            var wallet = (req.Wallet ?? "").Trim();
            var player = await db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.WalletAddress == wallet);
            var payout = await ledger.PayAsync(player?.Id, wallet, req.Lamports, string.IsNullOrWhiteSpace(req.Reason) ? "admin" : req.Reason.Trim());
            return Results.Ok(LedgerService.PayoutView(payout, sol.Cluster));
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
