using System;
using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Config;
using RoguePirates.V4.Weapons;
using UnityEngine;

namespace RoguePirates.V4.Stage
{
    /// <summary>
    /// Chest prices, the chest loot roll and chest-pool items (M14; port of V3's SaltwindStageEconomyLogic chest half,
    /// V3ChestLoot and SaltwindStageProgressionLogic.IsRunItemOfferEligible, was LegacyStageEconomy over the stage-profile
    /// context). Same formulas, rarity weights, pool order and random draws; the values come from the stage, item, evolution
    /// and unlock configs, and the purchase count is the module's own (was the profile context's).
    /// </summary>
    public sealed class ChestEconomy : IStageEconomy
    {
        readonly Func<ConfigSetRuntime> _config;
        readonly IStageRun _run;
        readonly IStageRandom _random;
        readonly Func<SaveService> _save;
        readonly Func<IWeaponManager> _weapons;
        readonly List<string> _equipped = new List<string>();
        readonly List<string> _candidates = new List<string>();
        readonly float[] _weights = new float[5];
        List<ItemConfig> _pool;

        public ChestEconomy(Func<ConfigSetRuntime> config, IStageRun run, IStageRandom random, Func<SaveService> save, Func<IWeaponManager> weapons)
        {
            _config = config;
            _run = run;
            _random = random;
            _save = save;
            _weapons = weapons;
        }

        /// <summary>M9: the run's item module (V3RunItemInventory.TryAddItem's place).</summary>
        public Func<string, bool> AddItem;

        /// <summary>V5 M3: the run's items (a chest's evolution-item chance skips items already carried).</summary>
        public Func<string, bool> HasItem;

        /// <summary>Chests bought this run (the price steps with it).</summary>
        public int Purchases { get; private set; }

        StageContentConfig Stage => _config?.Invoke()?.Single<StageContentConfig>();

        /// <summary>V3's chest pool: the items in it, in V3's catalog order (rolls index it).</summary>
        IReadOnlyList<ItemConfig> Pool => _pool ??= _config?.Invoke()?.All<ItemConfig>()
            .Where(i => i.inChestPool).OrderBy(i => i.chestPoolOrder).ToList() ?? new List<ItemConfig>();

        /// <summary>V3 StageBalanceProfileExportCodec.ChestPriceForPurchaseIndex: the base price, then ceil(increment × growth^n).</summary>
        public static int ChestPrice(StageContentConfig s, int completedPurchases)
        {
            if (completedPurchases <= 0) return s.chestBasePrice;
            return (int)Math.Ceiling(s.chestPriceIncrementBase * Math.Pow(s.chestPriceGrowth, completedPurchases));
        }

        public int QuoteChestPrice() => Stage != null ? ChestPrice(Stage, Purchases) : 0;

        /// <summary>V3 quirk kept: V3Economy.GetChestPrice returned the current price whatever count it was given.</summary>
        public int ProgressChestPrice(int paidChests) => QuoteChestPrice();

        public ChestPlan PlanChest(IReadOnlyList<string> equippedWeaponIds)
        {
            var stage = Stage;
            if (stage == null || _run == null || !_run.Exists) return new ChestPlan(false, 0, null, "missing export");
            var price = ChestPrice(stage, Purchases);
            if (_run.Gold < price) return new ChestPlan(false, 0, null, "insufficient gold");
            var evolutionItem = RollEvolutionItem(stage);
            if (evolutionItem != null) return new ChestPlan(true, price, evolutionItem, null);
            for (var attempt = 0; attempt < stage.chestLootAttempts; attempt++)
            {
                var itemId = RollSalvageItem(stage, _run.Luck);
                if (string.IsNullOrEmpty(itemId)) continue;
                if (!IsItemEligible(itemId, equippedWeaponIds)) continue;
                return new ChestPlan(true, price, itemId, null);
            }
            return new ChestPlan(false, 0, null, "no eligible loot");
        }

        /// <summary>
        /// V5 M3: with evolutionItemChance, the missing evolution item of a carried weapon that is not evolved and has reached
        /// evolutionItemMinWeaponLevel (one Loot draw for the chance, one to pick when several qualify; no draws when off).
        /// </summary>
        string RollEvolutionItem(StageContentConfig stage)
        {
            if (stage.evolutionItemChance <= 0f || HasItem == null) return null;
            var weapons = _weapons?.Invoke();
            var config = _config?.Invoke();
            if (weapons == null || config == null) return null;
            _candidates.Clear();
            var minLevel = EvolutionItemMinLevel(stage, config.Single<ProgressionConfig>());
            foreach (var slot in weapons.Slots)
            {
                if (slot?.Config == null || slot.Evolution != null || slot.Level < minLevel) continue;
                foreach (var evo in config.All<WeaponEvolutionConfig>())
                {
                    if (evo.baseWeapon == null || evo.baseWeapon.weaponId != slot.Id || string.IsNullOrEmpty(evo.requiredItemId)) continue;
                    if (HasItem(evo.requiredItemId) || _candidates.Contains(evo.requiredItemId)) continue;
                    if (Pool.Any(i => i.itemId == evo.requiredItemId)) _candidates.Add(evo.requiredItemId);
                }
            }
            if (_candidates.Count == 0) return null;
            if (_random.LootFloat() >= stage.evolutionItemChance) return null;
            return _candidates[_candidates.Count == 1 ? 0 : _random.LootInt(0, _candidates.Count)];
        }

