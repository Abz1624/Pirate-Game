using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.Hackathon.UI
{
    /// <summary>
    /// Hackathon (12 Oct): "Design defences" for the player's island. The island is a 15-minute level; the designer edits
    /// it one minute at a time and saves the whole design (PUT /api/islands/{id}/defence).
    /// 13 Oct: redrawn in the art team's parchment kit (<see cref="HackSkin"/>) after the owner's reference: a header with
    /// Test defences / Save / Close, a preset row (Swarm, Gunline, Elite guard and the preset's line), then three columns:
    /// the wave timeline (all 15 minutes with used / budget points, the guardian's minute marked), the selected minute
    /// (budget bar and its enemies with - / +; Add enemy lists every kind by role) and a summary (guardian card with its
    /// arrival minute, raid pressure, enemy style, total points, Add enemy, Duplicate previous minute, Clear minute).
    /// Over-budget minutes show red as you edit; the server's errors show in the minute column and the status strip.
    /// </summary>
    public sealed class DefencePanel : MonoBehaviour
    {
        // Layout (canvas units; the canvas scales from 1920 x 1080).
        const float HeaderHeight = 200f;
        const float StatusHeight = 50f;
        const float TimelineRow = 66f;
        const float EnemyRow = 96f;

        TextMeshProUGUI _title, _status, _presetLine;
        RectTransform _presetButtons;
        Button _save, _close, _test, _add, _duplicate, _clear, _earlier, _later;
        TextMeshProUGUI _addLabel;
        RectTransform _timeline, _enemies;
        ScrollRect _timelineScroll, _enemiesScroll;
        TextMeshProUGUI _minuteTitle, _minutePoints, _enemiesTitle;
        Image _barFill;
        TextMeshProUGUI _guardianName, _guardianArrival, _pressure, _style, _totals;

        bool _dirty;
        DefenceCatalog _catalog;
        IslandDefenceView _view;
        DefenceDesign _design;
        string _islandId, _islandName;
        int _selected = 1;
        bool _picking;
        bool _busy;
        readonly HashSet<string> _touched = new HashSet<string>();
        readonly Dictionary<string, int> _costs = new Dictionary<string, int>();
        readonly Dictionary<string, DefenceKind> _kinds = new Dictionary<string, DefenceKind>();
        readonly Dictionary<string, DefenceDesign> _presetDesigns = new Dictionary<string, DefenceDesign>();
        int[] _budgets = new int[DefenceRules.Windows];
        string _problems;

        public bool IsOpen => gameObject.activeSelf;
        public DefenceDesign Design => _design;
        public string StatusText => _status != null ? _status.text : null;
        public bool Busy => _busy;
        public string Problems => _problems;
        /// <summary>The minute shown in the middle column (1..15).</summary>
        public int Selected => _selected;
        /// <summary>True while the middle column lists every enemy kind (Add enemy).</summary>
        public bool Picking => _picking;

        public static DefencePanel Create(Transform canvas)
        {
            var rt = HackUi.Panel(canvas, "HackathonDefence", new Vector2(0.015f, 0.025f), new Vector2(0.985f, 0.975f), 229);
            // Dark under the parchment pieces: the Islands screen (or a lobby) behind must not read through the gaps.
            rt.GetComponent<Image>().color = new Color(0.02f, 0.06f, 0.1f, 0.92f);
            var panel = rt.gameObject.AddComponent<DefencePanel>();
            panel.Build(rt);
            return panel;
        }

        // ================================================================ skin helpers

        static Color Ink => HackSkin.I.ink;
        static Color InkDim => HackSkin.I.inkDim;
        static Color InkGold => HackSkin.I.inkGold;
        static Color InkGood => HackSkin.I.inkGood;
        static Color InkBad => HackSkin.I.inkBad;
        static Color OnWood => HackSkin.I.onWood;

        static TMP_FontAsset Font => HackSkin.I.font != null ? HackSkin.I.font : HackUi.TitleFont;

        static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>A child at (<paramref name="x"/>, <paramref name="y"/>) from its parent's top-left, <paramref name="w"/> x <paramref name="h"/>.</summary>
        static RectTransform TopLeft(Transform parent, string name, float x, float y, float w, float h)
        {
            var rt = Node(parent, name);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>A child at <paramref name="right"/> from its parent's right edge and <paramref name="y"/> from the top.</summary>
        static RectTransform TopRight(Transform parent, string name, float right, float y, float w, float h)
        {
            var rt = Node(parent, name);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-right, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>A child stretched across its parent's width, <paramref name="h"/> tall at <paramref name="y"/> from the top.</summary>
        static RectTransform Strip(Transform parent, string name, float left, float y, float right, float h)
        {
            var rt = Node(parent, name);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(left, -y - h);
            rt.offsetMax = new Vector2(-right, -y);
            return rt;
        }

        /// <summary>A child stretched across its parent's width, <paramref name="h"/> tall at <paramref name="bottom"/> from the bottom.</summary>
        static RectTransform BottomStrip(Transform parent, string name, float left, float bottom, float right, float h)
        {
            var rt = Node(parent, name);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, bottom + h);
            return rt;
        }

        /// <summary>A child filling its parent minus insets (left, bottom, right, top).</summary>
        static RectTransform Fill(Transform parent, string name, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            var rt = Node(parent, name);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Kit sprite <paramref name="sprite"/> on <paramref name="rt"/> (9-sliced when it has borders; <paramref name="thin"/>
        /// above 1 draws the border art smaller). Without the sprite, <paramref name="fallback"/> fills the rect.</summary>
        static Image Paint(RectTransform rt, string sprite, float thin = 1f, Color? tint = null, Color? fallback = null)
        {
            var img = rt.GetComponent<Image>();
            if (img == null) img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            var s = HackSkin.S(sprite);
            if (s != null)
            {
                img.sprite = s;
                img.type = s.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
                img.pixelsPerUnitMultiplier = thin;
                img.preserveAspect = img.type == Image.Type.Simple;
                img.color = tint ?? Color.white;
            }
            else img.color = fallback ?? tint ?? new Color(0.9f, 0.8f, 0.6f, 1f);
            return img;
        }

        static Image Icon(Transform parent, string sprite, float x, float y, float size, Color? tint = null)
        {
            var img = Paint(TopLeft(parent, sprite, x, y, size, size), sprite, 1f, tint ?? Ink, new Color(0f, 0f, 0f, 0f));
            img.preserveAspect = true;
            return img;
        }

        static TextMeshProUGUI Label(RectTransform rt, string text, float size, Color color, TextAlignmentOptions align, bool bold = false, float minSize = 0f)
        {
            // A rect that already draws (a kit sprite) cannot take a text as well: the text goes on a child filling it.
            if (rt.GetComponent<Graphic>() != null) rt = Fill(rt, "Text");
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null) t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            if (minSize > 0f)
            {
                t.enableAutoSizing = true;
                t.fontSizeMin = minSize;
                t.fontSizeMax = size;
            }
            return t;
        }

        static void Line(Transform parent, float left, float y, float right)
        {
            var img = Strip(parent, "Line", left, y, right, 2f).gameObject.AddComponent<Image>();
            img.color = new Color(0.45f, 0.3f, 0.15f, 0.35f);
            img.raycastTarget = false;
        }

        /// <summary>A kit button filling <paramref name="rt"/>: <paramref name="style"/> is Btn_Gold, Btn_Parchment or
        /// Btn_Square_Gold; an icon (kit name) sits left of the label; the label may be empty (icon only).</summary>
        static Button KitButton(RectTransform rt, string label, string icon, Action click, string style = "Btn_Gold", float size = 30f, float iconSize = 40f, float thin = 1f)
        {
            var img = Paint(rt, style, thin, null, new Color(0.95f, 0.75f, 0.3f, 1f));
            img.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var pressed = HackSkin.S(style == "Btn_Gold" ? "Btn_Gold_Pressed" : null);
            var disabled = HackSkin.S(style == "Btn_Gold" ? "Btn_Gold_Disabled" : style == "Btn_Square_Gold" ? "Btn_Square_Disabled" : null);
            if (img.sprite != null && (pressed != null || disabled != null))
            {
                b.transition = Selectable.Transition.SpriteSwap;
                b.spriteState = new SpriteState { pressedSprite = pressed != null ? pressed : img.sprite, highlightedSprite = img.sprite, selectedSprite = img.sprite, disabledSprite = disabled };
            }
            else
            {
                var c = b.colors;
                c.pressedColor = new Color(0.85f, 0.8f, 0.7f, 1f);
                c.disabledColor = new Color(1f, 1f, 1f, 0.5f);
                b.colors = c;
            }
            if (click != null) b.onClick.AddListener(() => click());

            var content = Fill(rt, "Content", 10f, 4f, 10f, 2f);
            var h = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = 12f;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            if (!string.IsNullOrEmpty(icon) && HackSkin.S(icon) != null)
            {
                var ic = Node(content, "Icon");
                var iimg = Paint(ic, icon, 1f, Ink);
                iimg.preserveAspect = true;
                var le = ic.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = le.minWidth = le.preferredHeight = le.minHeight = iconSize;
            }
            if (!string.IsNullOrEmpty(label))
            {
                var t = Label(Node(content, "Label"), label, size, Ink, TextAlignmentOptions.MidlineLeft, true);
                t.textWrappingMode = TextWrappingModes.Normal;
                t.lineSpacing = -12f;
            }
            return b;
        }

        static TextMeshProUGUI ButtonLabel(Button b) => b != null ? b.GetComponentInChildren<TextMeshProUGUI>(true) : null;

        /// <summary>A vertical scroll list in <paramref name="host"/> (kit scroll bar on the right); returns the content.</summary>
        static RectTransform Scroll(RectTransform host, out ScrollRect sr, float spacing, RectOffset padding)
        {
            var sr0 = host.gameObject.AddComponent<ScrollRect>();
            var viewport = Fill(host, "Viewport", 0f, 0f, 24f, 0f);
            var vimg = viewport.gameObject.AddComponent<Image>();
            vimg.color = new Color(0f, 0f, 0f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Node(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding;
            v.childControlHeight = v.childControlWidth = true;
            v.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var bar = Node(host, "Scrollbar");
            bar.anchorMin = new Vector2(1f, 0f);
            bar.anchorMax = Vector2.one;
            bar.pivot = new Vector2(1f, 0.5f);
            bar.sizeDelta = new Vector2(14f, 0f);
            bar.anchoredPosition = Vector2.zero;
            Paint(bar, "Scroll_Track", 1f, new Color(0.55f, 0.4f, 0.25f, 0.55f), new Color(0.4f, 0.28f, 0.16f, 0.4f)).raycastTarget = true;
            var area = Fill(bar, "Sliding Area");
            var handle = Fill(area, "Handle");
            var himg = Paint(handle, "Scroll_Thumb", 1f, null, new Color(0.95f, 0.72f, 0.25f, 1f));
            himg.raycastTarget = true;
            var sb = bar.gameObject.AddComponent<Scrollbar>();
            sb.handleRect = handle;
            sb.targetGraphic = himg;
            sb.direction = Scrollbar.Direction.BottomToTop;

            sr0.content = content;
            sr0.viewport = viewport;
            sr0.horizontal = false;
            sr0.movementType = ScrollRect.MovementType.Clamped;
            sr0.scrollSensitivity = 30f;
            sr0.verticalScrollbar = sb;
            sr0.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            sr = sr0;
            return content;
        }

        static RectTransform ListRow(Transform list, string name, float height, string sprite, float thin, Color? tint = null)
        {
            var rt = Node(list, name);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = height;
            if (sprite != null) Paint(rt, sprite, thin, tint, tint ?? new Color(0.95f, 0.88f, 0.72f, 1f));
            return rt;
        }

        static void Clear(Transform list)
        {
            for (var i = list.childCount - 1; i >= 0; i--)
            {
                var c = list.GetChild(i).gameObject;
                c.SetActive(false);
                Destroy(c);
            }
        }

        // ================================================================ build

        void Build(RectTransform rt)
        {
            BuildHeader(rt);
            var body = Fill(rt, "Body", 0f, StatusHeight + 6f, 0f, HeaderHeight + 10f);
            BuildTimeline(HackUiColumn(body, "Timeline", 0f, 0.285f));
            BuildMinute(HackUiColumn(body, "Minute", 0.293f, 0.707f));
            BuildSummary(HackUiColumn(body, "Summary", 0.715f, 1f));
            BuildStatus(rt);
        }

        static RectTransform HackUiColumn(Transform body, string name, float x0, float x1)
        {
            var rt = Node(body, name);
            rt.anchorMin = new Vector2(x0, 0f);
            rt.anchorMax = new Vector2(x1, 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            Paint(rt, "Panel_Card", 1.2f);
            return rt;
        }

        void BuildHeader(RectTransform rt)
        {
            var head = Strip(rt, "Header", 0f, 0f, 0f, HeaderHeight);
            Paint(head, "Panel_Card", 1.2f);

            // Medallion with the skull, poking over the corner as in the reference.
            var medal = TopLeft(head, "Medallion", -14f, -14f, 150f, 150f);
            var mimg = Paint(medal, "Medallion_Frame", 1f, null, new Color(0f, 0f, 0f, 0f));
            mimg.preserveAspect = true;
            // The kit's glyphs are dark ink, so the medallion's dark centre gets a parchment disc under the skull.
            if (mimg.sprite != null)
            {
                var disc = Fill(medal, "Disc", 34f, 34f, 34f, 34f).gameObject.AddComponent<Image>();
                disc.sprite = IslandUi.Circle;
                disc.color = new Color(0.96f, 0.88f, 0.7f, 1f);
                disc.raycastTarget = false;
            }
            var glyph = Paint(Fill(medal, "Skull", 44f, 46f, 44f, 42f), "Icon_Skull_Crossbones", 1f, Ink, new Color(0f, 0f, 0f, 0f));
            glyph.preserveAspect = true;

            // Title, then Test defences / Save / Close on the right.
            var titleRt = Strip(head, "Title", 150f, 10f, 840f, 96f);
            _title = Label(titleRt, "Design defences", 60f, Ink, TextAlignmentOptions.MidlineLeft, true, 34f);
            _close = KitButton(TopRight(head, "Close", 22f, 18f, 220f, 82f), "Close", "Icon_Close", Close, "Btn_Gold", 32f, 40f);
            _save = KitButton(TopRight(head, "Save", 256f, 18f, 220f, 82f), "Save", "Icon_Save", Save, "Btn_Gold", 32f, 40f);
            _test = KitButton(TopRight(head, "Test defences", 490f, 18f, 320f, 82f), "Test defences", "Icon_Play", TestDefences, "Btn_Gold", 32f, 40f);

            Line(head, 30f, 114f, 30f);

            // Preset row.
            Label(TopLeft(head, "PresetLabel", 40f, 124f, 130f, 66f), "Preset:", 30f, Ink, TextAlignmentOptions.MidlineLeft, true);
            _presetButtons = TopLeft(head, "Presets", 170f, 124f, 740f, 66f);
            var h = _presetButtons.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 14f;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            var divider = TopLeft(head, "Divider", 928f, 132f, 3f, 50f).gameObject.AddComponent<Image>();
            divider.color = new Color(0.45f, 0.3f, 0.15f, 0.5f);
            divider.raycastTarget = false;
            _presetLine = Label(Strip(head, "PresetLine", 950f, 122f, 34f, 70f), string.Empty, 27f, Ink, TextAlignmentOptions.MidlineLeft, false, 18f);
            _presetLine.textWrappingMode = TextWrappingModes.Normal;
        }

        void BuildTimeline(RectTransform col)
        {
            Icon(col, "Icon_Clock", 26f, 24f, 74f);
            Label(Strip(col, "Title", 114f, 20f, 16f, 50f), "Wave timeline", 42f, Ink, TextAlignmentOptions.MidlineLeft, true, 28f);
            Label(Strip(col, "Subtitle", 114f, 68f, 16f, 32f), "Configure each minute of the raid.", 23f, InkDim, TextAlignmentOptions.MidlineLeft, false, 16f);
            Line(col, 26f, 110f, 26f);
            var host = Fill(col, "List", 16f, 22f, 14f, 120f);
            _timeline = Scroll(host, out _timelineScroll, 8f, new RectOffset(4, 30, 4, 6));
        }

        void BuildMinute(RectTransform col)
        {
            var top = Strip(col, "Top", 16f, 14f, 16f, 116f);
            Paint(top, "Panel_Inset", 1.6f);
            _minuteTitle = Label(Strip(top, "Minute", 26f, 6f, 300f, 64f), "Minute 1", 50f, Ink, TextAlignmentOptions.MidlineLeft, true, 30f);
            _minutePoints = Label(TopRight(top, "Points", 26f, 10f, 300f, 56f), string.Empty, 32f, Ink, TextAlignmentOptions.MidlineRight, true, 20f);
            var track = Strip(top, "Bar", 26f, 74f, 26f, 30f);
            Paint(track, "Bar_Track", 1f, null, new Color(0.25f, 0.15f, 0.08f, 1f));
            var fill = Fill(track, "Fill", 4f, 4f, 4f, 4f);
            _barFill = Paint(fill, "Bar_Fill_Green", 1f, null, new Color(0.3f, 0.7f, 0.3f, 1f));

            _enemiesTitle = Label(Strip(col, "EnemiesTitle", 36f, 140f, 36f, 46f), "Enemies in this wave", 32f, Ink, TextAlignmentOptions.MidlineLeft, true, 22f);
            var host = Fill(col, "List", 20f, 22f, 16f, 192f);
            _enemies = Scroll(host, out _enemiesScroll, 8f, new RectOffset(2, 8, 2, 6));
        }

        void BuildSummary(RectTransform col)
        {
            Icon(col, "Icon_Flag", 30f, 22f, 70f);
            Label(Strip(col, "Title", 112f, 24f, 16f, 60f), "Summary", 44f, Ink, TextAlignmentOptions.MidlineLeft, true, 28f);
            Line(col, 26f, 100f, 26f);

            // Guardian card.
            var card = Strip(col, "Guardian", 18f, 112f, 18f, 140f);
            Paint(card, "Panel_Inset", 1.6f);
            var portrait = TopLeft(card, "Portrait", 14f, 14f, 112f, 112f);
            Paint(portrait, "Slot_Card", 2.4f, new Color(0.62f, 0.47f, 0.33f, 1f), new Color(0.55f, 0.4f, 0.28f, 1f));
            Paint(Fill(portrait, "Crown", 34f, 64f, 34f, 6f), "Crown_Gold", 1f, null, new Color(0f, 0f, 0f, 0f)).preserveAspect = true;
            Paint(Fill(portrait, "Skull", 18f, 10f, 18f, 30f), "Icon_Skull_Crossbones", 1f, OnWood, new Color(0f, 0f, 0f, 0f)).preserveAspect = true;
            _guardianName = Label(Strip(card, "Name", 140f, 12f, 12f, 40f), "Guardian", 27f, Ink, TextAlignmentOptions.MidlineLeft, true, 18f);
            _guardianArrival = Label(Strip(card, "Arrival", 140f, 50f, 12f, 32f), string.Empty, 23f, InkDim, TextAlignmentOptions.MidlineLeft, false, 16f);
            _earlier = KitButton(TopLeft(card, "Earlier", 140f, 86f, 48f, 44f), null, "Icon_Chevron_Left", () => GuardianMinute(-1), "Btn_Square_Gold", 24f, 26f, 3f);
            _later = KitButton(TopLeft(card, "Later", 196f, 86f, 48f, 44f), null, "Icon_Chevron_Right", () => GuardianMinute(1), "Btn_Square_Gold", 24f, 26f, 3f);
            Label(Strip(card, "Move", 254f, 86f, 12f, 44f), "Change minute", 21f, InkDim, TextAlignmentOptions.MidlineLeft, false, 14f);

            // Stats.
            _pressure = Stat(col, 266f, "Icon_Swords", "Total raid pressure");
            _style = Stat(col, 348f, "Icon_Group", "Enemy style");
            _totals = Stat(col, 430f, "Chest_2", "Total points used");
            Line(col, 40f, 344f, 40f);
            Line(col, 40f, 426f, 40f);

            // Actions.
            var small = BottomStrip(col, "Small", 18f, 20f, 18f, 82f);
            _duplicate = KitButton(Half(small, 0f, 0.5f, 0f, 6f), "Duplicate\nprevious minute", "Icon_Copy", DuplicatePreviousMinute, "Btn_Parchment", 21f, 38f);
            _clear = KitButton(Half(small, 0.5f, 1f, 6f, 0f), "Clear minute", "Icon_Trash", ClearMinute, "Btn_Parchment", 22f, 38f);
            _add = KitButton(BottomStrip(col, "AddEnemy", 18f, 114f, 18f, 92f), "Add enemy", "Icon_Plus", TogglePicker, "Btn_Gold", 36f, 46f);
            _addLabel = ButtonLabel(_add);
        }

        static RectTransform Half(Transform parent, float x0, float x1, float padLeft, float padRight)
        {
            var rt = Node(parent, "Half");
            rt.anchorMin = new Vector2(x0, 0f);
            rt.anchorMax = new Vector2(x1, 1f);
            rt.offsetMin = new Vector2(padLeft, 0f);
            rt.offsetMax = new Vector2(-padRight, 0f);
            return rt;
        }

        static TextMeshProUGUI Stat(RectTransform col, float y, string icon, string label)
        {
            var row = Strip(col, label, 30f, y, 20f, 78f);
            var ic = Paint(TopLeft(row, "Icon", 6f, 10f, 60f, 60f), icon, 1f, icon.StartsWith("Icon_") ? Ink : Color.white, new Color(0f, 0f, 0f, 0f));
            ic.preserveAspect = true;
            Label(Strip(row, "Label", 92f, 6f, 0f, 32f), label, 24f, Ink, TextAlignmentOptions.MidlineLeft, false, 16f);
            return Label(Strip(row, "Value", 92f, 38f, 0f, 36f), string.Empty, 28f, InkGold, TextAlignmentOptions.MidlineLeft, true, 18f);
        }

        void BuildStatus(RectTransform rt)
        {
            var strip = BottomStrip(rt, "Status", 6f, 0f, 6f, StatusHeight);
            Paint(strip, "Header_Wood", 2f, null, new Color(0.32f, 0.2f, 0.1f, 0.95f));
            // A parchment slip in the wood frame with ink text (light text on the wood read poorly on the phone).
            var paper = Fill(strip, "Paper", 10f, 6f, 10f, 6f);
            Paint(paper, "Panel_Inset", 1.6f, null, new Color(0.96f, 0.89f, 0.74f, 1f));
            var tint = Fill(paper, "Tint", 4f, 3f, 4f, 3f).gameObject.AddComponent<Image>();
            tint.raycastTarget = false;
            _status = Label(Fill(paper, "Text", 20f, 2f, 20f, 2f), string.Empty, 25f, Ink, TextAlignmentOptions.MidlineLeft, false, 16f);
            _status.textWrappingMode = TextWrappingModes.Normal;
            var hs = _status.gameObject.AddComponent<HackStatus>();
            hs.Background = tint;
            hs.Parchment = true;
        }

        // ================================================================ load, save, close

        public async void Open(string islandId, string islandName)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _islandId = islandId;
            _islandName = islandName;
            _title.text = $"Defences of {islandName}";
            _design = null;
            _selected = 1;
            _picking = false;
            _touched.Clear();
            _problems = null;
            Render();
            Status("Loading the design...", HackUi.Dim);
            _busy = true;
            RefreshButtons();
            try
            {
                if (_catalog == null)
                {
                    var (catalog, cerr) = await IslandApi.DefenceCatalog();
                    if (this == null) return;
                    if (cerr != null) { Status(cerr, HackUi.Bad); return; }
                    _catalog = catalog;
                    _presetDesigns.Clear();
                }
                var (view, error) = await IslandApi.Defence(islandId);
                if (this == null) return;
                if (error != null) { Status(error, HackUi.Bad); return; }
                Apply(view);
                Status(view.isCustom ? "Your saved design. Edit a minute, then Save." : "Default design. Pick a preset or edit a minute, then Save.", HackUi.Dim);
            }
            finally
            {
                _busy = false;
                if (this != null) RefreshButtons();
            }
        }

        void Apply(IslandDefenceView view)
        {
            _view = view;
            _costs.Clear();
            _kinds.Clear();
            var kinds = view.kinds != null && view.kinds.Length > 0 ? view.kinds : _catalog?.kinds;
            if (kinds != null) foreach (var k in kinds) if (k != null) { _costs[k.kind] = k.cost; _kinds[k.kind] = k; }
            _budgets = view.budgets != null && view.budgets.Length >= DefenceRules.Windows ? view.budgets : _budgets;
            _design = DefenceRules.Normalize(view.design, _catalog?.defaultGuardian ?? "barnacle_king");
            _dirty = false;
            Render();
        }

        /// <summary>Called after Close (the Islands screen reopens).</summary>
        public Action Closed;
        /// <summary>"Test defences": starts a practice raid on (islandId, islandName) with the saved design.</summary>
        public Action<string, string> Practice;
        public bool Dirty => _dirty;

        /// <summary>Saves unsaved changes first (a practice raid uses the saved design), then starts the practice raid.</summary>
        public async void TestDefences()
        {
            if (_design == null || _busy) return;
            if (_dirty && !await SaveNow()) return;
            if (this == null) return;
            var id = _islandId;
            var name = _islandName;
            Close();
            Practice?.Invoke(id, name);
        }

        public void Close()
        {
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        void Status(string text, Color color)
        {
            _status.text = text;
            // The callers pass the on-wood colours; the slip is parchment, so the skin's inks of the same meaning.
            var skin = HackSkin.I;
            _status.color = color == HackUi.Bad ? skin.inkBad : color == HackUi.Good ? skin.inkGood : color == HackUi.Gold ? skin.inkGold : Ink;
        }

        void RefreshButtons()
        {
            var ready = _design != null && !_busy;
            if (_save != null) _save.interactable = ready;
            if (_test != null) _test.interactable = ready;
        }

        public async void Save() => await SaveNow();

        async System.Threading.Tasks.Task<bool> SaveNow()
        {
            if (_design == null || _busy) return false;
            _busy = true;
            RefreshButtons();
            Status("Saving the design...", HackUi.Gold);
            try
            {
                var (view, error, problems) = await IslandApi.SaveDefence(_islandId, DefenceRules.ForSave(_design));
                if (this == null) return false;
                if (error != null)
                {
                    if (problems != null && problems.Length > 0)
                    {
                        var sb = new StringBuilder("<b>Not saved:</b>");
                        foreach (var p in problems) sb.Append("\n").Append(DefenceRules.Problem(p));
                        _problems = sb.ToString();
                    }
                    Status(error + (problems != null && problems.Length > 0 ? $" {problems.Length} problem{(problems.Length == 1 ? string.Empty : "s")} listed in the minute column." : string.Empty), HackUi.Bad);
                    Render();
                    return false;
                }
                _problems = null;
                Apply(view);
                Status($"Saved. Raiders of {_islandName} now face this design. Press Test defences to try it.", HackUi.Good);
                return true;
            }
            finally
            {
                _busy = false;
                if (this != null) RefreshButtons();
            }
        }

        // ================================================================ numbers

        public int Budget(int minute) => minute >= 1 && minute <= _budgets.Length ? _budgets[minute - 1] : 0;
        public int Cost(int minute) => _design != null ? DefenceRules.Cost(_design.windows[minute - 1], _costs) : 0;

        public int OverBudgetWindows()
        {
            var n = 0;
            if (_design == null) return 0;
            for (var m = 1; m <= DefenceRules.Windows; m++) if (Cost(m) > Budget(m)) n++;
            return n;
        }

        public int TotalCost()
        {
            var n = 0;
            if (_design != null) for (var m = 1; m <= DefenceRules.Windows; m++) n += Cost(m);
            return n;
        }

        public int TotalBudget()
        {
            var n = 0;
            for (var m = 1; m <= DefenceRules.Windows; m++) n += Budget(m);
            return n;
        }

        /// <summary>Low / Medium / High from the share of the whole budget in use ("Over budget" past it).</summary>
        public static string Pressure(int used, int budget)
        {
            if (budget <= 0 || used <= 0) return "None";
            var r = used / (float)budget;
            return r > 1f ? "Over budget" : r < 0.4f ? "Low" : r < 0.75f ? "Medium" : "High";
        }

        /// <summary>The design's mix by points per role: Swarm, Gunline, Bombers, Heavy hitters or Balanced mix.</summary>
        public string EnemyStyle()
        {
            if (_design == null) return "-";
            var byRole = new Dictionary<string, int>();
            var total = 0;
            foreach (var w in _design.windows)
            {
                if (w?.enemies == null) continue;
                foreach (var e in w.enemies)
                {
                    if (e == null || !_kinds.TryGetValue(e.kind ?? string.Empty, out var k)) continue;
                    var pts = k.cost * e.count;
                    var role = k.role ?? string.Empty;
                    byRole[role] = (byRole.TryGetValue(role, out var had) ? had : 0) + pts;
                    total += pts;
                }
            }
            if (total <= 0) return "No enemies yet";
            float Share(params string[] roles)
            {
                var s = 0;
                foreach (var r in roles) if (byRole.TryGetValue(r, out var v)) s += v;
                return s / (float)total;
            }
            if (Share("elite", "miniboss", "heavy") >= 0.45f) return "Heavy hitters";
            if (Share("fodder") >= 0.5f) return "Swarm";
            if (Share("shooter") >= 0.4f) return "Gunline";
            if (Share("bomber") >= 0.4f) return "Bombers";
            if (Share("melee") >= 0.5f) return "Melee rush";
            return "Balanced mix";
        }

        string KindName(string kind) => _kinds.TryGetValue(kind ?? string.Empty, out var k) && !string.IsNullOrEmpty(k.displayName) ? k.displayName : kind;

        string BossName(string kind)
        {
            var bosses = _view?.bosses ?? _catalog?.bosses;
            if (bosses != null) foreach (var b in bosses) if (b != null && b.kind == kind) return b.displayName;
            return "Barnacle King";
        }

        static string Clean(string s) => string.IsNullOrEmpty(s) ? string.Empty : s.Replace(((char)0x2014).ToString(), ", ").Replace(((char)0x2013).ToString(), "-");

        /// <summary>The preset the design currently equals (null: a custom design).</summary>
        public DefencePreset CurrentPreset()
        {
            if (_design == null || _catalog?.presets == null) return null;
            foreach (var p in _catalog.presets)
            {
                if (p?.design == null) continue;
                if (!_presetDesigns.TryGetValue(p.id ?? p.name, out var d))
                    _presetDesigns[p.id ?? p.name] = d = DefenceRules.Normalize(p.design, _catalog.defaultGuardian ?? "barnacle_king");
                if (Same(d, _design)) return p;
            }
            return null;
        }

        static bool Same(DefenceDesign a, DefenceDesign b)
        {
            if (a.guardian.minute != b.guardian.minute || a.guardian.kind != b.guardian.kind) return false;
            for (var i = 0; i < DefenceRules.Windows; i++)
            {
                var x = a.windows[i].enemies;
                var y = b.windows[i].enemies;
                if (x.Length != y.Length) return false;
                foreach (var e in x)
                {
                    var match = false;
                    foreach (var f in y) if (f.kind == e.kind && f.count == e.count) { match = true; break; }
                    if (!match) return false;
                }
            }
            return true;
        }

        // ================================================================ edits

        /// <summary>Shows minute <paramref name="minute"/> (1..15) in the middle column.</summary>
        public void SelectMinute(int minute)
        {
            minute = Mathf.Clamp(minute, 1, DefenceRules.Windows);
            if (minute == _selected) return;
            _selected = minute;
            _touched.Clear();
            Render(resetEnemies: true);
        }

        /// <summary>"Add enemy": the middle column lists every enemy kind by role (again: back to the minute's enemies).</summary>
        public void TogglePicker()
        {
            if (_design == null) return;
            _picking = !_picking;
            Render(resetEnemies: true);
        }

        public void Change(int minute, string kind, int delta)
        {
            if (_design == null || minute < 1 || minute > DefenceRules.Windows) return;
            DefenceRules.Add(_design.windows[minute - 1], kind, delta);
            if (minute == _selected) _touched.Add(kind);
            Edited();
        }

        /// <summary>Copies the previous minute's enemies into the selected minute (not saved until Save).</summary>
        public void DuplicatePreviousMinute()
        {
            if (_design == null || _selected <= 1) return;
            var from = _design.windows[_selected - 2].enemies ?? Array.Empty<DefenceEnemy>();
            var copy = new DefenceEnemy[from.Length];
            for (var i = 0; i < from.Length; i++) copy[i] = new DefenceEnemy { kind = from[i].kind, count = from[i].count };
            _design.windows[_selected - 1].enemies = copy;
            _touched.Clear();
            Edited();
            AppendStatus($" Minute {_selected} now copies minute {_selected - 1}.");
        }

        /// <summary>Removes every enemy from the selected minute (not saved until Save).</summary>
        public void ClearMinute()
        {
            if (_design == null) return;
            _design.windows[_selected - 1].enemies = Array.Empty<DefenceEnemy>();
            _touched.Clear();
            Edited();
            AppendStatus($" Minute {_selected} cleared.");
        }

        void AppendStatus(string more)
        {
            if (OverBudgetWindows() == 0) _status.text += more;
        }

        public void GuardianMinute(int delta)
        {
            if (_design == null) return;
            _design.guardian.minute = Mathf.Clamp(_design.guardian.minute + delta, 2, DefenceRules.Windows);
            _design.guardian.arrivesAtSecond = (_design.guardian.minute - 1) * 60;
            Edited();
        }

        public void ApplyPreset(DefencePreset preset)
        {
            if (preset?.design == null) return;
            _design = DefenceRules.Normalize(preset.design, _catalog?.defaultGuardian ?? "barnacle_king");
            _picking = false;
            _touched.Clear();
            Edited();
            Status($"Preset {preset.name}: {Clean(preset.description)} Save to keep it.", HackUi.Gold);
        }

        public bool ApplyPreset(string id)
        {
            if (_catalog?.presets == null) return false;
            foreach (var p in _catalog.presets) if (p != null && p.id == id) { ApplyPreset(p); return true; }
            return false;
        }

        void Edited()
        {
            _problems = null;
            _dirty = true;
            var over = OverBudgetWindows();
            Status(over > 0 ? $"{over} minute{(over == 1 ? " is" : "s are")} over budget. The server will refuse the design until they fit." : "Unsaved changes. Save to keep them.",
                over > 0 ? HackUi.Bad : HackUi.Gold);
            Render();
        }

        // ================================================================ render

        void Render(bool resetEnemies = false)
        {
            var keepTimeline = _timeline.anchoredPosition;
            var keepEnemies = _enemies.anchoredPosition;
            RenderPresets();
            RenderTimeline();
            RenderMinute();
            RenderSummary();
            RefreshButtons();
            Canvas.ForceUpdateCanvases();
            _timeline.anchoredPosition = keepTimeline;
            _enemies.anchoredPosition = resetEnemies ? Vector2.zero : keepEnemies;
        }

        void RenderPresets()
        {
            Clear(_presetButtons);
            var current = CurrentPreset();
            if (_catalog?.presets != null)
                foreach (var p in _catalog.presets)
                {
                    if (p == null) continue;
                    var preset = p;
                    var on = current == p;
                    var rt = Node(_presetButtons, p.name);
                    var le = rt.gameObject.AddComponent<LayoutElement>();
                    le.preferredWidth = le.minWidth = 230f;
                    KitButton(rt, p.name, null, () => ApplyPreset(preset), on ? "Btn_Gold" : "Btn_Parchment", 28f).interactable = _design != null && !_busy;
                }
            _presetLine.text = _design == null ? string.Empty
                : current != null ? Clean(current.description)
                : "Custom design. Pick a preset to replace the whole design.";
        }

        void RenderTimeline()
        {
            Clear(_timeline);
            if (_design == null) return;
            for (var m = 1; m <= DefenceRules.Windows; m++)
            {
                var minute = m;
                var on = m == _selected;
                var cost = Cost(m);
                var budget = Budget(m);
                var over = cost > budget;
                var boss = _design.guardian.minute == m;
                var row = ListRow(_timeline, "Minute " + m, TimelineRow, on ? "Btn_Gold" : "Slot_Card", on ? 1.4f : 2.4f);
                var img = row.GetComponent<Image>();
                img.raycastTarget = true;
                if (over && !on) img.color = new Color(1f, 0.78f, 0.72f, 1f);
                var b = row.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => SelectMinute(minute));
                if (on)
                {
                    // The arrow tab on the selected row's right edge.
                    var tab = Node(row, "Tab");
                    tab.anchorMin = tab.anchorMax = new Vector2(1f, 0.5f);
                    tab.sizeDelta = new Vector2(38f, 38f);
                    tab.anchoredPosition = new Vector2(-2f, 0f);
                    tab.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    tab.SetAsFirstSibling();
                    Paint(tab, "Btn_Square_Gold", 4f, null, new Color(0.96f, 0.76f, 0.3f, 1f));
                }
                var badge = Node(row, "Badge");
                badge.anchorMin = badge.anchorMax = new Vector2(0f, 0.5f);
                badge.pivot = new Vector2(0f, 0.5f);
                badge.anchoredPosition = new Vector2(12f, 0f);
                badge.sizeDelta = new Vector2(48f, 48f);
                Paint(badge, "Badge_Number", 2.2f, null, new Color(0.3f, 0.2f, 0.12f, 1f));
                Label(Fill(badge, "N"), m.ToString(), 26f, OnWood, TextAlignmentOptions.Center, true);

                var name = Node(row, "Name");
                name.anchorMin = new Vector2(0f, 0f);
                name.anchorMax = new Vector2(0.55f, 1f);
                name.offsetMin = new Vector2(76f, boss ? 26f : 0f);
                name.offsetMax = new Vector2(0f, boss ? -4f : 0f);
                Label(name, $"Minute {m}", 27f, Ink, TextAlignmentOptions.MidlineLeft, on, 18f);
                if (boss)
                {
                    var line = Node(row, "Boss");
                    line.anchorMin = new Vector2(0f, 0f);
                    line.anchorMax = new Vector2(0.62f, 0f);
                    line.pivot = new Vector2(0f, 0f);
                    line.offsetMin = new Vector2(76f, 4f);
                    line.offsetMax = new Vector2(0f, 30f);
                    Paint(TopLeft(line, "Skull", 0f, 1f, 24f, 24f), "Icon_Skull_Crossbones", 1f, Ink, new Color(0f, 0f, 0f, 0f)).preserveAspect = true;
                    Label(Fill(line, "Text", 30f), "Boss arrives", 20f, InkDim, TextAlignmentOptions.MidlineLeft, false, 14f);
                }
                var pts = Node(row, "Points");
                pts.anchorMin = new Vector2(0.5f, 0f);
                pts.anchorMax = Vector2.one;
                pts.offsetMin = Vector2.zero;
                pts.offsetMax = new Vector2(-20f, 0f);
                Label(pts, $"{cost} / {budget} pts", 26f, over ? InkBad : Ink, TextAlignmentOptions.MidlineRight, over || on, 16f);
            }
        }

        void RenderMinute()
        {
            Clear(_enemies);
            _minuteTitle.text = $"Minute {_selected}";
            if (_design == null)
            {
                _minutePoints.text = string.Empty;
                SetBar(0f, false);
                return;
            }
            var cost = Cost(_selected);
            var budget = Budget(_selected);
            var over = cost > budget;
            _minutePoints.text = over ? $"<color=#{ColorUtility.ToHtmlStringRGB(InkBad)}>Over budget  {cost} / {budget}</color>" : $"{cost} / {budget} points";
            SetBar(budget > 0 ? cost / (float)budget : 0f, over);
            _enemiesTitle.text = _picking ? $"Add enemies to minute {_selected}" : "Enemies in this wave";

            if (!string.IsNullOrEmpty(_problems)) TextRow(_problems, 22f, InkBad);

            if (_picking)
            {
                var left = budget - cost;
                var groups = new List<string>();
                var byRole = new Dictionary<string, List<DefenceKind>>();
                foreach (var k in _kinds.Values)
                {
                    var role = k.role ?? string.Empty;
                    if (!byRole.TryGetValue(role, out var list)) { byRole[role] = list = new List<DefenceKind>(); groups.Add(role); }
                    list.Add(k);
                }
                TextRow($"{left} point{(left == 1 ? string.Empty : "s")} left this minute. Press Done when finished.", 22f, left < 0 ? InkBad : InkDim);
                foreach (var role in groups)
                {
                    TextRow(DefenceRules.RoleName(role), 26f, InkGold, true);
                    foreach (var k in byRole[role]) EnemyRowFor(k.kind, CountOf(k.kind), k.cost <= left);
                }
                return;
            }

            var w = _design.windows[_selected - 1];
            var shown = new HashSet<string>();
            if (w.enemies != null)
                foreach (var e in w.enemies)
                {
                    shown.Add(e.kind);
                    EnemyRowFor(e.kind, e.count, true);
                }
            foreach (var kind in _touched)
                if (!shown.Contains(kind)) EnemyRowFor(kind, 0, true);
            if ((w.enemies == null || w.enemies.Length == 0) && _touched.Count == 0)
                TextRow("No enemies this minute. Press Add enemy to pick some, or Duplicate previous minute.", 24f, InkDim);
        }

        int CountOf(string kind)
        {
            var w = _design.windows[_selected - 1];
            if (w.enemies != null) foreach (var e in w.enemies) if (e.kind == kind) return e.count;
            return 0;
        }

        void SetBar(float value, bool over)
        {
            var rt = _barFill.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
            _barFill.enabled = value > 0.001f;
            var s = HackSkin.S(over ? "Bar_Fill_Gold" : "Bar_Fill_Green");
            if (s != null) _barFill.sprite = s;
            _barFill.color = over ? new Color(1f, 0.42f, 0.32f, 1f) : Color.white;
        }

        void TextRow(string text, float size, Color color, bool bold = false)
        {
            var row = Node(_enemies, "Text");
            var v = row.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(14, 14, 6, 6);
            v.childControlHeight = v.childControlWidth = true;
            v.childForceExpandHeight = false;
            row.gameObject.AddComponent<LayoutElement>().minHeight = 40f;
            var t = Label(Node(row, "Label"), text, size, color, TextAlignmentOptions.MidlineLeft, bold);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
        }

        void EnemyRowFor(string kind, int count, bool fits)
        {
            var minute = _selected;
            var row = ListRow(_enemies, kind, EnemyRow, "Slot_Card", 2.4f);
            var cost = _costs.TryGetValue(kind ?? string.Empty, out var c) ? c : 0;
            _kinds.TryGetValue(kind ?? string.Empty, out var k);
            EnemyIcon(row, k?.role);

            var name = Node(row, "Name");
            name.anchorMin = new Vector2(0f, 0.5f);
            name.anchorMax = new Vector2(1f, 1f);
            name.offsetMin = new Vector2(104f, 0f);
            name.offsetMax = new Vector2(-260f, -8f);
            Label(name, KindName(kind), 28f, Ink, TextAlignmentOptions.BottomLeft, true, 18f);
            var each = Node(row, "Each");
            each.anchorMin = Vector2.zero;
            each.anchorMax = new Vector2(1f, 0.5f);
            each.offsetMin = new Vector2(104f, 8f);
            each.offsetMax = new Vector2(-260f, 0f);
            Label(each, $"{cost} pt{(cost == 1 ? string.Empty : "s")} each", 23f, fits ? InkBad : new Color(InkBad.r, InkBad.g, InkBad.b, 0.55f), TextAlignmentOptions.TopLeft, false, 16f);

            var x = Node(row, "Count");
            x.anchorMin = new Vector2(1f, 0f);
            x.anchorMax = Vector2.one;
            x.pivot = new Vector2(1f, 0.5f);
            x.sizeDelta = new Vector2(90f, 0f);
            x.anchoredPosition = new Vector2(-170f, 0f);
            Label(x, $"x {count}", 32f, count > 0 ? Ink : InkDim, TextAlignmentOptions.Center, false, 20f);

            KitButton(SquareAt(row, -92f), null, "Icon_Minus", () => Change(minute, kind, -1), "Btn_Square_Gold", 24f, 34f, 2.4f).interactable = count > 0;
            KitButton(SquareAt(row, -16f), null, "Icon_Plus", () => Change(minute, kind, 1), "Btn_Square_Gold", 24f, 34f, 2.4f).interactable = count < DefenceRules.MaxCount;
        }

        static RectTransform SquareAt(Transform row, float right)
        {
            var rt = Node(row, "Square");
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(right, 0f);
            rt.sizeDelta = new Vector2(66f, 66f);
            return rt;
        }

        /// <summary>No enemy portraits in the project yet: a tinted card with the kit glyph for the enemy's role.</summary>
        static void EnemyIcon(RectTransform row, string role)
        {
            Color tint;
            string glyph;
            switch (role)
            {
                case "fodder": tint = new Color(0.95f, 0.85f, 0.6f); glyph = "Icon_Group"; break;
                case "melee": tint = new Color(0.95f, 0.6f, 0.5f); glyph = "Icon_Swords"; break;
                case "shooter": tint = new Color(0.62f, 0.78f, 0.95f); glyph = "Icon_Ship_Wheel"; break;
                case "bomber": tint = new Color(1f, 0.7f, 0.4f); glyph = "Icon_Skull_Crossbones"; break;
                case "heavy": tint = new Color(0.7f, 0.85f, 0.6f); glyph = "Icon_Shield"; break;
                case "elite": tint = new Color(0.85f, 0.65f, 0.95f); glyph = "Crown_Gold"; break;
                case "miniboss": tint = new Color(0.9f, 0.45f, 0.4f); glyph = "Icon_Skull_Crossbones"; break;
                default: tint = new Color(0.85f, 0.8f, 0.7f); glyph = "Icon_Info"; break;
            }
            var box = Node(row, "Icon");
            box.anchorMin = box.anchorMax = new Vector2(0f, 0.5f);
            box.pivot = new Vector2(0f, 0.5f);
            box.anchoredPosition = new Vector2(16f, 0f);
            box.sizeDelta = new Vector2(72f, 72f);
            Paint(box, "Btn_Square_Parchment", 2.6f, tint, tint);
            Paint(Fill(box, "Glyph", 14f, 14f, 14f, 14f), glyph, 1f, glyph.StartsWith("Icon_") ? Ink : Color.white, new Color(0f, 0f, 0f, 0f)).preserveAspect = true;
        }

        void RenderSummary()
        {
            if (_design == null)
            {
                _guardianName.text = "Guardian";
                _guardianArrival.text = string.Empty;
                _pressure.text = _style.text = _totals.text = "-";
                _earlier.interactable = _later.interactable = false;
                _duplicate.interactable = _clear.interactable = _add.interactable = false;
                return;
            }
            var g = _design.guardian;
            _guardianName.text = $"Guardian: {BossName(g.kind)}";
            _guardianArrival.text = $"Arrives at Minute {g.minute} ({IslandRules.GuardianArrival(g.minute)})";
            _earlier.interactable = g.minute > 2;
            _later.interactable = g.minute < DefenceRules.Windows;
            var used = TotalCost();
            var total = TotalBudget();
            _pressure.text = Pressure(used, total);
            _pressure.color = used > total ? InkBad : InkGold;
            _style.text = EnemyStyle();
            _totals.text = $"{used} / {total}";
            _totals.color = OverBudgetWindows() > 0 ? InkBad : Ink;
            _add.interactable = true;
            if (_addLabel != null) _addLabel.text = _picking ? "Done" : "Add enemy";
            var icon = _add.transform.Find("Content/Icon");
            if (icon != null)
            {
                var s = HackSkin.S(_picking ? "Icon_Check" : "Icon_Plus");
                if (s != null) icon.GetComponent<Image>().sprite = s;
            }
            _duplicate.interactable = _selected > 1;
            _clear.interactable = (_design.windows[_selected - 1].enemies?.Length ?? 0) > 0;
        }
    }
}
