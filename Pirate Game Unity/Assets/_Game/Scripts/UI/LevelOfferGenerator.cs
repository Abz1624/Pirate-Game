using System;
using System.Collections.Generic;
using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.V3;
using HighSeas.Weapons;
using UnityEngine;

namespace HighSeas.UI
{
    public static class LevelOfferGenerator
    {
        struct OfferCategoryWeights
        {
            public float equippedWeaponUpgrade;
            public float newWeapon;
            public float equippedLogbookUpgrade;
            public float newLogbook;
            public float newRunItem;
        }

        public static List<LevelOffer> GenerateOpeningWeaponDraft(
            GameDatabase database,
            LevelUpSettings settings,
            RunRng rng,
            IReadOnlyList<string> ownedWeapons,
            int weaponSlots,
            int cardCount = 3)
        {
            var result = new List<LevelOffer>(cardCount);
            var used = new HashSet<string>();
            for (var i = 0; i < cardCount; i++)
            {
                var def = PickNewWeapon(database, settings, ownedWeapons, new Dictionary<string, int>(), rng, used);
                if (def == null) break;
                var offer = BuildNewWeaponOffer(def);
                used.Add(def.id);
                result.Add(offer);
                Combat.RunCombatTelemetry.Instance?.RecordNewWeaponOfferShown();
            }
            return PadOrTrim(result, cardCount);
        }

        public static List<LevelOffer> Generate(
            GameDatabase database,
            LevelUpSettings settings,
            DemoPacingSettings pacing,
            RunRng rng,
            float luck,
            IReadOnlyList<string> ownedWeapons,
            IReadOnlyDictionary<string, int> weaponLevels,
            IReadOnlyList<string> ownedLogbooks,
            IReadOnlyDictionary<string, int> logbookLevels,
            int weaponSlots,
            int logbookSlots,
            bool isFirstLevelUp,
            PlayerStats stats = null,
            float currentHull = -1f,
            float maxHull = -1f,
            int playerLevel = 1,
            bool useFillStateWeighting = false)
        {
            var count = settings != null ? settings.cardsPerLevel : 3;
            var result = new List<LevelOffer>(count);

            if (isFirstLevelUp && settings != null && settings.scriptFirstLevelUp && !useFillStateWeighting)
            {
                AddFirstLevelOffers(result, database, settings, rng, luck, ownedWeapons, weaponLevels, ownedLogbooks, logbookSlots, pacing);
                return FinalizeOffers(result, count, rng, stats, currentHull, maxHull);
            }

            if (isFirstLevelUp && useFillStateWeighting && UsesMixedSaltwindLevelOffers())
            {
                AddMixedEarlyLevelOffers(
                    result,
                    database,
                    settings,
                    rng,
                    luck,
                    ownedWeapons,
                    weaponLevels,
                    ownedLogbooks,
                    logbookLevels,
                    logbookSlots,
                    weaponSlots,
                    count);
                return FinalizeOffers(result, count, rng, stats, currentHull, maxHull);
            }

            var usedKeys = new HashSet<string>();
            for (var i = 0; i < count; i++)
            {
                LevelOffer offer = default;
                for (var attempt = 0; attempt < 24; attempt++)
                {
                    offer = RollOffer(
                        database,
                        settings,
                        pacing,
                        rng,
                        luck,
                        ownedWeapons,
                        weaponLevels,
                        ownedLogbooks,
                        logbookLevels,
                        weaponSlots,
                        logbookSlots,
                        usedKeys,
                        useFillStateWeighting);
                    if (offer.id != null) break;
                }

                if (offer.id != null)
                {
                    result.Add(offer);
                    if (settings == null || settings.preventDuplicateCards)
                        usedKeys.Add(GetOfferKey(offer));
                }
            }

            if (useFillStateWeighting)
                ApplyWeaponPityGuarantee(result, database, settings, rng, ownedWeapons, weaponLevels, weaponSlots, playerLevel, usedKeys);

            return FinalizeOffers(useFillStateWeighting
                ? result
                : EnsureDemoNewWeaponOffer(
                    result,
                    database,
                    settings,
                    ownedWeapons,
                    weaponLevels,
                    rng,
                    count), count, rng, stats, currentHull, maxHull);
        }

