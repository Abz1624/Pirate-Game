using System.Collections.Generic;
using System.Reflection;
using HighSeas.Core;
using HighSeas.V3;
using RoguePirates.V4.Config;

namespace RoguePirates.V4.Legacy
{
    /// <summary>
    /// M9: V3's build rules that were keyed by content id (item switch cases, Captain switch cases, the adopted
    /// Captain decisions), as data for the config generator. Name-keyed like the rest of the legacy bridge (plan D3);
    /// the generated configs carry the values and V4 code never compares these ids.
    /// </summary>
    [DeletedInPhase(15, "The build configs are the source of truth once V3 is retired")]
    public static class LegacyBuildMapping
    {
        static T Const<T>(System.Type type, string name) =>
            (T)type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetRawConstantValue();

        /// <summary>V3RunItemInventory.ApplyItemEffect: the stat change each stack adds.</summary>
        public static ItemStatModifier[] ItemStatModifiers(string itemId)
        {
            ItemStatModifier M(StatId stat, float add, float mul = 1f) => new ItemStatModifier { stat = (int)stat, additive = add, multiplicative = mul };
            switch (itemId)
            {
                case "grapeshot_pouch": return new[] { M(StatId.Damage, 0f, 1.1f) };
                case "double_fuse_cord": return new[] { M(StatId.AttackSpeed, 0f, 1.08f) };
                case "serrated_scabbard": return new[] { M(StatId.CritChance, 0.04f) };
                case "broadside_drum": return new[] { M(StatId.Quantity, 1f) };
                case "dragons_breath_nozzle": return new[] { M(StatId.Size, 0f, 1.08f) };
                case "copper_conductor": return new[] { M(StatId.ProjectileSpeed, 0f, 1.1f) };
                case "cursed_compass": return new[] { M(StatId.Luck, 0.08f) };
                case "golden_figurehead": return new[] { M(StatId.GoldGain, 0f, 1.05f) };
                case "stormglass": return new[] { M(StatId.Quantity, 0.5f) };
                case "black_powder_keg": return new[] { M(StatId.CritDamage, 0f, 1.1f) };
                case "sea_witchs_eye": return new[] { M(StatId.Luck, 0.04f) };
                case "admirals_ledger": return new[] { M(StatId.Damage, 0f, 1.04f) };
                default: return new[] { M(StatId.Luck, 0.03f) }; // lucky_doubloon and every item without a case
            }
        }

        /// <summary>V3RunItemEffects: the triggered effects (per item). Effects V3 never reads are inactive (R-40).</summary>
        public static ItemEffect[] ItemEffects(string itemId)
        {
            var t = typeof(V3RunItemEffects);
            ItemEffect E(ItemEffectKind kind, float value, float threshold = 0f, float seconds = 0f, float cap = 0f, bool active = true) =>
                new ItemEffect { kind = kind, value = value, threshold = threshold, seconds = seconds, cap = cap, active = active };
            switch (itemId)
            {
                case "gunners_gloves": return new[] { E(ItemEffectKind.AttackSpeedAfterTurn, 1.12f, Const<float>(t, "HeadingChangeDegrees"), Const<float>(t, "GlovesBuffSeconds")) };
                case "ghost_lantern": return new[] { E(ItemEffectKind.CritAfterNearMiss, 0.08f, 0f, Const<float>(t, "LanternBuffSeconds")) };
                case "reinforced_keel": return new[] { E(ItemEffectKind.IncomingDamageAtSpeed, 0.88f, Const<float>(t, "KeelSpeedThreshold")) };
                case "captains_bell": return new[] { E(ItemEffectKind.FireAllEveryKills, 1f, Const<int>(t, "BellKillThreshold")) };
                case "admirals_ledger": return new[] { E(ItemEffectKind.DamagePerWeaponFamily, 0.03f, 0f, 0f, 0.18f) };
                case "golden_figurehead": return new[] { E(ItemEffectKind.DamagePerGold, 1f, 2000f, 0f, 0.15f) };
                case "black_powder_keg": return new[] { E(ItemEffectKind.ExplosionDamage, 1.12f, active: false) };
                case "powder_fuse": return new[] { E(ItemEffectKind.ExplosionDamageAfterDistance, 1.35f, 18f, active: false) };
                case "sea_witchs_eye": return new[] { E(ItemEffectKind.StatusDamage, 1.15f, active: false) };
                case "leviathan_tooth": return new[] { E(ItemEffectKind.ControlledTargetDamage, 1.2f, active: false) };
                case "stormglass": return new[] { E(ItemEffectKind.ExtraLightningJumps, 1f, active: false) };
                case "cursed_compass": return new[] { E(ItemEffectKind.SpawnDensity, 1.18f, active: false) };
                default: return new ItemEffect[0];
            }
        }

        /// <summary>M9 gate (R-40): the items whose effects V3 computed but never read, switched on by the approved fix.</summary>
        public static readonly string[] R40Items = { "black_powder_keg", "powder_fuse", "sea_witchs_eye", "leviathan_tooth", "stormglass", "cursed_compass" };

        /// <summary>M9 gate (R-34): the Logbook giving +2.0 crit chance per rank.</summary>
        public const string LookoutsGlass = "lookouts_glass";

        /// <summary>The save key of a V3 Captain (V3CaptainId name, lower case).</summary>
        public static string CaptainKey(V3CaptainId id) => id.ToString().ToLowerInvariant();

