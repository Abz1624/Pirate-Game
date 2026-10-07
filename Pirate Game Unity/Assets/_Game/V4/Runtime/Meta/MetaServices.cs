using System;
using System.Collections.Generic;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Core;

namespace RoguePirates.V5.Meta
{
    /// <summary>The player's account (local today; the server account from M10b).</summary>
    public interface IAccountService
    {
        string PlayerId { get; }
        string DisplayName { get; }
        bool IsGuest { get; }
    }

    /// <summary>The player's RUM (local save today; the server ledger from M10).</summary>
    public interface IEconomyService
    {
        int Balance { get; }
        bool CanAfford(int price);
        event Action<int> BalanceChanged;
    }

    /// <summary>What the player owns (the save's owned ids; starters count as owned).</summary>
    public interface IInventoryService
    {
        bool Owns(string contentId);
        IReadOnlyList<StoreEntry> Owned(StoreCategory category);
        ContentOrigin OriginOf(string contentId);
    }

    public enum PurchaseResult { Bought, AlreadyOwned, NotEnough, NotForSale, Locked }

    /// <summary>The shop: what is for sale and buying it.</summary>
    public interface IStoreService
    {
        IReadOnlyList<StoreEntry> Entries(StoreCategory category);
        PurchaseResult Buy(string contentId);
        StoreEntry Entry(string contentId);
    }

    /// <summary>One thing the shop sells (or the player owns), resolved from the configs.</summary>
    public sealed class StoreEntry
    {
        public string ContentId, DisplayName, Requires;
        public StoreCategory Category;
        public int Rarity, BasePrice, Price, SortOrder;
        public bool IsNew, OnSale, Free;
        public ContentOrigin Origin;
    }

    /// <summary>
    /// V5 M8: the meta services over the local save (one class, four interfaces, so the server versions drop in one at a
    /// time). Prices from the unlock config and the Captains; the catalogue adds category, rarity, "new", sales, origin and
    /// requirements. A purchase spends RUM through the save's own purchase (which never drops ownership: the M16 guard).
    /// </summary>
    public sealed class LocalMetaServices : IAccountService, IEconomyService, IInventoryService, IStoreService
    {
        readonly Func<SaveService> _save;
        readonly Func<ConfigSetRuntime> _config;
        readonly Func<string, bool> _bypassOwned;
        readonly Func<string, bool> _captainBuyable;

        /// <param name="captainBuyable">For a Captain: its other requirements (a weapon, lifetime gold) are met and the
        /// treasury holds its price (the menu's Captain locks); null = no requirements beyond the price.</param>
        public LocalMetaServices(Func<SaveService> save, Func<ConfigSetRuntime> config, Func<string, bool> bypassOwned = null,
            Func<string, bool> captainBuyable = null)
        {
            _save = save;
            _config = config;
            _bypassOwned = bypassOwned;
            _captainBuyable = captainBuyable;
        }

        // ---- Account -------------------------------------------------------------------------------------------------
        public string PlayerId => "local";
        public string DisplayName => "Captain";
        public bool IsGuest => true;

        // ---- Economy -------------------------------------------------------------------------------------------------
        public int Balance => _save()?.Meta.marks ?? 0;
        public bool CanAfford(int price) => price >= 0 && Balance >= price;
        public event Action<int> BalanceChanged;

        // ---- Catalogue ------------------------------------------------------------------------------------------------
        List<StoreEntry> _all;

        /// <summary>Every priced or starter thing in the configs, resolved on first use (one service per menu, whose config does not change).</summary>
        public IReadOnlyList<StoreEntry> All
        {
            get
            {
                if (_all != null) return _all;
                _all = new List<StoreEntry>();
                var config = _config();
                if (config == null) return _all;
                var unlocks = config.Single<UnlockConfig>();
                var catalog = First<StoreCatalogConfig>(config);
                void Add(string id, StoreCategory category, int price, string name, int rarity, bool free)
                {
                    if (string.IsNullOrEmpty(id)) return;
                    var c = catalog?.Find(id);
                    var sale = c != null ? c.saleShare : 0f;
                    _all.Add(new StoreEntry
                    {
                        ContentId = id, Category = c != null ? c.category : category, DisplayName = string.IsNullOrEmpty(name) ? id : name,
                        BasePrice = price, Price = sale > 0f ? (int)Math.Ceiling(price * (1f - sale)) : price, OnSale = sale > 0f,
                        Rarity = c != null ? c.rarity : rarity, IsNew = c != null && c.isNew, Origin = c != null ? c.origin : ContentOrigin.Web2,
                        Requires = c?.requires, SortOrder = c != null ? c.sortOrder : _all.Count, Free = free,
                    });
                }
                if (unlocks != null)
                {
                    foreach (var id in unlocks.starterWeaponIds) if (RoguePirates.V4.Foundation.Combat.WeaponFeatureFlags.IsWeaponEnabled(id)) Add(id, StoreCategory.Weapon, 0, WeaponName(config, id), 0, true);
                    // V5 M8z (7 Oct, I8z.5): a starter is never also sold (every weapon is a starter for now, D18 P3, but the
                    // weapon prices stay in the data): one row per weapon, the free one.
                    foreach (var u in unlocks.weaponUnlocks)
                        if (u != null && Array.IndexOf(unlocks.starterWeaponIds, u.weaponId) < 0 && RoguePirates.V4.Foundation.Combat.WeaponFeatureFlags.IsWeaponEnabled(u.weaponId))
                            Add(u.weaponId, StoreCategory.Weapon, u.rumCost, WeaponName(config, u.weaponId), 1, false);
                    // D34: content held out of every pool (its stat does nothing yet) is not sold.
                    foreach (var u in unlocks.logbookUnlocks) if (u != null && InAPool(config, u.contentId)) Add(u.contentId, StoreCategory.Logbook, u.rumCost, LogbookName(config, u.contentId), 1, false);
                    foreach (var u in unlocks.itemUnlocks) if (u != null && InAPool(config, u.contentId)) Add(u.contentId, StoreCategory.Item, u.rumCost, ItemName(config, u.contentId, out var r), r, false);
                }
                foreach (var cap in config.All<CaptainConfig>())
                    if (cap != null && cap.rumCost > 0) Add(cap.captainKey, StoreCategory.Captain, cap.rumCost, cap.displayName, 2, false);
                _all.Sort((a, b) => a.Category != b.Category ? a.Category.CompareTo(b.Category) : a.Price != b.Price ? a.Price.CompareTo(b.Price) : a.SortOrder.CompareTo(b.SortOrder));
                return _all;
            }
        }

