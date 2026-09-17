using TMPro;
using UnityEngine;

namespace HighSeas.V3
{
    static class V3MainMenuTypography
    {
        const string FallbackBodyResource = "Fonts & Materials/LiberationSans SDF - Fallback";

        static TMP_FontAsset _title;
        static TMP_FontAsset _body;
        static TMP_FontAsset _fallback;

        static TMP_FontAsset FallbackFont =>
            _fallback ??= Resources.Load<TMP_FontAsset>(FallbackBodyResource) ?? TMP_Settings.defaultFontAsset;

        static TMP_FontAsset TitleFont
        {
            get
            {
                if (_title != null)
                    return _title;

                var settings = V3MainMenuTypographySettings.Load();
                _title = settings != null ? settings.titleFont : null;
                return _title;
            }
        }

        static TMP_FontAsset BodyFont
        {
            get
            {
                if (_body != null)
                    return _body;

                var settings = V3MainMenuTypographySettings.Load();
                _body = settings != null ? settings.bodyFont : null;
                return _body;
            }
        }

        public static void ApplyTitle(TextMeshProUGUI tmp)
        {
            if (tmp == null)
                return;
            tmp.font = TitleFont != null ? TitleFont : BodyFont != null ? BodyFont : FallbackFont;
        }

        public static void ApplyBody(TextMeshProUGUI tmp)
        {
            if (tmp == null)
                return;
            tmp.font = BodyFont != null ? BodyFont : FallbackFont;
        }
    }
}
