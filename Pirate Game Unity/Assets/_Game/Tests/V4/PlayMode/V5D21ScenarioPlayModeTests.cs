using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.DevTools.Scenarios;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Presentation.Effects;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M8z (7 Oct, D21): the owner's answers checked in the real run scene, one Scenario Lab scenario each
    /// (Assets/_Game/V4/DevTools/Scenarios/Scenarios): the Harpoon's tar slows (Q23), the Squid Ink Pots auto-fire on the
    /// back of the pack with their configured slow (Q19), the Reef Crab and the boss take half of it (Q24), a slow does not
    /// shorten a charge dash (Q25), and the loot barrels have no sparkles (Q16).
    /// </summary>
    [Category("V5")]
    public sealed class V5D21ScenarioPlayModeTests
    {
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
            Assert.IsNull(Scenario.Current, "the scenario ended with its scene");
            StageBalanceProfileContext.ResetForTests();
            _isolation?.Dispose();
            _isolation = null;
        }

        static ScenarioRunner Runner => Scenario.Current;
        static V4Game Game => Runner.Game;
        static PlayerShipController Ship => Runner.Ship;
        static EnemySimulation World => V4TestEnemies.Simulation();

        static IEnumerator Load(string name)
        {
            yield return Scenario.Load(name);
            Assert.IsNotNull(Runner, name + " is loaded");
            CollectionAssert.IsEmpty(Runner.Errors, name + " set up without problems");
        }

        static WeaponConfig Weapon(string id) => Game.Config.All<WeaponConfig>().Single(w => w.weaponId == id);
        static WeaponRulesConfig Rules => Game.Config.All<WeaponRulesConfig>().First();
        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        static bool Slowed(int unit, out int index) => World.TryGetAgentByUnitId(unit, out index) && World.Agents[index].slowTimer > 0f;

        [UnityTest]
        public IEnumerator HarpoonTar_SlowsTheShark_ForTheTarsSeconds()
        {
            yield return Load("harpoon_tar_slow");
            var tar = Weapon("harpoon_gun").projectile;
            var unit = Runner.Spawned.Single().unitId;
            var index = -1;
            var deadline = Time.time + 4f;
            while (!Slowed(unit, out index) && Time.time < deadline) yield return null;
            Assert.IsTrue(Slowed(unit, out index), "a harpoon hit tarred the shark: it is slowed");
            Assert.AreEqual(1f - tar.tarSlow, World.SpeedMultiplier(index), 1e-4f, "by the tar's strength");
            Assert.LessOrEqual(World.Agents[index].slowTimer, tar.tarSlowSeconds + 1e-4f, "for the tar's seconds");
            // It sails slower while tarred: the ground it covers is the slowed share of its own speed.
            var moved = 0f;
            var expected = 0f;
            // Frames between hits (a hit's own knockback pushes it back for a moment).
            var frames = 0;
            for (var f = 0; f < 240 && frames < 12; f++)
            {
                if (!World.TryGetAgentByUnitId(unit, out index)) break;
                var e = World.Agents[index];
                var from = e.position;
                var speed = Flat(e.velocity);
                var clean = e.slowTimer > Time.deltaTime * 2f && e.knockbackTimer <= 0f && e.pushTimer <= 0f;
                yield return null;
                if (!clean || !World.TryGetAgentByUnitId(unit, out index) || World.Agents[index].knockbackTimer > 0f || World.Agents[index].pushTimer > 0f) continue;
                moved += Flat(World.Agents[index].position - from);
                expected += speed * Time.deltaTime;
                frames++;
            }
            Debug.Log($"[V5 D21 tar] moved {moved:0.000} m against {expected:0.000} m at full speed");
            Assert.Greater(expected, 0.05f, "it was measured sailing");
            Assert.AreEqual(1f - tar.tarSlow, moved / expected, 0.12f, "it moves at the slowed share of its speed");
        }

        [UnityTest]
        public IEnumerator InkPots_AutoFire_OnTheBackOfThePack_WithTheirSlow()
        {
            yield return Load("ink_pots_rear_of_pack");
            var w = Weapon("squid_ink_pots");
            var p = w.predictedArea;
            Assert.AreEqual(1, Game.Build.Progression.OwnedWeapons.Count);
            Assert.AreEqual("squid_ink_pots", Game.Build.Progression.OwnedWeapons[0]);
            var units = Runner.Spawned.Select(s => s.unitId).ToList(); // spawned at 8, 11, 14, 17 and 20 m
            Assert.AreEqual(5, units.Count);
            var deadline = Time.time + p.impactDelaySeconds + 3f;
            while (!units.Any(u => Slowed(u, out _)) && Time.time < deadline) yield return null; // no input: the weapon fires on its own
            var slowed = units.Where(u => Slowed(u, out _)).ToList();
            Assert.IsNotEmpty(slowed, "the pots fired with no input and the slick slowed someone");
            var distance = units.ToDictionary(u => u, u => World.TryGetAgentByUnitId(u, out var i) ? Flat(World.Agents[i].position - Ship.Position) : -1f);
            Debug.Log("[V5 D21 ink] " + string.Join(", ", units.Select(u => $"{distance[u]:0.0} m{(slowed.Contains(u) ? " slowed" : "")}")));
            Assert.Contains(units[4], slowed, "the rear shark (spawned at 20 m) is slowed");
            Assert.IsFalse(slowed.Contains(units[0]) || slowed.Contains(units[1]), "the front of the pack (8 and 11 m) is not");
            var nearestSlowed = slowed.Min(u => distance[u]);
            Assert.IsTrue(units.Where(u => !slowed.Contains(u) && distance[u] >= 0f).All(u => distance[u] < nearestSlowed), "every slowed shark is behind every unslowed one");
            foreach (var u in slowed)
            {
                Assert.IsTrue(World.TryGetAgentByUnitId(u, out var i));
                Assert.AreEqual(1f - p.slowFraction, World.SpeedMultiplier(i), 1e-4f, "the configured slow");
                Assert.LessOrEqual(World.Agents[i].slowTimer, p.slowSeconds + 1e-4f, "for its configured seconds");
            }
        }

        static IEnumerator HalfSlowOn(int unit)
        {
            var p = Weapon("squid_ink_pots").predictedArea;
            var index = -1;
            var deadline = Time.time + 8f;
            while (!Slowed(unit, out index) && Time.time < deadline) yield return null;
            if (!Slowed(unit, out index))
            {
                var found = World.TryGetAgentByUnitId(unit, out index);
                Debug.Log($"[V5 D21 half] unit {unit} found {found} distance {(found ? Flat(World.Agents[index].position - Ship.Position) : -1f):0.0} m remote {(found && World.Agents[index].remote)}; weapons {string.Join(",", Game.Build.Progression.OwnedWeapons)}");
            }
            Assert.IsTrue(Slowed(unit, out index), "the ink reached it");
            var expected = Mathf.Min(Rules.bossMaxSlow, p.slowFraction * Rules.bossSlowShare);
            Assert.AreEqual(1f - expected, World.SpeedMultiplier(index), 1e-4f, "half the ink's slow, at most the boss cap");
            Assert.Less(expected, p.slowFraction, "less than an ordinary enemy takes");
        }

        [UnityTest]
        public IEnumerator InkPots_SlowTheReefCrab_ByHalf()
        {
            yield return Load("ink_pots_vs_crab");
            yield return HalfSlowOn(Runner.Spawned.Single().unitId);
        }

        [UnityTest]
        public IEnumerator InkPots_SlowTheBoss_ByHalf()
        {
            yield return Load("ink_pots_vs_boss");
            var deadline = Time.time + 12f;
            while (!World.TryFindActiveBoss(out _, out _) && Time.time < deadline) yield return null;
            Assert.IsTrue(World.TryFindActiveBoss(out _, out var boss), "the boss rose");
            // It rises at the Beacon's whirlpool, out of the pots' 22 m: the ship sails up to 14 m from it.
            var away = Ship.Position - boss.position;
            away.y = 0f;
            var place = boss.position + (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.back) * 14f;
            place.y = Ship.transform.position.y;
            Ship.transform.position = place;
            var body = Ship.GetComponent<Rigidbody>();
            if (body != null) { body.position = place; body.linearVelocity = Vector3.zero; }
            yield return HalfSlowOn(boss.id);
        }

        [UnityTest]
        public IEnumerator ASlow_DoesNotShortenAChargeDash()
        {
            yield return Load("dash_under_slow");
            var unit = Runner.Spawned.Single().unitId;
            bool Dashing(out int i) => World.TryGetAgentByUnitId(unit, out i) && World.CombatStates.TryGet(unit, out var s) && s.phase == EnemyCombatPhase.Execute;
            var deadline = Time.time + 15f;
            int index;
            while (!Dashing(out index) && Time.time < deadline) yield return null;
            Assert.IsTrue(Dashing(out index), "the hammerhead committed to its charge");
            World.ApplySlow(index, 0.5f, 5f);
            Assert.AreEqual(0.5f, World.SpeedMultiplier(index), 1e-4f, "it carries a 50% slow");
            var moved = 0f;
            var full = 0f;
            for (var f = 0; f < 6 && Dashing(out index); f++)
            {
                var from = World.Agents[index].position;
                var speed = Flat(World.Agents[index].velocity);
                yield return null;
                if (!Dashing(out index)) break;
                moved += Flat(World.Agents[index].position - from);
                full += speed * Time.deltaTime;
            }
            Debug.Log($"[V5 D21 dash] slowed dash moved {moved:0.000} m against {full:0.000} m at full speed");
            Assert.Greater(full, 0.05f, "it dashed while measured");
            Assert.Greater(moved / full, 0.85f, "the dash keeps its reach under the slow (D21, Q25)");
        }

        [UnityTest]
        public IEnumerator LootBarrels_HaveNoSparkles()
        {
            yield return Load("loot_barrels");
            var pickups = Game.Build.Pickups;
            var at = Ship.Position + Vector3.forward * 12f;
            pickups.Spawn(PickupType.GoldBarrel, 20, at);
            pickups.Spawn(PickupType.XpBarrel, 5, at + Vector3.right * 3f);
            pickups.Spawn(PickupType.Powerup, 10, at + Vector3.left * 3f); // the health barrel
            yield return null;
            yield return new WaitForSeconds(0.5f);
            var barrels = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.gameObject.activeInHierarchy && t.name.StartsWith(PickupPresenter.KeyPrefix) && t.name.Contains("Barrel")).ToList();
            Debug.Log("[V5 D21 barrels] " + string.Join(", ", barrels.Select(b => $"{b.name}: {PickupPresenter.ActiveParticleSystems(b)} particle systems")));
            Assert.IsTrue(barrels.Any(b => b.name.Contains("LootBarrel")), "a gold barrel is on the water");
            Assert.IsTrue(barrels.Any(b => b.name.Contains("HealthBarrel")), "a health barrel is on the water");
            foreach (var b in barrels) Assert.AreEqual(0, PickupPresenter.ActiveParticleSystems(b), b.name + " has no sparkles");
        }
    }
}
