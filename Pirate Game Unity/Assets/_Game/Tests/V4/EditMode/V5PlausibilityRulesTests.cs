using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Progression;
using RoguePirates.V4.Telemetry;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M9a (I9a.3): the run checks' rules as the game reads them (server/RoguePirates.Api/Plausibility/plausibility-rules.json,
    /// made by plan/playtest/montecarlo/plausibility.mjs from the Monte Carlo export of this config). The file is for the config
    /// the game runs on now (a balance change without new rules fails here, before a build reaches players); its XP table is
    /// the game's; the game's checker (RunLogPlausibility) gives the API's and the generator's verdicts on the recorded
    /// voyages; a scripted voyage from the RunLogBuilder passes and a forged one is flagged.
    /// </summary>
    [Category("V5")]
    public sealed class V5PlausibilityRulesTests
    {
        static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        static string RulesPath => Path.Combine(Repo, "server", "RoguePirates.Api", "Plausibility", "plausibility-rules.json");
        static string FixtureDir => Path.Combine(Repo, "server", "RoguePirates.Api.Tests", "Fixtures", "plausibility");
        const string Regenerate = "regenerate the rules: export data.json (RoguePirates > V5 > Balance: export the Monte Carlo data), then in " +
                                  "plan/playtest/montecarlo run node plausibility.mjs, node plausibility.mjs --check and node plausibility.mjs --sessions";

        static RunLogPlausibility.Rules Shipped() => RunLogPlausibility.ParseRules(File.ReadAllText(RulesPath));

        static ConfigSetRuntime Config()
        {
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(RoguePirates.V4.Editor.MonteCarloExport.SetPath);
            Assert.IsNotNull(set, "the default config set");
            return new ConfigSetRuntime(set);
        }

        [Test]
        public void RulesFile_IsForTheConfigTheGameRunsOn_AndUsesItsXpTable()
        {
            Assert.IsTrue(File.Exists(RulesPath), RulesPath);
            var rules = Shipped();
            Assert.GreaterOrEqual(rules.rulesVersion, 1);
            Assert.AreEqual("shadow", rules.mode);
            Assert.AreEqual(RunLogBuilder.Version, rules.logVersion, "the rules read this run log version");
            using var rt = Config();
            Assert.AreEqual(rt.Hash, rules.configHash, "the rules were made for another config: " + Regenerate);
            CollectionAssert.Contains(rules.configHashes, rt.Hash);
            var p = rt.Single<ProgressionConfig>();
            Assert.AreEqual(p.startLevel, rules.startLevel);
            for (var level = 0; level < rules.xpToNext.Length; level++)
                Assert.AreEqual(ProgressionMath.XpToNext(p, level), rules.xpToNext[level], $"XP to the next level from {level}");
            foreach (var r in rules.rules.Where(r => r.bounds != null && r.bounds.Length > 0))
                Assert.IsTrue(r.bounds.All(b => b > 0), r.id + ": upper bounds only");
        }

        [Test]
        public void GameChecker_GivesTheApisVerdicts_OnTheRecordedVoyages()
        {
            var shipped = Shipped();
            var expected = JsonUtility.FromJson<Expected>(File.ReadAllText(Path.Combine(FixtureDir, "expected-verdicts.json")));
            Assert.IsNotNull(expected?.sessions);
            Assert.AreEqual(8, expected.sessions.Length);
            foreach (var s in expected.sessions)
            {
                var log = File.ReadAllText(Path.Combine(FixtureDir, s.file));
                var own = RunLogPlausibility.ParseRules(File.ReadAllText(Path.Combine(FixtureDir, s.rules)));
                var mine = RunLogPlausibility.Evaluate(own, log);
                Assert.AreEqual(RunLogPlausibility.Pass, mine.Status, s.file + " on its own config's rules: " + mine);
                Assert.AreEqual(s.ownVerdict, mine.Status, s.file);
                Assert.AreEqual(s.verdict, RunLogPlausibility.Evaluate(shipped, log).Status, s.file + " with the shipped rules");
                var bounds = RunLogPlausibility.Evaluate(shipped, log, checkConfig: false);
                Assert.AreEqual(s.boundsVerdict, bounds.Status, s.file + " bounds only: " + bounds);
                CollectionAssert.AreEqual(s.boundsReasons.Select(r => r.rule).ToArray(), bounds.Reasons.Select(r => r.Rule).ToArray(), s.file);
            }
        }

        [Serializable] sealed class Expected { public int rulesVersion; public string configHash; public Session[] sessions; }
        [Serializable] sealed class Session { public string file, session, id, rules, ownVerdict, verdict, boundsVerdict; public ReasonRow[] boundsReasons; }
        [Serializable] sealed class ReasonRow { public string code, rule; public int minute; public double value, bound; }

        /// <summary>
        /// A scripted voyage straight into the RunLogBuilder with plausible numbers for this config: 6 minutes, two kills a
        /// second from 0:20, XP and levels from the game's XP table, gold, damage, a chest, hull worn down and healed.
        /// <paramref name="killEvery"/> ticks between kills (lower = faster).
        /// </summary>
        static string ScriptedVoyage(string configHash, int killEvery = 15, int seconds = 360)
        {
            using var rt = Config();
            var p = rt.Single<ProgressionConfig>();
            var b = new RunLogBuilder();
            var c = new RunLogCounters { Level = Math.Max(1, p.startLevel), Hull = 100f, MaxHull = 100f };
            b.Begin(new RunLogHeader { Seed = 99, SeedSource = "local", RunId = "scripted", ConfigHash = configHash, BuildId = "test", Captain = "rook" }, c);
            int level = p.startLevel;
            long xpLeft = 0;
            var damage = 0f;
            const float dt = 1f / 30f;
            for (var i = 1; i <= seconds * 30; i++)
            {
                var t = i * dt;
                if (t < 20f) { b.Advance(t, c); continue; }
                if (i % (killEvery / 2) == 0) b.AddSpawn();
                if (i % killEvery == 0)
                {
                    c.Kills++;
                    c.Xp += 1;
                    xpLeft += 1;
                    while (xpLeft >= ProgressionMath.XpToNext(p, level)) { xpLeft -= ProgressionMath.XpToNext(p, level); level++; }
                    c.Level = Math.Max(1, level);
                    c.Gold += c.Kills % 2;
                    damage += 25f;
                    b.SetWeaponTotal("swivel_guns", c.Kills, damage);
                }
                if (i % 300 == 0) { b.AddDamageTaken(2f); c.Hull -= 2f; }
                if (i % 3000 == 0) { b.AddHealed(10f); c.Hull = Mathf.Min(c.MaxHull, c.Hull + 10f); }
                if (i == 4500) b.AddChest();
                b.Advance(t, c);
            }
            b.Close(seconds, "quit", c);
            return b.ToJson();
        }

        [Test]
        public void ScriptedVoyage_PassesTheRunChecks_AndAForgedOneIsFlagged()
        {
            var rules = Shipped();
            var honest = ScriptedVoyage(rules.configHash);
            var v = RunLogPlausibility.Evaluate(rules, honest);
            Assert.AreEqual(RunLogPlausibility.Pass, v.Status, v.ToString());
            Assert.IsTrue(v.Checked);

            // ten kills a tick-second: far past any minute's kill bound (and its XP and level with it)
            var forged = RunLogPlausibility.Evaluate(rules, ScriptedVoyage(rules.configHash, killEvery: 2));
            Assert.AreEqual(RunLogPlausibility.Flagged, forged.Status);
            CollectionAssert.Contains(forged.Reasons.Select(r => r.Code).ToArray(), "kills_per_minute");
            Debug.Log("[V5 M9a] forged scripted voyage: " + forged);

            // a log naming another config is from another build
            var other = RunLogPlausibility.Evaluate(rules, ScriptedVoyage("not-this-config"));
            Assert.AreEqual(RunLogPlausibility.OutdatedClient, other.Status);
            Assert.AreEqual("config_not_in_rules", other.Reasons[0].Code);
            Assert.AreEqual(RunLogPlausibility.OutdatedClient, RunLogPlausibility.Evaluate(rules, null).Status, "no log");
        }
    }
}
