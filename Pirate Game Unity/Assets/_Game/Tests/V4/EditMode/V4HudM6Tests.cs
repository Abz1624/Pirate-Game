using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.POI;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Events;
using RoguePirates.V4.Presentation.Hud;
using RoguePirates.V4.Progression;
using RoguePirates.V4.Views;
using RoguePirates.V4.Weapons;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    [Category("V4")]
    public sealed class V4HudM6Tests
    {
        static string GameRoot => Path.Combine(Application.dataPath, "_Game");

        // ---- Presentation isolation -------------------------------------------------------------------------------

        /// <summary>Legacy hubs presenters must not reach; they read IRunViews and the event stream only.</summary>
        static readonly string[] LegacyHubs =
        {
            "RunManager", "LevelSystem", "POIManager", "EnemyWorld", "V3DiscoverySystem", "WeaponController",
            "V3RunItemInventory", "V3RunContext", "V3CaptainSelection", "RunCombatTelemetry", "SaltwindRunSettlement",
            "WeaponDamageLedger", "RunMarksTracker", "V3RunBootstrap", "V3ToastHud", "EncounterSpawner", "PickupWorld",
            "FindFirstObjectByType<PlayerShipController>", "FindFirstObjectByType<RoguePirates.V4.Foundation.Player.PlayerShipController>",
            // M15: the shared progression state under its V4 names (V3CaptainSelection, V3ContentUnlocks, V3HomeSeed,
            // V3PlaytestFlags, V3RunContext before).
            "CaptainSelection", "ContentUnlocks", "HomeSeed", "PlaytestFlags", "ActiveRun",
            // Review finding: the ports to the shared run/ship/world (M15: was the legacy adapters), the legacy VFX
            // broker, the playtest flag and the weapon module's internals.
            "RoguePirates.V4.Ports", "JuiceVfxBroker", "WeaponEnemyPlaytestSession", "LegacyHazardTelegraphView",
            // M14.9b: the V3 effect components (the world presenters read the bounds through IWorldView).
            "WeaponJuiceController", "CombatVfxController", "TriggerBus",
            "WeaponManager", "WeaponCombat", "WeaponTargetingService", "LegacyWeaponEnemies", "EnemyWorld"
        };

        // Approved exceptions (not in the list): the combat debug overlay flag CombatDebugOverlays (V3's O key toggles it;
        // the enemy stat labels read it too) and the shared art/settings assets presenters read.

        [Test]
        public void Presenters_ReadOnlyViewsAndEvents()
        {
            var offenders = new List<string>();
            foreach (var file in Directory.GetFiles(Path.Combine(GameRoot, "V4/Presentation"), "*.cs", SearchOption.AllDirectories))
            {
                var code = StripComments(File.ReadAllText(file));
                foreach (var hub in LegacyHubs)
                    if (Regex.IsMatch(code, $@"(?<![\w.]){Regex.Escape(hub)}(?![\w])"))
                        offenders.Add($"{Path.GetFileName(file)}: {hub}");
            }
            Assert.IsEmpty(offenders, "Presenters must read IRunViews/events, not legacy hubs:\n" + string.Join("\n", offenders));
        }

        [Test]
        public void RuntimeNeverReferencesPresentation()
        {
            foreach (var file in Directory.GetFiles(Path.Combine(GameRoot, "V4/Runtime"), "*.cs", SearchOption.AllDirectories))
                StringAssert.DoesNotContain("RoguePirates.V4.Presentation", StripComments(File.ReadAllText(file)), Path.GetFileName(file));
        }

        /// <summary>M15: the legacy namespace is gone; nothing is tagged for deletion any more.</summary>
        [Test]
        public void NoLegacyNamespace_AndNoTaggedTypes_Remain()
        {
            var types = System.AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name.StartsWith("RoguePirates.V4"))
                .SelectMany(a => a.GetTypes())
                .Where(t => !t.IsNested && !t.Name.StartsWith("<"))
                .ToList();
            Assert.IsEmpty(types.Where(t => t.Namespace == "RoguePirates.V4.Legacy").Select(t => t.Name), "legacy namespace");
            Assert.IsEmpty(types.Where(t => System.Reflection.CustomAttributeExtensions.GetCustomAttribute<DeletedInPhaseAttribute>(t) != null)
                .Select(t => t.Name), "types tagged [DeletedInPhase]");
        }

        static string StripComments(string code) =>
            Regex.Replace(code, @"//[^\n]*|/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        // ---- Loadout HUD ------------------------------------------------------------------------------------------

        /// <summary>V5 M8z (7 Oct, review): the content with the art team's icons (V4IconCatalog). Art for more content goes here.</summary>
        static readonly string[] ArtWeapons =
        {
            "broadside_battery", "deck_cannons", "flamethrower", "flying_cutlasses", "harpoon_gun", "lightning_mast", "long_nine",
            "mortar", "powder_kegs", "swivel_guns",
            // Hackathon (13 Oct): the art team's icon set (Assets/_Game/V4/Demo/Art/Icons).
            "anchor_flail", "chainshot", "cursed_doubloons", "lighthouse_lantern", "maelstrom_charge", "monkey_gunners", "parrot_squadron",
            // Owner's mapping (13 Oct): Aura is the Siren Bell; Squid Ink Pots borrows the Kraken Ink Sac icon for now.
            "siren_bell", "squid_ink_pots",
        };

        static readonly string[] ArtItems =
        {
            "admirals_ledger", "black_powder_keg", "broadside_drum", "captains_bell", "copper_conductor", "cursed_compass",
            "double_fuse_cord", "dragons_breath_nozzle", "ghost_lantern", "golden_figurehead", "grapeshot_pouch", "gunners_gloves",
            "leviathan_tooth", "lucky_doubloon", "powder_fuse", "reinforced_keel", "sea_witchs_eye", "serrated_scabbard", "stormglass",
            // Hackathon (13 Oct): the art team's icon set.
            "banana_crate", "barnacle_crown", "black_spot", "captains_last_rum", "captains_perch", "crows_nest", "cursed_coin",
            "dead_mans_compass", "fresnel_lens", "jolly_roger", "leviathan_heart", "manta_sail", "powder_horn_of_the_line",
            "powder_monkey", "reef_carapace", "rusted_shackle", "shark_tooth_necklace", "siege_quadrant", "siren_scale", "skeleton_key",
            "smugglers_ledger", "spare_planks", "storm_conch", "swivel_mount", "whale_oil_barrel",
            // Owner's mapping (13 Oct): "chain shott" is the Chain Locker, the Kraken Ink Sac is the Cuttlefish Bone.
            "chain_locker", "cuttlefish_bone",
        };

        /// <summary>Hackathon (13 Oct): the Logbooks with the art team's icons; the rest show their name.</summary>
        internal static readonly string[] ArtLogbooks =
        {
            "carpenters_notes", "cartographers_folio", "fortune", "gunnery_manual", "ironclad", "lookouts_glass", "navigators_charts",
            "oversized_shot", "plunderer", "powder_drill", "quartermasters_ledger", "scavengers_net", "tidecaller",
            // Owner's mapping (13 Oct): Big Game Hunter (was Admiral) and Echo Arsenal (was Extra Hands).
            "admiral", "extra_hands",
        };

        [Test]
        public void IconCatalog_ArtContentHasItsIcon_EverythingElseFallsBackToItsName()
        {
            // V5 M8z (7 Oct, review): the 10 weapons and 19 items with art show their icon (R-90: V3 missed three apostrophe
            // names); playtest round 7 removed the borrowed placeholder icons of the rest on purpose, so they have no icon
            // and their slot and card show the name instead (it must not be empty).
            var catalog = IconCatalog.Load();
            Assert.IsNotNull(catalog, "Assets/_Game/V4/Resources/V4IconCatalog.asset");
            Assert.AreEqual(19, ArtWeapons.Length);
            Assert.AreEqual(46, ArtItems.Length);
            var weapons = AssetDatabase.FindAssets("t:WeaponConfig", new[] { "Assets/_Game/V4/Data/Weapons" })
                .Select(g => AssetDatabase.LoadAssetAtPath<WeaponConfig>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            var items = AssetDatabase.FindAssets("t:ItemConfig", new[] { "Assets/_Game/V4/Data/Build/Items" })
                .Select(g => AssetDatabase.LoadAssetAtPath<ItemConfig>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            CollectionAssert.IsSubsetOf(ArtWeapons, weapons.Select(w => w.weaponId).ToList());
            CollectionAssert.IsSubsetOf(ArtItems, items.Select(i => i.itemId).ToList());
            foreach (var w in weapons)
            {
                if (ArtWeapons.Contains(w.weaponId)) Assert.IsNotNull(catalog.Weapon(w.weaponId), w.weaponId + " shows its art icon");
                else
                {
                    Assert.IsNull(catalog.Weapon(w.weaponId), w.weaponId + ": no art (an icon here means new art: add it to ArtWeapons)");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(w.displayName), w.weaponId + ": the name stands in for the icon");
                }
            }
            foreach (var item in items.Where(i => i.inChestPool || ArtItems.Contains(i.itemId)))
            {
                if (ArtItems.Contains(item.itemId)) Assert.IsNotNull(catalog.Item(item.itemId), item.itemId + " shows its art icon");
                else
                {
                    Assert.IsNull(catalog.Item(item.itemId), item.itemId + ": no art (an icon here means new art: add it to ArtItems)");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(item.displayName), item.itemId + ": the name stands in for the icon");
                }
            }
            foreach (var logbook in AllLogbooks())
            {
                if (ArtLogbooks.Contains(logbook.logbookId)) Assert.IsNotNull(catalog.Logbook(logbook.logbookId), logbook.logbookId + " shows its art icon");
                else
                {
                    Assert.IsNull(catalog.Logbook(logbook.logbookId), logbook.logbookId + ": no art (an icon here means new art: add it to ArtLogbooks)");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(logbook.displayName), logbook.logbookId + ": the name stands in for the icon");
                }
            }
        }

        const string IconFolder = "Assets/_Game/V4/Demo/Art/Icons";

        static List<LogbookConfig> AllLogbooks() => AssetDatabase.FindAssets("t:LogbookConfig", new[] { "Assets/_Game/V4/Data/Build/Logbooks" })
            .Select(g => AssetDatabase.LoadAssetAtPath<LogbookConfig>(AssetDatabase.GUIDToAssetPath(g))).ToList();

        /// <summary>
        /// Hackathon (13 Oct): the art team's icon set. Every icon file is named after a content id that exists and is that
        /// id's sprite in V4IconCatalog; every catalog entry names real content and every assigned sprite loads; the files
        /// import as small UI sprites; the art still waiting for the owner (Icons/Unmapped) is in no catalog entry.
        /// </summary>
        [Test]
        public void IconCatalog_TheArtTeamsIconSet_IsWiredByContentId()
        {
            var catalog = IconCatalog.Load();
            Assert.IsNotNull(catalog);
            var weaponIds = AssetDatabase.FindAssets("t:WeaponConfig", new[] { "Assets/_Game/V4/Data/Weapons" })
                .Select(g => AssetDatabase.LoadAssetAtPath<WeaponConfig>(AssetDatabase.GUIDToAssetPath(g)).weaponId).ToList();
            var itemIds = AssetDatabase.FindAssets("t:ItemConfig", new[] { "Assets/_Game/V4/Data/Build/Items" })
                .Select(g => AssetDatabase.LoadAssetAtPath<ItemConfig>(AssetDatabase.GUIDToAssetPath(g)).itemId).ToList();
            var logbookIds = AllLogbooks().Select(l => l.logbookId).ToList();
            var kinds = new (string folder, List<string> ids, List<IconCatalog.Entry> entries, System.Func<string, Sprite> get)[]
            {
                ("Weapons", weaponIds, catalog.weapons, catalog.Weapon),
                ("Items", itemIds, catalog.items, catalog.Item),
                ("Logbooks", logbookIds, catalog.logbooks, catalog.Logbook),
            };
            var files = 0;
            foreach (var (folder, ids, entries, get) in kinds)
            {
                // Every content id has one catalog entry, and every entry names real content.
                CollectionAssert.AreEquivalent(ids, entries.Select(e => e.id).ToList(), folder + ": one catalog entry per content id");
                foreach (var e in entries)
                    if (e.sprite != null) Assert.IsTrue(AssetDatabase.Contains(e.sprite), folder + "/" + e.id + ": the sprite is a project asset");
                foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { IconFolder + "/" + folder }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var id = System.IO.Path.GetFileNameWithoutExtension(path);
                    files++;
                    CollectionAssert.Contains(ids, id, path + " is named after a content id");
                    Assert.AreSame(AssetDatabase.LoadAssetAtPath<Sprite>(path), get(id), path + " is the catalog's icon for " + id);
                }
            }
            // 13 Oct, owner's mapping: + siren_bell, mortar (the newer icon replaces New_items' mortar(1)) and squid_ink_pots
            // (the Kraken Ink Sac icon for now); + chain_locker, cuttlefish_bone; + admiral, extra_hands.
            Assert.AreEqual(10 + 27 + 15, files, "10 weapon icons (9 more are New_items'), 27 items, 15 Logbooks");
            Assert.IsNull(catalog.Item("cursed_chest_key"), "the Cursed Chest Key is not the Skeleton Key: no icon (owner, 13 Oct)");
            Assert.IsTrue(catalog.Weapon("mortar") != null && AssetDatabase.GetAssetPath(catalog.Weapon("mortar")) == IconFolder + "/Weapons/mortar.png",
                "the newer Mortar icon replaces mortar(1)");
            // Boarding Party, Grapeshot and Ramming Prow are old names of Monkey Buccaneer, Chainshot and Squid Ink Pots: unused.
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + "/Unmapped/Weapons/ramming_prow.png"));
            var all = catalog.weapons.Concat(catalog.items).Concat(catalog.logbooks).Where(e => e.sprite != null)
                .Select(e => AssetDatabase.GetAssetPath(e.sprite)).ToList();
            Assert.IsFalse(all.Any(p => p.StartsWith(IconFolder + "/Unmapped/")), "art waiting for the owner's mapping stays unwired");
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { IconFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, path);
                Assert.IsFalse(importer.mipmapEnabled, path + ": no mipmaps");
                Assert.IsTrue(importer.alphaIsTransparency, path);
                Assert.LessOrEqual(importer.maxTextureSize, 512, path + ": a UI icon, kept small for WebGL");
                Assert.AreEqual(TextureImporterCompression.Compressed, importer.textureCompression, path);
            }
        }

        /// <summary>
        /// 13 Oct (owner, after the hackathon): content whose icon is still being drawn stays in the pools and shows its
        /// name in place of the icon (it was taken out of every pool for the hackathon). Every other offered item has an icon.
        /// </summary>
        internal static readonly string[] IconPending = { "cursed_chest_key" };

        /// <summary>
        /// Everything the game offers (level-up weapons and Logbooks, chest and level-up items, the Rum shop, PvP drafts
        /// and the PvP bot's default draft) has an icon, or is listed as icon-pending and shows its name. Since 13 Oct
        /// (owner) nothing leaves a pool for a missing icon. The owner's renames are in.
        /// </summary>
        [Test]
        public void OfferedContent_AllHasAnIcon_OrIsListedIconPendingAndShowsItsName()
        {
            var catalog = IconCatalog.Load();
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>("Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset"));
            foreach (var w in config.All<WeaponConfig>()) Assert.IsNotNull(catalog.Weapon(w.weaponId), w.weaponId + " is offered and has an icon");
            foreach (var l in config.All<LogbookConfig>().Where(l => l.inSaltwindPool)) Assert.IsNotNull(catalog.Logbook(l.logbookId), l.logbookId + " is offered and has an icon");
            foreach (var i in config.All<ItemConfig>())
            {
                var offered = i.inChestPool || i.inLevelUpPool;
                Assert.IsTrue(offered, i.itemId + " is in a pool (nothing is switched off for a missing icon)");
                if (IconPending.Contains(i.itemId))
                {
                    Assert.IsNull(catalog.Item(i.itemId), i.itemId + " is listed icon-pending but has an icon now: take it off the list");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(i.displayName), i.itemId + " shows its name until its icon exists");
                }
                else Assert.IsNotNull(catalog.Item(i.itemId), i.itemId + " is offered, so it needs an icon (or an IconPending entry)");
                Assert.AreEqual(offered, RoguePirates.V5.Meta.LocalMetaServices.InAPool(config, i.itemId), i.itemId + ": the Rum shop sells only pooled items");
            }
            foreach (var id in Pvp.PvpBotArsenal.DefaultWeapons) Assert.IsNotNull(catalog.Weapon(id), "PvP bot draft: " + id);
            foreach (var id in Pvp.PvpBotArsenal.DefaultLogbooks) Assert.IsNotNull(catalog.Logbook(id), "PvP bot draft: " + id);
            var logbooks = config.All<LogbookConfig>().ToDictionary(l => l.logbookId);
            Assert.AreEqual("Big Game Hunter", logbooks["admiral"].displayName);
            Assert.AreEqual("Echo Arsenal", logbooks["extra_hands"].displayName);
            Assert.AreEqual("Kraken Ink Sack", config.All<ItemConfig>().Single(i => i.itemId == "cuttlefish_bone").displayName);
        }

        /// <summary>Hackathon (13 Oct, owner): an item on a level-up card showed its id ("whale_oil_barrel"); it shows the
        /// item's display name, like weapons and Logbooks.</summary>
        [Test]
        public void LevelUpCard_ForAnItem_ShowsTheItemsDisplayName()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>("Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset"));
            var card = OfferCards.ToCard(new Offer { Kind = OfferKind.NewRunItem, Id = "whale_oil_barrel", IsNew = true }, config);
            Assert.AreEqual("Whale Oil Barrel", card.title);
            Assert.AreEqual("whale_oil_barrel", card.id, "the card still carries the id");
            Assert.AreEqual(Foundation.UI.LevelOfferKind.NewRunItem, card.kind);
            var items = config.All<ItemConfig>().ToList();
            Assert.Greater(items.Count, 0);
            foreach (var item in items.Where(i => !string.IsNullOrWhiteSpace(i.displayName)))
            {
                var c = OfferCards.ToCard(new Offer { Kind = OfferKind.NewRunItem, Id = item.itemId, IsNew = true }, config);
                Assert.AreEqual(item.displayName, c.title, item.itemId);
                Assert.AreEqual(item.description ?? string.Empty, c.description, item.itemId);
            }
            // Unknown content still shows something.
            Assert.AreEqual("no_such_item", OfferCards.ToCard(new Offer { Kind = OfferKind.NewRunItem, Id = "no_such_item" }, config).title);
        }

        [TestCase("Deck Cannons", "Deck Canno…")]
        [TestCase("Long Nine", "Long Nine")]
        [TestCase("Flying Cutlasses", "Flying Cut…")]
        [TestCase("  Mortar ", "Mortar")]
        [TestCase("", "")]
        public void LoadoutLabels_AreCompactedAsV3(string name, string expected) =>
            Assert.AreEqual(expected, LoadoutHudPresenter.CompactLabel(name));

        [Test]
        public void LoadoutRowScale_MatchesV3()
        {
            Assert.AreEqual(0.64f, LoadoutHudPresenter.RowScale(1920f, 6), 1e-4f);
            Assert.AreEqual(Mathf.Clamp((800f - 48f) / (6 * 77.1365f + 5 * 4f + 32f + 4 * 77.1365f + 3 * 4f), 0.42f, 0.64f), LoadoutHudPresenter.RowScale(800f, 6), 1e-4f);
            Assert.AreEqual(0.42f, LoadoutHudPresenter.RowScale(300f, 6), 1e-4f);
        }

        // ---- Weapon effect data and the effects forwarder --------------------------------------------------------

        [Test]
        public void WeaponVisuals_ReproduceV3sWeaponIdChoices()
        {
            var configs = AssetDatabase.FindAssets("t:WeaponConfig", new[] { "Assets/_Game/V4/Data/Weapons" })
                .Select(g => AssetDatabase.LoadAssetAtPath<WeaponConfig>(AssetDatabase.GUIDToAssetPath(g)))
                .ToDictionary(w => w.weaponId);
            // V3 WeaponJuiceController: the cannon muzzle for these ids; Broadside's 1.12 muzzle scale fallback.
            var cannon = new[] { "long_nine", "deck_cannons", "broadside_battery", "swivel_guns", "mortar", "chainshot" }; // V5 M4: Chainshot is a cannon
            foreach (var w in configs.Values)
            {
                Assert.AreEqual(cannon.Contains(w.weaponId), w.visual.cannonMuzzle, w.weaponId + " muzzle");
                Assert.AreEqual(w.weaponId == "broadside_battery" ? 1.12f : 1f, w.visual.fallbackMuzzleScale, 1e-6f, w.weaponId);
            }
            // V3 WeaponTargetingVisualizer's id branches.
            Assert.AreEqual(ConfigEnums.AimShape.PredictedArea, configs["mortar"].visual.aimShape);
            Assert.AreEqual(4f, configs["mortar"].visual.aimMinAreaRadius);
            Assert.AreEqual(ConfigEnums.AimShape.TargetFan, configs["swivel_guns"].visual.aimShape);
            Assert.AreEqual(22f, configs["swivel_guns"].visual.aimTargetFanDegrees);
            Assert.AreEqual(ConfigEnums.AimShape.ForwardFan, configs["long_nine"].visual.aimShape);
            Assert.AreEqual(ConfigEnums.AimShape.StrikeDisc, configs["lightning_mast"].visual.aimShape);
            Assert.AreEqual(0.55f, configs["lightning_mast"].visual.firePulseSeconds);
            Assert.IsTrue(configs["lightning_mast"].visual.firePulseFades);
            Assert.IsTrue(configs["flying_cutlasses"].visual.orbitRingTinted);
            foreach (var w in configs.Values.Where(w => w.weaponId is not ("mortar" or "swivel_guns" or "long_nine" or "lightning_mast")))
                Assert.AreEqual(ConfigEnums.AimShape.ByTargeting, w.visual.aimShape, w.weaponId);
            var rules = AssetDatabase.LoadAssetAtPath<WeaponRulesConfig>("Assets/_Game/V4/Data/Weapons/WeaponRules.asset");
            Assert.AreEqual("powder_kegs", rules.blastVfxBinding);
            Assert.AreEqual("mortar", rules.impactVfxBinding);
        }

        sealed class CountingEffects : MonoBehaviour, IWeaponEffects
        {
            public int Calls;
            public void Fired(string weaponId, Vector3 origin, Vector3 direction) => Calls++;
            public void Aimed(string weaponId, Vector3 aimDirection, Vector3 targetPosition, bool hasTarget) => Calls++;
            public void Beam(Vector3 origin, Vector3 direction, float range, float halfWidth) => Calls++;
            public void SideSalvo(string weaponId, Vector3 shipPosition, Vector3 sideDirection) => Calls++;
            public int MineDropped(Vector3 position) { Calls++; return 7; }
            public void MineRemoved(int handle) => Calls++;
            public void Explosion(Vector3 position, float radius) => Calls++;
            public void LightningStrike(Vector3 position, float radius) => Calls++;
            public void FlameBurst(Vector3 origin, Vector3 aim) => Calls++;
            public void AreaTelegraph(Vector3 position, float radius) => Calls++;
            public void TimedRing(Vector3 position, float radius, float seconds) => Calls++;
            public void AreaImpact(Vector3 position, float radius) => Calls++;
            public void BurningPatch(Vector3 position, float radius) => Calls++;
            public void WaterSplash(Vector3 position) => Calls++;
            public int SkiffSpawned(Vector3 position, Vector3 forward) { Calls++; return 9; }
            public void SkiffMoved(int handle, Vector3 position, Vector3 facing) => Calls++;
            public void SkiffRemoved(int handle) => Calls++;
        }

        [Test]
        public void EffectsForwarder_ReachesThePresenter_AndSkipsWhenItIsGone()
        {
            IWeaponEffects target = null;
            var forwarder = new WeaponEffectsForwarder(() => target);
            Assert.AreEqual(-1, forwarder.MineDropped(Vector3.zero), "no presenter: no view");
            Assert.AreEqual(-1, forwarder.SkiffSpawned(Vector3.zero, Vector3.forward));
            Assert.DoesNotThrow(() => forwarder.Explosion(Vector3.zero, 1f));

            var go = new GameObject("fx");
            var fx = go.AddComponent<CountingEffects>();
            target = fx;
            Assert.AreEqual(7, forwarder.MineDropped(Vector3.zero));
            forwarder.Fired("x", Vector3.zero, Vector3.forward);
            Assert.AreEqual(2, fx.Calls);

            Object.DestroyImmediate(go);
            Assert.AreEqual(-1, forwarder.MineDropped(Vector3.zero), "a destroyed presenter is skipped");
            Assert.DoesNotThrow(() => forwarder.SkiffRemoved(3));
        }

        // ---- Results -----------------------------------------------------------------------------------------------

        sealed class FakeResults : IResultsView
        {
            public bool ObjectiveSecured { get; set; }
            public bool EndedByDeath { get; set; }
            public string ProfileFooter { get; set; } = "Saltwind · seed 101 · 07:12";
            public int GoldEarned { get; set; } = 120;
            public int GoldSpent { get; set; } = 60;
            public int GoldRemaining { get; set; } = 60;
            public bool HasRewardSection { get; set; } = true;
            public string RewardSection { get; set; }
            public string GoalProgress { get; set; } = "Goal: Mortar 12/40\n";
            public List<NamedValue> Build = new List<NamedValue> { new NamedValue { name = "Deck Cannons", value = 4 } };
            public List<NamedValue> Shares = new List<NamedValue> { new NamedValue { name = "Deck Cannons", value = 72.4f }, new NamedValue { name = "Mortar", value = 27.6f } };
            public void CollectFinalBuild(List<NamedValue> into) { into.Clear(); into.AddRange(Build); }
            public void CollectContributions(List<NamedValue> into) { into.Clear(); into.AddRange(Shares); }
        }

        sealed class FakeCommands : IHudCommands
        {
            public int Retries, Menus;
            public void RequestBoost() { }
            public void Retry() => Retries++;
            public void GoToMenu() => Menus++;
            public string SelectNextUnlockGoal() => "Unlock goal: Mortar";
            public string PurchaseUnlockGoal() => "Not enough Rum or already owned.";
        }

        sealed class FakeToasts : IToastSink
        {
            public readonly List<(string text, float seconds)> Shown = new List<(string, float)>();
            public void Show(string message, float seconds) => Shown.Add((message, seconds));
        }

        [Test]
        public void ResultsSummary_ReadsAtAGlance()
        {
            var results = new FakeResults();
            var go = new GameObject("canvas", typeof(Canvas));
            try
            {
                var presenter = ResultsPresenter.Create(go.transform);
                presenter.Bind(new RunViews { Results = results, Commands = new FakeCommands(), Toasts = new FakeToasts() }, null, null);

                // Playtest round 1 layout: outcome, Rum, goal, gold, then each weapon's level and damage share on one line.
                var summary = presenter.BuildSummary();
                StringAssert.StartsWith(SaltwindRunResultsLogic.BuildOutcomeSubline(false, false), summary);
                StringAssert.Contains("\nRUM EARNED  +0  (Rum is paid when the ship sinks)", summary);
                StringAssert.Contains(results.GoalProgress, summary);
                StringAssert.Contains("Gold: earned 120 · spent 60 · left 60", summary);
                StringAssert.Contains("Build:\n  · Deck Cannons Lv 4 · 72% dmg\n  · Mortar · 28% dmg\n", summary);
                StringAssert.EndsWith(results.ProfileFooter + System.Environment.NewLine, summary);

                results.RewardSection = "\nRum +12\n";
                StringAssert.Contains("\nRum +12\nGoal:", presenter.BuildSummary());
                results.HasRewardSection = false;
                StringAssert.DoesNotContain("Rum", presenter.BuildSummary());
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Results_OpenOnRunComplete_AndButtonsForwardCommands()
        {
            var go = new GameObject("canvas", typeof(Canvas));
            try
            {
                var events = new CombatEvents();
                var commands = new FakeCommands();
                var toasts = new FakeToasts();
                var presenter = ResultsPresenter.Create(go.transform);
                presenter.Bind(new RunViews { Results = new FakeResults { ObjectiveSecured = true, EndedByDeath = true }, Commands = commands, Toasts = toasts }, events, null);
                Assert.IsFalse(presenter.IsShowing);

                events.Publish(new CombatEvent(CombatEventKind.RunState, 1, 1f, (int)RunState.PlayerDead));
                events.EndTick();
                Assert.IsFalse(presenter.IsShowing, "only RunComplete opens the screen");

                events.Publish(new CombatEvent(CombatEventKind.RunState, 2, 2f, (int)RunState.RunComplete));
                events.EndTick();
                Assert.IsTrue(presenter.IsShowing);
                Assert.AreEqual(SaltwindRunResultsLogic.BuildHeadline(true, true), presenter.HeadlineText);

                presenter.RetryButton.onClick.Invoke();
                Assert.AreEqual(1, commands.Retries);
                Assert.IsFalse(presenter.IsShowing);
                presenter.MenuButton.onClick.Invoke();
                Assert.AreEqual(1, commands.Menus);
                Assert.AreEqual(1, go.GetComponentsInChildren<ResultsPresenter>(true).Length, "one results screen (V3 had two, R-63)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ---- Spawn warnings ---------------------------------------------------------------------------------------

        sealed class FakeEnemies : IEnemyView
        {
            public readonly Dictionary<int, EnemyTier> Tiers = new Dictionary<int, EnemyTier>();
            public void CollectThreats(List<ThreatMarker> into) => into.Clear();
            public EnemyTier TierOf(int definitionIndex) => Tiers[definitionIndex];
            public bool IsKnownDefinition(int definitionIndex) => Tiers.ContainsKey(definitionIndex);
        }

        [Test]
        public void SpawnWarnings_OnlyForAmbientCaptainAndGeneralSpawns_Throttled()
        {
            var go = new GameObject("canvas", typeof(Canvas));
            try
            {
                var events = new CombatEvents();
                var toasts = new FakeToasts();
                var enemies = new FakeEnemies();
                enemies.Tiers[0] = EnemyTier.Soldier;
                enemies.Tiers[1] = EnemyTier.Captain;
                enemies.Tiers[2] = EnemyTier.General;
                var minimap = MinimapPresenter.Create(go.transform);
                var presenter = SpawnWarningPresenter.Create(go.transform);
                presenter.Bind(new RunViews { Enemies = enemies, Toasts = toasts }, events, minimap);

                void Spawn(int def, bool ambient)
                {
                    events.Publish(new CombatEvent(CombatEventKind.Spawned, 1, 1f, 7, def, 3f, 0f, 4f, "x", ambient ? CombatEventFlags.AmbientSpawn : 0));
                    events.EndTick();
                }

                Spawn(0, true);
                Spawn(1, false);
                Assert.IsEmpty(toasts.Shown, "soldiers and scripted spawns never telegraphed");
                Assert.AreEqual(0, minimap.ActiveSpawnPings);

                Spawn(1, true);
                Assert.AreEqual(1, toasts.Shown.Count);
                Assert.AreEqual(("Captain incoming: watch the horizon", 2.5f), toasts.Shown[0]);
                Assert.AreEqual(1, minimap.ActiveSpawnPings);

                Spawn(2, true);
                Assert.AreEqual(1, toasts.Shown.Count, "at most one warning per 1.5 s");
                Assert.AreEqual(2, minimap.ActiveSpawnPings, "every telegraphed spawn still pings the minimap");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
