using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RoguePirates.V5.Net
{
    /// <summary>
    /// V5 M9b (I9b.1, D9b.1): the measurement hook. The server counts per-client bytes down and up, its tick time (all of
    /// the server's Update work since the previous net tick, see <see cref="NetServer"/>) and each snapshot's size; a
    /// client counts its own bytes and its prediction error and corrections. Bytes are message payloads; the wire
    /// estimate adds Netcode's named-message framing (<see cref="NetProtocol.NamedMessageOverheadBytes"/>) per message
    /// (the WebSocket frame and the transport's packet header come on top, shared by every message in a packet).
    /// </summary>
    public sealed class NetMetrics
    {
        public readonly string Name;
        /// <summary>Counting starts at <see cref="Reset"/> (the server's start, the client's creation).</summary>
        public NetMetrics(string name) { Name = name; }

        public sealed class Traffic
        {
            public long bytesDown, bytesUp, messagesDown, messagesUp;
        }

        readonly Dictionary<ulong, Traffic> _clients = new Dictionary<ulong, Traffic>();
        public readonly List<float> TickMs = new List<float>();
        public readonly List<float> SnapshotBytes = new List<float>();
        public readonly List<float> SnapshotEnemies = new List<float>();
        public readonly List<float> PredictionError = new List<float>();
        public readonly List<float> Correction = new List<float>();
        public readonly List<float> ResumeSeconds = new List<float>();
        /// <summary>Client: how far behind its own input the server's acknowledged state is, per snapshot (round trip plus
        /// the server's input buffer and the snapshot interval).</summary>
        public readonly List<float> InputLagMs = new List<float>();
        public float StartedAt { get; private set; }
        public int GuessedInputs, LateInputs, CatchUpSteps;

        public void Reset()
        {
            _clients.Clear();
            TickMs.Clear(); SnapshotBytes.Clear(); SnapshotEnemies.Clear(); PredictionError.Clear(); Correction.Clear(); ResumeSeconds.Clear(); InputLagMs.Clear();
            GuessedInputs = LateInputs = CatchUpSteps = 0;
            StartedAt = Time.realtimeSinceStartup;
        }

        public Traffic For(ulong clientId)
        {
            if (!_clients.TryGetValue(clientId, out var t)) _clients[clientId] = t = new Traffic();
            return t;
        }

        public void Down(ulong clientId, int bytes) { var t = For(clientId); t.bytesDown += bytes; t.messagesDown++; }
        public void Up(ulong clientId, int bytes) { var t = For(clientId); t.bytesUp += bytes; t.messagesUp++; }

        public float Elapsed => Mathf.Max(0.001f, Time.realtimeSinceStartup - StartedAt);

        public static float Percentile(List<float> values, float p)
        {
            if (values == null || values.Count == 0) return 0f;
            var sorted = values.OrderBy(v => v).ToList();
            var rank = Mathf.Clamp(p / 100f * (sorted.Count - 1), 0f, sorted.Count - 1);
            var lo = Mathf.FloorToInt(rank);
            var hi = Mathf.CeilToInt(rank);
            return Mathf.Lerp(sorted[lo], sorted[hi], rank - lo);
        }

        public static float Max(List<float> values) => values == null || values.Count == 0 ? 0f : values.Max();

        /// <summary>The report section as JSON (hand-written: nested stats JsonUtility cannot write).</summary>
        public string ToJson(string indent = "  ")
        {
            var seconds = Elapsed;
            var sb = new StringBuilder();
            sb.Append("{\n");
            Field(sb, indent, "name", Name);
            Field(sb, indent, "seconds", seconds);
            sb.Append(indent).Append("\"clients\": [");
            var first = true;
            foreach (var pair in _clients)
            {
                var t = pair.Value;
                sb.Append(first ? "\n" : ",\n").Append(indent).Append("  {");
                sb.Append("\"clientId\": ").Append(pair.Key);
                sb.Append(", \"bytesDownPerSecond\": ").Append(F(t.bytesDown / seconds));
                sb.Append(", \"bytesUpPerSecond\": ").Append(F(t.bytesUp / seconds));
                sb.Append(", \"wireDownPerSecondEstimate\": ").Append(F((t.bytesDown + t.messagesDown * NetProtocol.NamedMessageOverheadBytes) / seconds));
                sb.Append(", \"wireUpPerSecondEstimate\": ").Append(F((t.bytesUp + t.messagesUp * NetProtocol.NamedMessageOverheadBytes) / seconds));
                sb.Append(", \"messagesDown\": ").Append(t.messagesDown);
                sb.Append(", \"messagesUp\": ").Append(t.messagesUp);
                sb.Append('}');
                first = false;
            }
            sb.Append(first ? "],\n" : "\n" + indent + "],\n");
            Stats(sb, indent, "tickMs", TickMs);
            Stats(sb, indent, "snapshotBytes", SnapshotBytes);
            Stats(sb, indent, "snapshotEnemies", SnapshotEnemies);
            Stats(sb, indent, "predictionErrorMetres", PredictionError);
            Stats(sb, indent, "correctionMetres", Correction);
            Stats(sb, indent, "resumeSeconds", ResumeSeconds);
            Stats(sb, indent, "inputLagMs", InputLagMs);
            Field(sb, indent, "guessedInputs", GuessedInputs);
            Field(sb, indent, "lateInputs", LateInputs);
            sb.Append(indent).Append("\"catchUpSteps\": ").Append(CatchUpSteps).Append('\n');
            sb.Append('}');
            return sb.ToString();
        }

        static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        static void Field(StringBuilder sb, string indent, string name, string value) =>
            sb.Append(indent).Append('"').Append(name).Append("\": \"").Append(value).Append("\",\n");

        static void Field(StringBuilder sb, string indent, string name, double value) =>
            sb.Append(indent).Append('"').Append(name).Append("\": ").Append(F(value)).Append(",\n");

        static void Stats(StringBuilder sb, string indent, string name, List<float> values)
        {
            sb.Append(indent).Append('"').Append(name).Append("\": {");
            sb.Append("\"count\": ").Append(values.Count);
            sb.Append(", \"mean\": ").Append(F(values.Count > 0 ? values.Average() : 0f));
            sb.Append(", \"p50\": ").Append(F(Percentile(values, 50f)));
            sb.Append(", \"p95\": ").Append(F(Percentile(values, 95f)));
            sb.Append(", \"max\": ").Append(F(Max(values)));
            sb.Append("},\n");
        }

        /// <summary>One JSON report from a run's metrics (server first, then clients).</summary>
        public static string Report(string title, IDictionary<string, string> context, params NetMetrics[] sections)
        {
            var sb = new StringBuilder();
            sb.Append("{\n  \"report\": \"").Append(title).Append("\",\n");
            sb.Append("  \"utc\": \"").Append(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)).Append("\",\n");
            sb.Append("  \"unity\": \"").Append(Application.unityVersion).Append("\",\n");
            sb.Append("  \"editor\": ").Append(Application.isEditor ? "true" : "false").Append(",\n");
            if (context != null)
                foreach (var pair in context) sb.Append("  \"").Append(pair.Key).Append("\": \"").Append(pair.Value).Append("\",\n");
            sb.Append("  \"sections\": [\n");
            for (var i = 0; i < sections.Length; i++)
            {
                var section = sections[i].ToJson("      ").Replace("\n}", "\n    }");
                sb.Append("    ").Append(section).Append(i + 1 < sections.Length ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            return sb.ToString();
        }

        public static string WriteReport(string path, string title, IDictionary<string, string> context, params NetMetrics[] sections)
        {
            var json = Report(title, context, sections);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, json);
            return json;
        }
    }
}
