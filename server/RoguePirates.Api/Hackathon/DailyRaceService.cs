using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RoguePirates.Api.Data;
using RoguePirates.Api.Plausibility;
using RoguePirates.Api.Playtest;

namespace RoguePirates.Api.Hackathon;

/// <param name="Date">The race date (yyyy-MM-dd, UTC); default today.</param>
/// <param name="Seed">The race seed the run used (must match the race).</param>
/// <param name="Level">The run's level at the finish (only needed with a run log).</param>
/// <param name="Log">Optional run log (same format as POST /api/runs): scored by the run checks in shadow mode.</param>
public sealed record DailyRaceEntryRequest(string? Date, int Seed, double TimeSeconds, int Kills, int? Level = null,
    string? ClientVersion = null, JsonElement? Log = null);

/// <summary>
/// Hackathon: the daily async race. One course per UTC day: everyone plays the same seed on the same map and races to
/// <see cref="TargetKills"/> kills; the fastest time wins. The seed is derived from the date and a salt (DAILY_RACE_SALT,
/// a fixed default), so it is the same on every server and cannot be guessed ahead without the salt. One best time per
/// player and day; a run log, when sent, is scored by the run checks in shadow mode (stored, never a reason to refuse).
/// </summary>
public sealed class DailyRaceService
{
    public const string DefaultSalt = "rogue-pirates-daily-race-v1";
    public const int DefaultTargetKills = 100;
    public const int MaxRaceSeconds = 7200;
    /// <summary>Yesterday's race still takes entries this long after midnight (a run that started before it).</summary>
    public static readonly TimeSpan LateGrace = TimeSpan.FromMinutes(30);

    readonly GameDb _db;
    readonly IClock _clock;
    readonly IConfiguration _config;
    readonly PlausibilityService _checks;
    readonly ILogger<DailyRaceService> _log;

    public DailyRaceService(GameDb db, IClock clock, IConfiguration config, PlausibilityService checks, ILogger<DailyRaceService> log)
    {
        _db = db;
        _clock = clock;
        _config = config;
        _checks = checks;
        _log = log;
    }

    public int TargetKills => int.TryParse(_config["DAILY_RACE_TARGET_KILLS"], out var k) && k is > 0 and <= 10000 ? k : DefaultTargetKills;
    string Salt => string.IsNullOrWhiteSpace(_config["DAILY_RACE_SALT"]) ? DefaultSalt : _config["DAILY_RACE_SALT"]!.Trim();
    string Map => RunService.NormalizeMap(_config["DAILY_RACE_MAP"]);

    /// <summary>The seed of a date: HMAC-SHA256(salt, "yyyy-MM-dd"), first 4 bytes, positive and non-zero.</summary>
    public static int SeedFor(DateTime date, string salt)
    {
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(salt), Encoding.UTF8.GetBytes(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        var seed = BitConverter.ToInt32(mac, 0) & int.MaxValue;
        return seed == 0 ? 1 : seed;
    }

    static string Day(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    DateTime Today => DateTime.SpecifyKind(_clock.UtcNow.Date, DateTimeKind.Utc);

    async Task<DailyRace> RaceAsync(DateTime date)
    {
        var race = await _db.DailyRaces.FirstOrDefaultAsync(r => r.Date == date);
        if (race != null) return race;
        race = new DailyRace { Date = date, Seed = SeedFor(date, Salt), MapId = Map, CreatedAt = _clock.UtcNow };
        _db.DailyRaces.Add(race);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            // created by a parallel request
            _db.ChangeTracker.Clear();
            race = await _db.DailyRaces.FirstAsync(r => r.Date == date);
        }
        return race;
    }

    DateTime ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Today;
        if (!DateTime.TryParseExact(raw.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d))
            throw new ApiException(400, "invalid_date", "date must be yyyy-MM-dd (UTC).");
        return DateTime.SpecifyKind(d.Date, DateTimeKind.Utc);
    }

    object RaceView(DailyRace race) => new
    {
        raceId = race.Id, date = Day(race.Date), seed = race.Seed, mapId = race.MapId, targetKills = TargetKills,
        goal = $"Reach {TargetKills} kills as fast as you can.",
        opensAt = race.Date, closesAt = race.Date.AddDays(1), serverNow = _clock.UtcNow,
    };

    public async Task<object> TodayAsync(Player? me)
    {
        var race = await RaceAsync(Today);
        var entries = await _db.DailyRaceEntries.CountAsync(e => e.DailyRaceId == race.Id);
        return new { race = RaceView(race), entries, me = me == null ? null : await MeAsync(race, me) };
    }

    async Task<object?> MeAsync(DailyRace race, Player me)
    {
        var mine = await _db.DailyRaceEntries.AsNoTracking().FirstOrDefaultAsync(e => e.DailyRaceId == race.Id && e.PlayerId == me.Id);
        if (mine == null) return null;
        var better = await _db.DailyRaceEntries.CountAsync(e => e.DailyRaceId == race.Id
            && (e.TimeMs < mine.TimeMs || (e.TimeMs == mine.TimeMs && e.SubmittedAt < mine.SubmittedAt)
                || (e.TimeMs == mine.TimeMs && e.SubmittedAt == mine.SubmittedAt && e.Id < mine.Id)));
        return new { rank = better + 1, timeSeconds = mine.TimeMs / 1000.0, timeMs = mine.TimeMs, attempts = mine.Attempts, verdict = mine.Verdict, submittedAt = mine.SubmittedAt };
    }

