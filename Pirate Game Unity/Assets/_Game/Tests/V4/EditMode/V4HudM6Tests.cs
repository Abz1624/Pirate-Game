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

        [Test]
        public void IconCatalog_HasAnIconForEveryWeaponAndChestItem()
        {
            var catalog = IconCatalog.Load();
            Assert.IsNotNull(catalog, "Assets/_Game/V4/Resources/V4IconCatalog.asset");
            foreach (var guid in AssetDatabase.FindAssets("t:WeaponConfig", new[] { "Assets/_Game/V4/Data/Weapons" }))
            {
                var config = AssetDatabase.LoadAssetAtPath<WeaponConfig>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsNotNull(catalog.Weapon(config.weaponId), $"{config.weaponId} has no icon");
            }
            foreach (var guid in AssetDatabase.FindAssets("t:ItemConfig", new[] { "Assets/_Game/V4/Data/Build/Items" }))
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (item.inChestPool) Assert.IsNotNull(catalog.Item(item.itemId), $"{item.itemId} has no icon (R-90: V3 missed three apostrophe names)");
            }
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
            var cannon = new[] { "long_nine", "deck_cannons", "broadside_battery", "swivel_guns", "mortar" };
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
