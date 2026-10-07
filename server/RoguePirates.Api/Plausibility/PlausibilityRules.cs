using System.Reflection;
using System.Text.Json;

namespace RoguePirates.Api.Plausibility;

/// <summary>The verdicts a run can get (V5 M9a, I9a.3).</summary>
public static class Verdicts
{
    /// <summary>Every rule held: shown as "checked" on the weekly board.</summary>
    public const string Pass = "pass";
    /// <summary>At least one rule broke. Shadow mode (D23, D24): the run still counts and still pays out.</summary>
    public const string Flagged = "flagged";
    /// <summary>No log, another log version, or a config the rules were not made for (an older or newer build).</summary>
    public const string OutdatedClient = "outdated_client";
}

/// <summary>One broken rule: its reason code, the rule id, and where it broke (the minute index, the value and the bound).</summary>
public sealed record PlausibilityReason(string Code, string Rule, int? Minute = null, double? Value = null, double? Bound = null);

/// <summary>A run's verdict under one version of the rules.</summary>
public sealed record PlausibilityVerdict(string Status, IReadOnlyList<PlausibilityReason> Reasons, int RulesVersion)
{
    public bool Checked => Status == Verdicts.Pass;
}

/// <summary>One rule of the file (fields by kind: bounds for minute, total and level; margin, tolerance, ledger and overtime settings).</summary>
public sealed class PlausibilityRule
{
    public string Id { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Field { get; init; } = "";
    public string Reason { get; init; } = "";
    public double[] Bounds { get; init; } = Array.Empty<double>();
    public double Margin { get; init; }
    public double Tolerance { get; init; }
    public double Absolute { get; init; }
    public double FractionOfMaxHull { get; init; }
    public double Deadline { get; init; }
    public double Bound { get; init; }

    public double BoundAt(int minute) => Bounds.Length == 0 ? double.PositiveInfinity : Bounds[Math.Min(minute, Bounds.Length - 1)];
}

/// <summary>
/// V5 M9a (I9a.3): the plausibility rules (format <c>rp-plausibility-rules</c>), generated from the game's config and the refit
/// simulator by <c>plan/playtest/montecarlo/plausibility.mjs</c> and embedded in the API (Plausibility/plausibility-rules.json;
/// the game's EditMode tests read the same file). Upper bounds on a run's output only: per minute and per run (kills, XP,
/// gold, damage dealt, chests, points of interest), the level each minute, the level against the XP, kills against
/// spawns, the hull against damage taken and heals, and the time survived in overtime. <see cref="Evaluate"/> is the one
/// checker; the generator's <c>evaluate</c> and the game's test copy do the same.
/// </summary>
public sealed class PlausibilityRuleSet
{
    public const string FileFormat = "rp-plausibility-rules";
    const string ResourceName = "RoguePirates.Api.plausibility-rules.json";

    public int RulesVersion { get; init; }
    public string Mode { get; init; } = "shadow";
    public int LogVersion { get; init; }
    public string ConfigHash { get; init; } = "";
    public IReadOnlyList<string> ConfigHashes { get; init; } = Array.Empty<string>();
    public int[] XpToNext { get; init; } = Array.Empty<int>();
    public int StartLevel { get; init; }
    public IReadOnlyList<PlausibilityRule> Rules { get; init; } = Array.Empty<PlausibilityRule>();

    /// <summary>The rules shipped with this API (the embedded file).</summary>
    public static PlausibilityRuleSet LoadEmbedded()
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
                      ?? throw new InvalidOperationException($"embedded resource {ResourceName} is missing");
        using var r = new StreamReader(s);
        return Parse(r.ReadToEnd());
    }

