using System.Collections.Generic;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.UI;
using NUnit.Framework;
using UnityEngine;

namespace HighSeas.Tests
{
    public class CombatExperimentTests
    {
        GameDatabase _db;
        LevelUpSettings _settings;
        DemoPacingSettings _pacing;
        RunRng _rng;

        [SetUp]
        public void SetUp()
        {
            CombatExperimentContext.ResetForTests();
            CombatExperimentContext.SetActive(CombatExperimentProfile.CreateBuiltIn("D_FULL_COMBAT_V2"));

            _db = ScriptableObject.CreateInstance<GameDatabase>();
            _settings = ScriptableObject.CreateInstance<LevelUpSettings>();
            _pacing = ScriptableObject.CreateInstance<DemoPacingSettings>();
            _rng = new RunRng(4242);
            _db.weapons = new[]
            {
                MakeWeapon("deck_cannons"),
                MakeWeapon("broadside_battery"),
                MakeWeapon("long_nine"),
                MakeWeapon("swivel_guns")
            };
            _db.logbooks = new[]
            {
                MakeLogbook("log_damage", StatId.Damage)
            };
        }

        [TearDown]
        public void TearDown() => CombatExperimentContext.ResetForTests();

        [Test]
        public void Profile_AlwaysUsesFullCombatV2()
        {
            var profile = CombatExperimentProfile.CreateBuiltIn("A_CURRENT");
            Assert.That(profile.weaponSlotCap, Is.EqualTo(6));
            Assert.That(profile.directorV2Enabled, Is.True);
            Assert.That(profile.profileId, Is.EqualTo(CombatExperimentProfile.FullCombatV2Id));
        }

        [Test]
        public void Generate_WithSixSlots_DoesNotOfferNewWeaponWhenFull()
        {
            var owned = new List<string>
            {
                "deck_cannons", "broadside_battery", "long_nine", "swivel_guns", "deck_cannons", "broadside_battery"
            };
            var levels = new Dictionary<string, int>
            {
                ["deck_cannons"] = 2,
                ["broadside_battery"] = 2,
                ["long_nine"] = 2,
                ["swivel_guns"] = 2
            };

            for (var seed = 0; seed < 20; seed++)
            {
                var offers = LevelOfferGenerator.Generate(
                    _db, _settings, _pacing, new RunRng(seed), 0f,
                    owned, levels, new List<string>(), new Dictionary<string, int>(),
                    6, 2, false, null, -1f, -1f, 10, useFillStateWeighting: true);
                for (var c = 0; c < offers.Count; c++)
                    Assert.That(offers[c].kind, Is.Not.EqualTo(LevelOfferKind.NewWeapon));
            }
        }

        [Test]
        public void Generate_PityGuaranteesNewWeaponOptionAtLevelFour()
        {
            var owned = new List<string> { "deck_cannons" };
            var levels = new Dictionary<string, int> { ["deck_cannons"] = 2 };
            var offers = LevelOfferGenerator.Generate(
                _db, _settings, _pacing, _rng, 0f,
                owned, levels, new List<string>(), new Dictionary<string, int>(),
                6, 2, false, null, -1f, -1f, 4, useFillStateWeighting: true);

            var hasNewWeapon = false;
            for (var i = 0; i < offers.Count; i++)
            {
                if (offers[i].kind == LevelOfferKind.NewWeapon)
                    hasNewWeapon = true;
            }
            Assert.That(hasNewWeapon, Is.True);
        }

        [Test]
        public void OpeningDraft_ReturnsThreeDistinctWeapons()
        {
            var offers = LevelOfferGenerator.GenerateOpeningWeaponDraft(
                _db, _settings, _rng, new List<string> { "deck_cannons" }, 6);
            Assert.That(offers.Count, Is.EqualTo(3));
            var ids = new HashSet<string>();
            for (var i = 0; i < offers.Count; i++)
            {
                Assert.That(offers[i].kind, Is.EqualTo(LevelOfferKind.NewWeapon));
                Assert.That(ids.Add(offers[i].id), Is.True);
                Assert.That(offers[i].id, Is.Not.EqualTo("deck_cannons"));
            }
        }

        [Test]
        public void ForwardArcGeometry_RespectsHalfArcWidth()
        {
            var origin = Vector3.zero;
            var forward = Vector3.forward;
            Assert.That(WeaponTargeting.IsWithinForwardArc(origin, forward, new Vector3(0f, 0f, 5f), 90f), Is.True);
            Assert.That(WeaponTargeting.IsWithinForwardArc(origin, forward, new Vector3(5f, 0f, 0f), 45f), Is.False);
        }

        [Test]
        public void ResolveArcDegrees_PrefersTargetArcOverFanSpread()
        {
            var weapon = MakeWeapon("grapeshot");
            weapon.targetArcDegrees = 100f;
            weapon.fanSpreadDegrees = 50f;
            Assert.That(WeaponTargeting.ResolveArcDegrees(weapon), Is.EqualTo(100f));
        }
        static WeaponDefinition MakeWeapon(string id)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.id = id;
            weapon.displayName = id;
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
