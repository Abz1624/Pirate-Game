using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Weapons;
using UnityEngine;

namespace HighSeas.UI
{
    public static class LevelOfferGenerator
    {
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
            bool isFirstLevelUp)
        {
            var count = settings != null ? settings.cardsPerLevel : 3;
            var result = new List<LevelOffer>(count);

            if (isFirstLevelUp && settings != null && settings.scriptFirstLevelUp)
            {
                AddFirstLevelOffers(result, database, settings, rng, luck, ownedWeapons, weaponLevels, ownedLogbooks, logbookSlots, pacing);
                return PadOrTrim(result, count);
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
                        usedKeys);
                    if (offer.id != null) break;
                }

                if (offer.id != null)
                {
                    result.Add(offer);
                    if (settings == null || settings.preventDuplicateCards)
                        usedKeys.Add(GetOfferKey(offer));
                }
            }

            return PadOrTrim(EnsureDemoNewWeaponOffer(
                result,
                database,
                settings,
                ownedWeapons,
                weaponLevels,
                rng,
                count), count);
        }

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
            HashSet<string> usedKeys)
        {
            var canAddWeapon = ownedWeapons.Count < weaponSlots && HasUnownedWeapon(database, ownedWeapons);
            var canUpgradeWeapon = HasUpgradeableWeapon(database, settings, ownedWeapons, weaponLevels);
            var canOfferLogbook = database.logbooks != null && database.logbooks.Length > 0;

            var weaponWeight = settings != null ? settings.weaponOfferWeight : 0.55f;
            if (!canAddWeapon && !canUpgradeWeapon) weaponWeight = 0f;
            if (!canOfferLogbook) weaponWeight = 1f;

            var pickWeapon = rng.LootFloat() < weaponWeight;
            if (pickWeapon)
            {
                if (canAddWeapon && (!canUpgradeWeapon || rng.LootFloat() < 0.45f))
                {
                    var def = PickNewWeapon(database, settings, ownedWeapons, weaponLevels, rng);
                    if (def != null)
                    {
                        var offer = BuildNewWeaponOffer(def);
                        if (!usedKeys.Contains(GetOfferKey(offer))) return offer;
                    }
                }

                if (canUpgradeWeapon)
                {
                    var def = PickUpgradeWeapon(database, settings, ownedWeapons, weaponLevels, rng);
                    if (def != null)
                    {
                        weaponLevels.TryGetValue(def.id, out var level);
                        var offer = BuildWeaponUpgradeOffer(def, level, rng.RollRarity(luck), settings, pacing, rng);
                        if (!usedKeys.Contains(GetOfferKey(offer))) return offer;
                    }
                }
            }

            if (canOfferLogbook)
            {
                var lb = PickLogbook(database, ownedLogbooks, logbookLevels, logbookSlots, rng);
                if (lb != null)
                {
                    logbookLevels.TryGetValue(lb.id, out var lbLevel);
                    var offer = BuildLogbookOffer(lb, lbLevel, rng.RollRarity(luck));
                    if (!usedKeys.Contains(GetOfferKey(offer))) return offer;
                }
            }

            return default;
        }

        static string GetOfferKey(LevelOffer offer) => $"{offer.kind}:{offer.id}";

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
            RunRng rng)
        {
            var candidates = new List<WeaponDefinition>();
            var weights = new List<float>();
            for (var i = 0; i < database.weapons.Length; i++)
            {
                var w = database.weapons[i];
                if (w == null || ContainsId(ownedWeapons, w.id)) continue;
                candidates.Add(w);
                weights.Add(1f);
            }

            return WeightedPick(candidates, weights, rng);
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
            RunRng rng)
        {
            var candidates = new List<LogbookDefinition>();
            var weights = new List<float>();
            for (var i = 0; i < database.logbooks.Length; i++)
            {
                var lb = database.logbooks[i];
                if (lb == null) continue;
                var owned = ContainsId(ownedLogbooks, lb.id);
                logbookLevels.TryGetValue(lb.id, out var level);
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
            description = DescribeWeapon(def),
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
            return new LevelOffer
            {
                kind = LevelOfferKind.WeaponUpgrade,
                id = def.id,
                title = def.displayName,
                categoryLabel = "Weapon Upgrade",
                description = string.Join("\n", System.Array.ConvertAll(lines, l => l.displayText)),
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
                description = desc,
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

        static string DescribeWeapon(WeaponDefinition def) => def.pattern switch
        {
            WeaponPattern.AutoProjectile => "Reliable auto-target cannon fire.",
            WeaponPattern.FanVolley => "Spread volley into enemy clusters.",
            WeaponPattern.MineDrop => "Drop explosive kegs along your wake.",
            WeaponPattern.ChainLightning => "Chain lightning between nearby foes.",
            WeaponPattern.Orbit => "Orbiting blades strike nearby enemies.",
            WeaponPattern.TrailZone => "Burning oil trail damages and ignites.",
            WeaponPattern.AuraPulse => "Periodic curse aura around the ship.",
            _ => "Automatic weapon."
        };
    }
}
