using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace RoguePirates.V5.Net
{
    /// <summary>
    /// V5 M9b (I9b.1): the signed one-time join ticket. The API lobby issues it (part 2: an endpoint on the API), the
    /// match server checks it in the connection approval. Format (the contract both sides keep):
    /// <code>
    /// ticket  = "v1." + b64url(payloadJson) + "." + b64url(HMAC-SHA256(key, "v1." + b64url(payloadJson)))
    /// key     = UTF-8 bytes of the shared secret in the environment variable RP_MATCH_TICKET_SECRET (32 characters or more)
    /// payload = {"v":1,"tid":"&lt;unique id&gt;","sub":"&lt;player id&gt;","mid":"&lt;match id&gt;","mode":"voyage","aud":"rp-match","iat":&lt;unix s&gt;,"exp":&lt;unix s&gt;}
    /// b64url  = base64 with '-' and '_', no '=' padding
    /// </code>
    /// The server accepts a ticket once (its tid is remembered until it expires), only before exp (5 s clock skew), with a
    /// lifetime (exp - iat) of at most 120 s, for its own match id when it has one.
    /// </summary>
    [Serializable]
    public sealed class JoinTicketPayload
    {
        public int v = 1;
        public string tid;
        public string sub;
        public string mid;
        public string mode = "voyage";
        public string aud = JoinTicket.Audience;
        public long iat;
        public long exp;
    }

    public enum JoinTicketError
    {
        None,
        NoSecret,
        Malformed,
        BadSignature,
        WrongVersion,
        WrongAudience,
        NotYetValid,
        Expired,
        LifetimeTooLong,
        WrongMatch,
        Replayed,
    }

    public static class JoinTicket
    {
        public const string Prefix = "v1";
        public const string SecretEnvironmentVariable = "RP_MATCH_TICKET_SECRET";
        public const string Audience = "rp-match";
        public const int MaxLifetimeSeconds = 120;
        public const int ClockSkewSeconds = 5;
        public const int MinSecretLength = 32;

        public static long UnixNow() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>The key from a secret (its UTF-8 bytes); null when the secret is too short to sign with.</summary>
        public static byte[] KeyFromSecret(string secret) =>
            string.IsNullOrEmpty(secret) || secret.Length < MinSecretLength ? null : Encoding.UTF8.GetBytes(secret);

        /// <summary>The key from <see cref="SecretEnvironmentVariable"/> (never logged); null when unset or too short.</summary>
        public static byte[] KeyFromEnvironment() => KeyFromSecret(Environment.GetEnvironmentVariable(SecretEnvironmentVariable));

        public static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public static byte[] FromBase64Url(string text)
        {
            var s = text.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
                case 1: throw new FormatException("bad base64url length");
            }
            return Convert.FromBase64String(s);
        }

        public static byte[] Sign(byte[] key, string signingInput)
        {
            using (var hmac = new HMACSHA256(key)) return hmac.ComputeHash(Encoding.ASCII.GetBytes(signingInput));
        }

        /// <summary>Constant-time comparison (the signature check leaks no prefix length).</summary>
        public static bool SameBytes(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            var diff = 0;
            for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }

    /// <summary>The issuing side of the contract (the API lobby's job in part 2; the in-process tests and the spike issue
    /// their own with a per-session key).</summary>
    public sealed class JoinTicketIssuer
    {
        readonly byte[] _key;

        public JoinTicketIssuer(byte[] key)
        {
            if (key == null || key.Length < JoinTicket.MinSecretLength) throw new ArgumentException("a key of 32 bytes or more", nameof(key));
            _key = key;
        }

        public string Issue(string playerId, string matchId, long nowUnix, int lifetimeSeconds = 60, string mode = "voyage", string ticketId = null) =>
            Issue(new JoinTicketPayload
            {
                tid = ticketId ?? Guid.NewGuid().ToString("N"),
                sub = playerId,
                mid = matchId,
                mode = mode,
                iat = nowUnix,
                exp = nowUnix + lifetimeSeconds,
            });

        public string Issue(JoinTicketPayload payload)
        {
            var body = JoinTicket.Base64Url(Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload)));
            var signingInput = JoinTicket.Prefix + "." + body;
            return signingInput + "." + JoinTicket.Base64Url(JoinTicket.Sign(_key, signingInput));
        }
    }

    /// <summary>The match server's check: signature, version, audience, time window, match, and one use only.</summary>
    public sealed class JoinTicketVerifier
    {
        readonly byte[] _key;
        readonly Func<long> _clock;
        readonly Dictionary<string, long> _used = new Dictionary<string, long>();

        /// <summary>When set, a ticket for another match is refused.</summary>
        public string ExpectedMatchId { get; set; }

        /// <summary>Used ticket ids live in this process only: a server restart forgets them, so a ticket could be replayed
        /// on a restarted server inside its lifetime (at most 125 s). The match id in the ticket ties it to one server
        /// session (<see cref="ExpectedMatchId"/>, from this environment variable on a server).</summary>
        public const string MatchIdEnvironmentVariable = "RP_MATCH_ID";

        public JoinTicketVerifier(byte[] key, Func<long> clock = null)
        {
            _key = key;
            _clock = clock ?? JoinTicket.UnixNow;
        }

        public static JoinTicketVerifier FromEnvironment(Func<long> clock = null) => new JoinTicketVerifier(JoinTicket.KeyFromEnvironment(), clock);

        public bool HasKey => _key != null && _key.Length >= JoinTicket.MinSecretLength;
        public int UsedCount => _used.Count;

        public JoinTicketError Verify(string ticket, out JoinTicketPayload payload) => Verify(ticket, out payload, true);

        /// <summary>With <paramref name="consume"/> false the ticket passes every check but stays unused, so the caller can
        /// still refuse the join for its own reasons (a voyage already taken) without burning it; then <see cref="Consume"/>.</summary>
        public JoinTicketError Verify(string ticket, out JoinTicketPayload payload, bool consume)
        {
            payload = null;
            if (!HasKey) return JoinTicketError.NoSecret;
            if (string.IsNullOrEmpty(ticket) || ticket.Length > 2048) return JoinTicketError.Malformed;
            var parts = ticket.Split('.');
            if (parts.Length != 3) return JoinTicketError.Malformed;
            if (parts[0] != JoinTicket.Prefix) return JoinTicketError.WrongVersion;
            byte[] signature, body;
            try
            {
                signature = JoinTicket.FromBase64Url(parts[2]);
                body = JoinTicket.FromBase64Url(parts[1]);
            }
            catch (FormatException) { return JoinTicketError.Malformed; }
            // The signature first: nothing in an unsigned body is read.
            if (!JoinTicket.SameBytes(signature, JoinTicket.Sign(_key, parts[0] + "." + parts[1]))) return JoinTicketError.BadSignature;
            try { payload = JsonUtility.FromJson<JoinTicketPayload>(Encoding.UTF8.GetString(body)); }
            catch (ArgumentException) { return JoinTicketError.Malformed; }
            if (payload == null || string.IsNullOrEmpty(payload.tid) || string.IsNullOrEmpty(payload.sub)) return JoinTicketError.Malformed;
            if (payload.v != 1) return JoinTicketError.WrongVersion;
            if (payload.aud != JoinTicket.Audience) return JoinTicketError.WrongAudience;
            var now = _clock();
            if (payload.iat > now + JoinTicket.ClockSkewSeconds) return JoinTicketError.NotYetValid;
            if (now > payload.exp + JoinTicket.ClockSkewSeconds) return JoinTicketError.Expired;
            if (payload.exp - payload.iat > JoinTicket.MaxLifetimeSeconds) return JoinTicketError.LifetimeTooLong;
            if (!string.IsNullOrEmpty(ExpectedMatchId) && payload.mid != ExpectedMatchId) return JoinTicketError.WrongMatch;
            Forget(now);
            if (_used.ContainsKey(payload.tid)) return JoinTicketError.Replayed;
            if (consume) _used[payload.tid] = payload.exp;
            return JoinTicketError.None;
        }

        /// <summary>Marks a ticket checked with <c>consume: false</c> as used.</summary>
        public void Consume(JoinTicketPayload payload)
        {
            if (payload != null && !string.IsNullOrEmpty(payload.tid)) _used[payload.tid] = payload.exp;
        }

        /// <summary>Used ticket ids are kept only while their ticket could still pass the time check.</summary>
        void Forget(long now)
        {
            if (_used.Count < 64) return;
            var stale = new List<string>();
            foreach (var pair in _used)
                if (now > pair.Value + JoinTicket.ClockSkewSeconds) stale.Add(pair.Key);
            foreach (var id in stale) _used.Remove(id);
        }
    }
}
