using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RoguePirates.Api.Data;
using RoguePirates.Api.Playtest;

namespace RoguePirates.Api.Solana;

public sealed record WalletNonceRequest(string? Wallet);
public sealed record WalletVerifyRequest(string? Wallet, string? Signature, string? Message, string? Name);
public sealed record EmailRequest(string? Email);
public sealed record DepositVerifyRequest(string? Signature, string? Purpose, string? RefId, long? MinLamports, string? Memo);
public sealed record AdminPayoutRequest(string? Wallet, long Lamports, string? Reason);
public sealed record AdminAirdropRequest(string? Wallet, long? Lamports);

/// <summary>
/// Hackathon: sign-in with Solana. <c>nonce</c> issues a one-time message (5 minutes); <c>verify</c> checks the wallet's
/// ed25519 signature over exactly that message, marks it used, creates or finds the player keyed by the wallet, and
/// issues a bearer session in the same shape as POST /api/profiles.
/// </summary>
public sealed partial class WalletAuthService
{
    public static readonly TimeSpan NonceLifetime = TimeSpan.FromMinutes(5);

    readonly GameDb _db;
    readonly IClock _clock;
    readonly ApiSettings _settings;

    public WalletAuthService(GameDb db, IClock clock, ApiSettings settings)
    {
        _db = db;
        _clock = clock;
        _settings = settings;
    }

    public static string BuildMessage(string domain, string wallet, string nonce, DateTime issuedAt, DateTime expiresAt, string cluster) =>
        $"{domain} wants you to sign in with your Solana account:\n" +
        $"{wallet}\n\n" +
        "Sign in to Rogue Pirates. This request will not trigger a blockchain transaction or cost any fees.\n\n" +
        $"URI: https://{domain}\n" +
        "Version: 1\n" +
        $"Chain ID: {cluster}\n" +
        $"Nonce: {nonce}\n" +
        $"Issued At: {issuedAt:yyyy-MM-ddTHH:mm:ss.fffZ}\n" +
        $"Expiration Time: {expiresAt:yyyy-MM-ddTHH:mm:ss.fffZ}";

    static string NewNonce()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        var chars = new char[16];
        for (var i = 0; i < 16; i++) chars[i] = alphabet[bytes[i] % alphabet.Length];
        return new string(chars);
    }

    public async Task<object> IssueNonceAsync(WalletNonceRequest req)
    {
        var wallet = (req.Wallet ?? "").Trim();
        if (!SolanaService.IsValidAddress(wallet)) throw new ApiException(400, "invalid_wallet", "Not a Solana wallet address.");
        var now = _clock.UtcNow;
        var nonce = NewNonce();
        var expires = now + NonceLifetime;
        var message = BuildMessage(_settings.SiwsDomain, wallet, nonce, now, expires, _settings.SolanaCluster);
        // tidy: drop this wallet's stale nonces
        var stale = await _db.WalletNonces.Where(n => n.Wallet == wallet && (n.ExpiresAt < now || n.UsedAt != null)).ToListAsync();
        _db.WalletNonces.RemoveRange(stale);
        _db.WalletNonces.Add(new WalletNonce { Nonce = nonce, Wallet = wallet, Message = message, IssuedAt = now, ExpiresAt = expires });
        await _db.SaveChangesAsync();
        return new { wallet, nonce, message, issuedAt = now, expiresAt = expires };
    }

    [GeneratedRegex(@"^Nonce: ([A-Za-z0-9]{8,32})$", RegexOptions.Multiline)]
    private static partial Regex NonceLine();

    /// <summary>Verifies the signed message; returns the session (and the player row). <paramref name="current"/> is the
    /// caller's existing guest profile, if a bearer token was sent: the wallet is attached to it instead of a new profile.</summary>
    public async Task<object> VerifyAsync(WalletVerifyRequest req, Player? current)
    {
        var wallet = (req.Wallet ?? "").Trim();
        var message = (req.Message ?? "").Replace("\r\n", "\n");
        var signature = (req.Signature ?? "").Trim();
        if (!SolanaService.IsValidAddress(wallet)) throw new ApiException(400, "invalid_wallet", "Not a Solana wallet address.");
        if (message.Length == 0 || signature.Length == 0)
            throw new ApiException(400, "invalid_request", "wallet, signature (base58) and message are required.");

        var match = NonceLine().Match(message);
        if (!match.Success) throw new ApiException(400, "invalid_message", "The message has no nonce; ask for a new one.");
        var nonce = await _db.WalletNonces.FirstOrDefaultAsync(n => n.Nonce == match.Groups[1].Value);
        var now = _clock.UtcNow;
        if (nonce == null || nonce.Wallet != wallet || nonce.Message != message)
            throw new ApiException(401, "unknown_nonce", "This sign-in message was not issued for this wallet; ask for a new one.");
        if (nonce.UsedAt != null) throw new ApiException(401, "nonce_used", "This sign-in message was already used; ask for a new one.");
        if (now > nonce.ExpiresAt) throw new ApiException(401, "nonce_expired", "This sign-in message expired; ask for a new one.");
        if (!SolanaService.VerifyMessageSignature(wallet, message, signature))
            throw new ApiException(401, "bad_signature", "The signature does not match the wallet and message.");
        nonce.UsedAt = now;

        var isNew = false;
        var player = await _db.Players.FirstOrDefaultAsync(p => p.WalletAddress == wallet);
        if (player == null && current != null && current.WalletAddress == null)
        {
            // link the wallet to the caller's guest profile (keeps their name, rating and runs)
            player = await _db.Players.FirstAsync(p => p.Id == current.Id);
            player.WalletAddress = wallet;
        }
        if (player == null)
        {
            isNew = true;
            player = new Player
            {
                Id = Guid.NewGuid(),
                Name = DefaultName(req.Name, wallet),
                // wallet players sign in with sessions; this column must stay unique, so it gets an unusable random hash
                TokenHash = Tokens.Hash("wallet:" + Tokens.NewToken()),
                CreatedAt = now,
                Rating = Elo.Start,
                WalletAddress = wallet,
            };
            _db.Players.Add(player);
        }
        var token = Tokens.NewToken();
        _db.PlayerSessions.Add(new PlayerSession { PlayerId = player.Id, TokenHash = Tokens.Hash(token), CreatedAt = now });
        await _db.SaveChangesAsync();
        return new
        {
            playerId = player.Id, token, name = player.Name, tag = PlayerTags.For(player.Id),
            wallet = player.WalletAddress, email = player.Email, isNew,
        };
    }

    static string DefaultName(string? requested, string wallet)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            try { return NameRules.Validate(requested); } catch (ApiException) { /* fall back to the default */ }
        }
        var name = "Captain " + wallet[..4];
        return NameRules.IsProfane(name) ? "Captain " + wallet[^4..] : name;
    }

    [GeneratedRegex(@"^[^@\s]{1,64}@[^@\s]+\.[^@\s]{2,}$")]
    private static partial Regex EmailShape();

    public async Task<object> SetEmailAsync(Player player, EmailRequest req)
    {
        var email = (req.Email ?? "").Trim();
        if (email.Length == 0) player.Email = null;
        else if (email.Length > 254 || !EmailShape().IsMatch(email))
            throw new ApiException(400, "invalid_email", "That does not look like an email address.");
        else player.Email = email.ToLowerInvariant();
        await _db.SaveChangesAsync();
        return new { playerId = player.Id, wallet = player.WalletAddress, email = player.Email, emailVerified = false };
    }
}
