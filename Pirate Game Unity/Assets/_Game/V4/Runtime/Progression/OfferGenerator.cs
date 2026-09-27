using System;
using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Config;
using UnityEngine;

namespace RoguePirates.V4.Progression
{
    /// <summary>Level-up card kinds (same names and order as V3's LevelOfferKind).</summary>
    public enum OfferKind { WeaponUpgrade = 0, NewWeapon = 1, Logbook = 2, NewRunItem = 3, FallbackHeal = 4, FallbackDamage = 5, FallbackMoveSpeed = 6, Continue = 7, WeaponEvolution = 8 }

    /// <summary>One stat line of a weapon upgrade card.</summary>
    public readonly struct UpgradeLine
    {
        public readonly ConfigEnums.UpgradeAxis Axis;
        public readonly float Value;
        public UpgradeLine(ConfigEnums.UpgradeAxis axis, float value) { Axis = axis; Value = value; }
    }

    /// <summary>A level-up card (the data V3's LevelOffer carried; texts are the presenter's).</summary>
    public struct Offer
    {
        public OfferKind Kind;
        public string Id;
        public string EvolutionId;
        /// <summary>0 Common … 4 Legendary.</summary>
        public int Rarity;
        public int CurrentLevel, NextLevel;
        public bool IsWeapon, IsNew;
        public UpgradeLine[] Lines;
        public bool IsEmpty => Id == null;
        /// <summary>V3's offer key (duplicate cards compare it).</summary>
        public string Key => Kind == OfferKind.WeaponEvolution ? $"{Kind}:{Id}:{EvolutionId}" : $"{Kind}:{Id}";
    }

    /// <summary>The Loot stream draws the offers use (V3 RunRng).</summary>
    public interface IOfferRandom
    {
        float LootFloat();
    }

    /// <summary>What offers need to know about the save, the items and the evolutions (V3 read these singletons).</summary>
    public interface IOfferContext
    {
        /// <summary>The weapon can be offered (V3: its feature flag, and in V3 runs its unlock: starter or owned).</summary>
        bool IsWeaponOfferable(string weaponId);
        /// <summary>The weapon is unlocked (V3 V3ContentUnlocks.IsWeaponUnlocked: a starter, or owned in the save); checked again when a card is applied.</summary>
        bool IsWeaponUnlocked(string weaponId);
        bool HasItem(string itemId);
        bool IsEvolved(string weaponId);
    }

    /// <summary>The run's build as the offers see it (V3 LevelSystem's owned lists and level dictionaries).</summary>
    public interface IOfferState
    {
        IReadOnlyList<string> OwnedWeapons { get; }
        IReadOnlyList<string> OwnedLogbooks { get; }
        /// <summary>The weapon's level (0 when unknown, as V3's TryGetValue).</summary>
        int WeaponLevel(string weaponId);
        int LogbookLevel(string logbookId);
    }

    /// <summary>The player's numbers the fallback cards check (V3: hull, PlayerStats.GlobalDamage and MoveSpeed).</summary>
    public readonly struct OfferPlayer
    {
        public readonly float Hull, MaxHull, GlobalDamage, MoveSpeed;
        public readonly bool HasStats;
        public OfferPlayer(float hull, float maxHull, float globalDamage, float moveSpeed, bool hasStats = true)
        { Hull = hull; MaxHull = maxHull; GlobalDamage = globalDamage; MoveSpeed = moveSpeed; HasStats = hasStats; }
    }

    /// <summary>
    /// Level-up cards (M9 step 9.6): port of V3's LevelOfferGenerator on its Saltwind path (fill-state weighting,
    /// the Saltwind first level-up, the Saltwind Logbook pool, no run items), reading <see cref="ProgressionConfig"/>,
    /// <see cref="OfferWeightsConfig"/>, the weapon, Logbook and evolution configs. Every Loot draw happens where V3
    /// made it, in the same order (draws before duplicate checks, the guarantee replacing the last card).
    /// </summary>
    public sealed class OfferGenerator
    {
        readonly Func<ProgressionConfig> _progression;
        readonly Func<OfferWeightsConfig> _weights;
        readonly Func<IReadOnlyList<WeaponConfig>> _weapons;
        readonly Func<IReadOnlyList<LogbookConfig>> _logbooks;
        readonly Func<IReadOnlyList<WeaponEvolutionConfig>> _evolutions;
        readonly Func<bool> _evolutionsEnabled;

