using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.UI
{
    /// <summary>
    /// V5 M9i: the one parchment UI kit (the art team's 13 Oct pack, <see cref="HackSkin"/>), shared by V4's menus and
    /// HUD and the hackathon screens. It replaces the seven copies the hackathon left (V4's MenuKit and the leaderboard's
    /// kit, which read the skin by reflection, and the Market, Board, Island, PvP and Defence helpers), keeping each
    /// screen's look: where the copies differed (a scroll bar's width, a crown's size) the difference is a parameter.
    /// Sizes are for the 1920x1080 canvas. This file builds new kit pieces; <c>UiKit.Restyle.cs</c> re-skins existing
    /// controls (V4's menus). Without the skin every piece falls back to flat colours, so nothing breaks.
    /// </summary>
    public static partial class UiKit
    {
        // ---------------------------------------------------------------- skin

        public static Color Ink => HackSkin.I.ink;
        public static Color InkDim => HackSkin.I.inkDim;
        public static Color InkGold => HackSkin.I.inkGold;
        public static Color InkGood => HackSkin.I.inkGood;
        public static Color InkBad => HackSkin.I.inkBad;
        public static Color OnWood => HackSkin.I.onWood;

        /// <summary>The inks as rich-text colours (the skin's defaults).</summary>
        public const string InkHex = "#3d240f", DimHex = "#7a5a3a", GoldHex = "#8c4d05", GoodHex = "#2b7324", BadHex = "#a82917";

        /// <summary>The kit font (null: TMP's default).</summary>
        public static TMP_FontAsset Font => HackSkin.I.font;

        static readonly Dictionary<string, Sprite> Loose = new Dictionary<string, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Loose.Clear();

        /// <summary>The kit sprite called <paramref name="name"/>, else a loose one at Resources/IslandArt/<paramref name="name"/>
        /// (the Islands screen's RUM barrel and cannons); null when neither exists.</summary>
        public static Sprite S(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var s = HackSkin.S(name);
            if (s != null) return s;
            if (!Loose.TryGetValue(name, out s))
            {
                s = Resources.Load<Sprite>("IslandArt/" + name);
                Loose[name] = s;
            }
            return s;
        }

        /// <summary>True when the kit is present (its parchment card at least).</summary>
        public static bool Ready => S("Panel_Card") != null;

        /// <summary>A colour as rich text ("#RRGGBB").</summary>
        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>Rich-text colours written for dark panels (V4's and the server's) swapped for the parchment inks.</summary>
        public static string InkRich(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("#9aa4ad", DimHex).Replace("#ffd166", GoldHex).Replace("#FFD166", GoldHex)
                .Replace("#73d973", GoodHex).Replace("#7fd68b", GoodHex).Replace("#ff8c73", BadHex)
                .Replace("#7fc8ff", "#2a5f8f").Replace("#c08cff", "#6a3d9a");
        }

        // ---------------------------------------------------------------- rects

        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>A child at (<paramref name="x"/>, <paramref name="y"/>) from its parent's top-left, <paramref name="w"/> x <paramref name="h"/>.</summary>
        public static RectTransform TopLeft(Transform parent, string name, float x, float y, float w, float h)
        {
            var rt = Node(parent, name);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>A child at <paramref name="right"/> from its parent's right edge and <paramref name="y"/> from the top.</summary>
        public static RectTransform TopRight(Transform parent, string name, float right, float y, float w, float h)
        {
            var rt = Node(parent, name);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-right, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>A child stretched across its parent's width, <paramref name="h"/> tall at <paramref name="y"/> from the top.</summary>
        public static RectTransform Strip(Transform parent, string name, float left, float y, float right, float h)
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
        public static RectTransform BottomStrip(Transform parent, string name, float left, float bottom, float right, float h)
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
        public static RectTransform Fill(Transform parent, string name, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            var rt = Node(parent, name);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>A child between horizontal anchors (<paramref name="x0"/>..<paramref name="x1"/>), full height, with pixel padding.</summary>
        public static RectTransform Cols(Transform parent, string name, float x0, float x1, float padLeft = 0f, float padRight = 0f)
        {
            var rt = Node(parent, name);
            rt.anchorMin = new Vector2(x0, 0f);
            rt.anchorMax = new Vector2(x1, 1f);
            rt.offsetMin = new Vector2(padLeft, 0f);
            rt.offsetMax = new Vector2(-padRight, 0f);
            return rt;
        }

        // ---------------------------------------------------------------- layout groups

        /// <summary>A child of a layout group with a fixed (or flexible, when <paramref name="width"/> is 0) width.</summary>
        public static RectTransform Cell(Transform row, string name, float width, float height = -1f)
        {
            var rt = Node(row, name);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            if (width > 0f) { le.preferredWidth = le.minWidth = width; le.flexibleWidth = 0f; }
            else { le.flexibleWidth = 1f; le.minWidth = 80f; }
            if (height > 0f) le.preferredHeight = le.minHeight = height;
            return rt;
        }

        /// <summary>A table cell <paramref name="width"/> wide (0: takes the rest).</summary>
        public static RectTransform Cell(Transform row, float width)
        {
            var rt = Node(row, "Cell");
            var le = rt.gameObject.AddComponent<LayoutElement>();
            if (width > 0f) le.preferredWidth = le.minWidth = width;
            else { le.flexibleWidth = 1f; le.minWidth = 80f; }
            return rt;
        }

        /// <summary>A horizontal layout on <paramref name="rt"/> (children sized by their layout elements).</summary>
        public static HorizontalLayoutGroup Row(RectTransform rt, float spacing, RectOffset padding = null, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = padding ?? new RectOffset();
            h.childAlignment = align;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return h;
        }

        /// <summary>A fixed-height list row, optionally painted with a kit sprite.</summary>
        public static RectTransform ListRow(Transform list, string name, float height, string sprite = null, float thin = 1f, Color? tint = null)
        {
            var rt = Node(list, name);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = height;
            if (sprite != null) Paint(rt, sprite, thin, tint, tint ?? new Color(0.95f, 0.88f, 0.72f, 1f));
            return rt;
        }

        /// <summary>A table row (or the header bar): a list row with the table's columns layout.</summary>
        public static RectTransform TableRow(Transform list, float height, string sprite, float thin, Color? tint)
        {
            var rt = ListRow(list, "Row", height, sprite, thin, tint);
            Columns(rt);
            return rt;
        }

        /// <summary>A parchment table row (Panel_Inset; no art when <paramref name="tint"/> is null).</summary>
        public static RectTransform TableRow(Transform list, float height, Color? tint) =>
            TableRow(list, height, tint.HasValue ? "Panel_Inset" : null, 2.4f, tint);

        /// <summary>The table's columns: a horizontal layout with the side padding.</summary>
        public static void Columns(RectTransform rt)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(18, 18, 0, 0);
            h.spacing = 12f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
        }

        public static TextMeshProUGUI TextCell(Transform row, string text, float width, float size, TextAlignmentOptions align, Color color, bool bold = false) =>
            Label(Cell(row, width), text, size, color, align, bold, size * 0.6f);

        /// <summary>The rank cell: "#n" (or n) with the kit crown (<paramref name="crown"/> wide) for places 1 to 3.</summary>
        public static void RankCell(Transform row, int rank, float width, float size, bool hash = false, float crown = 52f)
        {
            var cell = Cell(row, width);
            var sprite = rank == 1 ? "Crown_Gold" : rank == 2 ? "Crown_Silver" : rank == 3 ? "Crown_Bronze" : null;
            if (sprite != null && S(sprite) != null)
            {
                var c = Node(cell, "Crown");
                c.anchorMin = new Vector2(0f, 0.12f);
                c.anchorMax = new Vector2(0f, 0.88f);
                c.pivot = new Vector2(0f, 0.5f);
                c.sizeDelta = new Vector2(crown, 0f);
                c.anchoredPosition = Vector2.zero;
                Picture(c, sprite);
            }
            Label(Fill(cell, "Rank", 60f), (hash ? "#" : string.Empty) + rank, size, Ink, TextAlignmentOptions.MidlineLeft, true);
        }

        public static void Clear(Transform list)
        {
            for (var i = list.childCount - 1; i >= 0; i--)
            {
                var c = list.GetChild(i).gameObject;
                c.SetActive(false);
                UnityEngine.Object.Destroy(c);
            }
        }

        // ---------------------------------------------------------------- art and text

        /// <summary>Kit sprite <paramref name="sprite"/> on <paramref name="rt"/> (9-sliced when it has borders; <paramref name="thin"/>
        /// above 1 draws the border art smaller), not a raycast target. Without the sprite, <paramref name="fallback"/> (or
        /// the tint, or tan) fills the rect.</summary>
        public static Image Paint(RectTransform rt, string sprite, float thin = 1f, Color? tint = null, Color? fallback = null)
        {
            var img = rt.GetComponent<Image>();
            if (img == null) img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            var s = S(sprite);
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

        /// <summary>A simple (not sliced) kit sprite, aspect kept; transparent without it.</summary>
        public static Image Picture(RectTransform rt, string sprite, Color? tint = null)
        {
            var img = Paint(rt, sprite, 1f, tint, new Color(0f, 0f, 0f, 0f));
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            return img;
        }

        /// <summary>The tint of a kit picture: glyphs ("Icon_*", not the green ones) in <paramref name="ink"/>, art untinted.</summary>
        public static Color IconTint(string sprite, Color ink) =>
            sprite != null && sprite.StartsWith("Icon_") && !sprite.EndsWith("_Green") ? ink : Color.white;

        /// <summary>An icon or picture filling <paramref name="rt"/>: kit glyphs tinted (<paramref name="tint"/>, else ink), art untinted.</summary>
        public static Image Icon(RectTransform rt, string sprite, Color? tint = null)
        {
            var img = Paint(rt, sprite, 1f, IconTint(sprite, tint ?? Ink), new Color(0f, 0f, 0f, 0f));
            img.preserveAspect = true;
            return img;
        }

        /// <summary>An icon <paramref name="size"/> square at (<paramref name="x"/>, <paramref name="y"/>) from the parent's top-left.</summary>
        public static Image Icon(Transform parent, string sprite, float x, float y, float size, Color? tint = null) =>
            Icon(TopLeft(parent, sprite, x, y, size, size), sprite, tint);

        /// <summary>A kit icon centred vertically at <paramref name="x"/> from the left of a row.</summary>
        public static Image RowIcon(Transform row, string sprite, float x, float size, Color? tint = null)
        {
            var rt = Node(row, sprite);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(size, size);
            return Icon(rt, sprite, tint);
        }

        /// <summary>Text in the kit font (one line, ellipsis; auto-sized down to <paramref name="minSize"/> when given).</summary>
        public static TextMeshProUGUI Label(RectTransform rt, string text, float size, Color color, TextAlignmentOptions align, bool bold = false, float minSize = 0f)
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

        /// <summary>A thin brown rule across the parent at <paramref name="y"/>.</summary>
        public static void Line(Transform parent, float left, float y, float right)
        {
            var img = Strip(parent, "Line", left, y, right, 2f).gameObject.AddComponent<Image>();
            img.color = new Color(0.45f, 0.3f, 0.15f, 0.35f);
            img.raycastTarget = false;
        }

        /// <summary>A tan chip ("Lv 3", a price) sized by the layout or its rect.</summary>
        public static TextMeshProUGUI Chip(RectTransform rt, string text, float size, Color? ink = null, bool bold = false)
        {
            Paint(rt, "Panel_Inset", 3f, new Color(0.93f, 0.82f, 0.62f, 1f), new Color(0.88f, 0.76f, 0.56f, 1f));
            return Label(Fill(rt, "Text", 10f, 0f, 10f, 0f), text, size, ink ?? Ink, TextAlignmentOptions.Center, bold, size * 0.6f);
        }

        /// <summary>A tag pill (Tag_Green, Tag_Red, Tag_Blue, Tag_Tan) filling <paramref name="rt"/>, with a label.</summary>
        public static void Tag(RectTransform rt, string sprite, string text, float size = 26f)
        {
            Paint(rt, sprite, 1f, null, new Color(0.85f, 0.75f, 0.55f, 1f));
            Label(Fill(rt, "Label", 10f, 2f, 10f, 2f), text, size, Ink, TextAlignmentOptions.Center, true, 14f);
        }

        /// <summary>A short kit tag (Tag_Green "YOU", Banner_Maroon "You") on the right of <paramref name="parent"/>.</summary>
        public static void Tag(RectTransform parent, string sprite, string text, float right, float w, float h, Color textColor)
        {
            var tag = Node(parent, "Tag");
            tag.anchorMin = tag.anchorMax = tag.pivot = new Vector2(1f, 0.5f);
            tag.anchoredPosition = new Vector2(-right, 0f);
            tag.sizeDelta = new Vector2(w, h);
            Paint(tag, sprite, 1.4f, null, new Color(0.3f, 0.6f, 0.25f, 1f));
            Label(Fill(tag, "Text"), text, h * 0.5f, textColor, TextAlignmentOptions.Center, true);
        }

        // ---------------------------------------------------------------- buttons and fields

        /// <summary>
        /// A kit button filling <paramref name="rt"/>: <paramref name="style"/> is Btn_Gold, Btn_Parchment, Btn_Green, Btn_Red
        /// or Btn_Square_*; an icon (kit name) sits <paramref name="spacing"/> left of the label; the label may be empty (icon
        /// only). The label wraps, or with <paramref name="wrap"/> false stays one centred line (the Market and wallet
        /// screens' buttons). Green and red buttons are dark enough for light text.
        /// </summary>
        public static Button KitButton(RectTransform rt, string label, string icon, Action click, string style = "Btn_Gold", float size = 30f, float iconSize = 40f,
            float thin = 1f, bool wrap = true, float spacing = 12f)
        {
            var img = Paint(rt, style, thin, null, new Color(0.95f, 0.75f, 0.3f, 1f));
            img.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            SetStyle(b, style, thin);
            if (click != null) b.onClick.AddListener(() => click());

            var ink = style == "Btn_Green" || style == "Btn_Red" ? OnWood : Ink;
            var content = Fill(rt, "Content", 10f, 4f, 10f, 2f);
            var h = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = spacing;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            if (!string.IsNullOrEmpty(icon) && S(icon) != null)
            {
                var ic = Node(content, "Icon");
                Icon(ic, icon, ink);
                var le = ic.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = le.minWidth = le.preferredHeight = le.minHeight = iconSize;
            }
            if (!string.IsNullOrEmpty(label))
            {
                var t = Label(Node(content, "Label"), label, size, ink, wrap ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center, true);
                if (wrap)
                {
                    t.textWrappingMode = TextWrappingModes.Normal;
                    t.lineSpacing = -12f;
                }
                else t.overflowMode = TextOverflowModes.Overflow;
            }
            return b;
        }

        /// <summary>A one-line kit button (as the Market's) as a layout child, <paramref name="width"/> wide (0: flexible).</summary>
        public static Button RowButton(Transform row, string label, string icon, Action click, float width, float height, string style = "Btn_Gold", float size = 30f)
            => KitButton(Cell(row, label ?? icon, width, height), label, icon, click, style, size, size * 1.25f, 1f, false, 10f);

        /// <summary>A row of equal kit buttons (label, icon, click, style).</summary>
        public static List<Button> ButtonRow(Transform list, float height, params (string label, string icon, Action click, string style)[] buttons)
        {
            var row = ListRow(list, "Buttons", height);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 18f;
            h.padding = new RectOffset(4, 4, 2, 2);
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = true;
            var made = new List<Button>();
            foreach (var (label, icon, click, style) in buttons)
            {
                var node = Node(row, label);
                node.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                made.Add(KitButton(node, label, icon, click, style ?? "Btn_Gold", 32f, 40f));
            }
            return made;
        }

        /// <summary>Swaps a kit button's look (a tab or the picked stake turns gold): the art, and its pressed and disabled
        /// art (sprite swap) or a colour tint for styles without them.</summary>
        public static void SetStyle(Button b, string style, float thin = 1f)
        {
            if (b == null) return;
            var img = b.targetGraphic as Image;
            var s = S(style);
            if (img != null && s != null)
            {
                img.sprite = s;
                img.type = s.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
                img.pixelsPerUnitMultiplier = thin;
                img.color = Color.white;
            }
            else if (img != null) img.color = style == "Btn_Gold" ? new Color(0.95f, 0.75f, 0.3f, 1f) : new Color(0.85f, 0.75f, 0.58f, 1f);
            var pressed = S(style == "Btn_Gold" ? "Btn_Gold_Pressed" : null);
            var disabled = S(style == "Btn_Gold" ? "Btn_Gold_Disabled" : style.StartsWith("Btn_Square") ? "Btn_Square_Disabled" : null);
            if (img != null && img.sprite != null && (pressed != null || disabled != null))
            {
                b.transition = Selectable.Transition.SpriteSwap;
                b.spriteState = new SpriteState { pressedSprite = pressed != null ? pressed : img.sprite, highlightedSprite = img.sprite, selectedSprite = img.sprite, disabledSprite = disabled };
            }
            else
            {
                b.transition = Selectable.Transition.ColorTint;
                var c = ColorBlock.defaultColorBlock;
                c.pressedColor = new Color(0.85f, 0.8f, 0.7f, 1f);
                c.disabledColor = new Color(1f, 1f, 1f, 0.5f);
                b.colors = c;
            }
        }

        public static TextMeshProUGUI ButtonLabel(Button b) => b != null ? b.GetComponentInChildren<TextMeshProUGUI>(true) : null;

        public static void SetLabel(Button b, string text)
        {
            var t = ButtonLabel(b);
            if (t != null) t.text = text;
        }

        /// <summary>A single-line kit input field filling <paramref name="rt"/>.</summary>
        public static TMP_InputField Input(RectTransform rt, string placeholder, int limit, float size = 30f)
        {
            var bg = Paint(rt, "Input_Field", 1f, null, new Color(0.98f, 0.95f, 0.88f, 1f));
            bg.raycastTarget = true;
            var area = Fill(rt, "Text Area", 20f, 6f, 20f, 6f);
            area.gameObject.AddComponent<RectMask2D>();
            var ph = Label(Fill(area, "Placeholder"), placeholder, size, new Color(Ink.r, Ink.g, Ink.b, 0.45f), TextAlignmentOptions.MidlineLeft);
            ph.fontStyle = FontStyles.Italic;
            var text = Label(Fill(area, "Text"), string.Empty, size, Ink, TextAlignmentOptions.MidlineLeft);
            text.overflowMode = TextOverflowModes.Overflow;
            var input = rt.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = bg;
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = ph;
            input.characterLimit = limit;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = TMP_InputField.ContentType.Standard;
            input.richText = false;
            input.fontAsset = text.font;
            input.pointSize = size;
            input.caretColor = Ink;
            input.selectionColor = new Color(0.95f, 0.72f, 0.25f, 0.5f);
            return input;
        }

        // ---------------------------------------------------------------- frames and lists

        /// <summary>The wood header across the top of a panel: skull, title (returned) and room on the right for its buttons.</summary>
        public static TextMeshProUGUI Header(RectTransform panel, string title, float height, out RectTransform head)
        {
            head = Strip(panel, "Header", 0f, 0f, 0f, height);
            Paint(head, "Header_Wood", 1.4f, null, new Color(0.32f, 0.2f, 0.1f, 0.97f));
            var skull = Paint(TopLeft(head, "Skull", 34f, (height - 92f) * 0.5f, 104f, 92f), "Icon_Skull_Crossbones", 1f, OnWood, new Color(0f, 0f, 0f, 0f));
            skull.preserveAspect = true;
            return Label(Strip(head, "Title", 160f, 0f, 700f, height), title, 58f, OnWood, TextAlignmentOptions.MidlineLeft, true, 34f);
        }

        /// <summary>The medallion with the skull (a parchment disc under the dark glyph).</summary>
        public static RectTransform Medallion(Transform parent, float x, float y, float size)
        {
            var medal = TopLeft(parent, "Medallion", x, y, size, size);
            var frame = Picture(medal, "Medallion_Frame");
            var inset = size * 0.23f;
            if (frame.sprite != null) Picture(Fill(medal, "Disc", inset, inset, inset, inset), "Dot_Beige", new Color(0.96f, 0.88f, 0.7f, 1f));
            var g = size * 0.3f;
            Picture(Fill(medal, "Skull", g, g, g, g), "Icon_Skull_Crossbones", Ink);
            return medal;
        }

        /// <summary>The scroll-shaped parchment backdrop of a board, returning the content area inside its rods.</summary>
        public static RectTransform ScrollSheet(RectTransform rt, float side = 104f, float top = 44f, float bottom = 44f)
        {
            Paint(Fill(rt, "Sheet"), "small_scroll_bg_2", 1.25f, null, new Color(0.95f, 0.87f, 0.7f, 1f));
            return Fill(rt, "Content", side, bottom, side, top);
        }

        /// <summary>A section heading in a list: a kit icon, a bold title and an optional dim hint after it.</summary>
        public static void Section(Transform list, string icon, string title, string hint = null, float height = 64f)
        {
            var row = Node(list, "Section " + title);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = height;
            Icon(TopLeft(row, "Icon", 4f, (height - 46f) * 0.5f, 46f, 46f), icon);
            var text = "<b>" + title + "</b>" + (string.IsNullOrEmpty(hint) ? string.Empty : "   <size=60%><color=" + Hex(InkDim) + ">" + hint + "</color></size>");
            var t = Label(Fill(row, "Title", 66f, 0f, 0f, 0f), text, 40f, Ink, TextAlignmentOptions.MidlineLeft, false, 24f);
            t.richText = true;
        }

        /// <summary>A section title with a rule under it: icon, title and an optional hint on the right.</summary>
        public static void SectionHead(Transform list, string icon, string title, string hint = null)
        {
            var row = ListRow(list, title, 70f);
            Icon(TopLeft(row, "Icon", 10f, 8f, 54f, 54f), icon);
            Label(Strip(row, "Title", 78f, 6f, hint != null ? 300f : 10f, 58f), title, 42f, Ink, TextAlignmentOptions.MidlineLeft, true, 26f);
            if (hint != null) Label(TopRight(row, "Hint", 12f, 14f, 290f, 44f), hint, 24f, InkDim, TextAlignmentOptions.MidlineRight, false, 16f);
            var line = BottomStrip(row, "Line", 8f, 0f, 8f, 2f).gameObject.AddComponent<Image>();
            line.color = new Color(0.45f, 0.3f, 0.15f, 0.35f);
            line.raycastTarget = false;
        }

        /// <summary>A wrapping ink line in a list (fixed height).</summary>
        public static TextMeshProUGUI Note(Transform list, string text, float size = 26f, Color? color = null, float height = 56f)
        {
            var row = Node(list, "Note");
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = height;
            var t = Label(Fill(row, "Text", 16f, 0f, 16f, 0f), text, size, color ?? InkDim, TextAlignmentOptions.MidlineLeft, false, size * 0.65f);
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        /// <summary>A wrapping text row whose height follows the text, optionally on a kit sprite (dark-panel colours inked).</summary>
        public static TextMeshProUGUI TextRow(Transform list, string text, float size, Color? color = null, string sprite = null, float thin = 2f, int pad = 18)
        {
            var rt = Node(list, "Text");
            rt.gameObject.AddComponent<LayoutElement>().minHeight = 40f;
            if (sprite != null) Paint(rt, sprite, thin);
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(pad, pad, sprite != null ? 16 : 4, sprite != null ? 16 : 4);
            v.childControlHeight = v.childControlWidth = true;
            v.childForceExpandHeight = false;
            var t = Label(Node(rt, "Label"), InkRich(text), size, color ?? Ink, TextAlignmentOptions.MidlineLeft);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// <summary>A parchment row "icon  label ........ value"; returns the value text.</summary>
        public static TextMeshProUGUI InfoRow(Transform list, string icon, string label, string value)
        {
            var row = ListRow(list, label, 70f, "Panel_Inset", 2.2f);
            Icon(TopLeft(row, "Icon", 18f, 13f, 44f, 44f), icon);
            Label(TopLeft(row, "Label", 76f, 10f, 300f, 50f), label, 28f, Ink, TextAlignmentOptions.MidlineLeft, false, 18f);
            return Label(Strip(row, "Value", 330f, 10f, 22f, 50f), InkRich(value), 27f, Ink, TextAlignmentOptions.MidlineRight, true, 16f);
        }

        /// <summary>A card in a list (Panel_Inset), <paramref name="height"/> tall, with a horizontal layout inside.</summary>
        public static RectTransform Card(Transform list, string name, float height, Color? tint = null)
        {
            var rt = Node(list, name);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = height;
            Paint(rt, "Panel_Inset", 1.6f, tint, tint ?? new Color(0.95f, 0.88f, 0.72f, 1f));
            Row(rt, 22f, new RectOffset(24, 24, 12, 12));
            return rt;
        }

        /// <summary>A vertical scroll list in <paramref name="host"/> with the kit scroll bar (<paramref name="bar"/> wide) on the
        /// right and the list <paramref name="gap"/> short of the right edge; returns the content.</summary>
        public static RectTransform Scroll(RectTransform host, float spacing, RectOffset padding, float bar = 14f, float gap = 24f) =>
            Scroll(host, out _, spacing, padding, bar, gap);

        /// <inheritdoc cref="Scroll(RectTransform, float, RectOffset, float, float)"/>
        public static RectTransform Scroll(RectTransform host, out ScrollRect sr, float spacing, RectOffset padding, float bar = 14f, float gap = 24f)
        {
            sr = host.gameObject.AddComponent<ScrollRect>();
            var viewport = Fill(host, "Viewport", 0f, 0f, gap, 0f);
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
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

            var track = Node(host, "Scrollbar");
            track.anchorMin = new Vector2(1f, 0f);
            track.anchorMax = Vector2.one;
            track.pivot = new Vector2(1f, 0.5f);
            track.sizeDelta = new Vector2(bar, 0f);
            track.anchoredPosition = Vector2.zero;
            Paint(track, "Scroll_Track", 1f, new Color(0.55f, 0.4f, 0.25f, 0.55f), new Color(0.4f, 0.28f, 0.16f, 0.4f)).raycastTarget = true;
            var handle = Fill(Fill(track, "Sliding Area"), "Handle");
            var himg = Paint(handle, "Scroll_Thumb", 1f, null, new Color(0.95f, 0.72f, 0.25f, 1f));
            himg.raycastTarget = true;
            var sb = track.gameObject.AddComponent<Scrollbar>();
            sb.handleRect = handle;
            sb.targetGraphic = himg;
            sb.direction = Scrollbar.Direction.BottomToTop;

            sr.content = content;
            sr.viewport = viewport;
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 30f;
            sr.verticalScrollbar = sb;
            sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        /// <summary>A full-screen dim blocker under <paramref name="panel"/> (clicks around it do not reach the menu).</summary>
        public static void Blocker(RectTransform panel, float alpha = 0.45f)
        {
            var b = Node(panel, "Blocker");
            b.SetAsFirstSibling();
            b.anchorMin = new Vector2(-2f, -2f);
            b.anchorMax = new Vector2(3f, 3f);
            b.offsetMin = b.offsetMax = Vector2.zero;
            b.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, alpha);
        }

        /// <summary>A text the kit screen keeps (another menu writes it): moved into <paramref name="parent"/>, in the kit font and ink.</summary>
        public static void Adopt(TextMeshProUGUI t, RectTransform parent, float size, Vector4 margin)
        {
            if (t == null) return;
            var rt = t.rectTransform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            if (Font != null) t.font = Font;
            t.fontSize = size;
            t.enableAutoSizing = true;
            t.fontSizeMin = Mathf.Max(14f, size * 0.6f);
            t.fontSizeMax = size;
            t.color = Ink;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.margin = margin;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
        }
    }
}
