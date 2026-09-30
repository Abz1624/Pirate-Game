using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Spawning;
using RoguePirates.V4.Weapons;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    [Category("V4")]
    public sealed class V4WeaponM5Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";

        // ---- Fakes ----------------------------------------------------------------------------------------------

        sealed class Enemy
        {
            public bool Active = true;
            public Vector3 Position, Velocity;
            public float Radius = 0.8f, Hp = 1000f;
            public bool Elite, Boss;
        }

        sealed class FakeEnemies : IEnemyQuery, IEnemyDamageSink
        {
            public readonly List<Enemy> List = new List<Enemy>();
            public readonly List<(int index, float amount, bool crit, string weapon)> Damage = new List<(int, float, bool, string)>();
            public readonly List<(int index, WeaponStatus status)> Statuses = new List<(int, WeaponStatus)>();
            public int Add(Vector3 position, float hp = 1000f) { List.Add(new Enemy { Position = position, Hp = hp }); return List.Count - 1; }
            public bool Exists => true;
            public int Capacity => List.Count;
            public bool IsActive(int i) => List[i].Active;
            public Vector3 Position(int i) => List[i].Position;
            public Vector3 Velocity(int i) => List[i].Velocity;
            public float Radius(int i) => List[i].Radius;
            public float Hp(int i) => List[i].Hp;
            public bool IsElite(int i) => List[i].Elite;
            public bool IsBoss(int i) => List[i].Boss;
            public bool TryGetNearest(Vector3 from, float range, out int index, bool preferElite = false)
            {
                index = -1;
                var best = float.MaxValue;
                for (var i = 0; i < List.Count; i++)
                {
                    if (!List[i].Active) continue;
                    var d = Vector3.Distance(from, List[i].Position);
                    if (d > range || d >= best) continue;
                    best = d;
                    index = i;
                }
                return index >= 0;
            }
            public void ApplyDamage(int index, float amount, bool isCrit, bool showHitFeedback = true, string weaponId = null)
            {
                Damage.Add((index, amount, isCrit, weaponId));
                List[index].Hp -= amount;
                if (List[index].Hp <= 0f) List[index].Active = false;
            }
            public void ApplyStatus(int index, WeaponStatus status, float strength) => Statuses.Add((index, status));
        }

        sealed class FakeShip : IShipState
        {
            public Vector3 Pos, Vel, Forward = Vector3.forward;
            public bool Exists => true;
            public Vector3 Position => Pos;
            public Vector3 Velocity => Vel;
            public Vector3 MovementForward => Forward;
            public Vector3 TransformPoint(Vector3 local) => Pos + Quaternion.LookRotation(Forward) * local;
        }

        sealed class FakeMods : IWeaponModifiers
        {
            public float AttackSpeed { get; set; } = 1f;
            public float ProjectileSpeed { get; set; } = 1f;
            public float Size { get; set; } = 1f;
            public float Duration { get; set; } = 1f;
            public float Quantity { get; set; }
            public float CritChance { get; set; } = 0.05f;
            public float CritDamage { get; set; } = 1.5f;
            public float EliteDamage { get; set; } = 1f;
            public float GlobalDamage { get; set; } = 1f;
            public float CaptainAdditiveDamage { get; set; }
            public float CaptainWeaponMultiplier(string weaponId) => 1f;
            public float ItemGlobalDamageMultiplier => 1f;
            public float ItemCritChanceBonus => 0f;
            public float ItemAttackSpeedMultiplier { get; set; } = 1f;
            public float FlameRangeMultiplier => 1f;
            public float BurnDamageMultiplier => 1f;
            public float PowderKegBlastRadiusMultiplier => 1f;
            public float ExplosionDamageMultiplier { get; set; } = 1f;
            public int ExplosionsResolved;
            public void ExplosionResolved() => ExplosionsResolved++;
            public int ExtraLightningJumps { get; set; }
        }

        sealed class FakeHooks : IWeaponCombatHooks
        {
            int _procs;
            public readonly List<string> Emissions = new List<string>();
            public int Hits;
            public void BeginDamageResolution() => _procs = 0;
            public bool TryRegisterProcEvent() { if (_procs >= 12) return false; _procs++; return true; }
            public void RecordHit(string weaponId, float damage, bool isCrit, bool isElite, bool isBoss) => Hits++;
            public void RecordEmission(string weaponId) => Emissions.Add(weaponId);
            public void RecordConcurrentTargets(string weaponId, int count) { }
            public void RecordControl(string weaponId, float seconds) { }
            public readonly List<Vector3> FlamePatches = new List<Vector3>();
            public void SpawnFlamePatch(Vector3 position, string weaponId) => FlamePatches.Add(position);
            public void ApplyShipRadiusDamage(Vector3 center, float radius, float damage, string weaponId) { }
            public bool TryResolveShipAim(string weaponId, out Vector3 direction) { direction = Vector3.forward; return false; }
            public bool ShipVersusShip => false;
            public bool IsPvP => false;
            public bool AutoFireDisabled => false;
        }

        sealed class FakeRandom : ICombatRandom
        {
            readonly System.Random _r = new System.Random(7);
            public int Draws;
            public float CombatFloat() { Draws++; return (float)_r.NextDouble(); }
        }

        sealed class FakeRun : IRunStatus
        {
            public bool Exists => true;
            public float ElapsedSeconds => 10f;
            public bool IsPaused => false;
            public bool IsRunEnded => false;
            public bool IsGameplayActive => true;
            public bool IsBossFight => false;
            public int PlayerLevel => 1;
            public int Kills => 0;
        }

        sealed class Entry { public object Key = new object(); public string Id; public int Level = 1; public float DamageBonus = 1f; public int Quantity; public string Evolution; public LoadoutMirror Mirror; }

        sealed class FakeLoadout : IWeaponLoadout
        {
            public readonly List<Entry> Entries = new List<Entry>();
            public int Count => Entries.Count;
            public LoadoutEntry Get(int i)
            {
                var e = Entries[i];
                return new LoadoutEntry { Key = e.Key, WeaponId = e.Id, EvolutionId = e.Evolution, Level = e.Level, DamageBonus = e.DamageBonus, QuantityBonus = e.Quantity,
                    CooldownMult = 1f, SpeedMult = 1f, SizeMult = 1f, DurationMult = 1f, KnockbackMult = 1f };
            }
            public void Mirror(int i, in LoadoutMirror state) => Entries[i].Mirror = state;
        }

        sealed class Harness : System.IDisposable
        {
            public ConfigSetRuntime Config;
            public FakeEnemies Enemies = new FakeEnemies();
            public FakeShip Ship = new FakeShip();
            public FakeMods Mods = new FakeMods();
            public FakeHooks Hooks = new FakeHooks();
            public FakeRandom Random = new FakeRandom();
            public FakeLoadout Loadout = new FakeLoadout();
            public WeaponCombat Combat;
            public WeaponManager Manager;

            public Harness(params string[] weaponIds) : this(false, weaponIds) { }

            /// <summary>v3Parity: the v3-parity preset applied (every approved fix off: V3's behavior).</summary>
            public Harness(bool v3Parity, params string[] weaponIds)
            {
                Config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
                if (v3Parity)
                {
                    var errors = new List<string>();
                    var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "..", "plan", "v4-verification", "presets", "v3-parity.json"));
                    Assert.IsTrue(PresetCodec.Apply(Config, PresetCodec.FromJson(System.IO.File.ReadAllText(path)), 0f, errors), string.Join("\n", errors));
                }
                Combat = new WeaponCombat
                {
                    Enemies = Enemies, Damage = Enemies, Ship = Ship, Mods = Mods, Hooks = Hooks, Random = Random,
                    Calculator = new DamageCalculator(Mods, Random),
                    Targeting = new WeaponTargetingService(Enemies, Ship),
                    Projectiles = new PlayerProjectiles(64, Enemies, Enemies, Hooks, null),
                    Rules = () => Config.All<WeaponRulesConfig>().First(),
                };
                Manager = new WeaponManager(Combat, Loadout, new FakeRun(), Config);
                foreach (var id in weaponIds) Loadout.Entries.Add(new Entry { Id = id });
            }

            public void Tick(int frames = 1, float dt = 1f / 60f)
            {
                for (var i = 0; i < frames; i++)
                {
                    Manager.Tick(dt);
                    Combat.Projectiles.Tick(dt);
                    Manager.TickOrbitPositions(i * dt);
                }
            }

            public WeaponSlot Slot(string id) => Manager.Slots.First(s => s.Id == id);
            public void Dispose() => Config.Dispose();
        }

        // ---- Data ------------------------------------------------------------------------------------------------

        [Test]
        public void EveryWeapon_HasItsV3Behavior_AndConstants()
        {
            using var h = new Harness(true);
            var w = h.Config.All<WeaponConfig>().ToDictionary(c => c.weaponId);
            Assert.AreEqual(ConfigEnums.WeaponBehavior.Projectile, w["deck_cannons"].behavior);
            Assert.AreEqual(ConfigEnums.WeaponBehavior.Fan, w["broadside_battery"].behavior);
            Assert.AreEqual(ConfigEnums.WeaponBehavior.Orbit, w["flying_cutlasses"].behavior);
            Assert.AreEqual(ConfigEnums.WeaponBehavior.ConeStream, w["flamethrower"].behavior);
            Assert.AreEqual(ConfigEnums.WeaponBehavior.Mine, w["powder_kegs"].behavior);
            Assert.AreEqual(ConfigEnums.WeaponBehavior.AreaStrike, w["lightning_mast"].behavior);
            Assert.AreEqual(ConfigEnums.WeaponBehavior.PredictedArea, w["mortar"].behavior);
            var swivel = w["swivel_guns"].projectile;
            // Demo (30 Sep 2026): shot lifetime cut to the new range (12 m x 1.15 / 16 m/s; was 2.4 s).
            Assert.AreEqual((2, 0.18f, 0.55f, 0.55f, 0.32f, 0.86f), (swivel.burstShots, swivel.burstGapSeconds, swivel.damageMultiplier, swivel.sizeMultiplier, swivel.minSize, swivel.lifeSeconds));
            Assert.IsTrue(swivel.closeDefenseTargeting && swivel.homing && !swivel.leadWithActualSpeed);
            Assert.IsTrue(w["long_nine"].lane.enabled && w["long_nine"].lane.beam);
            Assert.IsTrue(w["harpoon_gun"].projectile.tarOnHit);
            var fan = w["broadside_battery"].fan;
            // Demo: pellet lifetime cut to the new range (12 m x 1.15 / 42 m/s; was 1.2 s).
            Assert.AreEqual((false, 42f, 1.05f, 0.33f, false, false, false), (fan.quantityAddsPellets, fan.minSpeed, fan.hitRadius, fan.lifeSeconds, fan.muzzlePerPellet, fan.trackPelletTargets, fan.splashOnDespawn));
            // R-21: the level damage factor only where V3 applied it.
            CollectionAssert.AreEquivalent(new[] { "deck_cannons", "harpoon_gun", "long_nine", "swivel_guns" }, w.Values.Where(c => c.levelDamageFactor).Select(c => c.weaponId));
        }

        [Test]
        public void Evolutions_AreConfig_WithTheirBaseWeaponAndEffect()
        {
            using var h = new Harness();
            var e = h.Config.All<WeaponEvolutionConfig>().ToDictionary(c => c.evolutionId);
            Assert.AreEqual(6, e.Count);
            // M16: V3's evolution catalog is deleted; its six rows, as they were.
            var rows = new[]
            {
                ("stormshot_cannon", "deck_cannons", "grapeshot_pouch"), ("kings_broadside", "broadside_battery", "broadside_drum"),
                ("devils_wake", "powder_kegs", "double_fuse_cord"), ("bladed_tide", "flying_cutlasses", "serrated_scabbard"),
                ("hellfire_nozzle", "flamethrower", "dragons_breath_nozzle"), ("tempest_mast", "lightning_mast", "copper_conductor"),
            };
            foreach (var (evolution, weapon, item) in rows)
            {
                Assert.AreEqual(weapon, e[evolution].baseWeapon.weaponId, evolution);
                Assert.AreEqual(item, e[evolution].requiredItemId, evolution);
                Assert.AreNotEqual(ConfigEnums.EvolutionEffect.None, e[evolution].effect);
            }
        }

        // ---- Cooldowns and bursts ----------------------------------------------------------------------------------

        [Test]
        public void TargetedWeapons_KeepTheirCooldown_WithoutATarget()
        {
            foreach (var id in new[] { "deck_cannons", "swivel_guns", "long_nine", "harpoon_gun", "broadside_battery", "lightning_mast", "mortar" })
            {
                using var h = new Harness(id);
                h.Tick(30);
                Assert.IsEmpty(h.Hooks.Emissions, id + " emitted with no enemy");
                Assert.LessOrEqual(h.Slot(id).CooldownTimer, 0f, id + " spent its cooldown with no target");
                // Broadsides only fire to port or starboard.
                var side = h.Config.All<WeaponConfig>().First(c => c.weaponId == id).targeting == ConfigEnums.TargetingMode.PortStarboard;
                h.Enemies.Add(side ? new Vector3(6f, 0f, 0f) : new Vector3(0f, 0f, 6f));
                h.Tick(1);
                Assert.IsNotEmpty(h.Hooks.Emissions, id + " fires once an enemy is in range");
            }
        }

        [Test]
        public void SwivelGuns_UseTheFullCooldownBetweenShots_AsV3Did()
        {
            using var h = new Harness(true, "swivel_guns");
            h.Enemies.Add(new Vector3(0f, 0f, 5f));
            h.Tick(1);
            var slot = h.Slot("swivel_guns");
            Assert.AreEqual(1, h.Hooks.Emissions.Count);
            Assert.AreEqual(1, slot.BurstShotsLeft, "the second shot is pending");
            var cooldown = WeaponStatResolver.Cooldown(slot, h.Mods, 1f);
            Assert.AreEqual(cooldown, slot.CooldownTimer, 1e-5f, "V3 overwrote the 0.18 s burst gap with the full cooldown (register R-87)");
            h.Tick(Mathf.CeilToInt(cooldown * 60f) + 1);
            Assert.AreEqual(2, h.Hooks.Emissions.Count);
            Assert.AreEqual(0, slot.BurstShotsLeft);
            Assert.IsFalse(h.Loadout.Entries[0].Mirror.AwaitingBurstShot, "mirrored to the record");
        }

        // ---- Pattern specifics --------------------------------------------------------------------------------------

        [Test]
        public void PowderKeg_ArmsTriggers_AndItsBlastHitsAtMostTwelve()
        {
            using var h = new Harness("powder_kegs");
            h.Ship.Forward = Vector3.forward;
            h.Tick(1);
            Assert.AreEqual(1, h.Combat.Kegs.Count, "a keg astern");
            var keg = h.Combat.Kegs[0].Position;
            for (var i = 0; i < 20; i++) h.Enemies.Add(keg + new Vector3(0.1f * i, 0f, 0f));
            h.Tick(30);
            Assert.AreEqual(12, h.Enemies.Damage.Count(d => d.weapon == "powder_kegs"), "V3's proc budget: 12 hits per blast");
            Assert.IsTrue(h.Enemies.Damage.All(d => !d.crit), "triggered damage never crits (no draw)");
        }

        [Test]
        public void Mortar_Impact_MakesV3sEffectDraws()
        {
            using var h = new Harness(true, "mortar");
            h.Enemies.Add(new Vector3(0f, 0f, 10f));
            h.Tick(1);
            Assert.AreEqual(1, h.Combat.Strikes.Count);
            var before = h.Random.Draws;
            h.Tick(40);
            var draws = h.Random.Draws - before;
            // The same stream, replayed: one crit draw for the enemy hit, then V3's effect draws, 1 + 2 × (4 + ⌊r × 3⌋) (R-70).
            var replay = new System.Random(7);
            for (var i = 0; i < before + 1; i++) replay.NextDouble();
            var bursts = 4 + Mathf.FloorToInt((float)replay.NextDouble() * 3f);
            Assert.AreEqual(1 + 1 + 2 * bursts, draws);
        }

        [Test]
        public void FlameStream_StartsOnlyWithAnEnemyInTheCone_ThenRecharges()
        {
            using var h = new Harness("flamethrower");
            h.Enemies.Add(new Vector3(0f, 0f, -6f)); // astern
            h.Tick(10);
            Assert.IsEmpty(h.Hooks.Emissions);
            h.Enemies.List[0].Position = new Vector3(0f, 0f, 5f);
            h.Tick(1);
            var slot = h.Slot("flamethrower");
            Assert.AreEqual(1, h.Hooks.Emissions.Count);
            Assert.Greater(slot.StreamRemaining, 0f);
            h.Tick(Mathf.CeilToInt(h.Config.All<WeaponConfig>().First(c => c.weaponId == "flamethrower").stream.streamSeconds * 60f) + 2);
            Assert.AreEqual(0f, slot.StreamRemaining);
            Assert.Greater(slot.RechargeRemaining, 0f, "then recharges");
            Assert.Greater(h.Enemies.Statuses.Count(s => s.status == WeaponStatus.Burn), 0);
        }

        [Test]
        public void OrbitBlades_FollowTheShip_AndHitOnTheirInterval()
        {
            using var h = new Harness("flying_cutlasses");
            h.Loadout.Entries[0].Quantity = 1;
            h.Manager.SyncLoadout();
            h.Manager.TickOrbitPositions(0f);
            Assert.AreEqual(3, h.Manager.Orbit.Hits.Count, "2 + quantity bonus blades");
            var radius = h.Config.All<WeaponConfig>().First(c => c.weaponId == "flying_cutlasses").orbit.orbitRadius;
            foreach (var blade in h.Manager.Orbit.Hits)
                Assert.AreEqual(radius, new Vector2(blade.Position.x, blade.Position.z).magnitude, 0.01f);
            h.Enemies.Add(h.Manager.Orbit.Hits[0].Position);
            h.Manager.Tick(1f / 60f);
            Assert.AreEqual(1, h.Enemies.Damage.Count, "hit on the first tick");
            h.Manager.Tick(1f / 60f);
            Assert.AreEqual(1, h.Enemies.Damage.Count, "then waits its 0.12 s interval");
        }

        [Test]
        public void FireAll_SpeedsOrbitHits_AndFiresTheOthers()
        {
            using var h = new Harness("flying_cutlasses", "deck_cannons");
            h.Enemies.Add(new Vector3(0f, 0f, 6f));
            h.Manager.FireAllOnce();
            Assert.AreEqual(2f, h.Combat.OrbitVolleyTimer, 1e-5f);
            CollectionAssert.AreEquivalent(new[] { "deck_cannons" }, h.Hooks.Emissions.Where(e => e != "flying_cutlasses"));
        }

        [Test]
        public void LightningSalvo_StrikesForOneDamage_AsV3Did()
        {
            using var h = new Harness(true, "lightning_mast");
            h.Enemies.Add(new Vector3(0f, 0f, 5f));
            h.Manager.FireLightningSalvo();
            Assert.AreEqual(1, h.Enemies.Damage.Count, "the fallback strike, not the lightning weapon (register R-88)");
            Assert.AreEqual(1f, h.Enemies.Damage[0].amount, "level factor 0: the 1-damage floor");
        }

        // ---- Evolutions (data on the behaviors) ------------------------------------------------------------------

        static Harness Evolved(string weaponId, string evolutionId, bool v3Parity = false)
        {
            var h = new Harness(v3Parity, weaponId);
            h.Loadout.Entries[0].Evolution = evolutionId;
            h.Manager.SyncLoadout();
            Assert.IsNotNull(h.Slot(weaponId).Evolution, evolutionId + " resolved from config");
            return h;
        }

        [Test]
        public void Stormshot_FiresArcLanes_WithABurstFromTheFirstHit()
        {
            using var h = Evolved("deck_cannons", "stormshot_cannon", v3Parity: true);
            for (var i = 0; i < 4; i++) h.Enemies.Add(new Vector3(-3f + 2f * i, 0f, 8f + i));
            h.Manager.Tick(1f / 60f);
            Assert.AreEqual(4, h.Enemies.Damage.Count(d => d.weapon == "deck_cannons"), "the lane hits every enemy in its arc and range (R-18: arc 360)");
            Assert.Greater(h.Combat.Projectiles.ActiveCount, 0, "the first hit emits burst pellets");
        }

        [Test]
        public void KingsBroadside_QueuesAnOppositeSideVolley()
        {
            using var h = Evolved("broadside_battery", "kings_broadside");
            h.Enemies.Add(new Vector3(6f, 0f, 0f));
            h.Enemies.Add(new Vector3(-8f, 0f, 0f));
            h.Manager.Tick(1f / 60f);
            Assert.AreEqual(1, h.Combat.FollowUps.Count);
            Assert.Less(Vector3.Dot(h.Combat.FollowUps[0].SideDirection, Vector3.right), 0f, "the other side");
            var before = h.Combat.Projectiles.ActiveCount;
            h.Tick(10);
            Assert.AreEqual(0, h.Combat.FollowUps.Count, "fired after 0.12 s");
            Assert.Greater(h.Combat.Projectiles.ActiveCount + h.Enemies.Damage.Count, before);
        }

        [Test]
        public void DevilsWake_DropsTwoKegs_AndBlastsArmNeighbours()
        {
            using var h = Evolved("powder_kegs", "devils_wake");
            h.Tick(1);
            Assert.AreEqual(2, h.Combat.Kegs.Count, "a second keg beside the first");
            Assert.AreEqual(1.6f, Vector3.Distance(h.Combat.Kegs[0].Position, h.Combat.Kegs[1].Position), 1e-3f);
        }

        [Test]
        public void BladedTide_PulsesTheOrbitRadius()
        {
            using var h = Evolved("flying_cutlasses", "bladed_tide");
            float Radius(float t) { h.Manager.TickOrbitPositions(t); var p = h.Manager.Orbit.Hits[0].Position; return new Vector2(p.x, p.z).magnitude; }
            var pulse = h.Config.All<WeaponEvolutionConfig>().First(e => e.evolutionId == "bladed_tide").orbitPulse;
            var radius = h.Config.All<WeaponConfig>().First(c => c.weaponId == "flying_cutlasses").orbit.orbitRadius;
            var at0 = Radius(0f);
            Assert.AreEqual(radius * (pulse.minimum + pulse.amplitude * 0.5f), at0, 1e-3f);
            Assert.AreNotEqual(at0, Radius(0.5f), "it pulses over time");
        }

        [Test]
        public void HellfireNozzle_WidensTheCone()
        {
            using var plain = new Harness("flamethrower");
            using var evolved = Evolved("flamethrower", "hellfire_nozzle");
            var arc = WeaponTargetingService.ResolveArcDegrees(plain.Config.All<WeaponConfig>().First(c => c.weaponId == "flamethrower"));
            var angle = arc * 0.55f; // outside the plain half-arc, inside the widened one
            var dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            plain.Enemies.Add(dir * 5f);
            evolved.Enemies.Add(dir * 5f);
            plain.Tick(5);
            evolved.Tick(5);
            Assert.IsEmpty(plain.Hooks.Emissions, "outside the plain cone");
            Assert.IsNotEmpty(evolved.Hooks.Emissions, "inside the wider cone");
        }

        [Test]
        public void HellfireNozzle_LeavesPatchesOnTheWaterWhileStreaming_NotUnderTheParityPreset()
        {
            foreach (var parity in new[] { false, true })
            {
                using var h = Evolved("flamethrower", "hellfire_nozzle", parity);
                h.Enemies.Add(Vector3.forward * 4f, 1e9f); // starts the stream and never dies (no kill patches)
                h.Tick(120); // 2 s of streaming
                var cone = h.Config.All<WeaponEvolutionConfig>().First(e => e.evolutionId == "hellfire_nozzle").wideCone;
                if (parity) { Assert.IsFalse(cone.patchesAlongStream); Assert.IsEmpty(h.Hooks.FlamePatches, "V3: patches only where the flame kills"); continue; }
                Assert.IsTrue(cone.patchesAlongStream, "M8 playtest: on by default");
                var expected = Mathf.FloorToInt(2f / cone.streamPatchIntervalSeconds);
                Assert.That(h.Hooks.FlamePatches.Count, Is.InRange(expected - 1, expected + 1), "one patch per interval while the flame streams");
                foreach (var patch in h.Hooks.FlamePatches) Assert.Greater(patch.z, 1f, "on the water ahead of the ship");
            }
        }

        [Test]
        public void TempestMast_JumpsToTheNextEnemy_AndQueuesADelayedStrike()
        {
            using var h = Evolved("lightning_mast", "tempest_mast");
            h.Enemies.Add(new Vector3(0f, 0f, 8f));
            var far = h.Enemies.Add(new Vector3(0f, 0f, 14f)); // outside the first strike, inside the 9 m jump
            h.Manager.Tick(1f / 60f);
            Assert.IsTrue(h.Enemies.Damage.Any(d => d.index == far), "the jump strike hits the next enemy");
            Assert.AreEqual(1, h.Combat.Strikes.Count, "a delayed strike on the cluster");
            Assert.IsFalse(h.Combat.Strikes[0].AreaShell);
        }

        // ---- Fixes approved at the M5 gate (on by default; the v3-parity preset turns them off) ---------------------

        [Test]
        public void ParityPreset_TurnsEveryM5FixOff()
        {
            using var fixedRun = new Harness();
            using var v3 = new Harness(true);
            WeaponConfig W(Harness h, string id) => h.Config.All<WeaponConfig>().First(c => c.weaponId == id);
            Assert.IsTrue(fixedRun.Config.All<WeaponConfig>().All(c => c.levelDamageFactor), "R-21: the level factor on every weapon");
            Assert.IsTrue(W(fixedRun, "broadside_battery").fan.quantityAddsPellets, "R-22: Broadside takes Quantity");
            Assert.IsTrue(W(fixedRun, "lightning_mast").areaStrike.chain, "R-19: chain lightning");
            Assert.IsFalse(W(fixedRun, "swivel_guns").projectile.v3FullCooldownAfterFirstShot, "R-87");
            Assert.IsFalse(W(fixedRun, "mortar").predictedArea.v3ImpactEffectDraws, "R-70");
            Assert.IsTrue(W(fixedRun, "mortar").predictedArea.telegraphMatchesImpact, "R-27");
            var rules = fixedRun.Config.All<WeaponRulesConfig>().First();
            Assert.IsTrue(rules.itemAttackSpeedAppliedOnce && rules.carryCooldownOvershoot && rules.salvoUsesLightningWeapon && rules.salvoLevelFactor == 1f, "R-22, R-88");
            Assert.AreEqual(40f, fixedRun.Config.All<WeaponEvolutionConfig>().First(e => e.evolutionId == "stormshot_cannon").laneBurst.laneArcDegrees, "R-18");

            CollectionAssert.AreEquivalent(new[] { "deck_cannons", "harpoon_gun", "long_nine", "swivel_guns" }, v3.Config.All<WeaponConfig>().Where(c => c.levelDamageFactor).Select(c => c.weaponId));
            Assert.IsFalse(W(v3, "broadside_battery").fan.quantityAddsPellets);
            Assert.IsFalse(W(v3, "lightning_mast").areaStrike.chain);
            Assert.IsTrue(W(v3, "swivel_guns").projectile.v3FullCooldownAfterFirstShot);
            Assert.IsTrue(W(v3, "mortar").predictedArea.v3ImpactEffectDraws);
            var v3Rules = v3.Config.All<WeaponRulesConfig>().First();
            Assert.IsFalse(v3Rules.itemAttackSpeedAppliedOnce || v3Rules.carryCooldownOvershoot || v3Rules.salvoUsesLightningWeapon);
            Assert.AreEqual(0f, v3Rules.salvoLevelFactor);
            Assert.AreEqual(0f, v3.Config.All<WeaponEvolutionConfig>().First(e => e.evolutionId == "stormshot_cannon").laneBurst.laneArcDegrees);
        }

        [Test]
        public void R87_SwivelBurst_KeepsItsGap()
        {
            using var h = new Harness("swivel_guns");
            h.Enemies.Add(new Vector3(0f, 0f, 5f));
            h.Tick(1);
            var slot = h.Slot("swivel_guns");
            Assert.AreEqual(0.18f, slot.CooldownTimer, 1e-5f, "the second shot comes 0.18 s later");
            h.Tick(12);
            Assert.AreEqual(2, h.Hooks.Emissions.Count, "both shots of the burst");
        }

        /// <summary>M9 gate, R-40: keg blasts and mortar shells deal the explosion multiplier and use Powder Fuse's charge.</summary>
        [Test]
        public void R40_Explosions_TakeTheExplosionMultiplier_AndResolveTheCharge()
        {
            float KegHit(float explosion)
            {
                using var h = new Harness("powder_kegs");
                h.Mods.ExplosionDamageMultiplier = explosion;
                h.Ship.Forward = Vector3.forward;
                h.Tick(1);
                h.Enemies.Add(h.Combat.Kegs[0].Position);
                h.Tick(30);
                Assert.AreEqual(1, h.Mods.ExplosionsResolved, "one blast resolves the charge once");
                return h.Enemies.Damage.Single(d => d.weapon == "powder_kegs").amount;
            }
            Assert.AreEqual(KegHit(1f) * 1.5f, KegHit(1.5f), 1e-3f, "keg blast × explosion multiplier");

            float MortarHit(float explosion)
            {
                using var h = new Harness(true, "mortar");
                h.Mods.ExplosionDamageMultiplier = explosion;
                h.Mods.CritChance = 0f;
                h.Enemies.Add(new Vector3(0f, 0f, 10f));
                h.Tick(41);
                Assert.AreEqual(1, h.Mods.ExplosionsResolved, "the shell's impact resolves the charge");
                return h.Enemies.Damage.Single(d => d.weapon == "mortar").amount;
            }
            Assert.AreEqual(MortarHit(1f) * 1.5f, MortarHit(1.5f), 1e-3f, "mortar shell × explosion multiplier");
        }

        /// <summary>M9 gate, R-40: Stormglass adds chain-lightning jumps.</summary>
        [Test]
        public void R40_Stormglass_AddsAChainJump()
        {
            using var h = new Harness("lightning_mast");
            h.Mods.ExtraLightningJumps = 1;
            for (var i = 0; i < 9; i++) h.Enemies.Add(new Vector3(0f, 0f, 6f + 5f * i));
            h.Manager.Tick(1f / 60f);
            var a = h.Config.All<WeaponConfig>().First(c => c.weaponId == "lightning_mast").areaStrike;
            Assert.AreEqual(a.chainTargets + 1, h.Enemies.Damage.Count, "one more enemy along the chain");
        }

        [Test]
        public void R19_ChainLightning_JumpsToTheNearestUnhitEnemies_WithFalloff()
        {
            using var h = new Harness("lightning_mast");
            for (var i = 0; i < 7; i++) h.Enemies.Add(new Vector3(0f, 0f, 6f + 5f * i)); // 5 m apart, a line away from the ship
            h.Manager.Tick(1f / 60f);
            var a = h.Config.All<WeaponConfig>().First(c => c.weaponId == "lightning_mast").areaStrike;
            Assert.AreEqual(a.chainTargets, h.Enemies.Damage.Count, "chainTargets hits, one per enemy along the chain");
            CollectionAssert.AreEqual(Enumerable.Range(0, a.chainTargets).ToArray(), h.Enemies.Damage.Select(d => d.index).ToArray(), "nearest first");
            var ratio = h.Enemies.Damage[1].amount / h.Enemies.Damage[0].amount;
            Assert.AreEqual(a.chainFalloff, ratio, 0.35f, "each jump deals less (crits aside)");
            Assert.AreEqual(a.chainTargets, h.Enemies.Statuses.Count(st => st.status == WeaponStatus.Shock));
        }

        [Test]
        public void R18_Stormshot_LaneHasANarrowArc()
        {
            using var h = Evolved("deck_cannons", "stormshot_cannon");
            h.Enemies.Add(new Vector3(0f, 0f, 10f));   // ahead: the target
            h.Enemies.Add(new Vector3(8f, 0f, 3f));    // far off the aim line, still in range
            h.Manager.Tick(1f / 60f);
            Assert.AreEqual(1, h.Enemies.Damage.Count(d => d.weapon == "deck_cannons"), "only enemies inside the 40° lane");
        }

        [Test]
        public void R22_ItemAttackSpeed_AppliesOnce_AndOvershootCarries()
        {
            using var v3 = new Harness(true, "deck_cannons");
            using var fixedRun = new Harness("deck_cannons");
            foreach (var h in new[] { v3, fixedRun })
            {
                h.Mods.ItemAttackSpeedMultiplier = 2f;
                h.Enemies.Add(new Vector3(0f, 0f, 6f));
            }
            v3.Tick(300);
            fixedRun.Tick(300);
            var cd = WeaponStatResolver.Cooldown(fixedRun.Slot("deck_cannons"), fixedRun.Mods, 2f);
            Assert.AreEqual(300f / 60f / cd, fixedRun.Hooks.Emissions.Count, 1.5f, "one shot per cooldown");
            Assert.Greater(v3.Hooks.Emissions.Count, fixedRun.Hooks.Emissions.Count * 1.6f, "V3 applied the item bonus twice");
        }

        [Test]
        public void R88_Salvo_FiresTheLightningWeapon()
        {
            using var h = new Harness("lightning_mast");
            h.Enemies.Add(new Vector3(0f, 0f, 5f));
            h.Manager.FireLightningSalvo();
            CollectionAssert.Contains(h.Hooks.Emissions, "lightning_mast");
            Assert.Greater(h.Enemies.Damage[0].amount, 1f);
        }

        [Test]
        public void R70_Mortar_NoLongerDrawsForItsEffect()
        {
            using var h = new Harness("mortar");
            h.Enemies.Add(new Vector3(0f, 0f, 10f));
            h.Tick(1);
            var before = h.Random.Draws;
            h.Tick(40);
            Assert.AreEqual(1, h.Random.Draws - before, "only the crit roll for the enemy hit");
        }

        // ---- Projectiles and loadout -------------------------------------------------------------------------------

        [Test]
        public void PlayerProjectiles_TakeTheLowestFreeSlot_HitOnce_AndHomeOnlyWhenAsked()
        {
            var enemies = new FakeEnemies();
            var hooks = new FakeHooks();
            var projectiles = new PlayerProjectiles(4, enemies, enemies, hooks, null);
            var e = enemies.Add(new Vector3(0f, 0f, 3f));
            Assert.IsTrue(projectiles.TrySpawn(Vector3.zero, Vector3.forward, 20f, 5f, 3f, 0.3f, false, Color.white, "deck_cannons", ProjectileTraits.Plain));
            Assert.IsTrue(projectiles.TrySpawn(Vector3.zero, Vector3.back, 20f, 5f, 3f, 0.3f, false, Color.white, "deck_cannons", ProjectileTraits.Plain));
            for (var i = 0; i < 20; i++) projectiles.Tick(1f / 60f);
            Assert.AreEqual(1, enemies.Damage.Count(d => d.index == e), "one hit, then the projectile is spent");
            Assert.AreEqual(1, projectiles.ActiveCount, "the other flies on");
            Assert.IsTrue(projectiles.TrySpawn(Vector3.zero, Vector3.left, 20f, 5f, 3f, 0.3f, false, Color.white, null, ProjectileTraits.Plain));
            Assert.AreEqual(2, projectiles.ActiveCount);
        }

        [Test]
        public void Loadout_NewEntriesGetFreshSlots_UpgradesFollow_StateIsMirrored()
        {
            using var h = new Harness("deck_cannons");
            h.Enemies.Add(new Vector3(0f, 0f, 6f));
            h.Tick(1);
            var first = h.Slot("deck_cannons");
            Assert.Greater(first.CooldownTimer, 0f);
            h.Loadout.Entries[0].Level = 5;
            h.Loadout.Entries[0].DamageBonus = 1.4f;
            h.Tick(1);
            Assert.AreSame(first, h.Slot("deck_cannons"), "same record entry: same slot and cooldown");
            Assert.AreEqual((5, 1.4f), (first.Level, first.DamageBonus));
            Assert.AreEqual(first.CooldownTimer, h.Loadout.Entries[0].Mirror.CooldownTimer, 1e-6f, "cooldown mirrored to the record");

            h.Loadout.Entries[0] = new Entry { Id = "deck_cannons" }; // replaced (a new WeaponInstance in V3)
            h.Manager.SyncLoadout();
            Assert.AreNotSame(first, h.Slot("deck_cannons"));
            Assert.AreEqual(0f, h.Slot("deck_cannons").CooldownTimer, "a new weapon fires at once, as V3's new instance did");
        }
    }
}