        public OfferGenerator(Func<ProgressionConfig> progression, Func<OfferWeightsConfig> weights, Func<IReadOnlyList<WeaponConfig>> weaponsInDatabaseOrder,
            Func<IReadOnlyList<LogbookConfig>> logbooksInDatabaseOrder, Func<IReadOnlyList<WeaponEvolutionConfig>> evolutions, Func<bool> evolutionsEnabled)
        {
            _progression = progression;
            _weights = weights;
            _weapons = weaponsInDatabaseOrder;
            _logbooks = logbooksInDatabaseOrder;
            _evolutions = evolutions;
            _evolutionsEnabled = evolutionsEnabled;
        }

        /// <summary>Weapons in V3's run database order (configs with a database position).</summary>
        public static List<WeaponConfig> InDatabaseOrder(IEnumerable<WeaponConfig> weapons) =>
            weapons.Where(w => w != null && w.databaseOrder >= 0).OrderBy(w => w.databaseOrder).ToList();

        public static List<LogbookConfig> InDatabaseOrder(IEnumerable<LogbookConfig> logbooks) =>
            logbooks.Where(l => l != null).OrderBy(l => l.databaseOrder).ToList();

        ProgressionConfig P => _progression();

        /// <summary>
        /// V3 LevelOfferGenerator.Generate with fill-state weighting (Saltwind). <paramref name="playerLevel"/> is the
        /// level after the XP grant (every pending level-up uses it, as V3).
        /// </summary>
        public List<Offer> Generate(IOfferRandom rng, IOfferContext ctx, IOfferState state, float luck, bool isFirstLevelUp, OfferPlayer player, int playerLevel)
        {
            var p = P;
            var count = p.cardsPerLevel;
            var result = new List<Offer>(count);

            if (isFirstLevelUp)
            {
                AddFirstLevelOffers(result, rng, ctx, state, luck);
                return FinalizeOffers(result, count, player);
            }

            var usedKeys = new HashSet<string>();
            for (var i = 0; i < count; i++)
            {
                Offer offer = default;
                for (var attempt = 0; attempt < p.attemptsPerCard; attempt++)
                {
                    offer = RollOffer(rng, ctx, state, luck, usedKeys);
                    if (!offer.IsEmpty) break;
                }
                if (!offer.IsEmpty)
                {
                    result.Add(offer);
                    if (p.preventDuplicateCards) usedKeys.Add(offer.Key);
                }
            }

            ApplyNewWeaponGuarantee(result, rng, ctx, state, playerLevel, usedKeys);
            return FinalizeOffers(result, count, player);
        }

        // ---- First level-up (V3 AddSaltwindFirstLevelOffers) ---------------------------------------------------------

        void AddFirstLevelOffers(List<Offer> result, IOfferRandom rng, IOfferContext ctx, IOfferState state, float luck)
        {
            var first = FindWeapon(P.firstLevelUpWeaponId);
            if (first != null)
            {
                var level = state.WeaponLevel(first.weaponId);
                result.Add(BuildWeaponUpgradeOffer(first, level, RollRarity(rng, luck), rng));
            }

            var newWeapon = PickNewWeapon(rng, ctx, state);
            if (newWeapon != null) result.Add(BuildNewWeaponOffer(newWeapon));

            var logbook = PickLogbook(rng, state, levelsEmpty: true, newOnly: false);
            if (logbook != null) result.Add(BuildLogbookOffer(logbook, 0, RollRarity(rng, luck)));
        }

        // ---- One card (V3 RollOffer, fill-state branch) ---------------------------------------------------------------

