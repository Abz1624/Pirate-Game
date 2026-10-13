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
    /// the wallet panel (full address with Copy address and View on explorer, devnet SOL balance, profile, optional email, disconnect). Attached to V4's online
    /// menu when the menu scene loads, so V4 code is unchanged. Reads <see cref="Web3Session"/> and its Changed event.
    /// 13 Oct: entry, panel and captain name prompt redrawn in the art team's parchment kit (<see cref="MarketKit"/>).
    /// </summary>
    public sealed class WalletMenu : MonoBehaviour
    {
        static Color TextColor => MarketKit.Ink;
        static Color Dim => MarketKit.InkDim;
        static Color Gold => MarketKit.InkGold;
        static Color Good => MarketKit.InkGood;
        static Color Bad => MarketKit.InkBad;
        static string DimHex => MarketKit.Hex(MarketKit.InkDim);
        const float Row = 80f;
        const float HeaderHeight = 130f;

        OnlineMenuPresenter _online;
        Button _entry;
        RectTransform _entryHost;
        Transform _canvas;
        float _nextCover;
        RectTransform _panel;
        TextMeshProUGUI _address, _balance, _profile, _message;
        TMP_InputField _email, _name, _promptName;
        Button _connect, _test, _refresh, _airdrop, _signIn, _disconnect, _saveEmail, _copy, _explorer, _saveName, _promptSave, _promptSkip;
        RectTransform _prompt;
        TextMeshProUGUI _promptMessage;
        RectTransform _addressActions;

        public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;
        public string EntryLabel => _entry != null ? _entry.GetComponentInChildren<TextMeshProUGUI>(true).text : null;
        public string MessageText => _message != null ? _message.text : null;
        public bool NamePromptOpen => _prompt != null && _prompt.gameObject.activeSelf;
        public string NamePromptMessage => _promptMessage != null ? _promptMessage.text : null;
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
            // The entry: a gold kit button, top centre of the menu, above the art menu's layers.
            var main = FindDeep(canvas, "MainMenuPanel");
            var host = new GameObject("HackathonWalletEntry", typeof(RectTransform));
            host.transform.SetParent(main != null ? main : canvas, false);
            var hrt = (RectTransform)host.transform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 1f); // top centre (clear sky; the logo and RUM sit top right)
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.anchoredPosition = new Vector2(0f, -24f);
            hrt.sizeDelta = new Vector2(460f, 86f);
            SettingsPanel.DrawOnTop(host, 210);
            _entryHost = hrt;
            _canvas = canvas;
            _entry = MarketKit.KitButton(MarketKit.Fill(hrt, "Button"), "Connect Wallet", "Icon_User", Open, "Btn_Gold", 32f, 40f);

            BuildPanel(canvas);
            S.Changed += Refresh;
            OnlineService.Changed += Refresh;
            Refresh();
        }

        void BuildPanel(Transform canvas)
        {
            var go = new GameObject("HackathonWalletPanel", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            _panel = (RectTransform)go.transform;
            _panel.anchorMin = new Vector2(0.12f, 0.04f);
            _panel.anchorMax = new Vector2(0.88f, 0.96f);
            _panel.offsetMin = _panel.offsetMax = Vector2.zero;
            SettingsPanel.DrawOnTop(go, 240);
            MarketKit.Blocker(_panel);

            var card = MarketKit.Fill(_panel, "Card", 8f, 0f, 8f, HeaderHeight - 40f);
            MarketKit.Paint(card, "Panel_Card", 1.2f);
            var head = MarketKit.Strip(_panel, "Header", 0f, 0f, 0f, HeaderHeight);
            MarketKit.Header(head, "Wallet and profile", "Icon_User", 56f, 150f, 560f);
            MarketKit.KitButton(MarketKit.TopRight(head, "Close", 24f, 22f, 230f, 86f), "Close", "Icon_Close", Close, "Btn_Gold", 34f, 38f);
            MarketKit.Chip(MarketKit.TopRight(head, "Network", 276f, 30f, 260f, 70f), "Solana devnet", 28f, MarketKit.Ink, true);

            // The full address (wraps on narrow screens), then Copy address and View on explorer.
            var addr = MarketKit.Strip(card, "Address", 44f, 56f, 44f, 112f);
            MarketKit.Paint(addr, "Panel_Inset", 1.6f);
            _address = MarketKit.Label(MarketKit.Fill(addr, "Address", 26f, 8f, 26f, 8f), string.Empty, 30f, TextColor, TextAlignmentOptions.MidlineLeft, false, 18f);
            _address.textWrappingMode = TextWrappingModes.Normal;
            _addressActions = MarketKit.Strip(card, "AddressActions", 44f, 180f, 44f, 72f);
            MarketKit.Row(_addressActions, 16f);
            _copy = MarketKit.RowButton(_addressActions, "Copy address", "Icon_Copy", CopyAddress, 340f, 72f, "Btn_Parchment", 28f);
            _explorer = MarketKit.RowButton(_addressActions, "View on explorer", "Icon_Link", OpenExplorer, 380f, 72f, "Btn_Parchment", 28f);

            var bal = MarketKit.Strip(card, "Balance", 50f, 262f, 50f, 50f);
            MarketKit.Icon(MarketKit.TopLeft(bal, "Chest", 0f, 0f, 50f, 50f), "Chest_2");
            _balance = MarketKit.Label(MarketKit.Fill(bal, "Balance", 66f, 0f, 0f, 0f), string.Empty, 32f, Gold, TextAlignmentOptions.MidlineLeft, true, 20f);
            var prof = MarketKit.Strip(card, "Profile", 50f, 314f, 50f, 46f);
            MarketKit.Icon(MarketKit.TopLeft(prof, "User", 6f, 4f, 38f, 38f), "Icon_User", Dim);
            _profile = MarketKit.Label(MarketKit.Fill(prof, "Profile", 66f, 0f, 0f, 0f), string.Empty, 28f, Dim, TextAlignmentOptions.MidlineLeft, false, 18f);

            var actions = MarketKit.Strip(card, "Actions", 44f, 370f, 44f, Row);
            var h = MarketKit.Row(actions, 14f);
            h.childForceExpandWidth = true;
            _connect = Flex(MarketKit.RowButton(actions, "Connect Wallet", null, () => Run(S.Connect(false)), 0f, Row, "Btn_Gold", 30f));
            _test = Flex(MarketKit.RowButton(actions, "Test wallet (dev)", null, () => Run(S.Connect(true)), 0f, Row, "Btn_Parchment", 28f));
            _signIn = Flex(MarketKit.RowButton(actions, "Sign in again", null, () => Run(S.SignIn()), 0f, Row, "Btn_Gold", 30f));
            _refresh = Flex(MarketKit.RowButton(actions, "Refresh", "Icon_Refresh", () => Run(RefreshOnly()), 0f, Row, "Btn_Parchment", 28f));
            _airdrop = Flex(MarketKit.RowButton(actions, "Airdrop 1 SOL", null, () => Run(S.RequestAirdrop(1)), 0f, Row, "Btn_Green", 28f));
            _disconnect = Flex(MarketKit.RowButton(actions, "Disconnect", null, () => { S.Disconnect(); SetMessage("Disconnected.", Dim); }, 0f, Row, "Btn_Red", 28f));

            MarketKit.Line(card, 44f, 466f, 44f);

            // Captain name (the name other players see; the server's name rules).
            Caption(card, 480f, "Captain name", "3 to 16 letters, digits, spaces, _ or -");
            var nameRow = MarketKit.Strip(card, "Name", 44f, 524f, 44f, Row);
            MarketKit.Row(nameRow, 14f);
            _name = MarketKit.Input(MarketKit.Cell(nameRow, "NameInput", 0f, Row), "Your captain name", 16);
            _name.onSubmit.AddListener(_ => SaveName());
            _saveName = MarketKit.RowButton(nameRow, "Save name", "Icon_Save", SaveName, 280f, Row, "Btn_Gold", 30f);

            Caption(card, 616f, "Email", "optional, for match news");
            var email = MarketKit.Strip(card, "Email", 44f, 660f, 44f, Row);
            MarketKit.Row(email, 14f);
            _email = MarketKit.Input(MarketKit.Cell(email, "EmailInput", 0f, Row), "you@example.com", 254);
            _email.contentType = TMP_InputField.ContentType.EmailAddress;
            _email.onSubmit.AddListener(_ => SaveEmail());
            _saveEmail = MarketKit.RowButton(email, "Save email", "Icon_Save", SaveEmail, 280f, Row, "Btn_Gold", 30f);

            var msg = MarketKit.Fill(card, "Message", 44f, 30f, 44f, 756f);
            _message = MarketKit.Label(MarketKit.Fill(msg, "Message", 6f, 0f, 6f, 0f), string.Empty, 27f, Dim, TextAlignmentOptions.TopLeft, false, 18f);
            _message.textWrappingMode = TextWrappingModes.Normal;

            go.SetActive(false);
            BuildNamePrompt(canvas);
        }

        static void Caption(RectTransform card, float y, string title, string hint)
        {
            var t = MarketKit.Label(MarketKit.Strip(card, title, 50f, y, 50f, 40f),
                "<b>" + title + "</b>   <size=80%><color=" + MarketKit.Hex(MarketKit.InkDim) + ">(" + hint + ")</color></size>", 30f, MarketKit.Ink, TextAlignmentOptions.MidlineLeft, false, 18f);
            t.richText = true;
        }

        /// <summary>After a first wallet sign-in: "Name your captain" (skippable). Drawn above the wallet panel.</summary>
        void BuildNamePrompt(Transform canvas)
        {
            // The root is the full-screen blocker (dims and blocks the menu); the panel is its child, so it draws on top of
            // the blocker and keeps its own look.
            var go = new GameObject("HackathonNamePrompt", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvas, false);
            _prompt = (RectTransform)go.transform;
            _prompt.anchorMin = Vector2.zero;
            _prompt.anchorMax = Vector2.one;
            _prompt.offsetMin = _prompt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            SettingsPanel.DrawOnTop(go, 250);
            var panel = MarketKit.Node(_prompt, "Panel");
            panel.anchorMin = new Vector2(0.2f, 0.24f);
            panel.anchorMax = new Vector2(0.8f, 0.76f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;

            const float head = 116f;
            var card = MarketKit.Fill(panel, "Card", 8f, 0f, 8f, head - 36f);
            MarketKit.Paint(card, "Panel_Card", 1.2f);
            MarketKit.Header(MarketKit.Strip(panel, "Header", 0f, 0f, 0f, head), "Name your captain", "Icon_User", 52f, 136f, 30f);

            var intro = MarketKit.Label(MarketKit.Strip(card, "Intro", 48f, 52f, 48f, 84f),
                "Other captains see this name on the leaderboards, in 1v1 lobbies and on your island. You can change it later in the wallet panel.",
                28f, TextColor, TextAlignmentOptions.TopLeft, false, 18f);
            intro.textWrappingMode = TextWrappingModes.Normal;
            _promptName = MarketKit.Input(MarketKit.Strip(card, "Name", 44f, 146f, 44f, Row), "3 to 16 letters, digits, spaces, _ or -", 16);
            _promptName.onSubmit.AddListener(_ => SavePromptName());
            var buttons = MarketKit.Strip(card, "Buttons", 44f, 242f, 44f, Row);
            var h = MarketKit.Row(buttons, 16f);
            h.childForceExpandWidth = true;
            _promptSave = Flex(MarketKit.RowButton(buttons, "Save name", "Icon_Save", SavePromptName, 0f, Row, "Btn_Gold", 30f));
            _promptSkip = Flex(MarketKit.RowButton(buttons, "Skip for now", null, SkipNamePrompt, 0f, Row, "Btn_Parchment", 30f));
            _promptMessage = MarketKit.Label(MarketKit.Fill(card, "Message", 50f, 20f, 50f, 334f), string.Empty, 26f, Dim, TextAlignmentOptions.TopLeft, false, 16f);
            _promptMessage.textWrappingMode = TextWrappingModes.Normal;
            go.SetActive(false);
        }


        public void OpenNamePrompt()
        {
            if (_prompt == null) return;
            var current = S.Profile?.name;
            var isDefault = Web3Rules.IsDefaultName(current, S.Address);
            _promptName.text = isDefault ? string.Empty : current ?? string.Empty;
            _promptMessage.text = isDefault ? "For now you are " + current + "." : string.Empty;
            _promptMessage.color = Dim;
            _prompt.gameObject.SetActive(true);
            _prompt.SetAsLastSibling();
            _promptName.ActivateInputField();
        }

        public void SkipNamePrompt()
        {
            S.DismissNamePrompt();
            if (_prompt != null) _prompt.gameObject.SetActive(false);
            SetMessage("You can set your captain name here any time.", Dim);
        }

        public async void SavePromptName()
        {
            if (_prompt == null || S.Busy) return;
            _promptSave.interactable = _promptSkip.interactable = false;
            _promptMessage.text = "Saving...";
            _promptMessage.color = Gold;
            try
            {
                var error = await S.SetName(_promptName.text);
                if (this == null) return;
                if (error != null) { _promptMessage.text = error; _promptMessage.color = Bad; return; }
                _prompt.gameObject.SetActive(false);
                if (_name != null) _name.text = S.Profile?.name ?? string.Empty;
                SetMessage("Ahoy, " + S.Profile?.name + "! Your captain name is saved.", Good);
                Refresh();
            }
            catch (Exception e) { if (this != null) { _promptMessage.text = e.Message; _promptMessage.color = Bad; } }
            finally { if (this != null) _promptSave.interactable = _promptSkip.interactable = true; }
        }

        void SaveName() => Run(S.SetName(_name.text), "Captain name saved.");

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

        /// <summary>The entry sits over the menu's top edge: while another panel is open over the menu (settings, profile,
        /// leaderboard, 1v1, the hackathon screens, this wallet panel) it steps aside, so it never covers their headers.</summary>
        void LateUpdate()
        {
            if (_entryHost == null || _canvas == null || Time.unscaledTime < _nextCover) return;
            _nextCover = Time.unscaledTime + 0.15f;
            var covered = false;
            foreach (var c in _canvas.GetComponentsInChildren<Canvas>(false))
            {
                if (!c.overrideSorting || c.sortingOrder < 210 || c.transform == _entryHost) continue;
                if (c.transform.IsChildOf(_entryHost)) continue;
                covered = true;
                break;
            }
            if (_entryHost.gameObject.activeSelf == covered) _entryHost.gameObject.SetActive(!covered);
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
            _name.text = S.Profile?.name ?? string.Empty;
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

        void CopyAddress()
        {
            var address = S.Address;
            if (string.IsNullOrEmpty(address)) return;
            Web3Clipboard.Copy(address);
            SetMessage("Address copied: " + address, Good);
        }

        void OpenExplorer()
        {
            var url = Web3Rules.ExplorerAddress(S.Address);
            if (url != null) Application.OpenURL(url);
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
            // A first wallet sign-in asks for a captain name once (skippable), even with the wallet panel closed.
            if (S.SignedIn && !S.Busy && S.NamePromptDue && !NamePromptOpen) OpenNamePrompt();
            var address = S.Address;
            var signedIn = S.SignedIn;
            var connected = S.WalletConnected;
            SetLabel(_entry, signedIn ? "Wallet  " + Web3Rules.ShortAddress(address) : "Connect Wallet");
            if (_panel == null || !_panel.gameObject.activeSelf) return;

            _address.text = string.IsNullOrEmpty(address)
                ? "No wallet connected"
                : "<size=24><color=" + DimHex + ">Wallet address</color></size>" + (connected ? (S.Kind == Web3Session.WalletKind.TestWallet ? "   <size=24><color=" + DimHex + ">test wallet</color></size>" : string.Empty)
                    : "   <size=24><color=" + DimHex + ">session kept, reconnect to sign</color></size>") + "\n" + address;
            _addressActions.gameObject.SetActive(!string.IsNullOrEmpty(address));
            _balance.text = string.IsNullOrEmpty(address) ? string.Empty : "Balance  " + Web3Rules.FormatSol(S.Balance) + "  (devnet)";
            var p = S.Profile;
            _profile.text = p != null
                ? $"{p.name}<color={DimHex}>{p.tag}</color>   rating {p.rating}   " + (OnlineService.IsOnline ? "online" : "offline")
                : (string.IsNullOrEmpty(address) ? "Connect a Solana wallet to play online (leaderboard, 1v1, wagers)." : "Not signed in.");

            var busy = S.Busy;
            _connect.gameObject.SetActive(!connected);
            SetLabel(_connect, string.IsNullOrEmpty(address) ? "Connect Wallet" : "Reconnect");
            _test.gameObject.SetActive(!connected && Web3Session.TestWalletAvailable);
            _signIn.gameObject.SetActive(connected && !signedIn);
            _refresh.gameObject.SetActive(!string.IsNullOrEmpty(address));
            _airdrop.gameObject.SetActive(connected && Web3Session.TestWalletAvailable);
            _disconnect.gameObject.SetActive(!string.IsNullOrEmpty(address));
            foreach (var b in new[] { _connect, _test, _signIn, _refresh, _airdrop, _disconnect, _saveEmail, _saveName }) b.interactable = !busy;
            _email.interactable = signedIn && !busy;
            _saveEmail.interactable = signedIn && !busy;
            _name.interactable = signedIn && !busy;
            _saveName.interactable = signedIn && !busy;
            if (!signedIn && !_name.isFocused) _name.text = string.Empty;

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

        // ---------------------------------------------------------------- UI bits

        static Button Flex(Button b, float height = Row)
        {
            var le = b.GetComponent<LayoutElement>() ?? b.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.minWidth = 120f;
            le.preferredHeight = le.minHeight = height;
            return b;
        }

        static void SetLabel(Button b, string text) => MarketKit.SetLabel(b, text);

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
