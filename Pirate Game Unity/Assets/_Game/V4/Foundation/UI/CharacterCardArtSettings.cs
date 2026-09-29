using UnityEngine;
using RoguePirates.V4.Foundation.Progression;

namespace RoguePirates.V4.Foundation.UI
{
    [CreateAssetMenu(fileName = "V3CharacterCardArtSettings", menuName = "RoguePirates/Character Card Art")]
    public sealed class CharacterCardArtSettings : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public CaptainId captain;
            public Sprite scrollCard;
            public Sprite selectedCard;
            public Sprite menuPose;
            public Color menuThemeColor;
        }

        public Sprite menuBackground;
        public Entry[] entries;

        static CharacterCardArtSettings _cached;

        public static CharacterCardArtSettings Load()
        {
            if (_cached != null)
                return _cached;
            _cached = Resources.Load<CharacterCardArtSettings>("V3CharacterCardArtSettings");
            return _cached;
        }

        public static void Reload()
        {
            _cached = null;
            Load();
        }

        public Sprite GetScrollCard(CaptainId captain)
        {
            if (entries == null)
                return null;
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].captain == captain)
                    return entries[i].scrollCard;
            }

            return null;
        }

        public Sprite GetSelectedCard(CaptainId captain)
        {
            if (entries == null)
                return null;
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].captain == captain)
                    return entries[i].selectedCard != null
                        ? entries[i].selectedCard
                        : entries[i].scrollCard;
            }

            return null;
        }

        public Sprite GetMenuPose(CaptainId captain)
        {
            if (entries == null)
                return null;
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].captain == captain)
                    return entries[i].menuPose;
            }

            return null;
        }

        public Color GetMenuThemeColor(CaptainId captain)
        {
            if (entries != null)
            {
                for (var i = 0; i < entries.Length; i++)
                {
                    if (entries[i].captain == captain)
                        return entries[i].menuThemeColor;
                }
            }

            return DefaultMenuThemeColor(captain);
        }

        /// <summary>The Captains' menu colours when the settings asset has no entry (were V3's captain catalog).</summary>
        public static Color DefaultMenuThemeColor(CaptainId id) => id switch
        {
            CaptainId.Brine => Hex("4a5e27"),
            CaptainId.Flint => Hex("bf1d2c"),
            CaptainId.Richie => Hex("75225e"),
            CaptainId.Ember => Hex("ffac5c"),
            CaptainId.Roxie => Hex("3485e4"),
            _ => Hex("033285")
        };

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var color) ? color : Color.white;
    }
}
