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

        /// <summary>V5 M9b close: the server's per-tick and per-snapshot lists keep only a rolling window (about the last 5 to
        /// 10 minutes at 30 Hz: past this many samples the oldest half goes), so an idle match server does not keep every
        /// tick (1.72 million samples after 16 h on Railway, sorted for every stats line). The totals below cover the whole
        /// voyage.</summary>
        public const int ServerWindowSamples = 18000;
        /// <summary>Every tick measured since <see cref="Reset"/>, and the slowest one (the window drops older ticks).</summary>
        public long TicksMeasured { get; private set; }
        public float TickMsMax { get; private set; }

        /// <summary>One server tick's work time: into the rolling window, and the voyage's count and maximum.</summary>
        public void AddTick(float ms)
        {
            TicksMeasured++;
            if (ms > TickMsMax) TickMsMax = ms;
            AddCapped(TickMs, ms, ServerWindowSamples);
        }

        /// <summary>One snapshot's size and enemy count, in the rolling window.</summary>
        public void AddSnapshot(float bytes, float enemies)
        {
            AddCapped(SnapshotBytes, bytes, ServerWindowSamples);
            AddCapped(SnapshotEnemies, enemies, ServerWindowSamples);
        }

        public void Reset()
        {
            _clients.Clear();
            TickMs.Clear(); SnapshotBytes.Clear(); SnapshotEnemies.Clear(); PredictionError.Clear(); Correction.Clear(); ResumeSeconds.Clear(); InputLagMs.Clear();
            GuessedInputs = LateInputs = CatchUpSteps = 0;
            TicksMeasured = 0;
            TickMsMax = 0f;
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

        public static float Percentile(List<float> values, float p) => values == null ? 0f : PercentileOfLast(values, values.Count, p);

        /// <summary>
        /// The <paramref name="p"/>th percentile of the newest <paramref name="last"/> values. V5 M9b owner re-test fixes: on a
        /// reused buffer, not a LINQ sort (the browser client's HUD asked for the input delay's median ten times a second
        /// over every sample since the start, allocating two lists each time and slower as the voyage went on).
        /// </summary>
        public static float PercentileOfLast(List<float> values, int last, float p)
        {
            if (values == null || values.Count == 0 || last <= 0) return 0f;
            var n = Math.Min(last, values.Count);
            if (_scratch.Length < n) _scratch = new float[Mathf.NextPowerOfTwo(n)];
            values.CopyTo(values.Count - n, _scratch, 0, n);
            Array.Sort(_scratch, 0, n);
            var rank = Mathf.Clamp(p / 100f * (n - 1), 0f, n - 1);
            var lo = Mathf.FloorToInt(rank);
            var hi = Mathf.CeilToInt(rank);
            return Mathf.Lerp(_scratch[lo], _scratch[hi], rank - lo);
        }

        static float[] _scratch = new float[256];

        /// <summary>Adds <paramref name="value"/>; past <paramref name="max"/> values (0: no limit) the oldest half goes, so a long
        /// session's lists stay bounded (the browser client's statistics cover the newest samples).</summary>
        public static void AddCapped(List<float> values, float value, int max)
        {
            if (max > 0 && values.Count >= max) values.RemoveRange(0, values.Count - max / 2);
            values.Add(value);
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
            Field(sb, indent, "ticksMeasured", TicksMeasured);
            Field(sb, indent, "tickMsMaxVoyage", TickMsMax);
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