        public static List<LevelOffer> FinalizeOffers(
            List<LevelOffer> offers,
            int targetCount,
            RunRng rng,
            PlayerStats stats,
            float currentHull,
            float maxHull)
        {
            offers ??= new List<LevelOffer>();
            var used = new HashSet<string>();
            for (var i = 0; i < offers.Count; i++)
            {
                if (offers[i].id == null) continue;
                used.Add(GetOfferKey(offers[i]));
            }

            var fallbacks = BuildFallbackPool(stats, currentHull, maxHull);
            var fallbackIndex = 0;
            while (offers.Count < targetCount && fallbackIndex < fallbacks.Count)
            {
                var fallback = fallbacks[fallbackIndex++];
                var key = GetOfferKey(fallback);
                if (used.Contains(key)) continue;
                used.Add(key);
                offers.Add(fallback);
            }

            if (offers.Count == 0)
                return new List<LevelOffer> { BuildContinueOffer() };

            return PadOrTrim(offers, targetCount);
        }

        static List<LevelOffer> BuildFallbackPool(PlayerStats stats, float currentHull, float maxHull)
        {
            var pool = new List<LevelOffer>(3);
            if (IsFallbackHealUseful(currentHull, maxHull))
                pool.Add(BuildFallbackHealOffer());
            if (IsFallbackDamageUseful(stats))
                pool.Add(BuildFallbackDamageOffer());
            if (IsFallbackMoveSpeedUseful(stats))
                pool.Add(BuildFallbackMoveSpeedOffer());
            return pool;
        }

        public static bool IsFallbackHealUseful(float currentHull, float maxHull) =>
            maxHull > 0f && currentHull >= 0f && currentHull < maxHull * 0.999f;

        public static bool IsFallbackDamageUseful(PlayerStats stats) =>
            stats == null
            || stats.GlobalDamage + GameConstants.LevelOfferFallbackDamageBonus
               <= GameConstants.MaxGlobalDamageMultiplier + 0.0001f;

        public static bool IsFallbackMoveSpeedUseful(PlayerStats stats)
        {
            if (stats == null) return true;
            var projected = stats.MoveSpeed * GameConstants.LevelOfferFallbackMoveSpeedMultiplier;
            return projected <= GameConstants.MaxMoveSpeed + 0.0001f;
        }

        public static LevelOffer BuildContinueOffer() => new()
        {
            kind = LevelOfferKind.Continue,
            id = "continue",
            title = "Continue",
            categoryLabel = "No Upgrade",
            description = "Every upgrade is capped. Sail on without a level reward.",
            rarity = Rarity.Common,
            currentLevel = 0,
            nextLevel = 0,
            isWeapon = false,
            isNew = false,
            upgradeLines = Array.Empty<WeaponUpgradeLine>()
        };

        public static LevelOffer BuildFallbackHealOffer() => new()
        {
            kind = LevelOfferKind.FallbackHeal,
            id = "fallback_heal",
            title = "Patch the Hull",
            categoryLabel = "Fallback",
            description = $"Restore {GameConstants.LevelOfferFallbackHealFraction * 100f:0}% maximum Hull.",
            rarity = Rarity.Common,
            currentLevel = 0,
            nextLevel = 0,
            isWeapon = false,
            isNew = false,
            upgradeLines = Array.Empty<WeaponUpgradeLine>()
        };

        public static LevelOffer BuildFallbackDamageOffer() => new()
        {
            kind = LevelOfferKind.FallbackDamage,
            id = "fallback_damage",
            title = "Powder Reserve",
            categoryLabel = "Fallback",
            description = $"+{GameConstants.LevelOfferFallbackDamageBonus * 100f:0}% damage for this run.",
            rarity = Rarity.Common,
            currentLevel = 0,
            nextLevel = 0,
            isWeapon = false,
            isNew = false,
            upgradeLines = Array.Empty<WeaponUpgradeLine>()
        };

        public static LevelOffer BuildFallbackMoveSpeedOffer() => new()
        {
            kind = LevelOfferKind.FallbackMoveSpeed,
            id = "fallback_move_speed",
            title = "Fair Wind",
            categoryLabel = "Fallback",
            description = $"+{(GameConstants.LevelOfferFallbackMoveSpeedMultiplier - 1f) * 100f:0}% movement speed for this run.",
            rarity = Rarity.Common,
            currentLevel = 0,
            nextLevel = 0,
            isWeapon = false,
            isNew = false,
            upgradeLines = Array.Empty<WeaponUpgradeLine>()
        };

