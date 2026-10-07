using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RoguePirates.Api.Playtest;

/// <summary>
/// V5 M9a (I9a.1): the run log's format (<c>rp-runlog</c> version 1, written by the game's
/// <c>RoguePirates.V4.Telemetry.RunLogBuilder</c>) and its structural checks. Structural only: a log that is malformed,
/// oversized, missing its seed or config hash, or whose sums disagree with the summary fields is rejected with 400.
/// Whether the numbers are believable (rate bounds per minute, the survival-time bound) is the plausibility checker's
/// job (I9a.3), which reads the stored log.
///
/// Format (one JSON object):
/// <code>
/// { "format":"rp-runlog", "v":1, "seed":int (not 0), "seedSource":"local|requested|issued", "issuedRunId":str?,
///   "runId":str, "configHash":str (required), "balanceVersion":str, "contentHash":str, "stageProfile":str,
///   "buildId":str (required), "captain":str, "startingWeapon":str, "end":"sunk|victory|quit", "seconds":number,
///   "bossSummoned":number (-1 none), "bossDefeated":number (-1 none), "minuteSeconds":60,
///   "weapons":[weapon ids], "minutes":[ { "m":index, "spawns", "kills", "eliteKills", "bossKills", "gold", "xp",
///   "level", "taken", "dealt", "healed", "pickups", "chests", "pois", "alive", "hull", "maxHull", "wk":[kills per
///   weapon, aligned with "weapons"] } ], "inputEvery":seconds, "inputCols":["t","x","z","heading","steer","throttle",
///   "boost"], "inputs":[[...one row per sample...]] }
/// </code>
/// Every per-minute number is what happened in that minute (the level, alive, hull and maxHull are the values at the
/// minute's end). The last minute is partial.
/// </summary>
public static partial class RunLogRules
{
    public const string Format = "rp-runlog";
    public const int Version = 1;
    /// <summary>The largest log accepted (UTF-8 bytes of the log object). A 15-20 minute voyage is about 12-16 KB.</summary>
    public const int MaxLogBytes = 64 * 1024;
    public const int MinuteSeconds = 60;
    public const int MaxWeapons = 32, MaxInputSamples = 2000, MaxMinutes = RunService.MaxRunSeconds / MinuteSeconds + 2;

    public static readonly string[] InputCols = { "t", "x", "z", "heading", "steer", "throttle", "boost" };
    static readonly string[] SeedSources = { "local", "requested", "issued" };
    static readonly string[] EndReasons = { "sunk", "victory", "quit" };
    static readonly string[] CountFields = { "spawns", "kills", "eliteKills", "bossKills", "gold", "xp", "pickups", "chests", "pois", "alive" };
    static readonly string[] AmountFields = { "taken", "dealt", "healed", "hull", "maxHull" };

    [GeneratedRegex("^[A-Za-z0-9_.:-]{1,64}$")]
    private static partial Regex IdPattern();

    /// <summary>The header fields kept in their own columns.</summary>
    public sealed record Header(int Version, int Seed, string SeedSource, string ConfigHash, string? BalanceVersion, string? BuildId, string? ClientRunId);

    /// <summary>A log that passed the checks: its header and its JSON as stored.</summary>
    public sealed record Checked(Header Header, string Json, int Bytes);

