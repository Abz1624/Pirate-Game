using RoguePirates.V4.Presentation.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.Hackathon.UI
{
    /// <summary>
    /// Hackathon (12 Oct): the hackathon panels' frame (an on-top panel with a click blocker), the status colours the
    /// panels pass around (each kit maps them to parchment inks) and the art menu's font as the fallback font. 13 Oct: the
    /// old navy rows, amber buttons, grey scroll lists and cream inputs went; the screens draw with the parchment kit
    /// (<see cref="HackSkin"/>, MarketKit, BoardKit, IslandKit, PvpKit).
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
    }
}
