namespace RoguePirates.Api.Playtest;

/// <summary>
/// V5 M8z (6 Oct, owner: "add anticheat measures"; audited the same day: "check if this is applicable for all the weapons
/// and scenarios"): the server's checks on a client's "hit" batch in a refereed 1v1. The client reports raw weapon damage
/// (before the PvP scale x0.06, the 0.5 s invulnerability window and the 5% of max hull cap the victim's client applies),
/// so every bound here is on raw damage and set above the worst legitimate case: a legit batch must never be refused.
/// Damage the server refuses is never forwarded and never counts toward the time-out winner.
///
/// The three checks and why they hold for every weapon (the audit, 6 Oct):
/// <list type="bullet">
/// <item>Weapon in the draft. Every weapon tags its damage with its own base weapon id, evolved or not (an evolution only
/// changes the slot's numbers; kegs, delayed shells, whirlpools, summons and orbit blades carry the weapon's id), and
/// untagged damage (burn ticks, flame patches, the lightning salvo) is sent as "weapon". The client also maps any id
/// outside its draft (an item's pulse, should items ever reach a 1v1) to "weapon" (PvpMatchController.HitTag).</item>
/// <item>Range, as an engagement check (<see cref="InReach"/>): a hit is accepted while the two ships were within
/// <see cref="MaxHitDistance"/> of each other at some reported moment of the last <see cref="EngagementWindow"/>, or the
/// victim is now within it of a place the attacker sailed through in that time. The first covers damage that outlives
/// the contact (burns: 20 stacks decay over 40 s; parrots chasing for 6 s; whirlpools; shells in flight), the second what
/// the attacker leaves behind (Powder Kegs float for 30 s; Monkey Gunners' turrets stay 8 s where they were dropped). A
/// stale position no longer refuses anything (a background tab sends states at 1 Hz or none; the victim's ghost is hit
/// where it was last reported, which is where its last state put it).</item>
/// <item>Damage a second, as a bucket (<see cref="DamageBudget"/>): it fills at <see cref="SustainedDps"/> of the
/// attacker's server-counted level and holds <see cref="BankSeconds"/> of it, so damage stored up legitimately (a keg
/// field detonating at once, "All Guns!" firing every weapon) lands whole, while the long-run rate stays capped.</item>
/// </list>
/// </summary>
public static class HitRules
{
    /// <summary>The client's id for damage without a weapon tag (burns, hazards; PvpMatchController.FlushHits).</summary>
    public const string GenericWeaponId = "weapon";
    /// <summary>The practice bot's only weapon (the host's client reports its volleys; PvpBot.WeaponId).</summary>
    public const string BotWeaponId = "bot_cannons";

    // ---- damage per second, by the attacker's level (1 + the XP barrels the server gave it; nothing else levels in PvP) ----
    //
    // The worst legitimate raw damage a second at each level, measured with the game's own weapon code by the Unity test
    // PvpDamageCapTests (Pirate Game Unity/Assets/_Game/Tests/V4/EditMode/PvpDamageCapTests.cs, 6 Oct): all 19 weapons
    // fired at one 2.2 m target from their best of 48 placements, each upgrade axis at its best over 300 Common upgrade
    // paths at once (no real path gets all of them), evolutions counted from weapon level 12 (a 1v1 never offers one
    // today), Oversized Shot and Tidecaller at their maximum free from level 1, then the best six weapons, Extra Hands,
    // Gunner's Manual, Powdermaster's Drill and Lookout's Glass levels for the level-ups, Rook's +0.5% a level, crits
    // always proccing, and the burn ceiling (10 raw/s) on top:
    //
    //   level      1    2    3    5    8   10   11   12   15   20   25   30   35   40    45    50    55    60
    //   worst    354  427  505  598  779 1075 1250 1965 2883 4149 5095 6985 8926 9640 12193 13363 14488 15884
    //   allowed  600 1020 1440 2280 3540 4380 4800 5220 6480 8580 10680 12780 14880 16980 19080 21180 23280 25380
    //
    // allowed = 600 + 420 x (level - 1): at least x1.5 the worst at every level 1..61 (the test fails otherwise). For
    // scale, six level 1 weapons measured about 70 raw/s in real matches (3 Oct). The old rule (600 x 1.12^(level-1),
    // at most 10000, 2x in any 1 s and 5x in any 5 s) was under the worst case from level 12 to 15 and from 40 on, and
    // refused a keg field or an "All Guns!" volley landing at once.
    /// <summary>Sustained raw damage a second allowed at level 1.</summary>
    public const double BaseDps = 600;
    /// <summary>+ this a level above 1.</summary>
    public const double DpsPerLevel = 420;
    /// <summary>The most a client can send at all: 20 batches a second of <see cref="ArenaRules.MaxHitDamage"/>.</summary>
    public const double MaxRawDps = ArenaRules.HitMessagesPerSecond * ArenaRules.MaxHitDamage;
    /// <summary>The bucket starts with this many seconds of the level 1 rate: an "All Guns!" at the start adds at most
    /// 2.1 s of a weapon's own sustained damage (Maelstrom Charge; measured).</summary>
    public const double InitialBankSeconds = 10;
    /// <summary>The bucket holds this many seconds of the current level's rate: kegs float for 30 s, so a field of 30 s of
    /// drops can go off at once, plus a fire-all (2.1 s), x1.5.</summary>
    public const double BankSeconds = 50;

