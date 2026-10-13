using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.Hackathon.UI
{
    /// <summary>
    /// Hackathon UI (13 Oct): the parchment kit's building blocks for the 1v1 screens (<see cref="WagerMenu"/>,
    /// <see cref="MatchExtras"/>), the same patterns as <see cref="DefencePanel"/>: wood header, parchment card, inset
    /// rows, gold and parchment buttons with a kit icon, ink text. Sprites come from <see cref="HackSkin"/> by name.
    /// </summary>
    static class PvpKit
    {
        public static Color Ink => HackSkin.I.ink;
        public static Color InkDim => HackSkin.I.inkDim;
        public static Color InkGold => HackSkin.I.inkGold;
        public static Color InkGood => HackSkin.I.inkGood;
        public static Color InkBad => HackSkin.I.inkBad;
        public static Color OnWood => HackSkin.I.onWood;
        public static TMP_FontAsset Font => HackSkin.I.font != null ? HackSkin.I.font : HackUi.TitleFont;

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

        /// <summary>A kit icon (ink tinted for "Icon_*" glyphs, untinted art otherwise).</summary>
        public static Image Icon(Transform parent, string sprite, float x, float y, float size, Color? tint = null)
        {
            var img = Paint(TopLeft(parent, sprite, x, y, size, size), sprite, 1f, tint ?? (sprite.StartsWith("Icon_") ? Ink : Color.white), new Color(0f, 0f, 0f, 0f));
            img.preserveAspect = true;
            return img;
        }

        /// <summary>A kit icon centred vertically at <paramref name="x"/> from the left of a row.</summary>
        public static Image RowIcon(Transform row, string sprite, float x, float size, Color? tint = null)
        {
            var rt = Node(row, sprite);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(size, size);
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

        /// <summary>A kit button filling <paramref name="rt"/> (Btn_Gold, Btn_Parchment, Btn_Green, Btn_Red or Btn_Square_*),
        /// an optional kit icon left of the label.</summary>
        public static Button KitButton(RectTransform rt, string label, string icon, Action click, string style = "Btn_Gold", float size = 30f, float iconSize = 40f, float thin = 1f)
        {
            var img = Paint(rt, style, thin, null, new Color(0.95f, 0.75f, 0.3f, 1f));
            img.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            SetStyle(b, style, thin);
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
                var iimg = Paint(ic, icon, 1f, icon.StartsWith("Icon_") ? Ink : Color.white);
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

        /// <summary>Swaps a kit button's look (for example the picked stake turns gold).</summary>
        public static void SetStyle(Button b, string style, float thin = 1f)
        {
            if (b == null) return;
            var img = b.targetGraphic as Image;
            var s = HackSkin.S(style);
            if (img != null && s != null)
            {
                img.sprite = s;
                img.type = s.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
                img.pixelsPerUnitMultiplier = thin;
                img.color = Color.white;
            }
            var pressed = HackSkin.S(style == "Btn_Gold" ? "Btn_Gold_Pressed" : null);
            var disabled = HackSkin.S(style == "Btn_Gold" ? "Btn_Gold_Disabled" : style.StartsWith("Btn_Square") ? "Btn_Square_Disabled" : null);
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

        /// <summary>The wood header across the top of a panel: skull, title, and the panel's close or leave button.</summary>
        public static TextMeshProUGUI Header(RectTransform panel, string title, float height, out RectTransform head)
        {
            head = Strip(panel, "Header", 0f, 0f, 0f, height);
            Paint(head, "Header_Wood", 1.4f, null, new Color(0.32f, 0.2f, 0.1f, 0.97f));
            var skull = Paint(TopLeft(head, "Skull", 34f, (height - 92f) * 0.5f, 104f, 92f), "Icon_Skull_Crossbones", 1f, OnWood, new Color(0f, 0f, 0f, 0f));
            skull.preserveAspect = true;
            return Label(Strip(head, "Title", 160f, 0f, 700f, height), title, 58f, OnWood, TextAlignmentOptions.MidlineLeft, true, 34f);
        }

        /// <summary>A vertical scroll list in <paramref name="host"/> (kit scroll bar on the right); returns the content.</summary>
        public static RectTransform Scroll(RectTransform host, float spacing, RectOffset padding)
        {
            var sr = host.gameObject.AddComponent<ScrollRect>();
            var viewport = Fill(host, "Viewport", 0f, 0f, 20f, 0f);
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
            bar.sizeDelta = new Vector2(12f, 0f);
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

        public static RectTransform ListRow(Transform list, string name, float height, string sprite, float thin, Color? tint = null)
        {
            var rt = Node(list, name);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = height;
            if (sprite != null) Paint(rt, sprite, thin, tint, tint ?? new Color(0.95f, 0.88f, 0.72f, 1f));
            return rt;
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

        /// <summary>A V4 text the kit screen keeps (the menus write it): moved into <paramref name="parent"/>, in the kit font and ink.</summary>
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

        /// <summary>The ink colour for a message colour from the old dark-panel palette (gold, red, green).</summary>
        public static Color InkFor(Color c)
        {
            if (Near(c, HackUi.Bad)) return InkBad;
            if (Near(c, HackUi.Good)) return InkGood;
            if (Near(c, HackUi.Gold)) return InkGold;
            return Ink;
        }

        static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f && Mathf.Abs(a.b - b.b) < 0.02f;
    }
}
