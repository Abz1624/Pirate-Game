using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Progression;
using RoguePirates.V4.Spawning;
using RoguePirates.V4.Weapons;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M8z (6 Oct, owner: "check if this is applicable for all the weapons and scenarios"): the server referee caps the
    /// raw damage a second a PvP ship may claim by its level (server/RoguePirates.Api/Playtest/HitReferee.cs, HitRules). This
    /// measures the real worst case with the game's own weapon code and checks the server's numbers sit above it with a
    /// margin, so no legitimate damage is ever refused.
    ///
    /// How: every weapon (and every evolution, though a 1v1 never evolves) is fired by the real <see cref="WeaponManager"/>
    /// for 16 s at one target (the opponent: radius 2.2 m, never dies) at 36 placements (still or sailing; ahead, abeam,
    /// astern; 1.5 to 17 m), and the best placement counts. Its upgrades at each level are the best value of every axis over
    /// 300 random Common upgrade paths taken together (an upper bound no single path reaches). The non-multiplying Logbooks
    /// (Extra Hands +3 quantity, Oversized Shot +50% size, Tidecaller +50% duration) are on at their maximum from level 1,
    /// free. Then for each ship level L (L - 1 level-ups, as the server counts XP barrels) the six weapons and the
    /// multiplying Logbooks (Gunner's Manual +10% damage, Powdermaster's Drill +8% attack speed, Lookout's Glass +5% crit
    /// chance a level, five levels each) are chosen and levelled to maximise the sum, with Rook's +0.5% damage a level
    /// (cap 25%), crits counted as always proccing, and the burn's own ceiling (20 stacks x 0.5 = 10 raw/s) on top.
    /// Bursts: "All Guns!" (one extra firing of every weapon, a faster cutlass spin) is measured as the extra damage it adds,
    /// in seconds of that weapon's sustained damage; kegs left behind hold at most their 30 s life of production.
    /// </summary>
    public sealed class PvpDamageCapTests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";

        // ---- the server's numbers (HitRules in server/RoguePirates.Api/Playtest/HitReferee.cs): keep in step ----
        const double ServerBaseDps = 600, ServerDpsPerLevel = 420, ServerMaxRawDps = 20 * 2500;
        const double ServerInitialBankSeconds = 10, ServerBankSeconds = 50;
        static double ServerSustained(int level) => System.Math.Min(ServerMaxRawDps, ServerBaseDps + ServerDpsPerLevel * (level - 1));
        /// <summary>The server's allowance must be at least this many times the measured worst case.</summary>
        const double RequiredMargin = 1.5;

        const float TargetRadius = 2.2f, Dt = 1f / 60f, Seconds = 16f, Warmup = 1f, ShipSpeed = 6f;
        const int MaxShipLevel = 61; // 60 XP barrels in a 5 minute match (one each 5 s)

        sealed class Target : IEnemyQuery, IEnemyDamageSink
        {
            public Vector3 Pos, Vel;
            public float Damage;
            public bool Exists => true;
            public int Capacity => 1;
            public bool IsActive(int i) => true;
            public Vector3 Position(int i) => Pos;
            public Vector3 Velocity(int i) => Vel;
            public float Radius(int i) => TargetRadius;
            public float Hp(int i) => 1e9f;
            public bool IsElite(int i) => false;
            public bool IsBoss(int i) => false;
            public bool TryGetNearest(Vector3 from, float range, out int index, bool preferElite = false)
            {
                index = Vector3.Distance(from, Pos) <= range ? 0 : -1;
                return index >= 0;
            }
            public void ApplyDamage(int index, float amount, bool isCrit, bool showHitFeedback = true, string weaponId = null) => Damage += amount;
            public void ApplyStatus(int index, WeaponStatus status, float strength) { }
        }

        sealed class Ship : IShipState
        {
            public Vector3 Pos, Vel;
            public bool Exists => true;
            public Vector3 Position => Pos;
            public Vector3 Velocity => Vel;
            public Vector3 MovementForward => Vector3.forward;
            public Vector3 TransformPoint(Vector3 local) => Pos + local;
            public bool IsBoosting => true; // the Ramming Prow strikes only while boosting
        }

        /// <summary>Size and duration at their Logbook maximum, Extra Hands as measured; damage, attack speed and crits
        /// neutral (applied after).</summary>
        sealed class Mods : IWeaponModifiers
        {
            public float ExtraQuantity;
            public float AttackSpeed => 1f;
            public float ProjectileSpeed => 1f;
            public float Size => 1.5f;
            public float Duration => 1.5f;
            public float Quantity => ExtraQuantity;
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

        sealed class NoCrit : ICombatRandom
        {
            public float CombatFloat() => 0.999f;
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

        sealed class OneWeapon : IWeaponLoadout
        {
            public LoadoutEntry Entry;
            public int Count => 1;
            public LoadoutEntry Get(int i) => Entry;
            public void Mirror(int i, in LoadoutMirror state) { }
        }

        sealed class Rng : IOfferRandom
        {
            readonly System.Random _r;
            public Rng(int seed) => _r = new System.Random(seed);
            public float LootFloat() => (float)_r.NextDouble();
        }

        struct Placement
        {
            public bool Moving;
            public Vector3 Offset;
            public override string ToString() => $"{(Moving ? "sailing" : "still")} {Offset.x:0.#},{Offset.z:0.#}";
        }

        static readonly List<Placement> Placements = BuildPlacements();

        static List<Placement> BuildPlacements()
        {
            var list = new List<Placement>();
            foreach (var moving in new[] { false, true })
                foreach (var dir in new[] { Vector3.forward, Vector3.right, Vector3.back })
                    foreach (var d in new[] { 1.5f, 2.5f, 4f, 6f, 8.5f, 11f, 14f, 17f })
                        list.Add(new Placement { Moving = moving, Offset = dir * d });
            return list;
        }

        /// <summary>Raw damage on the target: sustained per second after the warm-up, and the run's total.</summary>
        static (float dps, float total) Measure(ConfigSetRuntime config, LoadoutEntry entry, Placement p, bool fireAll, int extraQuantity)
        {
            var target = new Target();
            var ship = new Ship { Vel = p.Moving ? Vector3.forward * ShipSpeed : Vector3.zero };
            target.Vel = ship.Vel;
            var mods = new Mods { ExtraQuantity = extraQuantity };
            var random = new NoCrit();
            var hooks = new Hooks();
            var combat = new WeaponCombat
            {
                Enemies = target, Damage = target, Ship = ship, Mods = mods, Hooks = hooks, Random = random,
                Calculator = new DamageCalculator(mods, random),
                Targeting = new WeaponTargetingService(target, ship),
                Projectiles = new PlayerProjectiles(256, target, target, hooks, null),
                Rules = () => config.All<WeaponRulesConfig>().First(),
            };
            var manager = new WeaponManager(combat, new OneWeapon { Entry = entry }, new Run(), config);
            manager.SyncLoadout();
            var frames = Mathf.RoundToInt(Seconds / Dt);
            var warm = Mathf.RoundToInt(Warmup / Dt);
            var fireAt = Mathf.RoundToInt(4f / Dt);
            float before = 0f;
            for (var f = 0; f < frames; f++)
            {
                ship.Pos += ship.Vel * Dt;
                target.Pos = ship.Pos + p.Offset;
                if (f == warm) before = target.Damage;
                if (fireAll && f == fireAt) manager.FireAllOnce();
                manager.TickOrbitPositions(f * Dt);
                manager.Tick(Dt);
                combat.Projectiles.Tick(Dt);
            }
            return ((target.Damage - before) / (Seconds - Warmup), target.Damage);
        }

        /// <summary>Per level 1..max: the best value of every upgrade axis over many Common paths (RaisePick's cards).</summary>
        static LoadoutEntry[] UpperUpgrades(ConfigSetRuntime c, WeaponConfig w, int paths = 300)
        {
            var max = Mathf.Max(1, w.maxLevel);
            var best = new LoadoutEntry[max + 1];
            for (var l = 1; l <= max; l++)
                best[l] = new LoadoutEntry { Key = w, WeaponId = w.weaponId, Level = l, DamageBonus = 0f, QuantityBonus = 0, CooldownMult = float.MaxValue,
                    SpeedMult = 0f, SizeMult = 0f, DurationMult = 0f, KnockbackMult = 1f };
            var generator = new OfferGenerator(() => c.Single<ProgressionConfig>(), () => c.Single<OfferWeightsConfig>(), () => OfferGenerator.InDatabaseOrder(c.All<WeaponConfig>()),
                () => OfferGenerator.InDatabaseOrder(c.All<LogbookConfig>()), () => c.All<WeaponEvolutionConfig>().ToList(), () => false);
            for (var path = 0; path < paths; path++)
            {
                var rng = new Rng(9000 + path * 31 + w.weaponId.GetHashCode());
                var loadout = new ProgressionLoadout(id => id == w.weaponId ? w : null, _ => null) { NewWeaponDamageBonus = c.Single<ProgressionConfig>().newWeaponDamageBonus };
                loadout.EnsureSlotCapacity(1);
                loadout.Equip(w.weaponId);
                var lw = loadout.Weapons[0];
                for (var guard = 0; guard < 40; guard++)
                {
                    var l = Mathf.Clamp(lw.Level, 1, max);
                    var b = best[l];
                    b.DamageBonus = Mathf.Max(b.DamageBonus, lw.DamageBonus);
                    b.QuantityBonus = Mathf.Max(b.QuantityBonus, lw.QuantityBonus);
                    b.CooldownMult = Mathf.Min(b.CooldownMult, lw.CooldownMult);
                    b.SpeedMult = Mathf.Max(b.SpeedMult, lw.SpeedMult);
                    b.SizeMult = Mathf.Max(b.SizeMult, lw.SizeMult);
                    b.DurationMult = Mathf.Max(b.DurationMult, lw.DurationMult);
                    best[l] = b;
                    if (lw.Level >= max) break;
                    var card = generator.WeaponUpgradeCard(w, lw.Level, 0, rng);
                    loadout.ApplyUpgradePackage(0, card.Lines, Mathf.Max(1, card.NextLevel - card.CurrentLevel));
                }
            }
            // a level no path stopped at (none with Common cards) takes the level below's values
            for (var l = 2; l <= max; l++) if (best[l].DamageBonus <= 0f) { var lv = best[l - 1]; lv.Level = l; best[l] = lv; }
            return best;
        }

        sealed class WeaponPower
        {
            public string Id;
            /// <summary>Sustained raw damage a second by Extra Hands quantity (0..3) and weapon level (1..12).</summary>
            public float[,] Dps;
            public int MaxLevel;
            /// <summary>The extra a "fire all" adds, in seconds of the weapon's sustained damage.</summary>
            public float FireAllSeconds;
            public string Best;
        }

        const int MaxExtraQuantity = 3; // Extra Hands: +1 a level, three levels

        static List<WeaponPower> MeasureAll(ConfigSetRuntime config, StringBuilder log)
        {
            var powers = new List<WeaponPower>();
            var weapons = config.All<WeaponConfig>().Where(w => w != null && !string.IsNullOrEmpty(w.weaponId) && w.databaseOrder >= 0
                && Foundation.Combat.WeaponFeatureFlags.IsWeaponEnabled(w.weaponId)).OrderBy(w => w.databaseOrder).ToList();
            foreach (var w in weapons)
            {
                var ups = UpperUpgrades(config, w);
                var max = ups.Length - 1;
                // the best three placements at levels 1 and 12 (most quantity) are tried at every level and quantity
                var bestAt = new List<Placement>();
                foreach (var probe in new[] { 1, max })
                {
                    var ranked = Placements.Select(p => (p, Measure(config, ups[probe], p, false, MaxExtraQuantity).dps)).OrderByDescending(x => x.dps).Take(3).Select(x => x.p);
                    foreach (var p in ranked) if (!bestAt.Contains(p)) bestAt.Add(p);
                }
                var power = new WeaponPower { Id = w.weaponId, Dps = new float[MaxExtraQuantity + 1, max + 1], MaxLevel = max };
                for (var q = 0; q <= MaxExtraQuantity; q++)
                    for (var l = 1; l <= max; l++)
                        power.Dps[q, l] = bestAt.Max(p => Measure(config, ups[l], p, false, q).dps);
                var top = bestAt.Select(p => (p, d: Measure(config, ups[max], p, false, MaxExtraQuantity).dps)).OrderByDescending(x => x.d).First().p;
                var line = new StringBuilder($"{w.weaponId,-20} L1 {power.Dps[1, 1],7:0.0}  L6 {power.Dps[1, Mathf.Min(6, max)],7:0.0}  L12 {power.Dps[1, max],7:0.0}  L12+3 hands {power.Dps[MaxExtraQuantity, max],7:0.0} raw/s");
                // evolutions (not dealt in a 1v1 today, where level-ups never offer one; counted at the top level anyway)
                foreach (var e in config.All<WeaponEvolutionConfig>().Where(e => e.baseWeapon == w))
                {
                    var evolved = ups[max];
                    evolved.EvolutionId = e.evolutionId;
                    for (var q = 0; q <= MaxExtraQuantity; q++)
                    {
                        var d = Placements.Max(p => Measure(config, evolved, p, false, q).dps);
                        power.Dps[q, max] = Mathf.Max(power.Dps[q, max], d);
                    }
                    line.Append($"; {e.evolutionId} {power.Dps[1, max]:0.0}/{power.Dps[MaxExtraQuantity, max]:0.0}");
                }
                // "All Guns!": the extra over 12 s after a fire-all at 4 s, at the top level's best placement
                var (sustained, plain) = Measure(config, ups[max], top, false, MaxExtraQuantity);
                var (_, withFire) = Measure(config, ups[max], top, true, MaxExtraQuantity);
                power.FireAllSeconds = sustained > 0f ? Mathf.Max(0f, withFire - plain) / sustained : 0f;
                power.Best = top.ToString();
                line.Append($"; fire-all +{power.FireAllSeconds:0.0} s; best {power.Best}");
                log.AppendLine(line.ToString());
                powers.Add(power);
            }
            return powers;
        }

        /// <summary>The worst build's raw damage a second at each ship level (index = level).</summary>
        static double[] WorstByLevel(List<WeaponPower> powers, int weaponsInDraft = 6)
        {
            const int LogbookMax = 4; // levels above the first, for Gunner's Manual, Powdermaster's Drill and Lookout's Glass
            var maxUps = MaxShipLevel - 1;
            var neg = double.NegativeInfinity;
            // knapsack per quantity: the best sum of six weapons with k weapon level-ups in total
            var weapons = new double[MaxExtraQuantity + 1, maxUps + 1];
            for (var q = 0; q <= MaxExtraQuantity; q++)
            {
                var dp = new double[weaponsInDraft + 1, maxUps + 1];
                for (var n = 0; n <= weaponsInDraft; n++) for (var k = 0; k <= maxUps; k++) dp[n, k] = n == 0 ? 0 : neg;
                foreach (var p in powers)
                    for (var n = weaponsInDraft; n >= 1; n--)
                        for (var k = maxUps; k >= 0; k--)
                            for (var u = 0; u <= System.Math.Min(k, p.MaxLevel - 1); u++)
                            {
                                var prev = dp[n - 1, k - u];
                                if (double.IsNegativeInfinity(prev)) continue;
                                var v = prev + p.Dps[q, 1 + u];
                                if (v > dp[n, k]) dp[n, k] = v;
                            }
                for (var k = 0; k <= maxUps; k++) weapons[q, k] = System.Math.Max(k > 0 ? weapons[q, k - 1] : neg, dp[weaponsInDraft, k]);
            }
            var worst = new double[MaxShipLevel + 1];
            for (var level = 1; level <= MaxShipLevel; level++)
            {
                var ups = level - 1;
                var best = 0.0;
                for (var q = 0; q <= MaxExtraQuantity; q++) // 0: not drafted; 1: drafted (level 1, free); 2, 3: one, two level-ups
                    for (var g = 0; g <= LogbookMax; g++)
                        for (var a = 0; a <= LogbookMax; a++)
                            for (var cc = 0; cc <= LogbookMax; cc++)
                            {
                                var left = ups - g - a - cc - System.Math.Max(0, q - 1);
                                if (left < 0) continue;
                                var damage = 1 + 0.1 * (1 + g) + System.Math.Min(0.25, 0.005 * level);
                                var speed = 1 + 0.08 * (1 + a);
                                var crit = 1 + System.Math.Min(1.0, 0.05 + 0.05 * (1 + cc)) * 0.5;
                                best = System.Math.Max(best, weapons[q, left] * damage * speed * crit);
                            }
                worst[level] = best + 10; // + the burn ceiling
            }
            return worst;
        }

        [Test]
        public void ServerDamageCap_IsAboveTheMeasuredWorstCase_AtEveryLevel()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            var log = new StringBuilder();
            var powers = MeasureAll(config, log); // every weapon (V5 M8z: was the retired simulator's content with all weapons owned)
            var worst = WorstByLevel(powers);
            log.AppendLine("level | worst raw/s | server sustained | margin");
            var failures = new List<string>();
            for (var level = 1; level <= MaxShipLevel; level++)
            {
                var allowed = ServerSustained(level);
                var margin = allowed / worst[level];
                if (level <= 15 || level % 5 == 0) log.AppendLine($"{level,5} | {worst[level],11:0} | {allowed,16:0} | x{margin:0.00}");
                if (margin < RequiredMargin) failures.Add($"level {level}: worst {worst[level]:0} raw/s, server allows {allowed:0} (x{margin:0.00} < x{RequiredMargin})");
            }
            // bursts: a fire-all is banked; the bank at the start and its size cover the largest
            var fireAll = powers.Max(p => p.FireAllSeconds);
            log.AppendLine($"fire-all burst: at most {fireAll:0.0} s of sustained damage (bank at start {ServerInitialBankSeconds} s, size {ServerBankSeconds} s)");
            var kegLife = config.All<WeaponConfig>().Where(w => w.behavior == ConfigEnums.WeaponBehavior.Mine).Select(w => w.mine.lifeSeconds).DefaultIfEmpty(0f).Max();
            log.AppendLine($"longest keg life: {kegLife:0} s; weapons measured: {powers.Count}");
            TestContext.WriteLine(log.ToString());
            Debug.Log("PvpDamageCapTests\n" + log);
            Assert.AreEqual(19, powers.Count, "every weapon measured");
            if (fireAll * RequiredMargin > ServerInitialBankSeconds) failures.Add($"a fire-all adds {fireAll:0.0} s of damage; the starting bank is {ServerInitialBankSeconds} s");
            if ((kegLife + fireAll) * RequiredMargin > ServerBankSeconds) failures.Add($"kegs ({kegLife} s) and a fire-all ({fireAll:0.0} s) can be stored; the bank holds {ServerBankSeconds} s");
            CollectionAssert.IsEmpty(failures, log.ToString());
        }
    }
}