        Offer RollOffer(IOfferRandom rng, IOfferContext ctx, IOfferState state, float luck, HashSet<string> usedKeys)
        {
            var owned = state.OwnedWeapons;
            var canAddWeapon = owned.Count < P.weaponSlots && HasUnownedWeapon(state);
            var canUpgradeWeapon = HasUpgradeableWeapon(state)
                || P.evolutionOffersWhenMaxed && _evolutionsEnabled() && !TryWeaponEvolutionOffer(ctx, state, usedKeys).IsEmpty; // R-31
            var canNewLogbook = CanOfferNewLogbook(state);
            var canUpgradeLogbook = CanOfferLogbookUpgrade(state);

            var w = _weights().RowFor(owned.Count);
            var total = 0f;
            if (canUpgradeWeapon) total += w.weaponUpgrade;
            if (canAddWeapon) total += w.newWeapon;
            if (canUpgradeLogbook) total += w.logbookUpgrade;
            if (canNewLogbook) total += w.newLogbook;
            if (total <= 0f) return default;

            var roll = rng.LootFloat() * total;
            var acc = 0f;
            if (canUpgradeWeapon)
            {
                acc += w.weaponUpgrade;
                if (roll <= acc) return TryWeaponUpgradeOffer(rng, ctx, state, luck, usedKeys);
            }
            if (canAddWeapon)
            {
                acc += w.newWeapon;
                if (roll <= acc) return TryNewWeaponOffer(rng, ctx, state, usedKeys);
            }
            if (canUpgradeLogbook)
            {
                acc += w.logbookUpgrade;
                if (roll <= acc) return TryLogbookOffer(rng, state, luck, usedKeys, newOnly: false);
            }
            if (canNewLogbook)
            {
                acc += w.newLogbook;
                if (roll <= acc) return TryLogbookOffer(rng, state, luck, usedKeys, newOnly: true);
            }
            return default; // V3's run-item category is off in Saltwind
        }

        Offer TryNewWeaponOffer(IOfferRandom rng, IOfferContext ctx, IOfferState state, HashSet<string> usedKeys)
        {
            var def = PickNewWeapon(rng, ctx, state);
            if (def == null) return default;
            var offer = BuildNewWeaponOffer(def);
            return usedKeys.Contains(offer.Key) ? default : offer;
        }

        Offer TryWeaponUpgradeOffer(IOfferRandom rng, IOfferContext ctx, IOfferState state, float luck, HashSet<string> usedKeys)
        {
            if (_evolutionsEnabled())
            {
                var evolution = TryWeaponEvolutionOffer(ctx, state, usedKeys);
                if (!evolution.IsEmpty) return evolution;
            }
            var def = PickUpgradeWeapon(rng, state);
            if (def == null) return default;
            var level = state.WeaponLevel(def.weaponId);
            if (level >= def.maxLevel) return default;
            var offer = BuildWeaponUpgradeOffer(def, level, RollRarity(rng, luck), rng);
            return usedKeys.Contains(offer.Key) ? default : offer;
        }

        Offer TryWeaponEvolutionOffer(IOfferContext ctx, IOfferState state, HashSet<string> usedKeys)
        {
            foreach (var weaponId in state.OwnedWeapons)
            {
                var level = state.WeaponLevel(weaponId);
                // V3 WeaponEvolutionCatalog.TryGetEligible: max level, not evolved, has an evolution, owns its item.
                if (level < P.maxWeaponLevel || ctx.IsEvolved(weaponId)) continue;
                var evo = _evolutions()?.FirstOrDefault(e => e != null && e.baseWeapon != null && e.baseWeapon.weaponId == weaponId);
                if (evo == null || !ctx.HasItem(evo.requiredItemId)) continue;
                var offer = new Offer
                {
                    Kind = OfferKind.WeaponEvolution, Id = weaponId, EvolutionId = evo.evolutionId, Rarity = 4,
                    CurrentLevel = level, NextLevel = level, IsWeapon = true, IsNew = false,
                };
                if (usedKeys.Contains(offer.Key)) continue;
                return offer;
            }
            return default;
        }

