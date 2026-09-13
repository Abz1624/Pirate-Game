using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>
    /// Tags placeholder objects for later art replacement. Search in hierarchy for "PLACEHOLDER_v3".
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlaceholderMarker : MonoBehaviour
    {
        [SerializeField] string category = "general";
        [SerializeField] string assetId = "unknown";
        [TextArea(1, 3)]
        [SerializeField] string replacementHint = "Replace with final production art.";

        public string Category => category;
        public string AssetId => assetId;
        public string ReplacementHint => replacementHint;
        public string SearchTag => $"PLACEHOLDER_v3_{category}_{assetId}";

        public void Configure(string cat, string id, string hint)
        {
            category = cat;
            assetId = id;
            replacementHint = hint;
            gameObject.name = SearchTag;
        }
    }
}
