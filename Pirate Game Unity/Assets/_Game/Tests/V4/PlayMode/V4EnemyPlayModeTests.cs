using System.Collections;
using System.Linq;
using System.Reflection;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Enemies;
using RoguePirates.V4.Foundation.Player;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Spawning;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoguePirates.V4.Tests
{
    /// <summary>M8: the enemy views and dev actions (the trace recorder, pilot and console read enemies only through them).</summary>
    [Category("V4")]
    public sealed class V4EnemyPlayModeTests
    {
        const float Timeout = 30f;
        V4TestSaveIsolation _isolation;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _isolation = new V4TestSaveIsolation();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.Boot, LoadSceneMode.Single);
            yield return null;
            StageBalanceProfileContext.ResetForTests();
            _isolation?.Dispose();
            _isolation = null;
        }

        static IEnumerator StartRun()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            var deadline = Time.realtimeSinceStartup + Timeout;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (RunManager.Instance != null && RunManager.Instance.State == RunState.Playing && StageBalanceProfileContext.IsActive) yield break;
                yield return null;
            }
            Assert.Fail("V4 run did not reach Playing.");
        }

        static T Get<T>() where T : class
        {
            Assert.IsTrue(Object.FindFirstObjectByType<V4Game>().Context.TryGet<T>(out var value), typeof(T).Name + " registered");
            return value;
        }

        [UnityTest]
        public IEnumerator Switchover_V3EnemyPiecesGone_V4ReplacementsWired()
        {
            yield return StartRun();
            // M14.14: frame steps (were legacy update slots).
            var frame = Object.FindFirstObjectByType<V4Game>().Frame;
            Assert.IsTrue(frame.Has(FrameStep.Hellfire), "hellfire patches tick in the frame");
            Assert.IsTrue(frame.Has(FrameStep.Telemetry), "telemetry samples in the frame");
            var wiring = Object.FindFirstObjectByType<V4Game>().Shell.Enemies;
            Assert.AreSame(wiring.Services.Hazards, Object.FindFirstObjectByType<V4Game>().Context.Get<RoguePirates.V4.Enemies.IHazardView>(),
                "the hazard rings draw the V4 hazards (M14.9b: the presenter reads the context's hazard view)");

            // A hellfire patch burns an enemy standing in it (V3's patch world read the legacy enemy world).
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            ship.Damage.SetPlaytestInvulnerable(true);
            var sim = wiring.Simulation;
            var barge = System.Array.FindIndex(sim.Definitions, d => d != null && d.id == "boarding_barge");
            var at = ship.Position + new Vector3(18f, 0f, 0f);
            var slot = sim.Spawn(barge, at);
            Assert.GreaterOrEqual(slot, 0);
            var hp = sim.Agents[slot].hp;
            typeof(HellfirePatches).GetMethod("AddPatch", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(wiring.Hellfire, new object[] { sim.Agents[slot].position });
            var end = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < end && sim.Agents[slot].active && sim.Agents[slot].hp >= hp) yield return null;
            Assert.IsTrue(!sim.Agents[slot].active || sim.Agents[slot].hp < hp, "the patch damaged the enemy through the V4 hit path");
            Assert.Greater(wiring.Statuses.BurnStacks(slot) + (sim.Agents[slot].active ? 0f : 1f), 0f, "and set it burning");
        }

        [UnityTest]
        public IEnumerator AreaDenialRelease_RunsInTheRun_AndNotUnderTheParityPreset()
        {
            foreach (var parity in new[] { false, true })
            {
                if (parity)
                    RunRequest.RequestPreset(System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../../plan/v4-verification/presets/v3-parity.json")));
                yield return StartRun();
                Object.FindFirstObjectByType<PlayerShipController>().Damage.SetPlaytestInvulnerable(true);
                var budget = Object.FindFirstObjectByType<V4Game>().Shell.Enemies.Services.Budget;
                while (budget.TryReserveAreaDenial(0)) { } // fill it (two Kraken casts)
                var t0 = RunManager.Instance.ElapsedSeconds;
                var deadline = Time.realtimeSinceStartup + 8f;
                while (RunManager.Instance.ElapsedSeconds < t0 + 2.3f && Time.realtimeSinceStartup < deadline)
                {
                    if (RunManager.Instance.IsPaused) RunManager.Instance.SetPause(PauseReason.None); // no level-up card holds the clock
                    yield return null;
                }
                Assert.GreaterOrEqual(RunManager.Instance.ElapsedSeconds, t0 + 2.3f);
                if (parity) Assert.AreEqual(2, budget.ActiveAreaDenial, "v3-parity: never released (R-06 off)");
                else Assert.Less(budget.ActiveAreaDenial, 2, "R-06: the shell frees each cast 2 s later on the run clock");
            }
        }

        static IEnumerator StartLab()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.ModuleLab, LoadSceneMode.Single);
            var deadline = Time.realtimeSinceStartup + Timeout;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (RunManager.Instance != null && RunManager.Instance.State == RunState.Playing && StageBalanceProfileContext.IsActive) yield break;
                yield return null;
            }
            Assert.Fail("V4 Module Lab did not reach Playing.");
        }

        static void KeepRunning()
        {
            if (!RunManager.Instance.IsPaused) return;
            var modal = Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Hud.LevelUpPresenter>(); // M14.11
            var card = modal != null ? modal.transform.Find("CardRow/Card0") : null;
            if (card != null && card.gameObject.activeInHierarchy) card.GetComponent<UnityEngine.UI.Button>()?.onClick.Invoke();
            else RunManager.Instance.SetPause(PauseReason.None);
        }

        static RoguePirates.V4.Composition.BuildModule Build() => Object.FindFirstObjectByType<V4Game>().Build;

        static int CountBarrels(PickupType type)
        {
            var pickups = Build().Pickups;
            var n = 0;
            for (var i = 0; i < pickups.Capacity; i++)
                if (pickups.TryGet(i, out var t, out _, out _, out _) && t == type) n++;
            return n;
        }

        /// <summary>Puts a Magnetic Tide powerup on the V4 powerup module at the position (collected when the ship is there).</summary>
        static void DropMagneticTideAt(Vector3 position, float lifetime = -1f) =>
            Assert.IsTrue(Build().Powerups.DevSpawn(RoguePirates.V4.Config.PowerupKind.MagneticTide, position, lifetime), "the tide was placed");

        [UnityTest]
        public IEnumerator R103_MagneticTide_OpensEveryBarrel_AndPays_NotUnderTheParityPreset()
        {
            foreach (var parity in new[] { false, true })
            {
                if (parity)
                    RunRequest.RequestPreset(System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../../plan/v4-verification/presets/v3-parity.json")));
                yield return StartLab(); // no ambient spawns, so no other pickups
                var ship = Object.FindFirstObjectByType<PlayerShipController>();
                var pickups = Build().Pickups;
                var far = ship.Position + new Vector3(30f, 0f, 0f);
                for (var i = 0; i < 3; i++) pickups.Spawn(PickupType.XpBarrel, 5, far + new Vector3(0f, 0f, i * 3f));
                for (var i = 0; i < 2; i++) pickups.Spawn(PickupType.GoldBarrel, 7, far + new Vector3(4f, 0f, i * 3f));
                pickups.Spawn(PickupType.Powerup, 3, far + new Vector3(-4f, 0f, 0f));
                var levels = Build().Progression;
                var gold = RunManager.Instance.Gold;
                var xp = levels.Level * 100000f + levels.CurrentXp;
                ship.Damage.SetPlaytestInvulnerable(false); // the lab ship is invulnerable: make room for the health barrel
                ship.Damage.TakeDamage(20f, ship.Position, false);
                ship.Damage.SetPlaytestInvulnerable(true);
                var hull = ship.Damage.CurrentHP;
                Assert.Less(hull, ship.Damage.MaxHP, "room to heal");
                DropMagneticTideAt(ship.Position);
                var deadline = Time.realtimeSinceStartup + 8f;
                while (Time.realtimeSinceStartup < deadline) { KeepRunning(); yield return null; }
                if (parity)
                {
                    Assert.AreEqual(0, CountBarrels(PickupType.XpBarrel) + CountBarrels(PickupType.GoldBarrel), "V3: XP and gold barrels are pulled and vanish");
                    Assert.AreEqual(1, CountBarrels(PickupType.Powerup), "V3: health barrels are not pulled");
                    Assert.AreEqual(gold, RunManager.Instance.Gold, "V3: the pulled barrels pay nothing");
                }
                else
                {
                    Assert.AreEqual(0, CountBarrels(PickupType.XpBarrel) + CountBarrels(PickupType.GoldBarrel) + CountBarrels(PickupType.Powerup), "every barrel opened");
                    Assert.Greater(RunManager.Instance.Gold, gold, "gold paid");
                    Assert.Greater(levels.Level * 100000f + levels.CurrentXp, xp, "XP paid");
                    Assert.Greater(ship.Damage.CurrentHP, hull, "health paid");
                }
            }
        }

        [UnityTest]
        public IEnumerator HellfirePatch_ShowsAGroundMarker_UntilItBurnsOut()
        {
            yield return StartLab();
            var enemies = Object.FindFirstObjectByType<V4Game>().Shell.Enemies;
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            var at = ship.Position + new Vector3(10f, 0f, 0f);
            var hooks = new RoguePirates.V4.Weapons.WeaponCombatHooks(null, enemies.Hellfire);
            Build().Progression.DevEvolve("flamethrower", "hellfire_nozzle"); // the Hellfire Nozzle evolution (the run's own)
            hooks.SpawnFlamePatch(at, "flamethrower");
            Assert.AreEqual(1, enemies.Hellfire.ActiveCount);
            yield return null;
            var view = Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Effects.HazardRingPresenter>();
            Assert.IsNotNull(view);
            var outline = view.transform.Find("PatchOutline0");
            Assert.IsNotNull(outline, "a marker ring per patch slot");
            Assert.IsTrue(outline.gameObject.activeSelf, "the burning patch shows its ground marker");
            var line = outline.GetComponent<LineRenderer>();
            Assert.AreEqual(at.x + 2.1f, line.GetPosition(0).x, 0.01f, "the ring is the burn radius around the patch");
            var deadline = Time.realtimeSinceStartup + 6f;
            while (enemies.Hellfire.ActiveCount > 0 && Time.realtimeSinceStartup < deadline) { KeepRunning(); yield return null; }
            yield return null;
            Assert.IsFalse(outline.gameObject.activeSelf, "the marker goes when the patch burns out");
        }

        static IEnumerator SummonBoss(RoguePirates.V4.Stage.IStageContent stage)
        {
            // An early summon can fail to place the boss (as in the run test): retry for a few frames.
            for (var i = 0; i < 60 && !stage.BossSummoned; i++)
            {
                stage.TrySummonBoss();
                yield return null;
            }
            Assert.IsTrue(stage.BossSummoned, "the F1 summon works in the lab (M8 playtest)");
        }

        [UnityTest]
        public IEnumerator ModuleLab_BossSummonAndKill_IsNotSaved()
        {
            yield return StartLab();
            var stage = Get<RoguePirates.V4.Stage.IStageContent>();
            var lockedBefore = RoguePirates.V4.Foundation.Progression.HomeSeed.HasLockedHomeSeed;
            yield return SummonBoss(stage);
            var sim = V4TestEnemies.Simulation();
            Assert.IsTrue(sim.TryFindActiveBoss(out var slot, out var boss), "the boss spawned");
            sim.ApplyDamage(slot, boss.maxHp + 500f, false, weaponId: "deck_cannons");
            var deadline = Time.realtimeSinceStartup + 5f;
            while (!stage.BossDefeated && Time.realtimeSinceStartup < deadline) { KeepRunning(); yield return null; }
            Assert.IsTrue(stage.BossDefeated, "the lab boss can be defeated");
            Assert.AreEqual(lockedBefore, RoguePirates.V4.Foundation.Progression.HomeSeed.HasLockedHomeSeed, "a lab boss victory does not lock the home seed");
        }

        [UnityTest]
        public IEnumerator ModuleLab_ClearAllDuringTheBossFight_LetsYouSummonAgain()
        {
            yield return StartLab();
            var stage = Get<RoguePirates.V4.Stage.IStageContent>();
            yield return SummonBoss(stage);
            Assert.AreEqual(RunState.BossFight, RunManager.Instance.State);
            Get<IEnemyDevControl>().DespawnAll();
            stage.Module.ForgetDespawnedBoss(); // what the lab's Clear all does after despawning
            for (var i = 0; i < 5; i++) { KeepRunning(); yield return null; }
            Assert.IsFalse(stage.BossSummoned);
            Assert.AreEqual(RunState.Playing, RunManager.Instance.State, "out of the boss fight");
            yield return SummonBoss(stage);
            Assert.IsTrue(V4TestEnemies.Simulation().TryFindActiveBoss(out _, out _), "a new boss spawned");
        }

        [UnityTest]
        public IEnumerator ModuleLab_PowderCacheChallenge_FourElites_ThenAFreeRareChest()
        {
            yield return StartLab();
            var stage = Get<RoguePirates.V4.Stage.IStageContent>();
            var layout = stage.Module.Layout;
            var cache = layout.pois.FindIndex(p => p.type == POIType.MerchantSkiff);
            Assert.GreaterOrEqual(cache, 0, "the lab has a Powder Cache");
            var sim = V4TestEnemies.Simulation();
            Object.FindFirstObjectByType<PlayerShipController>().SetWorldPosition(stage.Module.MarkerPosition(cache) + new Vector3(2f, 0f, 0f));
            for (var i = 0; i < 30; i++) { KeepRunning(); yield return null; }
            Assert.AreEqual(0, sim.ActiveCount, "sailing up does not spring it: you hold E");
            stage.Module.TryCompleteInteraction(cache); // the completed hold
            var deadline = Time.realtimeSinceStartup + 8f;
            var encounter = stage.Module.PowderCacheChallengeEncounter(cache); // M14: the run's V4 encounter tracker
            Assert.IsTrue(Get<RoguePirates.V4.Spawning.IEncounterTracker>().Exists(encounter), "the challenge's encounter");
            while (Get<RoguePirates.V4.Spawning.IEncounterTracker>().SpawnedCount(encounter) < 4 && Time.realtimeSinceStartup < deadline) { KeepRunning(); yield return null; }
            Assert.AreEqual(4, Get<RoguePirates.V4.Spawning.IEncounterTracker>().SpawnedCount(encounter), "4 elites spawned (queued encounters tick in the lab)");
            for (var i = 0; i < sim.Agents.Length; i++)
                if (sim.Agents[i].active) Assert.IsTrue(sim.Agents[i].isElite, "every ambusher is an elite");
            for (var i = 0; i < sim.Agents.Length; i++)
                if (sim.Agents[i].active) sim.ApplyDamage(i, sim.Agents[i].maxHp + 500f, false, weaponId: "deck_cannons");
            deadline = Time.realtimeSinceStartup + 5f;
            while (!stage.Module.IsPowderCacheChestReady(cache) && Time.realtimeSinceStartup < deadline) { KeepRunning(); yield return null; }
            Assert.IsTrue(stage.Module.IsPowderCacheChestReady(cache), "all sunk: the cache became a Rare Treasure Chest");
            Assert.IsTrue(stage.Module.CanInteract(cache));
            var inventory = Build().Items;
            var chestPool = Object.FindFirstObjectByType<V4Game>().Config.All<ItemConfig>().Where(i => i.inChestPool).ToList(); // the run's chest pool
            var stacks = chestPool.ToDictionary(e => e.itemId, e => inventory.StackCount(e.itemId));
            var gold = RunManager.Instance.Gold;
            stage.Module.TryCompleteInteraction(cache); // open it
            var gained = chestPool.Where(e => inventory.StackCount(e.itemId) > stacks[e.itemId]).ToList();
            Assert.AreEqual(1, gained.Count, "one item gained (a new one or another stack)");
            Assert.GreaterOrEqual(gained[0].rarity, (int)Rarity.Rare, "Rare or better");
            Assert.GreaterOrEqual(RunManager.Instance.Gold, gold, "free");
            Assert.IsTrue(layout.pois[cache].used);
        }

        [UnityTest]
        public IEnumerator ConsoleGoTo_PowderCache_PutsTheShipInItsPromptRange_InTheRunAndTheLab()
        {
            foreach (var lab in new[] { false, true })
            {
                if (lab) yield return StartLab(); else yield return StartRun();
                var game = Object.FindFirstObjectByType<V4Game>();
                var stage = Get<RoguePirates.V4.Stage.IStageContent>();
                var cache = stage.Module.Layout.pois.FindIndex(p => p.type == POIType.MerchantSkiff);
                var message = RoguePirates.V4.DevTools.Console.StageSection.GoTo(game, POIType.MerchantSkiff);
                if (cache < 0) { StringAssert.StartsWith("No Powder Cache", message); continue; }
                StringAssert.StartsWith("Moved to Powder Cache", message);
                Assert.IsTrue(stage.Module.IsPlayerInRange(cache), "in range of its hold prompt");
                for (var i = 0; i < 3; i++) { KeepRunning(); yield return null; }
                Assert.AreEqual(cache, stage.PromptTarget, "the prompt offers the challenge");
            }
        }

        [UnityTest]
        public IEnumerator R103_AnExpiredMagneticTide_OpensNothing()
        {
            yield return StartLab();
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            var far = ship.Position + new Vector3(30f, 0f, 0f);
            for (var i = 0; i < 3; i++) Build().Pickups.Spawn(PickupType.XpBarrel, 5, far + new Vector3(0f, 0f, i * 3f));
            // A tide far from the ship whose lifetime runs out on its next tick: expired, not collected.
            DropMagneticTideAt(far + new Vector3(0f, 0f, -20f), 0.0001f);
            var deadline = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < deadline) { KeepRunning(); yield return null; }
            Assert.AreEqual(0, Build().Powerups.ActiveCount, "the tide expired");
            Assert.AreEqual(3, CountBarrels(PickupType.XpBarrel), "an expired tide opens nothing");
        }

        [UnityTest]
        public IEnumerator TelemetrySlot_SamplesTheV4EnemyWorld()
        {
            yield return StartRun();
            Object.FindFirstObjectByType<PlayerShipController>().Damage.SetPlaytestInvulnerable(true);
            var sim = V4TestEnemies.Simulation();
            var deadline = Time.realtimeSinceStartup + 10f;
            while (sim.ActiveCount < 3 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.GreaterOrEqual(sim.ActiveCount, 3, "ambient spawns arrived");
            yield return null;
            var peak = Object.FindFirstObjectByType<V4Game>().Telemetry.PeakEnemies; // M14: the V4 telemetry
            Assert.GreaterOrEqual(peak, 3, "the telemetry slot counts the V4 world's enemies (V3's driver would read 0 now)");
        }

        [UnityTest]
        public IEnumerator Switchover_ParityPresetBarrels_RunOnTheV4World()
        {
            var preset = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../../plan/v4-verification/presets/v3-parity.json"));
            RunRequest.RequestPreset(preset);
            yield return StartRun();
            Assert.IsTrue(Object.FindFirstObjectByType<V4Game>().Frame.Has(FrameStep.Barrels), "the V4 barrels tick in the frame");
            Assert.AreEqual(2, Object.FindFirstObjectByType<V4Game>().Shell.Enemies.Barrels.Count);
        }

        [UnityTest]
        public IEnumerator EnemyContracts_MapTheSimulation_SlotBySlot_AndDevActionsUseTheHitPath()
        {
            yield return StartRun();
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            ship.Damage.SetPlaytestInvulnerable(true);
            var sink = Get<IEnemySpawnSink>();
            var enemies = Get<IEnemyWorldView>();
            var dev = Get<IEnemyDevControl>();
            var shots = Get<IHostileProjectileView>();
            var hazards = Get<IHazardView>();
            // M8: the views are the V4 enemy world's; compare them with its own arrays.
            var world = V4TestEnemies.Simulation();

            // Gunners that fire at the ship and a Kraken that lays traps, so shots and hazards exist while comparing.
            foreach (var id in new[] { "cannonboat", "cannonboat", "kraken_spawn", "shark" })
            {
                var index = sink.FindDefinition(id);
                Assert.GreaterOrEqual(index, 0, id);
                sink.Spawn(index, ship.Position + new Vector3(10f, 0f, 6f), ConfigEnums.EliteAffix.None, 1f, 1f, 1f);
            }

            var pool = Object.FindFirstObjectByType<V4Game>().Shell.Enemies.Services.Shots.Pool;
            var sawShot = false;
            var end = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
                Assert.AreEqual(world.Agents.Length, enemies.Capacity);
                Assert.AreEqual(world.ActiveCount, enemies.ActiveCount);
                for (var i = 0; i < world.Agents.Length; i++)
                {
                    var a = world.Agents[i];
                    Assert.AreEqual(a.active, enemies.TryGet(i, out var e), $"slot {i} active");
                    if (!a.active) continue;
                    Assert.AreEqual(i, e.slot);
                    Assert.AreEqual(a.id, e.unitId);
                    Assert.AreEqual(world.Definitions[a.definitionIndex].id, e.enemyId);
                    Assert.AreEqual(a.position, e.position);
                    Assert.AreEqual(a.velocity, e.velocity);
                    Assert.AreEqual(a.radius, e.radius);
                    Assert.AreEqual(a.hp, e.hp);
                    Assert.AreEqual(a.isElite, e.isElite);
                    Assert.AreEqual(a.isBoss, e.isBoss);
                    Assert.IsTrue(enemies.TryFindByUnitId(a.id, out var found) && found == i);
                }
                Assert.AreEqual(pool.Length, shots.Capacity);
                for (var i = 0; i < pool.Length; i++)
                {
                    Assert.AreEqual(pool[i].active, shots.TryGet(i, out var s), $"shot {i} active");
                    if (!pool[i].active) continue;
                    sawShot = true;
                    Assert.AreEqual(pool[i].position, s.position);
                    Assert.AreEqual(pool[i].velocity, s.velocity);
                }
                Assert.AreEqual(HazardTelegraphs.DamagingSlots + HazardTelegraphs.WarningSlots, hazards.Capacity); // V5 M8z (6 Oct, owner): warnings have their own slots
            }
            Assert.IsTrue(sawShot, "the cannonboats fired, so the shot view was compared on live shots");

            var target = -1;
            for (var i = 0; i < enemies.Capacity && target < 0; i++) if (enemies.TryGet(i, out _)) target = i;
            Assert.GreaterOrEqual(target, 0);
            dev.SetHealthFraction(target, 0.5f);
            Assert.AreEqual(world.Agents[target].maxHp * 0.5f, world.Agents[target].hp, 0.001f);
            var kills = RunManager.Instance.KillCount;
            dev.Kill(target, "deck_cannons");
            Assert.IsFalse(world.Agents[target].active, "killed");
            Assert.AreEqual(kills + 1, RunManager.Instance.KillCount, "through the kill path (the kill is counted)");
            dev.DespawnAll();
            Assert.AreEqual(0, enemies.ActiveCount);
        }
    }
}
