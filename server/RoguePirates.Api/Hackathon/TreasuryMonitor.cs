using RoguePirates.Api.Playtest;
using RoguePirates.Api.Solana;

namespace RoguePirates.Api.Hackathon;

/// <summary>
/// V5 M9h (C9h.2, treasury bounds until M11 splits the keys): the treasury balance, read from the chain at most every
/// <see cref="CacheFor"/> (GET /api/treasury is public, so clients never cause more than one RPC call per 30 s), and the
/// low-float alarm: below TREASURY_MIN_LAMPORTS the treasury reports <c>low</c> and an error is logged when it goes low and
/// once an hour while it stays low (Railway's logs; the owner tops it up by the rule in server/README.md).
/// </summary>
public sealed class TreasuryMonitor
{
    public static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan RealarmEvery = TimeSpan.FromHours(1);

    public const string TopUpRule =
        "Keep the treasury at or above TREASURY_MIN_LAMPORTS (default 2 SOL) on devnet: when it reports low, the owner tops it " +
        "up to at least twice the float (POST /api/admin/airdrop on devnet, or a transfer from the owner's wallet) the same day.";

    readonly SolanaService _sol;
    readonly ApiSettings _settings;
    readonly IClock _clock;
    readonly ILogger<TreasuryMonitor> _log;
    readonly SemaphoreSlim _gate = new(1, 1);
    (long lamports, DateTime at)? _cached;
    DateTime? _lastAlarm;

    public TreasuryMonitor(SolanaService sol, ApiSettings settings, IClock clock, ILogger<TreasuryMonitor> log)
    {
        _sol = sol;
        _settings = settings;
        _clock = clock;
        _log = log;
    }

    public long FloatLamports => _settings.TreasuryMinLamports;
    public bool IsLow(long lamports) => lamports < _settings.TreasuryMinLamports;
    /// <summary>Since when the treasury has been low (null while it is not, or before the first read).</summary>
    public DateTime? LowSince { get; private set; }

    /// <summary>The treasury balance and when it was read; null without a treasury. Cached for <see cref="CacheFor"/>
    /// unless <paramref name="fresh"/>. Every read updates the alarm.</summary>
    public async Task<(long lamports, DateTime readAt)?> BalanceAsync(bool fresh = false)
    {
        if (!_sol.TreasuryOn) return null;
        await _gate.WaitAsync();
        try
        {
            var now = _clock.UtcNow;
            if (!fresh && _cached is { } c && now - c.at < CacheFor && now >= c.at) return c;
            var lamports = await _sol.GetBalanceAsync(_sol.TreasuryAddress!);
            _cached = (lamports, now);
            Alarm(lamports, now);
            return _cached;
        }
        finally { _gate.Release(); }
    }

    void Alarm(long lamports, DateTime now)
    {
        if (!IsLow(lamports))
        {
            if (LowSince != null)
                _log.LogInformation("Treasury float restored: {Lamports} lamports (float {Float})", lamports, FloatLamports);
            LowSince = null;
            _lastAlarm = null;
            return;
        }
        LowSince ??= now;
        if (_lastAlarm == null || now - _lastAlarm >= RealarmEvery)
        {
            _lastAlarm = now;
            _log.LogError("TREASURY LOW: {Lamports} lamports, below the float of {Float} (TREASURY_MIN_LAMPORTS) since {Since:O}. Top it up.",
                lamports, FloatLamports, LowSince);
        }
    }
}