    /// <summary>Checks a log against the run's summary. Null when there is none (older clients); throws 400 when it fails.</summary>
    public static Checked? Check(JsonElement? log, SubmitRunRequest summary)
    {
        if (log == null || log.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        var root = log.Value;
        var json = root.GetRawText();
        var bytes = Encoding.UTF8.GetByteCount(json);
        if (bytes > MaxLogBytes) throw Bad("log_too_large", $"log is {bytes} bytes; at most {MaxLogBytes}.");
        if (root.ValueKind != JsonValueKind.Object) throw Malformed("log must be an object");

        if (Str(root, "format", 32, true) != Format) throw Malformed($"format must be \"{Format}\"");
        var v = Int(root, "v");
        if (v != Version) throw Bad("unsupported_log_version", $"log version {v} is not supported (expected {Version}).");

        var seed = Int(root, "seed");
        if (seed == 0) throw Bad("log_missing_seed", "log seed is missing or 0.");
        var configHash = Str(root, "configHash", 128, false);
        if (string.IsNullOrEmpty(configHash)) throw Bad("log_missing_config_hash", "log configHash is missing.");
        var seedSource = Str(root, "seedSource", 16, true)!;
        if (Array.IndexOf(SeedSources, seedSource) < 0) throw Malformed("seedSource must be local, requested or issued");
        var buildId = Str(root, "buildId", 64, true);
        var balanceVersion = Str(root, "balanceVersion", 64, false);
        var runId = Str(root, "runId", 64, false);
        foreach (var f in new[] { "issuedRunId", "contentHash", "stageProfile", "captain", "startingWeapon" }) Str(root, f, 128, false);
        var end = Str(root, "end", 16, true)!;
        if (Array.IndexOf(EndReasons, end) < 0) throw Malformed("end must be sunk, victory or quit");

        var seconds = Num(root, "seconds");
        if (seconds < 0 || seconds > RunService.MaxRunSeconds + MinuteSeconds) throw Malformed("seconds out of range");
        if (Math.Abs(Math.Floor(seconds) - summary.RunSeconds) > 1)
            throw Mismatch($"log seconds {seconds:0.#} disagree with runSeconds {summary.RunSeconds}");
        foreach (var f in new[] { "bossSummoned", "bossDefeated" })
        {
            var t = Num(root, f);
            if (t < -1 || t > seconds + 1) throw Malformed($"{f} out of range");
        }
        if (Int(root, "minuteSeconds") != MinuteSeconds) throw Malformed($"minuteSeconds must be {MinuteSeconds}");

        // weapons
        var weapons = Arr(root, "weapons");
        if (weapons.GetArrayLength() > MaxWeapons) throw Malformed($"more than {MaxWeapons} weapons");
        foreach (var w in weapons.EnumerateArray())
            if (w.ValueKind != JsonValueKind.String || !IdPattern().IsMatch(w.GetString()!)) throw Malformed("weapon ids must be 1-64 characters of A-Z, a-z, 0-9, _ . : -");
        var weaponCount = weapons.GetArrayLength();

        // minutes
        var minutes = Arr(root, "minutes");
        var count = minutes.GetArrayLength();
        var fullMinutes = (int)Math.Floor(seconds / MinuteSeconds);
        var least = Math.Max(1, (int)Math.Ceiling(seconds / MinuteSeconds));
        if (count < least || count > fullMinutes + 1 || count > MaxMinutes)
            throw Malformed($"{count} minutes for {seconds:0.#} seconds (expected {least}..{fullMinutes + 1})");
        long kills = 0;
        var lastLevel = 0;
        var i = 0;
        foreach (var m in minutes.EnumerateArray())
        {
            if (m.ValueKind != JsonValueKind.Object) throw Malformed($"minute {i} must be an object");
            if (Int(m, "m") != i) throw Malformed($"minute {i} has index {Int(m, "m")}");
            foreach (var f in CountFields)
                if (Int(m, f) < 0) throw Malformed($"minute {i} {f} is negative");
            foreach (var f in AmountFields)
            {
                var a = Num(m, f);
                if (a < 0) throw Malformed($"minute {i} {f} is negative");
            }
            var minuteKills = Int(m, "kills");
            if (Int(m, "eliteKills") + Int(m, "bossKills") > minuteKills) throw Malformed($"minute {i}: elite and boss kills exceed kills");
            var level = Int(m, "level");
            if (level < RunService.MinLevel || level > RunService.MaxLevel) throw Malformed($"minute {i} level out of range");
            if (level < lastLevel) throw Malformed($"minute {i}: level went down");
            lastLevel = level;
            var wk = Arr(m, "wk");
            if (wk.GetArrayLength() != weaponCount) throw Malformed($"minute {i}: wk has {wk.GetArrayLength()} entries for {weaponCount} weapons");
            long weaponKills = 0;
            foreach (var k in wk.EnumerateArray())
            {
                if (k.ValueKind != JsonValueKind.Number || !k.TryGetInt32(out var n) || n < 0) throw Malformed($"minute {i}: wk must be counts");
                weaponKills += n;
            }
            if (weaponKills > minuteKills) throw Malformed($"minute {i}: weapon kills {weaponKills} exceed kills {minuteKills}");
            kills += minuteKills;
            i++;
        }
        if (kills != summary.Kills) throw Mismatch($"log kills {kills} disagree with kills {summary.Kills}");
        if (lastLevel != summary.Level) throw Mismatch($"log level {lastLevel} disagrees with level {summary.Level}");

        // inputs
        var every = Num(root, "inputEvery");
        if (every <= 0 || every > 600) throw Malformed("inputEvery out of range");
        var cols = Arr(root, "inputCols");
        if (cols.GetArrayLength() != InputCols.Length || !cols.EnumerateArray().Select(c => c.ValueKind == JsonValueKind.String ? c.GetString() : null).SequenceEqual(InputCols))
            throw Malformed("inputCols must be " + string.Join(",", InputCols));
        var inputs = Arr(root, "inputs");
        if (inputs.GetArrayLength() > MaxInputSamples) throw Malformed($"more than {MaxInputSamples} input samples");
        var lastT = -1.0;
        foreach (var row in inputs.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() != InputCols.Length) throw Malformed("each input sample has " + InputCols.Length + " numbers");
            foreach (var x in row.EnumerateArray())
                if (x.ValueKind != JsonValueKind.Number || !double.IsFinite(x.GetDouble())) throw Malformed("input samples must be numbers");
            var t = row[0].GetDouble();
            if (t < lastT || t > seconds + 1) throw Malformed("input sample times must rise and stay inside the run");
            lastT = t;
        }

        return new Checked(new Header(v, seed, seedSource, configHash!, balanceVersion, buildId, runId), json, bytes);
    }

