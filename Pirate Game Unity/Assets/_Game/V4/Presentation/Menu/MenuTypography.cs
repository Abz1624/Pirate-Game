using TMPro;

namespace RoguePirates.V4.Presentation.Menu
{
    /// <summary>The menu's font (M14.13; port of V3's internal V3MainMenuTypography).</summary>
    static class MenuTypography
    {
        public static void ApplyTitle(TextMeshProUGUI tmp)
        {
            if (tmp != null && TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
        }

        public static void ApplyBody(TextMeshProUGUI tmp) => ApplyTitle(tmp);
    }
}
