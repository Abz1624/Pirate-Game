using System.Collections.Generic;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Enemies;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RoguePirates.V4.DevTools.Console
{
    /// <summary>One collapsible section of the Tuning Console. Sections arrive with their module (Run in M0).</summary>
    public interface IConsoleSection
    {
        string Title { get; }
        /// <summary>Extra words the search box matches (field names, ids).</summary>
        string SearchText { get; }
        void Draw(TuningConsole console);
    }

    /// <summary>
    /// In-game Tuning Console (development builds and the Editor only). F1, gamepad Select or the gear opens it.
    /// The game keeps running unless "Pause while open" is on. Always-visible header: run time, phase, Pressure,
    /// enemies alive, pools, FPS.
    /// </summary>
    public sealed class TuningConsole : MonoBehaviour
    {
        readonly List<IConsoleSection> _sections = new List<IConsoleSection>();
        readonly HashSet<string> _collapsed = new HashSet<string>();
        Rect _window = new Rect(20, 150, 600, 720); // below the HUD profile panel; resizable, remembered
        const string RectPrefKey = "v4_console_rect";
        const float MinWidth = 380f, MinHeight = 260f, Grip = 8f;
        int _resizeEdges; // bit 1 left, 2 right, 4 top, 8 bottom
        bool _rectLoaded;
        Vector2 _scroll;
        string _search = string.Empty;
        bool _open;
        bool _pauseWhileOpen;
        bool _pausedByConsole;
        float _fps;

        public V4Game Game { get; private set; }
        /// <summary>Counts objects created outside the V4 view pool (Pools section; off until switched on).</summary>
        public Diagnostics.SceneObjectCreationProbe CreationProbe { get; private set; }

        readonly List<System.Func<float, bool>> _everyFrame = new List<System.Func<float, bool>>();

        /// <summary>Runs a task each frame (unscaled time) until it returns true, whether or not the console is open.</summary>
        public void RunEveryFrame(System.Func<float, bool> task) { if (task != null) _everyFrame.Add(task); }
        public bool IsOpen => _open;
        /// <summary>The search box text; sections may filter their rows with it.</summary>
        public string SearchText => _search ?? string.Empty;

        public void Bind(V4Game game)
        {
            Game = game;
            if (_sections.Count == 0)
            {
                AddSection(new RunSection());
                AddSection(new TimelineSection());
                AddSection(new ConfigSection());
                AddSection(new PoolsSection());
                AddSection(new WeaponsSection());
                AddSection(new BuildSection()); // M9
                AddSection(new DifficultySection()); // M10
                // V5 M8z (I8z.7): the in-engine simulator's Balance section is retired; the balance model is the JavaScript
                // Monte Carlo (plan/playtest/montecarlo), fed by the Editor export of these configs.
                AddSection(new RumSection()); // M12
                AddSection(new NarrativeSection()); // M13
                AddSection(new EventsSection(game));
                AddSection(new StageSection());
                if (game.IsModuleLab) AddSection(new ModuleLabSection());
            }
            if (CreationProbe == null) CreationProbe = GetComponent<Diagnostics.SceneObjectCreationProbe>();
            if (CreationProbe == null) CreationProbe = gameObject.AddComponent<Diagnostics.SceneObjectCreationProbe>();
        }

        public void AddSection(IConsoleSection section)
        {
            foreach (var s in _sections) if (s.Title == section.Title) return;
            _sections.Add(section);
        }

        void Update()
        {
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.1f);
            for (var i = _everyFrame.Count - 1; i >= 0; i--)
            {
                bool done;
                try { done = _everyFrame[i](Time.unscaledDeltaTime); }
                catch (System.Exception e) { Debug.LogException(e); done = true; } // a failing task stops instead of throwing every frame
                if (done) _everyFrame.RemoveAt(i);
            }
            var toggle = (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                         || (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame);
            if (toggle) SetOpen(!_open);
            if (Game != null) StageSection.PollSummonKey(this);
        }

        public void SetOpen(bool open)
        {
            _open = open;
            // M11: a run that opened the developer console (edits, cheats, spawns) is never ranked.
            if (open && Game?.Context != null && Game.Context.TryGet<RoguePirates.V4.Difficulty.IPressureView>(out var pressure)) pressure.MarkUnranked();
            if (!open) { GUIUtility.keyboardControl = 0; SetGameKeyboardBlocked(false); if (_rectLoaded) SaveRect(); }
            ApplyPause();
        }

        void ApplyPause()
        {
            var run = RunManager.Instance;
            if (run == null) return;
            if (_open && _pauseWhileOpen && !run.IsPaused && !run.IsRunEnded)
            {
                run.SetPause(PauseReason.UserPause);
                _pausedByConsole = true;
            }
            else if ((!_open || !_pauseWhileOpen) && _pausedByConsole)
            {
                if (run.PauseReason == PauseReason.UserPause) run.SetPause(PauseReason.None);
                _pausedByConsole = false;
            }
        }

        void OnDisable()
        {
            if (_rectLoaded) SaveRect();
            _open = false;
            ApplyPause();
            SetGameKeyboardBlocked(false);
        }

        void OnDestroy()
        {
            SetGameKeyboardBlocked(false);
            RunSection.RestoreTimeScale(); // a slowed/frozen run must not carry into the next scene
        }

        bool _keyboardBlocked;

        /// <summary>
        /// While a console text field has focus, gameplay must not read the keys being typed (B summons the boss,
        /// digits pick level-up cards). IMGUI text input comes from GUI events, not the Input System keyboard device,
        /// so disabling the device only mutes gameplay.
        /// </summary>
        void SetGameKeyboardBlocked(bool blocked)
        {
            if (blocked == _keyboardBlocked) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) { _keyboardBlocked = false; return; }
            if (blocked) InputSystem.DisableDevice(keyboard); else InputSystem.EnableDevice(keyboard);
            _keyboardBlocked = blocked;
        }

        void OnGUI()
        {
            if (!_open)
            {
                if (GUI.Button(new Rect(Screen.width - 44, 8, 36, 28), "⚙")) SetOpen(true);
                return;
            }

            LoadRect();
            HandleResize(Event.current);
            _window = GUI.Window(0x7634, _window, DrawWindow, "V4 Tuning Console (F1)");
            ClampToScreen();
            if (Event.current.type == EventType.Repaint) SetGameKeyboardBlocked(GUIUtility.keyboardControl != 0);
        }

        /// <summary>Drag any edge or corner to resize (outside the window's own controls); the size and position are remembered.</summary>
        void HandleResize(Event e)
        {
            var mouse = e.mousePosition;
            // Released outside the game view: IMGUI never sends the MouseUp, so end the resize here.
            if (_resizeEdges != 0 && Mouse.current != null && !Mouse.current.leftButton.isPressed)
            {
                _resizeEdges = 0;
                SaveRect();
                return;
            }
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                var outer = new Rect(_window.x - Grip, _window.y - Grip, _window.width + 2 * Grip, _window.height + 2 * Grip);
                if (!outer.Contains(mouse)) return;
                _resizeEdges = 0;
                if (mouse.x <= _window.x + Grip) _resizeEdges |= 1;
                if (mouse.x >= _window.xMax - Grip) _resizeEdges |= 2;
                if (mouse.y <= _window.y + 2f && mouse.y >= _window.y - Grip) _resizeEdges |= 4; // top edge (the title bar moves the window)
                if (mouse.y >= _window.yMax - Grip) _resizeEdges |= 8;
                if (_resizeEdges != 0) e.Use();
            }
            else if (e.type == EventType.MouseDrag && _resizeEdges != 0)
            {
                if ((_resizeEdges & 1) != 0) { var right = _window.xMax; _window.xMin = Mathf.Min(mouse.x, right - MinWidth); _window.xMax = right; }
                if ((_resizeEdges & 2) != 0) _window.width = Mathf.Max(MinWidth, mouse.x - _window.x);
                if ((_resizeEdges & 4) != 0) { var bottom = _window.yMax; _window.yMin = Mathf.Min(mouse.y, bottom - MinHeight); _window.yMax = bottom; }
                if ((_resizeEdges & 8) != 0) _window.height = Mathf.Max(MinHeight, mouse.y - _window.y);
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _resizeEdges != 0)
            {
                _resizeEdges = 0;
                SaveRect();
                e.Use();
            }
        }

        void ClampToScreen()
        {
            _window.width = Mathf.Clamp(_window.width, MinWidth, Mathf.Max(MinWidth, Screen.width));
            _window.height = Mathf.Clamp(_window.height, MinHeight, Mathf.Max(MinHeight, Screen.height));
            // Keep the whole window on screen (a size saved on a larger screen shrinks to fit).
            _window.x = Mathf.Clamp(_window.x, 0f, Mathf.Max(0f, Screen.width - _window.width));
            _window.y = Mathf.Clamp(_window.y, 0f, Mathf.Max(0f, Screen.height - _window.height));
            if (Event.current.type == EventType.MouseUp) SaveRect(); // after moving by the title bar
        }

        void LoadRect()
        {
            if (_rectLoaded) return;
            _rectLoaded = true;
            try
            {
                var parts = PlayerPrefs.GetString(RectPrefKey, string.Empty).Split(',');
                if (parts.Length == 4 && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) &&
                    float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y) &&
                    float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w) &&
                    float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var h))
                    _window = new Rect(x, y, w, h);
                else
                    _window = new Rect(20f, 150f, Mathf.Min(640f, Screen.width * 0.45f), Mathf.Max(MinHeight, Screen.height - 190f));
            }
            catch (System.Exception) { }
        }

        void SaveRect()
        {
            try
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                PlayerPrefs.SetString(RectPrefKey, string.Join(",", _window.x.ToString("0", inv), _window.y.ToString("0", inv), _window.width.ToString("0", inv), _window.height.ToString("0", inv)));
            }
            catch (System.Exception) { }
        }

        void DrawWindow(int id)
        {
            // Default IMGUI windows are see-through; darken behind the content so it reads over the HUD.
            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(new Rect(0, 0, _window.width, _window.height), Texture2D.whiteTexture);
            GUI.color = previous;
            DrawHeader();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", GUILayout.Width(48));
            _search = GUILayout.TextField(_search ?? string.Empty);
            GUILayout.EndHorizontal();

            var pause = GUILayout.Toggle(_pauseWhileOpen, "Pause while open");
            if (pause != _pauseWhileOpen) { _pauseWhileOpen = pause; ApplyPause(); }

            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var section in _sections)
            {
                if (!Matches(section)) continue;
                var collapsed = _collapsed.Contains(section.Title);
                if (GUILayout.Button((collapsed ? "▶ " : "▼ ") + section.Title, GUI.skin.box))
                {
                    if (collapsed) _collapsed.Remove(section.Title); else _collapsed.Add(section.Title);
                }
                if (!collapsed) section.Draw(this);
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Close")) SetOpen(false);
            GUILayout.Label("drag an edge or corner to resize", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }

        bool Matches(IConsoleSection section)
        {
            if (string.IsNullOrWhiteSpace(_search)) return true;
            var needle = _search.Trim();
            return section.Title.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0
                   || (section.SearchText ?? string.Empty).IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        string PhaseLabel()
        {
            if (Game == null || Game.Context == null || !Game.Context.TryGet<RoguePirates.V4.Spawning.ISpawnController>(out var spawn)) return "—";
            return string.IsNullOrEmpty(spawn.CurrentPhaseLabel) ? "—" : spawn.CurrentPhaseLabel;
        }

        RoguePirates.V4.Presentation.V4PresentationRoot _presentation;

        string PoolsLabel()
        {
            if (_presentation == null) _presentation = FindFirstObjectByType<RoguePirates.V4.Presentation.V4PresentationRoot>();
            var pools = _presentation != null ? _presentation.Pools : null;
            if (pools == null) return "—";
            int inUse = 0, created = pools.TotalCreatedAfterPrewarm;
            foreach (var st in pools.Stats()) inUse += st.InUse;
            return created > 0 ? $"{inUse} (+{created})" : inUse.ToString();
        }

        void DrawHeader()
        {
            var run = RunManager.Instance;
            RoguePirates.V4.Enemies.IEnemyWorldView enemies = null;
            Game?.Context?.TryGet(out enemies);
            var t = run != null ? run.ElapsedSeconds : 0f;
            var alive = enemies != null ? enemies.ActiveCount : 0;
            RoguePirates.V4.Difficulty.IPressureView pressure = null;
            Game?.Context?.TryGet(out pressure);
            var pressureText = pressure != null ? pressure.Pressure.ToString("0.00") : "—";
            GUILayout.Label($"Run {Mathf.FloorToInt(t / 60f)}:{Mathf.FloorToInt(t % 60f):00}   Phase {PhaseLabel()}   Pressure {pressureText}   Enemies {alive}   Pools {PoolsLabel()}   FPS {_fps:0}",
                GUI.skin.box);
        }
    }
}
