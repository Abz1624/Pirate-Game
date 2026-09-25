using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RoguePirates.V4.DevTools.Trace
{
    /// <summary>
    /// Compares two JSONL traces. Exact mode: every event line (header excluded, frame numbers ignored) must match in order.
    /// Parity mode: per-minute spawn counts by enemy id and kill counts within a relative tolerance, plus level-up,
    /// boss and death times within an absolute tolerance.
    /// </summary>
    public static class TraceComparer
    {
        public sealed class Result
        {
            public bool Match;
            public int LinesCompared;
            public readonly List<string> Differences = new List<string>();
            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.AppendLine(Match ? $"MATCH ({LinesCompared} lines)" : $"DIFFERENT ({Differences.Count} differences, {LinesCompared} lines)");
                for (var i = 0; i < Differences.Count && i < 20; i++) sb.AppendLine(Differences[i]);
                return sb.ToString();
            }
        }

        static readonly Regex FrameField = new Regex(",\"f\":-?\\d+", RegexOptions.Compiled);
        static readonly Regex TypeField = new Regex("\"type\":\"([a-zA-Z]+)\"", RegexOptions.Compiled);
        static readonly HashSet<string> IgnoredTypes = new HashSet<string> { "header" };

        public static string Normalize(string line) => FrameField.Replace(line ?? string.Empty, string.Empty);

        public static string TypeOf(string line)
        {
            var m = TypeField.Match(line ?? string.Empty);
            return m.Success ? m.Groups[1].Value : string.Empty;
        }

        public static Result CompareExact(IReadOnlyList<string> expected, IReadOnlyList<string> actual)
        {
            var a = Filter(expected);
            var b = Filter(actual);
            var result = new Result();
            var n = Math.Min(a.Count, b.Count);
            for (var i = 0; i < n; i++)
            {
                result.LinesCompared++;
                if (a[i] == b[i]) continue;
                result.Differences.Add($"line {i}: expected {a[i]}\n          actual   {b[i]}");
                if (result.Differences.Count >= 50) break;
            }

            if (a.Count != b.Count)
                result.Differences.Add($"length: expected {a.Count} event lines, actual {b.Count}");
            result.Match = result.Differences.Count == 0;
            return result;
        }

        public static Result CompareParity(IReadOnlyList<string> expected, IReadOnlyList<string> actual,
            float minuteRelativeTolerance = 0.05f, float eventSecondsTolerance = 3f)
        {
            var result = new Result();
            var ma = Minutes(expected);
            var mb = Minutes(actual);
            var minutes = new SortedSet<int>(ma.Keys);
            minutes.UnionWith(mb.Keys);
            foreach (var m in minutes)
            {
                result.LinesCompared++;
                ma.TryGetValue(m, out var ea);
                mb.TryGetValue(m, out var eb);
                ea ??= new Dictionary<string, int>();
                eb ??= new Dictionary<string, int>();
                var keys = new SortedSet<string>(ea.Keys);
                keys.UnionWith(eb.Keys);
                foreach (var k in keys)
                {
                    ea.TryGetValue(k, out var x);
                    eb.TryGetValue(k, out var y);
                    var allowed = Math.Max(1f, Math.Max(x, y) * minuteRelativeTolerance);
                    if (Math.Abs(x - y) > allowed)
                        result.Differences.Add($"minute {m} {k}: expected {x}, actual {y}");
                }
            }

            foreach (var type in new[] { "level", "boss", "death" })
            {
                var ta = Times(expected, type);
                var tb = Times(actual, type);
                if (ta.Count != tb.Count)
                    result.Differences.Add($"{type}: expected {ta.Count} events, actual {tb.Count}");
                for (var i = 0; i < Math.Min(ta.Count, tb.Count); i++)
                    if (Math.Abs(ta[i] - tb[i]) > eventSecondsTolerance)
                        result.Differences.Add($"{type} #{i}: expected t={ta[i]:0.##}, actual t={tb[i]:0.##}");
            }

            result.Match = result.Differences.Count == 0;
            return result;
        }

        static List<string> Filter(IReadOnlyList<string> lines)
        {
            var list = new List<string>(lines.Count);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (IgnoredTypes.Contains(TypeOf(line))) continue;
                list.Add(Normalize(line));
            }
            return list;
        }

        static readonly Regex MinuteIndex = new Regex("\"m\":(\\d+)", RegexOptions.Compiled);
        static readonly Regex SpawnBlock = new Regex("\"spawns\":\\{([^}]*)\\}", RegexOptions.Compiled);
        static readonly Regex Pair = new Regex("\"([^\"]+)\":(\\d+)", RegexOptions.Compiled);
        static readonly Regex KillsField = new Regex("\"kills\":(\\d+)", RegexOptions.Compiled);
        static readonly Regex TimeField = new Regex("\"t\":(-?[0-9.]+)", RegexOptions.Compiled);

        static Dictionary<int, Dictionary<string, int>> Minutes(IReadOnlyList<string> lines)
        {
            var map = new Dictionary<int, Dictionary<string, int>>();
            foreach (var line in lines)
            {
                if (TypeOf(line) != "minute") continue;
                var m = int.Parse(MinuteIndex.Match(line).Groups[1].Value, CultureInfo.InvariantCulture);
                var counts = new Dictionary<string, int>();
                var block = SpawnBlock.Match(line);
                if (block.Success)
                    foreach (Match p in Pair.Matches(block.Groups[1].Value))
                        counts["spawn:" + p.Groups[1].Value] = int.Parse(p.Groups[2].Value, CultureInfo.InvariantCulture);
                var withoutSpawns = SpawnBlock.Replace(line, string.Empty);
                var kills = KillsField.Match(withoutSpawns);
                if (kills.Success) counts["kills"] = int.Parse(kills.Groups[1].Value, CultureInfo.InvariantCulture);
                map[m] = counts;
            }
            return map;
        }

        static List<float> Times(IReadOnlyList<string> lines, string type)
        {
            var list = new List<float>();
            foreach (var line in lines)
            {
                if (TypeOf(line) != type) continue;
                var t = TimeField.Match(line);
                if (t.Success) list.Add(float.Parse(t.Groups[1].Value, CultureInfo.InvariantCulture));
            }
            return list;
        }
    }
}
