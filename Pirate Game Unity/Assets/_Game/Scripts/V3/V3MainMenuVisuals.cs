using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighSeas.V3
{
    static class V3MainMenuVisuals
    {
        public struct ThemeGradient
        {
            public Color Top;
            public Color Bottom;
            public Color AccentGlow;
        }

        public static ThemeGradient BuildGradient(Color theme)
        {
            var top = Color.Lerp(theme, Color.white, 0.28f);
            var bottom = Color.Lerp(theme, new Color(0.04f, 0.06f, 0.1f), 0.55f);
            top.a = 1f;
            bottom.a = 1f;
            return new ThemeGradient
            {
                Top = top,
                Bottom = bottom,
                AccentGlow = new Color(theme.r, theme.g, theme.b, 0.22f)
            };
        }

        public static ThemeGradient Lerp(ThemeGradient from, ThemeGradient to, float t) =>
            new()
            {
                Top = Color.Lerp(from.Top, to.Top, t),
                Bottom = Color.Lerp(from.Bottom, to.Bottom, t),
                AccentGlow = Color.Lerp(from.AccentGlow, to.AccentGlow, t)
            };

        public static void ApplyBackground(
            Image top,
            Image bottom,
            Image vignette,
            Image accentGlow,
            ThemeGradient gradient)
        {
            if (top != null)
                top.color = gradient.Top;
            if (bottom != null)
                bottom.color = gradient.Bottom;
            if (vignette != null)
                vignette.color = new Color(0f, 0f, 0f, 0.38f);
            if (accentGlow != null)
                accentGlow.color = gradient.AccentGlow;
        }

        public static void StyleHeader(TextMeshProUGUI tmp)
        {
            if (tmp == null)
                return;
            tmp.outlineWidth = 0.14f;
            tmp.outlineColor = new Color(0f, 0f, 0f, 0.35f);
            tmp.enableVertexGradient = true;
            tmp.colorGradient = new VertexGradient(
                new Color(1f, 1f, 1f, 0.72f),
                new Color(1f, 1f, 1f, 0.72f),
                new Color(0.75f, 0.78f, 0.85f, 0.45f),
                new Color(0.75f, 0.78f, 0.85f, 0.45f));
        }

        public static void StyleCaptainName(TextMeshProUGUI tmp, Color theme)
        {
            if (tmp == null)
                return;
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = new Color(0f, 0f, 0f, 0.5f);
            var bright = Color.Lerp(Color.white, theme, 0.12f);
            var dim = Color.Lerp(theme, new Color(0.15f, 0.18f, 0.22f), 0.35f);
            bright.a = 1f;
            dim.a = 1f;
            tmp.enableVertexGradient = true;
            tmp.colorGradient = new VertexGradient(bright, bright, dim, dim);
        }

        public static void StyleBody(TextMeshProUGUI tmp)
        {
            if (tmp == null)
                return;
            tmp.outlineWidth = 0.1f;
            tmp.outlineColor = new Color(0f, 0f, 0f, 0.4f);
        }

        public static void AddDropShadow(Graphic graphic, Vector2 distance, Color color)
        {
            if (graphic == null)
                return;
            var shadow = graphic.gameObject.GetComponent<Shadow>();
            if (shadow == null)
                shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectDistance = distance;
            shadow.effectColor = color;
            shadow.useGraphicAlpha = true;
        }

        public static Image CreateGradientBand(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            return img;
        }
    }
}
