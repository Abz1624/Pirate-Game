using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using RoguePirates.V4.Composition;
using RoguePirates.V4.DevTools.Trace;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Platform;
using RoguePirates.V4.Foundation.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoguePirates.V4.DevTools.Bench
{
    /// <summary>
    /// V5 phone benchmark (1 Oct): a web build with the <c>V5_PHONE_BENCH</c> define, opened with <c>?bench=&lt;label&gt;</c>,
    /// skips the menu, sails a seeded Voyage with the human-like pilot (invulnerable, so every variant carries the same
    /// load), and measures the frame times in two windows: "early" (0:08-0:48) and "peak" (the run clock jumped to 3:35,
    /// measured 3:45-4:45, the sketch's biggest crowd). Each window logs one "[Bench] {json}" line (frame-time percentiles,
    /// frames over 33 ms, enemies alive, the wasm heap); "[Bench] done" ends it. The settings under test come from the
    /// URL: q (low|mid|high), rs (render scale), level (mobile|pc), shadows=0, post=0, msaa (1|2|4), hdr=0, fx=reduced,
    /// fps (a frame-rate cap), short=1 (10 s windows, for checking the harness), crowd=N (from the clock jump on, keep N
/// enemies alive around the ship with extra HP, so every variant carries the same load: the enemy-cap test), profile=1
/// (Editor: the average milliseconds of the game's V4 markers and Unity's main systems in the peak window, as one
/// "[BenchProfile] {json}" line; the Editor reads the query from PlayerPrefs "v5_bench_args"), bars=0 (no enemy health
/// bars), minimap=0 (the minimap stops updating: its fog-of-war rebuilds). The render scale changes only with rs= (else
/// the game's own: the phone tier's scale, 1.0 on desktop). The player's save and settings are not touched: the run uses
/// an in-memory save and a copy of the settings, and the few preferences the bench sets are restored when it quits (the
/// Editor puts back the render pipeline values it changed, too).
    /// </summary>
    public sealed class PhoneBench : MonoBehaviour
    {
        static Dictionary<string, string> _args;
        static bool _runRequested;

        const float WarmUp = 8f, PeakClock = 215f, PeakSettle = 10f;
        float _window;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _args = null;
            _runRequested = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            var args = Parse(Application.absoluteURL);
            // Android (the APK has no URL): "-v5bench <query>" on the command line, which the launch intent's "unity"
            // extra provides (adb shell am start ... --es unity "-v5bench bench=...&crowd=80").
            if (!args.ContainsKey("bench")) args = ParseCommandLine(Environment.GetCommandLineArgs());
#if UNITY_EDITOR
            // In the Editor (profiling): the query comes from PlayerPrefs "v5_bench_args", used once.
            if (!args.ContainsKey("bench") && PlayerPrefs.HasKey("v5_bench_args"))
            {
                args = Parse("?" + PlayerPrefs.GetString("v5_bench_args"));
                PlayerPrefs.DeleteKey("v5_bench_args");
            }
#endif
            if (!args.ContainsKey("bench")) return;
            _args = args;
            // Preferences the run reads, pinned for the bench and restored when it quits.
            Pin("v4_tutorial_progress", 99);       // no tutorial prompts
            Pin("v3_saltwind_runs_completed", 2);
            Pin("v3_selected_captain", 0);           // Rook
            Pin("v5_auto_quality", null);            // no tier carried over from an earlier variant
            Application.quitting += RestorePrefs;
            UnityEngine.Random.InitState(Int("seed", 20261001));
            if (Get("level") == "pc") QualitySettings.SetQualityLevel(Math.Max(0, Array.IndexOf(QualitySettings.names, "PC")), true);
            if (Get("level") == "mobile") QualitySettings.SetQualityLevel(Math.Max(0, Array.IndexOf(QualitySettings.names, "Mobile")), true);
            var fps = Int("fps", 0);
            if (fps > 0) Application.targetFrameRate = fps;
            // A fixed tier (never Auto, which would change the tier mid-measurement) and the effects setting.
            var settings = GameSettings.Current.Copy(); // a copy: the player's saved settings stay as they are
            GameSettings.UseForTests(settings);
            settings.quality = (int)Tier();
            settings.reducedEffects = Get("fx") == "reduced";
            settings.screenShake = false;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Debug.Log($"[Bench] armed {Application.absoluteURL}");
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_args == null) return;
            if (scene.name == V4SceneNames.MainMenu && !_runRequested)
            {
                _runRequested = true;
                if (AppRoot.Instance != null) AppRoot.Instance.ReplaceSave(new SaveService(new MemorySaveBackend())); // before the run touches it
                RunRequest.RequestSeed(Int("seed", 20261001));
                MenuServices.BeginSaltwindExperiment();
                SceneManager.LoadScene(V4SceneNames.SaltwindRun);
            }
            else if (scene.name == V4SceneNames.SaltwindRun && FindFirstObjectByType<PhoneBench>() == null)
                new GameObject("V5PhoneBench").AddComponent<PhoneBench>();
        }

        static readonly List<(string key, bool had, int value)> PinnedPrefs = new List<(string, bool, int)>();
        static readonly List<(string name, object value)> PipelineChanges = new List<(string, object)>();

        static void Pin(string key, int? value)
        {
            PinnedPrefs.Add((key, PlayerPrefs.HasKey(key), PlayerPrefs.GetInt(key, 0)));
            if (value.HasValue) PlayerPrefs.SetInt(key, value.Value); else PlayerPrefs.DeleteKey(key);
        }

        /// <summary>The pinned preferences back as they were (once the run has read them: web builds on one address share them).</summary>
        static void RestorePinnedPrefs()
        {
            if (PinnedPrefs.Count == 0) return;
            foreach (var (key, had, value) in PinnedPrefs)
                if (had) PlayerPrefs.SetInt(key, value); else PlayerPrefs.DeleteKey(key);
            PinnedPrefs.Clear();
            PlayerPrefs.Save();
        }

        static void RestorePrefs()
        {
            RestorePinnedPrefs();
            GameSettings.UseForTests(null);
            // The Editor shares its render pipeline asset with the project: put back what the bench changed.
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (pipeline != null)
                foreach (var (name, value) in PipelineChanges)
                {
                    var p = pipeline.GetType().GetProperty(name);
                    if (p != null && p.CanWrite) p.SetValue(pipeline, value);
                }
            PipelineChanges.Clear();
        }

        void OnDestroy()
        {
            if (Application.isEditor) RestorePrefs(); // leaving play mode
        }

        // ---- The run ------------------------------------------------------------------------------------------------

        readonly float[] _dt = new float[20000];
        int _frames, _enemySamples;
        readonly FrameTiming[] _timing = new FrameTiming[1];
        double _cpuMain, _cpuRender, _gpu;
        int _timed;
        long _enemySum;
        int _enemyMax;
        string _phase = "start";
        float _phaseAt;
        V4Game _game;
        RoguePirates.V4.Enemies.IEnemyWorldView _enemies;
        RoguePirates.V4.Spawning.IEnemySpawnSink _sink;
        PlayerShipController _ship;
        readonly List<int> _crowdDefinitions = new List<int>();
        readonly List<(string name, Unity.Profiling.ProfilerRecorder recorder, double sum, long max)> _markers = new List<(string, Unity.Profiling.ProfilerRecorder, double, long)>();
        int _markerFrames;
        System.Random _crowdRandom;

        void Start()
        {
            _window = Get("short") == "1" ? 10f : Get("short") == "2" ? 25f : 60f;
            if (AppRoot.Instance != null)
            {
                AppRoot.Instance.ReplaceSave(new SaveService(new MemorySaveBackend()));
                AppRoot.Instance.Config.ScreenShakeEnabled = false;
                AppRoot.Instance.Config.Quality = Tier();
            }
            ApplyRendering();
            var wanted = Int("fps", 0);
            if (wanted > 60)
                Screen.SetResolution(Screen.width, Screen.height, Screen.fullScreenMode, new RefreshRate { numerator = (uint)wanted, denominator = 1 });
            var ship = _ship = FindFirstObjectByType<PlayerShipController>();
            if (ship != null)
            {
                var player = HumanPlayer.Install(ship, HumanPlayer.BuildProfile.Strong);
                player.StrongBuildAtSeconds = 5f; // six weapons early: a full mid-game load of shots and effects
                if (ship.Damage != null) ship.Damage.SetPlaytestInvulnerable(true);
            }
            Debug.Log("[Bench] " + Json(new (string, object)[]
            {
                ("phase", "info"), ("label", Get("bench")), ("device", SystemInfo.deviceModel), ("gpu", SystemInfo.graphicsDeviceName),
                ("gfx", SystemInfo.graphicsDeviceVersion), ("cores", SystemInfo.processorCount), ("ramMB", SystemInfo.systemMemorySize),
                ("screen", $"{Screen.width}x{Screen.height}"), ("dpi", Screen.dpi), ("quality", QualitySettings.names[QualitySettings.GetQualityLevel()]),
                ("tier", Tier().ToString()), ("renderScale", RenderScale()), ("loadSeconds", Time.realtimeSinceStartup),
                ("heapMB", HeapMB()), ("astc", SystemInfo.SupportsTextureFormat(TextureFormat.ASTC_6x6)), ("dxt", SystemInfo.SupportsTextureFormat(TextureFormat.DXT5)),
                ("textures", TextureFormats()),
            }));
            Begin("warm");
        }

        void Update()
        {
            RestorePinnedPrefs(); // the run read them as it started; an install that is stopped early keeps none
            var run = RunManager.Instance;
            if (run == null) return;
            if (_game == null)
            {
                _game = FindFirstObjectByType<V4Game>();
                if (_game != null && _game.Context != null)
                {
                    _game.Context.TryGet(out _enemies);
                    _game.Context.TryGet(out _sink);
                }
            }
            if (_phase == "settle" || _phase == "peak") HoldCrowd();
            if (Get("bars") == "0" && (Time.frameCount & 15) == 0) HideEnemyBars();
            if (Get("minimap") == "0" && (Time.frameCount & 15) == 0)
            {
                var map = FindFirstObjectByType<RoguePirates.V4.Presentation.Hud.MinimapPresenter>();
                if (map != null && map.enabled) map.enabled = false; // the map freezes: no fog rebuilds, no marker updates
            }
            var t = Time.realtimeSinceStartup - _phaseAt;
            switch (_phase)
            {
                case "warm":
                    if (t >= WarmUp)
                    {
                        ApplyRendering(); // again: the HUD's quality setup may have run after Start
                        Begin("early");
                    }
                    break;
                case "early":
                    Sample();
                    if (t >= _window * 2f / 3f) { Report("early"); run.JumpClock(PeakClock); Begin("settle"); }
                    break;
                case "settle":
                    if (t >= PeakSettle) { Begin("peak"); if (Get("profile") == "1") StartMarkers(); }
                    break;
                case "peak":
                    Sample();
                    if (t >= _window) { Report("peak"); ReportMarkers(); Debug.Log("[Bench] done"); Begin("finished"); }
                    break;
            }
        }

        void Begin(string phase)
        {
            _phase = phase;
            _phaseAt = Time.realtimeSinceStartup;
            _frames = 0;
            _enemySamples = 0;
            _enemySum = 0;
            _enemyMax = 0;
            _cpuMain = _cpuRender = _gpu = 0;
            _timed = 0;
        }

        void Sample()
        {
            // Frame timing stats (on in benchmark Android builds; zero where the platform has none, as WebGL's GPU).
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timing) > 0)
            {
                _cpuMain += _timing[0].cpuMainThreadFrameTime;
                _cpuRender += _timing[0].cpuRenderThreadFrameTime;
                _gpu += _timing[0].gpuFrameTime;
                _timed++;
            }
            if (_markers.Count > 0)
            {
                _markerFrames++;
                for (var i = 0; i < _markers.Count; i++)
                {
                    var m = _markers[i];
                    var v = m.recorder.LastValue;
                    m.sum += v;
                    if (v > m.max) m.max = v;
                    _markers[i] = m;
                }
            }
            if (_frames < _dt.Length) _dt[_frames++] = Time.unscaledDeltaTime;
            if (_enemies != null && _enemies.Exists && (_frames & 7) == 0)
            {
                var n = _enemies.ActiveCount;
                _enemySum += n;
                _enemySamples++;
                if (n > _enemyMax) _enemyMax = n;
            }
        }

        void Report(string phase)
        {
            var n = Mathf.Max(1, _frames);
            var sorted = new float[n];
            Array.Copy(_dt, sorted, n);
            Array.Sort(sorted);
            var total = 0f;
            int over33 = 0, over50 = 0;
            for (var i = 0; i < n; i++)
            {
                total += sorted[i];
                if (sorted[i] > 1f / 30f + 0.0005f) over33++;
                if (sorted[i] > 0.05f) over50++;
            }
            float P(float q) => sorted[Mathf.Clamp(Mathf.CeilToInt(q * n) - 1, 0, n - 1)] * 1000f;
            var run = RunManager.Instance;
            Debug.Log("[Bench] " + Json(new (string, object)[]
            {
                ("phase", phase), ("label", Get("bench")), ("frames", n), ("seconds", total), ("fps", n / Mathf.Max(0.001f, total)),
                ("p50", P(0.5f)), ("p95", P(0.95f)), ("p99", P(0.99f)), ("max", sorted[n - 1] * 1000f),
                ("over33", 100f * over33 / n), ("over50", 100f * over50 / n),
                ("enemiesAvg", _enemySamples > 0 ? (float)_enemySum / _enemySamples : -1f), ("enemiesMax", _enemySamples > 0 ? _enemyMax : -1),
                ("runClock", run != null ? run.ElapsedSeconds : -1f), ("heapMB", HeapMB()),
                ("cpuMainMs", _timed > 0 ? (float)(_cpuMain / _timed) : -1f), ("cpuRenderMs", _timed > 0 ? (float)(_cpuRender / _timed) : -1f),
                ("gpuMs", _timed > 0 ? (float)(_gpu / _timed) : -1f),
                ("refreshHz", (float)Screen.currentResolution.refreshRateRatio.value),
            }));
        }

        /// <summary>Tops the crowd up to crowd=N: regular enemies of every kind, 14-30 m from the ship, at most 4 a frame.</summary>
        void HoldCrowd()
        {
            var target = Int("crowd", 0);
            if (target <= 0 || _sink == null || _ship == null || _game == null || _game.Config == null) return;
            if (_crowdDefinitions.Count == 0)
            {
                foreach (var a in _game.Config.All<RoguePirates.V4.Config.EnemyArchetypeConfig>())
                {
                    if (a == null || !a.inLegacyDatabase) continue;
                    var index = _sink.FindDefinition(a.enemyId);
                    if (index >= 0 && !_sink.DefinitionIsBoss(index)) _crowdDefinitions.Add(index);
                }
                _crowdRandom = new System.Random(Int("seed", 20261001));
                if (_crowdDefinitions.Count == 0) { Debug.LogWarning("[Bench] no enemy definitions for the crowd"); return; }
            }
            for (var i = 0; i < 4 && _sink.ActiveCount < target; i++)
            {
                var angle = (float)(_crowdRandom.NextDouble() * Math.PI * 2.0);
                var distance = 14f + (float)_crowdRandom.NextDouble() * 16f;
                var position = _ship.Position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                var definition = _crowdDefinitions[_crowdRandom.Next(_crowdDefinitions.Count)];
                _sink.Spawn(definition, position, RoguePirates.V4.Config.ConfigEnums.EliteAffix.None, 1f, 25f, 1f);
            }
        }

        /// <summary>bars=0: the enemies' world health bars off (their canvas and per-bar LateUpdate), the player's kept.</summary>
        void HideEnemyBars()
        {
            var player = FindFirstObjectByType<RoguePirates.V4.Presentation.World.ShipHealthBarPresenter>();
            var playerBar = player != null
                ? player.GetType().GetField("_bar", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(player) as Component
                : null;
            foreach (var bar in FindObjectsByType<RoguePirates.V4.Foundation.VFX.WorldHealthBar>(FindObjectsSortMode.None))
            {
                if (bar == playerBar) continue;
                // Their images draw through the shared canvas: hide the images themselves.
                foreach (var graphic in bar.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.enabled = false;
            }
        }

        // ---- Profiling (Editor) ----------------------------------------------------------------------------------------

        static readonly string[] EngineMarkers =
        {
            "PlayerLoop", "Update.ScriptRunBehaviourUpdate", "PreLateUpdate.ScriptRunBehaviourLateUpdate",
            "FixedUpdate.ScriptRunBehaviourFixedUpdate", "FixedUpdate.PhysicsFixedUpdate", "Physics.Simulate", "Physics.Processing",
            "PostLateUpdate.FinishFrameRendering", "RenderPipelineManager.DoRenderLoop_Internal()", "Director.ProcessFrame",
            "ParticleSystem.Update", "ParticleSystem.ScheduleGeometryJobs", "PostLateUpdate.UpdateAllRenderers",
            "Canvas.SendWillRenderCanvases", "PostLateUpdate.PlayerUpdateCanvases", "UGUI.Rendering.RenderOverlays",
            "PostLateUpdate.UpdateAllSkinnedMeshes", "GC.Collect", "GC.Alloc", "Gfx.WaitForPresentOnGfxThread",
            "PostLateUpdate.UpdateAudio", "PreUpdate.AIUpdatePostScript", "Update.DirectorUpdate",
        };

        void StartMarkers()
        {
            var handles = new List<Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle>();
            Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var h in handles)
            {
                var d = Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetDescription(h);
                var name = d.Name;
                // The game's markers, Unity's main systems and, in the Editor, every script's Update/LateUpdate/FixedUpdate.
                var script = name.EndsWith("Update() [Invoke]") || name.EndsWith("Update()") || name.Contains("Coroutine");
                if (!(name.StartsWith("V4.") || script || Array.IndexOf(EngineMarkers, name) >= 0 || name.StartsWith("Inl_") || name == "GC Allocated In Frame")) continue;
                _markers.Add((name, new Unity.Profiling.ProfilerRecorder(h, 1, Unity.Profiling.ProfilerRecorderOptions.Default), 0.0, 0L));
            }
            foreach (var m in _markers) m.recorder.Start();
            _markerFrames = 0;
        }

        void ReportMarkers()
        {
            if (_markers.Count == 0) return;
            var n = Math.Max(1, _markerFrames);
            var list = new List<(string key, object value)> { ("phase", "profile"), ("label", Get("bench")), ("frames", n), ("crowd", Int("crowd", 0)) };
            _markers.Sort((a, b) => b.sum.CompareTo(a.sum));
            foreach (var m in _markers)
            {
                // Markers are nanoseconds per frame; "GC Allocated In Frame" is bytes (reported as KB).
                var per = m.sum / n;
                if (per < 2000 && m.max < 5_000_000) { m.recorder.Dispose(); continue; } // under 0.002 ms average and never 5 ms: noise
                list.Add((m.name, m.name == "GC Allocated In Frame" ? (float)(per / 1024.0) : (float)(per / 1e6)));
                if (m.name != "GC Allocated In Frame") list.Add((m.name + " max", (float)(m.max / 1e6)));
                m.recorder.Dispose();
            }
            _markers.Clear();
            Debug.Log("[BenchProfile] " + Json(list.ToArray()));
        }

        // ---- Settings under test -----------------------------------------------------------------------------------

        static QualityTier Tier() => Get("q") switch { "low" => QualityTier.Low, "high" => QualityTier.High, _ => QualityTier.Mid };

        void ApplyRendering()
        {
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (pipeline != null)
            {
                var rs = Get("rs");
                if (rs != null) SetPipeline(pipeline, "renderScale", Float(rs)); // else the game's own render scale
                if (Get("msaa") != null) SetPipeline(pipeline, "msaaSampleCount", Int("msaa", 1));
                if (Get("hdr") == "0") SetPipeline(pipeline, "supportsHDR", false);
                if (Get("shadows") == "0") SetPipeline(pipeline, "shadowDistance", 0f);
            }
            if (Get("shadows") == "0")
                foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None)) light.shadows = LightShadows.None;
            if (Get("post") == "0")
                foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                {
                    var data = cam.GetComponent("UniversalAdditionalCameraData");
                    if (data != null) SetProperty(data, "renderPostProcessing", false);
                }
        }

        /// <summary>The loaded textures by format with their memory ("ASTC_6x6:120/40MB ..."): which compression the phone really got.</summary>
        static string TextureFormats()
        {
            var counts = new Dictionary<string, (int n, long bytes)>();
            foreach (var t in Resources.FindObjectsOfTypeAll<Texture2D>())
            {
                var key = t.format.ToString();
                counts.TryGetValue(key, out var c);
                counts[key] = (c.n + 1, c.bytes + UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t));
            }
            var sb = new StringBuilder();
            foreach (var kv in counts) sb.Append(kv.Key).Append(':').Append(kv.Value.n).Append('/').Append((kv.Value.bytes / (1024f * 1024f)).ToString("0.#", CultureInfo.InvariantCulture)).Append("MB ");
            return sb.ToString().Trim();
        }

        /// <summary>Changes a render pipeline value, remembering the original once (restored on quit).</summary>
        static void SetPipeline(object pipeline, string name, object value)
        {
            var p = pipeline.GetType().GetProperty(name);
            if (p != null && p.CanRead && !PipelineChanges.Exists(c => c.name == name)) PipelineChanges.Add((name, p.GetValue(pipeline)));
            SetProperty(pipeline, name, value);
        }

        static float RenderScale()
        {
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            var p = pipeline != null ? pipeline.GetType().GetProperty("renderScale") : null;
            return p != null ? (float)p.GetValue(pipeline) : -1f;
        }

        static void SetProperty(object target, string name, object value)
        {
            var p = target.GetType().GetProperty(name);
            if (p != null && p.CanWrite) p.SetValue(target, value);
            else Debug.LogWarning($"[Bench] {target.GetType().Name}.{name} is not settable");
        }

        // ---- Plumbing ------------------------------------------------------------------------------------------------

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern double V5BenchHeapBytes();
        static float HeapMB() => (float)(V5BenchHeapBytes() / (1024.0 * 1024.0));
