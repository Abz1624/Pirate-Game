using TMPro;

namespace HighSeas.V3
{
    static class V3MainMenuTypography
    {
        public static void ApplyTitle(TextMeshProUGUI tmp)
        {
            if (tmp != null && TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
        }

        public static void ApplyBody(TextMeshProUGUI tmp) => ApplyTitle(tmp);
    }
}
