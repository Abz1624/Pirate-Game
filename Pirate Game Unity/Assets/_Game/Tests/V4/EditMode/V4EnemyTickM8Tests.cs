using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.Player;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Legacy;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// M8 lockstep: V3's EnemyWorld + HostileProjectileWorld + StatusController and the V4 enemy simulation, hostile
    /// shots and statuses, fed the same crowd, the same ship path and the same hits, compared every frame (agents,
    /// combat states, shots, hazards, damage to the ship). Each world has its own ship: the ship's hit immunity frames use
    /// Time.time, which does not advance in edit mode, so a shared ship would block the second world's hits.
    /// </summary>
    [Category("V4")]
    public sealed class V4EnemyTickM8Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        const string ProfilePath = "Assets/_Game/Data/Balance/SaltwindSoloStageBalanceProfile.asset";
        const float Dt = 1f / 60f;

        static readonly FieldInfo HazardPlayer = typeof(SaltwindHazardTelegraph).GetField("_player", BindingFlags.Static | BindingFlags.NonPublic);
        static readonly FieldInfo HazardSlots = typeof(SaltwindHazardTelegraph).GetField("Slots", BindingFlags.Static | BindingFlags.NonPublic);
        static readonly FieldInfo ShotPool = typeof(HostileProjectileWorld).GetField("_pool", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo ShipVelocity = typeof(PlayerShipController).GetField("_velocity", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo ShipDamage = typeof(PlayerShipController).GetField("_damage", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo IFrameUntil = typeof(DamageReceiver).GetField("_iframeUntil", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo[] AgentFields = typeof(EnemyAgent).GetFields(BindingFlags.Instance | BindingFlags.Public);
        static readonly Dictionary<string, FieldInfo> UnitFields = typeof(EnemyUnit).GetFields(BindingFlags.Instance | BindingFlags.Public).ToDictionary(f => f.Name);

        readonly List<Object> _created = new List<Object>();
        HashSet<GameObject> _rootsBefore;
        readonly Dictionary<EnemyCombatProfile, bool> _latches = new Dictionary<EnemyCombatProfile, bool>();

        [SetUp]
        public void SetUp()
        {
            // The elite hammerhead latch writes the shared registry profile (V3 quirk, R-100): restore it afterwards.
            foreach (var id in new[] { "hammerhead", "elite_hammerhead" })
                if (EnemyCombatProfileRegistry.TryGet(id, out var profile)) _latches[profile] = profile.eliteHammerheadSecondCharge;
            ResetLegacyStatics();
            _rootsBefore = new HashSet<GameObject>(UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (!_rootsBefore.Contains(root)) Object.DestroyImmediate(root);
            ResetLegacyStatics();
            StageBalanceProfileContext.ResetForTests();
            foreach (var kv in _latches) kv.Key.eliteHammerheadSecondCharge = kv.Value;
            _latches.Clear();
        }

        static void ResetLegacyStatics()
        {
            SaltwindCombatAgentStateStore.ClearAll();
            SaltwindHazardTelegraph.ClearAll();
            // Fresh slots, as in a first V3 run: V3's static slots keep fields such as "armed" across runs (only mines
            // read it, and a new mine resets it), which a per-run V4 world never has.
            System.Array.Clear((System.Array)HazardSlots.GetValue(null), 0, 24);
            HazardPlayer.SetValue(null, null);
            SaltwindCombatEncounterBudget.ResetForRun();
        }

        GameDatabase CatalogAppliedDatabase()
        {
            var db = RoguePirates.V4.Editor.V3ConfigMigrator.LoadCleanDatabase();
            var clone = Object.Instantiate(db);
            _created.Add(clone);
            clone.enemies = db.enemies.Select(e => { if (e == null) return null; var c = Object.Instantiate(e); c.name = e.name; _created.Add(c); return c; }).ToArray();
            clone.weapons = db.weapons.Select(w => { if (w == null) return null; var c = Object.Instantiate(w); c.name = w.name; _created.Add(c); return c; }).ToArray();
            CombatWeaponCatalog.ApplyToDatabase(clone, CombatExperimentContext.Active);
            CombatSpecialistEnemyCatalog.ApplyToDatabase(clone, CombatExperimentContext.Active);
            return clone;
        }

        PlayerShipController MakeShip(string name, List<string> damageLog)
        {
            var go = new GameObject(name);
            _created.Add(go);
            var damage = go.AddComponent<DamageReceiver>();
            var ship = go.AddComponent<PlayerShipController>();
            ShipDamage.SetValue(ship, damage);
            damage.Initialize(1000000f);
            damage.Damaged += (amount, crit) => damageLog.Add($"{amount:R}");
            return ship;
        }

        static T Awake<T>(GameObject host) where T : MonoBehaviour
        {
            var c = host.AddComponent<T>();
            typeof(T).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(c, null);
            return c;
        }

        sealed class Worlds
        {
            public EnemyWorld Legacy;
            public HostileProjectileWorld LegacyShots;
            public StatusController LegacyStatuses;
            public EnemySimulation V4;
            public EnemyCombatServices Services;
            public EnemyStatuses V4Statuses;
            public PlayerShipController ShipA, ShipB;
            public readonly List<string> DamageA = new List<string>(), DamageB = new List<string>();
            public readonly List<string> V4Kills = new List<string>();
            public EnemyDefinition[] Defs;
        }

        sealed class RecordingKills : IEnemyKillTargets
        {
            readonly List<string> _log;
            public RecordingKills(List<string> log) { _log = log; }
            public void OnKilled(in EnemyUnit unit, EnemyDefinition def) => _log.Add($"{unit.id}:{def.id}");
        }

        Worlds Make(ConfigSetRuntime config, bool v3Rules = true)
        {
            if (v3Rules)
            {
                // Lockstep against V3: V3's hammerhead latch (R-100), which the default config fixes.
                Assert.IsTrue(config.TryApplyEdit("enemy.hammerhead", "world.eliteLatchesSecondChargeForAll", "true", 0f, out _));
                Assert.IsTrue(config.TryApplyEdit("enemy.hammerhead", "world.eliteChargesTwice", "false", 0f, out _));
            }
            var runtime = new V4RuntimeDatabase(config);
            var db = runtime.Build(CatalogAppliedDatabase());
            _created.Add(db);
            var w = new Worlds { Defs = db.enemies };
            // V3's kill path rolls kill gold on the run's Director stream.
            var runHost = new GameObject("run");
            _created.Add(runHost);
            Awake<RunManager>(runHost).BeginRun(101);
            w.ShipA = MakeShip("ship-legacy", w.DamageA);
            w.ShipB = MakeShip("ship-v4", w.DamageB);

            var legacyHost = new GameObject("legacy-world");
            _created.Add(legacyHost);
            w.Legacy = Awake<EnemyWorld>(legacyHost);
            w.Legacy.SetDefinitions(db.enemies);
            w.LegacyShots = Awake<HostileProjectileWorld>(legacyHost);
            w.LegacyStatuses = Awake<StatusController>(legacyHost);
            HazardPlayer.SetValue(null, w.ShipA);

            var v4Host = new GameObject("v4-world");
            _created.Add(v4Host);
            var s = new EnemyCombatServices();
            var target = new LegacyEnemyTarget(w.ShipB);
            var effects = new LegacyEnemyEffects();
            w.V4 = new EnemySimulation(w.Legacy.Agents.Length, new LegacyEnemyRunState(), new LegacyEnemyViewSink(v4Host.transform), new LegacyEnemySpawnParams(),
                new EnemyWorldSettings { islandAvoidanceProbeDistance = 10f }); // the component's code default (the scene sets 6)
            w.V4.SetDefinitions(db.enemies, EnemySimulation.BuildRules(config, db.enemies));
            s.Simulation = w.V4;
            s.Hazards = new HazardTelegraphs(target, effects);
            s.Budget = new EncounterBudget(() => StageBalanceProfileContext.Export?.Spawn?.MaxSimultaneousChargeWindups ?? 2,
                () => StageBalanceProfileContext.Export?.Spawn?.MaxSimultaneousAreaDenial ?? 2);
            s.Shots = new HostileProjectiles(new LegacyHostileShotViews(v4Host.transform));
            s.Effects = effects;
            s.SpawnParams = new LegacyEnemySpawnParams();
            s.Behaviors = new EnemyCombatBehaviors(s);
            s.Specialists = new EnemySpecialistCombat(s);
            s.ReefCrab = new EnemyReefCrabCombat(s);
            s.SpecialBehaviors = new EnemySpecialBehaviors(s);
            w.Services = s;
            w.V4Statuses = new EnemyStatuses();
            w.V4.Despawned += (slot, unit, profile) => w.V4Statuses.ClearIndex(slot);
            w.V4.Bind(s, target, null, new RecordingKills(w.V4Kills));
            return w;
        }

        static void CompareFrame(Worlds w, int frame)
        {
            var la = w.Legacy.Agents;
            var va = w.V4.Agents;
            Assert.AreEqual(w.Legacy.ActiveCount, w.V4.ActiveCount, $"frame {frame}: active count");
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
                        Assert.AreEqual(JsonUtility.ToJson(x), JsonUtility.ToJson(y), $"frame {frame}: slot {i} ({la[i].id}) patternParams");
                        continue;
                    }
                    Assert.AreEqual(x, y, $"frame {frame}: slot {i} ({la[i].id} {w.Defs[la[i].definitionIndex].id}) {f.Name}");
                }
                Assert.AreEqual(la[i].spawnProfile, va[i].spawnProfile);
                Assert.AreEqual(la[i].view != null, va[i].view != null);
                if (la[i].view != null)
                {
                    Assert.AreEqual(la[i].view.position, va[i].view.position, $"frame {frame}: slot {i} view position");
                    Assert.AreEqual(la[i].view.rotation, va[i].view.rotation, $"frame {frame}: slot {i} view rotation");
                }
                var hasA = SaltwindCombatAgentStateStore.TryGet(la[i].id, out var sa);
                var hasB = w.V4.CombatStates.TryGet(la[i].id, out var sb);
                Assert.AreEqual(hasA, hasB, $"frame {frame}: slot {i} combat state exists");
                if (hasA)
                {
                    var tag = $"frame {frame}: slot {i} ({w.Defs[la[i].definitionIndex].id}) state";
                    Assert.AreEqual(sa.phase, sb.phase, tag + " phase");
                    Assert.AreEqual(sa.phaseTimer, sb.phaseTimer, tag + " phaseTimer");
                    Assert.AreEqual(sa.attackSequence, sb.attackSequence, tag + " attackSequence");
                    Assert.AreEqual(sa.attackId, sb.attackId, tag + " attackId");
                    Assert.AreEqual(sa.committedDirection, sb.committedDirection, tag + " committedDirection");
                    Assert.AreEqual(sa.committedPosition, sb.committedPosition, tag + " committedPosition");
                    Assert.AreEqual(sa.orbitSign, sb.orbitSign, tag + " orbitSign");
                    Assert.AreEqual(sa.chargePassCount, sb.chargePassCount, tag + " chargePassCount");
                    Assert.AreEqual(sa.contactDisabled, sb.contactDisabled, tag + " contactDisabled");
                    Assert.AreEqual(sa.lastDeferReason, sb.lastDeferReason, tag + " lastDeferReason");
                }
            }

            var shots = (HostileProjectile[])ShotPool.GetValue(w.LegacyShots);
            var v4Shots = w.Services.Shots.Pool;
            for (var i = 0; i < shots.Length; i++)
            {
                Assert.AreEqual(shots[i].active, v4Shots[i].active, $"frame {frame}: shot {i} active");
                if (!shots[i].active) continue;
                Assert.AreEqual(shots[i].position, v4Shots[i].position, $"frame {frame}: shot {i} position");
                Assert.AreEqual(shots[i].velocity, v4Shots[i].velocity, $"frame {frame}: shot {i} velocity");
                Assert.AreEqual(shots[i].life, v4Shots[i].life, $"frame {frame}: shot {i} life");
                Assert.AreEqual(shots[i].damage, v4Shots[i].damage, $"frame {frame}: shot {i} damage");
            }

            var legacyHazards = new LegacyHazardView();
            for (var i = 0; i < 24; i++)
            {
                var a = legacyHazards.TryGet(i, out var ha);
                var b = w.Services.Hazards.TryGet(i, out var hb);
                Assert.AreEqual(a, b, $"frame {frame}: hazard {i} active");
                if (!a) continue;
                Assert.AreEqual(ha.center, hb.center, $"frame {frame}: hazard {i} center");
                Assert.AreEqual(ha.radius, hb.radius, $"frame {frame}: hazard {i} radius");
                Assert.AreEqual(ha.timer, hb.timer, $"frame {frame}: hazard {i} timer");
                Assert.AreEqual(ha.armed, hb.armed, $"frame {frame}: hazard {i} armed");
            }

            CollectionAssert.AreEqual(w.DamageA, w.DamageB, $"frame {frame}: damage to the ship");
            Assert.AreEqual(w.ShipA.Damage.CurrentHP, w.ShipB.Damage.CurrentHP, $"frame {frame}: ship hull");
        }

        void SpawnBoth(Worlds w, System.Func<EnemyWorld, int> legacy, System.Func<EnemySimulation, int> v4, string step)
        {
            Assert.AreEqual(legacy(w.Legacy), v4(w.V4), step);
        }

        void Run(Worlds w, int frames, System.Func<int, Vector3> path, int hitEvery = 20, System.Action<int> beforeTick = null)
        {
            for (var frame = 0; frame < frames; frame++)
            {
                beforeTick?.Invoke(frame);
                var pos = path(frame);
                var next = path(frame + 1);
                var velocity = (next - pos) / Dt;
                foreach (var ship in new[] { w.ShipA, w.ShipB })
                {
                    ship.transform.position = pos;
                    ShipVelocity.SetValue(ship, velocity);
                    IFrameUntil.SetValue(ship.Damage, -1f); // edit mode: Time.time is frozen, so clear the hit immunity each frame
                }

                w.Legacy.Tick(Dt, w.ShipA.Position, w.ShipA.Damage);
                w.LegacyShots.Tick(Dt, w.ShipA.Damage);
                w.LegacyStatuses.Tick(Dt, w.Legacy);

                w.V4.Tick(Dt, w.ShipB.Position, w.ShipB.Damage);
                w.Services.Shots.Tick(Dt, new LegacyEnemyTarget(w.ShipB));
                w.V4Statuses.Tick(Dt, w.V4.Agents, (i, d) => w.V4.ApplyDamage(i, d, false, false), (i, b) => w.V4.SetBurnVisual(i, b));

                if (hitEvery > 0 && frame % hitEvery == hitEvery - 1)
                {
                    // Same weapon hit and burn on the same slot in both worlds (the lowest active slot, then the next).
                    var slot = System.Array.FindIndex(w.Legacy.Agents, a => a.active);
                    if (slot >= 0)
                    {
                        var damage = 3f + frame % 7;
                        w.Legacy.ApplyDamage(slot, damage, frame % 3 == 0, true, "deck_cannons");
                        w.V4.ApplyDamage(slot, damage, frame % 3 == 0, true, "deck_cannons");
                        w.LegacyStatuses.ApplyStatus(slot, StatusType.Burn, 1f);
                        w.V4Statuses.ApplyStatus(slot, StatusType.Burn, 1f);
                    }
                }

                CompareFrame(w, frame);
            }
        }

        static Vector3 Circle(int frame, float radius, float speed)
        {
            var angle = frame * Dt * speed / radius;
            return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        [Test]
        public void R100_OnlyTheEliteHammerheadChargesTwice_AndTheSharedProfileStaysUnchanged()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            StageBalanceProfileContext.BeginRun(AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(ProfilePath));
            var w = Make(config, v3Rules: false);
            Assert.IsTrue(EnemyCombatProfileRegistry.TryGet("hammerhead", out var shared));
            shared.eliteHammerheadSecondCharge = false;
            var hammerhead = System.Array.FindIndex(w.Defs, d => d != null && d.id == "hammerhead");
            var elite = w.V4.Spawn(hammerhead, new Vector3(16f, 0f, 0f), EliteAffix.Rich);
            var plain = w.V4.Spawn(hammerhead, new Vector3(-16f, 0f, 0f));
            Assert.IsTrue(w.V4.Agents[elite].isElite && !w.V4.Agents[plain].isElite);
            bool eliteSecond = false, plainSecond = false;
            for (var frame = 0; frame < 1800; frame++)
            {
                w.ShipB.transform.position = Vector3.zero;
                IFrameUntil.SetValue(w.ShipB.Damage, -1f);
                w.V4.Tick(Dt, w.ShipB.Position, w.ShipB.Damage);
                foreach (var (slot, isElite) in new[] { (elite, true), (plain, false) })
                {
                    if (!w.V4.Agents[slot].active || !w.V4.CombatStates.TryGet(w.V4.Agents[slot].id, out var state)) continue;
                    // A second wind-up straight after the first pass is the second charge.
                    if (state.chargePassCount == 1 && state.phase == EnemyCombatPhase.Telegraph)
                    {
                        if (isElite) eliteSecond = true; else plainSecond = true;
                    }
                }
            }
            Assert.IsTrue(eliteSecond, "the elite hammerhead charges twice");
            Assert.IsFalse(plainSecond, "a plain hammerhead charges once");
            Assert.IsFalse(shared.eliteHammerheadSecondCharge, "the shared combat profile is not latched");
        }

        [Test]
        public void Crowd_EveryCombatArchetype_MatchesV3_FrameByFrame()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true; // V3's Destroy calls log in edit mode
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            StageBalanceProfileContext.BeginRun(AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(ProfilePath));
            Assert.IsTrue(SaltwindCombatFeature.IsEnabled);
            var w = Make(config);

            // Every enemy of the run database around the ship's path, then every Saltwind spawn profile.
            var k = 0;
            for (var d = 0; d < w.Defs.Length; d++)
            {
                if (w.Defs[d] == null || w.Defs[d].isBoss) continue;
                var index = d;
                var pos = Circle(0, 12f, 7f) + new Vector3(Mathf.Cos(k * 0.9f) * (14f + k % 5), 0f, Mathf.Sin(k * 0.9f) * (14f + k % 5));
                var affix = k % 4 == 1 ? EliteAffix.Rich : EliteAffix.None;
                SpawnBoth(w, e => e.Spawn(index, pos, affix), s => s.Spawn(index, pos, affix), "spawn " + w.Defs[d].id);
                k++;
            }
            var profiles = AssetDatabase.FindAssets("t:SpawnProfile", new[] { "Assets/_Game/Data/SpawnProfiles/Saltwind" })
                .Select(g => AssetDatabase.LoadAssetAtPath<SpawnProfile>(AssetDatabase.GUIDToAssetPath(g))).Where(p => p != null && p.enemy != null).ToList();
            foreach (var profile in profiles)
            {
                var index = System.Array.FindIndex(w.Defs, e => e != null && e.id == profile.enemy.id);
                if (index < 0) continue;
                var pos = new Vector3(Mathf.Cos(k * 1.3f) * 20f, 0f, Mathf.Sin(k * 1.3f) * 20f);
                var instance = 50 + k;
                SpawnBoth(w, e => e.SpawnFromProfile(index, pos, profile, instance, FormationRole.Auto, 0, -1), s => s.SpawnFromProfile(index, pos, profile, instance, FormationRole.Auto, 0, -1), "profile " + profile.name);
                k++;
            }
            CompareFrame(w, -1);

            Run(w, 1500, f => Circle(f, 12f, 7f));
            Assert.Greater(w.DamageA.Count, 0, "the crowd hurt the ship, so attacks were compared");
            Assert.Greater(w.V4Kills.Count, 0, "hits and burns killed enemies, so the kill path was compared");
        }

        [Test]
        public void Boss_WithTheDirectorsHoldAndCharge_MatchesV3()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            StageBalanceProfileContext.BeginRun(AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(ProfilePath));
            var w = Make(config);
            var profile = AssetDatabase.LoadAssetAtPath<SpawnProfile>("Assets/_Game/Data/SpawnProfiles/Saltwind/SaltwindBossSummon.asset");
            Assert.IsNotNull(profile);
            var boss = System.Array.FindIndex(w.Defs, e => e != null && e.id == profile.enemy.id);
            Assert.IsTrue(w.Defs[boss].isBoss);
            SpawnBoth(w, e => e.SpawnFromProfile(boss, new Vector3(18f, 0f, 4f), profile, 900, FormationRole.Auto, 0, -1),
                s => s.SpawnFromProfile(boss, new Vector3(18f, 0f, 4f), profile, 900, FormationRole.Auto, 0, -1), "boss");
            var shark = System.Array.FindIndex(w.Defs, e => e != null && e.id == "shark");
            SpawnBoth(w, e => e.Spawn(shark, new Vector3(-12f, 0f, 3f)), s => s.Spawn(shark, new Vector3(-12f, 0f, 3f)), "escort");
            Run(w, 900, f => Circle(f, 10f, 6f), hitEvery: 40, beforeTick: f =>
            {
                Assert.IsTrue(w.Legacy.TryFindActiveBoss(out var ls, out _) & w.V4.TryFindActiveBoss(out var vs, out _));
                Assert.AreEqual(ls, vs);
                if (f >= 120 && f < 200) { w.Legacy.SetBossAttackControl(ls, true, Vector3.zero, false); w.V4.SetBossAttackControl(vs, true, Vector3.zero, false); }
                else if (f >= 300 && f < 340) { var v = new Vector3(9f, 0f, -3f); w.Legacy.SetBossAttackControl(ls, false, v, true); w.V4.SetBossAttackControl(vs, false, v, true); }
                else { w.Legacy.ClearBossAttackControl(); w.V4.ClearBossAttackControl(); }
            });
        }

        sealed class StubHellfire : IHellfireEnvironment
        {
            public bool RunActive => RunManager.Instance != null && !RunManager.Instance.IsPaused && !RunManager.Instance.IsRunEnded;
            public float RollTickDamage(bool elite) => DamageSystem.RollDamage(4f, 1f, RunManager.Instance.Stats, elite, 0f, RunManager.Instance.Rng, 0f, "flamethrower");
            public bool IsHellfireWeapon(string weaponId) => true;
            public void OilDropped(Vector3 position, float radius) { }
        }

        [Test]
        public void HellfirePatches_BurnTheSameEnemies_WithTheSameRolls()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            StageBalanceProfileContext.BeginRun(AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(ProfilePath));
            var w = Make(config);
            var shark = System.Array.FindIndex(w.Defs, e => e != null && e.id == "shark");
            var cog = System.Array.FindIndex(w.Defs, e => e != null && e.id == "gunner_cog");
            for (var k = 0; k < 10; k++)
            {
                var pos = new Vector3(k % 5 * 1.2f, 0f, k / 5 * 1.5f);
                var index = k % 3 == 0 ? cog : shark;
                var affix = k % 4 == 0 ? EliteAffix.Rich : EliteAffix.None;
                SpawnBoth(w, e => e.Spawn(index, pos, affix), s => s.Spawn(index, pos, affix), "crowd " + k);
            }
            var patches = new[] { new Vector3(0.5f, 0f, 0.5f), new Vector3(3f, 0f, 1f), new Vector3(5f, 0f, 0f) };

            // V3: the component on the legacy world's object, patches added where a flame kill would add them.
            RunManager.Instance.BeginRun(101);
            RunManager.Instance.EnterPlaying();
            var legacy = w.Legacy.gameObject.AddComponent<HellfirePatchWorld>();
            typeof(HellfirePatchWorld).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(legacy, null);
            var add = typeof(HellfirePatchWorld).GetMethod("AddPatch", BindingFlags.Instance | BindingFlags.NonPublic);
            var update = typeof(HellfirePatchWorld).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var p in patches) add.Invoke(legacy, new object[] { p });
            var legacyHp = new List<float>();
            for (var f = 0; f < 200; f++)
            {
                update.Invoke(legacy, null);
                legacyHp.AddRange(w.Legacy.Agents.Take(12).Select(a => a.active ? a.hp : -1f));
            }

            RunManager.Instance.BeginRun(101);
            RunManager.Instance.EnterPlaying();
            var v4 = new HellfirePatches(w.V4, w.V4Statuses, new StubHellfire());
            foreach (var p in patches) v4.TrySpawnOnDirectFlameKill(p, "any");
            var v4Hp = new List<float>();
            for (var f = 0; f < 200; f++)
            {
                v4.Tick(Time.deltaTime);
                v4Hp.AddRange(w.V4.Agents.Take(12).Select(a => a.active ? a.hp : -1f));
            }
            CollectionAssert.AreEqual(legacyHp, v4Hp, "same damage rolls, same enemies, same kills");
            Assert.IsTrue(legacyHp.Any(h => h < 0f) || legacyHp.Last() < legacyHp.First(), "the patches did damage");
            Object.DestroyImmediate(legacy);
        }

        sealed class RecordingFeedback : IEnemyFeedback
        {
            public readonly List<string> Log;
            public RecordingFeedback(List<string> log) { Log = log; }
            public void DamageHighlight(Transform view) => Log.Add("highlight");
            public void HitShakeAndSound(float damage) => Log.Add("shake+sound");
            public void RaiseHit(float damage, bool isCrit, Vector3 position, string weaponId) => Log.Add($"hit {damage:R} {isCrit} {weaponId}");
            public void DamageNumber(Vector3 position, float damage, bool isCrit) => Log.Add($"number {damage:R}");
        }

        sealed class OrderKills : IEnemyKillTargets
        {
            readonly List<string> _log;
            readonly EnemySimulation _sim;
            public OrderKills(List<string> log, EnemySimulation sim) { _log = log; _sim = sim; }
            public void OnKilled(in EnemyUnit unit, EnemyDefinition def)
            {
                var id = unit.id;
                _log.Add($"killed {id} active={_sim.Agents[System.Array.FindIndex(_sim.Agents, a => a.id == id)].active}");
            }
        }

        [Test]
        public void HitPath_FeedbackAndKill_FollowV3sOrder()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            StageBalanceProfileContext.BeginRun(AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(ProfilePath));
            var w = Make(config);
            var log = new List<string>();
            w.V4.Bind(w.Services, new LegacyEnemyTarget(w.ShipB), new RecordingFeedback(log), new OrderKills(log, w.V4));
            w.V4.Despawned += (slot, unit, profile) => log.Add($"despawned {unit}");
            w.V4.SuppressHitHighlight = id => id == "powder_kegs";
            var shark = System.Array.FindIndex(w.Defs, e => e != null && e.id == "shark");
            var slot = w.V4.Spawn(shark, new Vector3(5f, 0f, 0f));
            var unitId = w.V4.Agents[slot].id;
            var maxHp = w.V4.Agents[slot].maxHp;

            // V3 EnemyWorld.ApplyDamage: highlight (not for Powder Kegs), knockback + shake + sound (damage > 0.25),
            // TriggerBus hit, damage number (damage >= 0.5); a lethal hit then runs the kill fan-out and the despawn.
            w.V4.ApplyDamage(slot, 1f, true, true, "deck_cannons");
            CollectionAssert.AreEqual(new[] { "highlight", "shake+sound", "hit 1 True deck_cannons", "number 1" }, log);
            Assert.Greater(w.V4.Agents[slot].knockbackTimer, 0f, "knockback away from the ship");
            log.Clear();
            w.V4.ApplyDamage(slot, 0.4f, false, true, "powder_kegs");
            CollectionAssert.AreEqual(new[] { "shake+sound", "hit 0.4 False powder_kegs" }, log, "no highlight for Powder Kegs, no number under 0.5");
            log.Clear();
            w.V4.ApplyDamage(slot, 0.2f, false, false, null);
            CollectionAssert.AreEqual(new[] { "hit 0.2 False " }, log, "burn ticks: the hit event only");
            log.Clear();
            w.V4.ApplyDamage(slot, maxHp * 3f, false, true, "deck_cannons");
            Assert.AreEqual("killed " + unitId + " active=True", log[4], "the fan-out runs before the slot is freed");
            Assert.AreEqual("despawned " + unitId, log[5]);
            Assert.AreEqual(6, log.Count);
            Assert.IsFalse(w.V4.Agents[slot].active);
            Assert.AreEqual("deck_cannons", w.V4.Agents[slot].lastHitWeaponId, "the last hit's weapon is kept for the kill event");
        }


        [Test]
        public void StandingStill_ThenSprinting_MatchesV3()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            StageBalanceProfileContext.BeginRun(AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(ProfilePath));
            var w = Make(config);
            var ids = new[] { "shark", "swordfish", "hammerhead", "cannonboat", "manta_ray", "bomb_boat", "kraken_spawn", "cutthroat_skiff", "deckhand_dinghy", "rammer_raft", "gunner_cog", "reef_crab_miniboss" };
            for (var k = 0; k < ids.Length; k++)
            {
                var index = System.Array.FindIndex(w.Defs, e => e != null && e.id == ids[k]);
                Assert.GreaterOrEqual(index, 0, ids[k]);
                var pos = new Vector3(Mathf.Cos(k) * 9f, 0f, Mathf.Sin(k) * 9f);
                SpawnBoth(w, e => e.Spawn(index, pos), s => s.Spawn(index, pos), ids[k]);
            }
            Run(w, 400, f => Vector3.zero, hitEvery: 0);
            Run(w, 800, f => new Vector3(f * Dt * 11f, 0f, Mathf.Sin(f * 0.02f) * 6f), hitEvery: 45);
        }
    }
}
