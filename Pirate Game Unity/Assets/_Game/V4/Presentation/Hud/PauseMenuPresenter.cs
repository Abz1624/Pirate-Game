using RoguePirates.V4.Foundation.Platform;
using RoguePirates.V4.Presentation.Menu;
using RoguePirates.V4.Views;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Hud
{
    /// <summary>
    /// V5 M7: the player's pause (the gamepad Start button or the touch pause button): Resume, Settings and Quit to
    /// the menu. It pauses only a running run (never over the level-up cards or a chest choice) through
    /// <see cref="IHudCommands.TogglePause"/>.
    /// </summary>
    public sealed class PauseMenuPresenter : MonoBehaviour
    {
        IRunViews _views;
        RectTransform _panel;
        Button _resume;
        SettingsPanel _settings;

        public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;
        public SettingsPanel Settings => _settings;

        public static PauseMenuPresenter Create(Transform runCanvas)
        {
            var existing = runCanvas.GetComponentInChildren<PauseMenuPresenter>(true);
            if (existing != null) return existing;
            var go = new GameObject("V5PauseMenu", typeof(RectTransform));
            go.transform.SetParent(runCanvas, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var presenter = go.AddComponent<PauseMenuPresenter>();
            presenter.Build(rt);
            return presenter;
        }

        void Build(RectTransform root)
        {
            var shade = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            shade.transform.SetParent(root, false);
            _panel = (RectTransform)shade.transform;
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.sizeDelta = new Vector2(520f, 380f);
            shade.GetComponent<Image>().color = new Color(0.06f, 0.1f, 0.16f, 0.96f);
            var v = shade.GetComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(40, 40, 40, 40);
            v.spacing = 24f;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            _resume = SettingsPanel.CreateButton(_panel, "Resume", Resume);
            SettingsPanel.CreateButton(_panel, "Settings", OpenSettings);
            SettingsPanel.CreateButton(_panel, "Quit to menu", Quit);
            _settings = SettingsPanel.Create(root);
            shade.SetActive(false);
        }

        public void Bind(IRunViews views) => _views = views;

        void Update()
        {
            // Something else ended the player's pause (the tuning console, the run ending): close the menu.
            var run = RoguePirates.V4.Foundation.Core.RunManager.Instance;
            if (IsOpen && run != null && run.PauseReason != RoguePirates.V4.Foundation.Core.PauseReason.UserPause && (_settings == null || !_settings.IsOpen))
                _panel.gameObject.SetActive(false);
            // Playtest (3 Oct, owner): Escape (and Android Back, which arrives as Escape) no longer pauses a running run; it
            // only closes the settings panel or the pause menu when one is already open.
            var escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            if (escape && _settings != null && _settings.IsOpen) { _settings.Close(); return; }
            if (escape && IsOpen) { Toggle(); return; }
            var wanted = TouchActionButton.Consume(TouchAction.Pause)
                || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
            if (!wanted) return;
            if (_settings != null && _settings.IsOpen) { _settings.Close(); return; }
            Toggle();
        }

        /// <summary>Opens the pause menu (pausing the run) or closes it (resuming); nothing while the run waits on a card.</summary>
        public void Toggle()
        {
            var commands = _views?.Commands;
            if (commands == null) return;
            var paused = commands.TogglePause();
            _panel.gameObject.SetActive(paused);
            if (paused)
            {
                transform.SetAsLastSibling();
                UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(_resume.gameObject); // gamepad navigation
            }
        }

        public void Resume()
        {
            if (IsOpen) Toggle();
        }

        void OpenSettings()
        {
            _panel.gameObject.SetActive(false);
            _settings.Open(() => _panel.gameObject.SetActive(true));
        }

        void Quit()
        {
            Resume();
            _views?.Commands?.GoToMenu();
        }
    }
}
