using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.UI;
using NUnit.Framework;
using UnityEngine;

namespace HighSeas.Tests
{
    public class LevelOfferGeneratorTests
    {
        GameDatabase _db;
        LevelUpSettings _settings;
        DemoPacingSettings _pacing;
        RunRng _rng;

        [SetUp]
        public void SetUp()
        {
            _db = ScriptableObject.CreateInstance<GameDatabase>();
            _settings = ScriptableObject.CreateInstance<LevelUpSettings>();
            _pacing = ScriptableObject.CreateInstance<DemoPacingSettings>();
            _rng = new RunRng(12345);

            _db.weapons = new[]
            {
                MakeWeapon("deck_cannons", "Deck Cannons", WeaponPattern.AutoProjectile),
                MakeWeapon("broadside_battery", "Broadside Battery", WeaponPattern.FanVolley),
                MakeWeapon("powder_kegs", "Powder Kegs", WeaponPattern.MineDrop),
                MakeWeapon("lightning_mast", "Lightning Mast", WeaponPattern.ChainLightning),
                MakeWeapon("flying_cutlasses", "Flying Cutlasses", WeaponPattern.Orbit),
                MakeWeapon("burning_oil", "Burning Oil", WeaponPattern.TrailZone)
            };
            _db.logbooks = new[]
            {
                MakeLogbook("log_damage", StatId.Damage),
                MakeLogbook("log_speed", StatId.AttackSpeed)
            };
        }

        [Test]
        public void FirstLevelUp_GuaranteesStarterUpgradeNewWeaponAndLogbook()
        {
            var ownedWeapons = new List<string> { "deck_cannons" };
            var weaponLevels = new Dictionary<string, int> { ["deck_cannons"] = 1 };
            var offers = LevelOfferGenerator.Generate(
                _db, _settings, _pacing, _rng, 0f,
                ownedWeapons, weaponLevels, new List<string>(), new Dictionary<string, int>(),
                2, 2, isFirstLevelUp: true);

            Assert.That(offers.Count, Is.EqualTo(3));
            Assert.That(HasKind(offers, LevelOfferKind.WeaponUpgrade, "deck_cannons"), Is.True);
            Assert.That(HasNewWeapon(offers), Is.True);
            Assert.That(HasKind(offers, LevelOfferKind.Logbook), Is.True);
        }

        [Test]
        public void Generate_PreventsDuplicateCards()
        {
            var ownedWeapons = new List<string> { "deck_cannons" };
            var weaponLevels = new Dictionary<string, int> { ["deck_cannons"] = 2 };
            for (var seed = 1; seed < 20; seed++)
            {
                var offers = LevelOfferGenerator.Generate(
                    _db, _settings, _pacing, new RunRng(seed), 0f,
                    ownedWeapons, weaponLevels, new List<string>(), new Dictionary<string, int>(),
                    2, 2, isFirstLevelUp: false);
                var keys = new HashSet<string>();
                for (var i = 0; i < offers.Count; i++)
                {
                    var key = $"{offers[i].kind}:{offers[i].id}";
                    Assert.That(keys.Add(key), Is.True, $"Duplicate card at seed {seed}: {key}");
                }
            }
        }

        [Test]
        public void Generate_ExcludesMaxedWeapons()
        {
            var ownedWeapons = new List<string> { "deck_cannons", "burning_oil" };
            var weaponLevels = new Dictionary<string, int>
            {
                ["deck_cannons"] = 12,
                ["burning_oil"] = 12
            };

            for (var i = 0; i < 30; i++)
            {
                var offers = LevelOfferGenerator.Generate(
                    _db, _settings, _pacing, new RunRng(500 + i), 0f,
                    ownedWeapons, weaponLevels, new List<string>(), new Dictionary<string, int>(),
                    2, 2, isFirstLevelUp: false);
                for (var c = 0; c < offers.Count; c++)
                {
                    if (offers[c].kind != LevelOfferKind.WeaponUpgrade) continue;
                    Assert.That(offers[c].id, Is.Not.EqualTo("deck_cannons"));
                    Assert.That(offers[c].id, Is.Not.EqualTo("burning_oil"));
                }
            }
        }

        [Test]
        public void Generate_WithFullWeaponSlots_DoesNotOfferNewWeapons()
        {
            var ownedWeapons = new List<string> { "deck_cannons", "burning_oil" };
            var weaponLevels = new Dictionary<string, int>
            {
                ["deck_cannons"] = 2,
                ["burning_oil"] = 2
            };

            for (var i = 0; i < 30; i++)
            {
                var offers = LevelOfferGenerator.Generate(
                    _db, _settings, _pacing, new RunRng(900 + i), 0f,
                    ownedWeapons, weaponLevels, new List<string>(), new Dictionary<string, int>(),
                    2, 2, isFirstLevelUp: false);
                for (var c = 0; c < offers.Count; c++)
                    Assert.That(offers[c].kind, Is.Not.EqualTo(LevelOfferKind.NewWeapon));
            }
        }

        [Test]
        public void WeaponUpgradeOffer_ScalesStatLinesByRarity()
        {
            var weapon = _db.GetWeapon("deck_cannons");
            var common = LevelOfferGenerator.BuildWeaponUpgradeOffer(weapon, 1, Rarity.Common, _settings, _pacing, _rng);
            var legendary = LevelOfferGenerator.BuildWeaponUpgradeOffer(weapon, 1, Rarity.Legendary, _settings, _pacing, _rng);
            Assert.That(common.upgradeLines.Length, Is.EqualTo(1));
            Assert.That(legendary.upgradeLines.Length, Is.EqualTo(3));
        }

        [Test]
        public void WeaponDefinitions_ExposePatternSpecificAxes()
        {
            Assert.That(_db.GetWeapon("deck_cannons").GetUpgradeConfigs().Count, Is.GreaterThanOrEqualTo(4));
            Assert.That(_db.GetWeapon("burning_oil").GetUpgradeConfigs().Count, Is.EqualTo(3));
            Assert.That(ContainsAxis(_db.GetWeapon("burning_oil"), UpgradeAxis.Duration), Is.True);
            Assert.That(ContainsAxis(_db.GetWeapon("lightning_mast"), UpgradeAxis.Quantity), Is.True);
        }

        static bool ContainsAxis(WeaponDefinition weapon, UpgradeAxis axis)
        {
            var configs = weapon.GetUpgradeConfigs();
            for (var i = 0; i < configs.Count; i++)
                if (configs[i].axis == axis) return true;
            return false;
        }

        static bool HasKind(List<LevelOffer> offers, LevelOfferKind kind, string id = null)
        {
            for (var i = 0; i < offers.Count; i++)
            {
                if (offers[i].kind != kind) continue;
                if (id == null || offers[i].id == id) return true;
            }
            return false;
        }

        static bool HasNewWeapon(List<LevelOffer> offers)
        {
            for (var i = 0; i < offers.Count; i++)
                if (offers[i].kind == LevelOfferKind.NewWeapon) return true;
            return false;
        }

        static WeaponDefinition MakeWeapon(string id, string name, WeaponPattern pattern)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.id = id;
            weapon.displayName = name;
            weapon.pattern = pattern;
            weapon.maxLevel = 12;
            return weapon;
        }

        static LogbookDefinition MakeLogbook(string id, StatId stat)
        {
            var logbook = ScriptableObject.CreateInstance<LogbookDefinition>();
            logbook.id = id;
            logbook.displayName = id;
            logbook.primaryStat = stat;
            logbook.perLevelAdditive = 0.05f;
            logbook.maxLevel = 10;
            return logbook;
        }
    }
}
