using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RoguePirates.V5.Net
{
    /// <summary>
    /// V5 M9b part 2a: the match server process around a <see cref="NetServer"/> (the Linux Dedicated Server build in
    /// Docker, later the Railway test service). One voyage per process:
    /// <list type="bullet">
    /// <item>the run is held (time scale 0) until the first player joins, so an idle server's voyage does not start
    /// without its player;</item>
    /// <item>once listening it writes the ready file (<see cref="ReadyFile"/>, the image's health check) and a READY log
    /// line (cold start is measured from <c>docker run</c> to that line);</item>
    /// <item>every <see cref="ReportEverySeconds"/> it logs a stats line and, when <c>RP_METRICS_DIR</c> is set, writes
    /// the server's measurement report there (tick time, bytes, memory);</item>
    /// <item>a stop request (the stop file <see cref="StopFile"/> the entry script touches on SIGTERM, or
    /// <see cref="Application.wantsToQuit"/>) ends the session cleanly: <c>end</c> ServerClosing, a moment to send it,
    /// then the process exits;</item>
    /// <item>when the voyage is over (ended, or its player gone past the resume grace) the process exits with 0.</item>
    /// </list>
    /// The secret is never logged.
    /// </summary>
    public sealed class NetServerHost : MonoBehaviour
    {
        public const string ReadyFile = "/tmp/rp-match-ready";
        public const string StopFile = "/tmp/rp-match-stop";
        public const string MetricsDirVariable = "RP_METRICS_DIR";
        public const float ReportEverySeconds = 10f;

        NetServer _server;
        ushort _port;
        string _metricsDir;
        string _matchId;
        bool _held;
        bool _closing;
        bool _allowQuit;
        float _nextReport;
        float _nextStopCheck;
        float _overSince = -1f;
        float _joinedAt = -1f;
        string _closeReason;

        public void Begin(NetServer server, ushort port, bool holdUntilJoin = true)
        {
            _server = server;
            _port = port;
            _metricsDir = Environment.GetEnvironmentVariable(MetricsDirVariable);
            _matchId = Environment.GetEnvironmentVariable(JoinTicketVerifier.MatchIdEnvironmentVariable) ?? "";
            if (holdUntilJoin) Hold(true);
            server.Joined += OnJoined;
            Application.wantsToQuit += WantsToQuit;
            TryDelete(StopFile);
            try { File.WriteAllText(ReadyFile, port.ToString()); } catch (Exception) { /* not a container: no ready file */ }
            _nextReport = Time.realtimeSinceStartup + ReportEverySeconds;
            Debug.Log($"[V5 Net] READY listening on ws://0.0.0.0:{port}/ match '{_matchId}' after {Time.realtimeSinceStartup:0.00} s in the process (run held until a player joins: {holdUntilJoin})");
        }

        void OnJoined(string player)
        {
            if (_joinedAt < 0f)
            {
                _joinedAt = Time.realtimeSinceStartup;
                _server.Metrics.Reset(); // the measurements cover the voyage, not the idle wait before it
            }
            if (_held) Hold(false);
            Debug.Log("[V5 Net] player joined; the run plays");
        }

        void Hold(bool hold)
        {
            _held = hold;
            Time.timeScale = hold ? 0f : 1f;
        }

        void Update()
        {
            if (_server == null) return;
            var now = Time.realtimeSinceStartup;
            if (now >= _nextReport)
            {
                _nextReport = now + ReportEverySeconds;
                Debug.Log("[V5 Net] stats " + StatsLine());
                WriteReport("periodic");
            }
            if (_closing) return;
            if (now >= _nextStopCheck)
            {
                _nextStopCheck = now + 0.25f;
                if (File.Exists(StopFile)) { StartCoroutine(Close("SIGTERM (stop file)")); return; }
            }
            var s = _server.Current;
            var over = s != null && !s.connected && (_server.EndSent || s.expired);
            if (!over) { _overSince = -1f; return; }
            if (_overSince < 0f) _overSince = now;
            if (now - _overSince > 3f) StartCoroutine(Close(_server.EndSent ? "voyage ended (" + _server.LastEndReason + ")" : "session expired"));
        }

        bool WantsToQuit()
        {
            if (_allowQuit) return true;
            if (!_closing) StartCoroutine(Close("quit requested"));
            return false;
        }

        IEnumerator Close(string reason)
        {
            if (_closing) yield break;
            _closing = true;
            _closeReason = reason;
            Debug.Log("[V5 Net] closing: " + reason);
            yield return _server.CloseGracefully();
            WriteReport("final");
            Debug.Log("[V5 Net] final " + StatsLine());
            Debug.Log("[V5 Net] EXIT " + reason);
            TryDelete(ReadyFile);
            _allowQuit = true;
            Application.Quit(0);
        }

        void OnDestroy()
        {
            Application.wantsToQuit -= WantsToQuit;
            if (_server != null) _server.Joined -= OnJoined;
        }

        string StatsLine()
        {
            var m = _server.Metrics;
            var s = _server.Current;
            return $"tick={_server.ServerTick} session={(s == null ? "none" : s.connected ? "connected" : "detached")} ticksMeasured={m.TickMs.Count} " +
                   $"tickMs p50={NetMetrics.Percentile(m.TickMs, 50f):0.000} p95={NetMetrics.Percentile(m.TickMs, 95f):0.000} max={NetMetrics.Max(m.TickMs):0.000} " +
                   $"rssMB={RssMegabytes():0.0} monoHeapMB={UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / 1048576f:0.0} " +
                   $"unityReservedMB={UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1048576f:0.0} fps={1f / Mathf.Max(1e-4f, Time.unscaledDeltaTime):0}";
        }

        void WriteReport(string kind)
        {
            if (string.IsNullOrEmpty(_metricsDir)) return;
            try
            {
                var m = _server.Metrics;
                NetMetrics.WriteReport(Path.Combine(_metricsDir, "server.json"), "V5 match server (Linux, Docker)", new Dictionary<string, string>
                {
                    ["kind"] = kind,
                    ["port"] = _port.ToString(),
                    ["matchId"] = _matchId,
                    ["serverTick"] = _server.ServerTick.ToString(),
                    ["stepsApplied"] = _server.StepsApplied.ToString(),
                    ["rejectedJoins"] = _server.RejectedJoins.ToString(),
                    ["lastRejectReason"] = _server.LastRejectReason ?? "",
                    ["endSent"] = _server.EndSent ? _server.LastEndReason.ToString() : "",
                    ["closeReason"] = _closeReason ?? "",
                    ["processSeconds"] = Time.realtimeSinceStartup.ToString("0.00"),
                    ["joinedAtProcessSeconds"] = _joinedAt.ToString("0.00"),
                    ["rssMB"] = RssMegabytes().ToString("0.0"),
                    ["monoHeapMB"] = (UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / 1048576f).ToString("0.0"),
                    ["unityReservedMB"] = (UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1048576f).ToString("0.0"),
                    ["platform"] = Application.platform.ToString(),
                    ["scriptingBackend"] = ScriptingBackend(),
                }, m);
            }
            catch (Exception e) { Debug.LogWarning("[V5 Net] report not written: " + e.Message); }
        }

        static string ScriptingBackend()
        {
#if ENABLE_IL2CPP
            return "IL2CPP";
#else
            return "Mono";
#endif
        }

        /// <summary>The process's resident memory from /proc (Linux); 0 elsewhere.</summary>
        static float RssMegabytes()
        {
            try
            {
                if (!File.Exists("/proc/self/status")) return 0f;
                foreach (var line in File.ReadAllLines("/proc/self/status"))
                {
                    if (!line.StartsWith("VmRSS:")) continue;
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    return parts.Length >= 2 && long.TryParse(parts[1], out var kb) ? kb / 1024f : 0f;
                }
            }
            catch (Exception) { /* not Linux */ }
            return 0f;
        }

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch (Exception) { /* not a container */ }
        }
    }
}
