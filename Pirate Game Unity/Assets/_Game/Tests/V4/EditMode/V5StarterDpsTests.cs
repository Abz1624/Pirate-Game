using System.Globalization;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Weapons;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M8z (7 Oct, review), T8z.x combat: the equal level-1 DPS rule (playtest round 2, plan/playtest/NOTES.md: "every
    /// weapon deals the same damage per second to one target at level 1"). Read from the config through
    /// <see cref="WeaponStatResolver"/>, as the game's own theoretical DPS (PlayerPowerIndex.Offense) counts it, for one
    /// target: base damage x level factor x volley x hits per firing / cooldown (no crit, no coverage, no items).
    /// </summary>
    public sealed class V5StarterDpsTests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";

        const string PendingReason =
            "V5 M8z (7 Oct, review): fails on today's data, owner decision pending. Playtest round 6 (commit 0bd92afb, owner " +
            "feedback 'weapon roles': heavy hitters fire faster, crowd weapons -25% damage) moved the starters off the round-2 " +
            "equal 8.96 DPS; the plan (T8z.x, V5_PLAN.md line 27) still names the equal rule. Un-ignore when the owner confirms " +
            "the rule (then retune the data), or rewrite it as the role bands he picks.";

        sealed class NoMods : IWeaponModifiers
        {
            public float AttackSpeed => 1f;
            public float ProjectileSpeed => 1f;
            public float Size => 1f;
            public float Duration => 1f;
            public float Quantity => 0f;
            public float CritChance => 0f;
            public float CritDamage => 1f;
            public float EliteDamage => 1f;
            public float GlobalDamage => 1f;
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

        /// <summary>One target, level 1, a new weapon (no upgrades): the resolver's numbers.</summary>
        public static float LevelOneSingleTargetDps(WeaponConfig config)
        {
            var mods = new NoMods();
            var slot = new WeaponSlot { Config = config, Level = 1 };
            WeaponStatResolver.ApplyEvolution(slot);
            var damage = config.baseDamage * WeaponStatResolver.LevelMult(slot);
            var cooldown = Mathf.Max(0.05f, WeaponStatResolver.Cooldown(slot, mods, 1f));
            return damage * WeaponStatResolver.ProjectileQuantity(slot, mods) * WeaponStatResolver.HitsPerFiring(config) / cooldown;
        }

        [Test]
        [Ignore(PendingReason)]
        public void EveryStartingWeapon_HasTheSameLevelOneSingleTargetDps_Within5Percent()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            var unlocks = config.Single<UnlockConfig>();
            Assert.IsNotNull(unlocks);
            var starters = unlocks.starterWeaponIds.Distinct().ToList();
            Assert.Greater(starters.Count, 1, "starting weapons in the unlock config");
            var weapons = config.All<WeaponConfig>().ToDictionary(w => w.weaponId);
            CollectionAssert.IsSubsetOf(starters, weapons.Keys, "every starting weapon has a config");
            var dps = starters.ToDictionary(id => id, id => LevelOneSingleTargetDps(weapons[id]));
            var sorted = dps.Values.OrderBy(v => v).ToList();
            var median = sorted[sorted.Count / 2];
            var table = string.Join(", ", dps.OrderBy(kv => kv.Value).Select(kv => kv.Key + " " + kv.Value.ToString("0.00", CultureInfo.InvariantCulture)));
            Debug.Log($"[V5 starter DPS] median {median:0.00}: {table}");
            var off = dps.Where(kv => Mathf.Abs(kv.Value / median - 1f) > 0.05f)
                .Select(kv => $"{kv.Key} {kv.Value:0.00} ({(kv.Value / median - 1f) * 100f:+0;-0}%)").ToList();
            CollectionAssert.IsEmpty(off, $"level-1 single-target DPS within 5% of the median {median:0.00} (all: {table})");
        }
    }
}
