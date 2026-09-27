using System.Collections;
using System.Linq;
using HighSeas.Balance;
using HighSeas.Core;
using HighSeas.Encounters;
using HighSeas.Enemies;
using HighSeas.Player;
using HighSeas.POI;
using NUnit.Framework;
using RoguePirates.V4.Events;
using RoguePirates.V4.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoguePirates.V4.Tests
{
    /// <summary>M6: the V4 presenters replace the V3 HUD, minimap, beacon guide, results, spawn warnings and weapon effects.</summary>
    [Category("V4")]
    public sealed class V4HudPlayModeTests
    {
        const float Timeout = 30f;
        V4TestSaveIsolation _isolation;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _isolation = new V4TestSaveIsolation();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.Boot, LoadSceneMode.Single);
            yield return null;
            StageBalanceProfileContext.ResetForTests();
            RunMarksTracker.Reset();
            RunEncounterTracker.ResetForTests();
            SaltwindRunSettlement.ClearLastResults();
            HighSeas.Combat.CombatDebugOverlays.SetVisible(false);
            _isolation?.Dispose();
            _isolation = null;
        }

        static IEnumerator WaitForPlaying()
        {
            var deadline = Time.realtimeSinceStartup + Timeout;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (RunManager.Instance != null && RunManager.Instance.State == RunState.Playing && StageBalanceProfileContext.IsActive
                    && Object.FindFirstObjectByType<V4PresentationRoot>() != null)
                    yield break;
                yield return null;
            }
            Assert.Fail("V4 run did not reach Playing with its presentation root.");
        }

        static string Text(string path)
        {
            var go = GameObject.Find(path);
            Assert.IsNotNull(go, path);
            var tmp = go.GetComponent("TextMeshProUGUI");
            Assert.IsNotNull(tmp, path + " has no TMP text");
            return (string)tmp.GetType().GetProperty("text").GetValue(tmp);
        }

        [UnityTest]
        public IEnumerator Presenters_ReplaceTheV3Views_AndShowTheRun()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return WaitForPlaying();
            var start = Time.realtimeSinceStartup;
            while (RunManager.Instance.ElapsedSeconds < 3f && Time.realtimeSinceStartup - start < 20f) yield return null;
            yield return new WaitForEndOfFrame();

            var root = Object.FindFirstObjectByType<V4PresentationRoot>();
            Assert.IsNotNull(root.Hud, "HUD presenter on the authored canvas");
            Assert.IsNotNull(root.Minimap);
            Assert.IsNotNull(root.BeaconGuide);
            Assert.IsNotNull(root.Results);
            Assert.IsNotNull(root.SpawnWarnings);
            Assert.IsNotNull(root.WeaponEffects);
            Assert.IsNotNull(root.AimOverlay);

            // None of the replaced V3 views exist at run time (not even re-added by a bootstrap).
            Assert.IsEmpty(Object.FindObjectsByType<HighSeas.V3.V3AuthoredGameplayHud>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            Assert.IsEmpty(Object.FindObjectsByType<HighSeas.V3.V3AuthoredBottomLoadoutHud>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            Assert.IsEmpty(Object.FindObjectsByType<HighSeas.V3.V3RunResultsScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            Assert.IsEmpty(Object.FindObjectsByType<HighSeas.V3.V3MinimapHud>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            Assert.IsEmpty(Object.FindObjectsByType<HighSeas.V3.V3BossBeaconGuide>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            Assert.IsEmpty(Object.FindObjectsByType<HighSeas.V3.V3SpawnWarningHud>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            Assert.IsEmpty(Object.FindObjectsByType<HighSeas.V3.V3SpawnEdgeIndicator>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            Assert.IsEmpty(Object.FindObjectsByType<HighSeas.Combat.WeaponTargetingVisualizer>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            var juice = Object.FindFirstObjectByType<HighSeas.VFX.WeaponJuiceController>();
            Assert.IsTrue(juice == null || !juice.enabled, "the juice controller no longer draws weapon effects");
            Assert.IsNotNull(HighSeas.UI.LevelSystem.Instance, "the level system is still the scene's");
            Assert.AreEqual(1, Object.FindObjectsByType<HighSeas.UI.LevelSystem>(FindObjectsSortMode.None).Length, "no second LevelSystem");

            // The HUD shows the run as V3's did.
            var run = RunManager.Instance;
            var elapsed = run.ElapsedSeconds;
            Assert.AreEqual($"{Mathf.FloorToInt(elapsed / 60f):00}:{Mathf.FloorToInt(elapsed % 60f):00}", Text("Canvas/TImer/Text (TMP)"));
            Assert.AreEqual(run.Gold.ToString("N0"), Text("Canvas/Coin_Count/Text (TMP)"));
            Assert.AreEqual($"KILLS {run.KillCount:N0}", Text("Canvas/Kill_Count/Text (TMP)"));
            Assert.AreEqual(Mathf.Max(1, HighSeas.UI.LevelSystem.Instance.Level).ToString(), Text("Canvas/Profile/Level_Count/Text (TMP)"));
            StringAssert.Contains("\nLV1", FirstWeaponLabel(),"first weapon slot shows the starting weapon at level 1");
            Assert.AreEqual("» Find the Treasury Beacon", Text("Canvas/Quest/View/Content/Quest_Slot/Text (TMP)"));
            Assert.AreEqual(HighSeas.V3.V3RunContext.Layout.pois.Count, root.Minimap.PoiMarkerCount, "one minimap glyph per POI");
        }

        static string FirstWeaponLabel()
        {
            var row = GameObject.Find("Canvas/Weapon_Slot").transform;
            var text = row.GetChild(0).Find("Weapon/Lvl_txt").GetComponent("TextMeshProUGUI");
            return (string)text.GetType().GetProperty("text").GetValue(text);
        }

        [UnityTest]
        public IEnumerator Results_OpenOnDeath_RetryReloadsTheV4Run_MenuLoadsTheV4Menu()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return WaitForPlaying();
            yield return DieAndWaitForResults();
            var root = Object.FindFirstObjectByType<V4PresentationRoot>();
            StringAssert.Contains("Run gold earned:", root.Results.SummaryText);
            Assert.IsFalse(GameObject.Find("Canvas").transform.Find("Profile").gameObject.activeSelf, "the profile hides behind the results, as V3");

            root.Results.RetryButton.onClick.Invoke();
            yield return null;
            var deadline = Time.realtimeSinceStartup + Timeout;
            while (Time.realtimeSinceStartup < deadline && root != null) yield return null; // destroyed with the old scene
            yield return WaitForPlaying();
            Assert.AreEqual(V4SceneNames.SaltwindRun, SceneManager.GetActiveScene().name, "Retry reloads the V4 run");

            yield return DieAndWaitForResults();
            Object.FindFirstObjectByType<V4PresentationRoot>().Results.MenuButton.onClick.Invoke();
            deadline = Time.realtimeSinceStartup + Timeout;
            while (Time.realtimeSinceStartup < deadline && SceneManager.GetActiveScene().name != V4SceneNames.MainMenu) yield return null;
            Assert.AreEqual(V4SceneNames.MainMenu, SceneManager.GetActiveScene().name, "Main menu loads the V4 menu");
        }

        static Rect ScreenRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        [UnityTest]
        public IEnumerator PlaytestFixes_BoostMeterAboveTheWeaponRow_ResultsBodyClearOfTheButtons()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return WaitForPlaying();
            yield return null;
            var canvas = GameObject.Find("Canvas").transform;
            var boost = ScreenRect(canvas.Find("Boost_Meter") as RectTransform);
            var row = ScreenRect(canvas.Find("Weapon_Slot") as RectTransform);
            Assert.GreaterOrEqual(boost.yMin, row.yMax - 0.5f, "R-91: the boost meter sits above the weapon row, not on its first slots");

            yield return DieAndWaitForResults();
            var root = Object.FindFirstObjectByType<V4PresentationRoot>();
            var body = GameObject.Find("run_results_body_viewport").GetComponent<RectTransform>();
            var bodyRect = ScreenRect(body);
            foreach (var button in new[] { root.Results.RetryButton, root.Results.MenuButton })
                Assert.GreaterOrEqual(bodyRect.yMin, ScreenRect(button.GetComponent<RectTransform>()).yMax, "R-92: the text area ends above the buttons");
            var goal = GameObject.Find("PLACEHOLDER_v3_ui_run_results_goal") ?? GameObject.Find("run_results_goal");
            if (goal != null) Assert.GreaterOrEqual(bodyRect.yMin, ScreenRect(goal.GetComponent<RectTransform>()).yMax);
            var panel = ScreenRect(body.parent as RectTransform);
            Assert.IsTrue(panel.Contains(bodyRect.min) && panel.Contains(bodyRect.max), "R-92: the text area stays inside the panel");
            Assert.IsNotNull(body.GetComponent<UnityEngine.UI.ScrollRect>(), "long results scroll inside the area");
            StringAssert.Contains("Run gold earned:", root.Results.SummaryText);
        }

        static IEnumerator DieAndWaitForResults()
        {
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            ship.Damage.SetPlaytestInvulnerable(false);
            ship.Damage.TakeDamage(99999f, Vector3.zero, false);
            var deadline = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var root = Object.FindFirstObjectByType<V4PresentationRoot>();
                if (root != null && root.Results != null && root.Results.IsShowing) yield break;
                yield return null;
            }
            Assert.Fail("the results screen did not open after death");
        }

        [UnityTest]
        public IEnumerator LegacyFeeds_PublishBeaconBossLandmarkAndOffers_Once()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return WaitForPlaying();
            var game = Object.FindFirstObjectByType<V4Game>();
            var counts = new System.Collections.Generic.Dictionary<string, int>();
            game.Events.TickPublished += batch =>
            {
                foreach (var e in batch)
                {
                    var key = e.Kind + ":" + e.A;
                    counts[key] = counts.TryGetValue(key, out var n) ? n + 1 : 1;
                }
            };
            Object.FindFirstObjectByType<PlayerShipController>().Damage.SetPlaytestInvulnerable(true);
            yield return null;

            // Beacon discovered, boss summoned, a landmark used, a level-up with cards.
            game.Context.TryGet<RoguePirates.V4.Stage.IStageContent>(out var poi); // M7: V4 stage content
            poi.RevealBeacon();
            var summoned = false;
            for (var i = 0; i < 60 && !summoned; i++) { summoned = poi.TrySummonBoss() && poi.BossSummoned; if (!summoned) yield return null; }
            Assert.IsTrue(summoned, "boss summon failed");
            var layout = poi.Module.Layout;
            // A Repair Wreck used through the stage module (hull damaged first so it has something to repair).
            var landmark = layout.pois.FindIndex(p => !p.used && p.type == HighSeas.Core.POIType.GildedWreck);
            Assert.GreaterOrEqual(landmark, 0, "no Repair Wreck in the layout");
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            ship.Damage.SetPlaytestInvulnerable(false);
            ship.Damage.TakeDamage(ship.Damage.MaxHP * 0.5f, Vector3.zero, false);
            ship.Damage.SetPlaytestInvulnerable(true);
            Assert.IsTrue(poi.Module.Interact(HighSeas.Core.POIType.GildedWreck, landmark), "wreck repaired");
            HighSeas.UI.LevelSystem.Instance.AddXP(500);
            for (var i = 0; i < 20; i++) yield return null;

            int C(string key) => counts.TryGetValue(key, out var n) ? n : 0;
            Assert.AreEqual(1, C($"{CombatEventKind.BeaconState}:3"), "BeaconState discovered, once");
            Assert.AreEqual(1, C($"{CombatEventKind.BossState}:1"), "BossState summoned, once");
            Assert.AreEqual(1, C($"{CombatEventKind.LandmarkUsed}:{landmark}"), "LandmarkUsed for that landmark, once");
            Assert.AreEqual(0, counts.Keys.Count(k => k.StartsWith(CombatEventKind.LandmarkUsed + ":") && k != $"{CombatEventKind.LandmarkUsed}:{landmark}"), "no other landmark reported");
            Assert.GreaterOrEqual(counts.Keys.Where(k => k.StartsWith(CombatEventKind.OffersShown + ":")).Sum(k => counts[k]), 1, "level-up cards shown");
        }

        [UnityTest]
        public IEnumerator Hud_BarsFollowTheShip_AndResultsTelemetryIsWrittenOnce()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return WaitForPlaying();
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            ship.Damage.TakeDamage(ship.Damage.MaxHP * 0.4f, Vector3.zero, false);
            yield return null;
            yield return new WaitForEndOfFrame();
            var hp = GameObject.Find("Canvas/Profile/Health_Slider").GetComponent<UnityEngine.UI.Slider>();
            Assert.AreEqual(Mathf.Clamp01(ship.Damage.CurrentHP / ship.Damage.MaxHP), hp.value, 1e-4f, "hull bar");
            var xp = GameObject.Find("Canvas/Profile/XP_Slider").GetComponent<UnityEngine.UI.Slider>();
            var levels = HighSeas.UI.LevelSystem.Instance;
            Assert.AreEqual(Mathf.Clamp01(levels.CurrentXP / Mathf.Max(1f, levels.XPToNext)), xp.value, 1e-4f, "XP bar");

            yield return new WaitForSeconds(0.6f); // the first hit's invulnerability frames would block the killing blow
            yield return DieAndWaitForResults();
            var telemetry = HighSeas.Combat.RunCombatTelemetry.Instance;
            var records = telemetry == null ? null : (System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>)typeof(HighSeas.Combat.RunCombatTelemetry)
                .GetField("_events", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(telemetry);
            if (records != null && records.Count > 0) // telemetry is on in the run's experiment profile
            {
                var names = records.Select(r => r.TryGetValue("event", out var n) ? n as string : null).ToList();
                Assert.AreEqual(1, names.Count(n => n == "run_results_shown"), "results shown recorded once (V3 recorded it twice)");
            }

            // Retry brings the profile back (it hides behind the results).
            var root = Object.FindFirstObjectByType<V4PresentationRoot>();
            root.Results.RetryButton.onClick.Invoke();
            yield return null;
            var deadline = Time.realtimeSinceStartup + Timeout;
            while (Time.realtimeSinceStartup < deadline && root != null) yield return null;
            yield return WaitForPlaying();
            Assert.IsTrue(GameObject.Find("Canvas").transform.Find("Profile").gameObject.activeSelf, "profile visible in the new run");
        }

        [UnityTest]
        public IEnumerator AmbientSpawns_AreFlaggedForTheSpawnWarnings()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return WaitForPlaying();
            var game = Object.FindFirstObjectByType<V4Game>();
            var ambient = 0;
            game.Events.TickPublished += batch =>
            {
                foreach (var e in batch)
                    if (e.Kind == CombatEventKind.Spawned && (e.Flags & CombatEventFlags.AmbientSpawn) != 0) ambient++;
            };
            var deadline = Time.realtimeSinceStartup + 20f;
            while (ambient == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.Greater(ambient, 0, "ambient spawns carry the ambient flag");
        }

        [UnityTest]
        public IEnumerator WeaponEffects_DrawThroughTheV4Presenter()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return WaitForPlaying();
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            ship.Damage.SetPlaytestInvulnerable(true);
            var record = Object.FindFirstObjectByType<HighSeas.Weapons.WeaponController>();
            record.EnsureWeaponSlotCapacity(6);
            var loadout = new[] { "deck_cannons", "powder_kegs", "flying_cutlasses" };
            record.SetWeaponLoadout(loadout);
            HighSeas.UI.LevelSystem.Instance.SetPlaytestWeaponLoadout(loadout);
            var effects = Object.FindFirstObjectByType<V4PresentationRoot>().WeaponEffects;
            var world = V4TestEnemies.Simulation();

            bool sawTimed = false, sawKeg = false, sawBlades = false;
            var deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline && !(sawTimed && sawKeg && sawBlades))
            {
                if (world.ActiveCount < 6) Surround(world, ship);
                sawTimed |= effects.LiveTimedCount > 0;
                sawKeg |= effects.LiveHandleCount > 0;
                sawBlades |= effects.BladeCount > 0;
                yield return null;
            }
            Assert.IsTrue(sawTimed, "muzzle/blast effects drawn by the V4 presenter");
            Assert.IsTrue(sawKeg, "powder kegs drawn by the V4 presenter");
            Assert.IsTrue(sawBlades, "orbit blades drawn by the V4 presenter");
        }

        static void Surround(RoguePirates.V4.Enemies.EnemySimulation world, PlayerShipController ship)
        {
            var shark = System.Array.FindIndex(world.Definitions, d => d != null && d.id == "shark");
            var forward = ship.MovementForward.sqrMagnitude > 0.01f ? ship.MovementForward.normalized : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, forward);
            foreach (var offset in new[] { forward * 5f, right * 6f, -right * 6f, -forward * 4f, forward * 9f, right * 3f })
                world.Spawn(shark, ship.Position + offset, EliteAffix.None, 0.01f, 50f);
        }
    }
}