        static List<LevelOffer> EnsureDemoNewWeaponOffer(
            List<LevelOffer> offers,
            GameDatabase database,
            LevelUpSettings settings,
            IReadOnlyList<string> ownedWeapons,
            IReadOnlyDictionary<string, int> weaponLevels,
            RunRng rng,
            int count)
        {
            if (settings == null || !settings.alwaysIncludeNewWeapon) return offers;
            if (!HasUnownedWeapon(database, ownedWeapons)) return offers;

            for (var i = 0; i < offers.Count; i++)
            {
                if (offers[i].kind == LevelOfferKind.NewWeapon)
                    return offers;
            }

            var def = PickNewWeapon(database, settings, ownedWeapons, weaponLevels, rng);
            if (def == null) return offers;

            var offer = BuildNewWeaponOffer(def);
            for (var i = offers.Count - 1; i >= 0; i--)
            {
                if (offers[i].kind == LevelOfferKind.NewWeapon) continue;
                offers[i] = offer;
                return offers;
            }

            if (offers.Count < count)
                offers.Add(offer);
            else if (offers.Count > 0)
                offers[offers.Count - 1] = offer;

            return offers;
        }

        static void AddFirstLevelOffers(
            List<LevelOffer> result,
            GameDatabase database,
            LevelUpSettings settings,
            RunRng rng,
            float luck,
            IReadOnlyList<string> ownedWeapons,
            IReadOnlyDictionary<string, int> weaponLevels,
            IReadOnlyList<string> ownedLogbooks,
            int logbookSlots,
            DemoPacingSettings pacing)
        {
            var starterId = settings.startingWeaponId;
            var starter = database.GetWeapon(starterId);
            if (starter != null)
            {
                weaponLevels.TryGetValue(starterId, out var level);
                result.Add(BuildWeaponUpgradeOffer(starter, level, rng.RollRarity(luck), settings, pacing, rng));
            }

            var newWeapon = PickNewWeapon(database, settings, ownedWeapons, weaponLevels, rng);
            if (newWeapon != null)
                result.Add(BuildNewWeaponOffer(newWeapon));

            var logbook = PickLogbook(database, ownedLogbooks, new Dictionary<string, int>(), logbookSlots, rng);
            if (logbook != null)
            {
                var rarity = rng.RollRarity(luck);
                var lbLevel = ContainsId(ownedLogbooks, logbook.id) ? 1 : 0;
                result.Add(BuildLogbookOffer(logbook, lbLevel, rarity));
            }
        }

        static bool UsesMixedSaltwindLevelOffers() =>
            V3RunContext.IsActive || StageBalanceProfileContext.IsActive;

        static void AddMixedEarlyLevelOffers(
            List<LevelOffer> result,
            GameDatabase database,
            LevelUpSettings settings,
            RunRng rng,
            float luck,
            IReadOnlyList<string> ownedWeapons,
            IReadOnlyDictionary<string, int> weaponLevels,
            IReadOnlyList<string> ownedLogbooks,
            IReadOnlyDictionary<string, int> logbookLevels,
            int logbookSlots,
            int weaponSlots,
            int count)
        {
            var used = new HashSet<string>();
            TryAddMixedOffer(result, used, TryRunItemOffer(rng, luck, used));
            TryAddMixedOffer(result, used, TryLogbookOffer(database, ownedLogbooks, logbookLevels, logbookSlots, rng, luck, used, newOnly: true));
            if (ownedWeapons.Count < weaponSlots)
                TryAddMixedOffer(result, used, TryNewWeaponOffer(database, settings, ownedWeapons, weaponLevels, rng, used));

            var attempts = 0;
            while (result.Count < count && attempts < 48)
            {
                attempts++;
                var roll = rng.LootFloat();
                LevelOffer candidate = roll switch
                {
                    < 0.34f => TryRunItemOffer(rng, luck, used),
                    < 0.62f => TryLogbookOffer(database, ownedLogbooks, logbookLevels, logbookSlots, rng, luck, used, newOnly: false),
                    _ => TryNewWeaponOffer(database, settings, ownedWeapons, weaponLevels, rng, used)
                };
                TryAddMixedOffer(result, used, candidate);
            }
        }

        static void TryAddMixedOffer(List<LevelOffer> result, HashSet<string> used, LevelOffer offer)
        {
            if (offer.id == null) return;
            var key = GetOfferKey(offer);
            if (used.Contains(key)) return;
            used.Add(key);
            result.Add(offer);
        }

        static bool ContainsId(IReadOnlyList<string> list, string id)
        {
            if (list == null) return false;
            for (var i = 0; i < list.Count; i++)
                if (list[i] == id) return true;
            return false;
        }

