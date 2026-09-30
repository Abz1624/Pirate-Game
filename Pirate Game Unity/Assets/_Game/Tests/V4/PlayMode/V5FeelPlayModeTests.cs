using System.Collections;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Platform;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Foundation.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 feel fixes (owner, 1 Oct 2026): hitting an island bounces the ship back and costs hull; the Captain's special
    /// ability has a round touch button on touch devices.
    /// </summary>
    [Category("V4")]
    public sealed class V5FeelPlayModeTests
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
            VirtualJoystick.ForceTouchControls = false;
            yield return SceneManager.LoadSceneAsync(V4SceneNames.Boot, LoadSceneMode.Single);
            yield return null;
            StageBalanceProfileContext.ResetForTests();
            _isolation?.Dispose();
            _isolation = null;
        }

        static IEnumerator StartRun()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            var deadline = Time.realtimeSinceStartup + Timeout;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (RunManager.Instance != null && RunManager.Instance.State == RunState.Playing && StageBalanceProfileContext.IsActive)
                    yield break;
                yield return null;
            }
            Assert.Fail("V4 run did not reach Playing.");
        }

        /// <summary>Answers any level-up cards (the first level comes within seconds) so the run is live.</summary>
        static IEnumerator Unpause()
        {
            var until = Time.realtimeSinceStartup + 10f;
            while (RunManager.Instance.IsPaused && Time.realtimeSinceStartup < until)
            {
                var cards = Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Hud.LevelUpPresenter>();
                if (cards != null && cards.IsShowing) cards.Pick(0);
                yield return null;
            }
            Assert.IsFalse(RunManager.Instance.IsPaused, "the run is live");
        }

        /// <summary>Waits <paramref name="frames"/> physics frames of live (unpaused) run time, answering level-up cards.</summary>
        static IEnumerator LiveFixedFrames(int frames)
        {
            var live = 0;
            var until = Time.realtimeSinceStartup + 15f;
            while (live < frames && Time.realtimeSinceStartup < until)
            {
                if (RunManager.Instance.IsPaused)
                {
                    var cards = Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Hud.LevelUpPresenter>();
                    if (cards != null && cards.IsShowing) cards.Pick(0);
                    yield return null;
                    continue;
                }
                yield return new WaitForFixedUpdate();
                live++;
            }
        }

        /// <summary>A point off an island's shore, the shore's outward normal, found by casting at the island.</summary>
        static bool FindShore(Vector3 shipPosition, out Vector3 shore, out Vector3 normal)
        {
            shore = normal = Vector3.zero;
            var islands = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
                .Where(c => c.enabled && !c.isTrigger && c.gameObject.layer == IslandObstacles.Layer)
                .OrderBy(c => (c.bounds.center - shipPosition).sqrMagnitude).ToList();
            foreach (var island in islands)
            {
                var center = island.bounds.center;
                var from = center + Vector3.forward * (island.bounds.extents.magnitude + 20f);
                from.y = shipPosition.y;
                var dir = center - from;
                dir.y = 0f;
                if (!Physics.Raycast(from, dir.normalized, out var hit, dir.magnitude + 50f, IslandObstacles.Mask)) continue;
                normal = hit.normal;
                normal.y = 0f;
                if (normal.sqrMagnitude < 0.1f) continue;
                normal.Normalize();
                shore = hit.point;
                return true;
            }
            return false;
        }

        [UnityTest]
        public IEnumerator HittingAnIsland_BouncesBack_AndCostsHull()
        {
            yield return StartRun();
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            Assert.IsTrue(ship.IslandImpact.Enabled, "RunConfig's island impact reaches the ship");
            Assert.IsTrue(FindShore(ship.transform.position, out var shore, out var normal), "an island shore to hit");
            yield return Unpause();
            var hull = ship.Damage.CurrentHP;
            var impacts = ship.IslandImpactCount;
            var bounced = false;
            ship.IslandImpacted += _ => bounced = true;
            ship.PlaceForTest(shore + normal * (ship.IslandCollisionRadius + 3f), -normal * 12f);
            var until = Time.time + 2f;
            while (!bounced && Time.time < until) yield return LiveFixedFrames(1);
            Assert.IsTrue(bounced, "the ship hit the shore");
            Assert.AreEqual(impacts + 1, ship.IslandImpactCount, "one impact");
            // The push fades over the bounce time, outside the steering's limits: still moving off after a few frames.
            yield return LiveFixedFrames(5);
            Assert.Greater(Vector3.Dot(ship.Velocity, normal), 1f, "moving away from the shore: no need to reverse");
            Assert.LessOrEqual(ship.Damage.CurrentHP, hull - ship.IslandImpact.Damage + 0.01f, "the impact cost hull");
        }

        [UnityTest]
        public IEnumerator HoldingIntoAShore_CostsHullAtMostEveryDamageCooldown_ThroughTheIncomingMultiplier()
        {
            yield return StartRun();
            var game = Object.FindFirstObjectByType<V4Game>();
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            // The build's incoming multiplier (Brace!, Reinforced Keel) applies to impacts: halve it for the test.
            Assert.IsTrue(game.Context.TryGet<RoguePirates.V4.Spawning.IPlayerDamageSink>(out var sink));
            ((RoguePirates.V4.Spawning.PlayerDamageService)sink).IncomingDamage = () => 0.5f;
            ship.Damage.SetPlaytestInvulnerable(false);
            Assert.IsTrue(FindShore(ship.transform.position, out var shore, out var normal));
            var settings = ship.IslandImpact;
            var service = (RoguePirates.V4.Spawning.PlayerDamageService)sink;
            var before = service.ImpactDamageTotal;
            var knocks = 0;
            ship.Damage.Damaged += (amount, crit) => knocks++;
            yield return Unpause();
            var damageCount = ship.IslandDamageCount;
            // Drive into the shore again and again for 5 s (a stick held into it).
            var start = Time.time;
            ship.PlaceForTest(shore + normal * (ship.IslandCollisionRadius + 2f), -normal * 12f);
            while (Time.time - start < 5f)
            {
                yield return new WaitForSeconds(0.3f);
                if (RunManager.Instance.IsPaused) yield return Unpause(); // a level-up mid-hold
                ship.PlaceForTest(shore + normal * (ship.IslandCollisionRadius + 2f), -normal * 12f);
                // PlaceForTest resets the cooldowns: keep them, as a held stick would.
                ship.KeepIslandCooldownsForTest();
            }
            var hits = ship.IslandDamageCount - damageCount;
            Assert.GreaterOrEqual(hits, 2, "repeated rams do cost hull");
            Assert.LessOrEqual(hits, Mathf.CeilToInt(5.4f / settings.DamageCooldown) + 1, "at most one hull cost per damage cooldown");
            Assert.AreEqual(hits * settings.Damage * 0.5f, service.ImpactDamageTotal - before, 0.01f, "each knock through the incoming multiplier");
            Assert.GreaterOrEqual(knocks, hits, "each knock raises the hit (feedback, items)");
        }

        [UnityTest]
        public IEnumerator UnderTheParityPreset_IslandsDoNotBounceOrHurt()
        {
            var preset = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../../plan/v4-verification/presets/v3-parity.json"));
            RunRequest.RequestPreset(preset);
            yield return StartRun();
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            Assert.IsFalse(ship.IslandImpact.Enabled, "V3: slide along the shore, no damage");
            Assert.IsTrue(FindShore(ship.transform.position, out var shore, out var normal));
            var hull = ship.Damage.CurrentHP;
            yield return Unpause();
            // Into the shore at an angle: V3 slides along it.
            var tangent = Vector3.Cross(Vector3.up, normal).normalized;
            var from = shore + normal * (ship.IslandCollisionRadius + 1f);
            ship.PlaceForTest(from, (-normal + tangent).normalized * 12f);
            yield return LiveFixedFrames(20);
            Assert.AreEqual(0, ship.IslandImpactCount);
            Assert.AreEqual(0, ship.IslandDamageCount);
            // (How far it slides depends on the random island's shape; what V3 guarantees is no push-off.)
            Assert.LessOrEqual(Vector3.Dot(ship.Velocity, normal), 0.5f, "no push off the shore");
        }

        [UnityTest]
        public IEnumerator AbilityButton_OnTouchDevices_ShowsAndFiresTheActive()
        {
            VirtualJoystick.ForceTouchControls = true;
            yield return StartRun();
            yield return null;
            var root = Object.FindFirstObjectByType<RoguePirates.V4.Presentation.V4PresentationRoot>();
            var button = root.CaptainAbilityButton;
            Assert.IsNotNull(button, "the ability button exists with the Captain HUD");
            yield return null;
            Assert.IsTrue(button.IsShown, "shown on touch devices");
            Assert.IsTrue(button.HasStickArt, "drawn with the touch stick's art");
            Object.FindFirstObjectByType<PlayerShipController>().Damage.SetPlaytestInvulnerable(true);
            yield return Unpause();
            yield return null;
            var captain = Object.FindFirstObjectByType<V4Game>().Build.Captain;
            var uses = captain.ActiveUses;
            var held = 0;
            captain.HeldWithoutTarget += _ => held++;
            // A real touch: the EventSystem's raycast at the button's centre reaches the pad.
            var cam = button.Pad.GetComponentInParent<Canvas>().rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : button.Pad.GetComponentInParent<Canvas>().rootCanvas.worldCamera;
            var centre = RectTransformUtility.WorldToScreenPoint(cam, button.Pad.TransformPoint(button.Pad.rect.center));
            var data = new PointerEventData(EventSystem.current) { position = centre };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            Assert.IsTrue(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<TouchAbilityButton>() != null,
                "the topmost thing under the button's centre is the button: " + (hits.Count > 0 ? hits[0].gameObject.name : "nothing")
                + $" (screen {Screen.width}x{Screen.height}, centre {centre}, pad {button.Pad.position} {button.Pad.rect}, parent {button.transform.parent.name},"
                + $" padRaycast {button.Pad.GetComponentInChildren<UnityEngine.UI.Image>().raycastTarget}, es {EventSystem.current != null}, paused {RunManager.Instance.IsPaused})");
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerDownHandler);
            yield return null;
            yield return null;
            Assert.AreEqual(1, captain.ActiveUses - uses + held, "the press reached the Captain (used, or held with no target)");
            Assert.IsFalse(TouchAbilityButton.ConsumePress(), "a press is read once");

            VirtualJoystick.ForceTouchControls = false;
            yield return null;
            Assert.IsFalse(button.IsShown, "hidden on desktop");
            VirtualJoystick.ForceTouchControls = true;
            yield return null;
            Assert.IsTrue(button.IsShown, "and back again (the presenter keeps running while hidden)");
        }
    }
}