    public async Task<object> SubmitAsync(Player player, DailyRaceEntryRequest req)
    {
        var date = ParseDate(req.Date);
        var now = _clock.UtcNow;
        if (date > Today) throw new ApiException(400, "race_not_open", "That race has not opened yet.");
        if (date < Today && now > date.AddDays(1) + LateGrace)
            throw new ApiException(409, "race_closed", $"The race of {Day(date)} is closed.");

        var race = await RaceAsync(date);
        if (req.Seed != race.Seed) throw new ApiException(400, "seed_mismatch", "This run was not played on the race's seed.");
        var target = TargetKills;
        if (req.Kills < target) throw new ApiException(400, "target_not_reached", $"The race needs {target} kills; this run has {req.Kills}.");
        // the same plausibility bound as the weekly board: at most 600 kills a minute
        var minSeconds = Math.Max(1.0, target * 60.0 / RunService.MaxKillsPerMinute);
        if (!double.IsFinite(req.TimeSeconds) || req.TimeSeconds < minSeconds || req.TimeSeconds > MaxRaceSeconds)
            throw new ApiException(422, "implausible_time", $"timeSeconds must be {minSeconds:0.#}..{MaxRaceSeconds}.");
        var timeMs = (int)Math.Round(req.TimeSeconds * 1000);

        var verdict = Score(player, req, race, now);

        await GameLocks.Runs.WaitAsync();
        try
        {
            var entry = await _db.DailyRaceEntries.FirstOrDefaultAsync(e => e.DailyRaceId == race.Id && e.PlayerId == player.Id);
            var improved = entry == null || timeMs < entry.TimeMs;
            if (entry == null)
            {
                entry = new DailyRaceEntry { DailyRaceId = race.Id, PlayerId = player.Id };
                _db.DailyRaceEntries.Add(entry);
            }
            entry.Attempts++;
            if (improved)
            {
                entry.TimeMs = timeMs;
                entry.Verdict = verdict;
                entry.SubmittedAt = now;
            }
            await _db.SaveChangesAsync();
            return new
            {
                accepted = true, improved, race = RaceView(race), verdict, @checked = verdict == Verdicts.Pass,
                best = await MeAsync(race, player),
            };
        }
        finally { GameLocks.Runs.Release(); }
    }

    /// <summary>The run checks' verdict on the log (shadow mode), or null without a log.</summary>
    string? Score(Player player, DailyRaceEntryRequest req, DailyRace race, DateTime now)
    {
        if (req.Log is not { } log || log.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        var seconds = (int)Math.Floor(req.TimeSeconds);
        var summary = new SubmitRunRequest(req.Kills, seconds, req.Level ?? 1, null, race.MapId, null, req.ClientVersion, log);
        var run = new RunRecord
        {
            PlayerId = player.Id, Kills = req.Kills, RunSeconds = seconds, Level = req.Level ?? 1, MapId = race.MapId,
            ClientVersion = req.ClientVersion, WeekStart = WeekMath.WeekStart(now), SubmittedAt = now,
        };
        try
        {
            var checkedLog = RunLogRules.Check(log, summary);
            if (checkedLog == null) return null;
            if (checkedLog.Header.Seed != race.Seed) return "seed_mismatch";
            return _checks.Score(run, checkedLog.Json, checkedLog.Header.BuildId, checkedLog.Header.ConfigHash, now).Status;
        }
        catch (ApiException ex)
        {
            _log.LogWarning("Daily race log of player {PlayerId} failed the structural checks (shadow mode, entry accepted): {Code} {Reason}", player.Id, ex.Code, ex.Message);
            return Verdicts.LogRejected;
        }
    }

    public async Task<object> BoardAsync(string? rawDate, int? limit, Player? me)
    {
        var date = ParseDate(rawDate);
        if (date > Today) throw new ApiException(400, "race_not_open", "That race has not opened yet.");
        var race = await RaceAsync(date);
        var take = Math.Clamp(limit ?? 50, 1, 100);
        var rows = await (from e in _db.DailyRaceEntries.AsNoTracking()
                          join p in _db.Players.AsNoTracking() on e.PlayerId equals p.Id
                          where e.DailyRaceId == race.Id
                          orderby e.TimeMs, e.SubmittedAt, e.Id
                          select new { e.PlayerId, p.Name, p.WalletAddress, e.TimeMs, e.Attempts, e.Verdict, e.SubmittedAt })
            .Take(take).ToListAsync();
        var total = await _db.DailyRaceEntries.CountAsync(e => e.DailyRaceId == race.Id);
        return new
        {
            race = RaceView(race), total,
            entries = rows.Select((r, i) => new
            {
                rank = i + 1, playerId = r.PlayerId, name = r.Name, tag = PlayerTags.For(r.PlayerId),
                wallet = r.WalletAddress == null ? null : PayoutOnce.Short(r.WalletAddress),
                timeSeconds = r.TimeMs / 1000.0, timeMs = r.TimeMs, attempts = r.Attempts, verdict = r.Verdict,
                @checked = r.Verdict == Verdicts.Pass, submittedAt = r.SubmittedAt,
            }),
            me = me == null ? null : await MeAsync(race, me),
        };
    }
}