        static List<LevelOffer> PadOrTrim(List<LevelOffer> offers, int count)
        {
            while (offers.Count > count && offers.Count > 0)
                offers.RemoveAt(offers.Count - 1);
            return offers;
        }

        static LevelOffer RollOffer(
            GameDatabase database,
            LevelUpSettings settings,
            DemoPacingSettings pacing,
            RunRng rng,
            float luck,
            IReadOnlyList<string> ownedWeapons,
            IReadOnlyDictionary<string, int> weaponLevels,
            IReadOnlyList<string> ownedLogbooks,
            IReadOnlyDictionary<string, int> logbookLevels,
            int weaponSlots,
            int logbookSlots,
            HashSet<string> usedKeys,
            bool useFillStateWeighting)
        {
            var canAddWeapon = ownedWeapons.Count < weaponSlots && HasUnownedWeapon(database, ownedWeapons);
            var canUpgradeWeapon = HasUpgradeableWeapon(database, settings, ownedWeapons, weaponLevels);
            var canOfferLogbook = database.logbooks != null && database.logbooks.Length > 0;
            var canNewLogbook = CanOfferNewLogbook(ownedLogbooks, logbookSlots, database, logbookLevels);
            var canUpgradeLogbook = CanOfferLogbookUpgrade(ownedLogbooks, database, logbookLevels);
            var canRunItem = UsesMixedSaltwindLevelOffers() && V3ItemCatalog.InteractionItemIds.Length > 0;

            if (useFillStateWeighting)
            {
                var weights = GetFillStateWeights(ownedWeapons.Count);
                var total = 0f;
                if (canUpgradeWeapon) total += weights.equippedWeaponUpgrade;
                if (canAddWeapon) total += weights.newWeapon;
                if (canUpgradeLogbook) total += weights.equippedLogbookUpgrade;
                if (canNewLogbook) total += weights.newLogbook;
                if (canRunItem) total += weights.newRunItem;
                if (total <= 0f) return default;

                var roll = rng.LootFloat() * total;
                var acc = 0f;
                if (canUpgradeWeapon)
                {
                    acc += weights.equippedWeaponUpgrade;
                    if (roll <= acc)
                        return TryWeaponUpgradeOffer(database, settings, pacing, rng, luck, ownedWeapons, weaponLevels, usedKeys);
                }
                if (canAddWeapon)
                {
                    acc += weights.newWeapon;
                    if (roll <= acc)
                        return TryNewWeaponOffer(database, settings, ownedWeapons, weaponLevels, rng, usedKeys);
                }
                if (canUpgradeLogbook)
                {
                    acc += weights.equippedLogbookUpgrade;
                    if (roll <= acc)
                        return TryLogbookOffer(database, ownedLogbooks, logbookLevels, logbookSlots, rng, luck, usedKeys, newOnly: false);
                }
                if (canNewLogbook)
                {
                    acc += weights.newLogbook;
                    if (roll <= acc)
                        return TryLogbookOffer(database, ownedLogbooks, logbookLevels, logbookSlots, rng, luck, usedKeys, newOnly: true);
                }
                if (canRunItem)
                    return TryRunItemOffer(rng, luck, usedKeys);
                return default;
            }

            var weaponWeight = settings != null ? settings.weaponOfferWeight : 0.55f;
            if (!canAddWeapon && !canUpgradeWeapon) weaponWeight = 0f;
            if (!canOfferLogbook) weaponWeight = 1f;

            var pickWeapon = rng.LootFloat() < weaponWeight;
            if (pickWeapon)
            {
                if (canAddWeapon && (!canUpgradeWeapon || rng.LootFloat() < 0.45f))
                    return TryNewWeaponOffer(database, settings, ownedWeapons, weaponLevels, rng, usedKeys);
                if (canUpgradeWeapon)
                    return TryWeaponUpgradeOffer(database, settings, pacing, rng, luck, ownedWeapons, weaponLevels, usedKeys);
            }

            if (canOfferLogbook)
                return TryLogbookOffer(database, ownedLogbooks, logbookLevels, logbookSlots, rng, luck, usedKeys, newOnly: false);

            return default;
        }

