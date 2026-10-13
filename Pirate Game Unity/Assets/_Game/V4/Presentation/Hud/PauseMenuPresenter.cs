using RoguePirates.V4.Foundation.Platform;
using RoguePirates.V4.Presentation.Menu;
using RoguePirates.V4.Views;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Hud
{
    /// <summary>
    /// V5 M7: the player's pause (the gamepad Start button or the touch pause button): Resume, Settings and Quit to
    /// the menu. It pauses only a running run (never over the level-up cards or a chest choice) through
    /// <see cref="IHudCommands.TogglePause"/>.
    /// Hackathon (13 Oct, owner: "a pause button so that players can pause or exit the match"): the on-screen pause button,
    /// Escape and the Android back button (which arrives as Escape) open and close it again. In a 1v1
    /// (<see cref="MatchMode"/>), which is online and cannot pause, they open the match menu instead (Resume, Leave match
    /// with a forfeit confirm) and the match keeps running; Leave goes through the 1v1's own leave path (the HUD's menu
    /// command: the match controller leaves the lobby, a forfeit, and returns to the menu).
    /// </summary>
    public sealed class PauseMenuPresenter : MonoBehaviour
    {
        /// <summary>The pause and match panels' canvas order (over the raid / race HUD canvases and the pause button).</summary>
        public const int PanelSortingOrder = 610;

        IRunViews _views;
        RectTransform _panel;
        Button _resume;
        SettingsPanel _settings;
        RectTransform _match;
        Button _matchResume, _matchLeave;
        TextMeshProUGUI _matchText;
        bool _confirmLeave, _leaving;

        public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;
        public bool MatchMenuOpen => _match != null && _match.gameObject.activeSelf;
        public bool ConfirmingLeave => MatchMenuOpen && _confirmLeave;
        public SettingsPanel Settings => _settings;

        /// <summary>True in a 1v1: the pause button opens the match menu and nothing pauses.</summary>
        public bool MatchMode { get; set; }

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
            var settings = SettingsPanel.CreateButton(_panel, "Settings", OpenSettings);
            var quit = SettingsPanel.CreateButton(_panel, "Quit to menu", Quit);
            // Hackathon UI (13 Oct, owner): the parchment kit (a sheet under a wood "Paused" header, kit buttons with icons).
            if (MenuKit.Ready)
            {
                _panel.sizeDelta = new Vector2(560f, 440f);
                MenuKit.Sheet(shade.GetComponent<Image>());
                v.padding = new RectOffset(48, 48, 120, 44);
                v.spacing = 20f;
                MenuKit.WoodHeader(_panel, "Paused", 92f, out _, 24f);
                MenuKit.StyleButton(_resume, "Btn_Gold");
                MenuKit.ButtonIcon(_resume, "Icon_Play");
                MenuKit.StyleButton(settings, "Btn_Parchment");
                MenuKit.ButtonIcon(settings, "Icon_Gear");
                MenuKit.StyleButton(quit, "Btn_Parchment");
                MenuKit.ButtonIcon(quit, "Icon_House");
            }
            SettingsPanel.DrawOnTop(shade, PanelSortingOrder);
            _settings = SettingsPanel.Create(root);
            var settingsCanvas = _settings.GetComponent<Canvas>();
            if (settingsCanvas != null) settingsCanvas.sortingOrder = PanelSortingOrder + 1;
            shade.SetActive(false);
            BuildMatchMenu(root);
        }

        /// <summary>Hackathon (13 Oct, owner): the 1v1's small kit panel "Match menu": Resume and Leave match (with a confirm).</summary>
        void BuildMatchMenu(RectTransform root)
        {
            var go = new GameObject("MatchMenu", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            go.transform.SetParent(root, false);
            _match = (RectTransform)go.transform;
            _match.anchorMin = _match.anchorMax = new Vector2(0.5f, 0.5f);
            _match.sizeDelta = new Vector2(560f, 420f);
            go.GetComponent<Image>().color = new Color(0.06f, 0.1f, 0.16f, 0.96f);
            var v = go.GetComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(40, 40, 40, 40);
            v.spacing = 20f;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            textGo.transform.SetParent(_match, false);
            textGo.GetComponent<LayoutElement>().preferredHeight = 64f;
            _matchText = textGo.GetComponent<TextMeshProUGUI>();
            _matchText.fontSize = 28f;
            _matchText.alignment = TextAlignmentOptions.Center;
            _matchText.color = new Color(0.95f, 0.93f, 0.88f, 1f);
            _matchText.textWrappingMode = TextWrappingModes.Normal;
            _matchText.raycastTarget = false;
            _matchResume = SettingsPanel.CreateButton(_match, "Resume", MatchResume);
            _matchLeave = SettingsPanel.CreateButton(_match, "Leave match", MatchLeave);
            if (MenuKit.Ready)
            {
                MenuKit.Sheet(go.GetComponent<Image>());
                v.padding = new RectOffset(48, 48, 116, 40);
                MenuKit.WoodHeader(_match, "Match menu", 92f, out _, 24f);
                MenuKit.InkText(_matchText, MenuKit.Ink);
                MenuKit.StyleButton(_matchResume, "Btn_Gold");
                MenuKit.ButtonIcon(_matchResume, "Icon_Play");
                MenuKit.StyleButton(_matchLeave, "Btn_Parchment");
                MenuKit.ButtonIcon(_matchLeave, "Icon_Flag");
            }
            SettingsPanel.DrawOnTop(go, PanelSortingOrder);
            go.SetActive(false);
        }

        public void Bind(IRunViews views) => _views = views;

        void Update()
        {
            if (MatchMode) { UpdateMatch(); return; }
            // Something else ended the player's pause (the tuning console, the run ending): close the menu.
            var run = RoguePirates.V4.Foundation.Core.RunManager.Instance;
            if (IsOpen && run != null && run.PauseReason != RoguePirates.V4.Foundation.Core.PauseReason.UserPause && (_settings == null || !_settings.IsOpen))
                _panel.gameObject.SetActive(false);
            // Escape (and Android Back, which arrives as Escape) closes the settings panel first, else opens or closes the
            // pause menu (Hackathon, 13 Oct, owner: back opens it again; on 3 Oct it only closed it).
            var escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            var wanted = escape || TouchActionButton.Consume(TouchAction.Pause)
                || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
            if (!wanted) return;
            if (_settings != null && _settings.IsOpen) { _settings.Close(); return; }
            Toggle();
        }

        void UpdateMatch()
        {
            var escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            var wanted = escape || TouchActionButton.Consume(TouchAction.Pause)
                || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
            var run = RoguePirates.V4.Foundation.Core.RunManager.Instance;
            // The match ended (its results screen has the way back): no match menu over it.
            if (MatchMenuOpen && run != null && run.IsRunEnded) { _match.gameObject.SetActive(false); return; }
            if (!wanted) return;
            if (MatchMenuOpen && _confirmLeave && escape) { ShowMatch(false); return; } // back from the confirm
            ToggleMatch();
        }

        void ToggleMatch()
        {
            if (_leaving) return;
            if (MatchMenuOpen) { _match.gameObject.SetActive(false); return; }
            var run = RoguePirates.V4.Foundation.Core.RunManager.Instance;
            if (run != null && run.IsRunEnded) return;
            ShowMatch(false);
            _match.gameObject.SetActive(true);
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(_matchResume.gameObject);
        }

        void ShowMatch(bool confirm)
        {
            _confirmLeave = confirm;
            _matchText.text = confirm ? "Leave? You forfeit the match." : "The match keeps running.";
            if (MenuKit.Ready) _matchText.color = confirm ? MenuKit.InkBad : MenuKit.InkDim;
            SetLabel(_matchResume, confirm ? "Stay" : "Resume");
            SetLabel(_matchLeave, confirm ? "Leave and forfeit" : "Leave match");
        }

        static void SetLabel(Button b, string text)
        {
            var t = b != null ? b.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            if (t != null) t.text = text;
        }

        void MatchResume()
        {
            if (_confirmLeave) { ShowMatch(false); return; }
            _match.gameObject.SetActive(false);
        }

        void MatchLeave()
        {
            if (!_confirmLeave) { ShowMatch(true); return; }
            if (_leaving) return;
            // 13 Oct (review): without the run's commands there is no way out from here: stay usable (no stuck flag).
            var commands = _views?.Commands;
            if (commands == null) { ShowMatch(false); return; }
            _leaving = true;
            _match.gameObject.SetActive(false);
            try { commands.GoToMenu(); } // the 1v1's leave path: leaves the match (a forfeit) and returns to the menu
            catch (System.Exception e)
            {
                _leaving = false; // the leave did not happen: the button works again
                Debug.LogException(e);
            }
        }

        /// <summary>Opens the pause menu (pausing the run) or closes it (resuming); nothing while the run waits on a card.
        /// In a 1v1 it opens or closes the match menu (nothing pauses).</summary>
        public void Toggle()
        {
            if (MatchMode) { ToggleMatch(); return; }
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
