using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.Hackathon.UI
{
    /// <summary>
    /// Hackathon UI (13 Oct): layout and kit helpers for the leaderboard screens (daily race panel, race HUD and results,
    /// weekly rewards strip), drawn with the parchment kit (<see cref="HackSkin"/>) after the owner's reference mockups.
    /// Same patterns as the Defence designer's helpers.
    /// </summary>
    internal static class BoardKit
    {
        public static Color Ink => HackSkin.I.ink;
        public static Color InkDim => HackSkin.I.inkDim;
        public static Color InkGold => HackSkin.I.inkGold;
        public static Color InkGood => HackSkin.I.inkGood;
        public static Color InkBad => HackSkin.I.inkBad;
        public static Color OnWood => HackSkin.I.onWood;

        /// <summary>Rich-text colours for ink on parchment.</summary>
        public const string GoldHex = "#8c4d05", DimHex = "#7a5a3a", GoodHex = "#2b7324", BadHex = "#a82917";

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

        /// <summary>Kit sprite on <paramref name="rt"/> (9-sliced when it has borders; <paramref name="thin"/> above 1 draws
        /// the border art smaller). Without the sprite, <paramref name="fallback"/> fills the rect.</summary>
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

        /// <summary>A simple (not sliced) kit sprite, aspect kept.</summary>
        public static Image Picture(RectTransform rt, string sprite, Color? tint = null)
        {
            var img = Paint(rt, sprite, 1f, tint, new Color(0f, 0f, 0f, 0f));
            img.type = Image.Type.Simple;
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

        public static void Line(Transform parent, float left, float y, float right)
        {
            var img = Strip(parent, "Line", left, y, right, 2f).gameObject.AddComponent<Image>();
            img.color = new Color(0.45f, 0.3f, 0.15f, 0.35f);
            img.raycastTarget = false;
        }

        /// <summary>A kit button filling <paramref name="rt"/> (Btn_Gold, Btn_Parchment or Btn_Square_Gold) with an
        /// optional kit icon left of the label.</summary>
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

            var content = Fill(rt, "Content", 10f, 4f, 10f, 2f);
            var h = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = 12f;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            if (!string.IsNullOrEmpty(icon) && HackSkin.S(icon) != null)
            {
                var ic = Node(content, "Icon");
                Picture(ic, icon, Ink);
                var le = ic.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = le.minWidth = le.preferredHeight = le.minHeight = iconSize;
            }
            if (!string.IsNullOrEmpty(label))
                Label(Node(content, "Label"), label, size, Ink, TextAlignmentOptions.MidlineLeft, true);
            return b;
        }

        public static void SetLabel(Button b, string text)
        {
            var t = b != null ? b.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            if (t != null) t.text = text;
        }

        /// <summary>The medallion with the skull (a parchment disc under the dark glyph), as the Defence header.</summary>
        public static RectTransform Medallion(Transform parent, float x, float y, float size)
        {
            var medal = TopLeft(parent, "Medallion", x, y, size, size);
            var mimg = Picture(medal, "Medallion_Frame");
            var inset = size * 0.23f;
            if (mimg.sprite != null) Picture(Fill(medal, "Disc", inset, inset, inset, inset), "Dot_Beige", new Color(0.96f, 0.88f, 0.7f, 1f));
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

        /// <summary>A vertical scroll list in <paramref name="host"/> (kit scroll bar on the right); returns the content.</summary>
        public static RectTransform Scroll(RectTransform host, float spacing, RectOffset padding)
        {
            var sr = host.gameObject.AddComponent<ScrollRect>();
            var viewport = Fill(host, "Viewport", 0f, 0f, 22f, 0f);
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

        /// <summary>A table row (or the header bar): a horizontal layout with the table's side padding.</summary>
        public static RectTransform Row(Transform list, float height, string sprite, float thin, Color? tint)
        {
            var rt = Node(list, "Row");
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = height;
            if (sprite != null) Paint(rt, sprite, thin, tint, tint ?? new Color(0.95f, 0.88f, 0.72f, 1f));
            Columns(rt);
            return rt;
        }

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

        /// <summary>A cell <paramref name="width"/> wide (0: takes the rest).</summary>
        public static RectTransform Cell(Transform row, float width)
        {
            var rt = Node(row, "Cell");
            var le = rt.gameObject.AddComponent<LayoutElement>();
            if (width > 0f) le.preferredWidth = le.minWidth = width;
            else { le.flexibleWidth = 1f; le.minWidth = 80f; }
            return rt;
        }

        public static TextMeshProUGUI TextCell(Transform row, string text, float width, float size, TextAlignmentOptions align, Color color, bool bold = false) =>
            Label(Cell(row, width), text, size, color, align, bold, size * 0.6f);

        /// <summary>The rank cell: "#n" (or n) with the kit crown for places 1 to 3.</summary>
        public static void RankCell(Transform row, int rank, float width, float size, bool hash)
        {
            var cell = Cell(row, width);
            var crown = rank == 1 ? "Crown_Gold" : rank == 2 ? "Crown_Silver" : rank == 3 ? "Crown_Bronze" : null;
            if (crown != null && HackSkin.S(crown) != null)
            {
                var c = Node(cell, "Crown");
                c.anchorMin = new Vector2(0f, 0.12f);
                c.anchorMax = new Vector2(0f, 0.88f);
                c.pivot = new Vector2(0f, 0.5f);
                c.sizeDelta = new Vector2(52f, 0f);
                c.anchoredPosition = Vector2.zero;
                Picture(c, crown);
            }
            Label(Fill(cell, "Rank", 60f), (hash ? "#" : string.Empty) + rank, size, Ink, TextAlignmentOptions.MidlineLeft, true);
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

        public static void Clear(Transform list)
        {
            for (var i = list.childCount - 1; i >= 0; i--)
            {
                var c = list.GetChild(i).gameObject;
                c.SetActive(false);
                UnityEngine.Object.Destroy(c);
            }
        }

        /// <summary>"5SBg...jH9A" for a long wallet address.</summary>
        public static string ShortWallet(string wallet) =>
            string.IsNullOrEmpty(wallet) ? "-" : wallet.Length > 12 ? wallet.Substring(0, 4) + "..." + wallet.Substring(wallet.Length - 4) : wallet;
    }
}