    static ApiException Bad(string code, string message) => new(400, code, message);
    static ApiException Malformed(string message) => Bad("malformed_log", message + ".");
    static ApiException Mismatch(string message) => Bad("log_mismatch", message + ".");

    static JsonElement Get(JsonElement o, string name)
    {
        if (!o.TryGetProperty(name, out var v)) throw Malformed($"{name} is missing");
        return v;
    }

    static int Int(JsonElement o, string name)
    {
        var v = Get(o, name);
        if (v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var n)) throw Malformed($"{name} must be a whole number");
        return n;
    }

    static double Num(JsonElement o, string name)
    {
        var v = Get(o, name);
        if (v.ValueKind != JsonValueKind.Number) throw Malformed($"{name} must be a number");
        var d = v.GetDouble();
        if (!double.IsFinite(d)) throw Malformed($"{name} must be finite");
        return d;
    }

    static JsonElement Arr(JsonElement o, string name)
    {
        var v = Get(o, name);
        if (v.ValueKind != JsonValueKind.Array) throw Malformed($"{name} must be an array");
        return v;
    }

    /// <summary>A string field (absent or null is null unless required); longer than <paramref name="max"/> fails.</summary>
    static string? Str(JsonElement o, string name, int max, bool required)
    {
        if (!o.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null)
        {
            if (required) throw Malformed($"{name} is missing");
            return null;
        }
        if (v.ValueKind != JsonValueKind.String) throw Malformed($"{name} must be a string");
        var s = v.GetString()!;
        if (s.Length > max) throw Malformed($"{name} is longer than {max} characters");
        if (required && s.Length == 0) throw Malformed($"{name} is empty");
        return s;
    }
}
