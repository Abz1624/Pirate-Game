namespace RoguePirates.Api.Playtest;

/// <summary>
/// The fixed 1v1 arena and match timeline the relay referees (owner's rules, 3 Oct 2026). Everything here is a constant
/// on purpose: both clients get the numbers in the match-start message, so tuning them is a server change only.
/// Coordinates are metres in the game's XZ plane; yaw is degrees about +Y (Unity), 0 = facing +Z, 90 = facing +X.
/// </summary>
public static class ArenaRules
{
    // ---- map ----
    public const double CenterX = 0, CenterZ = 0;
    public const double ArenaRadius = 120;
    public const double SpawnOffset = 45; // seat 0 at (-45, 0), seat 1 at (45, 0), both facing the centre

    // ---- timeline ----
    public static readonly TimeSpan Countdown = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan MatchLength = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan StormShrinkStart = TimeSpan.FromSeconds(150);
    public static readonly TimeSpan StormShrinkEnd = TimeSpan.FromSeconds(300);
    public const double StormStartRadius = ArenaRadius;
    public const double StormEndRadius = 25;
    public const double StormDamagePerSecond = 8;        // applied by clients outside the ring
    public const double StormDamageGrowthPerSecond = 2;  // +2 hp/s for every second spent continuously outside

    // ---- barrels ----
    public static readonly TimeSpan XpBarrelEvery = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan HealthBarrelEvery = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan BarrelLifetime = TimeSpan.FromSeconds(20);
    public const double ClaimRadius = 6;
    public const double BarrelMinShipDistance = 15;
    public const double BarrelRingMargin = 3; // barrels spawn at least this far inside the storm ring
    public const double HealthBarrelHealFraction = 0.2;

    // ---- relay limits ----
    public const int StateMessagesPerSecond = 30;
    public const int FxMessagesPerSecond = 30;
    public const int HitMessagesPerSecond = 20;
    public const int PickupMessagesPerSecond = 10;
    public const int FxMaxBytes = 2048;
    public const int MaxMessageBytes = 4096;
    public const double MaxHitDamage = 500;
    public static readonly TimeSpan DisconnectForfeit = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan RematchWindow = TimeSpan.FromSeconds(60);

    public static (double x, double z, double yaw) Spawn(int seat) =>
        seat == 0 ? (CenterX - SpawnOffset, CenterZ, 90) : (CenterX + SpawnOffset, CenterZ, 270);

    /// <summary>Storm radius after <paramref name="elapsed"/> of match time (from the end of the countdown).</summary>
    public static double StormRadius(TimeSpan elapsed)
    {
        if (elapsed <= StormShrinkStart) return StormStartRadius;
        if (elapsed >= StormShrinkEnd) return StormEndRadius;
        var f = (elapsed - StormShrinkStart).TotalMilliseconds / (StormShrinkEnd - StormShrinkStart).TotalMilliseconds;
        return StormStartRadius + (StormEndRadius - StormStartRadius) * f;
    }

    /// <summary>
    /// A fair barrel position: on the perpendicular bisector of the two ships (so equidistant from both), inside the storm
    /// ring less a margin, at least 15 m from both ships when the ring allows it (otherwise as far as the ring allows).
    /// Falls back to the arena centre when the bisector misses the ring. Positions are relative to the arena centre.
    /// </summary>
    public static (double x, double z) PlaceBarrel(Random rng, (double x, double z) a, (double x, double z) b, double stormRadius)
    {
        var r = Math.Max(0, stormRadius - BarrelRingMargin);
        double mx = (a.x + b.x) / 2 - CenterX, mz = (a.z + b.z) / 2 - CenterZ;
        double dx = b.x - a.x, dz = b.z - a.z;
        var len = Math.Sqrt(dx * dx + dz * dz);
        double ux, uz;
        if (len < 0.01)
        {
            // ships on top of each other: every point is equidistant; pick a random direction through them
            var angle = rng.NextDouble() * Math.PI * 2;
            ux = Math.Cos(angle);
            uz = Math.Sin(angle);
        }
        else
        {
            ux = -dz / len;
            uz = dx / len;
        }

        // |M + s*u| <= r  ->  s^2 + 2 s (M.u) + |M|^2 - r^2 <= 0
        var mu = mx * ux + mz * uz;
        var disc = mu * mu - (mx * mx + mz * mz - r * r);
        if (disc < 0) return (CenterX, CenterZ);
        var root = Math.Sqrt(disc);
        double s1 = -mu - root, s2 = -mu + root;

        // distance to each ship = sqrt((len/2)^2 + s^2) >= 15  ->  |s| >= sMin
        var half = len / 2;
        var sMin = Math.Sqrt(Math.Max(0, BarrelMinShipDistance * BarrelMinShipDistance - half * half));
        var segments = new List<(double lo, double hi)>();
        if (s1 <= Math.Min(s2, -sMin)) segments.Add((s1, Math.Min(s2, -sMin)));
        if (Math.Max(s1, sMin) <= s2) segments.Add((Math.Max(s1, sMin), s2));

        double s;
        if (segments.Count == 0)
            s = Math.Abs(s1) >= Math.Abs(s2) ? s1 : s2; // ring too small for 15 m: as far from both ships as it allows
        else
        {
            var total = segments.Sum(g => g.hi - g.lo);
            var pick = rng.NextDouble() * total;
            s = segments[^1].hi;
            foreach (var (lo, hi) in segments)
            {
                if (pick <= hi - lo) { s = lo + pick; break; }
                pick -= hi - lo;
            }
        }
        return (CenterX + mx + s * ux, CenterZ + mz + s * uz);
    }

