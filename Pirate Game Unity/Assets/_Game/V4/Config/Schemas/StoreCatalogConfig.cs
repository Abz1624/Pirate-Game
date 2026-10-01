using System;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    /// <summary>V5 M8: what a store entry sells.</summary>
    public enum StoreCategory { Weapon = 0, Logbook = 1, Item = 2, Captain = 3, Decoration = 4, Sail = 5 }

    /// <summary>V5 M8 (D4): where owned content came from; only web3 content can be minted.</summary>
    public enum ContentOrigin { Web2 = 0, Web3 = 1 }

    [Serializable]
    public sealed class StoreCatalogEntry
    {
        public string contentId;
        public StoreCategory category;
        [Tooltip("0 Common .. 4 Legendary (the card frame).")] [Range(0, 4)] public int rarity;
        [Tooltip("Shown with a \"New\" tag.")] public bool isNew;
        [Tooltip("Sale price share off (0 = no sale, 0.25 = 25% off).")] [Range(0f, 0.9f)] public float saleShare;
        [Tooltip("D4: Web2 content is bought with Rum and never minted.")] public ContentOrigin origin = ContentOrigin.Web2;
        [Tooltip("A content id that must be owned first (empty = none).")] public string requires;
        public int sortOrder;
    }

    /// <summary>
    /// V5 M8: the shop's catalogue. Prices stay in <see cref="UnlockConfig"/> (weapons, Logbooks, items) and
    /// <see cref="CaptainConfig.rumCost"/> (one source each); the catalogue adds the category, rarity, "new", sales, the
    /// origin and requirements. Priced content missing from the catalogue is still sold, with defaults.
    /// </summary>
    [CreateAssetMenu(menuName = "RoguePirates/V5/Store Catalog", fileName = "StoreCatalog")]
    public sealed class StoreCatalogConfig : V4ConfigAsset
    {
        public StoreCatalogEntry[] entries = Array.Empty<StoreCatalogEntry>();

        public StoreCatalogEntry Find(string contentId)
        {
            foreach (var e in entries) if (e != null && e.contentId == contentId) return e;
            return null;
        }

        public override void Validate(ConfigValidationReport report)
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            for (var i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e == null || string.IsNullOrWhiteSpace(e.contentId)) { report.Error(this, $"entries[{i}]", "missing content id"); continue; }
                if (!seen.Add(e.contentId)) report.Error(this, $"entries[{i}]", "duplicate " + e.contentId);
            }
        }
    }
}
