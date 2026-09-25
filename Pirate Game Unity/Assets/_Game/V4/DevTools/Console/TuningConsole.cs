using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.Enemies;
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
        Rect _window = new Rect(20, 210, 460, 560); // below the HUD profile panel
        Vector2 _scroll;
        string _search = string.Empty;
        bool _open;
        bool _pauseWhileOpen;
        bool _pausedByConsole;
        float _fps;
        EnemyWorld _world;

        public V4Game Game { get; private set; }
        public bool IsOpen => _open;

        public void Bind(V4Game game)
        {
            Game = game;
            if (_sections.Count == 0) AddSection(new RunSection());
        }

        public void AddSection(IConsoleSection section)
        {
            foreach (var s in _sections) if (s.Title == section.Title) return;
            _sections.Add(section);
        }

        void Update()
        {
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.1f);
            var toggle = (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                         || (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame);
            if (toggle) SetOpen(!_open);
        }

        public void SetOpen(bool open)
        {
            _open = open;
            if (!open) { GUIUtility.keyboardControl = 0; SetGameKeyboardBlocked(false); }
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

            _window = GUI.Window(0x7634, _window, DrawWindow, "V4 Tuning Console (F1)");
            if (Event.current.type == EventType.Repaint) SetGameKeyboardBlocked(GUIUtility.keyboardControl != 0);
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

            if (GUILayout.Button("Close")) SetOpen(false);
            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }

        bool Matches(IConsoleSection section)
        {
            if (string.IsNullOrWhiteSpace(_search)) return true;
            var needle = _search.Trim();
            return section.Title.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0
                   || (section.SearchText ?? string.Empty).IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void DrawHeader()
        {
            var run = RunManager.Instance;
            if (_world == null) _world = FindFirstObjectByType<EnemyWorld>();
            var t = run != null ? run.ElapsedSeconds : 0f;
            var alive = _world != null ? _world.ActiveCount : 0;
            GUILayout.Label($"Run {Mathf.FloorToInt(t / 60f)}:{Mathf.FloorToInt(t % 60f):00}   Phase —   Pressure —   Enemies {alive}   Pools —   FPS {_fps:0}",
                GUI.skin.box);
        }
    }
}
