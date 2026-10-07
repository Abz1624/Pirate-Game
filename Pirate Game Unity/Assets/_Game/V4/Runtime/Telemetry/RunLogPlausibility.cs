using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoguePirates.V4.Telemetry
{
    /// <summary>
    /// V5 M9a (I9a.3): the game's copy of the run checks (plausibility rules v1, shadow mode). The rules file is
    /// server/RoguePirates.Api/Plausibility/plausibility-rules.json, generated from the game's config and the refit simulator
    /// by plan/playtest/montecarlo/plausibility.mjs; the API embeds it and its PlausibilityRuleSet is the authority. This copy
    /// evaluates a run log the same way (the tests check that the three agree on the recorded voyages), so the game can
    /// check its own logs in tests and, later, show a local "checked" hint. It never blocks or changes anything.
    /// </summary>
    public static class RunLogPlausibility
    {
        public const string Pass = "pass", Flagged = "flagged", OutdatedClient = "outdated_client";

        [Serializable]
        public sealed class Rule
        {
            public string id, kind, field, reason;
            public double[] bounds;
            public double margin, tolerance, absolute, fractionOfMaxHull, deadline, bound;

            public double BoundAt(int minute) => bounds == null || bounds.Length == 0 ? double.PositiveInfinity : bounds[Math.Min(minute, bounds.Length - 1)];
        }

        [Serializable]
        public sealed class Rules
        {
            public string format, mode, configHash;
            public int rulesVersion, logVersion, startLevel;
            public string[] configHashes;
            public int[] xpToNext;
            public Rule[] rules;
        }

        [Serializable]
        sealed class Minute
        {
            public double spawns, kills, xp, gold, dealt, chests, pois, level, taken, healed, hull, maxHull;

            public double Get(string field)
            {
                switch (field)
                {
                    case "kills": return kills;
                    case "xp": return xp;
                    case "gold": return gold;
                    case "dealt": return dealt;
                    case "chests": return chests;
                    case "pois": return pois;
                    case "spawns": return spawns;
                    case "level": return level;
                    default: return 0;
                }
            }
        }

        [Serializable]
        sealed class Log
        {
            public int v;
            public string configHash;
            public double seconds;
            public Minute[] minutes;
        }

        public struct Reason
        {
            public string Code, Rule;
            public int Minute;
            public double Value, Bound;
            public override string ToString() => $"{Code} [{Rule}] minute {Minute}: {Value:0.#} > {Bound:0.#}";
        }

        public sealed class Verdict
        {
            public string Status;
            public int RulesVersion;
            public readonly List<Reason> Reasons = new List<Reason>();
            public bool Checked => Status == Pass;
            public override string ToString() => Status + (Reasons.Count > 0 ? ": " + string.Join(", ", Reasons) : "");
        }

        public static Rules ParseRules(string json)
        {
            var r = JsonUtility.FromJson<Rules>(json);
            if (r == null || r.format != "rp-plausibility-rules" || r.rules == null || r.xpToNext == null || r.xpToNext.Length == 0)
                throw new FormatException("not an rp-plausibility-rules file");
            return r;
        }

        static readonly string[] Totals = { "kills", "xp", "gold", "dealt", "chests", "pois", "spawns" };

        /// <summary>The XP to the next level (the table, then its last steps continued, as ProgressionMath).</summary>
        public static long XpToNext(Rules rules, int level)
        {
            var t = rules.xpToNext;
            if (level < t.Length) return t[level];
            var n = t.Length;
            var k = Math.Min(4, n - 1);
            var step = Math.Max(1L, (long)Math.Round((t[n - 1] - t[n - 1 - k]) / (double)k, MidpointRounding.AwayFromZero));
            return t[n - 1] + step * (level - n + 1);
        }

        public static int LevelFromXp(Rules rules, double totalXp)
        {
            var level = rules.startLevel;
            var left = totalXp;
            while (level < 1000)
            {
                var need = XpToNext(rules, level);
                if (left < need) break;
                left -= need;
                level++;
            }
            return level;
        }

        /// <summary>The verdict of a run log (its JSON, as RunLogBuilder writes it). <paramref name="checkConfig"/> false
        /// checks the bounds whatever config the log names.</summary>
        public static Verdict Evaluate(Rules rules, string logJson, bool checkConfig = true)
        {
            var verdict = new Verdict { Status = Pass, RulesVersion = rules.rulesVersion };
            Log log = null;
            if (!string.IsNullOrEmpty(logJson))
            {
                try { log = JsonUtility.FromJson<Log>(logJson); }
                catch (ArgumentException) { log = null; }
            }
            if (log == null) return Outdated(verdict, "no_log", "PR-VERSION");
            if (log.v != rules.logVersion) return Outdated(verdict, "log_version", "PR-VERSION");
            if (checkConfig && (log.configHash == null || Array.IndexOf(rules.configHashes ?? Array.Empty<string>(), log.configHash) < 0))
                return Outdated(verdict, "config_not_in_rules", "PR-CONFIG");

            var hit = new HashSet<string>();
            void Flag(Rule r, int minute, double value, double bound)
            {
                if (hit.Add(r.id)) verdict.Reasons.Add(new Reason { Code = r.reason, Rule = r.id, Minute = minute, Value = Math.Round(value, 1), Bound = Math.Round(bound, 1) });
            }
            var run = new Dictionary<string, double>();
            foreach (var f in Totals) run[f] = 0;
            var minutes = log.minutes ?? Array.Empty<Minute>();
            for (var i = 0; i < minutes.Length; i++)
            {
                var m = minutes[i];
                foreach (var f in Totals) run[f] += m.Get(f);
                var prev = i > 0 ? minutes[i - 1] : null;
                foreach (var r in rules.rules)
                {
                    switch (r.kind)
                    {
                        case "minute":
                            if (m.Get(r.field) > r.BoundAt(i)) Flag(r, i, m.Get(r.field), r.BoundAt(i));
                            break;
                        case "total":
                            if (run.TryGetValue(r.field, out var sum) && sum > r.BoundAt(i)) Flag(r, i, sum, r.BoundAt(i));
                            break;
                        case "level":
                            if (m.level > r.BoundAt(i)) Flag(r, i, m.level, r.BoundAt(i));
                            break;
                        case "levelFromXp":
                        {
                            var allowed = Math.Max(1, LevelFromXp(rules, run["xp"])) + r.tolerance;
                            if (m.level > allowed) Flag(r, i, m.level, allowed);
                            break;
                        }
                        case "killsWithinSpawns":
                            if (run["kills"] > run["spawns"] + r.margin) Flag(r, i, run["kills"], run["spawns"] + r.margin);
                            break;
                        case "hullLedger":
                            if (prev != null)
                            {
                                var expected = Math.Max(0, Math.Min(m.maxHull, prev.hull + m.healed - m.taken + Math.Max(0, m.maxHull - prev.maxHull)));
                                var allowed = expected + r.absolute + r.fractionOfMaxHull * m.maxHull;
                                if (m.hull > allowed) Flag(r, i, m.hull, allowed);
                            }
                            break;
                    }
                }
            }
            foreach (var r in rules.rules)
                if (r.kind == "overtimeSurvival" && log.seconds - r.deadline > r.bound) Flag(r, Math.Max(0, minutes.Length - 1), log.seconds - r.deadline, r.bound);
            if (verdict.Reasons.Count > 0) verdict.Status = Flagged;
            return verdict;
        }

        static Verdict Outdated(Verdict v, string code, string rule)
        {
            v.Status = OutdatedClient;
            v.Reasons.Add(new Reason { Code = code, Rule = rule, Minute = -1 });
            return v;
        }
    }
}