        /// <summary>Adopted decision (gameplay doc §21 row 5): each Captain's starting weapon.</summary>
        public static string AdoptedStartingWeapon(V3CaptainId id) => id switch
        {
            V3CaptainId.Brine => "deck_cannons",
            V3CaptainId.Richie => "broadside_battery",
            V3CaptainId.Flint => "powder_kegs",
            V3CaptainId.Ember => "flamethrower",
            V3CaptainId.Roxie => "lightning_mast",
            _ => "swivel_guns",
        };

        /// <summary>Adopted decision (gameplay doc §8): a Captain whose starting weapon is a Rum unlock needs it owned.</summary>
        public static string PrerequisiteWeapon(V3CaptainId id) => id switch
        {
            V3CaptainId.Ember => "flamethrower",
            V3CaptainId.Roxie => "lightning_mast",
            _ => string.Empty,
        };

        /// <summary>V3CaptainCatalog.IsLocked: the content a V3 save must own (Hank "brine", Sparky "flint").</summary>
        public static string V3OwnershipKey(V3CaptainId id) => id switch
        {
            V3CaptainId.Brine => "brine",
            V3CaptainId.Flint => "flint",
            _ => string.Empty,
        };

        /// <summary>V3CaptainController: passives.</summary>
        public static CaptainPassive Passive(V3CaptainId id)
        {
            var p = new CaptainPassive();
            switch (id)
            {
                case V3CaptainId.Rook: p.additiveDamagePerLevel = 0.005f; p.additiveDamageCap = 0.25f; break;
                case V3CaptainId.Flint:
                    p.additiveDamagePerLevel = 0.005f; p.additiveDamageCap = 0.25f;
                    p.runningFuse = true; p.runningFuseSpeed = 4f; p.runningFuseSeconds = 2f; p.runningFuseWeaponId = "powder_kegs"; p.runningFuseMultiplier = 1.15f;
                    break;
                case V3CaptainId.Richie: p.additiveDamageGoldDivisor = 4500f; p.goldDamageCap = 0.2f; break;
                case V3CaptainId.Ember: p.additiveDamagePerLevel = 0.004f; p.additiveDamageCap = 0.18f; p.burnDamageMultiplier = 1.12f; break;
                case V3CaptainId.Roxie: p.additiveDamagePerLevel = 0.003f; p.additiveDamageCap = 0.15f; p.lightningDamageMultiplier = 1.1f; break;
                case V3CaptainId.Brine: p.armorPerLevel = 0.5f; p.armorCap = 25f; break;
            }
            return p;
        }

        /// <summary>V3CaptainActiveController.TryActivate and GetDefinition: actives.</summary>
        public static CaptainActive Active(V3CaptainId id)
        {
            var a = new CaptainActive();
            switch (id)
            {
                case V3CaptainId.Brine:
                    a.displayName = "Brace!"; a.cooldownSeconds = 20f;
                    a.braceSeconds = 2f; a.braceIncomingDamageMultiplier = 0.4f; a.slowSeconds = 2f; a.slowMultiplier = 0.75f;
                    break;
                case V3CaptainId.Flint:
                    a.displayName = "Light the Fuse"; a.cooldownSeconds = 18f;
                    a.armPowderKegs = true; a.kegRadiusSeconds = 4f; a.kegRadiusMultiplier = 1.2f;
                    break;
                case V3CaptainId.Richie:
                    a.displayName = "Cut and Run"; a.cooldownSeconds = 16f;
                    a.moveSeconds = 3f; a.moveMultiplier = 1.35f; a.damageBuffSeconds = 5f; a.damageBuffAdditive = 0.1f;
                    a.pickupRadiusSeconds = 3f; a.pickupRadiusMultiplier = 1.5f; // in the summary; V3 never applies it (R-49)
                    break;
                case V3CaptainId.Ember:
                    a.displayName = "Stoke the Flames"; a.cooldownSeconds = 18f;
                    a.flameSeconds = 5f; a.flameRangeMultiplier = 1.2f; a.burnDamageMultiplier = 1.3f;
                    break;
                case V3CaptainId.Roxie:
                    a.displayName = "Ride the Gale"; a.cooldownSeconds = 15f;
                    a.moveSeconds = 2.5f; a.moveMultiplier = 1.4f; a.fireLightningSalvo = true;
                    a.nearMissWindowSeconds = 2.5f; a.nearMissCooldownRefund = 0.35f;
                    break;
                default:
                    a.displayName = "All Guns!"; a.cooldownSeconds = 18f; a.fireAllWeapons = true;
                    break;
            }
            return a;
        }

        /// <summary>Adopted decision D6 (R-35 to R-38): the stat each of the four misassigned Logbooks should raise; -2 = unchanged.</summary>
        public static int IntendedLogbookStat(string logbookId) => logbookId switch
        {
            "scavengers_net" => (int)StatId.PickupRadius,
            "cartographers_folio" => (int)StatId.XPGain,
            "quartermasters_ledger" => (int)StatId.GoldGain,
            "navigators_charts" => (int)StatId.MoveSpeed,
            _ => -2,
        };

        /// <summary>V3's powerup kinds in its enum order (the V4 enum mirrors it).</summary>
        public static readonly IReadOnlyList<V3FloorPowerupKind> PowerupOrder = (V3FloorPowerupKind[])System.Enum.GetValues(typeof(V3FloorPowerupKind));
    }
}
