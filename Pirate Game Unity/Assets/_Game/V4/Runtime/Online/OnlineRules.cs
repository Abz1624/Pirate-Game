using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RoguePirates.V4.Online
{
    /// <summary>
    /// Playtest (3 Oct, owner): the pure rules of the online client (no Unity calls, EditMode tested): profile names as the
    /// API accepts them, the weekly reset, the build hash sent with a run, the API base URL resolution and time labels.
    /// </summary>
    public static class OnlineRules
    {
        public const int NameMin = 3;
        public const int NameMax = 16;

        /// <summary>The name as the API stores it (trimmed), or null with a player-facing reason when it would be refused
        /// (3-16 letters, digits, space, _ or -; the server also has a small block list).</summary>
        public static string CleanName(string raw, out string error)
        {
            error = null;
            var name = (raw ?? string.Empty).Trim();
            if (name.Length < NameMin || name.Length > NameMax)
            {
                error = $"Names are {NameMin} to {NameMax} characters.";
                return null;
            }
            foreach (var c in name)
            {
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-') continue;
                error = "Use letters, digits, spaces, _ or - only.";
                return null;
            }
            return name;
        }

        /// <summary>A default guest name: "Captain" and four digits.</summary>
        public static string DefaultName(int seed) => "Captain" + (Math.Abs(seed) % 9000 + 1000).ToString(CultureInfo.InvariantCulture);

        /// <summary>The next weekly reset after <paramref name="utcNow"/>: Monday 00:00 UTC (the API's ISO week).</summary>
        public static DateTime NextWeeklyReset(DateTime utcNow)
        {
            var day = utcNow.Date;
            var daysToMonday = ((int)DayOfWeek.Monday - (int)day.DayOfWeek + 7) % 7;
            if (daysToMonday == 0) daysToMonday = 7;
            return DateTime.SpecifyKind(day.AddDays(daysToMonday), DateTimeKind.Utc);
        }

        /// <summary>"2d 4h 12m" (or "4h 12m 9s" under a day) until <paramref name="target"/>.</summary>
        public static string Countdown(DateTime utcNow, DateTime target)
        {
            var left = target - utcNow;
            if (left < TimeSpan.Zero) left = TimeSpan.Zero;
            return left.TotalDays >= 1
                ? $"{(int)left.TotalDays}d {left.Hours}h {left.Minutes}m"
                : $"{left.Hours}h {left.Minutes}m {left.Seconds}s";
        }

        /// <summary>Run time as m:ss (h:mm:ss from an hour).</summary>
        public static string RunTime(int seconds)
        {
            if (seconds < 0) seconds = 0;
            return seconds >= 3600
                ? $"{seconds / 3600}:{seconds / 60 % 60:00}:{seconds % 60:00}"
                : $"{seconds / 60}:{seconds % 60:00}";
        }

        /// <summary>An ISO 8601 UTC time from the API, or null.</summary>
        public static DateTime? ParseUtc(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return null;
            return DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t)
                ? DateTime.SpecifyKind(t, DateTimeKind.Utc) : (DateTime?)null;
        }

        /// <summary>A short stable hash of the build (weapon and logbook ids with levels, order independent): FNV-1a 64 in hex.</summary>
        public static string BuildHash(IEnumerable<string> parts)
        {
            var list = new List<string>();
            if (parts != null) foreach (var p in parts) if (!string.IsNullOrEmpty(p)) list.Add(p);
            list.Sort(StringComparer.Ordinal);
            var hash = 14695981039346656037UL;
            foreach (var b in Encoding.UTF8.GetBytes(string.Join("|", list)))
            {
                hash ^= b;
                hash *= 1099511628211UL;
            }
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        /// <summary>A query parameter of a page URL (unescaped), or null.</summary>
        public static string QueryValue(string url, string key)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key)) return null;
            var q = url.IndexOf('?');
            if (q < 0) return null;
            var hash = url.IndexOf('#', q);
            var query = hash < 0 ? url.Substring(q + 1) : url.Substring(q + 1, hash - q - 1);
            foreach (var pair in query.Split('&'))
            {
                var eq = pair.IndexOf('=');
                var k = eq < 0 ? pair : pair.Substring(0, eq);
                if (!string.Equals(Uri.UnescapeDataString(k), key, StringComparison.OrdinalIgnoreCase)) continue;
                return eq < 0 ? string.Empty : Uri.UnescapeDataString(pair.Substring(eq + 1).Replace('+', ' '));
            }
            return null;
        }

        /// <summary>A usable http(s) base URL without the trailing slash, or null.</summary>
        public static string NormalizeBaseUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            url = url.Trim().TrimEnd('/');
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
            return url;
        }

        /// <summary>Whether a host matches one of the patterns ("localhost", "*.roguepirates.fun" also matches the bare domain).</summary>
        public static bool HostAllowed(string url, IEnumerable<string> patterns)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || patterns == null) return false;
            var host = uri.Host.ToLowerInvariant();
            foreach (var raw in patterns)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var p = raw.Trim().ToLowerInvariant();
                if (p.StartsWith("*.", StringComparison.Ordinal))
                {
                    var bare = p.Substring(2);
                    if (host == bare || host.EndsWith("." + bare, StringComparison.Ordinal)) return true;
                }
                else if (host == p) return true;
            }
            return false;
        }

        /// <summary>
        /// The API base URL: the page's ?api= (WebGL; any host in the Editor and development builds, the allowed hosts in
        /// release builds, so a shared link cannot send tokens elsewhere), then the PlayerPrefs override, then the Editor
        /// URL in the Editor (and development builds when asked), else the production URL. Null = offline.
        /// </summary>
        public static string ResolveBaseUrl(string queryOverride, string prefsOverride, bool editor, bool developmentBuild,
            bool devBuildsUseEditorUrl, string editorUrl, string productionUrl, IEnumerable<string> allowedQueryHosts)
        {
            var q = NormalizeBaseUrl(queryOverride);
            if (q != null && (editor || developmentBuild || HostAllowed(q, allowedQueryHosts))) return q;
            var p = NormalizeBaseUrl(prefsOverride);
            if (p != null) return p;
            if (editor || (developmentBuild && devBuildsUseEditorUrl))
            {
                var e = NormalizeBaseUrl(editorUrl);
                if (e != null) return e;
            }
            return NormalizeBaseUrl(productionUrl);
        }
    }
}
