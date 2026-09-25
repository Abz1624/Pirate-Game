using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RoguePirates.V4.DevTools.Trace
{
    /// <summary>Tiny builder for one flat JSON object per trace line (invariant culture, stable key order).</summary>
    public sealed class JsonLine
    {
        readonly StringBuilder _sb = new StringBuilder(128);
        bool _first = true;

        public JsonLine(string type) { _sb.Append('{'); Str("type", type); }

        void Key(string key)
        {
            if (!_first) _sb.Append(',');
            _first = false;
            _sb.Append('"').Append(key).Append("\":");
        }

        public JsonLine Str(string key, string value)
        {
            Key(key);
            if (value == null) { _sb.Append("null"); return this; }
            _sb.Append('"');
            foreach (var c in value)
            {
                if (c == '"' || c == '\\') _sb.Append('\\');
                if (c < ' ') { _sb.Append(' '); continue; }
                _sb.Append(c);
            }
            _sb.Append('"');
            return this;
        }

        public JsonLine Int(string key, long value) { Key(key); _sb.Append(value.ToString(CultureInfo.InvariantCulture)); return this; }

        public JsonLine Num(string key, float value)
        {
            Key(key);
            _sb.Append(value.ToString("0.####", CultureInfo.InvariantCulture));
            return this;
        }

        public JsonLine Bool(string key, bool value) { Key(key); _sb.Append(value ? "true" : "false"); return this; }

        public JsonLine Raw(string key, string json) { Key(key); _sb.Append(string.IsNullOrEmpty(json) ? "null" : json); return this; }

        public JsonLine Counts(string key, SortedDictionary<string, int> counts)
        {
            Key(key);
            _sb.Append('{');
            var first = true;
            foreach (var pair in counts)
            {
                if (!first) _sb.Append(',');
                first = false;
                _sb.Append('"').Append(pair.Key).Append("\":").Append(pair.Value.ToString(CultureInfo.InvariantCulture));
            }
            _sb.Append('}');
            return this;
        }

        public override string ToString() => _sb.ToString() + "}";
    }
}
