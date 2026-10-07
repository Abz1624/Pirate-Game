using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Spawning;
using RoguePirates.V4.Weapons;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M8z (8 Oct, close): the final check's fixes. Every weapon's kills are credited to it (orbits, strikes, trails,
    /// cones, whirlpools and slicks dealt damage under their ledger id but no weapon id, so their kills went uncredited:
    /// the latest session's Lantern, Doubloons and Flail dealt ~58% of the damage with 0 kills), at level 1, at the top
    /// rank (milestones and echoes) and evolved. The removed Ramming Prow's ids resolve to the Squid Ink Pots' in a save.
    /// The echo rule is one rule for the manager, the director and the validator.
    /// </summary>
    public sealed class V5M8zCloseTests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        const float Dt = 1f / 60f;

        // ---- Kill credit: a world that kills and credits like EnemySimulation (the last hit's weapon id) ----------------

        sealed class Enemy
        {
            public bool Active = true;
            public Vector3 Start, Position;
            public float Hp, MaxHp;
            public string LastHit;
        }

        sealed class World : IEnemyQuery, IEnemyDamageSink
        {
            public readonly List<Enemy> List = new List<Enemy>();
            public readonly List<string> HitIds = new List<string>();
            public readonly List<string> Kills = new List<string>();
            public float Speed = 1.5f;

            public void Add(Vector3 p, float hp) => List.Add(new Enemy { Start = p, Position = p, Hp = hp, MaxHp = hp });
            public bool Exists => true;
            public int Capacity => List.Count;
            public bool IsActive(int i) => List[i].Active;
            public Vector3 Position(int i) => List[i].Position;
            public Vector3 Velocity(int i) => (Vector3.zero - List[i].Position).normalized * Speed;
            public float Radius(int i) => 0.8f;
            public float Hp(int i) => List[i].Hp;
            public float HpFraction(int i) => List[i].Hp / List[i].MaxHp;
            public bool IsElite(int i) => false;
            public bool IsBoss(int i) => false;
            public bool TryGetNearest(Vector3 from, float range, out int index, bool preferElite = false)
            {
                index = -1;
                var best = float.MaxValue;
                for (var i = 0; i < List.Count; i++)
                {
                    var d = Vector3.Distance(from, List[i].Position);
                    if (!List[i].Active || d > range || d >= best) continue;
                    best = d;
                    index = i;
                }
                return index >= 0;
            }

            /// <summary>EnemySimulation.ApplyDamage's credit: a hit with a weapon id marks the enemy; its death credits the mark.</summary>
            public void ApplyDamage(int index, float amount, bool isCrit, bool showHitFeedback = true, string weaponId = null)
            {
                var e = List[index];
                if (!e.Active) return;
                HitIds.Add(weaponId);
                if (!string.IsNullOrEmpty(weaponId)) e.LastHit = weaponId;
                e.Hp -= amount;
                if (e.Hp <= 0f) { Kills.Add(e.LastHit); Respawn(e); }
            }

            public void ApplyStatus(int index, WeaponStatus status, float strength) { }

            static void Respawn(Enemy e)
            {
                e.Hp = e.MaxHp;
                e.Position = e.Start;
                e.LastHit = null;
            }

            /// <summary>Everyone sails at the ship; one that reaches it starts again from its spot.</summary>
            public void Move(float dt)
            {
                foreach (var e in List)
                {
                    e.Position += (Vector3.zero - e.Position).normalized * Speed * dt;
                    if (e.Position.magnitude < 1.2f) e.Position = e.Start;
                }
            }
        }

        sealed class Ship : IShipState
        {
            public bool Exists => true;
            public Vector3 Position => Vector3.zero;
            public Vector3 Velocity => Vector3.zero;
            public Vector3 MovementForward => Vector3.forward;
            public Vector3 TransformPoint(Vector3 local) => local;
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

        sealed class NoCrit : ICombatRandom { public float CombatFloat() => 0.999f; }

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
            public readonly object Key = new object();
            public string Id, Evolution;
            public int Level = 1;
            public int Count => 1;
            public LoadoutEntry Get(int i) => new LoadoutEntry { Key = Key, WeaponId = Id, EvolutionId = Evolution, Level = Level, DamageBonus = 1f,
                CooldownMult = 1f, SpeedMult = 1f, SizeMult = 1f, DurationMult = 1f, KnockbackMult = 1f };
            public void Mirror(int i, in LoadoutMirror state) { }
        }

        static ConfigSetRuntime Load() => new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));

        /// <summary>Every weapon in the shipped set (19), each at level 1, at its top rank, and evolved where it evolves.</summary>
        static IEnumerable<TestCaseData> Cases()
        {
            using var config = Load();
            foreach (var w in config.All<WeaponConfig>().OrderBy(w => w.weaponId))
            {
                yield return new TestCaseData(w.weaponId, 1, null).SetName($"KillCredit_{w.weaponId}_level1");
                yield return new TestCaseData(w.weaponId, w.maxLevel, null).SetName($"KillCredit_{w.weaponId}_top");
                foreach (var e in config.All<WeaponEvolutionConfig>().Where(e => e.baseWeapon == w))
                    yield return new TestCaseData(w.weaponId, w.maxLevel, e.evolutionId).SetName($"KillCredit_{w.weaponId}_{e.evolutionId}");
            }
        }

        [Test]
        public void TheSetHasNineteenWeapons_AllCovered()
        {
            using var config = Load();
            Assert.AreEqual(19, config.All<WeaponConfig>().Count(), "the kill-credit cases cover every weapon");
        }

        [TestCaseSource(nameof(Cases))]
        public void FiringAWeapon_AndKillingWithIt_CreditsItsKills(string weaponId, int level, string evolutionId)
        {
            using var config = Load();
            var world = new World();
            // Three rings of eight, 4, 8 and 14 m out, sailing in (kegs, trails and orbits meet them; guns reach them).
            foreach (var r in new[] { 4f, 8f, 14f })
                for (var k = 0; k < 8; k++)
                {
                    var a = k * Mathf.PI / 4f + r * 0.1f;
                    world.Add(new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r), 12f);
                }
            var mods = new Mods();
            var ship = new Ship();
            var random = new NoCrit();
            var combat = new WeaponCombat
            {
                Enemies = world, Damage = world, Ship = ship, Mods = mods, Hooks = new Hooks(), Random = random,
                Calculator = new DamageCalculator(mods, random),
                Targeting = new WeaponTargetingService(world, ship),
                Projectiles = new PlayerProjectiles(128, world, world, new Hooks(), null),
                Rules = () => config.All<WeaponRulesConfig>().First(),
            };
            var loadout = new Loadout { Id = weaponId, Level = level, Evolution = evolutionId };
            var manager = new WeaponManager(combat, loadout, new Run(), config);
            var credited = new HashSet<string> { weaponId };
            if (evolutionId != null) credited.Add(evolutionId);

            var time = 0f;
            for (var step = 0; step < 12 * 60; step++)
            {
                time += Dt;
                manager.TickOrbitPositions(time);
                manager.Tick(Dt);
                combat.Projectiles.Tick(Dt);
                world.Move(Dt);
            }

            var label = $"{weaponId} level {level}" + (evolutionId != null ? $" ({evolutionId})" : string.Empty);
            Assert.IsNotEmpty(world.HitIds, label + " hit something");
            var uncredited = world.HitIds.Count(id => string.IsNullOrEmpty(id));
            Assert.AreEqual(0, uncredited, $"{label}: every hit names its weapon ({uncredited} of {world.HitIds.Count} did not)");
            var stray = world.HitIds.Where(id => !credited.Contains(id)).Distinct().ToList();
            CollectionAssert.IsEmpty(stray, label + ": hits name this weapon (or its evolution)");
            Assert.IsNotEmpty(world.Kills, label + " killed something");
            Assert.IsTrue(world.Kills.All(id => id != null && credited.Contains(id)), $"{label}: every kill is credited to it ({string.Join(", ", world.Kills.Distinct())})");
        }

        // ---- The removed Ramming Prow: a save's old ids resolve to the Squid Ink Pots' ---------------------------------

        [TearDown]
        public void ClearAliases() => ContentIdAliases.Register(null);

        [Test]
        public void TheConfigAliases_TheRammingProw_ToTheInkPots()
        {
            using var config = Load();
            var unlocks = config.Single<UnlockConfig>();
            var aliases = unlocks.AliasPairs().ToDictionary(p => p.Key, p => p.Value);
            Assert.AreEqual("squid_ink_pots", aliases["ramming_prow"]);
            Assert.AreEqual("black_tide", aliases["ironclad_ram"]);
            foreach (var kv in aliases)
            {
                var liveWeapon = config.All<WeaponConfig>().Any(w => w.weaponId == kv.Key);
                var liveEvolution = config.All<WeaponEvolutionConfig>().Any(e => e.evolutionId == kv.Key);
                Assert.IsFalse(liveWeapon || liveEvolution, kv.Key + " is gone from the content");
                var target = config.All<WeaponConfig>().Any(w => w.weaponId == kv.Value) || config.All<WeaponEvolutionConfig>().Any(e => e.evolutionId == kv.Value)
                             || config.All<ItemConfig>().Any(i => i.itemId == kv.Value) || config.All<LogbookConfig>().Any(l => l.logbookId == kv.Value);
                Assert.IsTrue(target, kv.Value + " exists");
            }
            var report = new ConfigValidationReport();
            unlocks.Validate(report);
            Assert.IsTrue(report.IsValid, string.Join("\n", report.Errors));
        }

        [Test]
        public void ASaveThatBoughtTheRammingProw_OwnsTheInkPots_WhenItLoads()
        {
            using var config = Load();
            var backend = new MemorySaveBackend();
            ContentIdAliases.Register(null);
            var old = new SaveService(backend);
            old.Meta.marksOwnedContentIds = new[] { "ramming_prow", "fortune" };
            old.Meta.unlockedIds = new[] { "ironclad_ram" };
            old.Meta.selectedUnlockGoal = "ramming_prow";
            old.Save();

            ContentIdAliases.Register(config.Single<UnlockConfig>().AliasPairs());
            var save = new SaveService(backend);
            CollectionAssert.AreEqual(new[] { "squid_ink_pots", "fortune" }, save.Meta.marksOwnedContentIds, "the old id is its new one, nothing dropped");
            CollectionAssert.AreEqual(new[] { "black_tide" }, save.Meta.unlockedIds, "and the evolution's");
            Assert.AreEqual("squid_ink_pots", save.Meta.selectedUnlockGoal);
            Assert.IsTrue(save.HasEffectiveContentOwned("squid_ink_pots"));
            Assert.IsTrue(save.HasEffectiveContentOwned("ramming_prow"), "asking for the old id asks for the new one");
            save.Save(); // the M16 guard migrates the stored copy the same way: nothing reads as dropped (no error)
            Assert.IsFalse(backend.TryLoad("highseas_meta_v1", out var json) && json.Contains("ramming_prow"), "written back under the new id");
            Assert.IsTrue(json.Contains("squid_ink_pots"));
        }

        [Test]
        public void AliasesRegisteredAfterTheSaveLoaded_StillResolveIt()
        {
            using var config = Load();
            var backend = new MemorySaveBackend();
            ContentIdAliases.Register(null);
            var save = new SaveService(backend);
            save.Meta.purchasedIds = new[] { "ramming_prow" };
            save.Save();
            save = new SaveService(backend); // loads before the config (the app's save does)
            Assert.IsFalse(save.HasEffectiveContentOwned("squid_ink_pots"), "no aliases yet");
            ContentIdAliases.Register(config.Single<UnlockConfig>().AliasPairs());
            Assert.IsTrue(save.HasEffectiveContentOwned("squid_ink_pots"), "data under the old id owns the new one");
            save.ApplyContentAliases();
            CollectionAssert.AreEqual(new[] { "squid_ink_pots" }, save.Meta.purchasedIds);
            Assert.AreEqual("squid_ink_pots", ContentIdAliases.Resolve("ramming_prow"));
            Assert.AreEqual("deck_cannons", ContentIdAliases.Resolve("deck_cannons"), "a live id is itself");
        }

        // ---- Echoes: one rule ------------------------------------------------------------------------------------------

        [Test]
        public void EchoMilestones_OnAnOrbitOrAStream_AreAConfigError()
        {
            foreach (var behavior in new[] { ConfigEnums.WeaponBehavior.Orbit, ConfigEnums.WeaponBehavior.ConeStream, ConfigEnums.WeaponBehavior.Aura })
            {
                var w = ScriptableObject.CreateInstance<WeaponConfig>();
                try
                {
                    w.weaponId = "test_weapon";
                    w.behavior = behavior;
                    w.maxLevel = 12;
                    w.stream.tickIntervalSeconds = 0.1f;
                    w.milestones = new[] { new WeaponMilestone { level = 8, echoes = 1 } };
                    var report = new ConfigValidationReport();
                    w.Validate(report);
                    var echoError = report.Errors.Any(e => e.ToString().Contains("echoes"));
                    Assert.AreEqual(!WeaponConfig.Echoes(behavior), echoError, behavior + ": " + string.Join(" | ", report.Errors));
                }
                finally { Object.DestroyImmediate(w); }
            }
            Assert.IsFalse(WeaponConfig.Echoes(ConfigEnums.WeaponBehavior.Orbit));
            Assert.IsFalse(WeaponConfig.Echoes(ConfigEnums.WeaponBehavior.ConeStream));
            Assert.IsTrue(WeaponConfig.Echoes(ConfigEnums.WeaponBehavior.Aura));
        }

        [Test]
        public void TheShippedWeapons_HaveNoEchoesWhereTheyCannotFire()
        {
            using var config = Load();
            foreach (var w in config.All<WeaponConfig>())
            {
                var report = new ConfigValidationReport();
                w.Validate(report);
                Assert.IsTrue(report.IsValid, w.weaponId + ": " + string.Join(" | ", report.Errors));
            }
        }
    }
}
