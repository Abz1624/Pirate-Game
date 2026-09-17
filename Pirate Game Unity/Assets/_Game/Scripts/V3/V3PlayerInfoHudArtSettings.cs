using UnityEngine;

namespace HighSeas.V3
{
    [CreateAssetMenu(fileName = "V3PlayerInfoHudArtSettings", menuName = "HighSeas/V3 Player Info HUD Art")]
    public sealed class V3PlayerInfoHudArtSettings : ScriptableObject
    {
        [System.Serializable]
        public struct CaptainPortrait
        {
            public V3CaptainId captain;
            public Sprite icon;
        }

        public Sprite emptyBar;
        public Sprite fullHpBar;
        public Sprite fullXpBar;
        public Sprite hpIcon;
        public Sprite xpIcon;
        public Sprite levelBadgeOverlay;
        public Sprite shieldOverlayPattern;
        public CaptainPortrait[] captainPortraits;

        static V3PlayerInfoHudArtSettings _cached;

        public static V3PlayerInfoHudArtSettings Load()
        {
            if (_cached == null)
                _cached = Resources.Load<V3PlayerInfoHudArtSettings>("V3PlayerInfoHudArtSettings");
            return _cached;
        }

        public static void Reload()
        {
            _cached = null;
            Load();
        }

        public Sprite GetCaptainIcon(V3CaptainId captain)
        {
            if (captainPortraits != null)
            {
                for (var i = 0; i < captainPortraits.Length; i++)
                {
                    if (captainPortraits[i].captain == captain && captainPortraits[i].icon != null)
                        return captainPortraits[i].icon;
                }
            }

            if (captainPortraits != null && captainPortraits.Length > 0 && captainPortraits[0].icon != null)
                return captainPortraits[0].icon;

            return null;
        }
    }
}
