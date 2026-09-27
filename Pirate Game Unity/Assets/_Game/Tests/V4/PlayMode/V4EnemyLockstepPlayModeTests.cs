using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Encounters;
using HighSeas.Enemies;
using HighSeas.Player;
using HighSeas.World;
using NUnit.Framework;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Legacy;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// M8 lockstep in the Module Lab (real islands, physics and V3's enemy models): the scene's V3 enemy world and a V4
    /// enemy world get the same crowd and are ticked side by side with the same ship path while the run is paused (so
    /// nothing else ticks the V3 world), compared every frame. Each world spawns its crowd in its own frame: a new ship
    /// model keeps its island colliders until the end of the frame (R-81), so spawning both in one frame would let the
    /// second world see the first world's models.
    /// </summary>
    [Category("V4")]
    public sealed class V4EnemyLockstepPlayModeTests
    {
        const float Timeout = 30f;
        static readonly FieldInfo HazardPlayer = typeof(SaltwindHazardTelegraph).GetField("_player", BindingFlags.Static | BindingFlags.NonPublic);
        static readonly FieldInfo ShotPool = typeof(HostileProjectileWorld).GetField("_pool", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo[] AgentFields = typeof(EnemyAgent).GetFields(BindingFlags.Instance | BindingFlags.Public);
        static readonly Dictionary<string, FieldInfo> UnitFields = typeof(EnemyUnit).GetFields(BindingFlags.Instance | BindingFlags.Public).ToDictionary(f => f.Name);

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
            RunMarksTracker.Reset();
            RunEncounterTracker.ResetForTests();
            SaltwindRunSettlement.ClearLastResults();
            _isolation?.Dispose();
            _isolation = null;
        }

        sealed class NoKills : IEnemyKillTargets
        {
            public int Count;
            public void OnKilled(in EnemyUnit unit, HighSeas.Data.EnemyDefinition def) => Count++;
        }

        static void Compare(EnemyWorld legacy, EnemySimulation v4, HostileProjectileWorld legacyShots, EnemyCombatServices s, int frame)
        {
            var la = legacy.Agents;
            var va = v4.Agents;
            Assert.AreEqual(legacy.ActiveCount, v4.ActiveCount, $"frame {frame}: active count");
            for (var i = 0; i < la.Length; i++)
            {
                Assert.AreEqual(la[i].active, va[i].active, $"frame {frame}: slot {i} active");
                if (!la[i].active) continue;
                object a = la[i];
                object b = va[i];
                foreach (var f in AgentFields)
                {
                    if (f.FieldType == typeof(Transform) || f.FieldType == typeof(SpawnProfile)) continue;
                    var x = f.GetValue(a);
                    var y = UnitFields[f.Name].GetValue(b);
                    if (f.FieldType == typeof(EnemySpawnRuntimeParams))
                    {
                        Assert.AreEqual(JsonUtility.ToJson(x), JsonUtility.ToJson(y), $"frame {frame}: slot {i} patternParams");
                        continue;
                    }
                    Assert.AreEqual(x, y, $"frame {frame}: slot {i} ({legacy.Definitions[la[i].definitionIndex].id}) {f.Name}");
                }
                if (la[i].view != null)
                {
                    Assert.AreEqual(la[i].view.position, va[i].view.position, $"frame {frame}: slot {i} view position");
                    Assert.AreEqual(la[i].view.rotation, va[i].view.rotation, $"frame {frame}: slot {i} view rotation");
                }
                var hasA = SaltwindCombatAgentStateStore.TryGet(la[i].id, out var sa);
                var hasB = v4.CombatStates.TryGet(la[i].id, out var sb);
                Assert.AreEqual(hasA, hasB);
                if (hasA)
                {
                    Assert.AreEqual(sa.phase, sb.phase, $"frame {frame}: slot {i} phase");
                    Assert.AreEqual(sa.phaseTimer, sb.phaseTimer, $"frame {frame}: slot {i} phaseTimer");
                    Assert.AreEqual(sa.committedDirection, sb.committedDirection, $"frame {frame}: slot {i} committedDirection");
                    Assert.AreEqual(sa.attackSequence, sb.attackSequence, $"frame {frame}: slot {i} attackSequence");
                }
            }
            var shots = (HostileProjectile[])ShotPool.GetValue(legacyShots);
            for (var i = 0; i < shots.Length; i++)
            {
                Assert.AreEqual(shots[i].active, s.Shots.Pool[i].active, $"frame {frame}: shot {i} active");
                if (shots[i].active) Assert.AreEqual(shots[i].position, s.Shots.Pool[i].position, $"frame {frame}: shot {i} position");
            }
            var legacyHazards = new LegacyHazardView();
            for (var i = 0; i < 24; i++)
            {
                Assert.AreEqual(legacyHazards.TryGet(i, out var ha), s.Hazards.TryGet(i, out var hb), $"frame {frame}: hazard {i}");
                if (legacyHazards.TryGet(i, out ha)) Assert.AreEqual(ha.center, hb.center, $"frame {frame}: hazard {i} center");
            }
        }

        [UnityTest]
        public IEnumerator ModuleLab_V4EnemyWorld_MatchesV3_AroundIslands()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.ModuleLab, LoadSceneMode.Single);
            var deadline = Time.realtimeSinceStartup + Timeout;
            while ((RunManager.Instance == null || RunManager.Instance.State != RunState.Playing) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(RunState.Playing, RunManager.Instance.State);
            Assert.IsTrue(IslandObstacles.IsAvailable, "the lab has islands");
            Assert.IsTrue(SaltwindCombatFeature.IsEnabled);

            var game = Object.FindFirstObjectByType<V4Game>();
            var legacy = Object.FindFirstObjectByType<EnemyWorld>();
            var legacyShots = HostileProjectileWorld.Instance ?? Object.FindFirstObjectByType<HostileProjectileWorld>();
            var legacyStatuses = StatusController.Instance;
            Assert.IsNotNull(legacyShots);
            Assert.IsNotNull(legacyStatuses);
            Assert.AreEqual(0, legacy.ActiveCount, "the lab starts empty");
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            HazardPlayer.SetValue(null, ship);
            // Nothing else ticks the V3 world while paused (the shell, weapons and spawner stop).
            RunManager.Instance.SetPause(PauseReason.UserPause);

            var s = new EnemyCombatServices();
            var target = new LegacyEnemyTarget(ship);
            var effects = new LegacyEnemyEffects();
            var root = new GameObject("v4-lockstep-world");
            var v4 = new EnemySimulation(legacy.Agents.Length, new LegacyEnemyRunState(), new LegacyEnemyViewSink(root.transform), new LegacyEnemySpawnParams(), new EnemyWorldSettings());
            v4.SetDefinitions(legacy.Definitions, EnemySimulation.BuildRules(game.Config, legacy.Definitions));
            s.Simulation = v4;
            s.Hazards = new HazardTelegraphs(target, effects);
            s.Budget = new EncounterBudget(() => StageBalanceProfileContext.Export?.Spawn?.MaxSimultaneousChargeWindups ?? 2,
                () => StageBalanceProfileContext.Export?.Spawn?.MaxSimultaneousAreaDenial ?? 2);
            s.Shots = new HostileProjectiles(new LegacyHostileShotViews(root.transform));
            s.Effects = effects;
            s.SpawnParams = new LegacyEnemySpawnParams();
            s.Behaviors = new EnemyCombatBehaviors(s);
            s.Specialists = new EnemySpecialistCombat(s);
            s.ReefCrab = new EnemyReefCrabCombat(s);
            s.SpecialBehaviors = new EnemySpecialBehaviors(s);
            var statuses = new EnemyStatuses();
            v4.Despawned += (slot, unit, profile) => statuses.ClearIndex(slot);
            var kills = new NoKills();
            v4.Bind(s, target, null, kills);
            // The V3 budget is static: start both from zero.
            SaltwindCombatEncounterBudget.ResetForRun();

            var islands = Physics.OverlapSphere(ship.Position, 400f, IslandObstacles.Mask, QueryTriggerInteraction.Ignore)
                .Select(c => c.bounds.center).Select(p => new Vector3(p.x, 0f, p.z))
                .OrderBy(p => (p - ship.Position).sqrMagnitude).Take(6).ToList();
            Assert.Greater(islands.Count, 0, "island colliders near the ship");
            var ids = new[] { "shark", "swordfish", "hammerhead", "rammer_raft", "cannonboat", "manta_ray", "bomb_boat", "kraken_spawn", "cutthroat_skiff", "deckhand_dinghy", "gunner_cog", "boarding_barge", "needle_sloop", "powder_barge", "net_cutter", "shield_frigate", "repair_tender", "treasure_raider", "reef_crab_miniboss" };
            var spawns = new List<(int index, Vector3 pos)>();
            for (var k = 0; k < ids.Length; k++)
            {
                var index = System.Array.FindIndex(legacy.Definitions, d => d != null && d.id == ids[k]);
                Assert.GreaterOrEqual(index, 0, ids[k]);
                var island = islands[k % islands.Count];
                spawns.Add((index, island + new Vector3(Mathf.Cos(k * 1.7f) * 9f, 0f, Mathf.Sin(k * 1.7f) * 9f)));
            }

            // Spawn each world's crowd in its own frame (R-81: new models keep island colliders until the frame ends).
            foreach (var (index, pos) in spawns) legacy.Spawn(index, pos);
            yield return null;
            yield return null;
            foreach (var (index, pos) in spawns) v4.Spawn(index, pos);
            yield return null;
            yield return null;
            Compare(legacy, v4, legacyShots, s, -1);

            var center = islands[0];
            for (var frame = 0; frame < 900; frame++)
            {
                var angle = frame * 0.012f;
                ship.SetWorldPosition(center + new Vector3(Mathf.Cos(angle) * 16f, 0f, Mathf.Sin(angle) * 16f));
                var dt = Time.deltaTime;
                legacy.Tick(dt, ship.Position, ship.Damage);
                legacyShots.Tick(dt, ship.Damage);
                legacyStatuses.Tick(dt, legacy);

                v4.Tick(dt, ship.Position, ship.Damage);
                s.Shots.Tick(dt, target);
                statuses.Tick(dt, v4.Agents, (i, d) => v4.ApplyDamage(i, d, false, false), (i, b) => v4.SetBurnVisual(i, b));

                if (frame % 25 == 24)
                {
                    var slot = System.Array.FindIndex(legacy.Agents, a => a.active);
                    if (slot >= 0)
                    {
                        legacy.ApplyDamage(slot, 6f, false, true, "deck_cannons");
                        v4.ApplyDamage(slot, 6f, false, true, "deck_cannons");
                        legacyStatuses.ApplyStatus(slot, StatusType.Burn, 1f);
                        statuses.ApplyStatus(slot, StatusType.Burn, 1f);
                    }
                }

                Compare(legacy, v4, legacyShots, s, frame);
                yield return null;
            }
            Assert.Greater(kills.Count, 0, "kills happened, so the kill timing was compared");
            v4.DespawnAllActive();
            s.Shots.ClearAllActive();
            Object.Destroy(root);
        }
    }
}
