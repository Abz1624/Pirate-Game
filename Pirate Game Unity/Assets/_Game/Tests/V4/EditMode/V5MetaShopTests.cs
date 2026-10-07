using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Presentation.Menu;
using RoguePirates.V5.Meta;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M8: the meta services and the shop over the local save. Prices, insufficient RUM, buying twice, ownership never
    /// dropped (the M16 guard), every priced weapon, Logbook, item and Captain sold, starters owned and never sold, D4 (RUM
    /// content is web2), and the hub's screens. A bought Logbook reaching the run's offers is the scripted menu check.
    /// </summary>
    public sealed class V5MetaShopTests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";

        static (LocalMetaServices meta, SaveService save, ConfigSetRuntime config) Rig(int rum)
        {
            var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            var save = new SaveService(new MemorySaveBackend());
            save.Meta.marks = rum;
            return (new LocalMetaServices(() => save, () => config), save, config);
        }

        [Test]
        public void TheShop_SellsEveryPricedThing_AndNeverTheStarters()
        {
            var (meta, _, config) = Rig(0);
            using (config)
            {
                var unlocks = config.Single<UnlockConfig>();
                // V5 M8z (7 Oct, I8z.5): a priced weapon is sold unless it is a starter (every weapon is one for now, D18 P3).
                foreach (var u in unlocks.weaponUnlocks.Where(u => !unlocks.starterWeaponIds.Contains(u.weaponId)))
                    Assert.IsNotNull(meta.Entries(StoreCategory.Weapon).FirstOrDefault(e => e.ContentId == u.weaponId), u.weaponId);
                Assert.AreEqual(meta.All.Count, meta.All.Select(e => e.ContentId).Distinct().Count(), "one store row per thing (no starter sold twice)");
                foreach (var u in unlocks.logbookUnlocks.Where(u => LocalMetaServices.InAPool(config, u.contentId))) Assert.AreEqual(u.rumCost, meta.Entries(StoreCategory.Logbook).Single(e => e.ContentId == u.contentId).Price, u.contentId);
                foreach (var u in unlocks.itemUnlocks.Where(u => LocalMetaServices.InAPool(config, u.contentId))) Assert.IsNotNull(meta.Entries(StoreCategory.Item).FirstOrDefault(e => e.ContentId == u.contentId), u.contentId);
                foreach (var c in config.All<CaptainConfig>().Where(c => c.rumCost > 0))
                    Assert.AreEqual(c.rumCost, meta.Entries(StoreCategory.Captain).Single(e => e.ContentId == c.captainKey).Price, c.captainKey);
                foreach (var id in unlocks.starterWeaponIds)
                {
                    Assert.IsTrue(meta.Owns(id), id + " is a starter");
                    Assert.IsNull(meta.Entries(StoreCategory.Weapon).FirstOrDefault(e => e.ContentId == id), "starters are not sold");
                    Assert.AreEqual(PurchaseResult.NotForSale, meta.Buy(id));
                }
                Assert.IsTrue(meta.All.All(e => e.Origin == ContentOrigin.Web2), "D4: RUM content is web2");
                foreach (var e in meta.All)
                    Assert.IsTrue(LocalMetaServices.InAPool(config, e.ContentId), e.ContentId + ": content out of every pool (D34) is not sold");
                Assert.IsFalse(meta.All.Any(e => e.ContentId == "ironclad"), "Ironclad waits for armor (D34)");
            }
        }

        [Test]
        public void Buying_SpendsTheRightRum_Once_AndNeedsEnough()
        {
            var (meta, save, config) = Rig(150);
            using (config)
            {
                var entry = meta.Entries(StoreCategory.Logbook).First();
                Assert.AreEqual(PurchaseResult.NotEnough, meta.Buy(entry.ContentId), "150 RUM is short");
                Assert.AreEqual(150, meta.Balance, "nothing spent");
                save.Meta.marks = entry.Price + 5;
                var changes = 0;
                meta.BalanceChanged += _ => changes++;
                Assert.AreEqual(PurchaseResult.Bought, meta.Buy(entry.ContentId));
                Assert.AreEqual(5, meta.Balance, "the price, exactly");
                Assert.AreEqual(1, changes);
                Assert.IsTrue(meta.Owns(entry.ContentId));
                Assert.AreEqual(PurchaseResult.AlreadyOwned, meta.Buy(entry.ContentId), "a second buy does nothing");
                Assert.AreEqual(5, meta.Balance);
                Assert.IsTrue(meta.Owned(StoreCategory.Logbook).Any(e => e.ContentId == entry.ContentId));
                StringAssert.DoesNotContain("—", meta.Message(entry.ContentId, PurchaseResult.Bought));
            }
        }

        [Test]
        public void ACaptain_KeepsItsOtherLocks_InTheShop()
        {
            var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            using (config)
            {
                var save = new SaveService(new MemorySaveBackend());
                save.Meta.marks = 5000;
                var meta = new LocalMetaServices(() => save, () => config, null, _ => false); // a weapon or gold requirement unmet
                var captain = meta.Entries(StoreCategory.Captain).First();
                Assert.AreEqual(PurchaseResult.Locked, meta.Buy(captain.ContentId));
                Assert.AreEqual(5000, meta.Balance, "nothing spent");
                Assert.IsFalse(meta.Owns(captain.ContentId));
                StringAssert.DoesNotContain("—", meta.Message(captain.ContentId, PurchaseResult.Locked));
                var open = new LocalMetaServices(() => save, () => config, null, _ => true);
                Assert.AreEqual(PurchaseResult.Bought, open.Buy(captain.ContentId));
            }
        }

        [Test]
        public void Ownership_SurvivesAStaleSave()
        {
            var backend = new MemorySaveBackend();
            var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            using (config)
            {
                var fresh = new SaveService(backend);
                var stale = new SaveService(backend); // loaded before the purchase
                fresh.Meta.marks = 1000;
                var meta = new LocalMetaServices(() => fresh, () => config);
                var id = meta.Entries(StoreCategory.Item).First().ContentId;
                Assert.AreEqual(PurchaseResult.Bought, meta.Buy(id));
                UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("would have dropped owned content"));
                stale.Save(); // the M16 guard keeps the purchase (and says so)
                Assert.IsTrue(new SaveService(backend).HasEffectiveContentOwned(id), "never drops ownership");
            }
        }

        [Test]
        public void TheHub_ShowsTheShop_AndBuysThroughIt_WithTouchSizedButtons()
        {
            var (meta, save, config) = Rig(1000000);
            using (config)
            {
                var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
                try
                {
                    var voyages = 0;
                    var hub = MenuHubPresenter.Create(canvas.transform, meta, () => voyages++);
                    // V5 M8z (7 Oct, I8z.5): the first tab with something to buy (every weapon is a free starter for now, D18 P3).
                    var category = new[] { StoreCategory.Weapon, StoreCategory.Logbook, StoreCategory.Item, StoreCategory.Captain }
                        .First(c => meta.Entries(c).Any(e => !meta.Owns(e.ContentId) && string.IsNullOrEmpty(e.Requires)));
                    hub.ShowShop(category);
                    Assert.AreEqual(MenuHubPresenter.Screen.Shop, hub.Showing);
                    Assert.AreEqual(meta.Entries(category).Count, hub.RowCount, "one row per thing for sale");
                    foreach (var b in hub.GetComponentsInChildren<Button>(true))
                        Assert.GreaterOrEqual(b.GetComponent<LayoutElement>().preferredHeight, 72f, b.name);
                    var id = meta.Entries(category).First(e => !meta.Owns(e.ContentId) && string.IsNullOrEmpty(e.Requires)).ContentId;
                    Assert.AreEqual(PurchaseResult.Bought, hub.Buy(id));
                    StringAssert.StartsWith("Unlocked", hub.LastMessage);
                    var list = (RectTransform)hub.GetComponentInChildren<ScrollRect>().content;
                    Assert.AreEqual(0f, list.sizeDelta.x, 1e-3f, "the list is exactly the view's width (labels not clipped)");
                    Assert.IsTrue(hub.GetComponent<Canvas>().overrideSorting, "drawn above the art menu");
                    hub.ShowModes();
                    Assert.AreEqual(5, hub.RowCount, "Voyage and four coming modes");
                    hub.ShowCollection();
                    hub.ShowProfile();
                    Assert.Greater(hub.RowCount, 3);
                }
                finally { Object.DestroyImmediate(canvas); }
            }
        }
    }
}
