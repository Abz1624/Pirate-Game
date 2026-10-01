using UnityEngine;

namespace RoguePirates.V4.Config
{
    /// <summary>
    /// Level-ups (M9): XP curve, slots, cards, refreshes, rarity rolls, upgrade packages, the new-weapon guarantee and
    /// the fallback cards. Values from V3's stage profile (progression block), LevelUpSettings, DemoPacingSettings and
    /// GameConstants; the offer category weights are in <see cref="OfferWeightsConfig"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Progression", fileName = "Progression")]
    public sealed class ProgressionConfig : V4ConfigAsset
    {
        [Header("Slots")]
        [Tooltip("Weapons the ship can carry.")] public int weaponSlots = 6;
        [Tooltip("Logbooks the ship can carry.")] public int logbookSlots = 4;

        [Header("XP curve (V3 stage profile)")]
        [Tooltip("The level a run starts at (V3 rules: 0).")] public int startLevel;
        [Tooltip("XP from level 0 to 1.")] public int levelZeroToOneXp = 5;
        [Tooltip("XP to the next level from level L ≥ 1 = round(offset + multiplier × (L + 1)^exponent), halves away from zero.")]
        public float xpCurveOffset = 5f;
        public double xpCurveMultiplier = 5.325000286102295;
        public double xpCurveExponent = 1.065000057220459;
        [Tooltip("V5 M3c (owner, 1 Oct 2026): XP from level L to L + 1 for L = 0, 1, 2... While it has entries it replaces the curve above (and levelZeroToOneXp); past its end each level adds the average step of its last four levels. Empty = the curve (V3, V5 M2). Fitted by DevTools XpTableTuner to growing level-up gaps.")]
        public int[] xpToNextTable = System.Array.Empty<int>();

        [Header("Cards")]
        [Tooltip("Cards per level-up.")] public int cardsPerLevel = 3;
        [Tooltip("Refreshes per run (new cards for the same level-up).")] public int refreshesPerRun = 2;
        [Tooltip("Attempts per card before it falls back.")] public int attemptsPerCard = 24;
        [Tooltip("No two cards with the same offer.")] public bool preventDuplicateCards = true;
        [Tooltip("Weapons at max level are not offered upgrades.")] public bool excludeMaxedWeapons = true;
        [Tooltip("The first level-up offers this weapon's upgrade, a new weapon and a Logbook (V3: swivel_guns); it also weighs 1.2 among upgrades.")]
        public string firstLevelUpWeaponId;
        public float firstLevelUpWeaponUpgradeWeight = 1.2f;
        [Tooltip("An owned Logbook weighs this much among Logbook picks (new ones weigh 1).")] public float ownedLogbookWeight = 1.5f;
        [Tooltip("Weapon level needed for an evolution offer.")] public int maxWeaponLevel = 12;
        [Tooltip("R-31 fix (off = V3): the weapon-upgrade card category stays open while a maxed weapon has an evolution it can take. V3 offered evolutions only through that category, which closes once every weapon is maxed, so evolutions became unreachable.")]
        public bool evolutionOffersWhenMaxed = true;
        [Tooltip("V5 M3 (Q18b): weapon level from which the evolution card can appear (with its item); the evolved weapon keeps levelling to its max. 0 = maxWeaponLevel (V3).")]
        public int evolutionUnlockLevel;
        [Tooltip("V5 M3 (Q18b): weapon levels a weapon-upgrade card grants per rarity (Common … Legendary), capped at the weapon's max level. V3: 1 each.")]
        public int[] levelsPerRarity = { 1, 1, 1, 1, 1 };
        [Tooltip("V5 M3 pity rule: after this many card deals (level-ups and refreshes) without a card of pityMinRarity or better, the next weapon upgrade dealt is at least that rarity (one card per deal; if the new-weapon guarantee replaces that card, the pity carries to the next deal). 0 = off (V3).")]
        public int rarePityLevelUps;
        public int pityMinRarity = 2;

