using UnityEngine;
using RoguePirates.V4.Foundation.Progression;

namespace RoguePirates.V4.Foundation.UI
{
    [CreateAssetMenu(fileName = "V3PlayerInfoHudArtSettings", menuName = "RoguePirates/Player Info HUD Art")]
    public sealed class PlayerInfoHudArtSettings : ScriptableObject
    {
        [System.Serializable]
        public struct CaptainPortrait
        {
            public CaptainId captain;
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

        static PlayerInfoHudArtSettings _cached;

        public static PlayerInfoHudArtSettings Load()
        {
            if (_cached == null)
                _cached = Resources.Load<PlayerInfoHudArtSettings>("V3PlayerInfoHudArtSettings");
            return _cached;
        }

        public static void Reload()
        {
            _cached = null;
            Load();
        }

        public Sprite GetCaptainIcon(CaptainId captain)
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
