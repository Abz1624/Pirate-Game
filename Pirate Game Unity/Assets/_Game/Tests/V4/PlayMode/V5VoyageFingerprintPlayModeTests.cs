#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.DevTools.Scenarios;
using RoguePirates.V4.DevTools.Trace;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Foundation.World;
using RoguePirates.V4.Progression;
using RoguePirates.V4.Spawning;
using RoguePirates.V4.Stage;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M9c S0: the byte-exact single-player voyage fingerprint, captured before the multi-ship refactor and used as a
    /// gate by every later M9c step (plan/v5/phase-09c-multiship/PLAN.md, review change 6). The M9a determinism harness:
    /// the real run scene through the Scenario Lab, a fixed frame time (one physics step a frame) and a fixed input track
    /// (the wander pilot, a function of run time only). Each case writes the per-minute run log, the enemies at fixed sim
    /// times and the ship's final state, and compares them with plan/v5/goldens/voyage-fingerprint-&lt;case&gt;.txt.
    /// Case (c), the solo server voyage, is in V5NetVoyageFingerprintPlayModeTests.
    /// </summary>
    [Category("V5")]
    public sealed class V5VoyageFingerprintPlayModeTests
    {
        static float FrameSeconds => Time.fixedDeltaTime;
        const float RealTimeLimit = 900f;
        V4TestSaveIsolation _isolation;
        VoyageFingerprint.PinnedDevice _device;
        float _captureDeltaTime;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _isolation = new V4TestSaveIsolation();
            _device = new VoyageFingerprint.PinnedDevice();
            _captureDeltaTime = Time.captureDeltaTime;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = _captureDeltaTime;
            yield return SceneManager.LoadSceneAsync(V4SceneNames.Boot, LoadSceneMode.Single);
            yield return null;
            StageBalanceProfileContext.ResetForTests();
            _device?.Dispose();
            _device = null;
            _isolation?.Dispose();
            _isolation = null;
        }

        // ---- Case (a): seed 8282, a plain voyage ------------------------------------------------------------------------

        static ScenarioConfig PlainVoyage()
        {
            var s = ScriptableObject.CreateInstance<ScenarioConfig>();
            s.name = "fingerprint_plain_8282";
            s.seed = 8282;
            s.invulnerable = true;
            s.skipLevelUpCards = true;
            s.ambientSpawning = true;
            s.scriptedPhases = ScenarioConfig.ScriptedPhaseMode.AsTheGame;
            s.loadout.replaceWeapons = false; // the starting weapon stays
            return s;
        }

        /// <summary>
        /// Case (a): seed 8282, the regular game (tests' switches), waves and scripted phases as the game runs them, the
        /// starting weapon, no cards (level-ups apply with an empty pick), the ship cannot sink; three minutes of the wander
        /// pilot. Enemies at 60/120/180 s.
        /// </summary>
        [UnityTest, Timeout(1200000)]
        public IEnumerator PlainVoyage_Seed8282_MatchesItsGolden()
        {
            Time.captureDeltaTime = FrameSeconds;
            yield return Scenario.Load(PlainVoyage());
            Assert.IsNotNull(Scenario.Current, "the scenario loaded");
            CollectionAssert.IsEmpty(Scenario.Current.Errors);
            var game = Scenario.Current.Game;
            var ship = Scenario.Current.Ship;
            ship.ConfigureBotControl(new ScriptedPilot());
            var fp = new VoyageFingerprint("a-plain-8282",
                "Case (a): seed 8282, regular game switches, ambient waves and scripted phases as the game, starting weapon,\n" +
                "no cards, invulnerable; wander pilot (ScriptedPilot wander-v1) at a fixed frame time (fixedDeltaTime) for 180 s.");
            fp.Line("seed", 8282);
            fp.Line("frameSeconds", FrameSeconds);
            var samples = new Queue<float>(new[] { 60f, 120f, 180f });
            var run = RunManager.Instance;
            var deadline = Time.realtimeSinceStartup + RealTimeLimit;
            while (run != null && !run.IsRunEnded && samples.Count > 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if (run.ElapsedSeconds >= samples.Peek()) fp.Enemies(samples.Dequeue().ToString("0"), game);
            }
            Assert.AreEqual(0, samples.Count, "the voyage reached 180 s");
            game.RunLog.Close();
            fp.RunLog(game.RunLog.Log);
            fp.Ship(0, game, ship);
            Assert.Greater(run.KillCount, 0, "the waves were fought");
            fp.AssertMatchesGolden();
        }

        // ---- Case (b): seed 4242, the Scenario Lab's jumps --------------------------------------------------------------

        const float EliteAt = 15f, PowerupAt = 30f, ChestAt = 45f, RamAt = 60f, RamUntil = 75f, BeaconAt = 80f, OvertimeAt = 100f;
        const float DamageOnAfterOvertime = 20f, ForceSinkAfterOvertime = 60f;

        static ScenarioConfig EventfulVoyage()
        {
            var s = ScriptableObject.CreateInstance<ScenarioConfig>();
            s.name = "fingerprint_events_4242";
            s.seed = 4242;
            s.playtest = ScenarioConfig.PlaytestSwitches.Shipped; // the owner's switches: the Beacon boss, chests
            s.invulnerable = true;
            s.skipLevelUpCards = false; // the real cards; the test picks card 0
            s.ambientSpawning = true;
            s.scriptedPhases = ScenarioConfig.ScriptedPhaseMode.AsTheGame;
            s.loadout.replaceWeapons = false;
            // as boss_at_beacon_early: the Beacon may summon the boss before the director's usual minimum time
            s.configEdits.Add(new ScenarioConfigEdit { configId = "director.saltwind", path = "bossMinSeconds", value = "0" });
            return s;
        }

        /// <summary>
        /// Case (b): seed 4242 with the shipped playtest switches, the real level-up cards (card 0 picked the frame they
        /// show), and Scenario Lab jumps at fixed run times: two elites (15 s), a Battle Signal powerup under the ship
        /// (30 s), a Salvage Chest under the ship with its price in gold, taken through the prompt's Take (45 s), damage on
        /// and six sturdy Deckhand Dinghies ramming (60-75 s), the Treasury Beacon used (80 s: the boss is summoned), a clock
        /// jump into overtime (100 s; the scripted phases already due are skipped, as the Scenario Lab's jump does), damage
        /// on again 20 s into overtime, and a forced sinking through the hull 60 s into overtime if the sea has not sunk the
        /// ship by then. The run ends sunk.
        /// </summary>
        [UnityTest, Timeout(1200000)]
        public IEnumerator EventfulVoyage_Seed4242_MatchesItsGolden()
        {
            Time.captureDeltaTime = FrameSeconds;
            yield return Scenario.Load(EventfulVoyage());
            Assert.IsNotNull(Scenario.Current, "the scenario loaded");
            CollectionAssert.IsEmpty(Scenario.Current.Errors);
            var game = Scenario.Current.Game;
            var ship = Scenario.Current.Ship;
            ship.ConfigureBotControl(new ScriptedPilot());
            var context = game.Context;
            Assert.IsTrue(context.TryGet<IOfferChoice>(out var cards), "the level-up cards");
            Assert.IsTrue(context.TryGet<IInteractionTouch>(out var touch), "the interaction prompt");
            Assert.IsTrue(context.TryGet<IStageContent>(out var stage), "the stage");
            Assert.IsTrue(context.TryGet<IEnemySpawnSink>(out var sink), "the enemy world");
            context.TryGet<ISpawnController>(out var spawn);

            var fp = new VoyageFingerprint("b-events-4242",
                "Case (b): seed 4242, shipped playtest switches, real level-up cards (card 0), wander pilot at fixed frame time;\n" +
                "jumps: elites 15 s, powerup 30 s, chest Take 45 s, ram 60-75 s (damage on), Beacon boss 80 s, overtime jump 100 s,\n" +
                "damage on 20 s into overtime, forced sinking 60 s into overtime if still afloat. Ends sunk.");
            fp.Line("seed", 4242);
            fp.Line("frameSeconds", FrameSeconds);
            var picks = new List<string>();
            var powerups = new List<string>();
            game.Build.Powerups.Collected += k => powerups.Add(k + "@" + VoyageFingerprint.F(RunManager.Instance.ElapsedSeconds));
            var ramHits = 0;
            var ramWindow = false;
            ship.Damage.Damaged += (amount, crit) => { if (ramWindow) ramHits++; };
            var chests = new List<string>();
            var run = RunManager.Instance;
            bool elites = false, powerup = false, chest = false, ram = false, ramOver = false, beacon = false, overtime = false, damageOn = false;
            var overtimeStart = -1f;
            var sinking = "none";
            var enemySamples = new Queue<float>(new[] { 60f, 100f });
            var overtimeProbes = new Queue<float>(new[] { 5f, 10f, 15f });
            var deadline = Time.realtimeSinceStartup + RealTimeLimit;
            while (run != null && !run.IsRunEnded && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if (run.IsRunEnded) break;
                var t = run.ElapsedSeconds;

                // The player's answers: the first card, the chest's Take.
                if (cards.IsShowing && cards.Shown.Count > 0)
                {
                    var o = cards.Shown[0];
                    picks.Add(o.Kind + ":" + o.Id + "@" + VoyageFingerprint.F(t));
                    cards.Pick(0);
                    continue;
                }
                if (run.PauseReason == PauseReason.POIChoice)
                {
                    chests.Add("take@" + VoyageFingerprint.F(t) + " gold=" + run.Gold);
                    touch.TouchPressed();
                    continue;
                }
                if (run.IsPaused) continue;

                if (enemySamples.Count > 0 && t >= enemySamples.Peek()) fp.Enemies(enemySamples.Dequeue().ToString("0"), game);
                if (overtime && overtimeProbes.Count > 0 && t >= overtimeStart + overtimeProbes.Peek()) fp.Enemies("overtime+" + overtimeProbes.Dequeue().ToString("0"), game);

                if (!elites && t >= EliteAt)
                {
                    elites = true;
                    Spawn(sink, "elite_hammerhead", ship.Position + new Vector3(24f, 0f, 6f), ConfigEnums.EliteAffix.None, 1f);
                    Spawn(sink, "cutthroat_skiff", ship.Position + new Vector3(-18f, 0f, 16f), ConfigEnums.EliteAffix.Armored, 1f);
                }
                if (!powerup && t >= PowerupAt)
                {
                    powerup = true;
                    Assert.IsTrue(game.Build.Powerups.DevSpawn(PowerupKind.BattleSignal, ship.Position), "a powerup under the ship");
                }
                if (!chest && t >= ChestAt)
                {
                    chest = true;
                    var price = stage.Module.QuoteChestPrice();
                    if (run.Gold < price) run.AddGold(price - run.Gold);
                    var index = stage.Module.DevPlacePoi(POIType.SalvageChest, ship.Position);
                    fp.Line("chest.placed", $"index={index} price={price}");
                }
                if (!ram && t >= RamAt)
                {
                    ram = true;
                    ramWindow = true;
                    ship.Damage.SetPlaytestInvulnerable(false);
                    for (var i = 0; i < 6; i++)
                        Spawn(sink, "deckhand_dinghy", ship.Position + Quaternion.AngleAxis(i * 60f, Vector3.up) * Vector3.forward * 12f, ConfigEnums.EliteAffix.None, 50f);
                }
                if (ram && !ramOver && t >= RamUntil)
                {
                    ramOver = true;
                    ramWindow = false;
                    ship.Damage.SetPlaytestInvulnerable(true);
                }
                if (!beacon && t >= BeaconAt)
                {
                    beacon = true;
                    var index = stage.Module.DevPlacePoi(POIType.BossBeacon, ship.Position + new Vector3(3f, 0f, 0f));
                    Assert.GreaterOrEqual(index, 0, "a Beacon placed");
                    ship.SetWorldPosition(stage.Module.Layout.pois[index].position + new Vector3(3f, 0f, 0f));
                    stage.Module.TryCompleteInteraction(index);
                    fp.Line("beacon.placed", index);
                }
                if (!overtime && t >= OvertimeAt)
                {
                    overtime = true;
                    fp.Enemies("before-overtime-jump", game);
                    var target = SaltwindFinaleLogic.ResolveStageDeadlineSeconds(StageBalanceProfileContext.Export) + 1f;
                    var timeline = game.Config?.Single<WaveTimelineConfig>();
                    if (spawn != null && timeline != null)
                        foreach (var p in timeline.phases)
                        {
                            if (p == null || PhaseKinds.IsAmbient(p) || p.isBoss) continue;
                            if ((PhaseKinds.IsFocus(p) || PhaseKinds.IsPulse(p)) && p.trigger.kind == PhaseTriggerKind.Time && p.trigger.timeSeconds < target && p.trigger.timeSeconds > t)
                                spawn.Skip(p.phaseId);
                        }
                    run.JumpClock(target);
                    overtimeStart = target;
                    fp.Line("overtime.jump", $"from={VoyageFingerprint.F(t)} to={VoyageFingerprint.F(target)}");
                }
                if (overtime && !damageOn && t >= overtimeStart + DamageOnAfterOvertime)
                {
                    damageOn = true;
                    ship.Damage.SetPlaytestInvulnerable(false);
                    fp.Enemies("overtime+20", game);
                }
                if (damageOn && t >= overtimeStart + ForceSinkAfterOvertime && ship.Damage.IsAlive)
                {
                    if (sinking == "none") sinking = "forced@" + VoyageFingerprint.F(t);
                    ship.Damage.TakeDamage(ship.Damage.MaxHP * 10f, ship.Position, false);
                }
            }
            Assert.IsTrue(run.IsRunEnded, "the voyage ended");
            if (sinking == "none") sinking = "sea@" + VoyageFingerprint.F(run.ElapsedSeconds);
            var recorder = game.RunLog;
            for (var i = 0; i < 10 && recorder.Closed == null; i++) yield return null; // the runtime closes it at the run's end
            Assert.IsNotNull(recorder.Closed, "the run closed its log");

            fp.Enemies("end", game);
            fp.Line("cards.picks", picks.Count == 0 ? "-" : string.Join(" ", picks));
            fp.Line("powerups.collected", powerups.Count == 0 ? "-" : string.Join(" ", powerups));
            fp.Line("chests", chests.Count == 0 ? "-" : string.Join(" ", chests));
            fp.Line("ram.hits", ramHits);
            fp.Line("sinking", sinking);
            fp.RunLog(recorder.Log);
            fp.Ship(0, game, ship);
            fp.WriteActual();

            // Coverage: every jump did what it is there for.
            Assert.Greater(picks.Count, 0, "level-up cards were picked");
            Assert.Greater(powerups.Count, 0, "a powerup was collected");
            Assert.Greater(chests.Count, 0, "a chest was taken");
            Assert.Greater(stage.PaidChestCount, 0, "and paid for");
            Assert.Greater(ramHits, 0, "the dinghies rammed the ship");
            Assert.IsTrue(stage.BossSummoned, "the Beacon summoned the boss");
            Assert.IsTrue(run.IsOvertime, "overtime");
            Assert.IsFalse(ship.Damage.IsAlive, "the ship sank (the death flow ends the run as RunComplete)");
            StringAssert.Contains("\"end\":\"sunk\"", recorder.Closed.Json);
            fp.AssertMatchesGolden();
        }

        static void Spawn(IEnemySpawnSink sink, string enemyId, Vector3 position, ConfigEnums.EliteAffix affix, float hpMultiplier)
        {
            var definition = sink.FindDefinition(enemyId);
            Assert.GreaterOrEqual(definition, 0, enemyId + " is in the run database");
            var slot = sink.Spawn(definition, PlayBounds.Clamp(position, 2f), affix, 1f, hpMultiplier, 1f);
            Assert.GreaterOrEqual(slot, 0, enemyId + " spawned");
        }

        // ---- Baseline: allocations per frame ----------------------------------------------------------------------------

        /// <summary>
        /// V5 M9c S0 baseline (a measurement, not a gate): the managed allocations per frame of case (a)'s voyage between 10
        /// and 60 s of run time in the Editor (the profiler's "GC Allocated In Frame" counter), written to
        /// Temp/V5Fingerprint/alloc-baseline.json for plan/v5/phase-09c-multiship/s0/baseline.json.
        /// </summary>
        [UnityTest, Timeout(1200000)]
        public IEnumerator Baseline_AllocationsPerFrame_Measured()
        {
            Time.captureDeltaTime = FrameSeconds;
            yield return Scenario.Load(PlainVoyage());
            Scenario.Current.Ship.ConfigureBotControl(new ScriptedPilot());
            var run = RunManager.Instance;
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"))
            {
                var bytes = new List<long>();
                var deadline = Time.realtimeSinceStartup + 300f;
                while (run != null && !run.IsRunEnded && run.ElapsedSeconds < 60f && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                    if (run.ElapsedSeconds >= 10f && recorder.Valid) bytes.Add(recorder.LastValue);
                }
                Assert.IsTrue(recorder.Valid, "the GC Allocated In Frame counter is available");
                Assert.Greater(bytes.Count, 100, "frames measured");
                bytes.Sort();
                long P(float q) => bytes[Mathf.Clamp(Mathf.CeilToInt(q * bytes.Count) - 1, 0, bytes.Count - 1)];
                var mean = bytes.Average();
                var zero = bytes.Count(b => b == 0) / (float)bytes.Count;
                var json = "{\n" +
                           $"  \"scenario\": \"fingerprint_plain_8282 (case a), run time 10-60 s, Editor\",\n" +
                           $"  \"frames\": {bytes.Count},\n" +
                           $"  \"meanBytes\": {mean.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)},\n" +
                           $"  \"p50Bytes\": {P(0.5f)},\n  \"p95Bytes\": {P(0.95f)},\n  \"p99Bytes\": {P(0.99f)},\n  \"maxBytes\": {bytes[bytes.Count - 1]},\n" +
                           $"  \"zeroFrames\": {zero.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)}\n}}\n";
                var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "V5Fingerprint", "alloc-baseline.json"));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, json);
                Debug.Log("[V5 M9c S0] allocations per frame: " + json);
            }
        }
    }
}
#endif