    public static PlausibilityRuleSet Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (Str(root, "format") != FileFormat) throw new FormatException($"not an {FileFormat} file");
        var rules = new List<PlausibilityRule>();
        foreach (var r in root.GetProperty("rules").EnumerateArray())
            rules.Add(new PlausibilityRule
            {
                Id = Str(r, "id") ?? "", Kind = Str(r, "kind") ?? "", Field = Str(r, "field") ?? "", Reason = Str(r, "reason") ?? "",
                Bounds = r.TryGetProperty("bounds", out var b) ? b.EnumerateArray().Select(x => x.GetDouble()).ToArray() : Array.Empty<double>(),
                Margin = Num(r, "margin"), Tolerance = Num(r, "tolerance"), Absolute = Num(r, "absolute"), FractionOfMaxHull = Num(r, "fractionOfMaxHull"),
                Deadline = Num(r, "deadline"), Bound = Num(r, "bound"),
            });
        var set = new PlausibilityRuleSet
        {
            RulesVersion = root.GetProperty("rulesVersion").GetInt32(),
            Mode = Str(root, "mode") ?? "shadow",
            LogVersion = root.GetProperty("logVersion").GetInt32(),
            ConfigHash = Str(root, "configHash") ?? "",
            ConfigHashes = root.GetProperty("configHashes").EnumerateArray().Select(x => x.GetString() ?? "").ToList(),
            XpToNext = root.GetProperty("xpToNext").EnumerateArray().Select(x => x.GetInt32()).ToArray(),
            StartLevel = root.TryGetProperty("startLevel", out var sl) ? sl.GetInt32() : 0,
            Rules = rules,
        };
        if (set.RulesVersion < 1 || set.XpToNext.Length == 0 || rules.Count == 0) throw new FormatException("rules file is incomplete");
        return set;
    }

    static string? Str(JsonElement o, string name) => o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    static double Num(JsonElement o, string name) => o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;

    // ---- XP and level (ProgressionMath: the table, then its last steps continued) --------------------------------------

    public long XpToNextAt(int level)
    {
        var t = XpToNext;
        if (level < t.Length) return t[level];
        var n = t.Length;
        var k = Math.Min(4, n - 1);
        var step = Math.Max(1, (long)Math.Round((t[n - 1] - t[n - 1 - k]) / (double)k, MidpointRounding.AwayFromZero));
        return t[n - 1] + step * (level - n + 1);
    }

    /// <summary>The level a run's total XP reaches from the start level.</summary>
    public int LevelFromXp(double totalXp)
    {
        var level = StartLevel;
        var left = totalXp;
        while (level < 1000)
        {
            var need = XpToNextAt(level);
            if (left < need) break;
            left -= need;
            level++;
        }
        return level;
    }

    // ---- the checker ----------------------------------------------------------------------------------------------------

    sealed class Minute
    {
        public double Spawns, Kills, Xp, Gold, Dealt, Chests, Pois, Level, Taken, Healed, Hull, MaxHull;

        public double Get(string field) => field switch
        {
            "kills" => Kills, "xp" => Xp, "gold" => Gold, "dealt" => Dealt, "chests" => Chests, "pois" => Pois, "spawns" => Spawns, "level" => Level,
            _ => 0,
        };
    }

    static readonly string[] Totals = { "kills", "xp", "gold", "dealt", "chests", "pois", "spawns" };

    /// <summary>
    /// The verdict of a run log (its JSON as stored; null when the client sent none). A log the API stored has passed the
    /// structural checks (RunLogRules); anything unreadable here is treated as outdated, never as an error.
    /// <paramref name="checkConfig"/> false checks the bounds whatever config the log names (tests of other builds' logs).
    /// </summary>
    public PlausibilityVerdict Evaluate(string? logJson, bool checkConfig = true)
    {
        if (string.IsNullOrEmpty(logJson)) return Outdated("no_log", "PR-VERSION");
        JsonDocument doc;
        try { doc = JsonDocument.Parse(logJson); }
        catch (JsonException) { return Outdated("log_unreadable", "PR-VERSION"); }
        using (doc)
        {
            var root = doc.RootElement;
            if (!root.TryGetProperty("v", out var v) || !v.TryGetInt32(out var version) || version != LogVersion) return Outdated("log_version", "PR-VERSION");
            var config = root.TryGetProperty("configHash", out var ch) && ch.ValueKind == JsonValueKind.String ? ch.GetString() : null;
            if (checkConfig && (config == null || !ConfigHashes.Contains(config))) return Outdated("config_not_in_rules", "PR-CONFIG");
            var seconds = root.TryGetProperty("seconds", out var se) && se.ValueKind == JsonValueKind.Number ? se.GetDouble() : 0;
            var minutes = new List<Minute>();
            if (root.TryGetProperty("minutes", out var ms) && ms.ValueKind == JsonValueKind.Array)
                foreach (var m in ms.EnumerateArray())
                    minutes.Add(new Minute
                    {
                        Spawns = N(m, "spawns"), Kills = N(m, "kills"), Xp = N(m, "xp"), Gold = N(m, "gold"), Dealt = N(m, "dealt"), Chests = N(m, "chests"),
                        Pois = N(m, "pois"), Level = N(m, "level"), Taken = N(m, "taken"), Healed = N(m, "healed"), Hull = N(m, "hull"), MaxHull = N(m, "maxHull"),
                    });
            return Check(minutes, seconds);
        }
    }

    static double N(JsonElement o, string name) => o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;

    PlausibilityVerdict Outdated(string code, string rule) =>
        new(Verdicts.OutdatedClient, new[] { new PlausibilityReason(code, rule) }, RulesVersion);

    PlausibilityVerdict Check(List<Minute> minutes, double seconds)
    {
        var reasons = new List<PlausibilityReason>();
        var hit = new HashSet<string>();
        void Flag(PlausibilityRule r, int minute, double value, double bound)
        {
            if (hit.Add(r.Id)) reasons.Add(new PlausibilityReason(r.Reason, r.Id, minute, Math.Round(value, 1), Math.Round(bound, 1)));
        }
        var run = Totals.ToDictionary(f => f, _ => 0.0);
        for (var i = 0; i < minutes.Count; i++)
        {
            var m = minutes[i];
            foreach (var f in Totals) run[f] += m.Get(f);
            var prev = i > 0 ? minutes[i - 1] : null;
            foreach (var r in Rules)
            {
                switch (r.Kind)
                {
                    case "minute":
                        if (m.Get(r.Field) > r.BoundAt(i)) Flag(r, i, m.Get(r.Field), r.BoundAt(i));
                        break;
                    case "total":
                        if (run.TryGetValue(r.Field, out var sum) && sum > r.BoundAt(i)) Flag(r, i, sum, r.BoundAt(i));
                        break;
                    case "level":
                        if (m.Level > r.BoundAt(i)) Flag(r, i, m.Level, r.BoundAt(i));
                        break;
                    case "levelFromXp":
                    {
                        var allowed = Math.Max(1, LevelFromXp(run["xp"])) + r.Tolerance;
                        if (m.Level > allowed) Flag(r, i, m.Level, allowed);
                        break;
                    }
                    case "killsWithinSpawns":
                        if (run["kills"] > run["spawns"] + r.Margin) Flag(r, i, run["kills"], run["spawns"] + r.Margin);
                        break;
                    case "hullLedger":
                        if (prev != null)
                        {
                            var expected = Math.Max(0, Math.Min(m.MaxHull, prev.Hull + m.Healed - m.Taken + Math.Max(0, m.MaxHull - prev.MaxHull)));
                            var allowed = expected + r.Absolute + r.FractionOfMaxHull * m.MaxHull;
                            if (m.Hull > allowed) Flag(r, i, m.Hull, allowed);
                        }
                        break;
                }
            }
        }
        foreach (var r in Rules)
            if (r.Kind == "overtimeSurvival" && seconds - r.Deadline > r.Bound) Flag(r, Math.Max(0, minutes.Count - 1), seconds - r.Deadline, r.Bound);
        return new PlausibilityVerdict(reasons.Count > 0 ? Verdicts.Flagged : Verdicts.Pass, reasons, RulesVersion);
    }
}