    // ---- range: an engagement check over the last seconds ----
    /// <summary>Longest weapon reach: Long Nine 18 m x its size upgrades x Oversized Shot (about 54 m), plus shots in flight,
    /// the 0.12 s the opponent is drawn in the past and the network delay.</summary>
    public const double MaxHitDistance = 80;
    /// <summary>How long damage may outlive the contact: a full burn (20 stacks, losing 0.5 a second) lasts 40 s; Powder
    /// Kegs float 30 s; Monkey Gunners 8 s; parrots 6 s.</summary>
    public static readonly TimeSpan EngagementWindow = TimeSpan.FromSeconds(45);
    /// <summary>One remembered position per this much time (a ship moves at most 4 m in it).</summary>
    public static readonly TimeSpan TrailSpacing = TimeSpan.FromSeconds(0.2);

    // ---- flag and forfeit (rejected damage counts not_in_draft, out_of_range and dps_cap) ----
    public const double FlagRejectedDamage = 500, FlagRejectedShare = 0.5;
    /// <summary>A rated match is forfeited only past all three: legit play has no rejected damage at all.</summary>
    public const double ForfeitRejectedDamage = 3000, ForfeitRejectedShare = 0.75;
    public const int ForfeitRejectedHits = 30;

    /// <summary>Sustained raw damage a second allowed at <paramref name="level"/>.</summary>
    public static double SustainedDps(int level) => Math.Min(MaxRawDps, BaseDps + DpsPerLevel * Math.Max(0, level - 1));

    /// <summary>The weapon may hit for this seat: one of its drafted weapons or the generic id (the bot: its cannons).</summary>
    public static bool WeaponAllowed(RelaySeat attacker, string weaponId)
    {
        if (attacker.IsBot) return weaponId == BotWeaponId;
        if (weaponId == GenericWeaponId) return true;
        // no draft on record (an old client): nothing to check against
        return attacker.Draft?.Weapons is not { Count: > 0 } weapons || weapons.Contains(weaponId, StringComparer.Ordinal);
    }

    /// <summary>The attacker can be dealing damage to the victim now: the ships were within <see cref="MaxHitDistance"/> at
    /// a reported moment of the last <see cref="EngagementWindow"/> (<paramref name="engagedAt"/>), or the victim is now
    /// that close to where the attacker was in that time (what it left behind).</summary>
    public static bool InReach(DateTime now, DateTime engagedAt, RelaySeat attacker, RelaySeat victim)
    {
        if (now - engagedAt <= EngagementWindow) return true;
        if (Distance(attacker, victim) <= MaxHitDistance) return true;
        return attacker.Trail.AnyWithin(now - EngagementWindow, victim.X, victim.Z, MaxHitDistance);
    }

    public static double Distance(RelaySeat a, RelaySeat b) => Distance(a.X, a.Z, b.X, b.Z);

    public static double Distance(double ax, double az, double bx, double bz)
    {
        double dx = ax - bx, dz = az - bz;
        return Math.Sqrt(dx * dx + dz * dz);
    }
}

/// <summary>
/// V5 M8z (6 Oct, owner; audited the same day): one attacker's damage allowance, a bucket that fills at the level's
/// sustained rate (<see cref="HitRules.SustainedDps"/>) up to <see cref="HitRules.BankSeconds"/> of it, from the match
/// start. Legit play deals at most the worst case (two thirds of the rate or less) plus what it stored (kegs afloat, a
/// fire-all), which the bank holds, so it is never short; a client claiming more than the rate drains it.
/// </summary>
public sealed class DamageBudget
{
    double _tokens;
    DateTime _at;

    public DamageBudget(DateTime startAt)
    {
        _at = startAt;
        _tokens = HitRules.InitialBankSeconds * HitRules.SustainedDps(1);
    }

    /// <summary>Raw damage the bucket holds now (after refilling to <paramref name="now"/> at <paramref name="level"/>).</summary>
    public double Available(DateTime now, int level)
    {
        Refill(now, level);
        return _tokens;
    }

    /// <summary>The part of <paramref name="dmg"/> the bucket covers at <paramref name="level"/> (taken from it); the rest
    /// is the caller's to drop.</summary>
    public double Accept(DateTime now, double dmg, int level)
    {
        Refill(now, level);
        var accepted = Math.Clamp(dmg, 0, Math.Max(0, _tokens));
        _tokens -= accepted;
        return accepted;
    }

    void Refill(DateTime now, int level)
    {
        if (now <= _at) return;
        var rate = HitRules.SustainedDps(level);
        _tokens = Math.Min(HitRules.BankSeconds * rate, _tokens + (now - _at).TotalSeconds * rate);
        _at = now;
    }
}

/// <summary>V5 M8z (6 Oct, audit): a seat's reported positions over the last <see cref="HitRules.EngagementWindow"/>, one
/// per <see cref="HitRules.TrailSpacing"/>, for the range check of what it left behind (kegs, turrets).</summary>
public sealed class PositionTrail
{
    readonly Queue<(DateTime at, double x, double z)> _points = new();
    DateTime _lastAt = DateTime.MinValue;

    public int Count => _points.Count;

    public void Add(DateTime at, double x, double z)
    {
        while (_points.Count > 0 && at - _points.Peek().at > HitRules.EngagementWindow) _points.Dequeue();
        if (_points.Count > 0 && at - _lastAt < HitRules.TrailSpacing) return;
        _points.Enqueue((at, x, z));
        _lastAt = at;
    }

    /// <summary>A position since <paramref name="since"/> within <paramref name="reach"/> of (x, z).</summary>
    public bool AnyWithin(DateTime since, double x, double z, double reach)
    {
        foreach (var p in _points)
            if (p.at >= since && HitRules.Distance(p.x, p.z, x, z) <= reach) return true;
        return false;
    }
}