    public static long Ms(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    /// <summary>Arena, spawns and storm as sent to clients (in the lobby state and the match-start message).</summary>
    public static object ArenaView() => new { centerX = CenterX, centerZ = CenterZ, radius = ArenaRadius };

    public static object[] SpawnsView(Guid seat0, Guid? seat1) => new object[]
    {
        SpawnView(0, seat0),
        SpawnView(1, seat1),
    };

    static object SpawnView(int seat, Guid? playerId)
    {
        var (x, z, yaw) = Spawn(seat);
        return new { seat, playerId, x, z, yaw };
    }

    public static object StormView(DateTime startsAt) => new
    {
        shrinkStartMs = Ms(startsAt + StormShrinkStart),
        shrinkEndMs = Ms(startsAt + StormShrinkEnd),
        startRadius = StormStartRadius,
        endRadius = StormEndRadius,
        damagePerSecond = StormDamagePerSecond,
        damageGrowthPerSecond = StormDamageGrowthPerSecond,
    };

    public static object BarrelRulesView() => new
    {
        xpEveryMs = (long)XpBarrelEvery.TotalMilliseconds,
        healthEveryMs = (long)HealthBarrelEvery.TotalMilliseconds,
        firstXpMs = (long)XpBarrelEvery.TotalMilliseconds,
        firstHealthMs = (long)HealthBarrelEvery.TotalMilliseconds,
        ttlMs = (long)BarrelLifetime.TotalMilliseconds,
        claimRadius = ClaimRadius,
        healthHealFraction = HealthBarrelHealFraction,
    };

    public static object LimitsView() => new
    {
        stateRateHz = 15,
        maxStatePerSecond = StateMessagesPerSecond,
        maxFxPerSecond = FxMessagesPerSecond,
        maxHitsPerSecond = HitMessagesPerSecond,
        maxPickupsPerSecond = PickupMessagesPerSecond,
        fxMaxBytes = FxMaxBytes,
        maxHitDamage = MaxHitDamage,
        disconnectForfeitMs = (long)DisconnectForfeit.TotalMilliseconds,
        rematchWindowMs = (long)RematchWindow.TotalMilliseconds,
    };
}

/// <summary>The server-side practice bot that can take seat 1 (its ship is simulated by the host's client).</summary>
public static class PracticeBot
{
    public static readonly Guid Id = new("00000000-0000-0000-0000-0000000000b0");
    public const string Name = "Practice Bot";
    public const int Rating = 1000;

    public static readonly DraftDto Draft = new(
        new List<string> { "deck_cannons", "mortar", "chainshot", "swivel_guns", "harpoon_gun", "long_nine" },
        new List<string> { "gunnery_manual", "ironclad", "admiral", "carpenters_notes", "lookouts_glass", "oversized_shot" });

    public static bool Is(Guid? id) => id == Id;
}

/// <summary>Token bucket: <c>capacity</c> messages at once, refilled at <c>capacity</c> per second.</summary>
public sealed class RateBucket
{
    readonly double _capacity;
    double _tokens;
    DateTime _last;

    public RateBucket(int perSecond, DateTime now)
    {
        _capacity = perSecond;
        _tokens = perSecond;
        _last = now;
    }

    public bool Take(DateTime now)
    {
        if (now > _last)
        {
            _tokens = Math.Min(_capacity, _tokens + (now - _last).TotalSeconds * _capacity);
            _last = now;
        }
        if (_tokens < 1) return false;
        _tokens -= 1;
        return true;
    }
}
