using System;
using System.Collections.Generic;
using RoguePirates.V4;
using RoguePirates.V4.Composition;
using RoguePirates.V4.Online;
using RoguePirates.V4.Presentation.Online;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using K = RoguePirates.Hackathon.UI.IslandKit;

namespace RoguePirates.Hackathon.UI
{
    /// <summary>
    /// Hackathon (12 Oct, U-3): the menu's "Islands" screen. Left: the world map (GET /api/islands: 24 slots on a
    /// 1000x1000 sea, markers for free, mine, raidable, shielded and listed islands). Right: "My island" (GET
    /// /api/islands/mine: claim with a name, the Island NFT, RUM with a filling bar and Collect, buildings with Upgrade,
    /// defence and shield), a tapped island's raid panel (Captain Marrow defends; loot and the RUM fee, no SOL stake; Raid starts
    /// the raid voyage, <see cref="RaidHud"/>) and the raid history (GET /api/raids/mine).
    /// 13 Oct: redrawn in the art team's parchment kit after the owner's references (wood header with the skull
    /// medallion, green status banner, the sea chart on a parchment card with a wooden legend, and a parchment card with
    /// My island / Raid history tabs: island card, RUM plank, building rows, defence and history rows with result tags).
    /// </summary>
    public sealed class IslandsPanel : MonoBehaviour
    {
        enum View { Mine, Island, History }

        // Layout (canvas units; the canvas is at least 1920 x 1080).
        const float HeaderHeight = 124f;
        const float StatusTop = 132f;
        const float StatusHeight = 58f;
        const float BodyTop = 202f;

        RectTransform _map, _markers, _list, _rumPill;
        ScrollRect _scroll;
        TextMeshProUGUI _rum, _status, _shield;
        Image _statusBg, _statusIcon;
        Color _statusColor;
        Button _close, _tabMine, _tabHistory;
        IslandWorld _world;
        MyIslands _mine;
        View _view = View.Mine;
        string _selectedSlotIsland;
        int _selectedSlot = -1;
        bool _busy;
        int _requests;
        RaidStart _pendingRaid;
        TMP_InputField _nameInput;
        DateTime? _shieldUntil;

        public bool IsOpen => gameObject.activeSelf;
        public IslandWorld World => _world;
        public MyIslands Mine => _mine;
        public string StatusText => _status != null ? _status.text : null;
        public IslandView MyIsland => _mine?.islands != null && _mine.islands.Length > 0 ? _mine.islands[Mathf.Clamp(_mineIndex, 0, _mine.islands.Length - 1)] : null;
        int _mineIndex;
        public int MarkerCount { get; private set; }
        public bool Busy => _busy;
        public RaidStart PendingRaid => _pendingRaid;
        /// <summary>The market screen (the "Market" button here opens it).</summary>
        public MarketPanel Market { get; set; }

        public static IslandsPanel Create(Transform canvas)
        {
            var rt = HackUi.Panel(canvas, "HackathonIslands", new Vector2(0.015f, 0.02f), new Vector2(0.985f, 0.98f), 227);
            rt.GetComponent<Image>().color = new Color(0.02f, 0.06f, 0.1f, 0.55f);
            var panel = rt.gameObject.AddComponent<IslandsPanel>();
            panel.Build(rt);
            return panel;
        }

        // ---------------------------------------------------------------- build

        void Build(RectTransform rt)
        {
            BuildHeader(rt);
            BuildStatus(rt);
            var body = K.Fill(rt, "Body", 0f, 0f, 0f, BodyTop);
            var left = K.Columns(body, "MapCard", 0f, 0.5f, 0f, 8f);
            K.Paint(left, "Panel_Card", 1.2f);
            var right = K.Columns(body, "DetailCard", 0.5f, 1f, 8f, 0f);
            K.Paint(right, "Panel_Card", 1.2f);
            BuildMap(left);
            BuildDetail(right);
        }

        void BuildHeader(RectTransform rt)
        {
            var head = K.Strip(rt, "Header", 0f, 0f, 0f, HeaderHeight);
            K.Paint(head, "Header_Wood", 1f, null, new Color(0.32f, 0.2f, 0.1f, 1f));

            // The skull medallion over the plank's left end, as in the reference.
            var medal = K.TopLeft(head, "Medallion", -14f, -14f, 152f, 152f);
            var mimg = K.Paint(medal, "Medallion_Frame", 1f, null, new Color(0f, 0f, 0f, 0f));
            mimg.preserveAspect = true;
            if (mimg.sprite != null)
            {
                var disc = K.Fill(medal, "Disc", 34f, 34f, 34f, 34f).gameObject.AddComponent<Image>();
                disc.sprite = IslandUi.Circle;
                disc.color = new Color(0.96f, 0.88f, 0.7f, 1f);
                disc.raycastTarget = false;
            }
            K.Paint(K.Fill(medal, "Skull", 44f, 46f, 44f, 42f), "Icon_Skull_Crossbones", 1f, K.Ink, new Color(0f, 0f, 0f, 0f)).preserveAspect = true;

            K.Label(K.TopLeft(head, "Title", 150f, 8f, 290f, 104f), "Islands", 72f, K.OnWood, TextAlignmentOptions.MidlineLeft, true, 40f);

            _rumPill = K.TopLeft(head, "Rum", 450f, 22f, 300f, 80f);
            var pill = _rumPill.gameObject.AddComponent<Image>();
            pill.color = new Color(0f, 0f, 0f, 0.22f);
            pill.raycastTarget = false;
            K.Icon(K.TopLeft(_rumPill, "Barrel", 6f, 4f, 72f, 72f), "Barrel");
            _rum = K.Label(K.Fill(_rumPill, "Value", 88f, 0f, 10f, 0f), string.Empty, 40f, K.OnWood, TextAlignmentOptions.MidlineLeft, true, 22f);

            _close = K.KitButton(K.TopRight(head, "Close", 24f, 20f, 230f, 84f), "Close", null, Close, "Btn_Gold", 36f);
            K.KitButton(K.TopRight(head, "Refresh", 268f, 20f, 230f, 84f), "Refresh", null, Refresh, "Btn_Gold", 36f);
            K.KitButton(K.TopRight(head, "Market", 512f, 20f, 230f, 84f), "Market", null, () => { if (Market != null) Market.Open(); }, "Btn_Gold", 36f);
        }

