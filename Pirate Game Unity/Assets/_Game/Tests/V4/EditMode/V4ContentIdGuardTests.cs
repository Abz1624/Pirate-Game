using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// M16: guards for the content-id fallbacks removed from Foundation — they were never reached with the shipped data,
    /// and these tests keep it that way.
    /// </summary>
    [Category("V4")]
    public sealed class V4ContentIdGuardTests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";

        [Test]
        public void BothGameDatabases_AreValid_SoNoStubIsNeeded()
        {
            Assert.IsTrue(GameDatabaseLoader.IsValid(AssetDatabase.LoadAssetAtPath<GameDatabase>(GameDatabaseLoader.AssetPath)), "Editor database");
            Assert.IsTrue(GameDatabaseLoader.IsValid(Resources.Load<GameDatabase>("GameDatabase")), "player database (Resources)");
        }

        [Test]
        public void TheHarpoonBinding_UsesTheHarpoonProjectile()
        {
            var juice = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
            Assert.IsNotNull(juice.harpoonProjectilePrefab);
            var harpoon = juice.weaponVfxBindings.Single(b => b != null && b.weaponId == "harpoon_gun");
            Assert.AreSame(juice.harpoonProjectilePrefab, harpoon.projectilePrefab, "so the harpoon never gets a cannonball trail");
        }

        /// <summary>M16: the aim overlay colours moved from V3's palette (keyed by weapon id) into the weapon configs.</summary>
        [Test]
        public void EveryWeapon_CarriesV3sAimOverlayColour()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            var palette = new (string id, float r, float g, float b)[]
            {
                ("deck_cannons", 0.35f, 0.85f, 1f), ("broadside_battery", 1f, 0.55f, 0.15f), ("powder_kegs", 0.95f, 0.35f, 0.25f),
                ("flying_cutlasses", 0.75f, 0.95f, 0.35f), ("flamethrower", 1f, 0.4f, 0.1f), ("lightning_mast", 0.55f, 0.75f, 1f),
                ("long_nine", 0.95f, 0.95f, 0.85f), ("swivel_guns", 0.65f, 0.55f, 1f), ("harpoon_gun", 0.35f, 0.95f, 0.75f),
                ("mortar", 1f, 0.45f, 0.15f),
                // V5 M4: the new weapons' own colours.
                ("anchor_flail", 0.6f, 0.62f, 0.7f), ("chainshot", 0.85f, 0.85f, 0.92f), ("siren_bell", 0.45f, 0.95f, 0.95f),
                ("ramming_prow", 0.95f, 0.75f, 0.35f),
            };
            var weapons = config.All<WeaponConfig>().ToDictionary(w => w.weaponId);
            Assert.AreEqual(palette.Length, weapons.Count, "one colour per weapon");
            foreach (var (id, r, g, b) in palette)
            {
                var c = weapons[id].aimOverlayColor;
                Assert.AreEqual(r, c.r, 1e-5f, id); Assert.AreEqual(g, c.g, 1e-5f, id); Assert.AreEqual(b, c.b, 1e-5f, id);
            }
        }

        [Test]
        public void TheStageProfileStarters_EqualTheUnlockConfig()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            CollectionAssert.AreEqual(config.Single<UnlockConfig>().starterWeaponIds, config.Single<StageContentConfig>().sharedProfile.starterWeaponIds);
        }
    }
}