        static OfferCategoryWeights GetFillStateWeights(int equippedWeapons)
        {
            var mixedEarly = UsesMixedSaltwindLevelOffers();
            if (equippedWeapons <= 2)
            {
                return mixedEarly
                    ? new OfferCategoryWeights { equippedWeaponUpgrade = 22f, newWeapon = 24f, equippedLogbookUpgrade = 16f, newLogbook = 14f, newRunItem = 24f }
                    : new OfferCategoryWeights { equippedWeaponUpgrade = 23f, newWeapon = 42f, equippedLogbookUpgrade = 18f, newLogbook = 9f, newRunItem = 8f };
            }
            if (equippedWeapons == 3) return new OfferCategoryWeights { equippedWeaponUpgrade = 30f, newWeapon = 26f, equippedLogbookUpgrade = 18f, newLogbook = 12f, newRunItem = 14f };
            if (equippedWeapons == 4) return new OfferCategoryWeights { equippedWeaponUpgrade = 34f, newWeapon = 22f, equippedLogbookUpgrade = 18f, newLogbook = 12f, newRunItem = 14f };
            if (equippedWeapons == 5) return new OfferCategoryWeights { equippedWeaponUpgrade = 40f, newWeapon = 16f, equippedLogbookUpgrade = 18f, newLogbook = 12f, newRunItem = 14f };
            return new OfferCategoryWeights { equippedWeaponUpgrade = 52f, newWeapon = 0f, equippedLogbookUpgrade = 22f, newLogbook = 12f, newRunItem = 14f };
        }

        static LevelOffer TryRunItemOffer(RunRng rng, float luck, HashSet<string> usedKeys)
        {
            var pool = V3ItemCatalog.InteractionItemIds;
            if (pool.Length == 0) return default;
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var id = pool[rng.LootInt(0, pool.Length)];
                if (!V3ContentUnlocks.IsItemUnlocked(id)) continue;
                var offer = BuildRunItemOffer(id, rng.RollRarity(luck));
                if (usedKeys.Contains(GetOfferKey(offer))) continue;
                return offer;
            }

            return default;
        }

        public static LevelOffer BuildRunItemOffer(string itemId, Rarity rarity)
        {
            return new LevelOffer
            {
                kind = LevelOfferKind.NewRunItem,
                id = itemId,
                title = V3ItemCatalog.GetDisplayName(itemId),
                categoryLabel = "Run Item",
                description = V3ItemCatalog.GetDescription(itemId),
                rarity = rarity,
                currentLevel = 0,
                nextLevel = 1,
                isWeapon = false,
                isNew = true,
                upgradeLines = System.Array.Empty<WeaponUpgradeLine>()
            };
        }

        static int GetPityTargetWeaponCount(int playerLevel)
        {
            var levels = StageBalanceProfileContext.IsActive
                ? StageBalanceProfileContext.Export?.Progression?.NewWeaponOfferProtectionLevels
                : null;
            return SaltwindStageProgressionLogic.GetNewWeaponPityTargetCount(playerLevel, levels);
        }

        static void ApplyWeaponPityGuarantee(
            List<LevelOffer> offers,
            GameDatabase database,
            LevelUpSettings settings,
            RunRng rng,
            IReadOnlyList<string> ownedWeapons,
            IReadOnlyDictionary<string, int> weaponLevels,
            int weaponSlots,
            int playerLevel,
            HashSet<string> usedKeys)
        {
            var target = GetPityTargetWeaponCount(playerLevel);
            if (target <= 0 || ownedWeapons.Count >= target || ownedWeapons.Count >= weaponSlots) return;
            if (UsesMixedSaltwindLevelOffers() && playerLevel <= 5 && ownedWeapons.Count <= 3) return;
            for (var i = 0; i < offers.Count; i++)
            {
                if (offers[i].kind == LevelOfferKind.NewWeapon) return;
            }

            var def = PickNewWeapon(database, settings, ownedWeapons, weaponLevels, rng);
            if (def == null) return;
            var offer = BuildNewWeaponOffer(def);
            if (offers.Count == 0)
            {
                offers.Add(offer);
                return;
            }

            var replaceIndex = offers.Count - 1;
            usedKeys.Remove(GetOfferKey(offers[replaceIndex]));
            offers[replaceIndex] = offer;
            usedKeys.Add(GetOfferKey(offer));
        }

        static LevelOffer TryNewWeaponOffer(
            GameDatabase database,
            LevelUpSettings settings,
            IReadOnlyList<string> ownedWeapons,
            IReadOnlyDictionary<string, int> weaponLevels,
            RunRng rng,
            HashSet<string> usedKeys)
        {
            var def = PickNewWeapon(database, settings, ownedWeapons, weaponLevels, rng);
            if (def == null) return default;
            var offer = BuildNewWeaponOffer(def);
            return usedKeys.Contains(GetOfferKey(offer)) ? default : offer;
        }

