using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace RoguePirates.Hackathon.UI
{
    /// <summary>
    /// Hackathon (12 Oct, U-3): building blocks of the islands and market screens on top of <see cref="HackUi"/>: rows of
    /// text, buttons, a RUM bar and pictures in a scroll list; a round marker sprite; the NFT picture cache.
    /// </summary>
    static class IslandUi
    {
        public static readonly Color RowTint = new Color(1f, 1f, 1f, 0.05f);
        public static readonly Color MineTint = new Color(1f, 0.82f, 0.4f, 0.16f);
        public const float ButtonRow = 84f;

        static Sprite _circle;

        /// <summary>A soft-edged white disc (marker and dot art, tinted per use).</summary>
        public static Sprite Circle
        {
            get
            {
                if (_circle != null) return _circle;
                const int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[size * size];
                var r = size * 0.5f - 1f;
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var dx = x + 0.5f - size * 0.5f;
                    var dy = y + 0.5f - size * 0.5f;
                    var a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
                tex.SetPixels32(px);
                tex.Apply();
                _circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                return _circle;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _circle = null; Pictures.Clear(); }

        // ---------------------------------------------------------------- rows

        /// <summary>A wrapping text row (its height follows the text, at least <paramref name="minHeight"/>).</summary>
        public static TextMeshProUGUI TextRow(Transform list, string text, float size = 26f, Color? color = null, float minHeight = 44f, Color? tint = null)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(VerticalLayoutGroup));
            go.transform.SetParent(list, false);
            go.GetComponent<Image>().color = tint ?? new Color(0f, 0f, 0f, 0f);
            go.GetComponent<Image>().raycastTarget = false;
            go.GetComponent<LayoutElement>().minHeight = minHeight;
            var v = go.GetComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(16, 16, 6, 6);
            v.childControlHeight = v.childControlWidth = true;
            v.childForceExpandHeight = false;
            var t = HackUi.Text(go.transform, "Label", text, size, TextAlignmentOptions.MidlineLeft, color ?? HackUi.TextColor);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// <summary>A row of buttons (equal widths unless given).</summary>
        public static List<Button> ButtonRowOf(Transform list, params (string label, Action click)[] buttons)
        {
            var row = HackUi.Row(list, ButtonRow, new Color(0f, 0f, 0f, 0f));
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.childForceExpandWidth = true;
            h.padding = new RectOffset(8, 8, 6, 6);
            var made = new List<Button>();
            foreach (var (label, click) in buttons)
            {
                var b = HackUi.Button(row, label, click);
                var le = b.GetComponent<LayoutElement>();
                le.flexibleWidth = 1f;
                le.minWidth = 120f;
                made.Add(b);
            }
            return made;
        }

        /// <summary>A button sized for a <see cref="HackUi.Row"/>.</summary>
        public static Button RowButton(Transform row, string label, Action click, float width = 200f)
        {
            var b = HackUi.Button(row, label, click);
            var le = b.GetComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = width;
            le.flexibleWidth = 0f;
            return b;
        }

        /// <summary>A labelled bar (0..1 fill) with a text over it.</summary>
        public static (Image fill, TextMeshProUGUI label) Bar(Transform list, float fill, string text, Color color)
        {
            var go = new GameObject("Bar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(list, false);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.4f);
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = 46f;
            var f = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            f.transform.SetParent(go.transform, false);
            var frt = (RectTransform)f.transform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = new Vector2(Mathf.Clamp01(fill), 1f);
            frt.offsetMin = new Vector2(3f, 3f);
            frt.offsetMax = new Vector2(-3f, -3f);
            var img = f.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var t = HackUi.Text(go.transform, "Label", text, 24, TextAlignmentOptions.Center, HackUi.TextColor);
            t.fontStyle = FontStyles.Bold;
            return (img, t);
        }

        public static void SetFill(Image fill, float value)
        {
            if (fill == null) return;
            ((RectTransform)fill.transform).anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        }

        /// <summary>A picture (<paramref name="size"/> square) loaded from <paramref name="url"/> into a row.</summary>
        public static RawImage Picture(Transform row, string url, float size)
        {
            var go = new GameObject("Picture", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
            go.transform.SetParent(row, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = size;
            le.preferredHeight = le.minHeight = size;
            le.flexibleWidth = 0f;
            var img = go.GetComponent<RawImage>();
            img.color = new Color(1f, 1f, 1f, 0.08f);
            img.raycastTarget = false;
            _ = LoadInto(img, url);
            return img;
        }

        static readonly Dictionary<string, Texture2D> Pictures = new Dictionary<string, Texture2D>();

        static async Task LoadInto(RawImage img, string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            if (!Pictures.TryGetValue(url, out var tex) || tex == null)
            {
                using (var req = UnityWebRequestTexture.GetTexture(url))
                {
                    try { await req.SendWebRequest(); }
                    catch (Exception) { /* read below */ }
                    if (req.result != UnityWebRequest.Result.Success) { Debug.LogWarning("IslandUi picture " + url + ": " + req.error); return; }
                    tex = DownloadHandlerTexture.GetContent(req);
                    Pictures[url] = tex;
                }
            }
            if (img == null) return;
            img.texture = tex;
            img.color = Color.white;
        }

        /// <summary>An address shortened for a label ("5SBg...jH9A").</summary>
        public static string Short(string address) => string.IsNullOrEmpty(address) ? "-" : Web3Rules.ShortAddress(address);
    }
}
