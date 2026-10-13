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
        BuildMessage(domain, "https://" + domain, wallet, nonce, issuedAt, expiresAt, cluster);

    public static string BuildMessage(string domain, string uri, string wallet, string nonce, DateTime issuedAt, DateTime expiresAt, string cluster) =>
        $"{domain} wants you to sign in with your Solana account:\n" +
        $"{wallet}\n\n" +
        "Sign in to Rogue Pirates. This request will not trigger a blockchain transaction or cost any fees.\n\n" +
        $"URI: {uri}\n" +
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

    /// <summary>The sign-in message names the page's own origin when the browser sent an allowed Origin header: browser
    /// wallets (Phantom) refuse a sign-in message whose domain is not the requesting page's. Without one (Android MWA, the
    /// Editor) it names SIWS_DOMAIN.</summary>
    public (string Domain, string Uri) DomainFor(string? origin)
    {
        if (_settings.IsOriginAllowed(origin) && Uri.TryCreate(origin!.TrimEnd('/'), UriKind.Absolute, out var u)
            && (u.Scheme == "https" || u.Scheme == "http"))
            return (u.Authority, u.GetLeftPart(UriPartial.Authority));
        return (_settings.SiwsDomain, "https://" + _settings.SiwsDomain);
    }

    public async Task<object> IssueNonceAsync(WalletNonceRequest req, string? origin = null)
    {
        var wallet = (req.Wallet ?? "").Trim();
        if (!SolanaService.IsValidAddress(wallet)) throw new ApiException(400, "invalid_wallet", "Not a Solana wallet address.");
        var now = _clock.UtcNow;
        var nonce = NewNonce();
        var expires = now + NonceLifetime;
        var (domain, uri) = DomainFor(origin);
        var message = BuildMessage(domain, uri, wallet, nonce, now, expires, _settings.SolanaCluster);
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
            // V5 M9h: the guest token is rotated away; from now on the profile is reached through wallet sessions only
            player.TokenHash = UnusableTokenHash();
        }
        if (player == null)
        {
            isNew = true;
            player = new Player
            {
                Id = Guid.NewGuid(),
                Name = DefaultName(req.Name, wallet),
                // wallet players sign in with sessions; this column must stay unique, so it gets an unusable random hash
                TokenHash = UnusableTokenHash(),
                CreatedAt = now,
                Rating = Elo.Start,
                WalletAddress = wallet,
            };
            _db.Players.Add(player);
        }
        var token = Tokens.NewToken();
        _db.PlayerSessions.Add(NewSession(player.Id, token, now));
        await _db.SaveChangesAsync();
        return new
        {
            playerId = player.Id, token, name = player.Name, tag = PlayerTags.For(player.Id),
            wallet = player.WalletAddress, email = player.Email, isNew,
        };
    }

    /// <summary>A unique Player.TokenHash no bearer token hashes to (wallet profiles sign in with sessions).</summary>
    public static string UnusableTokenHash() => Tokens.Hash("wallet:" + Tokens.NewToken());

    PlayerSession NewSession(Guid playerId, string token, DateTime now) => new()
    {
        PlayerId = playerId, TokenHash = Tokens.Hash(token), CreatedAt = now, LastUsedAt = now,
        ExpiresAt = now + TimeSpan.FromDays(_settings.SessionLifetimeDays),
    };

    /// <summary>
    /// V5 M9h: POST /api/auth/sessions/revoke-all ("sign out everywhere"). Every wallet session of the profile is deleted,
    /// the caller's included, and the profile's own token is rotated. A wallet profile signs in again with its wallet. A
    /// guest profile (no wallet) cannot, so it gets its new token back in the answer (shown once, like POST /api/profiles).
    /// </summary>
    public async Task<object> RevokeAllAsync(Player player)
    {
        var sessions = await _db.PlayerSessions.Where(s => s.PlayerId == player.Id).ExecuteDeleteAsync();
        var tracked = await _db.Players.FirstAsync(p => p.Id == player.Id);
        string? token = null;
        if (tracked.WalletAddress == null)
        {
            token = Tokens.NewToken();
            tracked.TokenHash = Tokens.Hash(token);
        }
        else tracked.TokenHash = UnusableTokenHash();
        await _db.SaveChangesAsync();
        return new { playerId = player.Id, revokedSessions = sessions, guestTokenRotated = token != null, token, signedOut = token == null };
    }

    /// <summary>Deletes sessions that expired more than a day ago (the sweep runs it). Returns how many.</summary>
    public static Task<int> PurgeExpiredAsync(GameDb db, DateTime now, TimeSpan lifetime)
    {
        var cutoff = now - TimeSpan.FromDays(1);
        var legacyCutoff = cutoff - lifetime;
        return db.PlayerSessions.Where(s => (s.ExpiresAt != null && s.ExpiresAt < cutoff) || (s.ExpiresAt == null && s.CreatedAt < legacyCutoff))
            .ExecuteDeleteAsync();
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