        static LevelOffer TryWeaponUpgradeOffer(
            GameDatabase database,
            LevelUpSettings settings,
            DemoPacingSettings pacing,
            RunRng rng,
            float luck,
            IReadOnlyList<string> ownedWeapons,
            IReadOnlyDictionary<string, int> weaponLevels,
            HashSet<string> usedKeys)
        {
            if (CombatExperimentContext.Active.weaponEvolutionsEnabled)
            {
                var evolution = TryWeaponEvolutionOffer(ownedWeapons, weaponLevels, usedKeys);
                if (evolution.id != null) return evolution;
            }

            var def = PickUpgradeWeapon(database, settings, ownedWeapons, weaponLevels, rng);
            if (def == null) return default;
            weaponLevels.TryGetValue(def.id, out var level);
            if (level >= def.maxLevel) return default;
            var offer = BuildWeaponUpgradeOffer(def, level, rng.RollRarity(luck), settings, pacing, rng);
            return usedKeys.Contains(GetOfferKey(offer)) ? default : offer;
        }

        static LevelOffer TryWeaponEvolutionOffer(
            IReadOnlyList<string> ownedWeapons,
            IReadOnlyDictionary<string, int> weaponLevels,
            HashSet<string> usedKeys)
        {
            for (var i = 0; i < ownedWeapons.Count; i++)
            {
                var weaponId = ownedWeapons[i];
                weaponLevels.TryGetValue(weaponId, out var level);
                if (!WeaponEvolutionCatalog.TryGetEligible(weaponId, level, out var evo)) continue;
                var offer = BuildEvolutionOffer(evo, level);
                if (usedKeys.Contains(GetOfferKey(offer))) continue;
                return offer;
            }

            return default;
        }

        static LevelOffer BuildEvolutionOffer(WeaponEvolutionDefinition evo, int level) =>
            new()
            {
                kind = LevelOfferKind.WeaponEvolution,
                id = evo.baseWeaponId,
                evolutionId = evo.evolutionId,
                title = evo.displayName,
                categoryLabel = "WEAPON EVOLUTION",
                description = evo.description,
                rarity = Rarity.Legendary,
                currentLevel = level,
                nextLevel = level,
                isWeapon = true,
                isNew = false
            };

        static LevelOffer TryLogbookOffer(
            GameDatabase database,
            IReadOnlyList<string> ownedLogbooks,
            IReadOnlyDictionary<string, int> logbookLevels,
            int logbookSlots,
            RunRng rng,
            float luck,
            HashSet<string> usedKeys,
            bool newOnly)
        {
            var lb = PickLogbook(database, ownedLogbooks, logbookLevels, logbookSlots, rng, newOnly);
            if (lb == null) return default;
            logbookLevels.TryGetValue(lb.id, out var lbLevel);
            var offer = BuildLogbookOffer(lb, lbLevel, rng.RollRarity(luck));
            return usedKeys.Contains(GetOfferKey(offer)) ? default : offer;
        }

        static bool CanOfferNewLogbook(
            IReadOnlyList<string> ownedLogbooks,
            int logbookSlots,
            GameDatabase database,
            IReadOnlyDictionary<string, int> logbookLevels)
        {
            if (ownedLogbooks.Count >= logbookSlots) return false;
            if (database.logbooks == null) return false;
            for (var i = 0; i < database.logbooks.Length; i++)
            {
                var lb = database.logbooks[i];
                if (lb == null || ContainsId(ownedLogbooks, lb.id)) continue;
                return true;
            }
            return false;
        }

        static bool CanOfferLogbookUpgrade(
            IReadOnlyList<string> ownedLogbooks,
            GameDatabase database,
            IReadOnlyDictionary<string, int> logbookLevels)
        {
            for (var i = 0; i < ownedLogbooks.Count; i++)
            {
                var def = database.GetLogbook(ownedLogbooks[i]);
                if (def == null) continue;
                logbookLevels.TryGetValue(ownedLogbooks[i], out var level);
                if (level < def.maxLevel) return true;
            }
            return false;
        }

        static string GetOfferKey(LevelOffer offer) =>
            offer.kind == LevelOfferKind.WeaponEvolution
                ? $"{offer.kind}:{offer.id}:{offer.evolutionId}"
                : $"{offer.kind}:{offer.id}";

