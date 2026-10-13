using System;
using RoguePirates.UI;
using RoguePirates.V4.Presentation.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.Hackathon.UI
{
    /// <summary>
    /// Hackathon (12 Oct): the hackathon panels' frame (an on-top panel with a click blocker), the status colours the
    /// panels pass around (<see cref="InkFor"/> maps them to parchment inks) and the art menu's font as the fallback font.
    /// 13 Oct: the old navy rows, amber buttons, grey scroll lists and cream inputs went; the screens draw with the
    /// parchment kit. V5 M9i: the kit is the shared <see cref="UiKit"/> (RoguePirates.UI); the few pieces only the
    /// hackathon screens use (the medallion header, the status strip, the island picture) are here.
    /// </summary>
    static class HackUi
    {
        public static readonly Color PanelColor = new Color(0.06f, 0.1f, 0.16f, 0.97f);
        public static readonly Color Dim = new Color(0.7f, 0.72f, 0.75f, 1f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.4f, 1f);
        public static readonly Color Good = new Color(0.45f, 0.85f, 0.45f, 1f);
        public static readonly Color Bad = new Color(1f, 0.55f, 0.45f, 1f);

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

        /// <summary>The tint of a status strip for a text colour.</summary>
        public static Color StatusTint(Color text)
        {
            if (Near(text, Bad)) return new Color(0.75f, 0.18f, 0.12f, 0.28f);
            if (Near(text, Good)) return new Color(0.2f, 0.6f, 0.25f, 0.25f);
            if (Near(text, Gold)) return new Color(1f, 0.75f, 0.25f, 0.16f);
            return new Color(1f, 1f, 1f, 0.05f);
        }

        static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f && Mathf.Abs(a.b - b.b) < 0.02f;

        // ---------------------------------------------------------------- parchment pieces of the hackathon screens

        /// <summary>The parchment ink for one of the status colours above (gold, good, bad, dim); others unchanged.</summary>
        public static Color InkFor(Color status)
        {
            if (Near(status, Bad)) return UiKit.InkBad;
            if (Near(status, Good)) return UiKit.InkGood;
            if (Near(status, Gold)) return UiKit.InkGold;
            if (Near(status, Dim)) return UiKit.InkDim;
            return status;
        }

        /// <summary>The wood header of a screen: a medallion with <paramref name="glyph"/> over the left end and the title
        /// in light ink. Returns the title.</summary>
        public static TMPro.TextMeshProUGUI Header(RectTransform header, string title, string glyph, float titleSize, float medal, float titleRight)
        {
            UiKit.Paint(header, "Header_Wood", 1.2f, null, new Color(0.36f, 0.22f, 0.1f, 1f));
            var m = UiKit.TopLeft(header, "Medallion", -medal * 0.08f, -medal * 0.08f, medal, medal);
            var mimg = UiKit.Paint(m, "Medallion_Frame", 1f, null, new Color(0f, 0f, 0f, 0f));
            mimg.preserveAspect = true;
            if (mimg.sprite != null)
            {
                var inset = medal * 0.23f;
                var disc = UiKit.Fill(m, "Disc", inset, inset, inset, inset).gameObject.AddComponent<Image>();
                disc.sprite = IslandUi.Circle;
                disc.color = new Color(0.96f, 0.88f, 0.7f, 1f);
                disc.raycastTarget = false;
            }
            var g = medal * 0.3f;
            UiKit.Icon(UiKit.Fill(m, "Glyph", g, g, g, g), glyph);
            return UiKit.Label(UiKit.Fill(header, "Title", medal * 0.98f, 4f, titleRight, 4f), title, titleSize, UiKit.OnWood, TMPro.TextAlignmentOptions.MidlineLeft, true, titleSize * 0.55f);
        }

        /// <summary>A parchment status line (the panel's messages in ink; the strip tints red, green or amber).</summary>
        public static TMPro.TextMeshProUGUI StatusLine(RectTransform strip, float size = 26f)
        {
            UiKit.Paint(strip, "Panel_Inset", 3f, new Color(0.93f, 0.84f, 0.66f, 1f), new Color(0.9f, 0.8f, 0.62f, 1f));
            var tint = UiKit.Fill(strip, "Tint", 6f, 5f, 6f, 5f).gameObject.AddComponent<Image>();
            tint.raycastTarget = false;
            var t = UiKit.Label(UiKit.Fill(strip, "Text", 22f, 2f, 22f, 2f), string.Empty, size, UiKit.InkDim, TMPro.TextAlignmentOptions.MidlineLeft, false, size * 0.6f);
            t.textWrappingMode = TMPro.TextWrappingModes.Normal;
            var s = t.gameObject.AddComponent<HackStatus>();
            s.Background = tint;
            s.Parchment = true;
            return t;
        }

        /// <summary>The island picture frame: a blue tile with a sand island, the NFT image over it once loaded.</summary>
        public static void IslandPicture(RectTransform frame, string url)
        {
            UiKit.Paint(frame, "Slot_Card", 2.4f, new Color(0.4f, 0.65f, 0.9f, 1f), new Color(0.15f, 0.42f, 0.65f, 1f));
            var sand = UiKit.Fill(frame, "Island", frame.sizeDelta.x * 0.24f, frame.sizeDelta.y * 0.24f, frame.sizeDelta.x * 0.24f, frame.sizeDelta.y * 0.24f).gameObject.AddComponent<Image>();
            sand.sprite = IslandUi.Circle;
            sand.color = new Color(0.93f, 0.84f, 0.58f, 1f);
            sand.raycastTarget = false;
            var green = UiKit.Fill(frame, "Green", frame.sizeDelta.x * 0.33f, frame.sizeDelta.y * 0.33f, frame.sizeDelta.x * 0.33f, frame.sizeDelta.y * 0.33f).gameObject.AddComponent<Image>();
            green.sprite = IslandUi.Circle;
            green.color = new Color(0.36f, 0.7f, 0.45f, 1f);
            green.raycastTarget = false;
            if (!string.IsNullOrEmpty(url)) IslandUi.PictureIn(UiKit.Fill(frame, "Nft", 10f, 10f, 10f, 10f), url);
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

        /// <summary>"5SBg...jH9A" for a long wallet address ("-" for none).</summary>
        public static string ShortWallet(string wallet) =>
            string.IsNullOrEmpty(wallet) ? "-" : wallet.Length > 12 ? wallet.Substring(0, 4) + "..." + wallet.Substring(wallet.Length - 4) : wallet;
    }
}
