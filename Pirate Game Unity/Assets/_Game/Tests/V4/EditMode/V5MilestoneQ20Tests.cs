using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Presentation.Hud;
using RoguePirates.V4.Progression;
using RoguePirates.V4.Spawning;
using RoguePirates.V4.Views;
using RoguePirates.V4.Weapons;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M8z (7 Oct, Q20): the nine new weapons' rank 4/8/12 milestones. One data-driven check that every milestone is
    /// applied from its rank and not before (and level 1 is untouched, D20), one that the upgrade card names it, and one
    /// in-play check per weapon on the real configs that the milestone's effect happens (an extra anchor, a second pulse...).
    /// </summary>
    public sealed class V5MilestoneQ20Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        const float Dt = 1f / 60f;

        static readonly string[] NewWeapons =
        {
            "anchor_flail", "chainshot", "siren_bell", "squid_ink_pots", "parrot_squadron", "monkey_gunners", "maelstrom_charge", "lighthouse_lantern", "cursed_doubloons",
        };

        // ---- the rig: one weapon on the real configs, enemies that never die ---------------------------------------------

        sealed class Enemies : IEnemyQuery, IEnemyDamageSink
        {
            public readonly List<Vector3> List = new List<Vector3>();
            public readonly List<(int index, float amount)> Hits = new List<(int, float)>();
            public readonly List<(int index, float fraction, float seconds)> Slows = new List<(int, float, float)>();
            public int Add(Vector3 p) { List.Add(p); return List.Count - 1; }
            public bool Exists => true;
            public int Capacity => List.Count;
            public bool IsActive(int i) => true;
            public Vector3 Position(int i) => List[i];
            public Vector3 Velocity(int i) => Vector3.zero;
            public float Radius(int i) => 0.8f;
            public float Hp(int i) => 1e6f;
            public bool IsElite(int i) => false;
            public bool IsBoss(int i) => false;
            public bool TryGetNearest(Vector3 from, float range, out int index, bool preferElite = false)
            {
                index = -1;
                var best = float.MaxValue;
                for (var i = 0; i < List.Count; i++)
                {
                    var d = Vector3.Distance(from, List[i]);
                    if (d > range || d >= best) continue;
                    best = d;
                    index = i;
                }
                return index >= 0;
            }
            public void ApplyDamage(int index, float amount, bool isCrit, bool showHitFeedback = true, string weaponId = null) => Hits.Add((index, amount));
            public void ApplyStatus(int index, WeaponStatus status, float strength) { }
            public void ApplySlow(int index, float fraction, float seconds) => Slows.Add((index, fraction, seconds));
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
            public string Id;
            public int Level = 1;
            public int Count => 1;
            public LoadoutEntry Get(int i) => new LoadoutEntry { Key = Key, WeaponId = Id, Level = Level, DamageBonus = 1f,
                CooldownMult = 1f, SpeedMult = 1f, SizeMult = 1f, DurationMult = 1f, KnockbackMult = 1f };
            public void Mirror(int i, in LoadoutMirror state) { }
        }

        sealed class Rig : System.IDisposable
        {
            public readonly ConfigSetRuntime Config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            public readonly Enemies Enemies = new Enemies();
            public readonly Loadout Loadout = new Loadout();
            public readonly WeaponCombat Combat;
            public readonly WeaponManager Manager;

            public Rig(string weaponId, int level)
            {
                Loadout.Id = weaponId;
                Loadout.Level = level;
                var ship = new Ship();
                var mods = new Mods();
                var random = new NoCrit();
                Combat = new WeaponCombat
                {
                    Enemies = Enemies, Damage = Enemies, Ship = ship, Mods = mods, Hooks = new Hooks(), Random = random,
                    Calculator = new DamageCalculator(mods, random),
                    Targeting = new WeaponTargetingService(Enemies, ship),
                    Projectiles = new PlayerProjectiles(64, Enemies, Enemies, new Hooks(), null),
                    Rules = () => Config.All<WeaponRulesConfig>().First(),
                };
                Manager = new WeaponManager(Combat, Loadout, new Run(), Config);
                Manager.SyncLoadout();
            }

            public WeaponSlot Slot => Manager.Slots[0];
            public WeaponConfig Weapon => Slot.Config;

            public void Play(float seconds)
            {
                for (var t = 0f; t < seconds; t += Dt)
                {
                    Manager.Tick(Dt);
                    Combat.Projectiles.Tick(Dt);
                }
            }

            public void Dispose() => Config.Dispose();
        }

        static List<WeaponConfig> Weapons(ConfigSetRuntime config) => config.All<WeaponConfig>().Where(w => NewWeapons.Contains(w.weaponId)).ToList();

        static WeaponSlot Resolved(WeaponConfig c, int level)
        {
            var slot = new WeaponSlot { Config = c, Level = level };
            WeaponStatResolver.ApplyEvolution(slot);
            return slot;
        }

        // ---- every milestone, from the data -----------------------------------------------------------------------------

        [Test]
        public void EveryNewWeapon_HasRank4_8_12Milestones_AppliedFromTheirRank_AndNotBefore()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            var weapons = Weapons(config);
            Assert.AreEqual(NewWeapons.Length, weapons.Count, "all nine new weapons are in the set");
            foreach (var c in weapons)
            {
                CollectionAssert.AreEqual(new[] { 4, 8, 12 }, c.milestones.Select(m => m.level), c.weaponId + ": rank 4/8/12 (Q20)");
                // D20: level 1 (and every rank below 4) is the weapon as it was.
                foreach (var level in new[] { 1, 2, 3 })
                {
                    var s = Resolved(c, level);
                    Assert.IsTrue(s.QuantityBonus == 0 && s.SizeMult == 1f && s.DurationMult == 1f && s.ExtraSlow == 0f && s.ExtraPierce == 0
                                  && s.SpinMult == 1f && s.ExtraSlowSeconds == 0f && s.Echoes == 0, $"{c.weaponId} L{level}: no milestone before rank 4");
                }
                foreach (var m in c.milestones)
                {
                    var before = Resolved(c, m.level - 1);
                    var at = Resolved(c, m.level);
                    var tag = $"{c.weaponId} rank {m.level}";
                    Assert.IsFalse(string.IsNullOrWhiteSpace(m.description), tag + ": the card's line");
                    Assert.AreEqual(m.extraQuantity, at.QuantityBonus - before.QuantityBonus, tag + " quantity");
                    Assert.AreEqual(m.sizeMultiplier, at.SizeMult / before.SizeMult, 1e-5f, tag + " size");
                    Assert.AreEqual(m.durationMultiplier, at.DurationMult / before.DurationMult, 1e-5f, tag + " duration");
                    Assert.AreEqual(m.extraSlow, at.ExtraSlow - before.ExtraSlow, 1e-5f, tag + " slow");
                    Assert.AreEqual(m.extraPierce, at.ExtraPierce - before.ExtraPierce, tag + " pierce");
                    Assert.AreEqual(m.spinMultiplier, at.SpinMult / before.SpinMult, 1e-5f, tag + " spin");
                    Assert.AreEqual(m.extraSlowSeconds, at.ExtraSlowSeconds - before.ExtraSlowSeconds, 1e-5f, tag + " slow seconds");
                    Assert.AreEqual(m.echoes, at.Echoes - before.Echoes, tag + " echoes");
                    var changes = m.extraQuantity > 0 || m.sizeMultiplier != 1f || m.durationMultiplier != 1f || m.extraSlow > 0f || m.extraPierce > 0
                                  || m.spinMultiplier != 1f || m.extraSlowSeconds > 0f || m.echoes > 0;
                    Assert.IsTrue(changes, tag + ": the milestone changes something");
                    // Applied at every sync from the record's values, never stacked.
                    using (var rig = new Rig(c.weaponId, m.level))
                    {
                        rig.Manager.SyncLoadout();
                        rig.Manager.SyncLoadout();
                        Assert.AreEqual(at.QuantityBonus, rig.Slot.QuantityBonus, tag + ": not stacked");
                        Assert.AreEqual(at.SizeMult, rig.Slot.SizeMult, 1e-5f, tag + ": not stacked");
                        Assert.AreEqual(at.Echoes, rig.Slot.Echoes, tag + ": not stacked");
                    }
                }
            }
        }

        [Test]
        public void TheUpgradeCard_NamesTheMilestone_ItReaches()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            foreach (var c in Weapons(config))
                foreach (var m in c.milestones)
                {
                    var views = new RunViews { Loadout = new CardLoadout { Owned = c.weaponId, Modifiers = new WeaponModifiersView
                        { Level = m.level - 1, DamageBonus = 1f, CooldownMult = 1f, SpeedMult = 1f, SizeMult = 1f, DurationMult = 1f } } };
                    var reaching = CardStatLines.WeaponUpgrade(c, new UpgradeLine[0], m.level, views);
                    CollectionAssert.Contains(reaching, $"Rank {m.level}: {m.description}", c.weaponId);
                    var earlier = CardStatLines.WeaponUpgrade(c, new UpgradeLine[0], m.level - 1,
                        new RunViews { Loadout = new CardLoadout { Owned = c.weaponId, Modifiers = new WeaponModifiersView
                            { Level = m.level - 2, DamageBonus = 1f, CooldownMult = 1f, SpeedMult = 1f, SizeMult = 1f, DurationMult = 1f } } });
                    Assert.IsFalse(earlier.Any(l => l.StartsWith($"Rank {m.level}:")), $"{c.weaponId}: not on the card before it is reached");
                }
        }

        sealed class CardLoadout : ILoadoutView
        {
            public WeaponModifiersView Modifiers;
            public string Owned;
            public int WeaponSlotCap => 6;
            public int WeaponCount => 0;
            public string WeaponId(int index) => null;
            public string WeaponName(int index) => null;
            public string WeaponIconName(int index) => null;
            public int WeaponLevel(int index) => 0;
            public int ItemCount => 0;
            public string ItemId(int index) => null;
            public string ItemName(int index) => null;
            public int ItemStacks(int index) => 0;
            public bool TryWeaponModifiers(string weaponId, out WeaponModifiersView modifiers)
            {
                modifiers = Modifiers;
                return weaponId == Owned;
            }
        }

        // ---- in play, per weapon --------------------------------------------------------------------------------------

        static float Angle(Vector3 p) => Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;

        [Test]
        public void AnchorFlail_OneMaceAThrow_ThenTwoThreeAndFourAtRanks4_8_12()
        {
            // Hackathon (12 Oct, owner): the flail throws chained maces, 1 a throw at rank 1, at most 4.
            int Thrown(int level)
            {
                using var rig = new Rig("anchor_flail", level);
                for (var i = 0; i < 6; i++) rig.Enemies.Add(new Vector3(-5f + 2f * i, 0f, 5f));
                rig.Manager.Tick(Dt);
                return rig.Manager.Maces.Count;
            }
            var expected = new[] { 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4 };
            for (var level = 1; level <= 12; level++) Assert.AreEqual(expected[level - 1], Thrown(level), $"rank {level}");
        }

        [Test]
        public void Chainshot_Pierce_WiderShot_LongerSlow()
        {
            int Pierce(int level)
            {
                using var rig = new Rig("chainshot", level);
                return rig.Combat.TraitsOf(rig.Slot).Pierce;
            }
            Assert.AreEqual(Pierce(3) + 1, Pierce(4), "rank 4: +1 pierce");
            using (var r7 = new Rig("chainshot", 7))
            using (var r8 = new Rig("chainshot", 8))
                Assert.AreEqual(r7.Slot.SizeMult * 4f / 3f, r8.Slot.SizeMult, 1e-4f, "rank 8: a shot 0.5 m wider (1.5 to 2 m)");

            float SlowSeconds(int level)
            {
                using var rig = new Rig("chainshot", level);
                rig.Enemies.Add(new Vector3(0f, 0f, 5f));
                rig.Play(1f);
                Assert.IsNotEmpty(rig.Enemies.Slows, "the shot slowed");
                return rig.Enemies.Slows[0].seconds;
            }
            Assert.AreEqual(SlowSeconds(11) + 1f, SlowSeconds(12), 1e-4f, "rank 12: the slow lasts 1 s longer");
        }

        [Test]
        public void SirenBell_LongerSlow_WiderPulse_SecondPulse()
        {
            float SlowSeconds(int level)
            {
                using var rig = new Rig("siren_bell", level);
                rig.Enemies.Add(new Vector3(0f, 0f, 3f));
                rig.Manager.Tick(Dt);
                return rig.Enemies.Slows[0].seconds;
            }
            Assert.AreEqual(SlowSeconds(3) + 0.5f, SlowSeconds(4), 1e-4f, "rank 4: the slow lasts 0.5 s longer");

            int Hits(int level, float z, float seconds)
            {
                using var rig = new Rig("siren_bell", level);
                rig.Enemies.Add(new Vector3(0f, 0f, z));
                rig.Play(seconds);
                return rig.Enemies.Hits.Count;
            }
            float radius;
            using (var probe = new Rig("siren_bell", 1)) radius = probe.Weapon.aura.radiusBase;
            Assert.AreEqual(0, Hits(7, radius + 0.5f, 0.1f), "rank 7: an enemy 0.5 m past the pulse is not reached");
            Assert.AreEqual(1, Hits(8, radius + 0.5f, 0.1f), "rank 8: +1 m radius reaches it");
            Assert.AreEqual(1, Hits(11, 3f, 0.6f), "one pulse a cooldown before rank 12");
            Assert.AreEqual(2, Hits(12, 3f, 0.6f), "rank 12: a second pulse 0.35 s later");
            Assert.AreEqual(1, Hits(12, 3f, 0.3f), "and not before 0.35 s");
        }

        [Test]
        public void ParrotSquadron_MoreParrots_LongerLife()
        {
            (int parrots, float life) At(int level)
            {
                using var rig = new Rig("parrot_squadron", level);
                rig.Enemies.Add(new Vector3(0f, 0f, 6f));
                rig.Manager.Tick(Dt);
                return (rig.Combat.Skiffs.Count, rig.Combat.Skiffs.Count > 0 ? rig.Combat.Skiffs[0].LifeTime : 0f);
            }
            Assert.AreEqual(At(3).parrots + 1, At(4).parrots, "rank 4: one more parrot");
            Assert.AreEqual(At(7).life * 1.25f, At(8).life, 1e-3f, "rank 8: parrots stay 25% longer");
            Assert.AreEqual(At(11).parrots + 1, At(12).parrots, "rank 12: one more parrot");
        }

        [Test]
        public void MonkeyGunners_LongerTurrets_SecondAndThirdTurret()
        {
            (int turrets, float life) At(int level)
            {
                using var rig = new Rig("monkey_gunners", level);
                rig.Enemies.Add(new Vector3(0f, 0f, 6f));
                rig.Manager.Tick(Dt);
                return (rig.Combat.Skiffs.Count, rig.Combat.Skiffs.Count > 0 ? rig.Combat.Skiffs[0].LifeTime : 0f);
            }
            Assert.AreEqual(At(3).life * 1.25f, At(4).life, 1e-3f, "rank 4: turrets stay 25% longer");
            Assert.AreEqual(1, At(7).turrets);
            Assert.AreEqual(2, At(8).turrets, "rank 8: a second turret");
            Assert.AreEqual(3, At(12).turrets, "rank 12: a third turret");
        }

        [Test]
        public void MaelstromCharge_WiderWhirlpool_LongerWhirlpool_SecondCharge()
        {
            float Radius(int level)
            {
                using var rig = new Rig("maelstrom_charge", level);
                rig.Enemies.Add(new Vector3(0f, 0f, 8f));
                rig.Manager.Tick(Dt);
                return rig.Combat.Strikes[0].Radius;
            }
            Assert.AreEqual(Radius(3) * 1.2f, Radius(4), 1e-3f, "rank 4: 20% wider");

            (int ticks, float seconds) Pool(int level)
            {
                using var rig = new Rig("maelstrom_charge", level);
                rig.Enemies.Add(new Vector3(0f, 0f, 8f));
                rig.Manager.Tick(Dt);
                rig.Slot.CooldownTimer = 100f;
                rig.Play(rig.Weapon.predictedArea.impactDelaySeconds + 0.05f);
                return (rig.Combat.Whirlpools[0].Ticks, rig.Combat.Whirlpools[0].Left);
            }
            var p7 = Pool(7);
            var p8 = Pool(8);
            Assert.AreEqual(p7.seconds + 1f, p8.seconds, 0.05f, "rank 8: the whirlpool lasts 1 s longer");
            Assert.Greater(p8.ticks, p7.ticks, "and deals more ticks");

            List<Vector3> Charges(int level)
            {
                using var rig = new Rig("maelstrom_charge", level);
                foreach (var z in new[] { 8f, 8.5f, 9f }) rig.Enemies.Add(new Vector3(0f, 0f, z)); // the densest pack
                foreach (var x in new[] { 10f, 10.5f }) rig.Enemies.Add(new Vector3(x, 0f, 0f));   // the next pack
                rig.Manager.Tick(Dt);
                return rig.Combat.Strikes.Select(s => s.Position).ToList();
            }
            Assert.AreEqual(1, Charges(11).Count);
            var two = Charges(12);
            Assert.AreEqual(2, two.Count, "rank 12: a second charge");
            Assert.AreEqual(8.5f, two[0].z, 0.6f, "the first on the densest pack");
            Assert.Greater(two[1].x, 9f, "the second on the next pack, not on the first");
        }

        [Test]
        public void LighthouseLantern_LongerBeams_FasterSweep_OneMoreBeam()
        {
            (int spheres, float reach, float angle) At(int level, float time)
            {
                using var rig = new Rig("lighthouse_lantern", level);
                rig.Manager.TickOrbitPositions(time);
                var hits = rig.Manager.OrbitHitsOf(rig.Slot);
                var tip = hits[rig.Weapon.orbit.beamSegments - 1].Position;
                return (hits.Count, new Vector2(tip.x, tip.z).magnitude, Angle(tip));
            }
            Assert.AreEqual(At(3, 0f).reach * 1.15f, At(4, 0f).reach, 0.01f, "rank 4: 15% longer beams");
            float spin;
            int segments;
            using (var probe = new Rig("lighthouse_lantern", 1)) { spin = probe.Weapon.orbit.angularSpeedDegrees; segments = probe.Weapon.orbit.beamSegments; }
            Assert.AreEqual(Mathf.Repeat(spin * 0.2f, 360f), Mathf.Repeat(At(7, 0.2f).angle - At(7, 0f).angle, 360f), 0.5f);
            Assert.AreEqual(Mathf.Repeat(spin * 1.25f * 0.2f, 360f), Mathf.Repeat(At(8, 0.2f).angle - At(8, 0f).angle, 360f), 0.5f, "rank 8: a 25% faster sweep");
            Assert.AreEqual(At(11, 0f).spheres + segments, At(12, 0f).spheres, "rank 12: one more beam");
        }

        [Test]
        public void CursedDoubloons_TwoMoreBounces_SecondCoin()
        {
            int Hits(int level, float seconds)
            {
                using var rig = new Rig("cursed_doubloons", level);
                for (var k = 0; k < 12; k++) rig.Enemies.Add(new Vector3(0f, 0f, 4f + 3f * k)); // a line to bounce along
                rig.Play(seconds);
                return rig.Enemies.Hits.Count;
            }
            Assert.AreEqual(Hits(3, 0.1f) + 1, Hits(4, 0.1f), "rank 4: +1 bounce");
            Assert.AreEqual(Hits(7, 0.1f) + 1, Hits(8, 0.1f), "rank 8: +1 bounce");
            Assert.AreEqual(Hits(11, 0.6f) * 2, Hits(12, 0.6f), "rank 12: a second coin 0.35 s later");
            Assert.AreEqual(Hits(11, 0.3f), Hits(12, 0.3f), "and not before");
        }

        [Test]
        public void TheEchoes_StopWhenTheWeaponLeaves_AndClearWithTheRun()
        {
            using var rig = new Rig("siren_bell", 12);
            rig.Enemies.Add(new Vector3(0f, 0f, 3f));
            rig.Manager.Tick(Dt);
            Assert.AreEqual(1, rig.Combat.Echoes.Count, "the second pulse is queued");
            rig.Manager.ClearInFlight(false);
            Assert.AreEqual(0, rig.Combat.Echoes.Count, "a cleared run drops it");
        }
    }
}
