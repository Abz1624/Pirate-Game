using RoguePirates.V4.Foundation.Core;
using UnityEngine;

namespace RoguePirates.V4.Foundation.UI
{
    [CreateAssetMenu(fileName = "LevelUpCardArtSettings", menuName = "RoguePirates/Level Up Card Art")]
    public sealed class LevelUpCardArtSettings : ScriptableObject
    {
        [System.Serializable]
        public struct RarityArt
        {
            public Rarity rarity;
            public Sprite background;
            public Sprite effectOverlay;
        }

        public RarityArt[] rarityArt;
        [Tooltip("Demo (art team's parchment cards): colour of the title, category, level and rarity text on the art; alpha 0 keeps the default colours. The description sits on the ribbon and stays light.")]
        public Color artTextColor = new Color(0f, 0f, 0f, 0f);

        static LevelUpCardArtSettings _cached;

        public static LevelUpCardArtSettings Load()
        {
            if (_cached != null)
                return _cached;
            _cached = Resources.Load<LevelUpCardArtSettings>("LevelUpCardArtSettings");
            return _cached;
        }

        public bool TryGet(Rarity rarity, out Sprite background, out Sprite effectOverlay)
        {
            background = null;
            effectOverlay = null;
            if (rarityArt == null)
                return false;

            for (var i = 0; i < rarityArt.Length; i++)
            {
                if (rarityArt[i].rarity != rarity)
                    continue;
                background = rarityArt[i].background;
                effectOverlay = rarityArt[i].effectOverlay;
                return background != null || effectOverlay != null;
            }

            return false;
        }
    }
}