        [Header("Rarity")]
        [Tooltip("Common, Uncommon, Rare, Epic, Legendary base weights, each × (1 + Luck)^(luckExponent × tier).")]
        public float[] rarityBaseWeights = { 60f, 26f, 10f, 3.2f, 0.8f };
        public float luckExponent = 0.55f;
        [Tooltip("Legendary never exceeds this share of the total.")] public float legendaryMaxShare = 0.06f;
        [Tooltip("Power per rarity (upgrade lines and Logbook ranks).")] public float[] rarityMultipliers = { 1f, 1.25f, 1.55f, 1.9f, 2.5f };
        [Tooltip("Upgrade lines per rarity.")] public int[] upgradeLinesPerRarity = { 1, 1, 2, 2, 3 };
        [Tooltip("Upgrade line value = axis increment × rarity power × this (Quantity lines ignore it). V3: LevelUpSettings 1.2 × pacing 1.2.")]
        public float upgradePowerMultiplier = 1.44f;
        [Tooltip("A newly equipped weapon's damage bonus (V3 pacing startingDamageMultiplier).")] public float newWeaponDamageBonus = 1.1f;

        [Header("New-weapon guarantee")]
        [Tooltip("From each of these player levels the loadout should reach 3, 4, 5, … weapons; a level-up without a new-weapon card gets one.")]
        public int[] newWeaponProtectionLevels = { 2, 3, 5, 8, 12 };
        [Tooltip("No guarantee while the player level is at most this and at most guaranteeSkipMaxWeapons are owned.")]
        public int guaranteeSkipMaxLevel = 5;
        public int guaranteeSkipMaxWeapons = 3;

        [Header("Fallback cards (when nothing else can be offered)")]
        [Tooltip("Heal this fraction of max hull.")] public float fallbackHealFraction = 0.10f;
        [Tooltip("+Damage, offered while global damage stays at or below the cap.")] public float fallbackDamageBonus = 0.02f;
        public float fallbackDamageCap = 11f;
        [Tooltip("× move speed, offered while move speed stays at or below the cap.")] public float fallbackMoveSpeedMultiplier = 1.01f;
        public float fallbackMoveSpeedCap = 12f;

        public override void Validate(ConfigValidationReport report)
        {
            if (weaponSlots < 1) report.Error(this, nameof(weaponSlots), "must be at least 1");
            if (logbookSlots < 0) report.Error(this, nameof(logbookSlots), "must be 0 or more");
            if (cardsPerLevel < 1) report.Error(this, nameof(cardsPerLevel), "must be at least 1");
            if (attemptsPerCard < 1) report.Error(this, nameof(attemptsPerCard), "must be at least 1");
            if (refreshesPerRun < 0) report.Error(this, nameof(refreshesPerRun), "must be 0 or more");
            if (levelZeroToOneXp < 1) report.Error(this, nameof(levelZeroToOneXp), "must be at least 1");
            if (xpToNextTable != null)
                for (var i = 0; i < xpToNextTable.Length; i++)
                    if (xpToNextTable[i] < 1) report.Error(this, $"xpToNextTable[{i}]", "must be at least 1");
            if (rarityBaseWeights == null || rarityBaseWeights.Length != 5) report.Error(this, nameof(rarityBaseWeights), "needs 5 values (Common to Legendary)");
            else report.WeightsNotZero(this, nameof(rarityBaseWeights), rarityBaseWeights);
            if (rarityMultipliers == null || rarityMultipliers.Length != 5) report.Error(this, nameof(rarityMultipliers), "needs 5 values");
            if (upgradeLinesPerRarity == null || upgradeLinesPerRarity.Length != 5) report.Error(this, nameof(upgradeLinesPerRarity), "needs 5 values");
            else foreach (var n in upgradeLinesPerRarity) if (n < 1) report.Error(this, nameof(upgradeLinesPerRarity), "every rarity needs at least 1 line");
            if (newWeaponProtectionLevels != null)
                for (var i = 1; i < newWeaponProtectionLevels.Length; i++)
                    if (newWeaponProtectionLevels[i] < newWeaponProtectionLevels[i - 1]) report.Error(this, nameof(newWeaponProtectionLevels), "levels must not decrease");
            report.Positive(this, nameof(maxWeaponLevel), maxWeaponLevel);
            if (evolutionUnlockLevel < 0 || evolutionUnlockLevel > maxWeaponLevel) report.Error(this, nameof(evolutionUnlockLevel), "must be 0 (= max level) or at most maxWeaponLevel");
            if (levelsPerRarity == null || levelsPerRarity.Length != 5) report.Error(this, nameof(levelsPerRarity), "needs 5 values");
            else foreach (var n in levelsPerRarity) if (n < 1) report.Error(this, nameof(levelsPerRarity), "every rarity grants at least 1 level");
            if (rarePityLevelUps < 0) report.Error(this, nameof(rarePityLevelUps), "must be 0 (off) or more");
            if (pityMinRarity < 0 || pityMinRarity > 4) report.Error(this, nameof(pityMinRarity), "must be 0-4");
        }
    }
}
