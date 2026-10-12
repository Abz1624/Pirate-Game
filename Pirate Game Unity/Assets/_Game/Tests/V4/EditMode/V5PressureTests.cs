using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.DevTools.Balance;
using RoguePirates.V4.Difficulty;
using RoguePirates.V4.Enemies;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M3d (owner, 1 Oct 2026, notes e, h, k and the sketch): pressure from enemy count, not time-scaled HP or damage;
    /// a peak-and-valley crowd from keyframes; stronger types late; an enemy that looks the same behaves the same.
    /// V5 M8z (I8z.7): the two checks that ran the in-engine simulator (the crowd's 3:45 peak, the boss fight's length at
    /// an early and a late summon) were retired with it; the JavaScript model (plan/playtest/montecarlo) reports both.
    /// </summary>
    public sealed class V5PressureTests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        static ConfigSet Set => AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);

        static string Value(V4ConfigAsset c, string path) => ConfigFieldWalker.Format(ConfigPath.Get(c, path));
        static string Normal(V4ConfigAsset c, string path, string text)
        {
            Assert.IsTrue(ConfigFieldWalker.TryParse(ConfigPath.TypeAt(c, path), text, out var v), path + ": " + text);
            return ConfigFieldWalker.Format(v);
        }

        // V5 M8z (7 Oct, I8z.5): the playtest replaced M3d's rules (D18 P1): enemy HP grows with time again and the keyframes,
        // hordes and late types were retuned. The checks that the data held M3d's values (CountPressure's tables) and that
        // HP and damage were flat are retired; what stays is that the curve never makes an enemy weaker with time.

        [Test]
        public void AnEnemy_NeverGetsWeakerWithTime()
        {
            using var rt = new ConfigSetRuntime(Set);
            var d = rt.Single<DifficultyCurveConfig>();
            var deadline = rt.Single<WaveTimelineConfig>().stageDeadlineSeconds / 60f;
            for (var minute = 0f; minute < deadline; minute += 0.5f)
            {
                Assert.LessOrEqual(CurveEvaluator.EnemyHp(d, 20f, minute, 1, 0), CurveEvaluator.EnemyHp(d, 20f, minute + 0.5f, 1, 0) + 1e-4f, $"HP at {minute} min");
                Assert.LessOrEqual(CurveEvaluator.EnemyDamage(d, 8f, minute, 1, 0), CurveEvaluator.EnemyDamage(d, 8f, minute + 0.5f, 1, 0) + 1e-4f, $"damage at {minute} min");
            }
            var director = rt.Single<DirectorConfig>();
            Assert.IsFalse(director.toughnessLever || director.capHpLever || director.focusLever, "the director's HP and damage levers are off");
            Assert.AreEqual((1f, 2f), (rt.Single<ExperimentConfig>().enemyHpMultiplier, rt.Single<ExperimentConfig>().enemyDamageMultiplier), "M3c's interim scale is gone (Hackathon 12 Oct, owner: enemy damage x2)");
        }

        [Test]
        public void TheFloor_FollowsALine_BetweenKeyframes()
        {
            var flow = new FlowSettings { smoothWaves = true };
            flow.waves.Add(new MinuteWave { startSeconds = 0f, minAliveShare = 0.2f, rateMultiplier = 1f });
            flow.waves.Add(new MinuteWave { startSeconds = 40f, minAliveShare = 0.6f, rateMultiplier = 2f });
            Assert.IsTrue(flow.Sample(10f, out var share, out var rate));
            Assert.AreEqual((0.3f, 1.25f), (share, rate));
            Assert.IsTrue(flow.Sample(60f, out share, out rate));
            Assert.AreEqual((0.6f, 2f), (share, rate), "past the last keyframe it holds");
            flow.smoothWaves = false;
            flow.Sample(10f, out share, out _);
            Assert.AreEqual(0.2f, share, "off: the M1 step");
        }

        [Test]
        public void TheCurve_RisesOverTheVoyage_InPeaksAndValleys()
        {
            // Review M3d #1 (owner's sketch): peaks and valleys growing upwards; nothing clamped at the cap.
            // V5 M8z (7 Oct, I8z.5): the playtest moved the keyframes to the 15-minute voyage, so the rule reads them
            // instead of M3d's times (3:35, 4:00-7:00).
            using var rt = new ConfigSetRuntime(Set);
            var waves = rt.Single<WaveTimelineConfig>().flow.waves.OrderBy(w => w.startSeconds).ToList();
            var half = waves.Count / 2;
            Assert.Less(waves.Take(half).Max(w => w.minAliveShare), waves.Skip(half).Max(w => w.minAliveShare), "the late peaks are higher than the early ones");
            var drops = 0;
            for (var i = 1; i < waves.Count; i++) if (waves[i].minAliveShare < waves[i - 1].minAliveShare) drops++;
            Assert.GreaterOrEqual(drops, 2, "valleys after peaks");
            Assert.That(waves.Select(w => w.minAliveShare), Has.All.LessThan(1f), "no keyframe is clamped at the cap");
        }

        [Test]
        public void HordeShips_AreTheOrdinaryShipsTheyLookLike()
        {
            // Review M3d #6 (note k): a horde is more of the same ship, not a faster or weaker copy of it.
            using var rt = new ConfigSetRuntime(Set);
            foreach (var h in rt.Single<WaveTimelineConfig>().flow.hordes)
                Assert.AreEqual((1f, 1f, 1f), (h.speedMultiplier, h.hpMultiplier, h.damageMultiplier), h.hordeId);
        }

        [Test]
        public void ThePerMinuteTargets_ComeFromTheKeyframes()
        {
            // Review M3d #2: derived from the design (the keyframes' mean floor), not fitted to the simulator; kills keep M2's bands.
            using var rt = new ConfigSetRuntime(Set);
            var targets = rt.Single<BalanceTargetsConfig>();
            var release = rt.Single<WaveTimelineConfig>().flow.release;
            Assert.AreEqual((CountPressure.HordeLullSeconds, CountPressure.HordeReleaseDelaySeconds, CountPressure.HordeReleaseSeconds, CountPressure.ReleaseFloorMultiplier),
                (release.preHordeLullSeconds, release.hordeReleaseDelaySeconds, release.afterHordeSeconds, release.floorMultiplier), "the targets use the data's horde windows");
            for (var m = 0; m < 10; m++)
            {
                var mean = CountPressure.MeanFloorShare(m * 60f, m * 60f + 60f);
                Assert.AreEqual(mean * 0.75f, targets.minutes[m].aliveShare.min, 0.006f, $"minute {m} alive min");
                Assert.Greater(targets.minutes[m].aliveShare.max, targets.minutes[m].aliveShare.min, $"minute {m}: M2's ceiling above the floor");
            }
            Assert.That(CountPressure.TargetEdits.Select(e => e.path), Has.None.Contains("killsPerMinute").And.None.Contains("aliveShare.max"), "kill bands and crowd ceilings unchanged");
            for (var m = 1; m <= 3; m++) Assert.AreEqual(0.05f, targets.minutes[m].damageTakenShare.min, 1e-5f, $"hull lost in minute {m} (1:00-4:00)");
        }

        [Test]
        public void TheSpawnersCombatOverrides_AreOff_AndFollowTheirSwitch()
        {
            using var rt = new ConfigSetRuntime(Set);
            Assert.IsFalse(new EnemySpawnParamResolver(null, rt).SpawnerCombatOverrides, "an enemy attacks by its own profile in every band");
            rt.Single<WaveTimelineConfig>().spawnerCombatOverrides = true; // V3 (was the v3-parity preset; V5 M8z, 7 Oct, I8z.5)
            Assert.IsTrue(new EnemySpawnParamResolver(null, rt).SpawnerCombatOverrides, "V3");
        }

        /// <summary>The meshes a model draws (two prefabs with the same meshes are the same ship, whatever their materials).</summary>
        static string Meshes(GameObject prefab)
        {
            if (prefab == null) return "cube";
            var meshes = prefab.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh)
                .Concat(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.sharedMesh))
                .Where(m => m != null).Select(m => AssetDatabase.GetAssetPath(m) + "#" + m.name).Distinct().OrderBy(x => x);
            return string.Join(";", meshes);
        }

        /// <summary>How it attacks.</summary>
        static string Attack(ResolvedEnemyStats s)
        {
            var c = s.combat;
            return c == null ? s.behavior.ToString() : $"{c.archetype}|{c.attackPattern}|{c.projectileCount}|{c.attackCooldown:0.0}|{c.attackRange:0}|{c.chargeWindUp:0.0}";
        }

        /// <summary>Two looks a player tells apart: other meshes, an elite's size and glow, a clearly different colour (one
        /// channel 0.25 apart) or a clearly different size (15%).</summary>
        static bool Distinct((string meshes, Color tint, float size, bool elite) a, (string meshes, Color tint, float size, bool elite) b)
        {
            if (a.meshes != b.meshes || a.elite != b.elite) return true;
            var colour = Mathf.Max(Mathf.Abs(a.tint.r - b.tint.r), Mathf.Abs(a.tint.g - b.tint.g), Mathf.Abs(a.tint.b - b.tint.b));
            var size = Mathf.Max(a.size, b.size) / Mathf.Max(0.01f, Mathf.Min(a.size, b.size));
            return colour >= 0.25f || size >= 1.15f;
        }

        [Test]
        public void EnemiesThatLookTheSame_AttackTheSame()
        {
            // Owner note k: "the same enemy shouldn't fire 1 cannonball vs 3 cannonballs from 1-2 min to 6-7 min". Every enemy
            // that can spawn (band pools, every spawner's anchor, the hordes, the registry elites) is compared by what the
            // player sees: the meshes (Cannonboat and CannonboatNavy share theirs), the tint, the drawn size, elite or not.
            using var rt = new ConfigSetRuntime(Set);
            var timeline = rt.Single<WaveTimelineConfig>();
            var ids = new HashSet<string>();
            foreach (var p in timeline.phases)
            {
                foreach (var pool in p.pools) foreach (var e in pool.entries) if (!string.IsNullOrEmpty(e.enemyId)) ids.Add(e.enemyId);
                if (p.spawner?.anchor != null) ids.Add(p.spawner.anchor.enemyId);
                if (p.alternateSpawner?.anchor != null) ids.Add(p.alternateSpawner.anchor.enemyId);
            }
            foreach (var sp in rt.All<EnemySpawnerConfig>()) if (sp.anchor != null) ids.Add(sp.anchor.enemyId);
            foreach (var h in timeline.flow.hordes) if (h.archetype != null) ids.Add(h.archetype.enemyId);
            var enemies = new List<(ResolvedEnemyStats s, (string, Color, float, bool) look)>();
            foreach (var id in ids)
            {
                var a = rt.All<EnemyArchetypeConfig>().FirstOrDefault(x => x.enemyId == id);
                var v = rt.All<EnemyVariantConfig>().FirstOrDefault(x => x.variantId == id && x.kind == VariantKind.DatabaseAlias);
                var s = a != null ? EnemyStatResolver.Resolve(a) : v != null ? EnemyStatResolver.Resolve(v) : null;
                if (s == null) continue;
                enemies.Add((s, (Meshes(s.visualPrefab), s.SeenColor, s.visualScale * s.viewSize, s.isElite))); // V5 M8z (7 Oct): the colour players see (type colour, else tint)
            }
            foreach (var v in rt.All<EnemyVariantConfig>().Where(x => x.kind == VariantKind.RegistryElite))
            {
                var s = EnemyStatResolver.Resolve(v);
                enemies.Add((s, (Meshes(s.visualPrefab), s.SeenColor, s.visualScale * s.viewSize, s.isElite)));
            }
            Assert.GreaterOrEqual(enemies.Count, 20, "the voyage's enemies, its spawners' and the elites");
            var clashes = new List<string>();
            for (var i = 0; i < enemies.Count; i++)
                for (var j = i + 1; j < enemies.Count; j++)
                    if (!Distinct(enemies[i].look, enemies[j].look) && Attack(enemies[i].s) != Attack(enemies[j].s))
                        clashes.Add($"{enemies[i].s.id} ({Attack(enemies[i].s)}) and {enemies[j].s.id} ({Attack(enemies[j].s)})");
            CollectionAssert.IsEmpty(clashes, "same look, different attack");
        }

        [Test]
        public void TheLookRule_WouldHaveCaughtTheOwnersExample()
        {
            // Before M3d the gunner cog (three shots) wore CannonboatNavy, which draws the cannon boat's (one shot) meshes.
            using var rt = new ConfigSetRuntime(Set);
            var cannonboat = EnemyStatResolver.Resolve(rt.All<EnemyArchetypeConfig>().Single(a => a.enemyId == "cannonboat"));
            var cog = EnemyStatResolver.Resolve(rt.All<EnemyArchetypeConfig>().Single(a => a.enemyId == "gunner_cog"));
            Assert.AreEqual(Meshes(cannonboat.visualPrefab), Meshes(cog.visualPrefab), "the same ship");
            Assert.IsFalse(Distinct((Meshes(cannonboat.visualPrefab), Color.white, 1f, false), (Meshes(cog.visualPrefab), Color.white, 1f, false)), "untinted they read the same");
            Assert.IsTrue(Distinct((Meshes(cannonboat.visualPrefab), cannonboat.visualTint, cannonboat.visualScale * cannonboat.viewSize, false),
                (Meshes(cog.visualPrefab), cog.visualTint, cog.visualScale * cog.viewSize, false)), "M3d's tint and size tell them apart");
        }
    }
}
