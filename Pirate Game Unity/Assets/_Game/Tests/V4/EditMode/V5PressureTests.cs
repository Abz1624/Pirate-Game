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

        [Test]
        public void TheData_HoldsEveryM3dValue()
        {
            using var rt = new ConfigSetRuntime(Set);
            var wrong = CountPressure.Edits.Where(e => Value(rt.Get<V4ConfigAsset>(e.configId), e.path) != Normal(rt.Get<V4ConfigAsset>(e.configId), e.path, e.value))
                .Select(e => e.configId + "." + e.path).ToList();
            CollectionAssert.IsEmpty(wrong);
            var t = CountPressure.Tuning.Default;
            Assert.AreEqual(t.BossBaseHp, rt.All<EnemyArchetypeConfig>().Single(a => a.enemyId == "barnacle_king").stats.baseHP);
            var timeline = rt.Single<WaveTimelineConfig>();
            Assert.IsTrue(timeline.flow.smoothWaves);
            Assert.AreEqual(CountPressure.Keyframes.Length, timeline.flow.waves.Count, "the keyframes");
            for (var i = 0; i < CountPressure.Keyframes.Length; i++)
            {
                var k = CountPressure.Keyframes[i];
                var w = timeline.flow.waves[i];
                Assert.AreEqual((k.t, Mathf.Clamp01(k.share * t.FloorScale), k.rate * t.RateScale, k.interval, k.perTick),
                    (w.startSeconds, w.minAliveShare, w.rateMultiplier, w.topUpIntervalSeconds, w.maxTopUpPerTick), $"keyframe {i} (the targets are built from these)");
            }
            Assert.AreEqual(CountPressure.Hordes.Select(h => h.t), timeline.flow.hordes.Select(h => h.timeSeconds).OrderBy(x => x), "the hordes on the peaks");
            Assert.AreEqual("CannonboatNavy", rt.All<EnemyArchetypeConfig>().Single(a => a.enemyId == "gunner_cog").visual.visualPrefab.name, "the gunner cog keeps its light cog model (review: not the Brigg)");
            foreach (var (id, hp, damage) in CountPressure.LateTypes)
                switch (rt.Get<V4ConfigAsset>(id))
                {
                    case EnemyArchetypeConfig a: Assert.AreEqual((hp * t.LateHp, damage * t.LateDamage), (a.stats.baseHP, a.stats.baseDamage), id); break;
                    case EnemyVariantConfig v: Assert.AreEqual((hp * t.LateHp, damage * t.LateDamage), (v.baseHP.value, v.baseDamage.value), id); break;
                }
            var tender = rt.Get<V4ConfigAsset>(CountPressure.RepairTender) as EnemyVariantConfig;
            Assert.AreEqual((true, CountPressure.RepairTenderDamage), (tender.baseDamage.enabled, tender.baseDamage.value), "the repair tender keeps the barge's old damage");
            Assert.AreEqual(t.Band3Specialists, timeline.phases.Single(p => p.phaseId == "band_3").pools[1].share, 1e-5f);
            Assert.AreEqual(t.Band4Specialists, timeline.phases.Single(p => p.phaseId == "band_4").pools[1].share, 1e-5f);
            Assert.AreEqual(CountPressure.Hordes.Select(h => h.count), timeline.flow.hordes.OrderBy(h => h.timeSeconds).Select(h => h.count), "the horde sizes");
            var targets = rt.Get<V4ConfigAsset>("balance_targets.saltwind");
            CollectionAssert.IsEmpty(CountPressure.TargetEdits.Where(e => Value(targets, e.path) != Normal(targets, e.path, e.value)).Select(e => e.path).ToList(), "the per-minute targets");
        }

        [Test]
        public void AnEnemy_HasTheSameHpAndDamage_At1And8Minutes()
        {
            using var rt = new ConfigSetRuntime(Set);
            var d = rt.Single<DifficultyCurveConfig>();
            Assert.AreEqual(CurveEvaluator.EnemyHp(d, 20f, 1f, 1, 0), CurveEvaluator.EnemyHp(d, 20f, 8f, 1, 0), 1e-4f, "no HP growth with time");
            Assert.AreEqual(CurveEvaluator.EnemyDamage(d, 8f, 1f, 1, 0), CurveEvaluator.EnemyDamage(d, 8f, 8f, 1, 0), 1e-4f, "no damage growth with time");
            Assert.AreEqual(d.ordinaryHp.Evaluate(0f), d.ordinaryHp.Evaluate(6f), 1e-5f, "ordinary time is flat too");
            var director = rt.Single<DirectorConfig>();
            Assert.IsFalse(director.toughnessLever || director.capHpLever || director.focusLever, "the director's HP and damage levers are off");
            Assert.IsTrue(director.threatRateLever && director.eliteLever, "it keeps its count lever and elite chance");
            Assert.AreEqual((1f, 1f), (rt.Single<ExperimentConfig>().enemyHpMultiplier, rt.Single<ExperimentConfig>().enemyDamageMultiplier), "M3c's interim scale is gone");
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
        public void TheCurve_KeepsTheSketchesOrder()
        {
            // Review M3d #1 (owner's sketch): small early peaks, a bigger one at 3:35 above the whole valley after it
            // (4:00-7:00), then peaks rising to the climax; nothing clamped at the cap.
            using var rt = new ConfigSetRuntime(Set);
            var waves = rt.Single<WaveTimelineConfig>().flow.waves;
            float At(float t) => waves.Single(w => Mathf.Approximately(w.startSeconds, t)).minAliveShare;
            var bigPeak = At(225f);
            Assert.That(waves.Where(w => w.startSeconds < 225f).Select(w => w.minAliveShare), Has.All.LessThan(bigPeak), "the early peaks are smaller");
            Assert.That(waves.Where(w => w.startSeconds >= 240f && w.startSeconds <= 420f).Select(w => w.minAliveShare), Has.All.LessThan(bigPeak), "the long valley stays under the 3:35-3:45 peak");
            Assert.Less(At(250f), At(480f), "the valley floor rises (growing upwards)");
            Assert.LessOrEqual(At(450f), At(575f), "the second late peak is at least the first");
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
        public void TheSpawnersCombatOverrides_AreOff_AndOnUnderTheParityPreset()
        {
            using var rt = new ConfigSetRuntime(Set);
            Assert.IsFalse(new EnemySpawnParamResolver(null, rt).SpawnerCombatOverrides, "an enemy attacks by its own profile in every band");
            using var parity = V4DifficultyM10Tests.Parity();
            Assert.IsTrue(new EnemySpawnParamResolver(null, parity).SpawnerCombatOverrides, "V3");
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

        [Test]
        public void TheParityPreset_TakesM3dBack()
        {
            using var parity = V4DifficultyM10Tests.Parity();
            var t = parity.Single<WaveTimelineConfig>();
            Assert.IsFalse(t.flow.smoothWaves);
            Assert.IsTrue(t.spawnerCombatOverrides);
            var d = parity.Single<DifficultyCurveConfig>();
            Assert.Greater(d.enemyHpPerMinute.c1, 0f, "V3's HP growth");
            Assert.AreEqual(22f, parity.All<EnemyArchetypeConfig>().Single(a => a.enemyId == "manta_ray").stats.baseHP, "the late types' V3 stats");
            Assert.AreEqual(Color.white, parity.All<EnemyArchetypeConfig>().Single(a => a.enemyId == "boarding_barge").visual.tint);
            Assert.AreEqual(800f, parity.All<EnemyArchetypeConfig>().Single(a => a.enemyId == "barnacle_king").stats.baseHP, "V3's boss");
            var director = parity.Single<DirectorConfig>();
            Assert.IsTrue(director.toughnessLever && director.capHpLever && director.focusLever);
        }
    }
}
