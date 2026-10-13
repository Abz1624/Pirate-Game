using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.Hackathon.UI
{
    /// <summary>
    /// Hackathon UI (13 Oct): layout and kit helpers of the Islands screen, drawn in the art team's parchment kit
    /// (<see cref="HackSkin"/>) like the Defence designer. Sprites are found by name in the skin first, then under
    /// Resources/IslandArt (the RUM barrel and the cannon art of the building rows).
    /// </summary>
    static class IslandKit
    {
        public static Color Ink => HackSkin.I.ink;
        public static Color InkDim => HackSkin.I.inkDim;
        public static Color InkGold => HackSkin.I.inkGold;
        public static Color InkGood => HackSkin.I.inkGood;
        public static Color InkBad => HackSkin.I.inkBad;
        public static Color OnWood => HackSkin.I.onWood;
        public static TMP_FontAsset Font => HackSkin.I.font != null ? HackSkin.I.font : HackUi.TitleFont;

        static readonly Dictionary<string, Sprite> Art = new Dictionary<string, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Art.Clear();

        /// <summary>A kit sprite, or Resources/IslandArt/<paramref name="name"/>.</summary>
        public static Sprite Find(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var s = HackSkin.S(name);
            if (s != null) return s;
            if (!Art.TryGetValue(name, out s))
            {
                s = Resources.Load<Sprite>("IslandArt/" + name);
                Art[name] = s;
            }
            return s;
        }

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

        public static RectTransform Fill(Transform parent, string name, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            var rt = Node(parent, name);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Columns(Transform parent, string name, float x0, float x1, float padLeft, float padRight)
        {
            var rt = Node(parent, name);
            rt.anchorMin = new Vector2(x0, 0f);
            rt.anchorMax = new Vector2(x1, 1f);
            rt.offsetMin = new Vector2(padLeft, 0f);
            rt.offsetMax = new Vector2(-padRight, 0f);
            return rt;
        }

        // ---------------------------------------------------------------- art and text

        /// <summary>Sprite <paramref name="sprite"/> on <paramref name="rt"/> (9-sliced when it has borders); without it,
        /// <paramref name="fallback"/> fills the rect.</summary>
        public static Image Paint(RectTransform rt, string sprite, float thin = 1f, Color? tint = null, Color? fallback = null)
        {
            var img = rt.GetComponent<Image>();
            if (img == null) img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            var s = Find(sprite);
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

        /// <summary>An icon or picture; kit glyphs ("Icon_...") are tinted with <paramref name="glyph"/>.</summary>
        public static Image Icon(RectTransform rt, string sprite, Color? glyph = null)
        {
            var ink = sprite.StartsWith("Icon_") && !sprite.EndsWith("_Green");
            var img = Paint(rt, sprite, 1f, ink ? glyph ?? Ink : Color.white, new Color(0f, 0f, 0f, 0f));
            img.preserveAspect = true;
            return img;
        }

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

        /// <summary>The server's and the raid HUD's light rich-text colours (made for dark panels) in parchment inks.</summary>
        public static string Inkify(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Replace("#7fd68b", Hex(InkGood)).Replace("#ffd166", Hex(InkGold)).Replace("#ff8c73", Hex(InkBad))
                .Replace("#9aa4ad", Hex(InkDim)).Replace("#7fc8ff", "#2a5f8f").Replace("#c08cff", "#6a3d9a");
        }

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        // ---------------------------------------------------------------- buttons

        /// <summary>A kit button filling <paramref name="rt"/> (Btn_Gold, Btn_Parchment, Btn_Red...); an optional icon sits
        /// left of the label.</summary>
        public static Button KitButton(RectTransform rt, string label, string icon, Action click, string style = "Btn_Gold", float size = 32f, float iconSize = 40f)
        {
            var img = Paint(rt, style, 1f, null, new Color(0.95f, 0.75f, 0.3f, 1f));
            img.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            SetStyle(b, style);
            if (click != null) b.onClick.AddListener(() => click());

            var content = Fill(rt, "Content", 12f, 4f, 12f, 2f);
            var h = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = 12f;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            if (!string.IsNullOrEmpty(icon) && Find(icon) != null)
            {
                var ic = Node(content, "Icon");
                Icon(ic, icon);
                var le = ic.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = le.minWidth = le.preferredHeight = le.minHeight = iconSize;
            }
            if (!string.IsNullOrEmpty(label))
            {
                var t = Label(Node(content, "Label"), label, size, Ink, TextAlignmentOptions.Midline, true);
                t.textWrappingMode = TextWrappingModes.Normal;
                t.lineSpacing = -12f;
            }
            return b;
        }

        /// <summary>Re-skins a kit button (tabs: Btn_Gold when active, Btn_Parchment when not).</summary>
        public static void SetStyle(Button b, string style)
        {
            if (b == null) return;
            var img = (Image)b.targetGraphic;
            var s = Find(style);
            if (s == null)
            {
                img.color = style == "Btn_Gold" ? new Color(0.95f, 0.75f, 0.3f, 1f) : new Color(0.85f, 0.75f, 0.58f, 1f);
                return;
            }
            img.sprite = s;
            img.type = s.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            img.color = Color.white;
            var pressed = Find(style == "Btn_Gold" ? "Btn_Gold_Pressed" : null);
            var disabled = Find(style == "Btn_Gold" ? "Btn_Gold_Disabled" : null);
            if (pressed != null || disabled != null)
            {
                b.transition = Selectable.Transition.SpriteSwap;
                b.spriteState = new SpriteState { pressedSprite = pressed != null ? pressed : s, highlightedSprite = s, selectedSprite = s, disabledSprite = disabled };
            }
            else
            {
                b.transition = Selectable.Transition.ColorTint;
                var c = b.colors;
                c.normalColor = c.highlightedColor = c.selectedColor = Color.white;
                c.pressedColor = new Color(0.85f, 0.8f, 0.7f, 1f);
                c.disabledColor = new Color(1f, 1f, 1f, 0.5f);
                b.colors = c;
            }
        }

        public static void SetLabel(Button b, string text)
        {
            var t = b != null ? b.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            if (t != null) t.text = text;
        }

        // ---------------------------------------------------------------- list

        /// <summary>A vertical scroll list in <paramref name="host"/> (kit scroll bar on the right); returns the content.</summary>
        public static RectTransform Scroll(RectTransform host, out ScrollRect sr, float spacing, RectOffset padding)
        {
            var sr0 = host.gameObject.AddComponent<ScrollRect>();
            var viewport = Fill(host, "Viewport", 0f, 0f, 26f, 0f);
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
            bar.sizeDelta = new Vector2(16f, 0f);
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

        /// <summary>A fixed-height list row, optionally painted with a kit sprite.</summary>
        public static RectTransform ListRow(Transform list, string name, float height, string sprite = null, float thin = 1f, Color? tint = null)
        {
            var rt = Node(list, name);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = height;
            if (sprite != null) Paint(rt, sprite, thin, tint, tint ?? new Color(0.95f, 0.88f, 0.72f, 1f));
            return rt;
        }

        /// <summary>A wrapping text row whose height follows the text, optionally on a kit sprite.</summary>
        public static TextMeshProUGUI TextRow(Transform list, string text, float size, Color? color = null, string sprite = null, float thin = 2f, int pad = 18)
        {
            var rt = Node(list, "Text");
            rt.gameObject.AddComponent<LayoutElement>().minHeight = 40f;
            if (sprite != null) Paint(rt, sprite, thin);
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(pad, pad, sprite != null ? 16 : 4, sprite != null ? 16 : 4);
            v.childControlHeight = v.childControlWidth = true;
            v.childForceExpandHeight = false;
            var t = Label(Node(rt, "Label"), Inkify(text), size, color ?? Ink, TextAlignmentOptions.MidlineLeft);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

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

        /// <summary>A section title: icon, title and an optional hint on the right.</summary>
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

        /// <summary>A parchment row "icon  label ........ value"; returns the value text.</summary>
        public static TextMeshProUGUI InfoRow(Transform list, string icon, string label, string value)
        {
            var row = ListRow(list, label, 70f, "Panel_Inset", 2.2f);
            Icon(TopLeft(row, "Icon", 18f, 13f, 44f, 44f), icon);
            Label(TopLeft(row, "Label", 76f, 10f, 300f, 50f), label, 28f, Ink, TextAlignmentOptions.MidlineLeft, false, 18f);
            return Label(Strip(row, "Value", 330f, 10f, 22f, 50f), Inkify(value), 27f, Ink, TextAlignmentOptions.MidlineRight, true, 16f);
        }

        /// <summary>A tag pill (Tag_Green, Tag_Red, Tag_Blue, Tag_Tan) with a label.</summary>
        public static void Tag(RectTransform rt, string sprite, string text, float size = 26f)
        {
            Paint(rt, sprite, 1f, null, new Color(0.85f, 0.75f, 0.55f, 1f));
            Label(Fill(rt, "Label", 10f, 2f, 10f, 2f), text, size, Ink, TextAlignmentOptions.Center, true, 14f);
        }

        /// <summary>The island picture frame: a blue tile with a sand island, the NFT image over it once loaded.</summary>
        public static void IslandPicture(RectTransform frame, string url)
        {
            Paint(frame, "Slot_Card", 2.4f, new Color(0.4f, 0.65f, 0.9f, 1f), new Color(0.15f, 0.42f, 0.65f, 1f));
            var sand = Fill(frame, "Island", frame.sizeDelta.x * 0.24f, frame.sizeDelta.y * 0.24f, frame.sizeDelta.x * 0.24f, frame.sizeDelta.y * 0.24f).gameObject.AddComponent<Image>();
            sand.sprite = IslandUi.Circle;
            sand.color = new Color(0.93f, 0.84f, 0.58f, 1f);
            sand.raycastTarget = false;
            var green = Fill(frame, "Green", frame.sizeDelta.x * 0.33f, frame.sizeDelta.y * 0.33f, frame.sizeDelta.x * 0.33f, frame.sizeDelta.y * 0.33f).gameObject.AddComponent<Image>();
            green.sprite = IslandUi.Circle;
            green.color = new Color(0.36f, 0.7f, 0.45f, 1f);
            green.raycastTarget = false;
            if (!string.IsNullOrEmpty(url)) IslandUi.PictureIn(Fill(frame, "Nft", 10f, 10f, 10f, 10f), url);
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

        /// <summary>"5 minutes ago", "2 hours ago", "1 day ago".</summary>
        public static string Ago(DateTime? utc)
        {
            if (!utc.HasValue) return string.Empty;
            var s = (DateTime.UtcNow - utc.Value).TotalSeconds;
            if (s < 60) return "just now";
            if (s < 3600) return Plural((int)(s / 60), "minute");
            if (s < 86400) return Plural((int)(s / 3600), "hour");
            return Plural((int)(s / 86400), "day");
        }

        static string Plural(int n, string unit) => n + " " + unit + (n == 1 ? string.Empty : "s") + " ago";
    }
}
