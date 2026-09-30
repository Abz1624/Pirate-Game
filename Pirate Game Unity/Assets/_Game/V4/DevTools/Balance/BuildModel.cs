using System;
using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Core;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Balance
{
    /// <summary>One Logbook rank or item stack in the build model.</summary>
    public struct BuildPiece
    {
        public string Id, Kind;
        public int Rarity;
        /// <summary>Build power gained (0.06 = +6%); NaN for utility-only pieces.</summary>
        public float Power;
        public string Utility, Assumption;
        public TargetBand Band;
        /// <summary>Its value depends on the weapons carried (explosions, burns, controlled targets, lightning) or on
        /// hoarding gold: scored for reference, not held to the band (review M3 #2: a generic build misprices it).</summary>
        public bool Synergy;
        public bool InBand => float.IsNaN(Power) || Synergy || Band.Contains(Power);
    }

    /// <summary>
    /// V5 M3 (G3.3): what one Logbook rank (at Common, rarity power 1) or one item stack (at its rarity) adds to a
    /// reference build: offense (every weapon at level 6 on the <see cref="WeaponModel"/> curves, each on its role's
    /// power) × effective hull. Conditional item effects count at a stated uptime. Utility stats (XP, gold, pickup
    /// radius, move speed, luck, projectile speed, knockback) are named, not scored. Bands by rarity: Common +5–8%,
    /// Uncommon +7–11%, Rare +10–15%, Epic +15–25%, Legendary +25–40% (plan/v5/V5_PLAN.md M3 G3.3).
    /// </summary>
    public static class BuildModel
    {
        /// <summary>Effects whose worth depends on the weapons carried or on hoarding gold (not banded).</summary>
        static readonly ItemEffectKind[] SynergyEffects =
        {
            ItemEffectKind.ExplosionDamage, ItemEffectKind.ExplosionDamageAfterDistance, ItemEffectKind.StatusDamage,
            ItemEffectKind.ControlledTargetDamage, ItemEffectKind.ExtraLightningJumps, ItemEffectKind.DamagePerGold,
        };

        public static readonly TargetBand[] Bands =
        {
            new TargetBand(0.05f, 0.08f), new TargetBand(0.07f, 0.11f), new TargetBand(0.10f, 0.15f), new TargetBand(0.15f, 0.25f), new TargetBand(0.25f, 0.40f),
        };

        const int ReferenceLevel = 6;
        const float BaseCritChance = 0.05f, BaseCritDamage = 1.5f;

        /// <summary>Uptimes and shares the conditional effects are scored at (model assumptions, in the report).</summary>
        public static readonly Dictionary<ItemEffectKind, (float uptime, string note)> EffectAssumptions = new Dictionary<ItemEffectKind, (float, string)>
        {
            [ItemEffectKind.AttackSpeedAfterTurn] = (0.5f, "turning hard half the time"),
            [ItemEffectKind.CritAfterNearMiss] = (0.3f, "a near miss every ~7 s"),
            [ItemEffectKind.IncomingDamageAtSpeed] = (0.7f, "at speed 70% of the time"),
            [ItemEffectKind.FireAllEveryKills] = (0.08f, "~1 kill a second: an extra volley every 12 s"),
            [ItemEffectKind.DamagePerWeaponFamily] = (3f, "3 weapon families"),
            [ItemEffectKind.DamagePerGold] = (0.25f, "a quarter of its cap on average"),
            [ItemEffectKind.ControlledTargetDamage] = (0.25f, "a quarter of hits on slowed or tarred targets"),
            [ItemEffectKind.StatusDamage] = (0.15f, "burns are ~15% of damage"),
            [ItemEffectKind.ExplosionDamage] = (0.25f, "explosions are ~25% of damage"),
            [ItemEffectKind.ExplosionDamageAfterDistance] = (0.03f, "boosts one explosion per charge (18 m without a hit): a small share of damage"),
            [ItemEffectKind.ExtraLightningJumps] = (0.1f, "lightning ~10% of damage, a jump adds ~+1 target"),
        };

        sealed class Mods : Weapons.IWeaponModifiers
        {
            public float Damage = 1f, Attack = 1f, Crit = BaseCritChance, CritDmg = BaseCritDamage, Qty, SizeMult = 1f;
            public float AttackSpeed => Attack;
            public float ProjectileSpeed => 1f;
            public float Size => SizeMult;
            public float Duration => 1f;
            public float Quantity => Qty;
            public float CritChance => Mathf.Min(1f, Crit);
            public float CritDamage => CritDmg;
            public float EliteDamage => 1f;
            public float GlobalDamage => Damage;
            public float CaptainAdditiveDamage => 0f;
            public float CaptainWeaponMultiplier(string weaponId) => 1f;
            public float ItemGlobalDamageMultiplier => 1f;
            public float ItemCritChanceBonus => 0f;
            public float ItemAttackSpeedMultiplier => 1f;
            public float FlameRangeMultiplier => 1f;
            public float BurnDamageMultiplier => 1f;
            public float PowderKegBlastRadiusMultiplier => 1f;
            public float ExplosionDamageMultiplier => 1f;
            public void ExplosionResolved() { }
            public int ExtraLightningJumps => 0;
        }

        /// <summary>The reference build's offense with these player stats: every weapon at level 6 with a median upgrade path.</summary>
        static float Offense(SimContent c, Mods m)
        {
            var coverage = c.Director != null ? c.Director.behaviorCoverage : null;
            var total = 0f;
            foreach (var w in c.WeaponsInOrder)
            {
                // Level 6 with the median damage growth measured by the weapon model: approximated by the config's level
                // factor and a flat package (the relative change a stat makes is what counts here).
                var slot = new Weapons.WeaponSlot { Config = w, Level = ReferenceLevel, DamageBonus = c.Progression.newWeaponDamageBonus, QuantityBonus = 1 };
                var damage = w.baseDamage * Weapons.WeaponStatResolver.LevelMult(slot) * m.GlobalDamage;
                var crit = 1f + Mathf.Clamp01(m.CritChance) * Mathf.Max(0f, m.CritDamage - 1f);
                var dps = damage * crit * Weapons.WeaponStatResolver.ProjectileQuantity(slot, m) / Mathf.Max(0.05f, Weapons.WeaponStatResolver.Cooldown(slot, m, 1f));
                var role = WeaponModel.RoleOf(w.behavior);
                if (role != WeaponRole.SingleTarget)
                    dps *= (coverage != null && (int)w.behavior < coverage.Length ? coverage[(int)w.behavior] : 1f) * m.SizeMult;
                total += dps;
            }
            return total;
        }

        static string UtilityName(StatId s)
        {
            switch (s)
            {
                case StatId.XPGain: return "XP gain";
                case StatId.GoldGain: return "gold gain";
                case StatId.PickupRadius: return "pickup radius";
                case StatId.MoveSpeed: return "move speed";
                case StatId.Luck: return "luck";
                case StatId.ProjectileSpeed: return "projectile speed";
                case StatId.Knockback: return "knockback";
                case StatId.DoubloonGain: return "doubloon gain";
                default: return null;
            }
        }

        /// <summary>Offense × effective hull after adding these stat changes to a fresh ship, relative to none.</summary>
        static float Score(SimContent c, IEnumerable<(StatId stat, float add, float mul)> changes, out List<string> utility, float extraOffense = 0f, float incomingMult = 1f)
        {
            var m = new Mods();
            var hull = 100f;
            utility = new List<string>();
            foreach (var (stat, add, mul) in changes)
            {
                switch (stat)
                {
                    case StatId.Damage: m.Damage = (m.Damage + add) * mul; break;
                    case StatId.AttackSpeed: m.Attack = (m.Attack + add) * mul; break;
                    case StatId.CritChance: m.Crit = (m.Crit + add) * mul; break;
                    case StatId.CritDamage: m.CritDmg = (m.CritDmg + add) * mul; break;
                    case StatId.Quantity: m.Qty = (m.Qty + add) * mul; break;
                    case StatId.Size: m.SizeMult = (m.SizeMult + add) * mul; break;
                    case StatId.Hull: hull = (hull + add) * mul; break;
                    default:
                        var name = UtilityName(stat);
                        if (name != null) utility.Add(add != 0f ? $"{name} +{add:0.###}" : $"{name} ×{mul:0.###}");
                        break;
                }
            }
            var baseOffense = Offense(c, new Mods());
            var offense = Offense(c, m) * (1f + extraOffense);
            var defense = hull / 100f / Mathf.Max(0.1f, incomingMult);
            return offense / baseOffense * defense - 1f;
        }

        public static List<BuildPiece> Pieces(SimContent c)
        {
            var list = new List<BuildPiece>();
            foreach (var lb in c.LogbooksInOrder.Where(l => l.inSaltwindPool))
            {
                var power = Score(c, new[] { ((StatId)lb.stat, lb.perLevelAdditive, lb.perLevelMultiplicative) }, out var utility);
                list.Add(new BuildPiece
                {
                    Id = lb.logbookId, Kind = "Logbook rank", Rarity = 0, Band = Bands[0],
                    Power = utility.Count > 0 && Mathf.Abs(power) < 1e-4f ? float.NaN : power, Utility = string.Join(", ", utility),
                });
            }
            foreach (var item in c.ChestPool)
            {
                var synergy = item.effects.Any(e => e != null && e.active && Array.IndexOf(SynergyEffects, e.kind) >= 0);
                var extra = 0f;
                var incoming = 1f;
                var notes = new List<string>();
                var stats = item.statModifiers.Select(s => ((StatId)s.stat, s.additive, s.multiplicative)).ToList();
                foreach (var e in item.effects.Where(e => e != null && e.active))
                {
                    if (!EffectAssumptions.TryGetValue(e.kind, out var a)) { notes.Add(e.kind + " (not modelled)"); continue; }
                    notes.Add($"{e.kind}: {a.note}");
                    switch (e.kind)
                    {
                        case ItemEffectKind.AttackSpeedAfterTurn: extra += (e.value - 1f) * a.uptime; break;
                        case ItemEffectKind.CritAfterNearMiss: extra += e.value * (BaseCritDamage - 1f) * a.uptime; break;
                        case ItemEffectKind.IncomingDamageAtSpeed: incoming *= Mathf.Lerp(1f, e.value, a.uptime); break;
                        case ItemEffectKind.FireAllEveryKills: extra += a.uptime; break;
                        case ItemEffectKind.DamagePerWeaponFamily: extra += Mathf.Min(e.cap > 0f ? e.cap : float.MaxValue, e.value * a.uptime); break;
                        case ItemEffectKind.DamagePerGold: extra += (e.cap > 0f ? e.cap : 0.1f) * a.uptime; break;
                        case ItemEffectKind.ControlledTargetDamage:
                        case ItemEffectKind.StatusDamage:
                        case ItemEffectKind.ExplosionDamage: extra += (e.value - 1f) * a.uptime; break;
                        case ItemEffectKind.ExplosionDamageAfterDistance: extra += (e.value - 1f) * a.uptime; break;
                        case ItemEffectKind.ExtraLightningJumps: extra += e.value * a.uptime; break;
                        default: notes.Add(e.kind + " (not scored)"); break;
                    }
                }
                var power = Score(c, stats, out var utility, extra, incoming);
                var rarity = Mathf.Clamp(item.rarity, 0, 4);
                list.Add(new BuildPiece
                {
                    Id = item.itemId, Kind = "Item", Rarity = rarity, Band = Bands[rarity], Synergy = synergy,
                    Power = Mathf.Abs(power) < 1e-4f && utility.Count > 0 ? float.NaN : power, Utility = string.Join(", ", utility), Assumption = string.Join("; ", notes),
                });
            }
            return list;
        }

        /// <summary>One retuned value: which piece, what changed, from and to.</summary>
        public struct PieceTune { public string Id, Field; public float From, To; }

        static readonly StatId[] LinearStats = { StatId.Damage, StatId.AttackSpeed, StatId.Hull, StatId.Size };
        /// <summary>
        /// V5 M3 (G3.3): moves every scored piece outside its band toward the band's middle by scaling its one linear value:
        /// a Logbook's per-rank value (damage, attack speed, hull, size) or a plain item's multiplier on those stats. Items
        /// with triggered effects are not tuned (review M3 #2: their worth depends on the build). Volley (Quantity) and crit pieces are
        /// left alone and stay flagged: a volley step cannot be fractional, and crit is weak everywhere (×1.5 crit damage),
        /// which is a design decision, not a per-piece value. Edits the copies in <paramref name="c"/>.
        /// </summary>
        public static List<PieceTune> TuneBands(SimContent c)
        {
            var log = new List<PieceTune>();
            for (var round = 0; round < 6; round++)
            {
                foreach (var piece in Pieces(c))
                {
                    if (float.IsNaN(piece.Power) || piece.InBand || piece.Power <= 0f) continue;
                    var goal = (piece.Band.min + piece.Band.max) / 2f;
                    var k = goal / piece.Power;
                    if (piece.Kind != "Item")
                    {
                        var lb = c.LogbooksInOrder.First(l => l.logbookId == piece.Id);
                        if (Array.IndexOf(LinearStats, (StatId)lb.stat) < 0) continue;
                        Record(log, piece.Id, "perLevelAdditive", lb.perLevelAdditive, lb.perLevelAdditive * k);
                        lb.perLevelAdditive *= k;
                        continue;
                    }
                    var item = c.Item(piece.Id);
                    if (piece.Synergy || item.effects.Any(e => e != null && e.active)) continue; // effects: flagged or synergy, not tuned
                    var stat = item.statModifiers.FirstOrDefault(m => Array.IndexOf(LinearStats, (StatId)m.stat) >= 0);
                    if (stat == null || item.statModifiers.Length != 1) continue;
                    if (stat.multiplicative != 1f)
                    {
                        var from = stat.multiplicative;
                        stat.multiplicative = 1f + (from - 1f) * k;
                        Record(log, piece.Id, "statModifiers[0].multiplicative", from, stat.multiplicative);
                    }
                    else
                    {
                        Record(log, piece.Id, "statModifiers[0].additive", stat.additive, stat.additive * k);
                        stat.additive *= k;
                    }
                }
            }
            return log;
        }

        static void Record(List<PieceTune> log, string id, string field, float from, float to)
        {
            var i = log.FindIndex(t => t.Id == id && t.Field == field);
            if (i >= 0) { var t = log[i]; t.To = to; log[i] = t; }
            else log.Add(new PieceTune { Id = id, Field = field, From = from, To = to });
        }

        static readonly string[] RarityNames = { "Common", "Uncommon", "Rare", "Epic", "Legendary" };

        public static string Report(IEnumerable<BuildPiece> pieces)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("| Piece | Kind | Rarity | Build power | Band | Utility | Effects (model assumption) |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var p in pieces)
            {
                var power = float.IsNaN(p.Power) ? "utility only" : $"{p.Power:+0.0%;-0.0%}";
                var mark = float.IsNaN(p.Power) ? "–" : p.Synergy ? "synergy, not banded:" : p.InBand ? "✓" : "✗";
                sb.AppendLine($"| {p.Id} | {p.Kind} | {RarityNames[p.Rarity]} | {power} | {mark} {p.Band.min:0%}–{p.Band.max:0%} | {p.Utility} | {p.Assumption} |");
            }
            return sb.ToString();
        }
    }
}
