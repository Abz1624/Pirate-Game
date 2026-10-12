using System;
using RoguePirates.V4.Online;
using RoguePirates.V4.Presentation.Menu;
using RoguePirates.V4.Presentation.Online;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RoguePirates.Hackathon.UI
{
    /// <summary>
    /// Hackathon (12 Oct, owner: web3 mode is the demo's path): a "Connect Wallet" button on the main menu (top centre) and
    /// the wallet panel (short address, devnet SOL balance, profile, optional email, disconnect). Attached to V4's online
    /// menu when the menu scene loads, so V4 code is unchanged. Reads <see cref="Web3Session"/> and its Changed event.
    /// </summary>
    public sealed class WalletMenu : MonoBehaviour
    {
        static readonly Color PanelColor = new Color(0.06f, 0.1f, 0.16f, 0.97f);
        static readonly Color TextColor = new Color(0.95f, 0.93f, 0.88f, 1f);
        static readonly Color Dim = new Color(0.7f, 0.72f, 0.75f, 1f);
        static readonly Color Gold = new Color(1f, 0.82f, 0.4f, 1f);
        static readonly Color Good = new Color(0.45f, 0.85f, 0.45f, 1f);
        static readonly Color Bad = new Color(1f, 0.55f, 0.45f, 1f);
        const float Row = 72f;

        OnlineMenuPresenter _online;
        Button _entry;
        RectTransform _panel;
        TextMeshProUGUI _address, _balance, _profile, _message;
        TMP_InputField _email;
        Button _connect, _test, _refresh, _airdrop, _signIn, _disconnect, _saveEmail;
        TMP_FontAsset _font;

        public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;
        public string EntryLabel => _entry != null ? _entry.GetComponentInChildren<TextMeshProUGUI>(true).text : null;
        public string MessageText => _message != null ? _message.text : null;
        static Web3Session S => Web3Session.Instance;

        // ---------------------------------------------------------------- attach

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryAttach();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryAttach();

        /// <summary>Adds the wallet menu to the main menu's online menu, once.</summary>
        public static WalletMenu TryAttach()
        {
            var online = FindFirstObjectByType<OnlineMenuPresenter>();
            if (online == null) return null;
            var existing = online.GetComponent<WalletMenu>();
            if (existing != null) return existing;
            var menu = online.gameObject.AddComponent<WalletMenu>();
            menu._online = online;
            menu.Build(online.transform.parent);
            return menu;
        }

        // ---------------------------------------------------------------- build

        void Build(Transform canvas)
        {
            var art = ArtMenuSettings.Load();
            _font = art != null ? art.font : null;

            // The entry: gold, top centre of the menu, above the art menu's layers.
            var main = FindDeep(canvas, "MainMenuPanel");
            var host = new GameObject("HackathonWalletEntry", typeof(RectTransform));
            host.transform.SetParent(main != null ? main : canvas, false);
            var hrt = (RectTransform)host.transform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 1f); // top centre (clear sky; the logo and RUM sit top right)
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.anchoredPosition = new Vector2(0f, -24f);
            hrt.sizeDelta = new Vector2(460f, 80f);
            SettingsPanel.DrawOnTop(host, 210);
            _entry = SettingsPanel.CreateButton(host.transform, "Connect Wallet", Open);
            Stretch((RectTransform)_entry.transform);
            var le = _entry.GetComponent<LayoutElement>();
            if (le != null) le.ignoreLayout = true;

            BuildPanel(canvas);
            S.Changed += Refresh;
            OnlineService.Changed += Refresh;
            Refresh();
        }

        void BuildPanel(Transform canvas)
        {
            var go = new GameObject("HackathonWalletPanel", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvas, false);
            _panel = (RectTransform)go.transform;
            _panel.anchorMin = new Vector2(0.18f, 0.2f);
            _panel.anchorMax = new Vector2(0.82f, 0.92f);
            _panel.offsetMin = _panel.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = PanelColor;
            SettingsPanel.DrawOnTop(go, 240);
            var blocker = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
            blocker.transform.SetParent(_panel, false);
            var brt = (RectTransform)blocker.transform;
            brt.anchorMin = new Vector2(-2f, -2f);
            brt.anchorMax = new Vector2(3f, 3f);
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            blocker.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

            var y = 16f;
            var header = Strip("Header", ref y, 80f);
            TextIn(header, "Title", "Pirate wallet (devnet)", 44, TextAlignmentOptions.MidlineLeft, Gold, Vector2.zero, new Vector2(0.72f, 1f), true);
            ButtonIn(header, "Close", Close, new Vector2(0.74f, 0.05f), new Vector2(1f, 0.95f));

            _address = TextIn(Strip("Address", ref y, 52f), "Address", string.Empty, 34, TextAlignmentOptions.MidlineLeft, TextColor, Vector2.zero, Vector2.one);
            _balance = TextIn(Strip("Balance", ref y, 44f), "Balance", string.Empty, 30, TextAlignmentOptions.MidlineLeft, Gold, Vector2.zero, Vector2.one);
            _profile = TextIn(Strip("Profile", ref y, 44f), "Profile", string.Empty, 28, TextAlignmentOptions.MidlineLeft, Dim, Vector2.zero, Vector2.one);
            y += 8f;

            var actions = Strip("Actions", ref y, Row);
            var h = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 12f;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = true;
            _connect = Flex(SettingsPanel.CreateButton(actions, "Connect Wallet", () => Run(S.Connect(false))));
            _test = Flex(SettingsPanel.CreateButton(actions, "Test wallet (dev)", () => Run(S.Connect(true))));
            _signIn = Flex(SettingsPanel.CreateButton(actions, "Sign in again", () => Run(S.SignIn())));
            _refresh = Flex(SettingsPanel.CreateButton(actions, "Refresh", () => Run(RefreshOnly())));
            _airdrop = Flex(SettingsPanel.CreateButton(actions, "Airdrop 1 SOL", () => Run(S.RequestAirdrop(1))));
            _disconnect = Flex(SettingsPanel.CreateButton(actions, "Disconnect", () => { S.Disconnect(); SetMessage("Disconnected.", Dim); }));
            y += 16f;

            TextIn(Strip("EmailLabel", ref y, 40f), "EmailLabel", "Email (optional, for match news)", 26, TextAlignmentOptions.MidlineLeft, Dim, Vector2.zero, Vector2.one);
            var email = Strip("Email", ref y, Row);
            var eh = email.gameObject.AddComponent<HorizontalLayoutGroup>();
            eh.spacing = 12f;
            eh.childControlWidth = eh.childControlHeight = true;
            eh.childForceExpandWidth = false;
            _email = Input(email, "you@example.com", 254);
            _email.contentType = TMP_InputField.ContentType.EmailAddress;
            _email.onSubmit.AddListener(_ => SaveEmail());
            _saveEmail = SettingsPanel.CreateButton(email, "Save email", SaveEmail);
            var sle = _saveEmail.GetComponent<LayoutElement>() ?? _saveEmail.gameObject.AddComponent<LayoutElement>();
            sle.minWidth = 240f;
            y += 12f;

            _message = TextIn(Strip("Message", ref y, 120f), "Message", string.Empty, 26, TextAlignmentOptions.TopLeft, Dim, Vector2.zero, Vector2.one);
            _message.textWrappingMode = TextWrappingModes.Normal;
            _message.overflowMode = TextOverflowModes.Overflow;

            go.SetActive(false);
        }

        // ---------------------------------------------------------------- behaviour

        System.Collections.IEnumerator Start()
        {
            yield return null; // after the online menu's Start (it opens the guest name prompt on a first launch)
            // Web3 mode: the first launch asks for a wallet instead of a guest name (hidden, not closed: closing would
            // make a guest profile).
            if (HackathonSettings.Load().web3Primary && OnlineService.Profiles.Profiles.Count == 0)
            {
                if (_online != null && _online.Profile != null && _online.Profile.IsOpen) _online.Profile.gameObject.SetActive(false);
                Open();
            }
        }

        void OnDestroy()
        {
            S.Changed -= Refresh;
            OnlineService.Changed -= Refresh;
        }

        public void Open()
        {
            if (_panel == null) return;
            _panel.gameObject.SetActive(true);
            _email.text = S.Email ?? string.Empty;
            if (string.IsNullOrEmpty(S.LastError)) SetMessage(string.Empty, Dim);
            Refresh();
            if (!string.IsNullOrEmpty(S.Address)) Run(RefreshOnly());
        }

        public void Close()
        {
            if (_panel != null) _panel.gameObject.SetActive(false);
        }

        async System.Threading.Tasks.Task<string> RefreshOnly()
        {
            await S.RefreshBalance();
            return null;
        }

        void SaveEmail() => Run(S.SetEmail(_email.text), "Email saved.");

        async void Run(System.Threading.Tasks.Task<string> task, string okMessage = null)
        {
            try
            {
                var error = await task;
                if (this == null) return;
                if (!string.IsNullOrEmpty(error)) SetMessage(error, Bad);
                else if (okMessage != null) SetMessage(okMessage, Good);
                Refresh();
            }
            catch (Exception e)
            {
                Debug.LogWarning("WalletMenu: " + e);
                if (this != null) SetMessage(e.Message, Bad);
            }
        }

        void Refresh()
        {
            if (_entry == null) return;
            var address = S.Address;
            var signedIn = S.SignedIn;
            var connected = S.WalletConnected;
            SetLabel(_entry, signedIn ? "Wallet  " + Web3Rules.ShortAddress(address) : "Connect Wallet");
            if (_panel == null || !_panel.gameObject.activeSelf) return;

            _address.text = string.IsNullOrEmpty(address)
                ? "No wallet connected"
                : Web3Rules.ShortAddress(address) + (connected ? (S.Kind == Web3Session.WalletKind.TestWallet ? "   <size=24><color=#9aa4ad>test wallet</color></size>" : string.Empty)
                    : "   <size=24><color=#9aa4ad>session kept, reconnect to sign</color></size>");
            _balance.text = string.IsNullOrEmpty(address) ? string.Empty : "Balance  " + Web3Rules.FormatSol(S.Balance) + "  (devnet)";
            var p = S.Profile;
            _profile.text = p != null
                ? $"{p.name}<color=#9aa4ad>{p.tag}</color>   rating {p.rating}   " + (OnlineService.IsOnline ? "online" : "offline")
                : (string.IsNullOrEmpty(address) ? "Connect a Solana wallet to play online (leaderboard, 1v1, wagers)." : "Not signed in.");

            var busy = S.Busy;
            _connect.gameObject.SetActive(!connected);
            SetLabel(_connect, string.IsNullOrEmpty(address) ? "Connect Wallet" : "Reconnect");
            _test.gameObject.SetActive(!connected && Web3Session.TestWalletAvailable);
            _signIn.gameObject.SetActive(connected && !signedIn);
            _refresh.gameObject.SetActive(!string.IsNullOrEmpty(address));
            _airdrop.gameObject.SetActive(connected && Web3Session.TestWalletAvailable);
            _disconnect.gameObject.SetActive(!string.IsNullOrEmpty(address));
            foreach (var b in new[] { _connect, _test, _signIn, _refresh, _airdrop, _disconnect, _saveEmail }) b.interactable = !busy;
            _email.interactable = signedIn && !busy;
            _saveEmail.interactable = signedIn && !busy;

            if (busy && !string.IsNullOrEmpty(S.Status)) SetMessage(S.Status, Gold);
            else if (!string.IsNullOrEmpty(S.LastError)) SetMessage(S.LastError, Bad);
            else if (_message.color == Gold) SetMessage(signedIn ? "Signed in. Online features use this wallet." : string.Empty, Good);
        }

        void SetMessage(string text, Color color)
        {
            if (_message == null) return;
            _message.text = text ?? string.Empty;
            _message.color = color;
        }

        // ---------------------------------------------------------------- UI bits (the menu hub's look)

        RectTransform Strip(string name, ref float fromTop, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_panel, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(24f, -fromTop - height);
            rt.offsetMax = new Vector2(-24f, -fromTop);
            fromTop += height + 8f;
            return rt;
        }

        TextMeshProUGUI TextIn(Transform parent, string name, string text, float size, TextAlignmentOptions align, Color color, Vector2 min, Vector2 max, bool title = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<TextMeshProUGUI>();
            if (title && _font != null) t.font = _font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        static Button ButtonIn(Transform parent, string label, Action click, Vector2 min, Vector2 max)
        {
            var b = SettingsPanel.CreateButton(parent, label, click);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var le = b.GetComponent<LayoutElement>();
            if (le != null) le.ignoreLayout = true;
            return b;
        }

        static Button Flex(Button b)
        {
            var le = b.GetComponent<LayoutElement>() ?? b.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.minWidth = 120f;
            le.preferredHeight = le.minHeight = Row;
            return b;
        }

        TMP_InputField Input(Transform parent, string placeholder, int limit)
        {
            var go = new GameObject("EmailInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = TextColor;
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = Row;
            le.flexibleWidth = 1f;
            var area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(go.transform, false);
            var art = (RectTransform)area.transform;
            art.anchorMin = Vector2.zero;
            art.anchorMax = Vector2.one;
            art.offsetMin = new Vector2(16f, 6f);
            art.offsetMax = new Vector2(-16f, -6f);
            var ph = TextIn(area.transform, "Placeholder", placeholder, 30, TextAlignmentOptions.MidlineLeft, new Color(0.3f, 0.3f, 0.3f, 0.6f), Vector2.zero, Vector2.one);
            ph.fontStyle = FontStyles.Italic;
            var text = TextIn(area.transform, "Text", string.Empty, 30, TextAlignmentOptions.MidlineLeft, new Color(0.08f, 0.06f, 0.04f, 1f), Vector2.zero, Vector2.one);
            text.overflowMode = TextOverflowModes.Overflow;
            var input = go.GetComponent<TMP_InputField>();
            input.textViewport = art;
            input.textComponent = text;
            input.placeholder = ph;
            input.characterLimit = limit;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.richText = false;
            input.fontAsset = text.font;
            input.pointSize = 30;
            return input;
        }

        static void SetLabel(Button b, string text)
        {
            var t = b != null ? b.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            if (t != null) t.text = text;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
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