        Offer TryLogbookOffer(IOfferRandom rng, IOfferState state, float luck, HashSet<string> usedKeys, bool newOnly)
        {
            var lb = PickLogbook(rng, state, levelsEmpty: false, newOnly);
            if (lb == null) return default;
            var offer = BuildLogbookOffer(lb, state.LogbookLevel(lb.logbookId), RollRarity(rng, luck));
            return usedKeys.Contains(offer.Key) ? default : offer;
        }

        // ---- New-weapon guarantee (V3 ApplyWeaponPityGuarantee) -------------------------------------------------------

        public static int GuaranteeTarget(ProgressionConfig p, int playerLevel)
        {
            var levels = p.newWeaponProtectionLevels ?? Array.Empty<int>();
            var target = 0;
            for (var i = 0; i < levels.Length; i++)
                if (playerLevel >= levels[i]) target = i + 3;
            return target;
        }

        void ApplyNewWeaponGuarantee(List<Offer> offers, IOfferRandom rng, IOfferContext ctx, IOfferState state, int playerLevel, HashSet<string> usedKeys)
        {
            var owned = state.OwnedWeapons.Count;
            var target = GuaranteeTarget(P, playerLevel);
            if (target <= 0 || owned >= target || owned >= P.weaponSlots) return;
            if (playerLevel <= P.guaranteeSkipMaxLevel && owned <= P.guaranteeSkipMaxWeapons) return;
            foreach (var o in offers) if (o.Kind == OfferKind.NewWeapon) return;
            var def = PickNewWeapon(rng, ctx, state);
            if (def == null) return;
            var offer = BuildNewWeaponOffer(def);
            if (offers.Count == 0) { offers.Add(offer); return; }
            var last = offers.Count - 1;
            usedKeys.Remove(offers[last].Key);
            offers[last] = offer;
            usedKeys.Add(offer.Key);
        }

        // ---- Fallbacks (V3 FinalizeOffers) ------------------------------------------------------------------------------

        public List<Offer> FinalizeOffers(List<Offer> offers, int targetCount, OfferPlayer player)
        {
            offers ??= new List<Offer>();
            var used = new HashSet<string>();
            foreach (var o in offers) if (!o.IsEmpty) used.Add(o.Key);
            var fallbacks = new List<Offer>(3);
            var p = P;
            if (player.MaxHull > 0f && player.Hull >= 0f && player.Hull < player.MaxHull * 0.999f) fallbacks.Add(Simple(OfferKind.FallbackHeal, "fallback_heal"));
            if (!player.HasStats || player.GlobalDamage + p.fallbackDamageBonus <= p.fallbackDamageCap + 0.0001f) fallbacks.Add(Simple(OfferKind.FallbackDamage, "fallback_damage"));
            if (!player.HasStats || player.MoveSpeed * p.fallbackMoveSpeedMultiplier <= p.fallbackMoveSpeedCap + 0.0001f) fallbacks.Add(Simple(OfferKind.FallbackMoveSpeed, "fallback_move_speed"));
            var index = 0;
            while (offers.Count < targetCount && index < fallbacks.Count)
            {
                var f = fallbacks[index++];
                if (!used.Add(f.Key)) continue;
                offers.Add(f);
            }
            if (offers.Count == 0) return new List<Offer> { Simple(OfferKind.Continue, "continue") };
            while (offers.Count > targetCount && offers.Count > 0) offers.RemoveAt(offers.Count - 1);
            return offers;
        }

        static Offer Simple(OfferKind kind, string id) => new Offer { Kind = kind, Id = id, Rarity = 0, Lines = Array.Empty<UpgradeLine>() };

        // ---- Picks ------------------------------------------------------------------------------------------------------

        WeaponConfig FindWeapon(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return null;
            foreach (var w in _weapons()) if (w.weaponId == weaponId) return w;
            return null;
        }

