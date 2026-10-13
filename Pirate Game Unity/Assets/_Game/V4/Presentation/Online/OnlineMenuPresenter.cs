using System;
using RoguePirates.V4.Online;
using RoguePirates.V4.Presentation.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Online
{
    /// <summary>
    /// Playtest (3 Oct, owner): the online pieces of the main menu: the profile chip (name#tag and the connection; opens
    /// the profile panel), the leaderboard panel (the art menu's Leaderboard button), and a "PvP (1v1)" button next to it
    /// opening the 1v1 menu (<see cref="PvpMenu"/>). Connects to the API on start; the first launch asks for a name. An
    /// invite link (?join=CODE on WebGL) asks for a name first when there is no profile, then joins and opens the draft.
    /// </summary>
    public sealed class OnlineMenuPresenter : MonoBehaviour
    {
        Button _chip;
        TextMeshProUGUI _chipName, _chipStatus;
        Image _dot;

        public LeaderboardPanel Leaderboard { get; private set; }
        public ProfilePanel Profile { get; private set; }
        public PvpMenu Pvp { get; private set; }
        public Button PvpButton { get; private set; }
        public string ChipText => _chipName != null ? _chipName.text + " | " + _chipStatus.text : null;

        /// <summary>The online menu on <paramref name="canvas"/>. The chip and PvP button go on the art menu's main panel
        /// (hidden with it on Captain select); the panels on the canvas.</summary>
        public static OnlineMenuPresenter Create(Transform canvas)
        {
            if (canvas == null) return null;
            var go = new GameObject("PlaytestOnlineMenu", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var menu = go.AddComponent<OnlineMenuPresenter>();
            menu.Build(canvas);
            return menu;
        }

        void Build(Transform canvas)
        {
            var main = FindDeep(canvas, "MainMenuPanel");
            BuildChip(main != null ? main : transform);
            BuildLastVoyageButton(main != null ? main : transform);
            PvpButton = BuildPvpButton(canvas, main);
            Leaderboard = LeaderboardPanel.Create(canvas);
            Profile = ProfilePanel.Create(canvas);
            Pvp = PvpMenu.Create(canvas, null);
        }

        /// <summary>V5 M8z (7 Oct, owner): "Download last voyage" under the profile chip, once a voyage was recorded.</summary>
        /// <summary>Hackathon (13 Oct, owner): hide the voyage download buttons (menu and results) for now.</summary>
        public const bool HideDownloadButtons = true;

        void BuildLastVoyageButton(Transform parent)
        {
            // V5 M8z (7 Oct, D20): playtest builds only (DemoJuiceSettings.downloadSessionFiles).
            // Hackathon (13 Oct, owner): the button is hidden for now; voyages are still recorded.
            if (HideDownloadButtons) return;
            if (!RoguePirates.V4.Foundation.Core.DemoMode.DownloadSessionFiles || !RoguePirates.V4.Telemetry.SessionFile.HasLast) return;
            TextMeshProUGUI note = null;
            var button = OnlineUi.Button(parent, "Download last voyage", () =>
            {
                var message = RoguePirates.V4.Telemetry.SessionFile.DownloadLast();
                if (note != null) note.text = message;
            });
            var rt = (RectTransform)button.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(48f, -224f);
            rt.sizeDelta = new Vector2(420f, OnlineUi.RowHeight);
            note = OnlineUi.TextAt(parent, "LastVoyageNote", string.Empty, 18, TextAlignmentOptions.TopLeft, OnlineUi.TextColor, new Vector2(0f, 1f), new Vector2(0f, 1f));
            var nrt = note.rectTransform;
            nrt.pivot = new Vector2(0f, 1f);
            nrt.anchoredPosition = new Vector2(48f, -224f - OnlineUi.RowHeight - 6f);
            nrt.sizeDelta = new Vector2(560f, 40f);
            note.color = OnlineUi.TextColor; // over the scene, not on parchment
            MenuKit.ButtonIcon(button, "Icon_Save", 30f);
        }

        void BuildChip(Transform parent)
        {
            var go = new GameObject("PlaytestProfileChip", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            // Below the art menu's Profile button (top left).
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(48f, -128f);
            rt.sizeDelta = new Vector2(420f, 84f);
            go.GetComponent<Image>().color = new Color(0.06f, 0.1f, 0.16f, 0.82f);
            MenuKit.Paint(go.GetComponent<Image>(), "Panel_Card", 2.2f); // Hackathon UI (13 Oct): a parchment card
            _chip = go.GetComponent<Button>();
            if (MenuKit.Ready)
            {
                var c = ColorBlock.defaultColorBlock;
                c.highlightedColor = new Color(1f, 0.97f, 0.9f, 1f);
                c.pressedColor = new Color(0.85f, 0.8f, 0.7f, 1f);
                _chip.colors = c;
            }
            _chip.onClick.AddListener(OpenProfile);
            var dot = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(rt, false);
            var drt = (RectTransform)dot.transform;
            drt.anchorMin = drt.anchorMax = new Vector2(0f, 0.5f);
            drt.sizeDelta = MenuKit.Ready ? new Vector2(22f, 22f) : new Vector2(18f, 18f);
            drt.anchoredPosition = new Vector2(24f, 0f);
            _dot = dot.GetComponent<Image>();
            _dot.raycastTarget = false;
            _chipName = OnlineUi.TextAt(rt, "Name", string.Empty, 30, TextAlignmentOptions.MidlineLeft, OnlineUi.TextColor, new Vector2(0f, 0.42f), Vector2.one, true);
            _chipName.margin = new Vector4(46f, 0f, 12f, 0f);
            _chipStatus = OnlineUi.TextAt(rt, "Status", string.Empty, 20, TextAlignmentOptions.MidlineLeft, OnlineUi.Dim, Vector2.zero, new Vector2(1f, 0.45f));
            _chipStatus.margin = new Vector4(46f, 0f, 12f, 0f);
        }

        /// <summary>A copy of the art menu's Leaderboard button above it ("PVP (1V1)"), or a plain button when the art is missing.</summary>
        Button BuildPvpButton(Transform canvas, Transform main)
        {
            var source = main != null ? FindDeep(main, "Leaderboard") : null;
            Button button;
            if (source != null && source.GetComponent<Button>() != null)
            {
                var copy = Instantiate(source.gameObject, source.parent, false);
                copy.name = "PlaytestPvp";
                var srt = (RectTransform)source;
                var crt = (RectTransform)copy.transform;
                // One button spacing above the Leaderboard (the gap to Ledger below it), when the parent does not lay out.
                var ledger = FindDeep(main, "Ledger") as RectTransform;
                var step = ledger != null ? srt.anchoredPosition.y - ledger.anchoredPosition.y : srt.rect.height + 16f;
                if (Mathf.Abs(step) < 1f) step = srt.rect.height + 16f;
                crt.anchoredPosition = srt.anchoredPosition + new Vector2(0f, step);
                copy.transform.SetSiblingIndex(source.GetSiblingIndex());
                button = copy.GetComponent<Button>();
                button.onClick = new Button.ButtonClickedEvent();
                button.interactable = true;
                foreach (var t in copy.GetComponentsInChildren<TextMeshProUGUI>(true)) t.text = "PVP (1V1)";
                foreach (var t in copy.GetComponentsInChildren<Text>(true)) t.text = "PVP (1V1)";
                RoguePirates.V4.Presentation.Menu.ArtMenuView.FitSideButtons(main); // Playtest (4 Oct, owner): one width for the column
            }
            else
            {
                button = OnlineUi.Button(main != null ? main : transform, "PvP (1v1)", null);
                var rt = (RectTransform)button.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.sizeDelta = new Vector2(300f, OnlineUi.RowHeight);
                rt.anchoredPosition = new Vector2(-48f, 140f);
                button.onClick = new Button.ButtonClickedEvent();
            }
            button.onClick.AddListener(OpenPvp);
            return button;
        }

        void OnEnable() => OnlineService.Changed += RefreshChip;
        void OnDisable() => OnlineService.Changed -= RefreshChip;

        void Start()
        {
            RefreshChip();
            Connect();
            // First launch: ask for a name (a default is filled in; closing keeps it).
            if (OnlineService.Profiles.Profiles.Count == 0) Profile.Open();
            // Playtest (3 Oct, owner): an invite link, or back from a match with a rematch lobby.
            var code = JoinCodeFromUrl();
            if (!string.IsNullOrEmpty(code)) JoinFromInvite(code);
            else if (PvpSession.Exists && PvpSession.Instance.OpenLobbyInMenu)
            {
                PvpSession.Instance.OpenLobbyInMenu = false;
                Pvp.ShowLobby();
            }
        }

        /// <summary>The menu's config (the draft lists the content from it).</summary>
        public void SetConfig(Func<RoguePirates.V4.Config.ConfigSetRuntime> config) => Pvp?.SetConfig(config);

        /// <summary>?join=CODE of the page (WebGL), or a code set for testing (<see cref="PvpSession.PendingJoinCode"/>).</summary>
        static string JoinCodeFromUrl()
        {
            var code = PvpSession.PendingJoinCode;
            PvpSession.PendingJoinCode = null;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (string.IsNullOrEmpty(code))
            {
                code = OnlineRules.QueryValue(Application.absoluteURL, "join");
                if (!string.IsNullOrEmpty(code)) LobbySockets.DropJoinFromUrl();
            }
#endif
            return string.IsNullOrEmpty(code) ? null : code.Trim();
        }

        /// <summary>A player without a profile names their captain first (the profile panel), then joins.</summary>
        async void JoinFromInvite(string code)
        {
            try
            {
                while (this != null && (OnlineService.Profiles.Profiles.Count == 0 || Profile.IsOpen || Profile.Busy)) await Awaitable.NextFrameAsync();
                if (this == null) return;
                await OnlineService.EnsureConnected();
                if (this == null) return;
                Pvp.JoinCode(code);
            }
            catch (Exception e) { Debug.LogWarning("OnlineMenuPresenter.JoinFromInvite: " + e.Message); }
        }

        async void Connect()
        {
            try
            {
                // Already online (back from a match): read the profile again, its rating may have changed.
                if (OnlineService.IsOnline) await OnlineService.SyncActive();
                else await OnlineService.EnsureConnected();
            }
            catch (Exception e) { Debug.LogWarning("OnlineMenuPresenter: " + e.Message); }
            if (this != null) RefreshChip();
        }

        public void OpenLeaderboard() => Leaderboard.Open();
        public void OpenProfile() => Profile.Open();

        public void OpenPvp() => Pvp.Open();

        void RefreshChip()
        {
            if (_chipName == null) return;
            var p = OnlineService.Active;
            _chipName.text = MenuKit.InkRich(p != null ? p.name + "<color=#9aa4ad>" + (p.tag ?? string.Empty) + "</color>" : "New captain");
            string status;
            Color dot;
            switch (OnlineService.State)
            {
                case OnlineState.Online:
                    status = p != null && p.Registered ? $"Online   rating {p.rating}   tap to edit" : "Online   tap to edit";
                    dot = OnlineUi.Good;
                    break;
                case OnlineState.Checking:
                case OnlineState.Unknown:
                    status = "Connecting...";
                    dot = OnlineUi.Gold;
                    break;
                default:
                    status = "Offline   tap to edit";
                    dot = new Color(0.55f, 0.58f, 0.62f, 1f);
                    break;
            }
            _chipStatus.text = status;
            _dot.color = dot;
            // Hackathon UI: the kit's coloured dots.
            var kitDot = MenuKit.S(dot == OnlineUi.Good ? "Dot_Green" : dot == OnlineUi.Gold ? "Dot_Yellow" : "Dot_Beige");
            if (kitDot != null)
            {
                _dot.sprite = kitDot;
                _dot.preserveAspect = true;
                _dot.color = dot == OnlineUi.Good || dot == OnlineUi.Gold ? Color.white : new Color(0.7f, 0.66f, 0.6f, 1f);
            }
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