        static bool HasUnownedWeapon(GameDatabase database, IReadOnlyList<string> ownedWeapons)
        {
            if (database.weapons == null) return false;
            for (var i = 0; i < database.weapons.Length; i++)
            {
                var w = database.weapons[i];
                if (w == null) continue;
                if (!ContainsId(ownedWeapons, w.id)) return true;
            }
            return false;
        }

        static bool HasUpgradeableWeapon(
            GameDatabase database,
            LevelUpSettings settings,
            IReadOnlyList<string> ownedWeapons,
            IReadOnlyDictionary<string, int> weaponLevels)
        {
            for (var i = 0; i < ownedWeapons.Count; i++)
            {
                var def = database.GetWeapon(ownedWeapons[i]);
                if (def == null) continue;
                weaponLevels.TryGetValue(ownedWeapons[i], out var level);
                if (level <= 0) level = 1;
                if (!IsWeaponMaxed(def, level, settings)) return true;
            }
            return false;
        }

        static bool IsWeaponMaxed(WeaponDefinition def, int level, LevelUpSettings settings)
        {
            if (settings != null && !settings.excludeMaxedWeapons) return false;
            return level >= def.maxLevel;
        }

        static WeaponDefinition PickNewWeapon(
            GameDatabase database,
            LevelUpSettings settings,
            IReadOnlyList<string> ownedWeapons,
            IReadOnlyDictionary<string, int> weaponLevels,
            RunRng rng,
            HashSet<string> excludedIds = null)
        {
            var candidates = new List<WeaponDefinition>();
            var weights = new List<float>();
            for (var i = 0; i < database.weapons.Length; i++)
            {
                var w = database.weapons[i];
                if (w == null || ContainsId(ownedWeapons, w.id)) continue;
                if (excludedIds != null && excludedIds.Contains(w.id)) continue;
                if (!IsWeaponOfferEligible(w.id)) continue;
                candidates.Add(w);
                weights.Add(1f);
            }

            return WeightedPick(candidates, weights, rng);
        }

        static bool IsWeaponOfferEligible(string weaponId)
        {
            if (!Combat.WeaponFeatureFlags.IsWeaponEnabled(weaponId))
                return false;
            if (!V3.V3RunContext.IsActive) return true;
            return V3.V3ContentUnlocks.IsWeaponUnlocked(weaponId);
        }

        static WeaponDefinition PickUpgradeWeapon(
            GameDatabase database,
            LevelUpSettings settings,
            IReadOnlyList<string> ownedWeapons,
            IReadOnlyDictionary<string, int> weaponLevels,
            RunRng rng)
        {
            var candidates = new List<WeaponDefinition>();
            var weights = new List<float>();
            for (var i = 0; i < ownedWeapons.Count; i++)
            {
                var def = database.GetWeapon(ownedWeapons[i]);
                if (def == null) continue;
                weaponLevels.TryGetValue(ownedWeapons[i], out var level);
                if (level <= 0) level = 1;
                if (IsWeaponMaxed(def, level, settings)) continue;
                candidates.Add(def);
                var weight = def.id == settings?.startingWeaponId ? 1.2f : 1f;
                weights.Add(weight);
            }

            return WeightedPick(candidates, weights, rng);
        }

        static LogbookDefinition PickLogbook(
            GameDatabase database,
            IReadOnlyList<string> ownedLogbooks,
            IReadOnlyDictionary<string, int> logbookLevels,
            int logbookSlots,
            RunRng rng,
            bool newOnly = false)
        {
            var candidates = new List<LogbookDefinition>();
            var weights = new List<float>();
            for (var i = 0; i < database.logbooks.Length; i++)
            {
                var lb = database.logbooks[i];
                if (lb == null) continue;
                var owned = ContainsId(ownedLogbooks, lb.id);
                logbookLevels.TryGetValue(lb.id, out var level);
                if (newOnly && owned) continue;
                if (!owned && ownedLogbooks.Count >= logbookSlots) continue;
                if (owned && level >= lb.maxLevel) continue;
                candidates.Add(lb);
                weights.Add(owned ? 1.5f : 1f);
            }

            return WeightedPick(candidates, weights, rng);
        }

