using UnityEngine;

namespace RoguePirates.V4.Foundation.UI
{
    /// <summary>Runtime-loaded minimap frame and ocean background sprites.</summary>
    public sealed class MinimapArtSettings : ScriptableObject
    {
        public Sprite background;
        public Sprite overlay;

        static MinimapArtSettings _cached;

        public static MinimapArtSettings Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<MinimapArtSettings>("V3MinimapArtSettings");
            return _cached;
        }
    }
}
