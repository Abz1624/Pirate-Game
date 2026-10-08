using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RoguePirates.Api.Playtest;

/// <summary>
/// V5 M9b (I9b.1 part 2a): the signed one-time join ticket for the Netcode match server, the API's half of the contract
/// in <c>plan/v5/phase-09b-netcode/README.md</c> (Unity's <c>JoinTicketVerifier</c> checks it):
/// <code>
/// ticket  = "v1." + b64url(payloadJson) + "." + b64url(HMAC-SHA256(key, "v1." + b64url(payloadJson)))
/// key     = UTF-8 bytes of RP_MATCH_TICKET_SECRET (32 characters or more), the same secret on the API and the match server
/// payload = {"v":1,"tid":"..","sub":"&lt;player id&gt;","mid":"&lt;match id&gt;","mode":"voyage","aud":"rp-match","iat":&lt;unix s&gt;,"exp":&lt;unix s&gt;}
/// b64url  = base64 with - and _, no = padding
/// </code>
/// Every ticket has a fresh random <c>tid</c> (the match server takes each one once) and a lifetime of at most
/// <see cref="MaxLifetimeSeconds"/>. The secret is never logged or returned.
/// </summary>
public static class MatchTickets
{
    public const string Prefix = "v1";
    public const string Audience = "rp-match";
    public const string VoyageMode = "voyage";
    public const int MaxLifetimeSeconds = 120;
    public const int MinSecretLength = 32;

    /// <summary>The payload, its properties in the contract's order (the JSON field order is free for the verifier, but a
    /// fixed order makes the contract vector reproducible byte for byte).</summary>
    public sealed record Payload(
        [property: System.Text.Json.Serialization.JsonPropertyName("v")] int V,
        [property: System.Text.Json.Serialization.JsonPropertyName("tid")] string Tid,
        [property: System.Text.Json.Serialization.JsonPropertyName("sub")] string Sub,
        [property: System.Text.Json.Serialization.JsonPropertyName("mid")] string Mid,
        [property: System.Text.Json.Serialization.JsonPropertyName("mode")] string Mode,
        [property: System.Text.Json.Serialization.JsonPropertyName("aud")] string Aud,
        [property: System.Text.Json.Serialization.JsonPropertyName("iat")] long Iat,
        [property: System.Text.Json.Serialization.JsonPropertyName("exp")] long Exp);

    /// <summary>The key from the secret (its UTF-8 bytes); null when unset or under 32 characters (no tickets then).</summary>
    public static byte[]? KeyFromSecret(string? secret) =>
        string.IsNullOrEmpty(secret) || secret.Length < MinSecretLength ? null : Encoding.UTF8.GetBytes(secret);

    public static string NewTicketId() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    public static string PayloadJson(Payload payload) => JsonSerializer.Serialize(payload);

    /// <summary>Signs <paramref name="payloadJson"/> exactly as given.</summary>
    public static string SignJson(byte[] key, string payloadJson)
    {
        var signingInput = Prefix + "." + Base64Url(Encoding.UTF8.GetBytes(payloadJson));
        return signingInput + "." + Base64Url(HMACSHA256.HashData(key, Encoding.ASCII.GetBytes(signingInput)));
    }

    public static string Issue(byte[] key, Payload payload)
    {
        if (payload.Exp - payload.Iat > MaxLifetimeSeconds || payload.Exp <= payload.Iat)
            throw new ArgumentOutOfRangeException(nameof(payload), $"a ticket lives between 1 and {MaxLifetimeSeconds} s");
        return SignJson(key, PayloadJson(payload));
    }

    public static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] FromBase64Url(string text)
    {
        var s = text.Replace('-', '+').Replace('_', '/');
        s += (s.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
        return Convert.FromBase64String(s);
    }
}