        /// <summary>The weapon level the evolution-item chance starts at: the stage's own, or the evolution card's.</summary>
        public static int EvolutionItemMinLevel(StageContentConfig stage, ProgressionConfig progression)
        {
            if (stage.evolutionItemMinWeaponLevel > 0) return stage.evolutionItemMinWeaponLevel;
            if (progression == null) return int.MaxValue;
            return progression.evolutionUnlockLevel > 0 ? progression.evolutionUnlockLevel : progression.maxWeaponLevel;
        }

        public void RegisterChestPurchase() => Purchases++;
        public bool TryAddItem(string itemId) => AddItem != null && AddItem(itemId);

        public string ItemName(string itemId)
        {
            var item = _config?.Invoke()?.All<ItemConfig>().FirstOrDefault(i => i.itemId == itemId);
            return item != null && !string.IsNullOrEmpty(item.displayName) ? item.displayName : itemId;
        }

        public string RollChestPoolItem()
        {
            var pool = Pool;
            return pool[_random.MapInt(0, pool.Count)].itemId;
        }

        public string RollChestPoolItem(int minRarity)
        {
            _candidates.Clear();
            foreach (var item in Pool) if (item.rarity >= minRarity) _candidates.Add(item.itemId);
            if (_candidates.Count == 0) return RollChestPoolItem();
            return _candidates[_random.MapInt(0, _candidates.Count)];
        }

        public int MapInt(int maxExclusive) => _random.MapInt(0, maxExclusive);

        public IReadOnlyList<string> EquippedWeaponIds
        {
            get
            {
                _equipped.Clear();
                var weapons = _weapons?.Invoke();
                if (weapons != null)
                    foreach (var slot in weapons.Slots)
                        if (!string.IsNullOrEmpty(slot.Id)) _equipped.Add(slot.Id);
                return _equipped.Count > 0 ? _equipped : null;
            }
        }

        /// <summary>V3 V3ChestLoot.RollSalvageItem: a rarity by the weights (rare+ boosted by stage depth, uncommon+ by luck),
        /// then a uniform item of that rarity, falling back to lower rarities.</summary>
        string RollSalvageItem(StageContentConfig stage, float luck)
        {
            var baseWeights = stage.chestRarityWeights;
            var count = Mathf.Min(baseWeights?.Length ?? 0, _weights.Length);
            var stageRareBoost = 1f + stage.chestRareBoostPerStageDepth * Mathf.Max(0, stage.chestStageDepth - 1);
            var luckBoost = 1f + Mathf.Max(0f, luck) / 100f;
            var total = 0f;
            for (var i = 0; i < count; i++)
            {
                var w = baseWeights[i];
                if (i >= 2) w *= stageRareBoost; // Rare and better
                if (i >= 1) w *= luckBoost;      // Uncommon and better
                _weights[i] = w;
                total += w;
            }
            var roll = _random.LootFloat() * total;
            var rarity = 0;
            var acc = 0f;
            for (var i = 0; i < count; i++)
            {
                acc += _weights[i];
                if (roll <= acc) { rarity = i; break; }
            }
            var pool = Pool;
            for (var tier = rarity; tier >= 0; tier--)
            {
                _candidates.Clear();
                foreach (var item in pool) if (item.rarity == tier) _candidates.Add(item.itemId);
                if (_candidates.Count == 0) continue;
                return _candidates[_random.LootInt(0, _candidates.Count)];
            }
            return pool.Count > 0 ? pool[0].itemId : null;
        }

        /// <summary>V3 SaltwindStageProgressionLogic.IsRunItemOfferEligible: an evolution's required item needs its base weapon
        /// in the starter pool, owned (meta save) or carried this run; every other item is eligible.</summary>
        bool IsItemEligible(string itemId, IReadOnlyList<string> runOwnedWeaponIds)
        {
            var config = _config?.Invoke();
            var evolution = config?.All<WeaponEvolutionConfig>().FirstOrDefault(e => e.requiredItemId == itemId);
            if (evolution == null) return true;
            var weaponId = evolution.baseWeapon != null ? evolution.baseWeapon.weaponId : null;
            if (string.IsNullOrEmpty(weaponId)) return false;
            var starters = config.Single<UnlockConfig>()?.starterWeaponIds;
            if (starters != null && Array.IndexOf(starters, weaponId) >= 0) return true;
            var save = _save?.Invoke();
            if (save != null && save.HasEffectiveContentOwned(weaponId)) return true;
            if (runOwnedWeaponIds != null)
                for (var i = 0; i < runOwnedWeaponIds.Count; i++)
                    if (runOwnedWeaponIds[i] == weaponId) return true;
            return false;
        }
    }
}
