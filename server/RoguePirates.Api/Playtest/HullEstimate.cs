namespace RoguePirates.Api.Playtest;

/// <summary>
/// V5 M9j (20 Oct, owner: "make the server decide or verify game results"): the relay's own estimate of one seat's hull,
/// built only from what the server accepted and awarded, so a client that ignores the hits it takes (an "immortal" ship)
/// shows up, and the time-out tie-break never reads a self-reported hull.
///
/// It replays the victim client's PvP hit rules (<c>PvpRules</c> / <c>PvpHitWindow</c> in the game): an accepted hit of
/// raw damage lands at once as raw x <see cref="DamageScale"/> x the incoming multiplier, capped at
/// <see cref="MaxHitFraction"/> of max hull, and opens a <see cref="WindowSeconds"/> window; hits inside the window pool
/// and land as one capped hit when it ends. Heals: +<see cref="ArenaRules.HealthBarrelHealFraction"/> of max hull per
/// health barrel the server awarded, capped at max hull.
///
/// It is an <b>estimate</b>: armour, max hull and regeneration live on the client (M22's server-run 1v1 replaces this).
/// So it leans toward the ship: an assumed incoming multiplier of <see cref="AssumedIncomingMultiplier"/> (Ironclad at
/// rank 5 is 0.57) and a regeneration allowance of <see cref="RegenSharePerSecond"/> of max hull a second, both of which
/// under-count the damage an unarmoured ship takes. Max hull is the seat's own report clamped to
/// [<see cref="MinMaxHull"/>, <see cref="MaxHullCeiling"/> at the server's level for the seat], and a raise in max hull
/// keeps the damage taken (a raise adds hull with it). Hits land at the server's arrival time, not the victim's frame time, so the pooling can differ
/// by a window or two; the margin (RELAY_HULL_MARGIN) covers it before anything is enforced.
/// </summary>
public sealed class HullEstimate
{
    public const double DamageScale = 0.06;
    public const double WindowSeconds = 0.5;
    public const double MaxHitFraction = 0.05;
    public const double AssumedIncomingMultiplier = 0.5;
    public const double RegenSharePerSecond = 0.002;
    /// <summary>The ship's hull before its first report (PvpRules: "a 120-hull ship" at level 1).</summary>
    public const double DefaultMaxHull = 120;
    public const double MinMaxHull = 50;
    /// <summary>The largest max hull taken from a report at the server's level for the seat: 250 + 50 a level above 1
    /// (an assumption, generous against a 120-hull level 1 ship; reports above it are counted in <see cref="ClampedReports"/>).</summary>
    public static double MaxHullCeiling(int level) => 250 + 50 * Math.Max(0, level - 1);
    /// <summary>RELAY_HULL_MARGIN default: the estimate must fall this far below zero (a share of max hull).</summary>
    public const double DefaultMargin = 0.25;

    double _windowEnds = double.NegativeInfinity;
    double _pending;
    double _lastAt = double.NaN;

    public double MaxHull { get; private set; } = DefaultMaxHull;
    /// <summary>Hull left (can go below zero: how far past sinking the accepted damage went).</summary>
    public double Hull { get; private set; } = DefaultMaxHull;
    /// <summary>The lowest <see cref="Share"/> reached so far.</summary>
    public double MinShare { get; private set; } = 1;
    /// <summary>Seconds (match time) when the share first fell below the margin; null while it never did.</summary>
    public double? BelowMarginAt { get; private set; }
    public int ClampedReports { get; private set; }
    public double Landed { get; private set; }

    public double Share => MaxHull > 0 ? Hull / MaxHull : 0;

    /// <summary>The seat reported its max hull (at <paramref name="t"/> seconds of match time, at the server's level).</summary>
    public void ReportMax(double t, double reported, int serverLevel)
    {
        if (!double.IsFinite(reported) || reported <= 0) return;
        Advance(t);
        var clamped = Math.Clamp(reported, MinMaxHull, MaxHullCeiling(serverLevel));
        if (clamped < reported - 1e-9) ClampedReports++;
        // the damage taken so far is kept: a level-up's extra hull comes with it, a lower max keeps the same loss
        Hull = clamped - (MaxHull - Hull);
        MaxHull = clamped;
    }

    /// <summary>An accepted hit of <paramref name="raw"/> weapon damage at <paramref name="t"/> seconds of match time.</summary>
    public void Hit(double t, double raw, double margin)
    {
        if (!(raw > 0)) return;
        Advance(t);
        Flush(t, margin);
        var amount = raw * DamageScale * AssumedIncomingMultiplier;
        if (t < _windowEnds)
        {
            _pending += amount;
            return;
        }
        _windowEnds = t + WindowSeconds;
        Land(t, Math.Min(amount, Cap), margin);
    }

    /// <summary>A health barrel awarded by the server.</summary>
    public void Heal(double t, double fraction)
    {
        Advance(t);
        Hull = Math.Min(MaxHull, Hull + MaxHull * fraction);
    }

    /// <summary>Each relay tick: the pooled hits land as one capped hit once their window is over.</summary>
    public void Flush(double t, double margin)
    {
        if (_pending <= 0 || t < _windowEnds) return;
        Advance(t);
        var take = Math.Min(_pending, Cap);
        _pending = 0;
        _windowEnds = t + WindowSeconds;
        Land(t, take, margin);
    }

    double Cap => Math.Max(1, MaxHull) * MaxHitFraction;

    void Land(double t, double amount, double margin)
    {
        Hull -= amount;
        Landed += amount;
        if (Share < MinShare) MinShare = Share;
        if (BelowMarginAt == null && Share < -margin) BelowMarginAt = t;
    }

    /// <summary>The regeneration allowance up to <paramref name="t"/> (only while above water: a ship past zero stays down).</summary>
    void Advance(double t)
    {
        if (double.IsNaN(_lastAt) || t <= _lastAt)
        {
            if (double.IsNaN(_lastAt)) _lastAt = t;
            return;
        }
        if (Hull > 0) Hull = Math.Min(MaxHull, Hull + MaxHull * RegenSharePerSecond * (t - _lastAt));
        _lastAt = t;
    }

    /// <summary>The verdict of a seat at the match's end: the client's own end (sunk) against the estimate.</summary>
    public string Verdict(bool clientSunk, bool tracked)
    {
        if (!tracked) return HullChecks.NotTracked;
        var serverSunk = BelowMarginAt != null;
        if (serverSunk && !clientSunk) return HullChecks.ServerSunkClientAlive;
        if (clientSunk && MinShare > 0) return HullChecks.ClientSunkFirst;
        return HullChecks.Agree;
    }
}

/// <summary>V5 M9j: the per-seat hull verdicts on a relay match's end.</summary>
public static class HullChecks
{
    /// <summary>The client's end and the estimate agree (both afloat, or sunk with the estimate at or past zero).</summary>
    public const string Agree = "agree";
    /// <summary>The estimate went past the margin but the client never reported sinking: what an "immortal" client does
    /// (or an armour, max hull or regeneration the estimate does not know).</summary>
    public const string ServerSunkClientAlive = "server_sunk_client_alive";
    /// <summary>The client sank while the estimate still had hull: expected for storm deaths and when the estimate
    /// under-counts (armour assumed).</summary>
    public const string ClientSunkFirst = "client_sunk_first";
    /// <summary>The server sank the ship from its estimate (RELAY_HULL_ENFORCE).</summary>
    public const string ServerSunk = "server_sunk";
    /// <summary>A seat not tracked (the bot, whose hull the host's client simulates).</summary>
    public const string NotTracked = "not_tracked";
}