        bool HasUnownedWeapon(IOfferState state)
        {
            foreach (var w in _weapons()) if (!Contains(state.OwnedWeapons, w.weaponId)) return true;
            return false;
        }

        bool HasUpgradeableWeapon(IOfferState state)
        {
            foreach (var id in state.OwnedWeapons)
            {
                var def = FindWeapon(id);
                if (def == null) continue;
                var level = state.WeaponLevel(id);
                if (level <= 0) level = 1;
                if (!IsMaxed(def, level)) return true;
            }
            return false;
        }

        bool IsMaxed(WeaponConfig def, int level) => P.excludeMaxedWeapons && level >= def.maxLevel;

        bool CanOfferNewLogbook(IOfferState state)
        {
            if (state.OwnedLogbooks.Count >= P.logbookSlots) return false;
            foreach (var lb in _logbooks()) if (!Contains(state.OwnedLogbooks, lb.logbookId)) return true;
            return false;
        }

        bool CanOfferLogbookUpgrade(IOfferState state)
        {
            foreach (var id in state.OwnedLogbooks)
            {
                var def = _logbooks().FirstOrDefault(l => l.logbookId == id);
                if (def == null) continue;
                if (state.LogbookLevel(id) < def.maxLevel) return true;
            }
            return false;
        }

        WeaponConfig PickNewWeapon(IOfferRandom rng, IOfferContext ctx, IOfferState state)
        {
            var candidates = new List<WeaponConfig>();
            var weights = new List<float>();
            foreach (var w in _weapons())
            {
                if (Contains(state.OwnedWeapons, w.weaponId)) continue;
                if (!ctx.IsWeaponOfferable(w.weaponId)) continue;
                candidates.Add(w);
                weights.Add(1f);
            }
            return WeightedPick(candidates, weights, rng);
        }

        WeaponConfig PickUpgradeWeapon(IOfferRandom rng, IOfferState state)
        {
            var candidates = new List<WeaponConfig>();
            var weights = new List<float>();
            foreach (var id in state.OwnedWeapons)
            {
                var def = FindWeapon(id);
                if (def == null) continue;
                var level = state.WeaponLevel(id);
                if (level <= 0) level = 1;
                if (IsMaxed(def, level)) continue;
                candidates.Add(def);
                weights.Add(def.weaponId == P.firstLevelUpWeaponId ? P.firstLevelUpWeaponUpgradeWeight : 1f);
            }
            return WeightedPick(candidates, weights, rng);
        }

        LogbookConfig PickLogbook(IOfferRandom rng, IOfferState state, bool levelsEmpty, bool newOnly)
        {
            var candidates = new List<LogbookConfig>();
            var weights = new List<float>();
            foreach (var lb in _logbooks())
            {
                if (!lb.inSaltwindPool) continue;
                var owned = Contains(state.OwnedLogbooks, lb.logbookId);
                var level = levelsEmpty ? 0 : state.LogbookLevel(lb.logbookId);
                if (newOnly && owned) continue;
                if (!owned && state.OwnedLogbooks.Count >= P.logbookSlots) continue;
                if (owned && level >= lb.maxLevel) continue;
                candidates.Add(lb);
                weights.Add(owned ? P.ownedLogbookWeight : 1f);
            }
            return WeightedPick(candidates, weights, rng);
        }

        static T WeightedPick<T>(List<T> items, List<float> weights, IOfferRandom rng) where T : class
        {
            if (items.Count == 0) return null;
            var total = 0f;
            for (var i = 0; i < weights.Count; i++) total += weights[i];
            var roll = rng.LootFloat() * total;
            var acc = 0f;
            for (var i = 0; i < items.Count; i++)
            {
                acc += weights[i];
                if (roll <= acc) return items[i];
            }
            return items[items.Count - 1];
        }

        static bool Contains(IReadOnlyList<string> list, string id)
        {
            if (list == null) return false;
            for (var i = 0; i < list.Count; i++) if (list[i] == id) return true;
            return false;
        }

        // ---- Rarity and upgrade packages (V3 RunRng.RollRarity, RollUpgradePackage) -----------------------------------

