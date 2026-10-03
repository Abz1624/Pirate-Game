using System;
using RoguePirates.V4.Online;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Online
{
    /// <summary>
    /// Playtest (3 Oct, owner): the online pieces of the main menu: the profile chip (name#tag and the connection; opens
    /// the profile panel), the leaderboard panel (the art menu's Leaderboard button), and a "PvP (1v1)" button next to it
    /// with a placeholder panel (the PvP task replaces it). Connects to the API on start; the first launch asks for a name.
    /// </summary>
    public sealed class OnlineMenuPresenter : MonoBehaviour
    {
        Button _chip;
        TextMeshProUGUI _chipName, _chipStatus;
        Image _dot;

        public LeaderboardPanel Leaderboard { get; private set; }
        public ProfilePanel Profile { get; private set; }
        public RectTransform PvpPanel { get; private set; }
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
            PvpButton = BuildPvpButton(canvas, main);
            Leaderboard = LeaderboardPanel.Create(canvas);
            Profile = ProfilePanel.Create(canvas);
            PvpPanel = BuildPvpPanel(canvas);
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
            _chip = go.GetComponent<Button>();
            _chip.onClick.AddListener(OpenProfile);
            var dot = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(rt, false);
            var drt = (RectTransform)dot.transform;
            drt.anchorMin = drt.anchorMax = new Vector2(0f, 0.5f);
            drt.sizeDelta = new Vector2(18f, 18f);
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

        RectTransform BuildPvpPanel(Transform canvas)
        {
            var rt = OnlineUi.Panel(canvas, "PlaytestPvp", new Vector2(0.28f, 0.3f), new Vector2(0.72f, 0.7f), 222);
            var header = OnlineUi.TopStrip(rt, "Header", 16f, 80f);
            OnlineUi.TextAt(header, "Title", "PvP (1v1)", 44, TextAlignmentOptions.MidlineLeft, OnlineUi.Gold, Vector2.zero, new Vector2(0.7f, 1f), true);
            OnlineUi.Button(header, "Close", () => rt.gameObject.SetActive(false), new Vector2(0.72f, 0.05f), new Vector2(1f, 0.95f));
            var body = OnlineUi.TextAt(rt, "Body", "Coming next: host a match, join with a code.\nDuel another captain with a drafted build.", 32,
                TextAlignmentOptions.Center, OnlineUi.TextColor, Vector2.zero, Vector2.one);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.rectTransform.offsetMin = new Vector2(32f, 32f);
            body.rectTransform.offsetMax = new Vector2(-32f, -112f);
            return rt;
        }

        void OnEnable() => OnlineService.Changed += RefreshChip;
        void OnDisable() => OnlineService.Changed -= RefreshChip;

        void Start()
        {
            RefreshChip();
            Connect();
            // First launch: ask for a name (a default is filled in; closing keeps it).
            if (OnlineService.Profiles.Profiles.Count == 0) Profile.Open();
        }

        async void Connect()
        {
            try { await OnlineService.EnsureConnected(); }
            catch (Exception e) { Debug.LogWarning("OnlineMenuPresenter: " + e.Message); }
            if (this != null) RefreshChip();
        }

        public void OpenLeaderboard() => Leaderboard.Open();
        public void OpenProfile() => Profile.Open();

        public void OpenPvp()
        {
            PvpPanel.gameObject.SetActive(true);
            PvpPanel.SetAsLastSibling();
        }

        void RefreshChip()
        {
            if (_chipName == null) return;
            var p = OnlineService.Active;
            _chipName.text = p != null ? p.name + "<color=#9aa4ad>" + (p.tag ?? string.Empty) + "</color>" : "New captain";
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
