using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Presentation.Hud;
using RoguePirates.V4.Progression;
using RoguePirates.V4.Pvp;
using RoguePirates.V4.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// Hackathon playtest fixes (12 Oct, owner): the minimap draws only the ship, points of interest and the boss on its own
    /// canvas (b); a goal run ends as a win (c); enemy damage x2 (d); enemy shots hit a ship at rest (e); orbit weapons
    /// spin at a constant world speed whatever the ship's heading (f); elite drops fly to the ship (g); the PvP bot fires
    /// the game's own weapons from its draft (p).
    /// </summary>
    [Category("V4")]
    public sealed class HackathonGameplayFixTests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        readonly List<Object> _owned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _owned) if (o != null) Object.DestroyImmediate(o);
            _owned.Clear();
        }

        // ---- b: minimap --------------------------------------------------------------------------------------------------

        [Test]
        public void Minimap_HasItsOwnCanvas_AndNoEnemyDots()
        {
            var canvas = new GameObject("canvas", typeof(RectTransform), typeof(Canvas));
            _owned.Add(canvas);
            var map = SnapshotMinimapPresenter.Create(canvas.transform);
            Assert.IsNotNull(map.GetComponent<Canvas>(), "a nested canvas: its icon moves never rebuild the HUD canvas");
            var names = map.GetComponentsInChildren<RectTransform>(true).Select(t => t.name).ToList();
            Assert.IsFalse(names.Contains("Enemy"), "no enemy dots");
            Assert.LessOrEqual(names.Count(n => n == "Threat"), 1, "at most the boss's icon");
            Assert.IsTrue(names.Contains("Player"));
        }

        // ---- c: the daily race's goal ends the run ---------------------------------------------------------------------

        sealed class Host : IRunLifecycleHost
        {
            public bool Exists => true;
            public RunState State { get; set; } = RunState.Playing;
            public string RunId => "run";
            public bool ObjectiveSecured => false;
            public void SetState(RunState state) => State = state;
            public void MarkFirstConquestComplete() { }
        }

        [Test]
        public void GoalRun_Completes_Once_AndStopsTheShip()
        {
            var host = new Host();
            var stopped = false;
            var life = new RunLifecycle(host, null, null) { SetShipMovement = on => stopped |= !on };
            life.CompleteGoalRun();
            Assert.AreEqual(RunState.RunComplete, host.State, "the run ends (the results show at RunComplete)");
            Assert.IsTrue(stopped, "the ship stops");
            host.State = RunState.PlayerDead;
            Assert.AreEqual(0, life.CompleteGoalRun(), "an ended run is left alone");
            Assert.AreEqual(RunState.PlayerDead, host.State);
        }

        /// <summary>13 Oct (review): a goal run ends through the normal end path: pending level-ups closed, the end recorded
        /// in the telemetry, and it is reached through the HUD command (no presentation write into the run).</summary>
        [Test]
        public void GoalRun_CancelsPendingLevelUps_RecordsTelemetry_ThroughTheHudCommand()
        {
            var host = new Host();
            var cancelled = 0;
            var telemetry = new RoguePirates.V4.Telemetry.RunTelemetry("test", 6, true, () => 12f, () => 1);
            var life = new RunLifecycle(host, telemetry, null) { CancelPendingLevelUps = () => cancelled++ };
            var commands = new RoguePirates.V4.Views.HudCommands(null, null, null, null, null, goal => life.CompleteGoalRun(goal));
            RoguePirates.V4.Views.IHudCommands contract = commands;
            contract.CompleteGoal("daily_race");
            Assert.AreEqual(RunState.RunComplete, host.State);
            Assert.AreEqual(1, cancelled, "pending level-ups are cancelled, as at a sinking");
            var json = telemetry.ExportJsonSummary();
            StringAssert.Contains("run_goal_complete", json);
            StringAssert.Contains("daily_race", json);
            contract.CompleteGoal("daily_race");
            Assert.AreEqual(1, cancelled, "an ended run is left alone");
            Assert.AreEqual(0, new RoguePirates.V4.Views.HudCommands(null, null, null, null).CompleteGoal("x"), "a 1v1 has no goal end");
        }

        // ---- d: enemy damage x2 ------------------------------------------------------------------------------------------

        [Test]
        public void EnemyDamage_IsDoubled_InTheShippedExperiment()
        {
            var e = AssetDatabase.LoadAssetAtPath<ExperimentConfig>("Assets/_Game/V4/Data/Run/ExperimentConfig.asset");
            Assert.IsNotNull(e);
            Assert.AreEqual(2f, e.enemyDamageMultiplier, 1e-5f, "one multiplier on every enemy's damage (contact, shots, hazards, the boss)");
        }

        // ---- e: enemy shots hit a ship at rest ---------------------------------------------------------------------------

        sealed class Target : IHostileShotTarget
        {
            public Vector3 Pos;
            public int Hits;
            public bool Exists => true;
            public bool IsAlive => true;
            public Vector3 HullPosition => Pos;
            public void TakeDamage(float amount, Vector3 source, bool isCrit) => Hits++;
        }

        [Test]
        public void EnemyShots_HitAShipAtRest_EveryTime_AndStillSpreadOnAMovingShip()
        {
            var shots = new HostileProjectiles(null);
            var target = new Target { Pos = Vector3.zero };
            const float speed = 9f;
            var fired = 0;
            for (var id = 1; id <= 40; id++)
            {
                var angle = id * 37f;
                var distance = 9f + id % 7; // 9..15 m: the ranged enemies' firing band
                var unit = new EnemyUnit { id = id, active = true, position = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance };
                var dir = EnemySimulation.Spread(EnemySimulation.AimLed(unit.position, target.Pos, Vector3.zero, speed, target.Pos - unit.position), unit, Vector3.zero);
                var muzzle = unit.position + dir * 1.6f + Vector3.up * 0.55f;
                Assert.IsTrue(shots.TrySpawn(muzzle, dir, speed, 1f, 1.35f, 0.22f, 0.13f, EnemySimulation.LedShotDistance(muzzle, target.Pos, Vector3.zero, speed)));
                fired++;
                for (var t = 0f; t < 3f && shots.ActiveCount > 0; t += 1f / 60f) shots.Tick(1f / 60f, target);
            }
            Assert.AreEqual(fired, target.Hits, "every shot at a ship at rest hits");

            var spread = 0;
            for (var id = 1; id <= 40; id++)
            {
                var unit = new EnemyUnit { id = id, active = true, position = new Vector3(id, 0f, 12f) };
                var moving = EnemySimulation.Spread(Vector3.forward, unit, new Vector3(6f, 0f, 0f));
                if (Vector3.Angle(Vector3.forward, moving) > 1f) spread++;
                Assert.LessOrEqual(Vector3.Angle(Vector3.forward, moving), EnemySimulation.ShotSpreadDegrees + 1e-3f);
            }
            Assert.Greater(spread, 20, "a moving ship still sees the aim wander (it can dodge)");
        }

        // ---- f: orbit weapons spin in world space ---------------------------------------------------------------------

        sealed class TurningShip : IShipState
        {
            public float Yaw;
            public bool Exists => true;
            public Vector3 Position => Vector3.zero;
            public Vector3 Velocity => Vector3.zero;
            public Vector3 MovementForward => Quaternion.Euler(0f, Yaw, 0f) * Vector3.forward;
            public Vector3 TransformPoint(Vector3 local) => Quaternion.Euler(0f, Yaw, 0f) * local;
        }

        [Test]
        public void OrbitBlades_KeepTheirWorldSpin_WhenTheShipTurns()
        {
            var config = ScriptableObject.CreateInstance<WeaponConfig>();
            _owned.Add(config);
            config.weaponId = "test_orbit";
            config.behavior = ConfigEnums.WeaponBehavior.Orbit;
            config.orbit.baseBlades = 1;
            config.orbit.beamSegments = 0;
            var slot = new WeaponSlot { Config = config, Level = 1 };
            var ship = new TurningShip();
            var orbit = new OrbitBehavior(new WeaponCombat { Ship = ship });
            var slots = new List<WeaponSlot> { slot };

            orbit.ComputeHits(slots, 1f);
            var straight = orbit.Hits[0].Position;
            ship.Yaw = -90f; // turned hard left
            orbit.ComputeHits(slots, 1f);
            var turned = orbit.Hits[0].Position;
            Assert.Less(Vector3.Distance(straight, turned), 1e-3f, "the same moment puts the blade at the same world spot whatever the heading");

            // Turning left at the spin's own rate no longer freezes the blade.
            ship.Yaw = 0f;
            orbit.ComputeHits(slots, 0f);
            var a = orbit.Hits[0].Position;
            ship.Yaw = -config.orbit.angularSpeedDegrees * 0.25f;
            orbit.ComputeHits(slots, 0.25f);
            var b = orbit.Hits[0].Position;
            var swept = Vector3.Angle(new Vector3(a.x, 0f, a.z), new Vector3(b.x, 0f, b.z));
            Assert.AreEqual(config.orbit.angularSpeedDegrees * 0.25f, swept, 0.5f, "a quarter second sweeps a quarter second of spin");
        }

        // ---- g: elite drops fly to the ship -----------------------------------------------------------------------------

        sealed class PowerRng : IPowerupRandom { public float PowerupFloat() => 0.5f; }

        sealed class World : IPowerupWorld
        {
            public bool IsFreeSpot(Vector3 position, float margin, float clearance) => true;
            public bool IsBossFight => false;
            public bool ShipAlive => true;
            public float ShipHull => 50f;
            public float ShipMaxHull => 100f;
            public void HealShip(float amount) { }
            public void SyncShipSpeed() { }
        }

        sealed class Sink : IStatModifierSink
        {
            public void Add(string source, int stat, float additive, float multiplicative = 1f) { }
            public void RemoveSource(string source) { }
            public void SetBase(int stat, float value) { }
        }

        [Test]
        public void ElitePowerups_FlyToTheShip_WithinTheBarrelRadius()
        {
            var c = ScriptableObject.CreateInstance<PowerupConfig>();
            _owned.Add(c);
            var module = new PowerupModule(() => c, new PowerRng(), new Sink(), new World(), null);
            module.ResetForRun();
            var collected = 0;
            module.Collected += _ => collected++;
            Assert.IsTrue(module.DevSpawn(PowerupKind.RepairSupplies, Vector3.zero));
            var ship = new Vector3(c.magnetRadius - 0.5f, 0f, 0f); // inside the pull, well outside the old touch radius
            for (var t = 0f; t < 1.5f && collected == 0; t += 1f / 60f) module.Tick(1f / 60f, ship);
            Assert.AreEqual(1, collected, "it flies over and is collected");

            Assert.IsTrue(module.DevSpawn(PowerupKind.RepairSupplies, Vector3.zero));
            var far = new Vector3(c.magnetRadius + 2f, 0f, 0f);
            for (var t = 0f; t < 1.5f; t += 1f / 60f) module.Tick(1f / 60f, far);
            Assert.AreEqual(1, collected, "outside the pull it waits");
        }

        // ---- p: the PvP bot's weapons ------------------------------------------------------------------------------------

        sealed class Fx : IWeaponEffects
        {
            public readonly List<string> Weapons = new List<string>();
            public void Fired(string weaponId, Vector3 origin, Vector3 direction) => Weapons.Add(weaponId);
            public void Aimed(string weaponId, Vector3 aimDirection, Vector3 targetPosition, bool hasTarget) { }
            public void Beam(Vector3 origin, Vector3 direction, float range, float halfWidth) { }
            public void SideSalvo(string weaponId, Vector3 shipPosition, Vector3 sideDirection) => Weapons.Add(weaponId);
            public int MineDropped(Vector3 position) => 1;
            public void MineRemoved(int handle) { }
            public void Explosion(Vector3 position, float radius) { }
            public void LightningStrike(Vector3 position, float radius) { }
            public void FlameBurst(Vector3 origin, Vector3 aim) { }
            public void AreaTelegraph(Vector3 position, float radius) { }
            public void TimedRing(Vector3 position, float radius, float seconds) { }
            public void AreaImpact(Vector3 position, float radius) { }
            public void BurningPatch(Vector3 position, float radius) { }
            public void WaterSplash(Vector3 position) { }
            public int SkiffSpawned(Vector3 position, Vector3 forward) => 1;
            public void SkiffMoved(int handle, Vector3 position, Vector3 facing) { }
            public void SkiffRemoved(int handle) { }
        }

        [Test]
        public void PvpBot_CarriesOnlyRealWeaponsAndLogbooks_AndFiresThem()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            var weaponIds = new HashSet<string>(config.All<WeaponConfig>().Select(w => w.weaponId));
            var logbookIds = new HashSet<string>(config.All<LogbookConfig>().Select(l => l.logbookId));

            var odd = new PvpBotArsenal(config, 7, new[] { "bot_cannons", "pink_spheres", "deck_cannons" }, new[] { "nope", "gunnery_manual" }, new Fx(), null);
            CollectionAssert.AreEqual(new[] { "deck_cannons" }, odd.WeaponIds, "unknown weapons are dropped");
            CollectionAssert.AreEqual(new[] { "gunnery_manual" }, odd.LogbookIds, "unknown Logbooks are dropped");

            var fx = new Fx();
            var bot = new PvpBotArsenal(config, 7, null, null, fx, null);
            Assert.Greater(bot.WeaponIds.Count, 0, "the default draft has real weapons");
            Assert.IsTrue(bot.WeaponIds.All(weaponIds.Contains), "weapons from the game's list only");
            Assert.IsTrue(bot.LogbookIds.All(logbookIds.Contains), "Logbooks from the game's list only");
            Assert.AreEqual(bot.WeaponIds.Count, bot.Loadout.Count, "every drafted weapon is carried");
            Assert.IsTrue(bot.LogbookIds.All(id => bot.LogbookLevel(id) == 1), "every drafted Logbook at rank 1");

            var hits = new List<string>();
            var dealt = 0f;
            bot.Hit = (dmg, id) => { hits.Add(id); dealt += dmg; };
            bot.ShipPosition = Vector3.zero;
            bot.ShipYaw = 0f;
            bot.TargetPosition = new Vector3(8f, 0f, 6f);
            bot.TargetRadius = 1.6f;
            bot.TargetAlive = true;
            bot.Active = true;
            for (var t = 0f; t < 12f; t += 1f / 60f) bot.Tick(1f / 60f);

            Assert.Greater(fx.Weapons.Count, 0, "it fires (the weapon presenter draws its real weapons)");
            Assert.IsTrue(fx.Weapons.All(bot.WeaponIds.Contains), "only its drafted weapons fire: " + string.Join(",", fx.Weapons.Distinct()));
            Assert.Greater(dealt, 0f, "its weapons hit the ship");
            Assert.IsTrue(hits.All(id => bot.WeaponIds.Contains(id)), "each hit is tagged with the weapon that dealt it: " + string.Join(",", hits.Distinct()));

            var before = bot.Loadout.Sum(w => w.Level) + bot.LogbookIds.Sum(bot.LogbookLevel);
            Assert.IsNotNull(bot.LevelUp());
            Assert.AreEqual(before + 1, bot.Loadout.Sum(w => w.Level) + bot.LogbookIds.Sum(bot.LogbookLevel), "a level-up raises one pick");
            bot.Dispose();
        }
    }
}