        /// <summary>V3 RunRng.RollRarity(luck) with the legendary clamp: one Loot draw.</summary>
        public int RollRarity(IOfferRandom rng, float luck)
        {
            var p = P;
            var weights = new float[5];
            for (var i = 0; i < 5; i++)
                weights[i] = p.rarityBaseWeights[i] * Mathf.Pow(1f + luck, p.luckExponent * i);
            var totalBefore = 0f;
            for (var i = 0; i < 5; i++) totalBefore += weights[i];
            if (weights[4] / totalBefore > p.legendaryMaxShare) weights[4] = totalBefore * p.legendaryMaxShare;
            var total = 0f;
            for (var i = 0; i < 5; i++) total += weights[i];
            var roll = rng.LootFloat() * total;
            var acc = 0f;
            for (var i = 0; i < 5; i++)
            {
                acc += weights[i];
                if (roll <= acc) return i;
            }
            return 0;
        }

        Offer BuildWeaponUpgradeOffer(WeaponConfig def, int currentLevel, int rarity, IOfferRandom rng) => new Offer
        {
            Kind = OfferKind.WeaponUpgrade, Id = def.weaponId, Rarity = rarity,
            CurrentLevel = Mathf.Max(1, currentLevel), NextLevel = Mathf.Max(1, currentLevel) + 1,
            IsWeapon = true, IsNew = false, Lines = RollUpgradePackage(def, rarity, rng),
        };

        static Offer BuildNewWeaponOffer(WeaponConfig def) => new Offer
        {
            Kind = OfferKind.NewWeapon, Id = def.weaponId, Rarity = 0, CurrentLevel = 0, NextLevel = 1,
            IsWeapon = true, IsNew = true, Lines = Array.Empty<UpgradeLine>(),
        };

        static Offer BuildLogbookOffer(LogbookConfig def, int currentLevel, int rarity) => new Offer
        {
            Kind = OfferKind.Logbook, Id = def.logbookId, Rarity = rarity, CurrentLevel = currentLevel, NextLevel = currentLevel + 1,
            IsWeapon = false, IsNew = currentLevel <= 0, Lines = Array.Empty<UpgradeLine>(),
        };

        UpgradeLine[] RollUpgradePackage(WeaponConfig def, int rarity, IOfferRandom rng)
        {
            var p = P;
            var axes = def.upgradeAxes ?? Array.Empty<WeaponUpgradeAxis>();
            var lineCount = p.upgradeLinesPerRarity[rarity];
            var rarityMult = p.rarityMultipliers[rarity];
            var powerMult = p.upgradePowerMultiplier;
            var lines = new List<UpgradeLine>(lineCount);
            var used = new HashSet<ConfigEnums.UpgradeAxis>();
            for (var i = 0; i < lineCount; i++)
            {
                var cfg = PickAxis(axes, used, rng);
                if (cfg == null) break;
                used.Add(cfg.axis);
                var rolled = cfg.baseIncrement * rarityMult * powerMult;
                if (cfg.axis == ConfigEnums.UpgradeAxis.Quantity) rolled = Mathf.Max(1f, Mathf.Round(cfg.baseIncrement * rarityMult));
                lines.Add(new UpgradeLine(cfg.axis, rolled));
            }
            return lines.ToArray();
        }

        static WeaponUpgradeAxis PickAxis(WeaponUpgradeAxis[] axes, HashSet<ConfigEnums.UpgradeAxis> used, IOfferRandom rng)
        {
            var total = 0f;
            foreach (var a in axes) if (!used.Contains(a.axis)) total += Mathf.Max(0.01f, a.offerWeight);
            if (total <= 0f) return null;
            var roll = rng.LootFloat() * total;
            var acc = 0f;
            foreach (var a in axes)
            {
                if (used.Contains(a.axis)) continue;
                acc += Mathf.Max(0.01f, a.offerWeight);
                if (roll <= acc) return a;
            }
            return null;
        }
    }
}
