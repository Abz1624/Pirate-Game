using System;
using System.Collections.Generic;
using RoguePirates.V4.Online;
using RoguePirates.V4.Presentation.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Online
{
    /// <summary>
    /// Playtest (3 Oct, owner): "guest profiles, no sign-in; give an option to edit the name so multiple players can use
    /// the same PC". Edit the active profile's name, switch between the profiles on this device, make a new one, or
    /// forget one here (asks twice). Opens in "new profile" mode on first launch (a default name is filled in).
    /// </summary>
    public sealed class ProfilePanel : MonoBehaviour
    {
        RectTransform _list;
        TextMeshProUGUI _title, _active, _status, _message;
        TMP_InputField _input;
        Button _save, _newButton, _close, _cancel;
        bool _creating, _firstLaunch, _busy;
        string _confirmForget, _inputFor;
        readonly List<GameObject> _rows = new List<GameObject>();

        public bool IsOpen => gameObject.activeSelf;
        public bool Creating => _creating;
        public string Message => _message != null ? _message.text : null;
        public bool Busy => _busy;

        public static ProfilePanel Create(Transform canvas)
        {
            var rt = OnlineUi.Panel(canvas, "PlaytestProfiles", new Vector2(0.2f, 0.06f), new Vector2(0.8f, 0.94f), 225);
            var panel = rt.gameObject.AddComponent<ProfilePanel>();
            panel.Build(rt);
            return panel;
        }

        void Build(RectTransform rt)
        {
            var header = OnlineUi.TopStrip(rt, "Header", 16f, 80f);
            _title = OnlineUi.TextAt(header, "Title", "Captain profile", 44, TextAlignmentOptions.MidlineLeft, OnlineUi.Gold, Vector2.zero, new Vector2(0.72f, 1f), true);
            _close = OnlineUi.Button(header, "Close", Close, new Vector2(0.74f, 0.05f), new Vector2(1f, 0.95f));
            // Hackathon UI (13 Oct, owner): parchment sheet under a wood header (the title and a gold Close in it).
            var head = OnlineUi.Sheet(rt, "Captain profile", 92f, out var kitTitle, 280f);
            if (head != null)
            {
                _title.gameObject.SetActive(false);
                _title = kitTitle;
                MenuKit.ButtonIcon(_close, "Icon_Close", 30f);
                MenuKit.HeaderButton(head, _close, 26f, 220f, 68f);
                header.gameObject.SetActive(false);
            }

            var info = OnlineUi.TopStrip(rt, "Active", 104f, 52f);
            _active = OnlineUi.TextAt(info, "Name", string.Empty, 34, TextAlignmentOptions.MidlineLeft, OnlineUi.TextColor, Vector2.zero, new Vector2(0.65f, 1f));
            _status = OnlineUi.TextAt(info, "Status", string.Empty, 26, TextAlignmentOptions.MidlineRight, OnlineUi.Dim, new Vector2(0.65f, 0f), Vector2.one);

            var edit = OnlineUi.TopStrip(rt, "Edit", 168f, OnlineUi.RowHeight);
            var h = edit.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 12f;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            _input = OnlineUi.Input(edit, "Captain name (3-16 letters)", OnlineRules.NameMax);
            _input.onSubmit.AddListener(_ => Submit());
            _save = OnlineUi.Button(edit, "Save name", Submit);
            _save.GetComponent<LayoutElement>().minWidth = 220f;
            _cancel = OnlineUi.Button(edit, "Cancel", CancelCreate);
            _cancel.GetComponent<LayoutElement>().minWidth = 160f;

            var listLabel = OnlineUi.TopStrip(rt, "ListLabel", 256f, 40f);
            OnlineUi.TextAt(listLabel, "Label", "Profiles on this device", 26, TextAlignmentOptions.MidlineLeft, OnlineUi.Dim, Vector2.zero, Vector2.one);
            var body = OnlineUi.Area(rt, "Body", Vector2.zero, Vector2.one, new Vector4(24f, 168f, 24f, 300f));
            _list = OnlineUi.ScrollList(body, "Scroll", Vector4.zero);

            var footer = OnlineUi.Area(rt, "Footer", Vector2.zero, new Vector2(1f, 0f), new Vector4(24f, 16f, 24f, 0f));
            footer.offsetMax = new Vector2(-24f, 16f + OnlineUi.RowHeight);
            _newButton = OnlineUi.Button(footer, "New profile", BeginCreate, new Vector2(0f, 0f), new Vector2(0.4f, 1f));
            _message = OnlineUi.TextAt(rt, "Message", string.Empty, 26, TextAlignmentOptions.Center, OnlineUi.Gold, new Vector2(0f, 0f), new Vector2(1f, 0f));
            _message.rectTransform.offsetMin = new Vector2(24f, 96f);
            _message.rectTransform.offsetMax = new Vector2(-24f, 156f);
            _message.textWrappingMode = TextWrappingModes.Normal;
            var msgOffset = _message.rectTransform;
            msgOffset.anchorMin = new Vector2(0f, 0f);
            msgOffset.anchorMax = new Vector2(1f, 0f);
        }

        void OnEnable() => OnlineService.Changed += Refresh;
        void OnDisable() => OnlineService.Changed -= Refresh;

        /// <summary>Opens on the active profile (or in "new profile" mode when there is none: the first launch).</summary>
        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _message.text = string.Empty;
            _confirmForget = null;
            _firstLaunch = OnlineService.Profiles.Profiles.Count == 0;
            if (_firstLaunch) BeginCreate();
            else { _creating = false; Refresh(); }
        }

        public void Close()
        {
            // First launch: closing keeps a default name, so a profile always exists.
            if (OnlineService.Profiles.Profiles.Count == 0 && !_busy) { Create(OnlineRules.DefaultName(Environment.TickCount)); }
            gameObject.SetActive(false);
        }

        void BeginCreate()
        {
            _creating = true;
            _input.text = OnlineRules.DefaultName(UnityEngine.Random.Range(0, int.MaxValue));
            _message.text = _firstLaunch ? "Welcome aboard! Pick a captain name (you can change it later)." : string.Empty;
            Refresh();
            _input.ActivateInputField();
        }

        void CancelCreate()
        {
            if (OnlineService.Profiles.Profiles.Count == 0) return;
            _creating = false;
            _message.text = string.Empty;
            Refresh();
        }

        /// <summary>Save name (rename the active profile) or Create (a new one), from the field.</summary>
        public void Submit()
        {
            if (_busy) return;
            if (_creating) Create(_input.text);
            else Rename(_input.text);
        }

        async void Create(string name)
        {
            _busy = true;
            _message.text = "Creating...";
            string error;
            try { error = await OnlineService.CreateProfile(name); }
            catch (Exception e) { error = e.Message; }
            _busy = false;
            if (this == null) return;
            if (error != null) { _message.text = error; return; }
            _creating = false;
            _firstLaunch = false;
            _message.text = OnlineService.IsOnline ? "Profile ready." : "Saved on this device. It goes online next time you are connected.";
            Refresh();
        }

        async void Rename(string name)
        {
            _busy = true;
            _message.text = "Saving...";
            string error;
            try { error = await OnlineService.RenameActive(name); }
            catch (Exception e) { error = e.Message; }
            _busy = false;
            if (this == null) return;
            _message.text = error ?? (OnlineService.IsOnline ? "Name saved." : "Name saved on this device. It goes online next time you are connected.");
            Refresh();
        }

        async void Use(string localId)
        {
            _confirmForget = null;
            string error;
            try { error = await OnlineService.SwitchTo(localId); }
            catch (Exception e) { error = e.Message; }
            if (this == null) return;
            _message.text = error ?? "Switched to " + (OnlineService.Active != null ? OnlineService.Active.Display : "?");
            Refresh();
        }

        void Forget(string localId)
        {
            if (_confirmForget != localId)
            {
                _confirmForget = localId;
                _message.text = "Press Sure? to forget this profile on this device. Its scores stay on the board.";
                Refresh();
                return;
            }
            _confirmForget = null;
            OnlineService.Forget(localId);
            _message.text = "Profile forgotten on this device.";
            if (OnlineService.Profiles.Profiles.Count == 0) { _firstLaunch = true; BeginCreate(); }
            else
            {
                if (OnlineService.IsOnline) _ = OnlineService.SyncActive();
                Refresh();
            }
        }

        void Refresh()
        {
            if (!gameObject.activeSelf) return;
            var active = OnlineService.Active;
            _title.text = _creating ? (_firstLaunch ? "Welcome, captain" : "New profile") : "Captain profile";
            _active.text = MenuKit.InkRich(_creating ? "Choose a name" : active != null ? active.Display + (active.Registered ? $"  <size=70%><color=#9aa4ad>rating {active.rating}</color></size>" : string.Empty) : "No profile");
            _status.text = StatusLabel(active);
            _status.color = MenuKit.Ready ? (OnlineService.IsOnline ? MenuKit.InkGood : MenuKit.InkDim) : OnlineService.IsOnline ? OnlineUi.Good : OnlineUi.Dim;
            OnlineUi.SetLabel(_save, _creating ? "Create" : "Save name");
            _cancel.gameObject.SetActive(_creating && OnlineService.Profiles.Profiles.Count > 0);
            _newButton.gameObject.SetActive(!_creating);
            // The field follows the active profile (a switch or a new profile replaces it, even while focused); typing is kept.
            if (!_creating && active != null && (_inputFor != active.localId || !_input.isFocused))
            {
                _input.text = active.name;
                _inputFor = active.localId;
            }

            foreach (var r in _rows) { if (r != null) { r.SetActive(false); Destroy(r); } }
            _rows.Clear();
            foreach (var p in OnlineService.Profiles.Profiles)
            {
                var isActive = active != null && p.localId == active.localId;
                var row = OnlineUi.Row(_list, OnlineUi.RowHeight, isActive ? new Color(1f, 0.82f, 0.4f, 0.18f) : new Color(1f, 1f, 1f, 0.05f));
                OnlineUi.Cell(row, p.Display + (p.Registered ? string.Empty : "  <size=70%><color=#9aa4ad>(not online yet)</color></size>"), 0f, 30, TextAlignmentOptions.MidlineLeft, OnlineUi.TextColor);
                var id = p.localId;
                var use = OnlineUi.Button(row, isActive ? "Playing" : "Use", () => Use(id));
                use.interactable = !isActive;
                use.GetComponent<LayoutElement>().minWidth = 170f;
                var forget = OnlineUi.Button(row, _confirmForget == id ? "Sure?" : "Forget", () => Forget(id));
                forget.GetComponent<LayoutElement>().minWidth = 150f;
                if (_confirmForget == id)
                {
                    if (MenuKit.Ready) MenuKit.StyleButton(forget, "Btn_Red");
                    else forget.GetComponent<Image>().color = new Color(0.9f, 0.4f, 0.3f, 1f);
                }
                else MenuKit.StyleButton(forget, "Btn_Parchment");
                _rows.Add(row.gameObject);
            }
        }

        static string StatusLabel(LocalProfile p)
        {
            switch (OnlineService.State)
            {
                case OnlineState.Online: return p != null && !p.Registered ? "Online (registering)" : "Online";
                case OnlineState.Checking: return "Connecting...";
                case OnlineState.NotConfigured: return "Offline (no server set)";
                default: return "Offline";
            }
        }
    }
}