        void BuildStatus(RectTransform rt)
        {
            var strip = K.Strip(rt, "Status", 8f, StatusTop, 8f, StatusHeight);
            _statusBg = K.Paint(strip, "Banner_Green", 1f, null, new Color(0.2f, 0.45f, 0.2f, 1f));
            _statusIcon = K.Paint(K.TopLeft(strip, "Icon", 26f, 7f, 44f, 44f), "Icon_Check_Green", 1f, null, new Color(0f, 0f, 0f, 0f));
            _statusIcon.preserveAspect = true;
            _status = K.Label(K.Fill(strip, "Text", 84f, 2f, 28f, 2f), string.Empty, 28f, K.OnWood, TextAlignmentOptions.MidlineLeft, true, 16f);
            _status.textWrappingMode = TextWrappingModes.Normal;
        }

        void BuildMap(RectTransform left)
        {
            _map = K.Fill(left, "Map", 34f, 108f, 34f, 34f);
            _map.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.35f, 0.56f, 1f);
            _map.gameObject.AddComponent<RectMask2D>();
            DrawSea(_map);
            _markers = K.Fill(_map, "Markers");
            Legend(left);
        }

        void BuildDetail(RectTransform right)
        {
            var tabs = K.Strip(right, "Tabs", 40f, 34f, 40f, 84f);
            _tabMine = K.KitButton(K.Columns(tabs, "My island", 0f, 0.5f, 0f, 9f), "My island", null, () => Show(View.Mine), "Btn_Gold", 36f);
            _tabHistory = K.KitButton(K.Columns(tabs, "Raid history", 0.5f, 1f, 9f, 0f), "Raid history", null, () => Show(View.History), "Btn_Parchment", 36f);
            var host = K.Fill(right, "List", 36f, 34f, 30f, 134f);
            _list = K.Scroll(host, out _scroll, 12f, new RectOffset(2, 10, 2, 12));
        }

        // ---------------------------------------------------------------- map art

        static void DrawSea(RectTransform map)
        {
            // Grid lines (a chart look).
            for (var i = 1; i < 10; i++)
            {
                Line(map, new Vector2(i / 10f, 0f), new Vector2(i / 10f, 1f), 2f, true);
                Line(map, new Vector2(0f, i / 10f), new Vector2(1f, i / 10f), 2f, false);
            }
            // Region names (the server's regions sit roughly here).
            Region(map, "Saltwind Reach", 0.25f, 0.95f);
            Region(map, "Coral Shallows", 0.72f, 0.95f);
            Region(map, "Black Tide", 0.2f, 0.62f);
            Region(map, "Maelstrom Deep", 0.68f, 0.62f);
            Region(map, "Ember Isles", 0.3f, 0.04f);
            Region(map, "Golden Wake", 0.78f, 0.04f);
            var compass = K.Label(K.Node(map, "Compass"), "N\n<size=60%>^</size>", 34f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Center, true);
            compass.textWrappingMode = TextWrappingModes.Normal;
            var crt = compass.rectTransform;
            crt.anchorMin = new Vector2(0.005f, 0.72f);
            crt.anchorMax = new Vector2(0.07f, 0.84f);
            crt.offsetMin = crt.offsetMax = Vector2.zero;
        }

