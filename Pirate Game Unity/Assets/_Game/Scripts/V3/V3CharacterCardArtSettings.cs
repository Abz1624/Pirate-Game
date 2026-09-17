using UnityEngine;

namespace HighSeas.V3
{
    [CreateAssetMenu(fileName = "V3CharacterCardArtSettings", menuName = "HighSeas/V3 Character Card Art")]
    public sealed class V3CharacterCardArtSettings : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public V3CaptainId captain;
            public Sprite scrollCard;
            public Sprite selectedCard;
        }

        public Sprite menuBackground;
        public Entry[] entries;

        static V3CharacterCardArtSettings _cached;

        public static V3CharacterCardArtSettings Load()
        {
            if (_cached != null)
                return _cached;
            _cached = Resources.Load<V3CharacterCardArtSettings>("V3CharacterCardArtSettings");
            return _cached;
        }

        public Sprite GetScrollCard(V3CaptainId captain)
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

        public Sprite GetSelectedCard(V3CaptainId captain)
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
    }
}
