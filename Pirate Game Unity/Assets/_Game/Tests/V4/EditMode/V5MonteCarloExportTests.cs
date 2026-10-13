using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Editor;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Progression;
using RoguePirates.V4.Spawning;
using UnityEditor;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M8z (I8z.7, owner 7 Oct 2026: one simulator that reads the game's real configs): the Editor export the JavaScript
    /// Monte Carlo reads (plan/playtest/montecarlo/data.json) holds what the model needs, resolved the way the game resolves
    /// it. The model's own balance checks run in JavaScript on the owner's recorded voyages (fit_sessions.mjs).
    /// </summary>
    [Category("V5")]
    public sealed class V5MonteCarloExportTests
    {
        static ConfigSet Set => AssetDatabase.LoadAssetAtPath<ConfigSet>(MonteCarloExport.SetPath);

        static Dictionary<string, object> D(object o) => (Dictionary<string, object>)o;

        [Test]
        public void TheExport_HoldsEveryEnemyWeaponPhaseAndTheResolvedXpTable()
        {
            using var rt = new ConfigSetRuntime(Set);
            var data = MonteCarloExport.Build(rt);
            var enemies = D(data["enemies"]);
            foreach (var a in rt.All<EnemyArchetypeConfig>()) Assert.IsTrue(enemies.ContainsKey(a.enemyId), a.enemyId);
            foreach (var v in rt.All<EnemyVariantConfig>().Where(v => v.baseArchetype != null)) Assert.IsTrue(enemies.ContainsKey(v.variantId), v.variantId);
            var disabled = rt.Single<UnlockConfig>().disabledWeaponIds ?? new string[0];
            var weapons = D(data["weapons"]);
            foreach (var w in rt.All<WeaponConfig>().Where(w => !string.IsNullOrEmpty(w.weaponId)))
                Assert.AreEqual(!disabled.Contains(w.weaponId), weapons.ContainsKey(w.weaponId), w.weaponId + ": exported unless switched off");
            var timeline = D(data["timeline"]);
            var t = rt.Single<WaveTimelineConfig>();
            Assert.AreEqual(t.phases.Count(PhaseKinds.IsFocus), ((IList)timeline["focus"]).Count, "every miniboss and scheduled elite");
            Assert.AreEqual(t.stageDeadlineSeconds, (float)timeline["deadline"]);
            var xp = (IList)D(data["progression"])["xpToNext"];
            var p = rt.Single<ProgressionConfig>();
            for (var level = 0; level < xp.Count; level++) Assert.AreEqual(ProgressionMath.XpToNext(p, level), (int)xp[level], $"XP to the next level from {level}");
            var director = D(data["director"]);
            Assert.AreEqual(rt.Single<DirectorConfig>().bossHpAsIfAtSeconds, (float)director["bossHpAsIfAtSeconds"]);
            Assert.AreEqual(rt.Single<RunConfig>().islandImpactDamage, (float)D(data["run"])["islandImpactDamage"]);
        }

        [Test]
        public void Variants_AndEliteSpawners_AreResolvedAsTheGameDoes()
        {
            using var rt = new ConfigSetRuntime(Set);
            var enemies = D(MonteCarloExport.Build(rt)["enemies"]);
            foreach (var v in rt.All<EnemyVariantConfig>().Where(v => v.baseArchetype != null))
            {
                var r = EnemyStatResolver.Resolve(v);
                var e = D(enemies[v.variantId]);
                Assert.AreEqual(r.baseHP, (float)e["hp"], 1e-5f, v.variantId + " HP (a registry elite keeps its base's)");
                Assert.AreEqual(r.baseDamage, (float)e["dmg"], 1e-5f, v.variantId + " damage");
                Assert.AreEqual((float)r.xpValue, (float)e["xp"], 1e-5f, v.variantId + " XP");
            }
            var focus = ((IList)D(MonteCarloExport.Build(rt)["timeline"])["focus"]).Cast<Dictionary<string, object>>().ToList();
            foreach (var ph in rt.Single<WaveTimelineConfig>().phases.Where(PhaseKinds.IsFocus))
            {
                var row = focus.Single(f => (string)f["id"] == ph.phaseId);
                var sp = ph.spawner;
                if (ph.focus.placement != FocusPlacement.SpawnerProfile || sp == null || !sp.elite.isElite) continue;
                var affix = sp.elite.affix == ConfigEnums.EliteAffix.None || sp.elite.affix == ConfigEnums.EliteAffix.Rich ? 1.25f
                    : sp.elite.affix == ConfigEnums.EliteAffix.Armored ? 1.8f : sp.elite.affix == ConfigEnums.EliteAffix.Swift ? 0.9f : 1f;
                Assert.AreEqual(ph.focus.hpMultiplier * sp.elite.hpMultiplier * affix, (float)row["hpMult"], 1e-4f, ph.phaseId + ": focus × elite spawner × affix (none = Rich)");
            }
        }

        [Test]
        public void TheModelsData_IsTheExportOfTheseConfigs()
        {
            // 13 Oct (review): a stale data.json is a failure (it was inconclusive, so a config change could ship with the
            // server's run checks built on old numbers): re-export from the Editor (RoguePirates ▸ V5 ▸ Balance: export the
            // Monte Carlo data) after a balance change, then regenerate the run-check rules.
            using var rt = new ConfigSetRuntime(Set);
            var path = Path.Combine(MonteCarloExport.ModelFolder, "data.json");
            Assert.IsTrue(File.Exists(path), "plan/playtest/montecarlo/data.json");
            var text = File.ReadAllText(path);
            StringAssert.Contains("\"source\": \"unity\"", text, "data.json is the Editor export (not the YAML fallback)");
            if (!text.Contains(rt.Hash)) Assert.Fail("data.json was exported from other config values: re-export it (RoguePirates > V5 > Balance: export the Monte Carlo data).");
        }
    }
}
