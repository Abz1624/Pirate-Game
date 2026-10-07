using System.Globalization;
using System.Text;

namespace RoguePirates.V4.Telemetry
{
    /// <summary>
    /// The JSON writing the run's telemetry files share (the session file, the run log): invariant culture, short floats,
    /// NaN and infinity as null, escaped strings. V5 M9a (I9a.1): moved out of <see cref="SessionRecorder"/> so the run log
    /// writes the same way.
    /// </summary>
    internal static class TelemetryJson
    {
        public static void Key(StringBuilder sb, string key, bool first)
        {
            if (!first) sb.Append(',');
            sb.Append('"').Append(key).Append("\":");
        }

        public static void Str(StringBuilder sb, string key, string value, bool first = false)
        {
            Key(sb, key, first);
            if (value == null) sb.Append("null");
            else sb.Append('"').Append(Esc(value)).Append('"');
        }

        public static void Num(StringBuilder sb, string key, float value, bool first = false)
        {
            Key(sb, key, first);
            Num(sb, value, "0.###");
        }

        /// <summary>A bare number with the given format (null for NaN or infinity).</summary>
        public static void Num(StringBuilder sb, float value, string format)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) sb.Append("null");
            else sb.Append(value.ToString(format, CultureInfo.InvariantCulture));
        }

        public static void Int(StringBuilder sb, string key, int value, bool first = false)
        {
            Key(sb, key, first);
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        public static void Long(StringBuilder sb, string key, long value, bool first = false)
        {
            Key(sb, key, first);
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        public static void Bool(StringBuilder sb, string key, bool value, bool first = false)
        {
            Key(sb, key, first);
            sb.Append(value ? "true" : "false");
        }

        public static string Esc(string s)
        {
            var needs = false;
            foreach (var c in s)
                if (c == '"' || c == '\\' || c < 0x20) { needs = true; break; }
            if (!needs) return s;
            var sb = new StringBuilder(s.Length + 8);
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
