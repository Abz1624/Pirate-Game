namespace RoguePirates.Api.Playtest;

/// <summary>
/// V5 M8z (6 Oct, owner: "add anticheat measures"): the server's checks on a client's "hit" batch in a refereed 1v1. The
/// client reports raw weapon damage (before the PvP scale x0.06, the 0.5 s invulnerability window and the 5% of max hull
/// cap the victim's client applies), so every bound here is on raw damage and generous on purpose: a legit batch must
/// never be refused. Damage the server refuses is never forwarded and never counts toward the time-out winner.
/// </summary>
public static class HitRules
{
    /// <summary>The client's id for damage without a weapon tag (burns, hazards; PvpMatchController.FlushHits).</summary>
    public const string GenericWeaponId = "weapon";
    /// <summary>The practice bot's only weapon (the host's client reports its volleys; PvpBot.WeaponId).</summary>
    public const string BotWeaponId = "bot_cannons";

    // ---- damage per second, by the attacker's level (1 + the XP barrels the server gave it; nothing else levels in PvP) ----
    /// <summary>Sustained raw damage a second allowed at level 1. Six level 1 weapons measured about 70 raw/s at close
    /// range (3 Oct); the strongest level 1 six (Lighthouse Lantern alone is 107/s) with crits stays near 250.</summary>
    public const double BaseDps = 600;
    /// <summary>x this per level above 1 (one level-up raises one weapon or Logbook: about +3 to 5% of a build).</summary>
    public const double DpsGrowthPerLevel = 0.12;
    /// <summary>The most a client can send at all: 20 batches a second of 500.</summary>
    public const double MaxRawDps = ArenaRules.HitMessagesPerSecond * ArenaRules.MaxHitDamage;
    public static readonly TimeSpan ShortWindow = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan LongWindow = TimeSpan.FromSeconds(5);
    /// <summary>Bursts (a mortar volley and a crit together): the 1 s window allows twice the sustained rate.</summary>
    public const double BurstFactor = 2;

    // ---- range, from the last reported positions ----
    /// <summary>Longest weapon range is 18 m (Long Nine); x2 for range Logbooks, plus shots in flight and summons that
    /// keep fighting after their ship sails off.</summary>
    public const double MaxHitDistance = 80;
    /// <summary>A generous top speed (boosting) for the movement since the last reported state.</summary>
    public const double ShipSpeed = 20;
    /// <summary>The victim is shown where it last reported (extrapolated at most 0.5 s): its staleness counts up to this.</summary>
    public static readonly TimeSpan VictimStalenessCap = TimeSpan.FromSeconds(1);
    /// <summary>Clients send "state" 15 times a second on the same socket as their hits: an attacker this long without one
    /// is not reporting its position.</summary>
    public static readonly TimeSpan AttackerStateMaxAge = TimeSpan.FromSeconds(2);

    // ---- flag and forfeit (rejected damage counts out_of_draft, stale_state, out_of_range and dps_cap) ----
    public const double FlagRejectedDamage = 500, FlagRejectedShare = 0.5;
    /// <summary>A rated match is forfeited only past all three: legit play has no rejected damage at all.</summary>
    public const double ForfeitRejectedDamage = 3000, ForfeitRejectedShare = 0.75;
    public const int ForfeitRejectedHits = 30;

    /// <summary>Sustained raw damage a second allowed at <paramref name="level"/>.</summary>
    public static double SustainedDps(int level) =>
        Math.Min(MaxRawDps, BaseDps * Math.Pow(1 + DpsGrowthPerLevel, Math.Max(0, level - 1)));

    /// <summary>The weapon may hit for this seat: one of its drafted weapons or the generic id (the bot: its cannons).</summary>
    public static bool WeaponAllowed(RelaySeat attacker, string weaponId)
    {
        if (attacker.IsBot) return weaponId == BotWeaponId;
        if (weaponId == GenericWeaponId) return true;
        // no draft on record (an old client): nothing to check against
        return attacker.Draft?.Weapons is not { Count: > 0 } weapons || weapons.Contains(weaponId, StringComparer.Ordinal);
    }

    /// <summary>How far apart the two ships may be for a hit, given how old their last reported positions are.</summary>
    public static double Reach(TimeSpan attackerAge, TimeSpan victimAge)
    {
        var a = Math.Clamp(attackerAge.TotalSeconds, 0, AttackerStateMaxAge.TotalSeconds);
        var v = Math.Clamp(victimAge.TotalSeconds, 0, VictimStalenessCap.TotalSeconds);
        return MaxHitDistance + ShipSpeed * (a + v);
    }

    public static double Distance(RelaySeat a, RelaySeat b)
    {
        double dx = a.X - b.X, dz = a.Z - b.Z;
        return Math.Sqrt(dx * dx + dz * dz);
    }
}

/// <summary>V5 M8z (6 Oct, owner): one attacker's accepted raw damage over the last 5 s; caps a new batch by level.</summary>
public sealed class DamageWindow
{
    readonly Queue<(DateTime at, double dmg)> _recent = new();

    /// <summary>The part of <paramref name="dmg"/> that fits under both windows' caps at <paramref name="level"/> (recorded
    /// as accepted); the rest is the caller's to drop.</summary>
    public double Accept(DateTime now, double dmg, int level)
    {
        while (_recent.Count > 0 && now - _recent.Peek().at >= HitRules.LongWindow) _recent.Dequeue();
        double shortSum = 0, longSum = 0;
        foreach (var (at, d) in _recent)
        {
            longSum += d;
            if (now - at < HitRules.ShortWindow) shortSum += d;
        }
        var sustained = HitRules.SustainedDps(level);
        var shortCap = sustained * HitRules.BurstFactor * HitRules.ShortWindow.TotalSeconds;
        var longCap = sustained * HitRules.LongWindow.TotalSeconds;
        var accepted = Math.Max(0, Math.Min(dmg, Math.Min(shortCap - shortSum, longCap - longSum)));
        if (accepted > 0) _recent.Enqueue((now, accepted));
        return accepted;
    }
}
