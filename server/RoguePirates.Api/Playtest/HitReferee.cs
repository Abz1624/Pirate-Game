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
    /// <summary>Longest reach from the attacker's reported position (edge-case audit, 6 Oct, measured from the weapon code at
    /// every upgrade axis at its most and Oversized Shot x1.5): Deck Cannons 43.4 m, Chainshot 43 m, Flamethrower 36.6 m,
    /// Swivel Guns 36 m, everything else under 30 m (Long Nine is an 18 m beam); plus the victim drawn up to 0.5 s
    /// extrapolated (17 m at the fastest 34 m/s), the 0.12 s drawn in the past (4 m) and a state's age (2 m): 67 m. The
    /// bot fires within 26 m. Parrots chase up to 86 m from where they were summoned, but only summon with the victim
    /// within 15 m, which opens the engagement.</summary>
    public const double MaxHitDistance = 80;
    /// <summary>How long damage may outlive the contact, in the attacker's game time: a full burn (20 stacks, losing 0.5 a
    /// second; Tidecaller does not lengthen it) lasts 40 s, after a Hellfire patch (2.4 s) re-lit it: 42.4 s, plus the
    /// 0.1 s batch, x1.02 for the clock's step (<see cref="MaxClockStep"/>): 43.4 s. Powder Kegs float 30.2 s; Monkey
    /// Gunners 9 s; parrots 6 s; whirlpools 3.6 s. Was 45 s (6 Oct, edge-case audit: 48 s for margin).</summary>
    public static readonly TimeSpan EngagementWindow = TimeSpan.FromSeconds(48);
    /// <summary>One remembered position per this much time (a ship moves at most 7 m in it).</summary>
    public static readonly TimeSpan TrailSpacing = TimeSpan.FromSeconds(0.2);

    // ---- reported positions: the speed check (edge-case audit, 6 Oct; see SpeedCheck) ----
    /// <summary>The fastest a PvP ship can sail, metres a second: the speed cap 75 x MoveSpeed / 50 x 1.625 with Rook's 6.2
    /// MoveSpeed and Navigator's Charts at 5 ranks (+3.1) is 22.7, x1.5 boost: 34 (PlayerShipController; the drag holds
    /// a real ship to about 12.2, 18.3 boosted). Hits do not push a ship; an island bounce adds at most about 6 m.</summary>
    public const double MaxShipSpeed = 35;
    /// <summary>Allowed over the fastest ship.</summary>
    public const double SpeedMargin = 1.5;
    /// <summary>Metres allowed on top (an island bounce, rounding, a state sent mid-frame).</summary>
    public const double SpeedSlack = 15;
    /// <summary>The gap before an accepted report (it was held back by a lag spike) is credited to the next, up to this.</summary>
    public static readonly TimeSpan MaxLagCredit = TimeSpan.FromSeconds(10);
    /// <summary>Ships sail from this long before the start (a client's estimate of the server clock may run ahead).</summary>
    public static readonly TimeSpan StartSkew = TimeSpan.FromSeconds(0.5);

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
        // Hackathon (12 Oct, owner): the bot fires its drafted weapons (the game's own); its old cannons tag still counts.
        if (attacker.IsBot) return weaponId == BotWeaponId || (attacker.Draft?.Weapons?.Contains(weaponId, StringComparer.Ordinal) ?? false);
        if (weaponId == GenericWeaponId) return true;
        // no draft on record (an old client): nothing to check against
        return attacker.Draft?.Weapons is not { Count: > 0 } weapons || weapons.Contains(weaponId, StringComparer.Ordinal);
    }

    /// <summary>The attacker can be dealing damage to the victim now: the ships were within <see cref="MaxHitDistance"/> at
    /// a reported moment of the attacker's last <see cref="EngagementWindow"/> (<see cref="RelaySeat.EngagedClock"/>), they
    /// are that close now, or the victim is that close to where the attacker was in that time (what it left behind).</summary>
    /// <remarks>Edge-case audit (6 Oct): the window runs on the attacker's game clock (<see cref="RelaySeat.Clock"/>), not
    /// the wall clock, so damage a stalled game (a hidden tab, a lost socket) delivers when it runs again still counts; the
    /// attacker's side is its speed-checked position; the victim's is either its checked or its reported one (the
    /// attacker's client draws and hits the reported one, and a victim's own report can never help a cheat).</remarks>
    public static bool InReach(RelaySeat attacker, RelaySeat victim)
    {
        if (attacker.Clock - attacker.EngagedClock <= EngagementWindow.TotalSeconds) return true;
        if (Near(attacker.X, attacker.Z, victim)) return true;
        var since = attacker.Clock - EngagementWindow.TotalSeconds;
        return attacker.Trail.AnyWithin(since, victim.X, victim.Z, MaxHitDistance)
            || attacker.Trail.AnyWithin(since, victim.ReportedX, victim.ReportedZ, MaxHitDistance);
    }

    /// <summary>A batch is the rest of a burst carried over (a client sends at most <see cref="ArenaRules.MaxHitDamage"/>
    /// a batch, one each 0.1 s, the rest after): the batch before it was full, came at most <see cref="CarryGap"/> before,
    /// and the burst began in reach at most <see cref="MaxCarry"/> ago. The worst legit burst, a keg field of 30 s of the
    /// level 60 worst build plus a fire-all (32.1 x 15884 raw), takes 20.4 s at 25000 raw a second; without this its tail
    /// would be refused once the victim left the kegs or the kegs' drop point left the window.</summary>
    public static bool CarriedOn(RelaySeat attacker) =>
        attacker.Clock - attacker.LastHitFullAt <= CarryGap && attacker.Clock - attacker.ReachClock <= MaxCarry;

    public const double CarryGap = 0.5, MaxCarry = 30;

    /// <summary>(x, z) within <see cref="MaxHitDistance"/> of the victim's checked or reported position.</summary>
    public static bool Near(double x, double z, RelaySeat victim) =>
        Distance(x, z, victim.X, victim.Z) <= MaxHitDistance || Distance(x, z, victim.ReportedX, victim.ReportedZ) <= MaxHitDistance;

    // ---- the seat's game clock (edge-case audit, 6 Oct): the engagement window runs on it ----
    /// <summary>A seat's clock advances by the time between two of its messages, at most this much: the most game time one
    /// frame advances (the run clock runs at most 5 ticks of 1/60 s a frame: 0.083 s; RunClock.MaxStepsPerFrame), and a
    /// client below 15 frames a second sends a state every frame. A game that stalls (a hidden tab stops its frames, a
    /// hitch, a lost socket) sends nothing, so its burns, kegs and summons, frozen on its own client, do not run out on the
    /// server meanwhile. In play (15 states and 10 batches a second) it runs with the wall clock.</summary>
    public const double MaxClockStep = 0.085;

    /// <summary>Advances <paramref name="seat"/>'s game clock to <paramref name="now"/> (a message of its own arrived).</summary>
    public static void Heard(RelaySeat seat, DateTime now)
    {
        // it starts at 0 at the match start (HeardAt's first value) and stands still in the countdown
        if (now <= seat.HeardAt) return;
        seat.Clock += Math.Min(MaxClockStep, (now - seat.HeardAt).TotalSeconds);
        seat.HeardAt = now;
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

/// <summary>V5 M8z (6 Oct, audit): a seat's checked positions over the last <see cref="HitRules.EngagementWindow"/> of its
/// game clock (<see cref="RelaySeat.Clock"/>, seconds), one per <see cref="HitRules.TrailSpacing"/>, for the range check
/// of what it left behind (kegs, turrets).</summary>
public sealed class PositionTrail
{
    readonly Queue<(double at, double x, double z)> _points = new();
    double _lastAt = double.NegativeInfinity;

    public int Count => _points.Count;

    public void Add(double at, double x, double z)
    {
        while (_points.Count > 0 && at - _points.Peek().at > HitRules.EngagementWindow.TotalSeconds) _points.Dequeue();
        if (_points.Count > 0 && at - _lastAt < HitRules.TrailSpacing.TotalSeconds - 1e-6) return;
        _points.Enqueue((at, x, z));
        _lastAt = at;
    }

    /// <summary>A position since <paramref name="since"/> (game clock) within <paramref name="reach"/> of (x, z).</summary>
    public bool AnyWithin(double since, double x, double z, double reach)
    {
        foreach (var p in _points)
            if (p.at >= since && HitRules.Distance(p.x, p.z, x, z) <= reach) return true;
        return false;
    }
}

/// <summary>
/// V5 M8z (6 Oct, edge-case audit): the speed check on a seat's reported positions. A client could otherwise report its
/// ship next to the victim for one state (the victim barely sees a blink) and open <see cref="HitRules.EngagementWindow"/>
/// of hits, or report itself onto a barrel to claim it. The server keeps a checked position that moves toward each report
/// out of a bucket of metres: it fills at <see cref="HitRules.MaxShipSpeed"/> x <see cref="HitRules.SpeedMargin"/> a
/// second and holds <see cref="HitRules.SpeedSlack"/> plus that rate x the silence it is still catching up on (the
/// longest recent gap between reports, up to <see cref="HitRules.MaxLagCredit"/>, used up at the wall clock's pace). So
/// states held back by a lag spike and arriving together, or a state delayed and the rest on time, carry their
/// movement through; a burst of states each a little further cannot add up to a jump, and no client can be reported
/// further than the fastest ship x 1.5 could sail in the time since it was last heard, plus 15 m.
/// </summary>
public sealed class SpeedCheck
{
    double _x, _z, _tokens, _gap;
    DateTime _lastAt, _gapEnd;

    public int Clamps { get; private set; }

    static double Rate => HitRules.MaxShipSpeed * HitRules.SpeedMargin;

    /// <summary>At (x, z) (the spawn); it may not move before <paramref name="from"/> (the start, less the clock skew)
    /// beyond <see cref="HitRules.SpeedSlack"/>.</summary>
    public SpeedCheck(double x, double z, DateTime from)
    {
        _x = x;
        _z = z;
        _lastAt = _gapEnd = from;
        _tokens = HitRules.SpeedSlack;
    }

    /// <summary>The checked position for a report of (x, z) at <paramref name="now"/>.</summary>
    public (double x, double z, bool clamped) Report(DateTime now, double x, double z)
    {
        if (now > _lastAt)
        {
            var gap = (now - _lastAt).TotalSeconds;
            _lastAt = now;
            var remaining = _gap - (now - _gapEnd).TotalSeconds;
            if (gap > remaining)
            {
                _gap = Math.Min(gap, HitRules.MaxLagCredit.TotalSeconds);
                _gapEnd = now;
            }
            var credit = Math.Max(0, _gap - (now - _gapEnd).TotalSeconds);
            _tokens = Math.Min(HitRules.SpeedSlack + Rate * credit, _tokens + Rate * gap);
        }
        var d = HitRules.Distance(_x, _z, x, z);
        if (d <= _tokens)
        {
            _tokens -= d;
            _x = x;
            _z = z;
            return (x, z, false);
        }
        Clamps++;
        var k = _tokens / d;
        _x += (x - _x) * k;
        _z += (z - _z) * k;
        _tokens = 0;
        return (_x, _z, true);
    }
}
