using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Runtime-loaded minimap frame and ocean background sprites.</summary>
    public sealed class V3MinimapArtSettings : ScriptableObject
    {
        public Sprite background;
        public Sprite overlay;

        static V3MinimapArtSettings _cached;

        public static V3MinimapArtSettings Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<V3MinimapArtSettings>("V3MinimapArtSettings");
            return _cached;
        }
    }
}
