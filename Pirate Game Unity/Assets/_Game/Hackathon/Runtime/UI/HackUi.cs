using System;
using RoguePirates.V4.Presentation.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.Hackathon.UI
{
    /// <summary>
    /// Hackathon (12 Oct): a copy of V4's online panel building blocks (<c>OnlineUi</c>, internal to V4's presentation
    /// assembly), so the hackathon panels match the online menus' look (dark navy panel, cream text, the settings' amber
    /// 72-unit buttons, the art menu's font for titles) without changing V4.
    /// </summary>
    static class HackUi
    {
        public static readonly Color PanelColor = new Color(0.06f, 0.1f, 0.16f, 0.97f);
        public static readonly Color TextColor = new Color(0.95f, 0.93f, 0.88f, 1f);
        public static readonly Color Dim = new Color(0.7f, 0.72f, 0.75f, 1f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.4f, 1f);
        public static readonly Color Good = new Color(0.45f, 0.85f, 0.45f, 1f);
        public static readonly Color Bad = new Color(1f, 0.55f, 0.45f, 1f);
        public const float RowHeight = 72f;

        static TMP_FontAsset _titleFont;
        static bool _fontLooked;

        /// <summary>The art menu's font (null: TMP's default).</summary>
        public static TMP_FontAsset TitleFont
        {
            get
            {
                if (_fontLooked) return _titleFont;
                _fontLooked = true;
                var s = ArtMenuSettings.Load();
                _titleFont = s != null ? s.font : null;
                return _titleFont;
            }
        }

        /// <summary>A panel over the canvas (anchored between <paramref name="min"/> and <paramref name="max"/>), drawn on top,
        /// with an invisible blocker so clicks around it do not reach the menu behind.</summary>
        public static RectTransform Panel(Transform canvas, string name, Vector2 min, Vector2 max, int order)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvas, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = PanelColor;
            SettingsPanel.DrawOnTop(go, order);
            var blocker = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
            blocker.transform.SetParent(rt, false);
            var brt = (RectTransform)blocker.transform;
            brt.anchorMin = new Vector2(-2f, -2f);
            brt.anchorMax = new Vector2(3f, 3f);
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            blocker.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
            go.SetActive(false);
            return rt;
        }

        /// <summary>A child stretched between anchors with pixel insets (left, bottom, right, top).</summary>
        public static RectTransform Area(Transform parent, string name, Vector2 min, Vector2 max, Vector4 inset = default)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = new Vector2(inset.x, inset.y);
            rt.offsetMax = new Vector2(-inset.z, -inset.w);
            return rt;
        }

        /// <summary>A strip <paramref name="height"/> tall at <paramref name="fromTop"/> below the panel's top edge.</summary>
        public static RectTransform TopStrip(Transform parent, string name, float fromTop, float height, float sideInset = 24f)
        {
            var rt = Area(parent, name, new Vector2(0f, 1f), new Vector2(1f, 1f));
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(sideInset, -fromTop - height);
            rt.offsetMax = new Vector2(-sideInset, -fromTop);
            return rt;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, TextAlignmentOptions align, Color color, bool title = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<TextMeshProUGUI>();
            if (title && TitleFont != null) t.font = TitleFont;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        /// <summary>A text placed between anchors in its parent.</summary>
        public static TextMeshProUGUI TextAt(Transform parent, string name, string text, float size, TextAlignmentOptions align, Color color, Vector2 min, Vector2 max, bool title = false)
        {
            var t = Text(parent, name, text, size, align, color, title);
            var rt = t.rectTransform;
            rt.anchorMin = min;
            rt.anchorMax = max;
            return t;
        }

        /// <summary>The settings' amber button (72 units), placed between anchors when given.</summary>
        public static Button Button(Transform parent, string label, Action click, Vector2? min = null, Vector2? max = null)
        {
            var b = SettingsPanel.CreateButton(parent, label, click);
            b.name = label;
            if (min.HasValue && max.HasValue)
            {
                var rt = (RectTransform)b.transform;
                rt.anchorMin = min.Value;
                rt.anchorMax = max.Value;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                var le = b.GetComponent<LayoutElement>();
                if (le != null) le.ignoreLayout = true;
            }
            return b;
        }

        public static void SetLabel(Button b, string text)
        {
            var t = b != null ? b.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            if (t != null) t.text = text;
        }

        /// <summary>A vertical scroll list filling <paramref name="parent"/>; returns the content rows go into.</summary>
        public static RectTransform ScrollList(Transform parent, string name, Vector4 inset)
        {
            var scroll = new GameObject(name, typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(RectMask2D));
            scroll.transform.SetParent(parent, false);
            var srt = (RectTransform)scroll.transform;
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(inset.x, inset.y);
            srt.offsetMax = new Vector2(-inset.z, -inset.w);
            scroll.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.18f);
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(scroll.transform, false);
            var list = (RectTransform)content.transform;
            list.anchorMin = new Vector2(0f, 1f);
            list.anchorMax = new Vector2(1f, 1f);
            list.pivot = new Vector2(0.5f, 1f);
            list.sizeDelta = Vector2.zero;
            var v = content.GetComponent<VerticalLayoutGroup>();
            v.spacing = 4f;
            v.padding = new RectOffset(8, 8, 8, 8);
            v.childControlHeight = v.childControlWidth = true;
            v.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = scroll.GetComponent<ScrollRect>();
            sr.content = list;
            sr.viewport = srt;
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 30f;
            return list;
        }

        /// <summary>A horizontal row in a list (<paramref name="height"/> tall, tinted).</summary>
        public static RectTransform Row(Transform list, float height, Color tint)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(list, false);
            go.GetComponent<Image>().color = tint;
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = height;
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(16, 12, 4, 4);
            h.spacing = 12f;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            h.childAlignment = TextAnchor.MiddleLeft;
            return (RectTransform)go.transform;
        }

        /// <summary>A cell in a row: fixed <paramref name="width"/>, or flexible when width is 0.</summary>
        public static TextMeshProUGUI Cell(Transform row, string text, float width, float size, TextAlignmentOptions align, Color color)
        {
            var t = Text(row, "Cell", text, size, align, color);
            var le = t.gameObject.AddComponent<LayoutElement>();
            if (width > 0f) { le.preferredWidth = width; le.minWidth = width; }
            else { le.flexibleWidth = 1f; le.minWidth = 100f; }
            return t;
        }

        /// <summary>A single-line name field (TMP input), stretched in <paramref name="parent"/>.</summary>
        public static TMP_InputField Input(Transform parent, string placeholder, int characterLimit)
        {
            var go = new GameObject("NameInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.95f, 0.93f, 0.88f, 1f);
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = RowHeight;
            le.flexibleWidth = 1f;
            var area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(go.transform, false);
            var art = (RectTransform)area.transform;
            art.anchorMin = Vector2.zero;
            art.anchorMax = Vector2.one;
            art.offsetMin = new Vector2(16f, 6f);
            art.offsetMax = new Vector2(-16f, -6f);
            var ph = Text(area.transform, "Placeholder", placeholder, 30, TextAlignmentOptions.MidlineLeft, new Color(0.3f, 0.3f, 0.3f, 0.6f));
            ph.fontStyle = FontStyles.Italic;
            var text = Text(area.transform, "Text", string.Empty, 30, TextAlignmentOptions.MidlineLeft, new Color(0.08f, 0.06f, 0.04f, 1f));
            text.overflowMode = TextOverflowModes.Overflow;
            var input = go.GetComponent<TMP_InputField>();
            input.textViewport = art;
            input.textComponent = text;
            input.placeholder = ph;
            input.characterLimit = characterLimit;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = TMP_InputField.ContentType.Standard;
            input.richText = false;
            input.fontAsset = text.font;
            input.pointSize = 30;
            return input;
        }
    }
}
