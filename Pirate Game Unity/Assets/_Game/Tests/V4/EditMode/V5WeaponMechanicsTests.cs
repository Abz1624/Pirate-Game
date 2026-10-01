using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Spawning;
using RoguePirates.V4.Weapons;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M4a: the weapon mechanics the new weapons need, each opt-in (defaults leave today's weapons as they were, which
    /// <see cref="V5WeaponGoldenTests"/> checks): piercing shots and their slow, several Orbit weapons at once with
    /// per-target hits and knockback, an aura that pulses without a target and slows. The enemy side (slow caps,
    /// knockback distance) is in V4EnemyTickM8Tests. The fakes record slows and knockbacks, so a mechanic that does
    /// nothing fails here (review: default interface methods would hide it).
    /// </summary>
    public sealed class V5WeaponMechanicsTests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";

        sealed class Enemy { public bool Active = true; public Vector3 Position; public float Hp = 1e6f; }

        sealed class Enemies : IEnemyQuery, IEnemyDamageSink
        {
            public readonly List<Enemy> List = new List<Enemy>();
            public readonly List<int> Hits = new List<int>();
            public readonly List<(int index, WeaponStatus status)> Statuses = new List<(int, WeaponStatus)>();
            public readonly List<(int index, float fraction, float seconds)> Slows = new List<(int, float, float)>();
            public readonly List<(int index, float meters)> Knocks = new List<(int, float)>();
            public int Add(Vector3 p) { List.Add(new Enemy { Position = p }); return List.Count - 1; }
            public bool Exists => true;
            public int Capacity => List.Count;
            public bool IsActive(int i) => List[i].Active;
            public Vector3 Position(int i) => List[i].Position;
            public Vector3 Velocity(int i) => Vector3.zero;
            public float Radius(int i) => 0.8f;
            public float Hp(int i) => List[i].Hp;
            public bool IsElite(int i) => false;
            public bool IsBoss(int i) => false;
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
            public void ApplyDamage(int index, float amount, bool isCrit, bool showHitFeedback = true, string weaponId = null) => Hits.Add(index);
            public void ApplyStatus(int index, WeaponStatus status, float strength) => Statuses.Add((index, status));
            public void ApplySlow(int index, float fraction, float seconds) => Slows.Add((index, fraction, seconds));
            public void Knockback(int index, Vector3 from, float meters) => Knocks.Add((index, meters));
        }

        sealed class Ship : IShipState
        {
            public bool Exists => true;
            public Vector3 Position => Vector3.zero;
            public Vector3 Velocity => Vector3.zero;
            public Vector3 MovementForward => Vector3.forward;
            public Vector3 TransformPoint(Vector3 local) => local;
            public bool Boosting;
            public bool IsBoosting => Boosting;
        }

        sealed class Mods : IWeaponModifiers
        {
            public float AttackSpeed => 1f;
            public float ProjectileSpeed => 1f;
            public float Size => 1f;
            public float Duration => 1f;
            public float Quantity => 0f;
            public float CritChance => 0f;
            public float CritDamage => 1.5f;
            public float EliteDamage => 1f;
            public float GlobalDamage => 1f;
            public float CaptainAdditiveDamage => 0f;
            public float CaptainWeaponMultiplier(string weaponId) => 1f;
            public float ItemGlobalDamageMultiplier => 1f;
            public float ItemCritChanceBonus => 0f;
            public float ItemAttackSpeedMultiplier => 1f;
            public float FlameRangeMultiplier => 1f;
            public float BurnDamageMultiplier => 1f;
            public float PowderKegBlastRadiusMultiplier => 1f;
            public float ExplosionDamageMultiplier => 1f;
            public void ExplosionResolved() { }
            public int ExtraLightningJumps => 0;
        }

        sealed class Hooks : IWeaponCombatHooks
        {
            public void BeginDamageResolution() { }
            public bool TryRegisterProcEvent() => true;
            public void RecordHit(string weaponId, float damage, bool isCrit, bool isElite, bool isBoss) { }
            public void RecordEmission(string weaponId) { }
            public void RecordConcurrentTargets(string weaponId, int count) { }
            public void RecordControl(string weaponId, float seconds) { }
            public void SpawnFlamePatch(Vector3 position, string weaponId) { }
            public void ApplyShipRadiusDamage(Vector3 center, float radius, float damage, string weaponId) { }
            public bool TryResolveShipAim(string weaponId, out Vector3 direction) { direction = Vector3.forward; return false; }
            public bool ShipVersusShip => false;
            public bool IsPvP => false;
            public bool AutoFireDisabled => false;
        }

        sealed class Random : ICombatRandom
        {
            readonly System.Random _r = new System.Random(3);
            public float CombatFloat() => (float)_r.NextDouble();
        }

        sealed class Run : IRunStatus
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

        sealed class Loadout : IWeaponLoadout
        {
            public readonly List<(object key, string id)> Entries = new List<(object, string)>();
            public float Knockback = 1f, Cooldown = 1f;
            public string Evolution;
            public int Count => Entries.Count;
            public LoadoutEntry Get(int i) => new LoadoutEntry { Key = Entries[i].key, WeaponId = Entries[i].id, EvolutionId = Evolution, Level = 1, DamageBonus = 1f,
                CooldownMult = Cooldown, SpeedMult = 1f, SizeMult = 1f, DurationMult = 1f, KnockbackMult = Knockback };
            public void Mirror(int i, in LoadoutMirror state) { }
        }

        sealed class Rig : System.IDisposable
        {
            public readonly ConfigSetRuntime Config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            public readonly Enemies Enemies = new Enemies();
            public readonly Loadout Loadout = new Loadout();
            public readonly WeaponCombat Combat;
            public readonly WeaponManager Manager;
            public readonly Ship Ship = new Ship();

            public Rig()
            {
                var mods = new Mods();
                var ship = Ship;
                var random = new Random();
                Combat = new WeaponCombat
                {
                    Enemies = Enemies, Damage = Enemies, Ship = ship, Mods = mods, Hooks = new Hooks(), Random = random,
                    Calculator = new DamageCalculator(mods, random),
                    Targeting = new WeaponTargetingService(Enemies, ship),
                    Projectiles = new PlayerProjectiles(64, Enemies, Enemies, new Hooks(), null),
                    Rules = () => Config.All<WeaponRulesConfig>().First(),
                };
                Manager = new WeaponManager(Combat, Loadout, new Run(), Config);
            }

            public WeaponConfig Weapon(string id) => Config.All<WeaponConfig>().Single(w => w.weaponId == id);
            public void Dispose() => Config.Dispose();
        }

        static void Fly(PlayerProjectiles shots, float seconds)
        {
            for (var t = 0f; t < seconds; t += 1f / 60f) shots.Tick(1f / 60f);
        }

        [Test]
        public void APlainShot_StopsAtItsFirstHit()
        {
            using var rig = new Rig();
            foreach (var z in new[] { 4f, 6f, 8f, 10f }) rig.Enemies.Add(new Vector3(0f, 0f, z));
            Assert.IsTrue(rig.Combat.Projectiles.TrySpawn(Vector3.zero, Vector3.forward, 20f, 10f, 2f, 0.3f, false, Color.white, "test", ProjectileTraits.Plain));
            Fly(rig.Combat.Projectiles, 1f);
            CollectionAssert.AreEqual(new[] { 0 }, rig.Enemies.Hits, "today's shots: one hit");
            CollectionAssert.IsEmpty(rig.Enemies.Slows);
        }

        [Test]
        public void APiercingShot_HitsOneMorePerPierce_EachEnemyOnce_AndSlows()
        {
            using var rig = new Rig();
            foreach (var z in new[] { 4f, 4.3f, 6f, 8f, 10f }) rig.Enemies.Add(new Vector3(0f, 0f, z));
            var traits = ProjectileTraits.Plain;
            traits.Pierce = 2;
            traits.SlowFraction = 0.25f;
            traits.SlowSeconds = 2f;
            Assert.IsTrue(rig.Combat.Projectiles.TrySpawn(Vector3.zero, Vector3.forward, 4f, 10f, 4f, 0.75f, false, Color.white, "test", traits));
            Fly(rig.Combat.Projectiles, 3.5f);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, rig.Enemies.Hits, "first hit + 2 pierces, each enemy once although the slow wide shot overlaps them for many frames");
            Assert.AreEqual(3, rig.Enemies.Slows.Count);
            Assert.IsTrue(rig.Enemies.Slows.All(s => Mathf.Approximately(s.fraction, 0.25f) && Mathf.Approximately(s.seconds, 2f)));
        }

        [Test]
        public void TheProjectileBlock_CarriesPierceAndSlow_IntoItsShots()
        {
            using var rig = new Rig();
            var w = rig.Weapon("deck_cannons");
            Assert.AreEqual((0, 0f), (w.projectile.pierce, w.projectile.slowFraction), "today's weapons: no pierce, no slow");
            w.projectile.pierce = 3;
            w.projectile.slowFraction = 0.3f;
            w.projectile.slowSeconds = 1.5f;
            var traits = rig.Combat.TraitsOf(w);
            Assert.AreEqual((3, 0.3f, 1.5f), (traits.Pierce, traits.SlowFraction, traits.SlowSeconds));
        }

        [Test]
        public void TwoOrbitWeapons_EachHaveTheirBlades()
        {
            using var rig = new Rig();
            rig.Loadout.Entries.Add((new object(), "flying_cutlasses"));
            rig.Loadout.Entries.Add((new object(), "flying_cutlasses"));
            rig.Manager.SyncLoadout();
            rig.Manager.TickOrbitPositions(0.3f);
            var slots = rig.Manager.Slots;
            Assert.AreEqual(2, slots.Count);
            Assert.Greater(rig.Manager.OrbitHitsOf(slots[0]).Count, 0, "the first orbit weapon");
            Assert.Greater(rig.Manager.OrbitHitsOf(slots[1]).Count, 0, "and the second (only the first had blades before)");
            Assert.AreSame(rig.Manager.OrbitHitsOf(slots[0]), rig.Manager.OrbitHits, "the first's blades are still the cutlass drawing's");
        }

        [Test]
        public void APerTargetOrbit_HitsEachEnemyOncePerInterval_AndKnocksBack()
        {
            using var rig = new Rig();
            var w = rig.Weapon("flying_cutlasses");
            w.orbit.perTargetIntervalSeconds = 0.75f;
            w.orbit.knockbackMeters = 2f;
            w.orbit.hitRadius = 100f; // every blade touches the enemy all the time
            rig.Loadout.Knockback = 1.5f;
            rig.Loadout.Entries.Add((new object(), "flying_cutlasses"));
            rig.Manager.SyncLoadout();
            rig.Enemies.Add(new Vector3(3f, 0f, 0f));
            const float dt = 1f / 60f;
            for (var f = 0; f < 120; f++) // 2 s
            {
                rig.Manager.TickOrbitPositions(f * dt);
                rig.Manager.Tick(dt);
            }
            Assert.AreEqual(3, rig.Enemies.Hits.Count, "at 0, 0.75 and 1.5 s");
            Assert.AreEqual(3, rig.Enemies.Knocks.Count);
            Assert.IsTrue(rig.Enemies.Knocks.All(k => Mathf.Approximately(k.meters, 3f)), "2 m × the Knockback upgrade (1.5)");
        }

        [Test]
        public void AnAura_PulsesWithoutATarget_SlowsWhatItHits_AndCursesOnlyWhenSet()
        {
            using var rig = new Rig();
            var w = rig.Weapon("mortar"); // a runtime copy turned into an aura for the test
            w.behavior = ConfigEnums.WeaponBehavior.Aura;
            w.aura.radiusBase = 5f;
            w.aura.slowFraction = 0.2f;
            w.aura.slowSeconds = 1.5f;
            w.aura.curseSeconds = 0f;
            rig.Loadout.Entries.Add((new object(), "mortar"));
            rig.Manager.SyncLoadout();
            Assert.IsTrue(rig.Manager.FireSlot(0), "pulses with no enemy anywhere");
            var near = rig.Enemies.Add(new Vector3(3f, 0f, 0f));
            rig.Enemies.Add(new Vector3(9f, 0f, 0f));
            rig.Manager.FireSlot(0);
            CollectionAssert.AreEqual(new[] { near }, rig.Enemies.Hits, "only the enemy inside the radius");
            CollectionAssert.AreEqual(new[] { (near, 0.2f, 1.5f) }, rig.Enemies.Slows);
            CollectionAssert.IsEmpty(rig.Enemies.Statuses, "no curse when curseSeconds is 0");
        }

        [Test]
        public void TheFlame_ReachesTheConesRange_AndSpreadsToItsArc()
        {
            // R-89: the damage cone reaches `range` past the muzzle; the flame's particles are simulated and the farthest
            // one measured (review: a test that re-used the presenter's own formula could not fail).
            var settings = Resources.Load<RoguePirates.V4.Foundation.Data.DemoJuiceSettings>("DemoJuiceSettings");
            Assert.IsNotNull(settings);
            var host = new GameObject("flame test");
            try
            {
                var flame = host.AddComponent<RoguePirates.V4.Presentation.Weapons.FlameStreamPresenter>();
                flame.Bind(null, settings, null);
                var type = typeof(RoguePirates.V4.Presentation.Weapons.FlameStreamPresenter);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                Assert.IsTrue((bool)type.GetMethod("EnsureMount", flags).Invoke(flame, null), "the stream effect exists");
                var mount = host.transform.Find("V4FlameStream");
                Assert.IsNotNull(mount);
                foreach (var (reach, arc) in new[] { (9.5f, 55f), (13f, 80f) })
                {
                    type.GetMethod("Shape", flags).Invoke(flame, new object[] { reach, arc });
                    var farthest = 0f;
                    var buffer = new ParticleSystem.Particle[2048];
                    for (var i = 0; i < flame.ParticleSystemCount; i++)
                    {
                        if (!flame.Travels(i)) continue;
                        var ps = flame.Particles(i);
                        var emission = ps.emission;
                        emission.enabled = true;
                        ps.Simulate(ps.main.startLifetimeMultiplier * 1.5f + 0.1f, false, true, true);
                        var n = ps.GetParticles(buffer);
                        for (var k = 0; k < n; k++)
                        {
                            var world = ps.main.simulationSpace == ParticleSystemSimulationSpace.Local ? ps.transform.TransformPoint(buffer[k].position) : buffer[k].position;
                            farthest = Mathf.Max(farthest, Vector3.Distance(world, mount.position));
                        }
                        var shape = ps.shape;
                        if (shape.enabled && shape.shapeType == ParticleSystemShapeType.Cone)
                            Assert.AreEqual(arc * 0.5f, shape.angle, 0.01f, $"particle system {i} spreads to the cone's half-arc");
                    }
                    Debug.Log($"[V5 M4a flame] cone {reach} m: farthest flame particle {farthest:0.00} m");
                    Assert.That(farthest, Is.InRange(reach * 0.75f, reach * 1.15f), $"the flame reaches about the cone's {reach} m");
                }
            }
            finally { Object.DestroyImmediate(host); }
        }

        // ---- M4 proper: the new weapons and the Empowered evolutions (real configs) ------------------------------------

        static readonly string[] NewWeapons = { "anchor_flail", "chainshot", "siren_bell", "ramming_prow" };

        [Test]
        public void TheNewWeapons_AreBuyable_Offerable_AndEachEvolutionIsReachable()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            var weapons = config.All<WeaponConfig>().ToDictionary(w => w.weaponId);
            var unlocks = config.All<UnlockConfig>().Single();
            var icons = RoguePirates.V4.Presentation.Hud.IconCatalog.Load();
            foreach (var id in NewWeapons)
            {
                Assert.IsTrue(weapons.ContainsKey(id), id);
                Assert.GreaterOrEqual(weapons[id].databaseOrder, 0, $"{id} is in the level-up offers once bought");
                Assert.IsFalse(unlocks.starterWeaponIds.Contains(id), $"{id} is bought, not a starter");
                Assert.Greater(unlocks.weaponUnlocks.Single(u => u.weaponId == id).rumCost, 0, $"{id} has a Rum price");
                Assert.IsNotNull(icons.Weapon(id), $"{id} has an icon");
            }
            var items = config.All<ItemConfig>().ToDictionary(i => i.itemId);
            var evolutions = config.All<WeaponEvolutionConfig>().ToList();
            foreach (var id in NewWeapons.Concat(new[] { "harpoon_gun", "long_nine", "mortar", "swivel_guns" }))
            {
                var evo = evolutions.Single(e => e.baseWeapon != null && e.baseWeapon.weaponId == id);
                Assert.AreEqual(ConfigEnums.EvolutionEffect.Empowered, evo.effect, id);
                Assert.IsTrue(items.TryGetValue(evo.requiredItemId, out var item), $"{evo.evolutionId} needs {evo.requiredItemId}");
                Assert.IsTrue(item.inChestPool, $"{item.itemId} drops from chests (ChestEconomy only rolls evolution items in its pool)");
                Assert.IsNotNull(icons.Item(item.itemId), $"{item.itemId} has an icon");
            }
            Assert.AreEqual(14, evolutions.Count, "the six from V3 and the eight from V5 M4");
            Assert.AreEqual(evolutions.Count, evolutions.Select(e => e.baseWeapon.weaponId).Distinct().Count(), "one evolution per weapon");
        }

        [Test]
        public void AnEmpoweredEvolution_AddsItsNumbers_OnTopOfTheUpgrades()
        {
            using var rig = new Rig();
            rig.Loadout.Entries.Add((new object(), "chainshot"));
            rig.Manager.SyncLoadout();
            var slot = rig.Manager.Slots[0];
            var plainMult = WeaponStatResolver.LevelMult(slot);
            Assert.AreEqual((0, 2), (slot.QuantityBonus, rig.Combat.TraitsOf(slot).Pierce), "the weapon alone");

            rig.Loadout.Evolution = "bar_shot_barrage";
            rig.Manager.SyncLoadout();
            var e = slot.Evolution.empower;
            Assert.AreEqual((e.extraQuantity, 2 + e.extraPierce), (slot.QuantityBonus, rig.Combat.TraitsOf(slot).Pierce), "more shots, more pierce");
            Assert.AreEqual(plainMult * e.damageMultiplier, WeaponStatResolver.LevelMult(slot), 1e-5f, "harder");
            rig.Manager.SyncLoadout();
            Assert.AreEqual(e.extraQuantity, slot.QuantityBonus, "applied once per sync, not stacked");

            rig.Loadout.Evolution = null;
            rig.Manager.SyncLoadout();
            Assert.AreEqual((0, 2, plainMult), (slot.QuantityBonus, rig.Combat.TraitsOf(slot).Pierce, WeaponStatResolver.LevelMult(slot)), "gone with the evolution");
        }

        [Test]
        public void TheSirenBell_SlowsAroundTheShip_AndItsChoir_WiderAndHarder()
        {
            using var rig = new Rig();
            var bell = rig.Weapon("siren_bell");
            rig.Loadout.Entries.Add((new object(), "siren_bell"));
            rig.Manager.SyncLoadout();
            var near = rig.Enemies.Add(new Vector3(bell.aura.radiusBase - 0.5f, 0f, 0f));
            var outside = rig.Enemies.Add(new Vector3(bell.aura.radiusBase + 1.5f, 0f, 0f));
            rig.Manager.FireSlot(0);
            CollectionAssert.AreEqual(new[] { near }, rig.Enemies.Hits);
            CollectionAssert.AreEqual(new[] { (near, bell.aura.slowFraction, bell.aura.slowSeconds) }, rig.Enemies.Slows);
            CollectionAssert.IsEmpty(rig.Enemies.Statuses, "the bell does not curse");

            rig.Enemies.Hits.Clear();
            rig.Enemies.Slows.Clear();
            rig.Loadout.Evolution = "drowned_choir";
            rig.Manager.SyncLoadout();
            rig.Manager.FireSlot(0);
            var e = rig.Manager.Slots[0].Evolution.empower;
            CollectionAssert.AreEqual(new[] { near, outside }, rig.Enemies.Hits, "the choir reaches further");
            Assert.AreEqual(bell.aura.slowFraction + e.extraSlow, rig.Enemies.Slows[0].fraction, 1e-5f, "and slows harder");
        }

        [Test]
        public void TheRammingProw_StrikesAheadOnlyWhileBoosting_AndShoves()
        {
            using var rig = new Rig();
            var prow = rig.Weapon("ramming_prow");
            rig.Loadout.Entries.Add((new object(), "ramming_prow"));
            rig.Manager.SyncLoadout();
            var ahead = rig.Enemies.Add(new Vector3(0f, 0f, prow.aura.forwardOffset + 0.5f));
            rig.Enemies.Add(new Vector3(0f, 0f, -prow.aura.forwardOffset)); // behind: inside a ship-centred pulse, not the prow's
            const float dt = 1f / 60f;
            for (var f = 0; f < 60; f++) rig.Manager.Tick(dt);
            CollectionAssert.IsEmpty(rig.Enemies.Hits, "no boost, no strike");

            rig.Ship.Boosting = true;
            for (var f = 0; f < 60; f++) rig.Manager.Tick(dt);
            Assert.Greater(rig.Enemies.Hits.Count, 1, "strikes again and again while boosting");
            Assert.IsTrue(rig.Enemies.Hits.All(i => i == ahead), "only what is ahead");
            Assert.IsTrue(rig.Enemies.Knocks.All(k => k.index == ahead && Mathf.Approximately(k.meters, prow.aura.knockbackMeters)), "and shoves it");
            Assert.AreEqual(rig.Enemies.Hits.Count, rig.Enemies.Knocks.Count);
        }

        [Test]
        public void TheAnchorFlail_HitsEachEnemyOncePerInterval_AndKnocksBack_AndItsCooldownCardsShortenIt()
        {
            int Hits(float cooldownMult)
            {
                using var rig = new Rig();
                var flail = rig.Weapon("anchor_flail");
                Assert.IsNotNull(flail.visual.orbitModel, "drawn with its own model, not the cutlass blades");
                flail.orbit.hitRadius = 100f; // the anchor touches the enemy all the time
                rig.Loadout.Entries.Add((new object(), "anchor_flail"));
                rig.Loadout.Cooldown = cooldownMult; // as a Cooldown card
                rig.Manager.SyncLoadout();
                rig.Enemies.Add(new Vector3(3f, 0f, 0f));
                const float dt = 1f / 60f;
                for (var f = 0; f < 180; f++) // 3 s
                {
                    rig.Manager.TickOrbitPositions(f * dt);
                    rig.Manager.Tick(dt);
                }
                Assert.AreEqual(rig.Enemies.Hits.Count, rig.Enemies.Knocks.Count, "every hit shoves");
                Assert.IsTrue(rig.Enemies.Knocks.All(k => Mathf.Approximately(k.meters, flail.orbit.knockbackMeters)));
                return rig.Enemies.Hits.Count;
            }
            Assert.AreEqual(Mathf.CeilToInt(3f / 0.75f), Hits(1f), "once per 0.75 s");
            Assert.Greater(Hits(0.5f), Hits(1f), "a Cooldown card shortens the per-target interval");
        }

        /// <summary>Review (M4): each Empowered evolution must change its weapon in play, not only in its numbers.</summary>
        [Test]
        public void EveryEmpoweredEvolution_ChangesItsWeaponInPlay()
        {
            using var rig = new Rig();
            // Kraken Chain: a second anchor.
            rig.Loadout.Entries.Add((new object(), "anchor_flail"));
            rig.Manager.SyncLoadout();
            rig.Manager.TickOrbitPositions(0f);
            Assert.AreEqual(1, rig.Manager.OrbitHitsOf(rig.Manager.Slots[0]).Count);
            rig.Loadout.Evolution = "kraken_chain";
            rig.Manager.SyncLoadout();
            rig.Manager.TickOrbitPositions(0f);
            Assert.AreEqual(2, rig.Manager.OrbitHitsOf(rig.Manager.Slots[0]).Count, "Kraken Chain swings two anchors");
        }

        /// <summary>Shots of the first volley or burst (the target is far enough that none lands before it ends).</summary>
        static int ShotsFired(string weaponId, string evolutionId)
        {
            using var rig = new Rig();
            rig.Enemies.Add(new Vector3(0f, 0f, 11.5f)); // in range (Swivel 12 m), reached after the burst
            rig.Loadout.Entries.Add((new object(), weaponId));
            rig.Loadout.Evolution = evolutionId;
            rig.Manager.SyncLoadout();
            const float dt = 1f / 60f;
            var gap = rig.Weapon(weaponId).projectile.burstGapSeconds;
            for (var f = 0; f < 120; f++)
            {
                rig.Manager.Tick(dt);
                var slot = rig.Manager.Slots[0];
                if (rig.Combat.Projectiles.ActiveCount > 0 && slot.BurstShotsLeft == 0 && slot.CooldownTimer > gap + 0.05f) break; // the full cooldown started
            }
            return rig.Combat.Projectiles.ActiveCount;
        }

        [Test]
        public void SwivelBattery_AndWhalersVolley_FireMoreShots()
        {
            var swivel = ShotsFired("swivel_guns", null);
            Assert.AreEqual(swivel + 2, ShotsFired("swivel_guns", "swivel_battery"), "Swivel Battery: two more shots in the burst");
            var harpoon = ShotsFired("harpoon_gun", null);
            Assert.AreEqual(harpoon + 1, ShotsFired("harpoon_gun", "whalers_volley"), "Whaler's Volley: one more harpoon");
        }

        [Test]
        public void SiegeBombard_AndShipOfTheLine_HitWider()
        {
            using var rig = new Rig();
            rig.Enemies.Add(new Vector3(0f, 0f, 8f));
            rig.Loadout.Entries.Add((new object(), "mortar"));
            rig.Manager.SyncLoadout();
            rig.Manager.FireSlot(0);
            var plain = rig.Combat.Strikes[0].Radius;
            rig.Combat.Strikes.Clear();
            rig.Loadout.Evolution = "siege_bombard";
            rig.Manager.SyncLoadout();
            rig.Manager.FireSlot(0);
            Assert.AreEqual(plain * rig.Manager.Slots[0].Evolution.empower.sizeMultiplier, rig.Combat.Strikes[0].Radius, 1e-3f, "Siege Bombard: a wider shell");

            int LaneHits(string evolution)
            {
                using var r = new Rig();
                foreach (var x in new[] { 0f, 1.2f }) r.Enemies.Add(new Vector3(x, 0f, 8f)); // the plain beam reaches 1.12 m beside its line (half width and the enemy radius)
                r.Loadout.Entries.Add((new object(), "long_nine"));
                r.Loadout.Evolution = evolution;
                r.Manager.SyncLoadout();
                r.Manager.FireSlot(0);
                return r.Enemies.Hits.Distinct().Count();
            }
            Assert.Greater(LaneHits("ship_of_the_line"), LaneHits(null), "Ship of the Line: a wider beam reaches enemies beside the line");
        }

        [Test]
        public void TheShipState_IsNotBoosting_ByDefault()
        {
            IShipState ship = new Ship();
            Assert.IsFalse(ship.IsBoosting);
        }
    }
}