        static T WeightedPick<T>(List<T> items, List<float> weights, RunRng rng)
        {
            if (items.Count == 0) return default;
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

        public static LevelOffer BuildNewWeaponOffer(WeaponDefinition def) => new()
        {
            kind = LevelOfferKind.NewWeapon,
            id = def.id,
            title = def.displayName,
            categoryLabel = "New Weapon",
            description = V3ContentDescriptions.GetWeaponDescription(def),
            rarity = Rarity.Uncommon,
            currentLevel = 0,
            nextLevel = 1,
            isWeapon = true,
            isNew = true,
            upgradeLines = System.Array.Empty<WeaponUpgradeLine>()
        };

        public static LevelOffer BuildWeaponUpgradeOffer(
            WeaponDefinition def,
            int currentLevel,
            Rarity rarity,
            LevelUpSettings settings,
            DemoPacingSettings pacing,
            RunRng rng)
        {
            var lines = RollUpgradePackage(def, rarity, settings, pacing, rng);
            var upgradeText = string.Join("\n", System.Array.ConvertAll(lines, l => l.displayText));
            return new LevelOffer
            {
                kind = LevelOfferKind.WeaponUpgrade,
                id = def.id,
                title = def.displayName,
                categoryLabel = "Weapon Upgrade",
                description = $"{V3ContentDescriptions.GetWeaponDescription(def)}\n{upgradeText}",
                rarity = rarity,
                currentLevel = Mathf.Max(1, currentLevel),
                nextLevel = Mathf.Max(1, currentLevel) + 1,
                isWeapon = true,
                isNew = false,
                upgradeLines = lines
            };
        }

        public static LevelOffer BuildLogbookOffer(LogbookDefinition def, int currentLevel, Rarity rarity)
        {
            var mult = GameConstants.RarityMultipliers[(int)rarity];
            var value = def.perLevelAdditive * mult;
            var desc = def.primaryStat == StatId.Quantity && value >= 1f
                ? $"+{Mathf.RoundToInt(value)} {def.primaryStat}"
                : $"+{value * 100f:0}% {def.primaryStat}";
            return new LevelOffer
            {
                kind = LevelOfferKind.Logbook,
                id = def.id,
                title = def.displayName,
                categoryLabel = currentLevel > 0 ? "Logbook Upgrade" : "New Logbook",
                description = $"{V3ContentDescriptions.GetLogbookDescription(def)}\n{desc}",
                rarity = rarity,
                currentLevel = currentLevel,
                nextLevel = currentLevel + 1,
                isWeapon = false,
                isNew = currentLevel <= 0,
                upgradeLines = System.Array.Empty<WeaponUpgradeLine>()
            };
        }

        static WeaponUpgradeLine[] RollUpgradePackage(
            WeaponDefinition def,
            Rarity rarity,
            LevelUpSettings settings,
            DemoPacingSettings pacing,
            RunRng rng)
        {
            var configs = def.GetUpgradeConfigs();
            var lineCount = settings != null ? settings.StatLinesForRarity(rarity) : 1;
            var rarityMult = GameConstants.RarityMultipliers[(int)rarity];
            var powerMult = settings != null ? settings.upgradePowerMultiplier : 1f;
            if (pacing != null) powerMult *= pacing.upgradePowerMultiplier;

            var lines = new List<WeaponUpgradeLine>(lineCount);
            var usedAxes = new HashSet<UpgradeAxis>();
            for (var i = 0; i < lineCount; i++)
            {
                var cfg = PickAxisConfig(configs, usedAxes, rng);
                if (cfg == null) break;
                usedAxes.Add(cfg.Value.axis);
                var rolled = cfg.Value.baseIncrement * rarityMult * powerMult;
                if (cfg.Value.axis == UpgradeAxis.Quantity)
                    rolled = Mathf.Max(1f, Mathf.Round(cfg.Value.baseIncrement * rarityMult));
                lines.Add(new WeaponUpgradeLine
                {
                    axis = cfg.Value.axis,
                    value = rolled,
                    displayText = cfg.Value.FormatValue(rolled)
                });
            }

            return lines.ToArray();
        }

        static WeaponUpgradeAxisConfig? PickAxisConfig(
            IReadOnlyList<WeaponUpgradeAxisConfig> configs,
            HashSet<UpgradeAxis> used,
            RunRng rng)
        {
            var total = 0f;
            for (var i = 0; i < configs.Count; i++)
            {
                if (used.Contains(configs[i].axis)) continue;
                total += Mathf.Max(0.01f, configs[i].offerWeight);
            }
            if (total <= 0f) return null;

            var roll = rng.LootFloat() * total;
            var acc = 0f;
            for (var i = 0; i < configs.Count; i++)
            {
                if (used.Contains(configs[i].axis)) continue;
                acc += Mathf.Max(0.01f, configs[i].offerWeight);
                if (roll <= acc) return configs[i];
            }

            return null;
        }

    }
}
