using System;
using System.Linq;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.UI;
using RoguePirates.V4.Config;
using RoguePirates.V4.Progression;

namespace RoguePirates.V4.Presentation.Hud
{
    /// <summary>
    /// The level-up card texts (M14.11; was LegacyLevelUpPresenter.ToCard, which read V3's weapon definitions and text
    /// tables): the same titles, category labels and lines V3's LevelOfferGenerator wrote, from the V4 configs — weapon
    /// name, description, firing line and base stats from <see cref="WeaponConfig"/>, evolutions and Logbooks from their
    /// configs, the fallback amounts from <see cref="ProgressionConfig"/>. The card model is the kept card view's
    /// (<see cref="LevelOffer"/>, a shared UI type until M15).
    /// </summary>
    public static class OfferCards
    {
        /// <param name="views">V5 M8z (7 Oct): the run views the "current → next" numbers read (null: no numbers).</param>
        public static LevelOffer ToCard(Offer o, ConfigSetRuntime config, RoguePirates.V4.Views.IRunViews views = null)
        {
            var progression = config?.Single<ProgressionConfig>();
            switch (o.Kind)
            {
                case OfferKind.Continue:
                    return Fixed(LevelOfferKind.Continue, "continue", "Continue", "No Upgrade", "Every upgrade is capped. Sail on without a level reward.");
                case OfferKind.FallbackHeal:
                    return Fixed(LevelOfferKind.FallbackHeal, "fallback_heal", "Patch the Hull", "Fallback",
                        $"Restore {(progression != null ? progression.fallbackHealFraction : 0.1f) * 100f:0}% maximum Hull.");
                case OfferKind.FallbackDamage:
                    return Fixed(LevelOfferKind.FallbackDamage, "fallback_damage", "Powder Reserve", "Fallback",
                        $"+{(progression != null ? progression.fallbackDamageBonus : 0.02f) * 100f:0}% damage for this run.");
                case OfferKind.FallbackMoveSpeed:
                    return Fixed(LevelOfferKind.FallbackMoveSpeed, "fallback_move_speed", "Fair Wind", "Fallback",
                        $"+{((progression != null ? progression.fallbackMoveSpeedMultiplier : 1.01f) - 1f) * 100f:0}% movement speed for this run.");
                case OfferKind.WeaponEvolution:
                {
                    var evo = config?.All<WeaponEvolutionConfig>().FirstOrDefault(e => e.evolutionId == o.EvolutionId);
                    return new LevelOffer
                    {
                        kind = LevelOfferKind.WeaponEvolution, id = o.Id, evolutionId = o.EvolutionId,
                        title = evo != null ? evo.displayName : o.EvolutionId, categoryLabel = "WEAPON EVOLUTION",
                        description = evo != null ? evo.description : string.Empty, rarity = Rarity.Legendary,
                        currentLevel = o.CurrentLevel, nextLevel = o.NextLevel, isWeapon = true, isNew = false,
                    };
                }
                case OfferKind.NewWeapon:
                {
                    var weapon = Weapon(config, o.Id);
                    if (weapon == null) return Plain(o, "New Weapon", config);
                    // V3 LevelOfferGenerator.BuildNewWeaponOffer (stat summary from V3ContentDescriptions.GetWeaponStatSummary).
                    var behavior = string.IsNullOrEmpty(weapon.behaviorLine) ? "Auto-attacks nearby enemies" : weapon.behaviorLine;
                    // Playtest (3 Oct): the card shows numbers only (CardStatLines), no description.
                    var statLines = CardStatLines.NewWeapon(weapon);
                    return new LevelOffer
                    {
                        kind = LevelOfferKind.NewWeapon, id = weapon.weaponId, title = weapon.displayName, categoryLabel = "New Weapon",
                        description = string.Join("\n", statLines), behaviorLine = behavior, effectLines = statLines,
                        rarity = Rarity.Common, hideRarity = true, currentLevel = 0, nextLevel = 1, isWeapon = true, isNew = true,
                        upgradeLines = Array.Empty<WeaponUpgradeLine>(),
                    };
                }
                case OfferKind.WeaponUpgrade:
                {
                    var weapon = Weapon(config, o.Id);
                    var lines = (o.Lines ?? Array.Empty<UpgradeLine>()).Select(l => new WeaponUpgradeLine
                    {
                        axis = (UpgradeAxis)(int)l.Axis,
                        value = l.Value,
                        displayText = new WeaponUpgradeAxisConfig
                        {
                            axis = (UpgradeAxis)(int)l.Axis,
                            displayFormat = weapon?.upgradeAxes?.FirstOrDefault(a => a.axis == l.Axis)?.displayFormat,
                        }.FormatValue(l.Value),
                    }).ToArray();
                    var card = Plain(o, "Weapon Upgrade", config);
                    // Playtest (3 Oct): current to next numbers only (no weapon description).
                    card.effectLines = CardStatLines.WeaponUpgrade(weapon, o.Lines, o.NextLevel, views) ?? lines.Select(l => l.displayText).ToArray();
                    card.description = string.Join("\n", card.effectLines);
                    card.upgradeLines = lines;
                    return card;
                }
                case OfferKind.Logbook:
                {
                    var logbook = config?.All<LogbookConfig>().FirstOrDefault(l => l.logbookId == o.Id);
                    var mult = progression != null && o.Rarity >= 0 && o.Rarity < progression.rarityMultipliers.Length ? progression.rarityMultipliers[o.Rarity] : 1f;
                    var line = logbook != null && StatResolver.IsStat(logbook.stat) ? StatDisplay.FormatLogbookBonus((StatId)logbook.stat, logbook.perLevelAdditive * mult) : string.Empty;
                    if (logbook != null && logbook.stat == (int)StatId.Hull) line += " (also heals)";
                    var card = Plain(o, o.CurrentLevel > 0 ? "Logbook Upgrade" : "New Logbook", config);
                    card.title = logbook != null ? logbook.displayName : o.Id;
                    // Playtest (3 Oct): the stat from its current value to the next.
                    card.effectLines = CardStatLines.Logbook(logbook, logbook != null ? logbook.perLevelAdditive * mult : 0f, views) ?? new[] { line };
                    card.description = string.Join("\n", card.effectLines);
                    card.upgradeLines = Array.Empty<WeaponUpgradeLine>();
                    return card;
                }
                default: return Plain(o, string.Empty, config);
            }
        }

        static WeaponConfig Weapon(ConfigSetRuntime config, string id) => config?.All<WeaponConfig>().FirstOrDefault(w => w.weaponId == id);

        static LevelOffer Fixed(LevelOfferKind kind, string id, string title, string category, string description) => new LevelOffer
        {
            kind = kind, id = id, title = title, categoryLabel = category, description = description, rarity = Rarity.Common,
            currentLevel = 0, nextLevel = 0, isWeapon = false, isNew = false, upgradeLines = Array.Empty<WeaponUpgradeLine>(),
        };

        static LevelOffer Plain(Offer o, string category, ConfigSetRuntime config)
        {
            var weapon = o.IsWeapon ? Weapon(config, o.Id) : null;
            return new LevelOffer
            {
                kind = (LevelOfferKind)(int)o.Kind, id = o.Id, evolutionId = o.EvolutionId,
                title = weapon != null ? weapon.displayName : o.Id, categoryLabel = category, rarity = (Rarity)o.Rarity,
                currentLevel = o.CurrentLevel, nextLevel = o.NextLevel, isWeapon = o.IsWeapon, isNew = o.IsNew,
                upgradeLines = Array.Empty<WeaponUpgradeLine>(),
            };
        }
    }
}