#else
        static float HeapMB() => -1f;
#endif

        static string Get(string key) => _args != null && _args.TryGetValue(key, out var v) ? v : null;
        static int Int(string key, int fallback) => int.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        static float Float(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 1f;

        /// <summary>The query after "-v5bench" on a command line (Android: the launch intent's "unity" extra); empty when absent.</summary>
        public static Dictionary<string, string> ParseCommandLine(string[] line)
        {
            if (line != null)
                for (var i = 0; i < line.Length - 1; i++)
                    if (line[i] == "-v5bench") return Parse("?" + line[i + 1]);
            return new Dictionary<string, string>();
        }

        public static Dictionary<string, string> Parse(string url)
        {
            var args = new Dictionary<string, string>();
            var q = string.IsNullOrEmpty(url) ? -1 : url.IndexOf('?');
            if (q < 0) return args;
            var query = url.Substring(q + 1);
            var hash = query.IndexOf('#');
            if (hash >= 0) query = query.Substring(0, hash);
            foreach (var pair in query.Split('&'))
            {
                if (pair.Length == 0) continue;
                var eq = pair.IndexOf('=');
                var key = Uri.UnescapeDataString(eq < 0 ? pair : pair.Substring(0, eq));
                args[key] = eq < 0 ? "1" : Uri.UnescapeDataString(pair.Substring(eq + 1));
            }
            return args;
        }

        static string Json((string key, object value)[] fields)
        {
            var sb = new StringBuilder("{");
            for (var i = 0; i < fields.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(fields[i].key).Append("\":");
                switch (fields[i].value)
                {
                    case null: sb.Append("null"); break;
                    case string s: sb.Append('"').Append(s.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"'); break;
                    case bool b: sb.Append(b ? "true" : "false"); break;
                    case float f: sb.Append(f.ToString("0.###", CultureInfo.InvariantCulture)); break;
                    case IFormattable n: sb.Append(n.ToString(null, CultureInfo.InvariantCulture)); break;
                    default: sb.Append('"').Append(fields[i].value).Append('"'); break;
                }
            }
            return sb.Append('}').ToString();
        }
    }
}
