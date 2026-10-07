using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Spawning;
using RoguePirates.V4.Weapons;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M4 (plan review #10): each weapon and evolution is fired for 10 s (600 frames of 1/60 s) at a fixed ring of
    /// enemies, at levels 1 and 6, and every hit (enemy, amount, crit) and status is folded into a fingerprint. Until V5 M8z
    /// the fingerprints were compared with goldens recorded before M4 (commit d3184a3d, default data and the v3-parity
    /// preset); since V5 M8z (7 Oct, review) with goldens recorded on today's data (plan/v5/goldens/weapon-goldens.txt).
    /// </summary>
    public sealed class V5WeaponGoldenTests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";

        sealed class Enemy { public bool Active = true; public Vector3 Position; public float Hp = 1e6f; }

        sealed class Enemies : IEnemyQuery, IEnemyDamageSink
        {
            public readonly List<Enemy> List = new List<Enemy>();
            public readonly StringBuilder Log = new StringBuilder();
            public float Total;
            public int Hits, Statuses;
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
            public void ApplyDamage(int index, float amount, bool isCrit, bool showHitFeedback = true, string weaponId = null)
            {
                Hits++;
                Total += amount;
                Log.Append(index).Append(':').Append(amount.ToString("0.###", CultureInfo.InvariantCulture)).Append(isCrit ? "c" : "").Append(';');
            }
            public void ApplyStatus(int index, WeaponStatus status, float strength)
            {
                Statuses++;
                Log.Append(index).Append('s').Append((int)status).Append(';');
            }
            // M4 mechanics, logged so a slow or knockback today's data started to apply would change the fingerprint (the
            // code before M4 never calls them; there they are plain methods).
            public void ApplySlow(int index, float fraction, float seconds) => Log.Append(index).Append("slow").Append(fraction).Append(';');
            public void Knockback(int index, Vector3 from, float meters) => Log.Append(index).Append("knock").Append(meters).Append(';');
        }

        sealed class Ship : IShipState
        {
            public Vector3 Pos;
            public bool Exists => true;
            public Vector3 Position => Pos;
            public Vector3 Velocity => Moving ? Vector3.forward * 4f : Vector3.zero;
            public bool Moving;
            public bool IsBoosting => Moving; // V5 M8z (7 Oct, I8z.5): sailing through the ring boosts (the Ramming Prow needs it)
            public Vector3 MovementForward => Vector3.forward;
            public Vector3 TransformPoint(Vector3 local) => Pos + local;
        }

        sealed class Mods : IWeaponModifiers
        {
            public float AttackSpeed => 1f;
            public float ProjectileSpeed => 1f;
            public float Size => 1f;
            public float Duration => 1f;
            public float Quantity => 0f;
            public float CritChance => 0.05f;
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
            int _procs;
            public void BeginDamageResolution() => _procs = 0;
            public bool TryRegisterProcEvent() { if (_procs >= 12) return false; _procs++; return true; }
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
            readonly System.Random _r = new System.Random(11);
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
            public string Id, Evolution;
            public int Level = 1;
            public readonly object Key = new object();
            public int Count => 1;
            public LoadoutEntry Get(int i) => new LoadoutEntry { Key = Key, WeaponId = Id, EvolutionId = Evolution, Level = Level, DamageBonus = 1f, QuantityBonus = 0,
                CooldownMult = 1f, SpeedMult = 1f, SizeMult = 1f, DurationMult = 1f, KnockbackMult = 1f };
            public void Mirror(int i, in LoadoutMirror state) { }
        }

        static string Fingerprint(ConfigSetRuntime config, string weaponId, string evolutionId, int level, bool moving)
        {
            var enemies = new Enemies();
            // A fixed ring of enemies at 3, 6, 9 and 14 m (four bearings each) that never move or die.
            foreach (var r in new[] { 3f, 6f, 9f, 14f })
                for (var k = 0; k < 4; k++)
                {
                    var a = (k * 90f + r * 7f) * Mathf.Deg2Rad;
                    enemies.List.Add(new Enemy { Position = new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r) });
                }
            var mods = new Mods();
            var ship = new Ship { Moving = moving, Pos = moving ? new Vector3(0f, 0f, -12f) : Vector3.zero };
            var random = new Random();
            var combat = new WeaponCombat
            {
                Enemies = enemies, Damage = enemies, Ship = ship, Mods = mods, Hooks = new Hooks(), Random = random,
                Calculator = new DamageCalculator(mods, random),
                Targeting = new WeaponTargetingService(enemies, ship),
                Projectiles = new PlayerProjectiles(64, enemies, enemies, new Hooks(), null),
                Rules = () => config.All<WeaponRulesConfig>().First(),
            };
            var loadout = new Loadout { Id = weaponId, Evolution = evolutionId, Level = level };
            var manager = new WeaponManager(combat, loadout, new Run(), config);
            manager.SyncLoadout();
            const float dt = 1f / 60f;
            for (var f = 0; f < 600; f++)
            {
                if (moving) ship.Pos += Vector3.forward * (4f * dt); // sails through the ring (the flame needs a heading and enemies ahead)
                manager.TickOrbitPositions(f * dt);
                manager.Tick(dt);
                combat.Projectiles.Tick(dt);
            }
            var log = enemies.Log.ToString();
            unchecked
            {
                var hash = 17;
                foreach (var ch in log) hash = hash * 31 + ch;
                return $"{weaponId}|{evolutionId ?? "-"}|L{level}{(moving ? "|moving" : "")}: hits {enemies.Hits}, statuses {enemies.Statuses}, total {enemies.Total.ToString("0.###", CultureInfo.InvariantCulture)}, log {hash:X8}";
            }
        }

        /// <summary>Every weapon and evolution in the offers, at levels 1 and 6, still and moving.</summary>
        static List<(string key, string fingerprint, bool acted)> All(ConfigSetRuntime config)
        {
            var lines = new List<(string, string, bool)>();
            var weapons = config.All<WeaponConfig>().Where(w => w.databaseOrder >= 0).Select(w => w.weaponId).OrderBy(id => id).ToList();
            var evolutions = config.All<WeaponEvolutionConfig>().Where(e => e.baseWeapon != null).OrderBy(e => e.evolutionId).ToList();
            void Add(string id, string evolution, int level, bool moving)
            {
                var f = Fingerprint(config, id, evolution, level, moving);
                lines.Add((f.Substring(0, f.IndexOf(':')), f, !f.Contains("hits 0, statuses 0,")));
            }
            foreach (var moving in new[] { false, true })
                foreach (var level in new[] { 1, 6 })
                {
                    foreach (var id in weapons) Add(id, null, level, moving);
                    foreach (var e in evolutions) Add(e.baseWeapon.weaponId, e.evolutionId, level, moving);
                }
            return lines;
        }

        /// <summary>
        /// V5 M8z (7 Oct, review): today's goldens. Recorded on the playtest data (saltwind-playtest-r10, the default
        /// config set) after the playtest retuned the weapons and the v3-parity preset was retired (D18); the goldens
        /// recorded before M4 stay in plan/v5/phase-04-weapons/weapon-goldens.txt as M4's evidence.
        /// <para>
        /// To re-record after a deliberate change to a weapon's numbers or behaviour: delete the golden file, run this
        /// test once (it writes the file and reports Inconclusive), check the diff of the file in git names only the
        /// weapons you meant to change, then run it again (it passes).
        /// </para>
        /// </summary>
        static string GoldenPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "plan", "v5", "goldens", "weapon-goldens.txt"));

        /// <summary>
        /// V5 M8z (7 Oct, review): every weapon and evolution in the offers, at levels 1 and 6, still and sailing through
        /// the ring, fires exactly as recorded (hits, statuses, total damage and the hash of every hit in order). Any change
        /// to a weapon's numbers or behaviour fails here until the goldens are re-recorded (see <see cref="GoldenPath"/>).
        /// It also checks that every weapon hits something and that the same inputs give the same hits (no unseeded draw).
        /// </summary>
        [Test]
        public void TodaysWeaponsAndEvolutions_FireExactlyAsRecorded()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            var lines = All(config);
            var silent = lines.GroupBy(l => l.key.Replace("|moving", "")).Where(g => !g.Any(l => l.acted)).Select(g => g.Key).ToList();
            CollectionAssert.IsEmpty(silent, "never hits anything, still or moving");
            var again = All(config);
            CollectionAssert.AreEqual(lines.Select(l => l.fingerprint), again.Select(l => l.fingerprint), "the same hits on a second run");
            Assert.GreaterOrEqual(lines.Count, 2 * 2 * 19, "every weapon, two levels, still and moving");

            var today = lines.Select(l => l.fingerprint).ToList();
            if (!File.Exists(GoldenPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(GoldenPath));
                var header = new[]
                {
                    "# V5WeaponGoldenTests: each weapon and evolution fired for 10 s at a fixed ring of enemies (levels 1 and 6, still and moving).",
                    "# Recorded " + System.DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " on " + SetPath + ". Re-record: delete this file, run the test once, review the diff.",
                };
                File.WriteAllLines(GoldenPath, header.Concat(today));
                Assert.Inconclusive("goldens written to " + GoldenPath + ": review them, then run again");
            }
            var golden = File.ReadAllLines(GoldenPath).Where(l => l.Length > 0 && !l.StartsWith("#")).ToList();
            string Key(string line) => line.Substring(0, line.IndexOf(':'));
            var todayByKey = today.ToDictionary(Key);
            var goldenKeys = new HashSet<string>(golden.Select(Key));
            var problems = new List<string>();
            foreach (var g in golden)
            {
                if (!todayByKey.TryGetValue(Key(g), out var now)) problems.Add("gone:    " + g);
                else if (now != g) problems.Add("was " + g + "\n    now " + now);
            }
            problems.AddRange(today.Where(l => !goldenKeys.Contains(Key(l))).Select(l => "new:     " + l));
            CollectionAssert.IsEmpty(problems, "a weapon fires differently from its golden (re-record only for a deliberate change: see GoldenPath)");
            Assert.AreEqual(golden.Count, today.Count);
        }
    }
}
