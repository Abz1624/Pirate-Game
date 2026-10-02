using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoguePirates.V4.Presentation.Hud
{
    /// <summary>
    /// HUD icons by content id (M6). V3RunHudIconResolver loaded the sprites with AssetDatabase from
    /// <c>Assets/UI/Weapons/</c> and <c>Assets/UI/Item Icons/</c>, which exists only in the Editor, so player builds showed
    /// no icons. The catalog holds direct references instead; the editor tool
    /// (menu RoguePirates/V4/Build Icon Catalog) fills it from the same files V3 looked up.
    /// Lives in a Resources folder so the runtime-created presentation root can load it in builds.
    /// </summary>
    [CreateAssetMenu(fileName = "V4IconCatalog", menuName = "RoguePirates/V4/Icon Catalog")]
    public sealed class IconCatalog : ScriptableObject
    {
        public const string ResourcePath = "V4IconCatalog";

        [Serializable]
        public struct Entry
        {
            public string id;
            public Sprite sprite;
        }

        public List<Entry> weapons = new List<Entry>();
        public List<Entry> items = new List<Entry>();
        [Tooltip("Playtest (3 Oct): Logbook icons (Tools > Rogue Pirates > Demo > Wire Art Team Assets fills them from Assets/UI/New_items/Logbooks). Without one, the name shows.")]
        public List<Entry> logbooks = new List<Entry>();

        Dictionary<string, Sprite> _weaponLookup;
        Dictionary<string, Sprite> _itemLookup;
        Dictionary<string, Sprite> _logbookLookup;

        public static IconCatalog Load() => Resources.Load<IconCatalog>(ResourcePath);

        public Sprite Weapon(string weaponId) => Find(ref _weaponLookup, weapons, weaponId);
        public Sprite Item(string itemId) => Find(ref _itemLookup, items, itemId);
        public Sprite Logbook(string logbookId) => Find(ref _logbookLookup, logbooks, logbookId);

        static Sprite Find(ref Dictionary<string, Sprite> lookup, List<Entry> entries, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (lookup == null)
            {
                lookup = new Dictionary<string, Sprite>();
                foreach (var e in entries)
                    if (!string.IsNullOrEmpty(e.id) && e.sprite != null) lookup[e.id] = e.sprite;
            }
            return lookup.TryGetValue(id, out var sprite) ? sprite : null;
        }

        void OnValidate()
        {
            _weaponLookup = null;
            _itemLookup = null;
            _logbookLookup = null;
        }
    }
}