        static T First<T>(ConfigSetRuntime config) where T : V4ConfigAsset
        {
            foreach (var c in config.All<T>()) return c;
            return null;
        }

        static string WeaponName(ConfigSetRuntime c, string id)
        {
            foreach (var w in c.All<WeaponConfig>()) if (w.weaponId == id) return w.displayName;
            return id;
        }

        /// <summary>A Logbook in the Saltwind pool, or an item in the chest or level-up pool (unknown ids count as pooled).</summary>
        public static bool InAPool(ConfigSetRuntime c, string id)
        {
            foreach (var l in c.All<LogbookConfig>()) if (l.logbookId == id) return l.inSaltwindPool;
            foreach (var i in c.All<ItemConfig>()) if (i.itemId == id) return i.inChestPool || i.inLevelUpPool;
            return true;
        }

        static string LogbookName(ConfigSetRuntime c, string id)
        {
            foreach (var l in c.All<LogbookConfig>()) if (l.logbookId == id) return l.displayName;
            return id;
        }

        static string ItemName(ConfigSetRuntime c, string id, out int rarity)
        {
            foreach (var i in c.All<ItemConfig>()) if (i.itemId == id) { rarity = i.rarity; return i.displayName; }
            rarity = 0;
            return id;
        }

        public IReadOnlyList<StoreEntry> Entries(StoreCategory category)
        {
            var list = new List<StoreEntry>();
            foreach (var e in All) if (e.Category == category && !e.Free) list.Add(e);
            return list;
        }

        public StoreEntry Entry(string contentId)
        {
            foreach (var e in All) if (e.ContentId == contentId) return e;
            return null;
        }

        // ---- Inventory -----------------------------------------------------------------------------------------------
        public bool Owns(string contentId)
        {
            if (string.IsNullOrEmpty(contentId)) return false;
            var e = Entry(contentId);
            if (e != null && e.Free) return true;
            if (_bypassOwned != null && _bypassOwned(contentId)) return true;
            return _save()?.HasEffectiveContentOwned(contentId) ?? false;
        }

        public IReadOnlyList<StoreEntry> Owned(StoreCategory category)
        {
            var list = new List<StoreEntry>();
            foreach (var e in All) if (e.Category == category && Owns(e.ContentId)) list.Add(e);
            return list;
        }

        /// <summary>D4: content bought with RUM is web2 (never minted); web3 origin comes with the chain phases.</summary>
        public ContentOrigin OriginOf(string contentId) => Entry(contentId)?.Origin ?? ContentOrigin.Web2;

        // ---- Store ---------------------------------------------------------------------------------------------------
        public PurchaseResult Buy(string contentId)
        {
            var save = _save();
            var e = Entry(contentId);
            if (save == null || e == null || e.Free || e.Origin != ContentOrigin.Web2) return PurchaseResult.NotForSale;
            if (Owns(contentId)) return PurchaseResult.AlreadyOwned;
            if (!string.IsNullOrEmpty(e.Requires) && !Owns(e.Requires)) return PurchaseResult.Locked;
            if (!CanAfford(e.Price)) return PurchaseResult.NotEnough;
            if (e.Category == StoreCategory.Captain && _captainBuyable != null && !_captainBuyable(contentId)) return PurchaseResult.Locked;
            if (!save.TryPurchaseMarksUnlock(contentId, e.Price)) return PurchaseResult.NotEnough;
            BalanceChanged?.Invoke(Balance);
            return PurchaseResult.Bought;
        }

        /// <summary>The shop's message for a result (UI text: no em dashes).</summary>
        public string Message(string contentId, PurchaseResult result)
        {
            var name = Entry(contentId)?.DisplayName ?? contentId;
            switch (result)
            {
                case PurchaseResult.Bought: return $"Unlocked {name}!";
                case PurchaseResult.AlreadyOwned: return $"{name} is already yours.";
                case PurchaseResult.NotEnough: return $"Not enough RUM for {name}.";
                case PurchaseResult.Locked:
                    var requires = Entry(contentId)?.Requires;
                    return string.IsNullOrEmpty(requires) ? $"{name} is still locked: see the Captain select." : $"{name}: unlock {Entry(requires)?.DisplayName ?? requires} first.";
                default: return $"{name} is not for sale.";
            }
        }
    }
}
