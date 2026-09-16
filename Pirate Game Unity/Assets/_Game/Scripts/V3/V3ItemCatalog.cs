using System.Collections.Generic;
using HighSeas.Core;

namespace HighSeas.V3
{
    /// <summary>Doc §11.3 first interaction Items + evolution synergy modules.</summary>
    public static class V3ItemCatalog
    {
        public static readonly string[] InteractionItemIds =
        {
            "gunners_gloves",
            "cursed_compass",
            "ghost_lantern",
            "reinforced_keel",
            "captains_bell",
            "black_powder_keg",
            "stormglass",
            "golden_figurehead",
            "leviathan_tooth",
            "powder_fuse",
            "sea_witchs_eye",
            "admirals_ledger"
        };

        public static readonly string[] PlaytestItemOrder =
        {
            "gunners_gloves",
            "cursed_compass",
            "ghost_lantern",
            "reinforced_keel",
            "captains_bell",
            "black_powder_keg",
            "stormglass",
            "golden_figurehead",
            "leviathan_tooth",
            "powder_fuse",
            "sea_witchs_eye",
            "admirals_ledger",
            "lucky_doubloon",
            "grapeshot_pouch",
            "double_fuse_cord",
            "serrated_scabbard",
            "broadside_drum",
            "dragons_breath_nozzle",
            "copper_conductor"
        };

        public static IReadOnlyList<(string id, Rarity rarity)> ChestPool { get; } = new (string, Rarity)[]
        {
            ("lucky_doubloon", Rarity.Common),
            ("gunners_gloves", Rarity.Common),
            ("ghost_lantern", Rarity.Uncommon),
            ("reinforced_keel", Rarity.Uncommon),
            ("captains_bell", Rarity.Uncommon),
            ("grapeshot_pouch", Rarity.Uncommon),
            ("double_fuse_cord", Rarity.Uncommon),
            ("serrated_scabbard", Rarity.Uncommon),
            ("black_powder_keg", Rarity.Rare),
            ("stormglass", Rarity.Rare),
            ("golden_figurehead", Rarity.Rare),
            ("leviathan_tooth", Rarity.Rare),
            ("powder_fuse", Rarity.Rare),
            ("sea_witchs_eye", Rarity.Rare),
            ("admirals_ledger", Rarity.Rare),
            ("cursed_compass", Rarity.Epic),
            ("broadside_drum", Rarity.Epic),
            ("dragons_breath_nozzle", Rarity.Epic),
            ("copper_conductor", Rarity.Epic)
        };

        public static string GetDisplayName(string itemId) => itemId switch
        {
            "gunners_gloves" => "Gunner's Gloves",
            "cursed_compass" => "Cursed Compass",
            "ghost_lantern" => "Ghost Lantern",
            "reinforced_keel" => "Reinforced Keel",
            "captains_bell" => "Captain's Bell",
            "black_powder_keg" => "Black Powder Keg",
            "stormglass" => "Stormglass",
            "golden_figurehead" => "Golden Figurehead",
            "leviathan_tooth" => "Leviathan Tooth",
            "powder_fuse" => "Powder Fuse",
            "sea_witchs_eye" => "Sea Witch's Eye",
            "admirals_ledger" => "Admiral's Ledger",
            "lucky_doubloon" => "Lucky Doubloon",
            "grapeshot_pouch" => "Grapeshot Pouch",
            "double_fuse_cord" => "Double-Fuse Cord",
            "serrated_scabbard" => "Serrated Scabbard",
            "broadside_drum" => "Broadside Drum",
            "dragons_breath_nozzle" => "Dragon's Breath Nozzle",
            "copper_conductor" => "Copper Conductor",
            _ => itemId?.Replace('_', ' ') ?? "Item"
        };

        public static string GetDescription(string itemId) => itemId switch
        {
            "gunners_gloves" => "Meaningful heading changes grant a brief attack-speed surge.",
            "cursed_compass" => "More Luck and denser fights — higher reward under pressure.",
            "ghost_lantern" => "Dodging telegraphed threats raises crit chance briefly.",
            "reinforced_keel" => "Damage reduction while moving above cruise speed.",
            "captains_bell" => "Every 12 kills triggers a free weapon emission burst.",
            "black_powder_keg" => "Critical hits amplify explosive weapon damage.",
            "stormglass" => "Chain lightning effects gain +1 jump target.",
            "golden_figurehead" => "Unspent Gold raises Luck during the stage.",
            "leviathan_tooth" => "Crowd-controlled enemies take increased damage.",
            "powder_fuse" => "Distance sailed without hits charges the next explosion.",
            "sea_witchs_eye" => "Status effects apply with higher proc pressure.",
            "admirals_ledger" => "More distinct weapon families — modest global damage.",
            "lucky_doubloon" => "Increases Luck for chests and offers.",
            "grapeshot_pouch" => "Evolution synergy — Stormshot Cannon.",
            "double_fuse_cord" => "Evolution synergy — Devil's Wake.",
            "serrated_scabbard" => "Evolution synergy — Bladed Tide.",
            "broadside_drum" => "Evolution synergy — King's Broadside.",
            "dragons_breath_nozzle" => "Evolution synergy — Hellfire Nozzle.",
            "copper_conductor" => "Evolution synergy — Tempest Mast.",
            _ => "Run item with a conditional combat interaction."
        };
    }
}
