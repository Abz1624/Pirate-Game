using TMPro;
using UnityEngine;

namespace HighSeas.V3
{
    [CreateAssetMenu(fileName = "V3MainMenuTypographySettings", menuName = "HighSeas/V3 Main Menu Typography")]
    public sealed class V3MainMenuTypographySettings : ScriptableObject
    {
        public TMP_FontAsset titleFont;
        public TMP_FontAsset bodyFont;

        static V3MainMenuTypographySettings _cached;

        public static V3MainMenuTypographySettings Load()
        {
            if (_cached != null)
                return _cached;
            _cached = Resources.Load<V3MainMenuTypographySettings>("V3MainMenuTypographySettings");
            return _cached;
        }

        public static void Reload()
        {
            _cached = null;
            Load();
        }
    }
}
