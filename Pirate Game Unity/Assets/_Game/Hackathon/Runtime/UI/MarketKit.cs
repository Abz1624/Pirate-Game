using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.Hackathon.UI
{
    /// <summary>
    /// Hackathon (13 Oct): layout and kit helpers for the Market, wallet panel, captain name prompt and rewards strip, in
    /// the art team's parchment kit (<see cref="HackSkin"/>): a wood header with a medallion, parchment cards, kit buttons
    /// and input fields. The same patterns as <see cref="DefencePanel"/>'s helpers; sizes are for the 1920x1080 canvas.
    /// </summary>
    static class MarketKit
    {
        public static Color Ink => HackSkin.I.ink;
        public static Color InkDim => HackSkin.I.inkDim;
        public static Color InkGold => HackSkin.I.inkGold;
        public static Color InkGood => HackSkin.I.inkGood;
        public static Color InkBad => HackSkin.I.inkBad;
        public static Color OnWood => HackSkin.I.onWood;
        public static TMP_FontAsset Font => HackSkin.I.font != null ? HackSkin.I.font : HackUi.TitleFont;

        /// <summary>A colour as rich text ("#rrggbb").</summary>
        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>The parchment ink for one of <see cref="HackUi"/>'s status colours (gold, good, bad, dim).</summary>
        public static Color InkFor(Color hackUi)
        {
            if (Near(hackUi, HackUi.Bad)) return InkBad;
            if (Near(hackUi, HackUi.Good)) return InkGood;
            if (Near(hackUi, HackUi.Gold)) return InkGold;
            if (Near(hackUi, HackUi.Dim)) return InkDim;
            return hackUi;
        }

        static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f && Mathf.Abs(a.b - b.b) < 0.02f;

        // ---------------------------------------------------------------- rects

        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform TopLeft(Transform parent, string name, float x, float y, float w, float h)
        {
            var rt = Node(parent, name);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform TopRight(Transform parent, string name, float right, float y, float w, float h)
        {
            var rt = Node(parent, name);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-right, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

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

        public static RectTransform Fill(Transform parent, string name, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            var rt = Node(parent, name);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

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

        // ---------------------------------------------------------------- paint

        /// <summary>Kit sprite <paramref name="sprite"/> on <paramref name="rt"/> (9-sliced when it has borders; <paramref name="thin"/>
        /// above 1 draws the border art smaller). Without the sprite, <paramref name="fallback"/> fills the rect.</summary>
        public static Image Paint(RectTransform rt, string sprite, float thin = 1f, Color? tint = null, Color? fallback = null)
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

        public static Image Icon(RectTransform rt, string sprite, Color? tint = null)
        {
            var img = Paint(rt, sprite, 1f, tint ?? (sprite.StartsWith("Icon_") ? Ink : Color.white), new Color(0f, 0f, 0f, 0f));
            img.preserveAspect = true;
            return img;
        }

        public static TextMeshProUGUI Label(RectTransform rt, string text, float size, Color color, TextAlignmentOptions align, bool bold = false, float minSize = 0f)
        {
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

        // ---------------------------------------------------------------- buttons and fields

        /// <summary>A kit button filling <paramref name="rt"/>: <paramref name="style"/> is Btn_Gold, Btn_Green, Btn_Red,
        /// Btn_Parchment or Btn_Square_Gold; an icon (kit name) sits left of the label; the label may be empty (icon only).</summary>
        public static Button KitButton(RectTransform rt, string label, string icon, Action click, string style = "Btn_Gold", float size = 30f, float iconSize = 40f, float thin = 1f)
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

            // Green and red buttons are dark enough for light text.
            var light = style == "Btn_Green" || style == "Btn_Red";
            var ink = light ? OnWood : Ink;
            var content = Fill(rt, "Content", 10f, 4f, 10f, 2f);
            var h = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = 10f;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            if (!string.IsNullOrEmpty(icon) && HackSkin.S(icon) != null)
            {
                var ic = Node(content, "Icon");
                var iimg = Paint(ic, icon, 1f, ink);
                iimg.preserveAspect = true;
                var le = ic.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = le.minWidth = le.preferredHeight = le.minHeight = iconSize;
            }
            if (!string.IsNullOrEmpty(label))
            {
                var t = Label(Node(content, "Label"), label, size, ink, TextAlignmentOptions.Center, true);
                t.overflowMode = TextOverflowModes.Overflow;
            }
            return b;
        }

        /// <summary>A kit button as a layout child, <paramref name="width"/> wide (0: flexible).</summary>
        public static Button RowButton(Transform row, string label, string icon, Action click, float width, float height, string style = "Btn_Gold", float size = 30f)
            => KitButton(Cell(row, label ?? icon, width, height), label, icon, click, style, size, size * 1.25f);

        public static void SetLabel(Button b, string text)
        {
            var t = b != null ? b.GetComponentInChildren<TextMeshProUGUI>(true) : null;
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

        // ---------------------------------------------------------------- frames

        /// <summary>The wood header of a screen: a medallion with <paramref name="glyph"/> over the left end and the title
        /// in light ink. Returns the title.</summary>
        public static TextMeshProUGUI Header(RectTransform header, string title, string glyph, float titleSize, float medal, float titleRight)
        {
            Paint(header, "Header_Wood", 1.2f, null, new Color(0.36f, 0.22f, 0.1f, 1f));
            var m = TopLeft(header, "Medallion", -medal * 0.08f, -medal * 0.08f, medal, medal);
            var mimg = Paint(m, "Medallion_Frame", 1f, null, new Color(0f, 0f, 0f, 0f));
            mimg.preserveAspect = true;
            if (mimg.sprite != null)
            {
                var inset = medal * 0.23f;
                var disc = Fill(m, "Disc", inset, inset, inset, inset).gameObject.AddComponent<Image>();
                disc.sprite = IslandUi.Circle;
                disc.color = new Color(0.96f, 0.88f, 0.7f, 1f);
                disc.raycastTarget = false;
            }
            var g = medal * 0.3f;
            Icon(Fill(m, "Glyph", g, g, g, g), glyph);
            var t = Label(Fill(header, "Title", medal * 0.98f, 4f, titleRight, 4f), title, titleSize, OnWood, TextAlignmentOptions.MidlineLeft, true, titleSize * 0.55f);
            return t;
        }

        /// <summary>A section heading in a list: a kit icon, a bold title and an optional dim hint.</summary>
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

        /// <summary>A wrapping ink line in a list.</summary>
        public static TextMeshProUGUI Note(Transform list, string text, float size = 26f, Color? color = null, float height = 56f)
        {
            var row = Node(list, "Note");
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = height;
            var t = Label(Fill(row, "Text", 16f, 0f, 16f, 0f), text, size, color ?? InkDim, TextAlignmentOptions.MidlineLeft, false, size * 0.65f);
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
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

        /// <summary>A vertical scroll list in <paramref name="host"/> (kit scroll bar on the right); returns the content.</summary>
        public static RectTransform Scroll(RectTransform host, float spacing, RectOffset padding)
        {
            var sr = host.gameObject.AddComponent<ScrollRect>();
            var viewport = Fill(host, "Viewport", 0f, 0f, 24f, 0f);
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

            var bar = Node(host, "Scrollbar");
            bar.anchorMin = new Vector2(1f, 0f);
            bar.anchorMax = Vector2.one;
            bar.pivot = new Vector2(1f, 0.5f);
            bar.sizeDelta = new Vector2(14f, 0f);
            bar.anchoredPosition = Vector2.zero;
            Paint(bar, "Scroll_Track", 1f, new Color(0.55f, 0.4f, 0.25f, 0.55f), new Color(0.4f, 0.28f, 0.16f, 0.4f)).raycastTarget = true;
            var handle = Fill(Fill(bar, "Sliding Area"), "Handle");
            var himg = Paint(handle, "Scroll_Thumb", 1f, null, new Color(0.95f, 0.72f, 0.25f, 1f));
            himg.raycastTarget = true;
            var sb = bar.gameObject.AddComponent<Scrollbar>();
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

        public static void Clear(Transform list)
        {
            for (var i = list.childCount - 1; i >= 0; i--)
            {
                var c = list.GetChild(i).gameObject;
                c.SetActive(false);
                UnityEngine.Object.Destroy(c);
            }
        }

        /// <summary>A parchment status line (the panel's messages in ink; the strip tints red, green or amber).</summary>
        public static TextMeshProUGUI StatusLine(RectTransform strip, float size = 26f)
        {
            Paint(strip, "Panel_Inset", 3f, new Color(0.93f, 0.84f, 0.66f, 1f), new Color(0.9f, 0.8f, 0.62f, 1f));
            var tint = Fill(strip, "Tint", 6f, 5f, 6f, 5f).gameObject.AddComponent<Image>();
            tint.raycastTarget = false;
            var t = Label(Fill(strip, "Text", 22f, 2f, 22f, 2f), string.Empty, size, InkDim, TextAlignmentOptions.MidlineLeft, false, size * 0.6f);
            t.textWrappingMode = TextWrappingModes.Normal;
            var s = t.gameObject.AddComponent<HackStatus>();
            s.Background = tint;
            s.Parchment = true;
            return t;
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
    }
}