        static void Line(RectTransform map, Vector2 a, Vector2 b, float width, bool vertical)
        {
            var go = new GameObject("Grid", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(map, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = a;
            rt.anchorMax = b;
            rt.sizeDelta = vertical ? new Vector2(width, 0f) : new Vector2(0f, width);
            var img = go.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.07f);
            img.raycastTarget = false;
        }

        static void Region(RectTransform map, string name, float x, float y)
        {
            var t = K.Label(K.Node(map, name), name.ToUpperInvariant(), 24f, new Color(0.85f, 0.93f, 1f, 0.45f), TextAlignmentOptions.Center, true);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(x - 0.2f, y - 0.03f);
            rt.anchorMax = new Vector2(x + 0.2f, y + 0.03f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            t.characterSpacing = 4f;
        }

        static readonly (IslandRules.Marker kind, string label, string dot, Color color)[] LegendItems =
        {
            (IslandRules.Marker.Mine, "Yours", "Dot_Yellow", new Color(1f, 0.82f, 0.3f)),
            (IslandRules.Marker.Raidable, "Raidable", "Dot_Red", new Color(0.95f, 0.35f, 0.3f)),
            (IslandRules.Marker.Shielded, "Shielded", "Dot_Blue", new Color(0.45f, 0.75f, 1f)),
            (IslandRules.Marker.Listed, "On market", "Dot_Purple", new Color(0.75f, 0.5f, 0.95f)),
            (IslandRules.Marker.Free, "Free", "Dot_Beige", new Color(0.85f, 0.8f, 0.65f, 1f)),
        };

        static (string dot, Color color) MarkerArt(IslandRules.Marker kind)
        {
            foreach (var l in LegendItems) if (l.kind == kind) return (l.dot, l.color);
            return ("Dot_Beige", new Color(0.7f, 0.7f, 0.7f));
        }

        static void Legend(RectTransform left)
        {
            var strip = K.BottomStrip(left, "Legend", 30f, 30f, 30f, 66f);
            K.Paint(strip, "Header_Wood", 1.4f, new Color(0.62f, 0.5f, 0.45f, 1f), new Color(0.25f, 0.15f, 0.08f, 1f));
            var h = strip.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = 10f;
            h.padding = new RectOffset(16, 16, 8, 8);
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            foreach (var (_, label, dot, color) in LegendItems)
            {
                var d = K.Node(strip, "Dot");
                var img = K.Paint(d, dot, 1f, null, color);
                if (img.sprite == null) img.sprite = IslandUi.Circle;
                img.preserveAspect = true;
                var le = d.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = le.minWidth = 34f;
                le.preferredHeight = le.minHeight = 38f;
                var t = K.Label(K.Node(strip, label), label, 28f, K.OnWood, TextAlignmentOptions.MidlineLeft);
                var tle = t.gameObject.AddComponent<LayoutElement>();
                tle.preferredWidth = t.GetPreferredValues(label).x + 18f;
                tle.minWidth = tle.preferredWidth * 0.7f;
            }
        }

        void DrawMarkers()
        {
            for (var i = _markers.childCount - 1; i >= 0; i--) Destroy(_markers.GetChild(i).gameObject);
            MarkerCount = 0;
            if (_world?.slots == null) return;
            var byId = new Dictionary<string, IslandView>();
            if (_world.islands != null)
                foreach (var i in _world.islands)
                    if (i != null && !string.IsNullOrEmpty(i.islandId)) byId[i.islandId] = i;
            foreach (var s in _world.slots)
            {
                if (s == null) continue;
                IslandView island = null;
                if (!string.IsNullOrEmpty(s.islandId)) byId.TryGetValue(s.islandId, out island);
                var kind = island == null ? IslandRules.Marker.Free : IslandRules.MarkerOf(island);
                var (ax, ay) = IslandRules.MapAnchor(s.x, s.y, _world.mapWidth, _world.mapHeight);
                var size = island == null ? 28f : 44f;
                var go = new GameObject("Slot" + s.slot, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(_markers, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
                rt.sizeDelta = new Vector2(size, size * 1.12f);
                var (dot, color) = MarkerArt(kind);
                var img = go.GetComponent<Image>();
                var sprite = K.Find(dot);
                img.sprite = sprite != null ? sprite : IslandUi.Circle;
                img.color = sprite != null ? Color.white : color;
                img.preserveAspect = true;
                if (s.slot == _selectedSlot || kind == IslandRules.Marker.Mine)
                {
                    var ring = K.Node(go.transform, "Selected");
                    ring.SetAsFirstSibling();
                    ring.anchorMin = Vector2.zero;
                    ring.anchorMax = Vector2.one;
                    ring.offsetMin = new Vector2(-12f, -12f);
                    ring.offsetMax = new Vector2(12f, 12f);
                    var rimg = K.Paint(ring, "Pin_Selected_Ring", 1f, s.slot == _selectedSlot ? Color.white : new Color(1f, 0.9f, 0.5f, 1f), new Color(1f, 1f, 1f, 0.45f));
                    if (rimg.sprite == null) rimg.sprite = IslandUi.Circle;
                    rimg.preserveAspect = true;
                }
                var slot = s;
                var picked = island;
                go.GetComponent<Button>().onClick.AddListener(() => Select(slot, picked));
                var label = island != null ? island.name : s.name;
                var mine = kind == IslandRules.Marker.Mine;
                var t = K.Label(K.Node(rt, "Name"), label, island != null ? 24f : 19f,
                    mine ? new Color(1f, 0.85f, 0.35f, 1f) : island != null ? Color.white : new Color(0.92f, 0.96f, 1f, 0.8f), TextAlignmentOptions.Top, island != null);
                var trt = t.rectTransform;
                trt.anchorMin = new Vector2(0.5f, 0f);
                trt.anchorMax = new Vector2(0.5f, 0f);
                trt.pivot = new Vector2(0.5f, 1f);
                trt.sizeDelta = new Vector2(240f, 30f);
                trt.anchoredPosition = new Vector2(0f, -2f);
                t.outlineWidth = 0.18f;
                t.outlineColor = new Color32(0, 25, 50, 220);
                MarkerCount++;
            }
        }

        // ---------------------------------------------------------------- open / refresh

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(_close.gameObject);
            if (RaidHud.LastResult != null && _view != View.Island) _view = View.Mine;
            Refresh();
        }

        public void Close() => gameObject.SetActive(false);

        public async void Refresh()
        {
            var request = ++_requests;
            Status("Loading the islands...", HackUi.Dim);
            try
            {
                var (world, error) = await IslandApi.World();
                if (this == null || request != _requests) return;
                if (error != null) { Status(error, HackUi.Bad); return; }
                _world = world;
                if (Web3Session.Instance.SignedIn)
                {
                    var (mine, mineError) = await IslandApi.Mine();
                    if (this == null || request != _requests) return;
                    if (mineError != null) Status(mineError, HackUi.Bad);
                    else _mine = mine;
                }
                else _mine = null;
                if (_mine != null || !Web3Session.Instance.SignedIn)
                    Status(Web3Session.Instance.SignedIn ? $"{_world.islands?.Length ?? 0} islands claimed, {_world.freeSlots} free. Tap an island on the map." : "Connect a wallet (top of the menu) to claim and raid islands.", HackUi.Dim);
                Render();
                // An island whose NFT is still pending: try the mint again once.
                var me = MyIsland;
                if (me != null && me.nft != null && me.nft.status == "pending" && !_busy) RetryMint();
            }
            catch (Exception e)
            {
                Debug.LogWarning("IslandsPanel: " + e);
                if (this != null) Status("Could not load the islands: " + e.Message, HackUi.Bad);
            }
        }

        void Render()
        {
            _rumPill.gameObject.SetActive(_mine != null);
            _rum.text = _mine != null ? $"{_mine.rum} RUM" : string.Empty;
            DrawMarkers();
            Show(_view);
        }

        void Show(View view)
        {
            var changed = view != _view;
            _view = view;
            K.SetStyle(_tabMine, view == View.Mine ? "Btn_Gold" : "Btn_Parchment");
            K.SetStyle(_tabHistory, view == View.History ? "Btn_Gold" : "Btn_Parchment");
            ClearList();
            switch (view)
            {
                case View.Mine: ShowMine(); break;
                case View.Island: ShowIsland(); break;
                case View.History: ShowHistory(); break;
            }
            if (changed && _scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }

        void Select(IslandSlotView slot, IslandView island)
        {
            _selectedSlot = slot.slot;
            _selectedSlotIsland = island?.islandId;
            if (island != null && island.isMine)
            {
                if (_mine?.islands != null)
                    for (var i = 0; i < _mine.islands.Length; i++)
                        if (_mine.islands[i] != null && _mine.islands[i].islandId == island.islandId) _mineIndex = i;
                _view = View.Mine;
                Render();
                return;
            }
            _view = View.Island;
            Render();
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }

        void ClearList()
        {
            _shield = null;
            _shieldUntil = null;
            K.Clear(_list);
        }

        /// <summary>The green banner under the header (maroon for errors; the tick shows for a success).</summary>
        void Status(string text, Color color)
        {
            if (_status == null) return;
            _statusColor = color;
            _status.text = text;
            var bad = color == HackUi.Bad;
            var busy = color == HackUi.Gold;
            _status.color = busy ? new Color(1f, 0.88f, 0.5f, 1f) : K.OnWood;
            var s = K.Find(bad ? "Banner_Maroon" : "Banner_Green");
            if (s != null) _statusBg.sprite = s;
            else _statusBg.color = bad ? new Color(0.5f, 0.15f, 0.12f, 1f) : new Color(0.2f, 0.45f, 0.2f, 1f);
            _statusIcon.gameObject.SetActive(color == HackUi.Good);
        }

        void Update()
        {
            if (_shield == null || !_shieldUntil.HasValue) return;
            var left = IslandRules.TimeLeft(DateTime.UtcNow, _shieldUntil);
            _shield.text = left == null ? "None (raidable)" : $"<color=#2a5f8f>Shielded for {left}</color>";
        }

        // ---------------------------------------------------------------- my island

        void ShowMine()
        {
            LastRaidBlock();
            if (!Web3Session.Instance.SignedIn)
            {
                K.TextRow(_list, "<b>Connect a wallet</b> (Wallet button, top of the menu) to claim an island. Claiming mints your Island NFT on Solana devnet.", 28f, null, "Panel_Inset");
                return;
            }
            var me = MyIsland;
            if (me == null)
            {
                K.TextRow(_list, $"<size=130%><b>Claim your island</b></size>\nName it, and the crew settles a free spot on the map. " +
                                 "Your island is minted as an NFT to your wallet, brews RUM every hour, and Captain Marrow defends it against raiders.", 27f, null, "Panel_Inset");
                var row = K.ListRow(_list, "Claim", 92f);
                var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.spacing = 16f;
                h.padding = new RectOffset(4, 4, 4, 4);
                h.childControlWidth = h.childControlHeight = true;
                h.childForceExpandWidth = false;
                h.childForceExpandHeight = true;
                _nameInput = HackUi.Input(row, "Island name (3 to 16 letters)", 16);
                K.Paint((RectTransform)_nameInput.transform, "Input_Field", 1.4f, null, new Color(0.97f, 0.92f, 0.8f, 1f)).raycastTarget = true;
                if (_nameInput.textComponent != null) { _nameInput.textComponent.color = K.Ink; if (K.Font != null) _nameInput.textComponent.font = K.Font; }
                var claim = K.Node(row, "ClaimButton");
                var le = claim.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = le.minWidth = 270f;
                K.KitButton(claim, "Claim island", "Icon_Flag", Claim, "Btn_Gold", 32f);
                return;
            }

            // More than one island (bought on the market): pick which one to manage.
            if (_mine.islands.Length > 1)
            {
                var picks = new List<(string, string, Action, string)>();
                for (var i = 0; i < _mine.islands.Length; i++)
                {
                    var index = i;
                    var isl = _mine.islands[i];
                    if (isl == null) continue;
                    picks.Add((isl.name, null, () => { _mineIndex = index; Show(View.Mine); }, index == _mineIndex ? "Btn_Gold" : "Btn_Parchment"));
                }
                K.ButtonRow(_list, 76f, picks.ToArray());
            }

            IslandCard(me, true);

            // RUM plank.
            var rum = me.rum ?? new IslandRum();
            var plank = K.ListRow(_list, "Rum", 246f, "Header_Wood", 1f, null);
            K.Icon(K.TopLeft(plank, "Barrel", 22f, 28f, 124f, 124f), "Barrel");
            K.Label(K.TopLeft(plank, "Title", 166f, 16f, 220f, 58f), "RUM", 50f, K.OnWood, TextAlignmentOptions.MidlineLeft, true);
            K.Label(K.Strip(plank, "Split", 360f, 22f, 30f, 44f), $"<color=#e8c27a>Exposed</color> {rum.exposed}  •  <color=#e8c27a>Vault</color> {rum.vault}",
                28f, K.OnWood, TextAlignmentOptions.MidlineRight, false, 16f);
            K.Label(K.TopLeft(plank, "Rate", 166f, 74f, 320f, 42f), $"{rum.ratePerHour} / hr", 34f, K.OnWood, TextAlignmentOptions.MidlineLeft, true);
            K.Label(K.Strip(plank, "Note", 400f, 72f, 30f, 40f), "Exposed RUM can be looted; the vault is safe.", 21f, new Color(0.95f, 0.85f, 0.7f, 0.85f), TextAlignmentOptions.MidlineRight, false, 14f);
            var track = K.Strip(plank, "Bar", 166f, 124f, 220f, 32f);
            K.Paint(track, "Bar_Track", 1f, null, new Color(0.2f, 0.12f, 0.06f, 1f));
            var fill = K.Fill(track, "Fill", 4f, 4f, 4f, 4f);
            fill.anchorMax = new Vector2(Mathf.Max(0.001f, IslandRules.RumFill(rum.total, rum.cap)), 1f);
            K.Paint(fill, "Bar_Fill_Gold", 1f, null, new Color(0.95f, 0.7f, 0.2f, 1f));
            K.Label(K.TopRight(plank, "Amount", 28f, 114f, 186f, 50f), $"{rum.total} / {rum.cap}", 36f, K.OnWood, TextAlignmentOptions.MidlineRight, true, 20f);
            K.KitButton(K.Strip(plank, "Collect", 24f, 164f, 24f, 66f), $"Collect {rum.total} RUM", null, Collect, "Btn_Gold", 34f);

            // Buildings.
            K.SectionHead(_list, "Icon_House", "Buildings", "upgrade with RUM");
            foreach (var kind in IslandRules.BuildingKinds)
            {
                var b = me.Building(kind);
                if (b == null) continue;
                var row = K.ListRow(_list, kind, 112f, "Panel_Inset", 2.2f);
                K.Icon(K.TopLeft(row, "Icon", 16f, 12f, 88f, 88f), BuildingArt(kind));
                var at = b.level >= b.maxLevel;
                K.Label(K.Strip(row, "Name", 122f, 14f, 392f, 46f), IslandRules.BuildingName(kind), 32f, K.Ink, TextAlignmentOptions.MidlineLeft, true, 20f);
                K.Label(K.Strip(row, "Effect", 122f, 60f, 392f, 38f), IslandRules.BuildingEffect(kind, b.level), 23f, K.InkDim, TextAlignmentOptions.MidlineLeft, false, 15f);
                K.Tag(K.TopRight(row, "Level", 266f, 30f, 112f, 52f), "Tag_Tan", $"Lv {b.level}/{b.maxLevel}", 26f);
                var captured = kind;
                var up = K.KitButton(K.TopRight(row, "Upgrade", 16f, 18f, 236f, 76f), at ? "Max level" : $"{b.nextCost} RUM", at ? null : "Icon_Arrow_Up", () => Upgrade(captured), "Btn_Gold", 30f, 32f);
                up.interactable = !at && !me.listed;
            }

            // Defence and shield.
            K.SectionHead(_list, "Icon_Shield", "Defence");
            K.TextRow(_list, $"{HackathonRules.CaptainName} guards it with your design: 15 one-minute waves and the guardian. " +
                             "Raiders must sink the guardian to conquer it.", 25f);
            var islandId = me.islandId;
            var islandName = me.name;
            var defenceButtons = K.ButtonRow(_list, 92f, ("Design defences", "Icon_Gear", () => OpenDefence(islandId, islandName), "Btn_Gold"),
                ("Test defences", "Icon_Swords", () => StartPractice(islandId, islandName), "Btn_Gold"));
            defenceButtons[0].interactable = !me.listed;
            defenceButtons[1].interactable = !_busy;
            K.TextRow(_list, "Test defences: sail a practice raid against your saved design. No RUM fee, no loot, no shield, nothing recorded.", 21f, K.InkDim);
            K.InfoRow(_list, "Icon_Trophy", "Defence record:", IslandRules.DefenceRecordLine(me.defenceRecord));
            _shield = K.InfoRow(_list, "Icon_Shield", "Shield:", me.shielded ? "<color=#2a5f8f>Shielded</color>" : "None (raidable)");
            _shieldUntil = OnlineRules.ParseUtc(me.shieldUntil);
            Update();
        }

        static string BuildingArt(string kind)
        {
            switch (kind)
            {
                case "rum_store": return "Barrel";
                case "vault": return "Chest_2";
                case "cannon_tower": return "Cannons";
                default: return "Icon_Shield";
            }
        }

        /// <summary>The island card: picture, name, level tag, spot and region, NFT state; mine adds View NFT / Sell.</summary>
        void IslandCard(IslandView island, bool mine)
        {
            var card = K.ListRow(_list, "Island", mine ? 292f : 262f, "Panel_Inset", 1.6f);
            K.IslandPicture(K.TopLeft(card, "Picture", 22f, 22f, 164f, 164f), island.nft?.imageUrl);
            K.Label(K.Strip(card, "Name", 206f, 16f, 170f, 64f), island.name, 52f, K.Ink, TextAlignmentOptions.MidlineLeft, true, 28f);
            K.Tag(K.TopRight(card, "Level", 24f, 24f, 128f, 56f), "Tag_Tan", $"Lv {island.level}", 30f);
            K.Label(K.Strip(card, "Where", 206f, 84f, 24f, 40f), $"{island.slotName}  •  {island.region}", 30f, K.Ink, TextAlignmentOptions.MidlineLeft, false, 18f);

            if (mine)
            {
                var nft = island.nft;
                var minted = nft != null && nft.Minted;
                if (minted) K.Icon(K.TopLeft(card, "Tick", 206f, 132f, 38f, 38f), "Icon_Check_Green");
                var nftLine = nft == null ? "No NFT yet"
                    : minted ? $"<color={K.Hex(K.InkGood)}>NFT minted</color>  <size=80%>{IslandUi.Short(nft.mint)}</size>"
                    : $"<color={K.Hex(K.InkBad)}>NFT {nft.status}</color>" + (string.IsNullOrEmpty(nft.error) ? string.Empty : $"  <size=75%>{nft.error}</size>");
                if (island.listed) nftLine += "  •  <color=#6a3d9a>Listed on the market</color>";
                K.Label(K.Strip(card, "Nft", minted ? 252f : 206f, 130f, 24f, 42f), nftLine, 29f, K.Ink, TextAlignmentOptions.MidlineLeft, true, 16f);

                var buttons = K.Strip(card, "Buttons", 22f, 198f, 22f, 76f);
                if (minted)
                {
                    var mint = nft.mint;
                    K.KitButton(K.Columns(buttons, "View NFT", 0f, 0.5f, 0f, 9f), "View NFT", "Icon_Link", () => Application.OpenURL(IslandRules.ExplorerAddress(mint)), "Btn_Gold", 32f, 34f);
                    K.KitButton(K.Columns(buttons, "Sell", 0.5f, 1f, 9f, 0f), "Sell", "Icon_Tag", () => { if (Market != null) Market.Open(); }, "Btn_Gold", 32f, 34f);
                }
                else K.KitButton(buttons, "Retry mint", "Icon_Refresh", RetryMint, "Btn_Gold", 32f, 34f);
                return;
            }

            var owner = island.owner != null ? $"Owner  <b>{island.owner.name}</b>  <color={K.Hex(K.InkDim)}>{island.owner.walletShort}</color>" : "Owner  -";
            K.Label(K.Strip(card, "Owner", 206f, 128f, 24f, 40f), owner, 27f, K.Ink, TextAlignmentOptions.MidlineLeft, false, 16f);
            var (tag, text) = island.listed ? ("Tag_Blue", "On the market")
                : island.shielded ? ("Tag_Blue", "Shielded " + (IslandRules.TimeLeft(DateTime.UtcNow, OnlineRules.ParseUtc(island.shieldUntil)) ?? string.Empty))
                : island.raidInProgress ? ("Tag_Red", "Under attack")
                : island.raidable ? ("Tag_Red", "Raidable") : ("Tag_Tan", "Not raidable");
            K.Tag(K.TopLeft(card, "State", 206f, 182f, 260f, 56f), tag, text.Trim(), 26f);
        }

        void LastRaidBlock()
        {
            var last = RaidHud.LastResult;
            if (last == null) return;
            K.TextRow(_list, "<b>Last raid</b>\n" + last, 26f, null, "Panel_Inset");
        }

        async void Claim()
        {
            if (_busy) return;
            var name = _nameInput != null ? _nameInput.text : null;
            _busy = true;
            Status("Claiming the island and minting its NFT on devnet (a few seconds)...", HackUi.Gold);
            try
            {
                var (island, error) = await IslandApi.Claim(name);
                if (this == null) return;
                if (error != null) { Status(error, HackUi.Bad); return; }
                Status(island.nft != null && island.nft.Minted ? $"{island.name} is yours! Island NFT minted: {IslandUi.Short(island.nft.mint)}." : $"{island.name} is yours! The NFT mint is pending (retrying).", HackUi.Good);
                _view = View.Mine;
            }
            finally { _busy = false; }
            KeepStatusRefresh();
        }

        async void RetryMint()
        {
            var me = MyIsland;
            if (me == null || _busy) return;
            _busy = true;
            Status("Minting the Island NFT on devnet...", HackUi.Gold);
            try
            {
                var (island, error) = await IslandApi.RetryMint(me.islandId);
                if (this == null) return;
                if (error != null) { Status(error, HackUi.Bad); return; }
                Status(island != null && island.nft != null && island.nft.Minted ? "Island NFT minted: " + IslandUi.Short(island.nft.mint) : "The NFT is minted.", HackUi.Good);
            }
            finally { _busy = false; }
            KeepStatusRefresh();
        }

        async void Collect()
        {
            var me = MyIsland;
            if (me == null || _busy) return;
            _busy = true;
            Status("Collecting...", HackUi.Gold);
            try
            {
                var (data, error) = await IslandApi.Collect(me.islandId);
                if (this == null) return;
                if (error != null) { Status(error, HackUi.Bad); return; }
                Status($"Collected {data.collected} RUM ({data.fromStore} from the store, {data.fromVault} from the vault). You have {data.rum} RUM.", HackUi.Good);
            }
            finally { _busy = false; }
            KeepStatusRefresh();
        }

        async void Upgrade(string kind)
        {
            var me = MyIsland;
            if (me == null || _busy) return;
            _busy = true;
            Status("Building...", HackUi.Gold);
            try
            {
                var (data, error) = await IslandApi.Build(me.islandId, kind);
                if (this == null) return;
                if (error != null) { Status(error, HackUi.Bad); return; }
                Status($"{IslandRules.BuildingName(kind)} is now level {data.level} (-{data.cost} RUM). You have {data.rum} RUM.", HackUi.Good);
            }
            finally { _busy = false; }
            KeepStatusRefresh();
        }

        /// <summary>Reloads everything but keeps the status line of the action that just finished.</summary>
        async void KeepStatusRefresh()
        {
            var text = _status.text;
            var color = _statusColor;
            var request = ++_requests;
            var (world, _) = await IslandApi.World();
            if (this == null || request != _requests) return;
            if (world != null) _world = world;
            var (mine, _) = await IslandApi.Mine();
            if (this == null || request != _requests) return;
            if (mine != null) _mine = mine;
            Render();
            Status(text, color);
        }

        // ---------------------------------------------------------------- another island: raid

        IslandView SelectedIsland()
        {
            if (_world?.islands == null || string.IsNullOrEmpty(_selectedSlotIsland)) return null;
            foreach (var i in _world.islands) if (i != null && i.islandId == _selectedSlotIsland) return i;
            return null;
        }

        void ShowIsland()
        {
            var island = SelectedIsland();
            if (island == null)
            {
                IslandSlotView slot = null;
                if (_world?.slots != null) foreach (var s in _world.slots) if (s != null && s.slot == _selectedSlot) slot = s;
                K.TextRow(_list, slot == null ? "Tap an island on the map." :
                    $"<size=130%><b>{slot.name}</b></size>\n{slot.region}\nUnclaimed waters. Claiming settles a random free spot like this one.", 27f, null, "Panel_Inset");
                BackButton();
                return;
            }

            IslandCard(island, false);
            var exposed = island.rum != null ? island.rum.exposed : 0;
            K.InfoRow(_list, "Barrel", "RUM per hour", (island.rum != null ? island.rum.ratePerHour : 0).ToString());
            K.InfoRow(_list, "Icon_Trophy", "Defence record:", IslandRules.DefenceRecordLine(island.defenceRecord));
            K.SectionHead(_list, "Icon_Skull_Crossbones", $"Defender: {HackathonRules.CaptainName}", HackathonRules.CaptainTitle);
            K.TextRow(_list, "<b>Conquest:</b> survive the island's designed waves (15 minutes) and sink its guardian, the Barnacle King. " +
                             "There is no beacon: he rises on his own at the minute the owner set (the raid HUD counts down to it). " +
                             "Reaching the guardian's minute without sinking it is a partial win.", 24f, null, "Panel_Inset");
            K.InfoRow(_list, "Chest_2", "Loot (exposed " + exposed + ")", $"win <color={K.Hex(K.InkGood)}>{exposed * 25 / 100}</color>  •  partial {exposed * 25 / 200}");
            K.InfoRow(_list, "Icon_Tag", "Raid fee", $"{IslandRules.RaidFeeRum} RUM  (you have {(_mine != null ? _mine.rum : 0)})");
            K.TextRow(_list, "A raided island gets a 4 h shield.", 21f, K.InkDim);

            if (island.isMine) { BackButton(); return; }
            if (!Web3Session.Instance.SignedIn) { K.TextRow(_list, "Connect a wallet to raid.", 26f, K.InkBad); BackButton(); return; }

            if (_pendingRaid != null && _pendingRaid.islandId == island.islandId)
            {
                K.TextRow(_list, "Raid started.", 25f, K.InkGold);
                K.ButtonRow(_list, 96f, ("Set sail: raid!", "Icon_Ship_Wheel", (Action)SetSail, "Btn_Gold"));
                BackButton();
                return;
            }

            var raid = K.ButtonRow(_list, 96f, ($"Raid {island.name}", "Icon_Swords", () => StartRaid(island), "Btn_Gold"))[0];
            raid.interactable = island.raidable && !_busy;
            if (!island.raidable)
                K.TextRow(_list, island.listed ? "Listed islands cannot be raided." : island.shielded ? "Shielded: try again later." : "Not raidable right now.", 24f, K.InkDim);
            BackButton();
        }

        void BackButton() => K.ButtonRow(_list, 80f, ("Back to my island", "Icon_Chevron_Left", () => Show(View.Mine), "Btn_Parchment"));

        async void StartRaid(IslandView island)
        {
            if (_busy) return;
            _busy = true;
            Status($"Starting the raid on {island.name} (fee {IslandRules.RaidFeeRum} RUM)...", HackUi.Gold);
            try
            {
                var (raid, error) = await IslandApi.StartRaid(island.islandId);
                if (this == null) return;
                if (error != null) { Status(error, HackUi.Bad); return; }
                _pendingRaid = raid;
                if (_mine != null) _mine.rum = raid.rum;
            }
            finally { _busy = false; }
            SetSail();
        }

        /// <summary>"Test defences": a practice raid on your own island with its saved design (no fee, nothing recorded).</summary>
        public async void StartPractice(string islandId, string islandName)
        {
            if (_busy || string.IsNullOrEmpty(islandId)) return;
            _busy = true;
            Status($"Preparing a practice raid on {islandName}...", HackUi.Gold);
            try
            {
                var (raid, error) = await IslandApi.PracticeRaid(islandId);
                if (this == null) return;
                if (error != null) { Status(error, HackUi.Bad); return; }
                _pendingRaid = raid;
            }
            finally { _busy = false; }
            SetSail();
        }

        void SetSail()
        {
            var raid = _pendingRaid;
            if (raid == null) return;
            _pendingRaid = null;
            if (PvpSession.Exists && PvpSession.Instance.Lobby != null) _ = PvpSession.Instance.Leave();
            RaidHud.Begin(raid);
            MenuServices.BeginSaltwindExperiment();
            SceneManager.LoadScene(V4SceneNames.SaltwindRun);
        }

        DefencePanel _defence;
        /// <summary>The defence designer (Hackathon, 12 Oct); created on first use.</summary>
        public DefencePanel Defence => _defence;

        public void OpenDefence(string islandId, string islandName)
        {
            if (_defence == null) { _defence = DefencePanel.Create(transform.parent); _defence.Closed = Open; _defence.Practice = StartPractice; }
            gameObject.SetActive(false); // the designer covers the Islands screen; Close comes back here
            _defence.Open(islandId, islandName);
        }

        // ---------------------------------------------------------------- history

        async void ShowHistory()
        {
            if (!Web3Session.Instance.SignedIn) { K.TextRow(_list, "Connect a wallet to see your raids.", 26f, null, "Panel_Inset"); return; }
            K.TextRow(_list, "Loading your raids...", 24f, K.InkDim);
            var (raids, error) = await IslandApi.MyRaids();
            if (this == null || _view != View.History) return;
            ClearList();
            if (error != null) { K.TextRow(_list, error, 24f, K.InkBad, "Panel_Inset"); return; }
            LastRaidBlock();
            K.SectionHead(_list, "Icon_Swords", "Your raids");
            Rows(raids.attacks, true);
            K.SectionHead(_list, "Icon_Shield", "Raids on your island");
            Rows(raids.defences, false);
        }

        void Rows(RaidRow[] rows, bool attacks)
        {
            if (rows == null || rows.Length == 0)
            {
                K.TextRow(_list, attacks ? "No raids yet. Tap a red island on the map." : "Nobody has raided you yet.", 24f, K.InkDim, "Panel_Inset");
                return;
            }
            foreach (var r in rows)
            {
                if (r == null) continue;
                var row = K.ListRow(_list, "Raid", 108f, "Panel_Inset", 2.2f);

                // Picture: the raided island (its NFT when the map knows it) or the raider's black flag.
                var pic = K.TopLeft(row, "Picture", 14f, 12f, 84f, 84f);
                if (attacks) K.IslandPicture(pic, IslandImage(r.islandId));
                else
                {
                    K.Paint(pic, "Slot_Card", 3f, new Color(0.3f, 0.22f, 0.16f, 1f), new Color(0.2f, 0.14f, 0.1f, 1f));
                    K.Icon(K.Fill(pic, "Flag", 14f, 14f, 14f, 14f), "Icon_Skull_Crossbones", K.OnWood);
                }

                var who = attacks ? r.islandName : (string.IsNullOrEmpty(r.attacker?.name) ? "A raider" : r.attacker.name);
                K.Label(K.Strip(row, "Name", 114f, 12f, 446f, 46f), who, 31f, K.Ink, TextAlignmentOptions.MidlineLeft, true, 18f);
                var when = K.Ago(OnlineRules.ParseUtc(r.startedAt));
                var detail = (attacks ? when : $"{when}  •  on {r.islandName}") + (r.seconds > 0 ? "  •  " + IslandRules.Clock(r.seconds) : string.Empty);
                K.Label(K.Strip(row, "When", 114f, 58f, 446f, 38f), detail, 22f, K.InkDim, TextAlignmentOptions.MidlineLeft, false, 14f);

                var (tag, label) = ResultTag(r.result, attacks);
                K.Tag(K.TopRight(row, "Result", 262f, 28f, 172f, 52f), tag, label, 25f);

                var loot = K.TopRight(row, "Loot", 14f, 24f, 236f, 60f);
                if (r.rumLooted > 0)
                {
                    K.Icon(K.TopLeft(loot, "Barrel", 0f, 6f, 48f, 48f), "Barrel");
                    K.Label(K.Fill(loot, "Value", 56f), (attacks ? "+" : "-") + r.rumLooted + " RUM", 30f, attacks ? K.InkGood : K.InkBad, TextAlignmentOptions.MidlineLeft, true, 18f);
                }
                else K.Label(K.Fill(loot, "Value", 8f), attacks ? "No loot" : "0 RUM lost", 27f, K.InkDim, TextAlignmentOptions.MidlineLeft, false, 16f);
            }
        }

        /// <summary>The tag of a raid result, from the raider's side (Won / Lost) or the owner's (Raided / Defended).</summary>
        static (string sprite, string label) ResultTag(string result, bool attacks)
        {
            switch (result)
            {
                case "won": return attacks ? ("Tag_Green", "Won") : ("Tag_Red", "Raided");
                case "partial": return attacks ? ("Tag_Tan", "Partial") : ("Tag_Tan", "Partly raided");
                case "lost": return attacks ? ("Tag_Red", "Lost") : ("Tag_Green", "Defended");
                case "started": return attacks ? ("Tag_Blue", "In progress") : ("Tag_Blue", "Under attack");
                case "abandoned": return attacks ? ("Tag_Tan", "Abandoned") : ("Tag_Green", "Defended");
                case "expired": return attacks ? ("Tag_Tan", "Expired") : ("Tag_Green", "Defended");
            }
            return ("Tag_Tan", string.IsNullOrEmpty(result) ? "-" : result);
        }

        string IslandImage(string islandId)
        {
            if (_world?.islands != null)
                foreach (var i in _world.islands)
                    if (i != null && i.islandId == islandId) return i.nft?.imageUrl;
            return null;
        }
    }
}
