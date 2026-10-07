using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Presentation.Effects;
using RoguePirates.V4.Presentation.Hud;
using RoguePirates.V4.Progression;
using RoguePirates.V4.Views;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M8z (7 Oct, I8z.2): what moved out of code into data or the run views: the enemy type colours and the elite
    /// flag (enemy config), the level-up cards' numbers (IRunViews), the weapon switch-off list (UnlockConfig).
    /// </summary>
    [Category("V5")]
    public sealed class V5ArchitectureM8zTests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
            WeaponFeatureFlags.Register(null);
        }

        T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            _created.Add(o);
            return o;
        }

        static ConfigSet Set => AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);

        [Test]
        public void EnemyTypeColours_AndTheEliteFlag_AreEnemyData()
        {
            using var rt = new ConfigSetRuntime(Set);
            var coloured = 0;
            foreach (var a in rt.All<EnemyArchetypeConfig>())
            {
                var s = EnemyStatResolver.Resolve(a);
                if (a.visual.typeColor.a > 0f) { coloured++; Assert.AreEqual(a.visual.typeColor, s.SeenColor, a.enemyId); }
                else Assert.AreEqual(s.visualTint, s.SeenColor, a.enemyId + ": no type colour keeps the tint");
                Assert.IsFalse(s.isElite, a.enemyId);
            }
            var elites = 0;
            foreach (var v in rt.All<EnemyVariantConfig>())
            {
                var s = EnemyStatResolver.Resolve(v);
                if (v.look.typeColor.a > 0f) coloured++;
                Assert.AreEqual(v.isElite, s.isElite, v.variantId);
                if (!v.isElite) continue;
                elites++;
                Assert.AreEqual(v.baseArchetype.enemyId, s.baseEnemyId, v.variantId + ": its VFX profile falls back to its base enemy's");
            }
            Assert.AreEqual(17, coloured, "the 17 ship types and variants the owner coloured (3 Oct)");
            Assert.AreEqual(2, elites, "the two registry elites");

            // The runtime definition carries both, and the presenter reads them from it.
            var def = Make<RoguePirates.V4.Foundation.Data.EnemyDefinition>();
            var variant = rt.All<EnemyVariantConfig>().First(v => v.isElite);
            Ports.V4RuntimeDatabase.Apply(EnemyStatResolver.Resolve(variant), def);
            Assert.IsTrue(def.isElite);
            Assert.AreEqual(variant.baseArchetype.enemyId, def.baseEnemyId);
            var ship = rt.All<EnemyArchetypeConfig>().First(a => a.visual.typeColor.a > 0f);
            Ports.V4RuntimeDatabase.Apply(EnemyStatResolver.Resolve(ship), def);
            Assert.AreEqual(ship.visual.typeColor, EnemyViewPresenter.TypeColor(def));
        }

        [Test]
        public void TheWeaponSwitchOffList_IsConfig()
        {
            using var rt = new ConfigSetRuntime(Set);
            var unlocks = rt.Single<UnlockConfig>();
            Assert.IsNotNull(unlocks.disabledWeaponIds, "UnlockConfig.disabledWeaponIds");
            var weapon = rt.All<WeaponConfig>().First().weaponId;
            Assert.IsTrue(WeaponFeatureFlags.IsWeaponEnabled(weapon));
            WeaponFeatureFlags.Register(new[] { weapon });
            Assert.IsFalse(WeaponFeatureFlags.IsWeaponEnabled(weapon), "a listed weapon is switched off");
            WeaponFeatureFlags.Register(unlocks.disabledWeaponIds);
            Assert.IsTrue(WeaponFeatureFlags.IsWeaponEnabled(weapon), "the shipped list switches none off");
        }

        sealed class FakeLoadout : ILoadoutView
        {
            public WeaponModifiersView Modifiers;
            public string Owned;
            public int WeaponSlotCap => 6;
            public int WeaponCount => 0;
            public string WeaponId(int index) => null;
            public string WeaponName(int index) => null;
            public string WeaponIconName(int index) => null;
            public int WeaponLevel(int index) => 0;
            public int ItemCount => 0;
            public string ItemId(int index) => null;
            public string ItemName(int index) => null;
            public int ItemStacks(int index) => 0;
            public bool TryWeaponModifiers(string weaponId, out WeaponModifiersView modifiers)
            {
                modifiers = Modifiers;
                return weaponId == Owned;
            }
        }

        sealed class FakeProgression : IProgressionView
        {
            public float Hull;
            public bool Exists => true;
            public int Level => 1;
            public float CurrentXp => 0f;
            public float XpToNext => 1f;
            public int LogbookCount => 0;
            public string LogbookId(int index) => null;
            public string LogbookName(int index) => null;
            public int LogbookRank(int index) => 0;
            public float StatValue(StatId stat) => stat == StatId.Hull ? Hull : 0f;
            public float StatBase(StatId stat) => stat == StatId.Hull ? 100f : 0f;
        }

        [Test]
        public void CardStatLines_ReadTheRunViews()
        {
            var weapon = Make<WeaponConfig>();
            weapon.weaponId = "test_gun";
            weapon.baseDamage = 10f;
            weapon.baseCooldown = 2f;
            weapon.maxLevel = 8;
            var views = new RunViews
            {
                Loadout = new FakeLoadout
                {
                    Owned = "test_gun",
                    Modifiers = new WeaponModifiersView { Level = 2, DamageBonus = 1f, CooldownMult = 1f, SpeedMult = 1f, SizeMult = 1f, DurationMult = 1f },
                },
                Progression = new FakeProgression { Hull = 120f },
            };
            var lines = CardStatLines.WeaponUpgrade(weapon, new[] { new UpgradeLine(ConfigEnums.UpgradeAxis.Cooldown, 0.25f) }, 3, views);
            Assert.IsNotNull(lines, "an owned weapon's upgrade shows its numbers");
            CollectionAssert.Contains(lines, "Reload 2s → 1.5s");
            Assert.IsNull(CardStatLines.WeaponUpgrade(weapon, null, 3, new RunViews()), "no loadout view: no numbers (the card keeps its text)");

            var logbook = Make<LogbookConfig>();
            logbook.stat = (int)StatId.Hull;
            logbook.perLevelMultiplicative = 1f;
            var hull = CardStatLines.Logbook(logbook, 6f, views);
            Assert.AreEqual(1, hull.Length);
            StringAssert.EndsWith("120 → 126", hull[0]);
        }
    }
}
