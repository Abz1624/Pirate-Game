using System;
using System.Collections.Generic;
using RoguePirates.V4.Foundation.UI;
using RoguePirates.V4.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Hud
{
    /// <summary>
    /// Binds the art team's HUD_SaltWind prefab to the V4 run views (demo): the Captain panel (portrait, name and level,
    /// hull and XP bars), the timer, gold and kills, the objective banner (the current quest line, one pip per step done)
    /// and the weapon row (icons from the icon catalogue, levels). It replaces the authored HUD's profile, timer, counters,
    /// quest list and weapon row, which it keeps hidden; items and Logbooks, the minimap, boost, Captain active, level-up
    /// cards and results stay V4's. The HUD hides while the results screen hides the profile.
    /// </summary>
    public sealed class SaltWindHudPresenter : MonoBehaviour
    {
        static readonly string[] Replaced = { "TImer", "Coin_Count", "Kill_Count", "Profile", "Quest", "Weapon_Slot", "Loadout_Right" };

        IRunViews _views;
        IconCatalog _icons;
        SaltWindHudSettings _settings;
        AuthoredHudPresenter _authored;
        GameObject _hud;
        TextMeshProUGUI _name, _time, _gold, _kills, _rum;
        Image _portrait;
        Slider _hp, _xp;
        readonly List<Image> _markers = new List<Image>();
        readonly List<WeaponSlotView> _weapons = new List<WeaponSlotView>();
        // Playtest (3 Oct, owner): the Logbooks in a copy of the weapon row, left of it; slots without art show the name.
        readonly List<WeaponSlotView> _logbooks = new List<WeaponSlotView>();
        // Playtest (3 Oct, owner): the items in a copy of the weapon row at the bottom centre (the art's ItemBar hides).
        readonly List<WeaponSlotView> _itemSlots = new List<WeaponSlotView>();
        GridLayoutGroup _itemGrid;
        float _itemRowFor = -1f; // the HUD width the item row's columns were fitted to
        TMP_FontAsset _font;
        readonly List<string> _quest = new List<string>(3);
        int _lastCaptain = -1;

        /// <summary>The art's minimap frame (V4's minimap follows it).</summary>
        public RectTransform ArtMinimap { get; private set; }
        /// <summary>The art's level-up cards (the level-up presenter shows them).</summary>
        public Transform UpgradeSelection { get; private set; }

        sealed class WeaponSlotView
        {
            public GameObject Root;
            public Image Icon;
            public GameObject Empty, Badge, Locked, Selected;
            public TextMeshProUGUI Level;
            public TextMeshProUGUI Name; // the name, when there is no icon
        }

        public GameObject Hud => _hud;
        // Last values shown: text is rebuilt only when they change (no per-frame garbage on phones).
        int _shownSeconds = -1, _shownGold = int.MinValue, _shownKills = int.MinValue, _shownRum = int.MinValue, _shownLevel = -1, _shownDone = -1;
        bool _shownOvertime;
        readonly int[] _shownWeaponLevels = new int[8];
        RectTransform _safeArea;
        Rect _lastSafe;
        Vector2Int _lastScreen;

        /// <summary>The HUD's SafeArea follows the screen's safe area (notch, cutout) plus a margin for rounded corners on phones.</summary>
        void FitSafeArea()
        {
            if (_safeArea == null) return;
            var screen = new Vector2Int(Screen.width, Screen.height);
            var safe = Screen.safeArea;
            if (safe == _lastSafe && screen == _lastScreen) return;
            _lastSafe = safe;
            _lastScreen = screen;
            var margin = Application.isMobilePlatform ? RoguePirates.V4.Foundation.Core.DemoMode.MobileHudEdgeMargin * screen.y : 0f;
            var xMin = Mathf.Max(safe.xMin, margin);
            var xMax = Mathf.Min(safe.xMax, screen.x - margin);
            var yMin = Mathf.Max(safe.yMin, margin * 0.5f);
            var yMax = Mathf.Min(safe.yMax, screen.y - margin * 0.5f);
            _safeArea.anchorMin = new Vector2(xMin / Mathf.Max(1, screen.x), yMin / Mathf.Max(1, screen.y));
            _safeArea.anchorMax = new Vector2(xMax / Mathf.Max(1, screen.x), yMax / Mathf.Max(1, screen.y));
            _safeArea.offsetMin = Vector2.zero;
            _safeArea.offsetMax = Vector2.zero;
        }
        /// <summary>Off: the authored HUD shows as before (the V4 HUD PlayMode tests cover those widgets).</summary>
        public static bool Disabled
        {
            get => RoguePirates.V4.Foundation.Core.DemoMode.Off;
            set => RoguePirates.V4.Foundation.Core.DemoMode.Off = value;
        }

        /// <summary>Instantiates the HUD on the authored canvas; null when the settings or prefab are missing.</summary>
        public static SaltWindHudPresenter Install(Canvas authoredCanvas, AuthoredHudPresenter authored, IRunViews views, IconCatalog icons, MinimapPresenter minimap = null)
        {
            var settings = SaltWindHudSettings.Load();
            if (Disabled && authoredCanvas != null) authoredCanvas.transform.Find("HUD_SaltWind")?.gameObject.SetActive(false);
            if (Disabled || settings == null || settings.hudPrefab == null || authoredCanvas == null) return null;
            var p = authoredCanvas.GetComponent<SaltWindHudPresenter>();
            if (p == null) p = authoredCanvas.gameObject.AddComponent<SaltWindHudPresenter>();
            p.Bind(settings, authored, views, icons);
            return p;
        }

        void Bind(SaltWindHudSettings settings, AuthoredHudPresenter authored, IRunViews views, IconCatalog icons)
        {
            _settings = settings;
            _authored = authored;
            _views = views;
            _icons = icons;
            // The HUD is placed in the run scene (Tools > Rogue Pirates > Demo > Build Demo Scenes) so artists can tweak it;
            // a scene without it gets one from the prefab.
            if (_hud == null) _hud = transform.Find("HUD_SaltWind")?.gameObject;
            if (_hud != null && _weapons.Count == 0) BindWidgets(_hud.transform);
            if (_hud == null)
            {
                _hud = Instantiate(settings.hudPrefab, transform, false);
                _hud.name = "HUD_SaltWind";
                var rt = (RectTransform)_hud.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
                _hud.transform.SetAsFirstSibling(); // under the boost meter and the Captain active
                BindWidgets(_hud.transform);
            }
            _lastCaptain = -1;
            if (_authored != null && _hud != null) _authored.WeaponRowForBoost = Find(_hud.transform, "WeaponLoadout") as RectTransform;
        }

        void BindWidgets(Transform root)
        {
            _hud = root.gameObject;
            _safeArea = Find(root, "SafeArea") as RectTransform;
            UpgradeSelection = Find(root, "UpgradeSelection");
            // The art's minimap is a frame: V4's minimap draws the map inside it (it follows ArtMinimap).
            var minimap = Find(root, "Minimap");
            SetActive(minimap, "MapContent", false);
            SetActive(minimap, "PlayerMarker", false);
            // V4's map sits in the frame's inner mask (the frame's compass points are outside it).
            ArtMinimap = (Find(minimap, "Mask") ?? minimap) as RectTransform;
            // Playtest (3 Oct, owner): no minimap for now (V4's map hides with it, see V4PresentationRoot.InstallHud).
            if (minimap != null) minimap.gameObject.SetActive(false);
            // The HUD is display only (the level-up cards keep their buttons).
            foreach (var g in root.GetComponentsInChildren<Graphic>(true))
                if (UpgradeSelection == null || !g.transform.IsChildOf(UpgradeSelection)) g.raycastTarget = false;
            foreach (var s in root.GetComponentsInChildren<Selectable>(true))
                if (UpgradeSelection == null || !s.transform.IsChildOf(UpgradeSelection)) s.interactable = false;

            var itemBar = Find(root, "ItemBar");
            if (itemBar != null) itemBar.gameObject.SetActive(false);

            var panel = Find(root, "PlayerPanel");
            _name = Find(panel, "Name")?.GetComponent<TextMeshProUGUI>();
            _portrait = Find(panel, "Portrait")?.GetComponent<Image>();
            _hp = Find(panel, "HealthBar")?.GetComponent<Slider>();
            _xp = Find(panel, "ExperienceBar")?.GetComponent<Slider>();
            foreach (var s in new[] { _hp, _xp }) if (s != null) { s.minValue = 0f; s.maxValue = 1f; s.wholeNumbers = false; s.transition = Selectable.Transition.None; }
            _time = Find(Find(root, "MatchTimer"), "Time")?.GetComponent<TextMeshProUGUI>();
            var currency = Find(root, "CurrencyBar");
            PlaceTopRight(currency, Find(root, "MatchTimer"));
            _gold = Find(Find(currency, "Gold"), "Amount")?.GetComponent<TextMeshProUGUI>();
            _kills = Find(Find(currency, "Kills"), "Amount")?.GetComponent<TextMeshProUGUI>();
            _rum = Find(Find(currency, "Rum"), "Amount")?.GetComponent<TextMeshProUGUI>();
            var title = Find(root, "MatchTitle");
            var markers = Find(title, "KillMarkers");
            if (markers != null)
                for (var i = 0; i < markers.childCount; i++)
                {
                    var img = markers.GetChild(i).GetComponent<Image>();
                    if (img != null) _markers.Add(img);
                }

            var loadout = Find(root, "WeaponLoadout");
            if (loadout != null)
            {
                var levelText = Find(loadout, "Level");
                _font = levelText != null && levelText.GetComponent<TextMeshProUGUI>() != null ? levelText.GetComponent<TextMeshProUGUI>().font : null;
                var logbookRow = Find(root, "LogbookLoadout");
                if (logbookRow == null)
                {
                    logbookRow = Instantiate(loadout.gameObject, loadout.parent, false).transform;
                    logbookRow.name = "LogbookLoadout";
                    // Playtest (3 Oct, owner): the Logbooks at the bottom left, mirroring the weapons at the bottom right.
                    var lrt = (RectTransform)logbookRow;
                    var wrt = (RectTransform)loadout;
                    lrt.anchorMin = lrt.anchorMax = Vector2.zero;
                    lrt.pivot = Vector2.zero;
                    lrt.anchoredPosition = new Vector2(-wrt.anchoredPosition.x, wrt.anchoredPosition.y);
                    var lgrid = logbookRow.GetComponent<GridLayoutGroup>();
                    if (lgrid != null) lgrid.childAlignment = TextAnchor.LowerLeft;
                }
                var itemRow = Find(root, "ItemLoadout");
                if (itemRow == null) itemRow = MakeItemRow(loadout);
                _itemGrid = itemRow.GetComponent<GridLayoutGroup>();
                BindRow(loadout, _weapons);
                BindRow(logbookRow, _logbooks);
                BindRow(itemRow, _itemSlots);
            }
        }

        /// <summary>Item slots made up front (two rows); items are uncapped, so more are added when a run holds more kinds.</summary>
        const int ItemSlots = 20;

        /// <summary>
        /// Playtest (3 Oct, owner): the items as weapon-style slots (icon or name, stack count in the badge) in a copy of the
        /// weapon row, centred at the bottom between the Logbooks and the weapons. Only filled slots show; the row wraps
        /// to a second line when the items outgrow the space between the two side rows.
        /// </summary>
        Transform MakeItemRow(Transform loadout)
        {
            var row = Instantiate(loadout.gameObject, loadout.parent, false).transform;
            row.name = "ItemLoadout";
            var rt = (RectTransform)row;
            var wrt = (RectTransform)loadout;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, wrt.anchoredPosition.y);
            var grid = row.GetComponent<GridLayoutGroup>();
            if (grid != null) grid.childAlignment = TextAnchor.LowerCenter;
            if (row.childCount == 0) return row;
            var template = row.GetChild(0).gameObject;
            for (var i = row.childCount; i < ItemSlots; i++) Instantiate(template, row, false);
            // Names that sort in order (BindRow sorts by name).
            for (var i = 0; i < row.childCount; i++) row.GetChild(i).name = $"ItemSlot_{i + 1:00}";
            return row;
        }

        /// <summary>The item row's columns: as many as fit between the Logbook and weapon rows (3 to 10).</summary>
        void FitItemRow()
        {
            if (_itemGrid == null || _safeArea == null) return;
            var width = _safeArea.rect.width;
            if (Mathf.Approximately(width, _itemRowFor)) return;
            _itemRowFor = width;
            var cell = _itemGrid.cellSize.x + _itemGrid.spacing.x;
            var side = _weapons.Count > 0 && _weapons[0].Root != null ? ((RectTransform)_weapons[0].Root.transform.parent).rect.width : 330f;
            var free = width - 2f * (side + 12f + 24f);
            _itemGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _itemGrid.constraintCount = Mathf.Clamp(Mathf.FloorToInt((free + _itemGrid.spacing.x) / Mathf.Max(1f, cell)), 3, 10);
        }

        /// <summary>
        /// Playtest (3 Oct, owner): coins, Rum and kills 50% larger in the top-right corner, the match timer just below
        /// them (it was top-left). Both leave the art's layout groups and are placed against the corner by hand, since a
        /// layout group ignores scale.
        /// </summary>
        static void PlaceTopRight(Transform currency, Transform timer)
        {
            const float scale = 1.5f;
            var bar = currency as RectTransform;
            if (bar == null) return;
            var corner = bar.parent;
            Unlayout(bar);
            bar.anchorMin = bar.anchorMax = bar.pivot = Vector2.one;
            bar.anchoredPosition = Vector2.zero;
            bar.localScale = new Vector3(scale, scale, 1f);
            var t = timer as RectTransform;
            if (t == null) return;
            t.SetParent(corner, false);
            Unlayout(t);
            t.anchorMin = t.anchorMax = t.pivot = Vector2.one;
            LayoutRebuilder.ForceRebuildLayoutImmediate(bar);
            t.anchoredPosition = new Vector2(0f, -(bar.rect.height * scale + 8f));
        }

        static void Unlayout(Transform t)
        {
            var e = t.GetComponent<LayoutElement>();
            if (e == null) e = t.gameObject.AddComponent<LayoutElement>();
            e.ignoreLayout = true;
        }

        void BindRow(Transform row, List<WeaponSlotView> into)
        {
            var slots = new List<Transform>();
            for (var i = 0; i < row.childCount; i++) slots.Add(row.GetChild(i));
            slots.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            foreach (var t in slots)
            {
                t.SetSiblingIndex(slots.IndexOf(t));
                into.Add(SlotView(t));
            }
        }

        WeaponSlotView SlotView(Transform t)
        {
            var content = Find(t, "Content");
            return new WeaponSlotView
            {
                Root = t.gameObject,
                Icon = Find(content, "Icon")?.GetComponent<Image>(),
                Empty = Find(content, "EmptyLabel")?.gameObject,
                Badge = Find(content, "LevelBadge")?.gameObject,
                Level = Find(Find(content, "LevelBadge"), "Level")?.GetComponent<TextMeshProUGUI>(),
                Locked = Find(t, "LockedOverlay")?.gameObject,
                Selected = Find(t, "SelectedFrame")?.gameObject,
                Name = NameLabel(content != null ? content : t, 15f),
            };
        }

        /// <summary>A centred name label in a slot (shown when the slot has no icon).</summary>
        TextMeshProUGUI NameLabel(Transform parent, float size)
        {
            var existing = parent.Find("NameLabel");
            var go = existing != null ? existing.gameObject : new GameObject("NameLabel", typeof(RectTransform));
            if (existing == null) go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(5f, 5f);
            rt.offsetMax = new Vector2(-5f, -5f);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (t == null) t = go.AddComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;
            t.fontSize = size;
            t.enableAutoSizing = true;
            t.fontSizeMin = 8f;
            t.fontSizeMax = size;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            t.outlineWidth = 0.2f;
            t.outlineColor = new Color32(0, 0, 0, 255);
            t.raycastTarget = false;
            t.text = string.Empty;
            go.SetActive(false);
            return t;
        }

        static void SetName(TextMeshProUGUI label, string name)
        {
            if (label == null) return;
            var show = !string.IsNullOrEmpty(name);
            if (label.gameObject.activeSelf != show) label.gameObject.SetActive(show);
            if (show && label.text != name) label.text = name;
        }

        void LateUpdate()
        {
            if (_hud == null || _views == null) return;
            // The replaced authored widgets stay hidden (the results screen re-shows the profile on retry).
            for (var i = 0; i < Replaced.Length; i++)
            {
                var t = transform.Find(Replaced[i]);
                if (t != null && t.gameObject.activeSelf) t.gameObject.SetActive(false);
            }
            if (_settings.hideBoostMeter)
            {
                var boost = transform.Find("Boost_Meter");
                if (boost != null && boost.gameObject.activeSelf) boost.gameObject.SetActive(false);
            }
            FitSafeArea();
            var show = _authored == null || _authored.ProfileVisible;
            if (_hud.activeSelf != show) _hud.SetActive(show);
            HideCompanions(!show);
            if (!show) return;

            var run = _views.Run;
            if (run != null && run.Exists)
            {
                var seconds = Mathf.FloorToInt(run.ElapsedSeconds);
                if (_time != null && (seconds != _shownSeconds || run.IsOvertime != _shownOvertime))
                {
                    _shownSeconds = seconds;
                    _shownOvertime = run.IsOvertime;
                    _time.text = $"{seconds / 60:00}:{seconds % 60:00}";
                    _time.color = run.IsOvertime ? RunHudTheme.TimerOvertime : Color.white;
                }
                if (_gold != null && run.Gold != _shownGold) { _shownGold = run.Gold; _gold.text = run.Gold.ToString("N0"); }
                if (_kills != null && run.Kills != _shownKills) { _shownKills = run.Kills; _kills.text = run.Kills.ToString("N0"); }
                // Rum: the player's treasury (this run's Rum is settled on the results screen).
                var app = RoguePirates.V4.Foundation.Core.AppRoot.Instance;
                var rum = app != null && app.Save != null ? app.Save.Meta.marks : 0;
                if (_rum != null && rum != _shownRum) { _shownRum = rum; _rum.text = rum.ToString("N0"); }
            }

            var captain = _views.Captain;
            var progression = _views.Progression;
            if (captain != null && _name != null)
            {
                var level = progression != null && progression.Exists ? Mathf.Max(1, progression.Level) : 1;
                if (level != _shownLevel || captain.CaptainId != _lastCaptain)
                {
                    _shownLevel = level;
                    _name.text = $"{captain.DisplayTitle}  <size=80%>Lv {level}</size>";
                }
                if (_portrait != null && captain.CaptainId != _lastCaptain)
                {
                    _lastCaptain = captain.CaptainId;
                    var sprite = _settings.PortraitOf(captain.CaptainId);
                    if (sprite != null) { _portrait.sprite = sprite; _portrait.preserveAspect = true; }
                }
            }
            if (progression != null && progression.Exists && _xp != null)
                _xp.value = Mathf.Clamp01(progression.CurrentXp / Mathf.Max(1f, progression.XpToNext));
            var ship = _views.Ship;
            if (ship != null && ship.Exists && _hp != null) _hp.value = Mathf.Clamp01(ship.Hp / Mathf.Max(1f, ship.MaxHp));

            RefreshObjective();
            RefreshWeapons();
            RefreshLogbooks();
            RefreshItems();
        }

        // Playtest (3 Oct, owner): the widgets around the art HUD (boost meter, Captain active, touch buttons, item popup)
        // hide with it on the results screen; they stayed on the sea alone and the end screen looked broken.
        static readonly string[] Companions = { "Boost_Meter", "V4CaptainActive", "V4CaptainActiveEffect", "V4CaptainAbilityButton", "V4ItemReward", "V5TouchControls" };
        readonly List<GameObject> _hiddenCompanions = new List<GameObject>();
        bool _companionsHidden;

        void HideCompanions(bool hide)
        {
            if (hide == _companionsHidden) return;
            _companionsHidden = hide;
            if (!hide)
            {
                foreach (var go in _hiddenCompanions) if (go != null) go.SetActive(true);
                _hiddenCompanions.Clear();
                return;
            }
            foreach (var name in Companions)
            {
                var t = transform.Find(name);
                if (t == null || !t.gameObject.activeSelf) continue;
                t.gameObject.SetActive(false);
                _hiddenCompanions.Add(t.gameObject);
            }
        }

        readonly int[] _shownLogbookRanks = new int[8];

        void RefreshLogbooks()
        {
            var progression = _views.Progression;
            var count = progression != null && progression.Exists ? progression.LogbookCount : 0;
            for (var i = 0; i < _logbooks.Count; i++)
            {
                var w = _logbooks[i];
                var filled = i < count;
                if (w.Locked != null) w.Locked.SetActive(false);
                if (w.Selected != null) w.Selected.SetActive(false);
                if (w.Empty != null) w.Empty.SetActive(!filled);
                if (w.Badge != null) w.Badge.SetActive(filled);
                var id = filled ? progression.LogbookId(i) : null;
                var sprite = filled && _icons != null ? _icons.Logbook(id) : null;
                if (w.Icon != null)
                {
                    w.Icon.enabled = sprite != null;
                    if (sprite != null && w.Icon.sprite != sprite) { w.Icon.sprite = sprite; w.Icon.preserveAspect = true; }
                }
                SetName(w.Name, filled && sprite == null ? progression.LogbookName(i) : null);
                if (filled && w.Level != null)
                {
                    var rank = progression.LogbookRank(i);
                    if (i >= _shownLogbookRanks.Length || _shownLogbookRanks[i] != rank)
                    {
                        if (i < _shownLogbookRanks.Length) _shownLogbookRanks[i] = rank;
                        w.Level.text = rank.ToString();
                    }
                }
            }
        }

        readonly int[] _shownItemStacks = new int[ItemSlots];

        void RefreshItems()
        {
            var loadout = _views.Loadout;
            if (loadout == null) return;
            FitItemRow();
            while (_itemSlots.Count < loadout.ItemCount && _itemSlots.Count > 0)
            {
                var first = _itemSlots[0].Root.transform;
                var slot = Instantiate(_itemSlots[0].Root, first.parent, false).transform;
                slot.name = $"ItemSlot_{_itemSlots.Count + 1:00}";
                _itemSlots.Add(SlotView(slot));
            }
            for (var i = 0; i < _itemSlots.Count; i++)
            {
                var w = _itemSlots[i];
                var filled = i < loadout.ItemCount;
                if (w.Root.activeSelf != filled) w.Root.SetActive(filled);
                if (!filled) continue;
                if (w.Locked != null) w.Locked.SetActive(false);
                if (w.Selected != null) w.Selected.SetActive(false);
                if (w.Empty != null) w.Empty.SetActive(false);
                if (w.Badge != null) w.Badge.SetActive(true);
                var sprite = _icons != null ? _icons.Item(loadout.ItemId(i)) : null;
                if (w.Icon != null)
                {
                    w.Icon.enabled = sprite != null;
                    if (sprite != null && w.Icon.sprite != sprite) { w.Icon.sprite = sprite; w.Icon.preserveAspect = true; }
                }
                SetName(w.Name, sprite == null ? loadout.ItemName(i) : null);
                var stacks = loadout.ItemStacks(i);
                if (w.Level != null && (i >= _shownItemStacks.Length || _shownItemStacks[i] != stacks || w.Level.text.Length == 0))
                {
                    if (i < _shownItemStacks.Length) _shownItemStacks[i] = stacks;
                    w.Level.text = stacks.ToString();
                }
            }
        }

        void RefreshObjective()
        {
            var stage = _views.Stage;
            if (stage == null) return;
            var done = stage.BossDefeated ? 3 : stage.BossSummoned ? 2 : stage.Beacon == BeaconReveal.Discovered ? 1 : 0;
            if (done == _shownDone) return;
            _shownDone = done;
            for (var i = 0; i < _markers.Count; i++)
            {
                var sprite = i < done ? _settings.markerDone : _settings.markerOpen;
                if (sprite != null && _markers[i].sprite != sprite) _markers[i].sprite = sprite;
            }
        }

        void RefreshWeapons()
        {
            var loadout = _views.Loadout;
            if (loadout == null) return;
            var cap = loadout.WeaponSlotCap;
            for (var i = 0; i < _weapons.Count; i++)
            {
                var w = _weapons[i];
                var open = i < cap;
                var filled = open && i < loadout.WeaponCount;
                if (w.Locked != null) w.Locked.SetActive(!open);
                if (w.Selected != null) w.Selected.SetActive(false);
                if (w.Empty != null) w.Empty.SetActive(open && !filled);
                if (w.Badge != null) w.Badge.SetActive(filled);
                if (w.Icon != null)
                {
                    var sprite = filled && _icons != null ? _icons.Weapon(loadout.WeaponId(i)) : null;
                    w.Icon.enabled = sprite != null;
                    if (sprite != null && w.Icon.sprite != sprite) { w.Icon.sprite = sprite; w.Icon.preserveAspect = true; }
                    SetName(w.Name, filled && sprite == null ? loadout.WeaponName(i) : null);
                }
                if (filled && w.Level != null)
                {
                    var lv = loadout.WeaponLevel(i);
                    if (i >= _shownWeaponLevels.Length || _shownWeaponLevels[i] != lv)
                    {
                        if (i < _shownWeaponLevels.Length) _shownWeaponLevels[i] = lv;
                        w.Level.text = lv.ToString();
                    }
                }
            }
        }

        static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = Find(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        static void SetActive(Transform root, string name, bool active)
        {
            var t = Find(root, name);
            if (t != null) t.gameObject.SetActive(active);
        }
    }
}
