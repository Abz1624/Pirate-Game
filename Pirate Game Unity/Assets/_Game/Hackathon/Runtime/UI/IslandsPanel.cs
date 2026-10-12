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

namespace RoguePirates.Hackathon.UI
{
    /// <summary>
    /// Hackathon (12 Oct, U-3): the menu's "Islands" screen. Left: the world map (GET /api/islands: 24 slots on a
    /// 1000x1000 sea, markers for free, mine, raidable, shielded and listed islands). Right: "My island" (GET
    /// /api/islands/mine: claim with a name, the Island NFT, RUM with a filling bar and Collect, buildings with Upgrade,
    /// defence and shield), a tapped island's raid panel (Captain Marrow defends; loot and the RUM fee, no SOL stake; Raid starts
    /// the raid voyage, <see cref="RaidHud"/>) and the raid history (GET /api/raids/mine).
    /// </summary>
    public sealed class IslandsPanel : MonoBehaviour
    {
        enum View { Mine, Island, History }

        RectTransform _map, _markers, _list;
        TextMeshProUGUI _rum, _status, _shield;
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
        string _shieldPrefix;

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
            var rt = HackUi.Panel(canvas, "HackathonIslands", new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.96f), 227);
            var panel = rt.gameObject.AddComponent<IslandsPanel>();
            panel.Build(rt);
            return panel;
        }

        void Build(RectTransform rt)
        {
            var header = HackUi.TopStrip(rt, "Header", 14f, 80f);
            HackUi.TextAt(header, "Title", "Islands", 46, TextAlignmentOptions.MidlineLeft, HackUi.Gold, Vector2.zero, new Vector2(0.22f, 1f), true);
            _rum = HackUi.TextAt(header, "Rum", string.Empty, 28, TextAlignmentOptions.MidlineLeft, HackUi.TextColor, new Vector2(0.2f, 0f), new Vector2(0.5f, 1f));
            HackUi.Button(header, "Market", () => { if (Market != null) Market.Open(); }, new Vector2(0.5f, 0.06f), new Vector2(0.66f, 0.94f));
            HackUi.Button(header, "Refresh", Refresh, new Vector2(0.675f, 0.06f), new Vector2(0.835f, 0.94f));
            _close = HackUi.Button(header, "Close", Close, new Vector2(0.845f, 0.06f), new Vector2(1f, 0.94f));

            var statusStrip = HackUi.TopStrip(rt, "Status", 98f, 40f);
            _status = HackUi.TextAt(statusStrip, "Text", string.Empty, 24, TextAlignmentOptions.MidlineLeft, HackUi.Dim, Vector2.zero, Vector2.one);

            // Left: the map (square, as large as fits).
            var left = HackUi.Area(rt, "MapArea", Vector2.zero, new Vector2(0.5f, 1f), new Vector4(24f, 20f, 10f, 146f));
            var holder = HackUi.Area(left, "MapHolder", Vector2.zero, Vector2.one, new Vector4(0f, 44f, 0f, 0f));
            var mapGo = new GameObject("Map", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter), typeof(RectMask2D));
            mapGo.transform.SetParent(holder, false);
            _map = (RectTransform)mapGo.transform;
            _map.anchorMin = Vector2.zero;
            _map.anchorMax = Vector2.one;
            _map.offsetMin = _map.offsetMax = Vector2.zero;
            mapGo.GetComponent<Image>().color = new Color(0.09f, 0.33f, 0.47f, 1f);
            var fit = mapGo.GetComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 1f;
            DrawSea(_map);
            _markers = HackUi.Area(_map, "Markers", Vector2.zero, Vector2.one);
            Legend(left);

            // Right: tabs and the detail list.
            var right = HackUi.Area(rt, "Detail", new Vector2(0.5f, 0f), Vector2.one, new Vector4(10f, 20f, 24f, 146f));
            var tabs = HackUi.Area(right, "Tabs", new Vector2(0f, 1f), Vector2.one);
            tabs.offsetMin = new Vector2(0f, -76f);
            _tabMine = HackUi.Button(tabs, "My island", () => Show(View.Mine), new Vector2(0f, 0f), new Vector2(0.49f, 1f));
            _tabHistory = HackUi.Button(tabs, "Raid history", () => Show(View.History), new Vector2(0.51f, 0f), new Vector2(1f, 1f));
            var body = HackUi.Area(right, "Body", Vector2.zero, Vector2.one, new Vector4(0f, 0f, 0f, 88f));
            _list = HackUi.ScrollList(body, "Scroll", Vector4.zero);
            _list.GetComponent<VerticalLayoutGroup>().spacing = 8f;
        }

        // ---------------------------------------------------------------- map art

        static void DrawSea(RectTransform map)
        {
            // Grid lines (a chart look) and a lighter shallows band.
            for (var i = 1; i < 10; i++)
            {
                Line(map, new Vector2(i / 10f, 0f), new Vector2(i / 10f, 1f), 2f, true);
                Line(map, new Vector2(0f, i / 10f), new Vector2(1f, i / 10f), 2f, false);
            }
            // Region names (the server's regions sit roughly here).
            Region(map, "Saltwind Reach", 0.25f, 0.96f);
            Region(map, "Coral Shallows", 0.72f, 0.96f);
            Region(map, "Black Tide", 0.2f, 0.62f);
            Region(map, "Maelstrom Deep", 0.68f, 0.62f);
            Region(map, "Ember Isles", 0.3f, 0.04f);
            Region(map, "Golden Wake", 0.78f, 0.04f);
            var compass = HackUi.TextAt(map, "Compass", "N\n<size=60%>^</size>", 30, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.5f),
                new Vector2(0.005f, 0.74f), new Vector2(0.07f, 0.84f), true);
            compass.textWrappingMode = TextWrappingModes.Normal;
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
            var t = HackUi.TextAt(map, name, name.ToUpperInvariant(), 20, TextAlignmentOptions.Center, new Color(0.85f, 0.95f, 1f, 0.32f),
                new Vector2(x - 0.2f, y - 0.03f), new Vector2(x + 0.2f, y + 0.03f), true);
            t.characterSpacing = 6f;
        }

        static readonly (IslandRules.Marker kind, string label, Color color)[] LegendItems =
        {
            (IslandRules.Marker.Mine, "Yours", new Color(1f, 0.82f, 0.3f)),
            (IslandRules.Marker.Raidable, "Raidable", new Color(0.95f, 0.35f, 0.3f)),
            (IslandRules.Marker.Shielded, "Shielded", new Color(0.45f, 0.75f, 1f)),
            (IslandRules.Marker.Listed, "On market", new Color(0.75f, 0.5f, 0.95f)),
            (IslandRules.Marker.Free, "Free", new Color(0.85f, 0.8f, 0.65f, 0.55f)),
        };

        static Color MarkerColor(IslandRules.Marker kind)
        {
            foreach (var l in LegendItems) if (l.kind == kind) return l.color;
            return new Color(0.7f, 0.7f, 0.7f);
        }

        static void Legend(RectTransform left)
        {
            var strip = HackUi.Area(left, "Legend", Vector2.zero, new Vector2(1f, 0f));
            strip.pivot = new Vector2(0.5f, 0f);
            strip.offsetMin = new Vector2(0f, -2f);
            strip.offsetMax = new Vector2(0f, 34f);
            var h = strip.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = 8f;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            foreach (var (_, label, color) in LegendItems)
            {
                var dot = new GameObject("Dot", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                dot.transform.SetParent(strip, false);
                dot.GetComponent<Image>().sprite = IslandUi.Circle;
                dot.GetComponent<Image>().color = color;
                var le = dot.GetComponent<LayoutElement>();
                le.preferredWidth = le.minWidth = 20f;
                le.preferredHeight = 20f;
                var t = HackUi.Text(strip, label, label, 20, TextAlignmentOptions.MidlineLeft, HackUi.Dim);
                t.gameObject.AddComponent<LayoutElement>().preferredWidth = t.GetPreferredValues(label).x + 14f;
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
                var size = island == null ? 22f : 40f;
                var go = new GameObject("Slot" + s.slot, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(_markers, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
                rt.sizeDelta = new Vector2(size, size);
                var img = go.GetComponent<Image>();
                img.sprite = IslandUi.Circle;
                img.color = MarkerColor(kind);
                if (s.slot == _selectedSlot)
                {
                    var ring = new GameObject("Selected", typeof(RectTransform), typeof(Image));
                    ring.transform.SetParent(go.transform, false);
                    ring.transform.SetAsFirstSibling();
                    var rrt = (RectTransform)ring.transform;
                    rrt.anchorMin = Vector2.zero;
                    rrt.anchorMax = Vector2.one;
                    rrt.offsetMin = new Vector2(-10f, -10f);
                    rrt.offsetMax = new Vector2(10f, 10f);
                    ring.GetComponent<Image>().sprite = IslandUi.Circle;
                    ring.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.45f);
                    ring.GetComponent<Image>().raycastTarget = false;
                }
                var slot = s;
                var picked = island;
                go.GetComponent<Button>().onClick.AddListener(() => Select(slot, picked));
                var label = island != null ? island.name : s.name;
                var t = HackUi.Text(rt, "Name", label, island != null ? 20 : 15, TextAlignmentOptions.Top,
                    island != null ? HackUi.TextColor : new Color(0.9f, 0.95f, 1f, 0.55f), island != null);
                var trt = t.rectTransform;
                trt.anchorMin = new Vector2(0.5f, 0f);
                trt.anchorMax = new Vector2(0.5f, 0f);
                trt.pivot = new Vector2(0.5f, 1f);
                trt.sizeDelta = new Vector2(220f, 28f);
                trt.anchoredPosition = new Vector2(0f, -2f);
                t.outlineWidth = 0.15f;
                t.outlineColor = new Color32(0, 20, 40, 200);
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
            _rum.text = _mine != null ? $"<color=#ffd166>{_mine.rum}</color> RUM" : string.Empty;
            DrawMarkers();
            Show(_view);
        }

        void Show(View view)
        {
            _view = view;
            Tint(_tabMine, view == View.Mine);
            Tint(_tabHistory, view == View.History);
            ClearList();
            switch (view)
            {
                case View.Mine: ShowMine(); break;
                case View.Island: ShowIsland(); break;
                case View.History: ShowHistory(); break;
            }
        }

        static void Tint(Button b, bool on)
        {
            if (b == null) return;
            b.GetComponent<Image>().color = on ? new Color(1f, 0.78f, 0.3f, 1f) : new Color(0.55f, 0.45f, 0.25f, 1f);
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
        }

        void ClearList()
        {
            _shield = null;
            _shieldUntil = null;
            for (var i = _list.childCount - 1; i >= 0; i--)
            {
                var c = _list.GetChild(i).gameObject;
                c.SetActive(false);
                Destroy(c);
            }
        }

        void Status(string text, Color color)
        {
            if (_status == null) return;
            _status.text = text;
            _status.color = color;
        }

        void Update()
        {
            if (_shield == null || !_shieldUntil.HasValue) return;
            var left = IslandRules.TimeLeft(DateTime.UtcNow, _shieldUntil);
            _shield.text = left == null ? _shieldPrefix + "no shield" : _shieldPrefix + "<color=#7fc8ff>shielded for " + left + "</color>";
        }

        // ---------------------------------------------------------------- my island

        void ShowMine()
        {
            LastRaidBlock();
            if (!Web3Session.Instance.SignedIn)
            {
                IslandUi.TextRow(_list, "Connect a wallet (Wallet button, top of the menu) to claim an island. Claiming mints your Island NFT on Solana devnet.", 28);
                return;
            }
            var me = MyIsland;
            if (me == null)
            {
                IslandUi.TextRow(_list, "<color=#ffd166><b>Claim your island</b></color>\nName it, and the crew settles a free spot on the map. " +
                                        "Your island is minted as an NFT to your wallet, brews RUM every hour, and Captain Marrow defends it against raiders.", 27);
                var row = HackUi.Row(_list, IslandUi.ButtonRow, new Color(0f, 0f, 0f, 0f));
                _nameInput = HackUi.Input(row, "Island name (3 to 16 letters)", 16);
                IslandUi.RowButton(row, "Claim island", Claim, 240f);
                return;
            }

            // More than one island (bought on the market): pick which one to manage.
            if (_mine.islands.Length > 1)
            {
                var picks = new List<(string, Action)>();
                for (var i = 0; i < _mine.islands.Length; i++)
                {
                    var index = i;
                    var isl = _mine.islands[i];
                    if (isl == null) continue;
                    picks.Add(((index == _mineIndex ? "> " : string.Empty) + isl.name, () => { _mineIndex = index; Show(View.Mine); }));
                }
                IslandUi.ButtonRowOf(_list, picks.ToArray());
            }

            // Header: picture, name, NFT.
            var head = HackUi.Row(_list, 176f, IslandUi.MineTint);
            IslandUi.Picture(head, me.nft?.imageUrl, 160f);
            var nftLine = me.nft == null ? string.Empty
                : me.nft.Minted ? $"NFT <color=#7fd68b>minted</color>  {IslandUi.Short(me.nft.mint)}"
                : $"NFT <color=#ff8c73>{me.nft.status}</color>" + (string.IsNullOrEmpty(me.nft.error) ? string.Empty : $"  <size=80%>{me.nft.error}</size>");
            var info = HackUi.Cell(head, $"<color=#ffd166><size=120%><b>{me.name}</b></size></color>\n{me.slotName}, {me.region}   Level {me.level}\n{nftLine}" +
                                         (me.listed ? "\n<color=#c08cff>Listed on the market</color>" : string.Empty), 0f, 26, TextAlignmentOptions.MidlineLeft, HackUi.TextColor);
            info.textWrappingMode = TextWrappingModes.Normal;
            if (me.nft != null && me.nft.Minted)
                IslandUi.ButtonRowOf(_list, ("View NFT on explorer", () => Application.OpenURL(IslandRules.ExplorerAddress(me.nft.mint))),
                    ("Sell on market", () => { if (Market != null) Market.Open(); }));
            else
                IslandUi.ButtonRowOf(_list, ("Retry mint", RetryMint));

            // RUM.
            var rum = me.rum ?? new IslandRum();
            IslandUi.TextRow(_list, $"<color=#ffd166><b>RUM</b></color>   brewing {rum.ratePerHour}/h   " +
                                    $"<color=#ff8c73>exposed {rum.exposed}</color>   <color=#7fc8ff>vault {rum.vault}</color> (never raidable)", 25);
            IslandUi.Bar(_list, IslandRules.RumFill(rum.total, rum.cap), $"{rum.total} / {rum.cap} RUM", new Color(0.85f, 0.55f, 0.15f, 1f));
            IslandUi.ButtonRowOf(_list, ($"Collect {rum.total} RUM", Collect));

            // Buildings.
            IslandUi.TextRow(_list, "<color=#ffd166><b>Buildings</b></color>", 26);
            foreach (var kind in IslandRules.BuildingKinds)
            {
                var b = me.Building(kind);
                if (b == null) continue;
                var row = HackUi.Row(_list, IslandUi.ButtonRow, IslandUi.RowTint);
                var at = b.level >= b.maxLevel;
                var cell = HackUi.Cell(row, $"<b>{IslandRules.BuildingName(kind)}</b>  Lv {b.level}/{b.maxLevel}\n<size=80%><color=#9aa4ad>{IslandRules.BuildingEffect(kind, b.level)}</color></size>",
                    0f, 25, TextAlignmentOptions.MidlineLeft, HackUi.TextColor);
                cell.textWrappingMode = TextWrappingModes.Normal;
                var captured = kind;
                var up = IslandUi.RowButton(row, at ? "Max level" : $"Upgrade {b.nextCost}", () => Upgrade(captured), 240f);
                up.interactable = !at && !me.listed;
            }

            // Defence and shield.
            var d = me.defence ?? new IslandDefence();
            IslandUi.TextRow(_list, $"<color=#ffd166><b>Defence</b></color>   {HackathonRules.CaptainName} guards it   difficulty {d.difficulty}/7\n" +
                                    $"hull x{d.hullMultiplier:0.##}   +{d.extraWaves} waves   guns lv {d.weaponLevel}", 25);
            _shieldPrefix = "<color=#ffd166><b>Shield</b></color>   ";
            _shield = IslandUi.TextRow(_list, _shieldPrefix, 25);
            _shieldUntil = OnlineRules.ParseUtc(me.shieldUntil);
            _shield.text = _shieldPrefix + (me.shielded ? "<color=#7fc8ff>shielded</color>" : "no shield (raidable by other captains)");
            Update();
        }

        void LastRaidBlock()
        {
            var last = RaidHud.LastResult;
            if (last == null) return;
            IslandUi.TextRow(_list, "<color=#ffd166><b>Last raid</b></color>\n" + last, 26, null, 44f, new Color(1f, 0.82f, 0.4f, 0.12f));
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
            var color = _status.color;
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
                IslandUi.TextRow(_list, slot == null ? "Tap an island on the map." :
                    $"<color=#ffd166><b>{slot.name}</b></color>  ({slot.region})\nUnclaimed waters. Claiming settles a random free spot like this one.", 27);
                IslandUi.ButtonRowOf(_list, ("Back to my island", () => Show(View.Mine)));
                return;
            }

            var head = HackUi.Row(_list, 176f, IslandUi.RowTint);
            IslandUi.Picture(head, island.nft?.imageUrl, 160f);
            var owner = island.owner != null ? $"{island.owner.name}  <color=#9aa4ad>{island.owner.walletShort}</color>" : "-";
            var state = island.listed ? "<color=#c08cff>On the market</color>"
                : island.shielded ? "<color=#7fc8ff>Shielded</color> " + (IslandRules.TimeLeft(DateTime.UtcNow, OnlineRules.ParseUtc(island.shieldUntil)) ?? string.Empty)
                : island.raidInProgress ? "<color=#ff8c73>Under attack right now</color>"
                : island.raidable ? "<color=#ff8c73>Raidable</color>" : string.Empty;
            var cell = HackUi.Cell(head, $"<color=#ffd166><size=120%><b>{island.name}</b></size></color>\n{island.slotName}, {island.region}   Level {island.level}\nOwner {owner}\n{state}",
                0f, 26, TextAlignmentOptions.MidlineLeft, HackUi.TextColor);
            cell.textWrappingMode = TextWrappingModes.Normal;

            var d = island.defence ?? new IslandDefence();
            var target = IslandRules.RaidTargetKills(d.difficulty, d.hullMultiplier);
            var exposed = island.rum != null ? island.rum.exposed : 0;
            IslandUi.TextRow(_list, $"<color=#ffd166><b>Defender: {HackathonRules.CaptainName}</b></color>, {HackathonRules.CaptainTitle}\n" +
                                    $"Difficulty {d.difficulty}/7   hull x{d.hullMultiplier:0.##}   +{d.extraWaves} waves   guns lv {d.weaponLevel}\n" +
                                    $"<b>Raid:</b> sink {target} of his fleet within {IslandRules.Clock(IslandRules.RaidTimeLimitSeconds)}. Half of that is a partial win.", 25);
            IslandUi.TextRow(_list, $"<color=#ffd166><b>Loot</b></color>   exposed RUM {exposed}   win <color=#7fd68b>{exposed * 25 / 100}</color>   partial {exposed * 25 / 200}   " +
                                    $"\n<color=#9aa4ad>Fee {IslandRules.RaidFeeRum} RUM (you have {(_mine != null ? _mine.rum : 0)}). A raided island gets a 4 h shield.</color>", 25);

            if (island.isMine) return;
            if (!Web3Session.Instance.SignedIn) { IslandUi.TextRow(_list, "Connect a wallet to raid.", 26, HackUi.Bad); return; }

            if (_pendingRaid != null && _pendingRaid.islandId == island.islandId)
            {
                IslandUi.TextRow(_list, "Raid started.", 25, HackUi.Gold);
                IslandUi.ButtonRowOf(_list, ("Set sail: raid!", SetSail));
                return;
            }

            var raid = IslandUi.ButtonRowOf(_list, ($"Raid {island.name}", () => StartRaid(island)))[0];
            raid.interactable = island.raidable && !_busy;
            if (!island.raidable)
                IslandUi.TextRow(_list, island.listed ? "Listed islands cannot be raided." : island.shielded ? "Shielded: try again later." : "Not raidable right now.", 24, HackUi.Dim);
        }

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

        // ---------------------------------------------------------------- history

        async void ShowHistory()
        {
            if (!Web3Session.Instance.SignedIn) { IslandUi.TextRow(_list, "Connect a wallet to see your raids.", 26); return; }
            IslandUi.TextRow(_list, "Loading raids...", 24, HackUi.Dim);
            var (raids, error) = await IslandApi.MyRaids();
            if (this == null || _view != View.History) return;
            ClearList();
            if (error != null) { IslandUi.TextRow(_list, error, 24, HackUi.Bad); return; }
            LastRaidBlock();
            IslandUi.TextRow(_list, "<color=#ffd166><b>Your raids</b></color>", 27);
            Rows(raids.attacks, true);
            IslandUi.TextRow(_list, "<color=#ffd166><b>Raids on your islands</b></color>", 27);
            Rows(raids.defences, false);
        }

        void Rows(RaidRow[] rows, bool attacks)
        {
            if (rows == null || rows.Length == 0) { IslandUi.TextRow(_list, attacks ? "No raids yet. Tap a red island on the map." : "Nobody has raided you yet.", 24, HackUi.Dim); return; }
            foreach (var r in rows)
            {
                if (r == null) continue;
                var row = HackUi.Row(_list, 96f, IslandUi.RowTint);
                var color = r.result == "won" ? "#7fd68b" : r.result == "partial" ? "#ffd166" : r.result == "lost" ? "#ff8c73" : "#9aa4ad";
                var who = attacks ? r.islandName : $"{r.attacker?.name} on {r.islandName}";
                var when = OnlineRules.ParseUtc(r.startedAt);
                var c = HackUi.Cell(row, $"<b>{who}</b>   <color={color}>{r.result}</color>   {(attacks ? "+" : "-")}{r.rumLooted} RUM" +
                                         (r.seconds > 0 ? "   " + IslandRules.Clock(r.seconds) : string.Empty) +
                                         (when.HasValue ? $"\n<size=80%><color=#9aa4ad>{when.Value.ToLocalTime():dd MMM HH:mm}</color></size>" : string.Empty),
                    0f, 23, TextAlignmentOptions.MidlineLeft, HackUi.TextColor);
                c.textWrappingMode = TextWrappingModes.Normal;
                c.overflowMode = TextOverflowModes.Overflow;
            }
        }
    }
}
