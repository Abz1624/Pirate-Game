using System;
using System.Collections.Generic;

namespace RoguePirates.Hackathon
{
    /// <summary>
    /// Hackathon (12 Oct): island defence design helpers the designer and the raid share (the server's rules, see
    /// server/README.md "Island defence designs"; the server stays the judge).
    /// </summary>
    public static class DefenceRules
    {
        public const int Windows = 15;
        public const int WindowSeconds = 60;
        public const int MaxCount = 60;

        /// <summary>A window's cost with the given kind costs (unknown kinds cost 0 here; the server rejects them).</summary>
        public static int Cost(DefenceWindow w, IReadOnlyDictionary<string, int> costs)
        {
            if (w?.enemies == null) return 0;
            var total = 0;
            foreach (var e in w.enemies)
                if (e != null && costs != null && costs.TryGetValue(e.kind ?? string.Empty, out var c)) total += c * Math.Max(0, e.count);
            return total;
        }

        /// <summary>A deep copy of <paramref name="source"/> with all 15 windows present (minute order) and a guardian.</summary>
        public static DefenceDesign Normalize(DefenceDesign source, string defaultGuardian = "barnacle_king", int defaultMinute = 5)
        {
            var d = new DefenceDesign { windows = new DefenceWindow[Windows] };
            for (var m = 1; m <= Windows; m++)
            {
                DefenceWindow from = null;
                if (source?.windows != null)
                    foreach (var w in source.windows) if (w != null && w.minute == m) { from = w; break; }
                var list = new List<DefenceEnemy>();
                if (from?.enemies != null)
                    foreach (var e in from.enemies)
                        if (e != null && !string.IsNullOrEmpty(e.kind) && e.count > 0) list.Add(new DefenceEnemy { kind = e.kind, count = e.count });
                d.windows[m - 1] = new DefenceWindow { minute = m, startSecond = (m - 1) * WindowSeconds, enemies = list.ToArray(), budget = from != null ? from.budget : 0 };
            }
            var g = source?.guardian;
            d.guardian = new DefenceGuardian
            {
                kind = string.IsNullOrEmpty(g?.kind) ? defaultGuardian : g.kind,
                displayName = g?.displayName,
                minute = g != null && g.minute >= 2 && g.minute <= Windows ? g.minute : defaultMinute,
            };
            d.guardian.arrivesAtSecond = (d.guardian.minute - 1) * WindowSeconds;
            return d;
        }

        /// <summary>Adds <paramref name="delta"/> of <paramref name="kind"/> to a window (0..60; a kind at 0 is removed).</summary>
        public static void Add(DefenceWindow w, string kind, int delta)
        {
            var list = new List<DefenceEnemy>(w.enemies ?? Array.Empty<DefenceEnemy>());
            var e = list.Find(x => x.kind == kind);
            if (e == null) { if (delta <= 0) return; e = new DefenceEnemy { kind = kind, count = 0 }; list.Add(e); }
            e.count = Math.Max(0, Math.Min(MaxCount, e.count + delta));
            if (e.count == 0) list.Remove(e);
            w.enemies = list.ToArray();
        }

        /// <summary>The design the PUT sends: windows with enemies only, no zero counts.</summary>
        public static DefenceDesign ForSave(DefenceDesign d)
        {
            var windows = new List<DefenceWindow>();
            foreach (var w in d.windows)
            {
                if (w?.enemies == null || w.enemies.Length == 0) continue;
                windows.Add(new DefenceWindow { minute = w.minute, enemies = w.enemies });
            }
            return new DefenceDesign { windows = windows.ToArray(), guardian = new DefenceGuardian { kind = d.guardian.kind, minute = d.guardian.minute } };
        }

        /// <summary>The window holding second <paramref name="seconds"/> of the raid (1..15; 15 past the end).</summary>
        public static int MinuteAt(double seconds) => Math.Max(1, Math.Min(Windows, (int)(seconds / WindowSeconds) + 1));

        public static string RoleName(string role)
        {
            switch (role)
            {
                case "fodder": return "Fodder";
                case "melee": return "Melee";
                case "shooter": return "Shooters";
                case "bomber": return "Bombers";
                case "heavy": return "Heavy";
                case "elite": return "Elites";
                case "miniboss": return "Miniboss";
                default: return string.IsNullOrEmpty(role) ? "Other" : char.ToUpperInvariant(role[0]) + role.Substring(1);
            }
        }

        /// <summary>A server problem as a readable line ("Minute 3: over budget (40 of 32 points)").</summary>
        public static string Problem(DefenceProblem p)
        {
            if (p == null) return string.Empty;
            var where = p.minute > 0 && (p.message == null || !p.message.StartsWith("Minute")) ? $"Minute {p.minute}: " : string.Empty;
            return where + (string.IsNullOrEmpty(p.message) ? (p.code ?? string.Empty).Replace('_', ' ') : p.message.Replace(((char)0x2014).ToString(), ", ").Replace(((char)0x2013).ToString(), "-"));
        }
    }
}
