using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HighSeas.Balance;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.V3;
using RoguePirates.V4.Config;
using RoguePirates.V4.Legacy;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// M9: creates or updates the build configs from V3 — progression (stage profile progression block, LevelUpSettings,
    /// DemoPacingSettings, GameConstants), offer weights, the 10 Logbooks (the V3 database), the 19 items (V3ItemCatalog,
    /// V3RunItemInventory, V3RunItemEffects), floor powerups (V3FloorPowerupWorld, the profile's rewards), the 6 Captains
    /// (V3CaptainProfile, V3CaptainCatalog, V3CaptainController, V3CaptainActiveController, the profile's unlock costs),
    /// unlocks and pickups — then applies the adopted decisions (D6) on top and adds everything to the config set.
    /// Idempotent. The v3-parity preset restores V3's values where the decisions differ.
    /// </summary>
    public static class V4BuildConfigGenerator
    {
        const string BuildRoot = V3ConfigMigrator.DataRoot + "/Build";

        [MenuItem("RoguePirates/V4/Generate Build Configs (M9)")]
        static void GenerateFromMenu() => Debug.Log(Generate());

        public static string Generate()
        {
            var log = new StringBuilder();
            foreach (var folder in new[] { BuildRoot, BuildRoot + "/Items", BuildRoot + "/Logbooks", BuildRoot + "/Captains" }) EnsureFolder(folder);
            var profile = AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(V3ConfigMigrator.ProfilePath);
            var pacing = AssetDatabase.LoadAssetAtPath<DemoPacingSettings>(V3ConfigMigrator.PacingPath);
            var levelUp = AssetDatabase.LoadAssetAtPath<LevelUpSettings>("Assets/_Game/Data/LevelUpSettings.asset");
            if (profile == null || pacing == null || levelUp == null) return "missing V3 source assets (profile, pacing or level-up settings)\n";
            var created = new List<V4ConfigAsset>();

            var progression = Asset<ProgressionConfig>(BuildRoot + "/Progression_Saltwind.asset", "progression.saltwind", log);
            FillProgression(progression, profile, pacing, levelUp);
            created.Add(progression);
            created.Add(Asset<OfferWeightsConfig>(BuildRoot + "/OfferWeights_Saltwind.asset", "offer_weights.saltwind", log)); // defaults = V3's Saltwind table

            var db = V3ConfigMigrator.LoadCleanDatabase();
            for (var i = 0; i < db.logbooks.Length; i++)
            {
                var def = db.logbooks[i];
                if (def == null) continue;
                var l = Asset<LogbookConfig>($"{BuildRoot}/Logbooks/{def.id}.asset", "logbook." + def.id, log);
                l.logbookId = def.id;
                l.displayName = V3LogbookCatalog.GetDisplayName(def.id);
                l.stat = (int)def.primaryStat >= 0 && (int)def.primaryStat <= 20 ? (int)def.primaryStat : -1; // V3's stat 22 did nothing
                l.perLevelAdditive = def.perLevelAdditive;
                l.perLevelMultiplicative = def.perLevelMultiplicative;
                l.maxLevel = def.maxLevel;
                l.inSaltwindPool = V3LogbookCatalog.IsSaltwindPoolId(def.id);
                l.databaseOrder = i;
                created.Add(l);
            }

            var chestPool = V3ItemCatalog.ChestPool;
            var collection = V3ItemCatalog.PlaytestItemOrder;
            foreach (var itemId in collection)
            {
                var item = Asset<ItemConfig>($"{BuildRoot}/Items/{itemId}.asset", "item." + itemId, log);
                item.itemId = itemId;
                item.displayName = V3ItemCatalog.GetDisplayName(itemId);
                item.description = V3ItemCatalog.GetDescription(itemId);
                var chestIndex = -1;
                for (var c = 0; c < chestPool.Count; c++) if (chestPool[c].id == itemId) chestIndex = c;
                item.inChestPool = chestIndex >= 0;
                item.chestPoolOrder = chestIndex;
                item.rarity = chestIndex >= 0 ? (int)chestPool[chestIndex].rarity : 0;
                item.inLevelUpPool = Array.IndexOf(V3ItemCatalog.InteractionItemIds, itemId) >= 0;
                item.collectionOrder = Array.IndexOf(collection, itemId);
                item.statModifiers = LegacyBuildMapping.ItemStatModifiers(itemId);
                item.effects = LegacyBuildMapping.ItemEffects(itemId);
                created.Add(item);
            }

            var powerups = Asset<PowerupConfig>(BuildRoot + "/Powerups_Saltwind.asset", "powerups.saltwind", log);
            FillPowerups(powerups, profile);
            created.Add(powerups);

            foreach (V3CaptainId id in Enum.GetValues(typeof(V3CaptainId)))
            {
                var key = LegacyBuildMapping.CaptainKey(id);
                var c = Asset<CaptainConfig>($"{BuildRoot}/Captains/{key}.asset", "captain." + key, log);
                FillCaptain(c, id, profile);
                created.Add(c);
            }

            var unlocks = Asset<UnlockConfig>(BuildRoot + "/Unlocks_Saltwind.asset", "unlocks.saltwind", log);
            unlocks.starterWeaponIds = (string[])profile.starterWeaponIds.Clone();
            unlocks.weaponUnlocks = profile.weaponUnlocks.Select(u => new WeaponUnlock { weaponId = u.contentId, rumCost = u.marksCost }).ToArray();
            unlocks.firstConquestWeaponId = levelUp.startingWeaponId; // V3RunBootstrap.ResolveStartingWeapon's forced weapon (LevelUpSettings: swivel_guns)
            unlocks.startingWeaponRule = StartingWeaponRule.V3AlwaysFirstConquestWeapon;
            created.Add(unlocks);

            var pickups = Asset<PickupConfig>(BuildRoot + "/Pickups_Saltwind.asset", "pickups.saltwind", log);
            FillPickups(pickups, pacing);
            created.Add(pickups);

            log.Append(FillWeaponOffers(db));

            foreach (var c in created) EditorUtility.SetDirty(c);
            AssetDatabase.SaveAssets();
            log.Append(ApplyAdoptedDecisions());

            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(V3ConfigMigrator.ConfigSetPath);
            if (set != null)
            {
                foreach (var c in created) if (!set.configs.Contains(c)) set.configs.Add(c);
                set.configs = set.configs.Where(c => c != null).OrderBy(c => c.configId, StringComparer.Ordinal).ToList();
                EditorUtility.SetDirty(set);
                AssetDatabase.SaveAssets();
                log.AppendLine($"config set: {set.configs.Count} configs");
                log.Append(ConfigValidator.Validate(set));
            }
            return log.ToString();
        }

        [MenuItem("RoguePirates/V4/Apply M9 Adopted Decisions")]
        static void ApplyAdoptedDecisionsFromMenu() => Debug.Log(ApplyAdoptedDecisions());

        /// <summary>
        /// Adopted decisions (D6, gameplay doc §21): Captain starting weapons (First Conquest still Swivel Guns), actives
        /// on, Captain locks, the four Logbook stat fixes. The v3-parity preset turns them off again.
        /// </summary>
        public static string ApplyAdoptedDecisions()
        {
            var unlocks = AssetDatabase.LoadAssetAtPath<UnlockConfig>(BuildRoot + "/Unlocks_Saltwind.asset");
            if (unlocks != null) { unlocks.startingWeaponRule = StartingWeaponRule.CaptainWeapon; EditorUtility.SetDirty(unlocks); }
            foreach (var guid in AssetDatabase.FindAssets("t:LogbookConfig", new[] { BuildRoot + "/Logbooks" }))
            {
                var l = AssetDatabase.LoadAssetAtPath<LogbookConfig>(AssetDatabase.GUIDToAssetPath(guid));
                var intended = LegacyBuildMapping.IntendedLogbookStat(l.logbookId);
                if (intended == -2) continue;
                l.stat = intended;
                EditorUtility.SetDirty(l);
            }
            var experiment = AssetDatabase.FindAssets("t:ExperimentConfig", new[] { V3ConfigMigrator.DataRoot })
                .Select(g => AssetDatabase.LoadAssetAtPath<ExperimentConfig>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
            if (experiment != null) { experiment.captainActivesEnabled = true; EditorUtility.SetDirty(experiment); }
            AssetDatabase.SaveAssets();
            return "M9 adopted decisions applied (starting weapons, actives on, Captain locks, Logbook stats)\n";
        }

        /// <summary>
        /// The weapons' level-up data as V3's run database has it (the loaded database after CombatWeaponCatalog adds
        /// its four weapons): database order, max level and upgrade axes (V3's pattern defaults where a weapon has none).
        /// </summary>
        static string FillWeaponOffers(GameDatabase source)
        {
            var clone = Object.Instantiate(source);
            var created = new List<Object> { clone };
            try
            {
                clone.weapons = source.weapons.Select(w => { if (w == null) return null; var c = Object.Instantiate(w); c.name = w.name; created.Add(c); return c; }).ToArray();
                HighSeas.Combat.CombatWeaponCatalog.ApplyToDatabase(clone, HighSeas.Combat.CombatExperimentContext.Active);
                var n = 0;
                foreach (var guid in AssetDatabase.FindAssets("t:WeaponConfig", new[] { V3ConfigMigrator.DataRoot + "/Weapons" }))
                {
                    var w = AssetDatabase.LoadAssetAtPath<WeaponConfig>(AssetDatabase.GUIDToAssetPath(guid));
                    var index = Array.FindIndex(clone.weapons, d => d != null && d.id == w.weaponId);
                    if (index < 0) continue;
                    var def = clone.weapons[index];
                    w.databaseOrder = index;
                    w.maxLevel = def.maxLevel;
                    w.upgradeAxes = def.GetUpgradeConfigs().Select(x => new WeaponUpgradeAxis
                    {
                        axis = (ConfigEnums.UpgradeAxis)(int)x.axis, baseIncrement = x.baseIncrement, offerWeight = x.offerWeight,
                        maxAccumulated = x.maxAccumulated, displayFormat = x.displayFormat ?? string.Empty
                    }).ToArray();
                    EditorUtility.SetDirty(w);
                    n++;
                }
                return $"weapon offer data: {n} weapons (run database order, max level, upgrade axes)\n";
            }
            finally
            {
                foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            }
        }

        static void FillProgression(ProgressionConfig p, StageBalanceProfile profile, DemoPacingSettings pacing, LevelUpSettings levelUp)
        {
            p.weaponSlots = profile.weaponSlotCap;
            p.logbookSlots = profile.logbookSlotCap;
            p.startLevel = 0; // LevelSystem.Initialize with V3 rules
            p.levelZeroToOneXp = profile.levelZeroToOneXpCost;
            p.xpCurveOffset = 5f; // StageBalanceProfileExportCodec.XpToNextLevel
            p.xpCurveMultiplier = profile.xpCurveMultiplier;
            p.xpCurveExponent = profile.xpCurveExponent;
            p.cardsPerLevel = levelUp.cardsPerLevel;
            p.refreshesPerRun = levelUp.refreshesPerRun;
            p.attemptsPerCard = 24;
            p.preventDuplicateCards = levelUp.preventDuplicateCards;
            p.excludeMaxedWeapons = levelUp.excludeMaxedWeapons;
            p.firstLevelUpWeaponId = levelUp.startingWeaponId;
            p.firstLevelUpWeaponUpgradeWeight = 1.2f;
            p.ownedLogbookWeight = 1.5f;
            p.maxWeaponLevel = GameConstants.MaxWeaponLevel;
            p.rarityBaseWeights = new[] { 60f, 26f, 10f, 3.2f, 0.8f };
            p.luckExponent = 0.55f;
            p.legendaryMaxShare = 0.06f;
            p.rarityMultipliers = (float[])GameConstants.RarityMultipliers.Clone();
            p.upgradeLinesPerRarity = new[] { levelUp.commonStatLines, levelUp.uncommonStatLines, levelUp.rareStatLines, levelUp.epicStatLines, levelUp.legendaryStatLines };
            p.upgradePowerMultiplier = levelUp.upgradePowerMultiplier * pacing.upgradePowerMultiplier;
            p.newWeaponDamageBonus = pacing.startingDamageMultiplier;
            p.newWeaponProtectionLevels = (int[])profile.newWeaponOfferProtectionLevels.Clone();
            p.guaranteeSkipMaxLevel = 5;
            p.guaranteeSkipMaxWeapons = 3;
            p.fallbackHealFraction = GameConstants.LevelOfferFallbackHealFraction;
            p.fallbackDamageBonus = GameConstants.LevelOfferFallbackDamageBonus;
            p.fallbackDamageCap = GameConstants.MaxGlobalDamageMultiplier;
            p.fallbackMoveSpeedMultiplier = GameConstants.LevelOfferFallbackMoveSpeedMultiplier;
            p.fallbackMoveSpeedCap = GameConstants.MaxMoveSpeed;
        }

        static void FillPowerups(PowerupConfig c, StageBalanceProfile profile)
        {
            c.dropChancePerKill = profile.randomPowerupChancePerKill;
            c.minIntervalSeconds = profile.randomPowerupMinIntervalSeconds;
            c.elitesAlwaysDrop = true;
            c.maxPerStage = V3FloorPowerupWorld.MaxPerStage;
            c.lifetimeSeconds = V3FloorPowerupWorld.LifetimeSeconds;
            c.collectRadius = V3FloorPowerupWorld.CollectRadius;
            c.searchRings = 4; c.searchPointsPerRing = 8; c.searchRingSpacing = 1.5f; c.boundsMargin = 1f; c.islandClearance = 0.45f;
            c.pool = new[]
            {
                new PowerupPoolEntry { kind = PowerupKind.RepairSupplies, weight = 25f },
                new PowerupPoolEntry { kind = PowerupKind.PowderRush, weight = 25f },
                new PowerupPoolEntry { kind = PowerupKind.MagneticTide, weight = 25f },
                new PowerupPoolEntry { kind = PowerupKind.DeadCalm, weight = 25f },
            };
            c.repairMaxHullFraction = profile.repairSuppliesMaxHullFraction;
            c.battleSignalDamageMultiplier = V3FloorPowerupWorld.BattleSignalDamageMultiplier;
            c.battleSignalSeconds = V3FloorPowerupWorld.BattleSignalDuration;
            c.tailwindSpeedMultiplier = V3FloorPowerupWorld.TailwindSpeedMultiplier;
            c.tailwindSeconds = V3FloorPowerupWorld.TailwindDuration;
            c.fullSailSpeedMultiplier = 1.45f;
            c.fullSailSeconds = 8f;
            c.powderRushBonus = profile.powderRushAttackSpeedBonus;
            c.powderRushSeconds = profile.powderRushDurationSeconds;
            c.powderRushIsDamageInBattleSignalSlot = true;
            c.deadCalmSlowFraction = profile.deadCalmSlowFraction;
            c.deadCalmSeconds = profile.deadCalmDurationSeconds;
        }

        static void FillCaptain(CaptainConfig c, V3CaptainId id, StageBalanceProfile profile)
        {
            var v3 = V3CaptainProfile.Get(id);
            c.captainKey = LegacyBuildMapping.CaptainKey(id);
            c.legacyId = (int)id;
            c.displayName = V3CaptainCatalog.GetMenuLabel(id);
            c.title = V3CaptainCatalog.GetDisplayTitle(id);
            c.menuDescription = V3CaptainCatalog.GetMenuDescription(id);
            c.passiveSummary = V3CaptainCatalog.GetPassiveSummary(id);
            c.activeSummary = V3CaptainCatalog.GetActiveSummary(id);
            c.themeColor = V3CaptainCatalog.GetMenuThemeColor(id);
            c.menuOrder = Array.IndexOf(V3CaptainCatalog.MenuOrder, id);
            c.baseHull = v3.BaseHull;
            c.baseMoveSpeed = v3.BaseMoveSpeed;
            c.v3StartingWeaponId = v3.StartingWeaponId;
            c.startingWeaponId = LegacyBuildMapping.AdoptedStartingWeapon(id);
            c.passive = LegacyBuildMapping.Passive(id);
            c.active = LegacyBuildMapping.Active(id);
            var unlock = profile.captainUnlocks.FirstOrDefault(u => u.contentId == c.captainKey);
            c.rumCost = unlock != null ? unlock.marksCost : 0;
            c.prerequisiteWeaponId = LegacyBuildMapping.PrerequisiteWeapon(id);
            c.v3OwnershipKey = LegacyBuildMapping.V3OwnershipKey(id);
            c.v3UnlockedByFirstConquest = id == V3CaptainId.Flint;
        }

        static void FillPickups(PickupConfig c, DemoPacingSettings pacing)
        {
            c.healthDropChance = pacing.healthDropChance;
            c.healthDropValue = 25;
            c.dropSpreadRadius = 1.6f; c.dropAngleJitterDegrees = 15f; c.dropRadiusJitter = 0.15f;
            c.mergeRadius = 2f;
            c.xpAutoPickupRadius = pacing.xpAutoPickupRadius;
            c.goldAutoPickupRadius = pacing.goldAutoPickupRadius;
            c.barrelTouchRadius = pacing.barrelTouchRadius;
            c.flySpeedMin = 10f; c.flySpeedMax = 28f; c.collectDistance = 0.75f;
            c.bobAmplitude = 0.12f; c.bobSpeed = 3.5f;
            c.burstCountMin = pacing.barrelBurstVisualMin;
            c.burstCountMax = pacing.barrelBurstVisualMax;
            c.burstMinimum = 3;
            c.maxFlyingSpawnsPerFrame = pacing.maxFlyingPickupSpawnsPerFrame;
        }

        static T Asset<T>(string path, string configId, StringBuilder log) where T : V4ConfigAsset
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
                log.AppendLine("created " + path);
            }
            asset.configId = configId;
            asset.balanceVersion = "saltwind-v3-parity";
            return asset;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
