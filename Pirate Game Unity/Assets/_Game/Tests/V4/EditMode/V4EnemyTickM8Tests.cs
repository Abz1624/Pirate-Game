using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.Enemies;
using RoguePirates.V4.Foundation.Player;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Ports;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// M8: the V4 enemy simulation with its combat services, hostile shots and statuses in edit mode: the R-100 hammerhead
    /// charge rule and the order of the hit path (feedback, kill fan-out, despawn). The ship's hit immunity frames use
    /// Time.time, which does not advance in edit mode, so tests clear them each frame.
    /// </summary>
    [Category("V4")]
    public sealed class V4EnemyTickM8Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        const float Dt = 1f / 60f;

        static readonly FieldInfo ShipDamage = typeof(PlayerShipController).GetField("_damage", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo IFrameUntil = typeof(DamageReceiver).GetField("_iframeUntil", BindingFlags.Instance | BindingFlags.NonPublic);

        readonly List<Object> _created = new List<Object>();
        HashSet<GameObject> _rootsBefore;

        [SetUp]
        public void SetUp()
        {
            _rootsBefore = new HashSet<GameObject>(UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (!_rootsBefore.Contains(root)) Object.DestroyImmediate(root);
            StageBalanceProfileContext.ResetForTests();
        }

        GameDatabase LoadedDatabase()
        {
            // After a Play Mode session the cached experiment profile can be a destroyed object (register R-82): re-resolve it.
            if (CombatExperimentContext.Active == null) CombatExperimentContext.ResetForTests();
            var db = GameDatabaseLoader.Load();
            var clone = Object.Instantiate(db);
            _created.Add(clone);
            clone.enemies = db.enemies.Select(e => { if (e == null) return null; var c = Object.Instantiate(e); c.name = e.name; _created.Add(c); return c; }).ToArray();
            clone.weapons = db.weapons.Select(w => { if (w == null) return null; var c = Object.Instantiate(w); c.name = w.name; _created.Add(c); return c; }).ToArray();
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
            public EnemySimulation V4;
            public EnemyCombatServices Services;
            public EnemyStatuses V4Statuses;
            public PlayerShipController ShipB;
            public readonly List<string> DamageB = new List<string>();
            public readonly List<string> V4Kills = new List<string>();
            public EnemyDefinition[] Defs;
        }

        sealed class RecordingKills : IEnemyKillTargets
        {
            readonly List<string> _log;
            public RecordingKills(List<string> log) { _log = log; }
            public void OnKilled(in EnemyUnit unit, EnemyDefinition def) => _log.Add($"{unit.id}:{def.id}");
        }

        /// <summary>Bare model objects (no pool, no presentation), so a unit has a view and hits take the highlight path.</summary>
        sealed class BareViews : IEnemyViewSink
        {
            public void Prepare(EnemyDefinition[] definitions) { }
            public float FallbackVisualScale(EnemyDefinition def) => EnemyVisualFallbackResolver.ResolveVisualScale(def);
            public Transform CreateView(EnemyDefinition def, Vector3 position, float scale, bool elite, int animationSeed)
            {
                var go = new GameObject(def.id);
                go.transform.position = position;
                return go.transform;
            }
            public Transform AttachHealthBar(EnemyDefinition def, Transform view, bool elite) => null;
            public void Release(EnemyDefinition def, Transform view, Transform healthBar) { if (view != null) Object.DestroyImmediate(view.gameObject); }
            public void TickSpring(Transform view, ref bool springing, bool isSpringing) { }
            public void TickWake(Transform view, EnemyCategory category, Vector3 velocity) { }
            public void SetBurning(Transform view, bool burning) { }
            public void SetHealthFill(Transform healthBar, float fill) { }
        }

        Worlds Make(ConfigSetRuntime config)
        {
            var runtime = new V4RuntimeDatabase(config);
            var db = runtime.Build(LoadedDatabase());
            _created.Add(db);
            var w = new Worlds { Defs = db.enemies };
            // The run the simulation's run port reads.
            var runHost = new GameObject("run");
            _created.Add(runHost);
            Awake<RunManager>(runHost).BeginRun(101);
            w.ShipB = MakeShip("ship-v4", w.DamageB);

            var s = new EnemyCombatServices();
            var target = new ShipPort(w.ShipB);
            var effects = new RoguePirates.V4.Views.EnemyEffectsForwarder(null);
            // M14: the V4 resolver over per-run combat profiles; combat on when the test's profile context enables it.
            var spawnParams = new EnemySpawnParamResolver(new EnemyCombatProfiles(config), config) { CombatEnabled = SaltwindCombatFeature.IsEnabled };
            w.V4 = new EnemySimulation(RoguePirates.V4.Foundation.Platform.QualityProfile.Current.enemyCap, new RunPort(), new BareViews(), spawnParams,
                new EnemyWorldSettings { islandAvoidanceProbeDistance = 10f }); // the component's code default (the scene sets 6)
            w.V4.SetDefinitions(db.enemies, EnemySimulation.BuildRules(config, db.enemies));
            s.Simulation = w.V4;
            s.Hazards = new HazardTelegraphs(target, effects);
            s.Budget = new EncounterBudget(() => StageBalanceProfileContext.Export?.Spawn?.MaxSimultaneousChargeWindups ?? 2,
                () => StageBalanceProfileContext.Export?.Spawn?.MaxSimultaneousAreaDenial ?? 2);
            s.Shots = new HostileProjectiles(new RoguePirates.V4.Views.HostileShotViewsForwarder(null));
            s.Effects = effects;
            s.SpawnParams = spawnParams;
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

        [Test]
        public void R100_OnlyTheEliteHammerheadChargesTwice_AndTheSharedProfileStaysUnchanged()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            using var profilePort = new RoguePirates.V4.Ports.StageProfilePort(config); // M15: the profile from the V4 configs (V3's asset is gone)
            StageBalanceProfileContext.BeginRun(profilePort.Profile);
            var w = Make(config);
            // M14: V4's run copy of the hammerhead profile (V3's shared registry is no longer read or written by V4).
            Assert.IsTrue(w.Services.SpawnParams.TryGetCombatProfile("hammerhead", out var shared));
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

        /// <summary>
        /// V5 (owner, 10 Oct): the leash puts an ordinary enemy left 45 m behind back ahead of the ship at
        /// <see cref="RunConfig.enemyLeashReturnDistance"/> (31 m; a 26 m constant before), which is just outside the top of the
        /// run scene's top-down view: the camera's height, distance back and field of view are read from the scene, and the
        /// ground it sees ahead of the ship at the top edge of the screen is worked out from them (30.5 m).
        /// </summary>
        [Test]
        public void Leash_PutsEnemiesBackAtTheConfiguredDistance_JustOutsideTheTopOfTheView()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            // The camera, from the run scene (FollowCamera's top-down offset and the camera's vertical field of view).
            var scene = System.IO.File.ReadAllText("Assets/_Game/Scenes/V4/V4SaltwindRun.unity");
            var offset = System.Text.RegularExpressions.Regex.Match(scene, @"\n  offset: \{x: ([-\d.]+), y: ([-\d.]+), z: ([-\d.]+)\}");
            var fov = System.Text.RegularExpressions.Regex.Match(scene, @"field of view: ([\d.]+)");
            Assert.IsTrue(offset.Success && fov.Success, "the run scene's camera offset and field of view");
            var height = float.Parse(offset.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            var back = -float.Parse(offset.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
            var halfFov = float.Parse(fov.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) * 0.5f;
            var pitch = Mathf.Atan2(height, back) * Mathf.Rad2Deg; // the camera looks at the ship
            var viewAhead = height / Mathf.Tan((pitch - halfFov) * Mathf.Deg2Rad) - back;
            Assert.AreEqual(30.5f, viewAhead, 0.1f, "the top edge of the view is 30.5 m ahead of the ship (25 m up, 20 m back, 50 degrees)");

            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            var run = config.Single<RunConfig>();
            Assert.AreEqual(31f, run.enemyLeashReturnDistance, 1e-4f, "the owner's 31 m (10 Oct)");
            Assert.Greater(run.enemyLeashReturnDistance, viewAhead, "put back outside the view ahead of the ship");
            Assert.Less(run.enemyLeashReturnDistance, RunConfig.EnemyLeashDistance - 5f, "well inside the leash distance (not leashed again)");

            using var profilePort = new RoguePirates.V4.Ports.StageProfilePort(config);
            StageBalanceProfileContext.BeginRun(profilePort.Profile);
            var w = Make(config);
            w.V4.LeashReturnDistance = () => config.Single<RunConfig>().enemyLeashReturnDistance; // as EnemyWorldModule wires it
            var dinghy = System.Array.FindIndex(w.Defs, d => d != null && d.id == "deckhand_dinghy");
            Assert.GreaterOrEqual(dinghy, 0);
            var slot = w.V4.Spawn(dinghy, new Vector3(0f, 0f, -60f));
            w.ShipB.transform.position = Vector3.zero;
            w.V4.Tick(Dt, w.ShipB.Position, w.ShipB.Damage);
            var p = w.V4.Agents[slot].position;
            var distance = new Vector2(p.x, p.z).magnitude;
            Assert.AreEqual(31f, distance, 0.5f, "moved back to the configured distance");
            Assert.Greater(p.z, 0f, "ahead of the ship (its heading, +z while it stands still)");

            run.enemyLeashReturnDistance = 35f; // a console edit applies at the next check
            var far = w.V4.Spawn(dinghy, new Vector3(0f, 0f, -70f));
            for (var i = 0; i < 40 && w.V4.Agents[far].position.z < -60f; i++) w.V4.Tick(Dt, w.ShipB.Position, w.ShipB.Damage);
            var q = w.V4.Agents[far].position; // right after the check that moved it
            Assert.AreEqual(35f, new Vector2(q.x, q.z).magnitude, 0.5f, "read from the config at each check");
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
            using var profilePort = new RoguePirates.V4.Ports.StageProfilePort(config); // M15: the profile from the V4 configs (V3's asset is gone)
            StageBalanceProfileContext.BeginRun(profilePort.Profile);
            var w = Make(config);
            var log = new List<string>();
            w.V4.Bind(w.Services, new ShipPort(w.ShipB), new RecordingFeedback(log), new OrderKills(log, w.V4));
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
    
        // ---- V5 M4: weapon slows and knockbacks --------------------------------------------------------------------

        (Worlds w, int slot) OneEnemy(ConfigSetRuntime config, string id, Vector3 at, EliteAffix affix = EliteAffix.None)
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var w = Make(config);
            w.V4.WeaponRules = () => config.Single<WeaponRulesConfig>();
            var def = System.Array.FindIndex(w.Defs, d => d != null && d.id == id);
            Assert.GreaterOrEqual(def, 0, id);
            return (w, w.V4.Spawn(def, at, affix));
        }

        void Step(Worlds w, int frames)
        {
            for (var f = 0; f < frames; f++)
            {
                w.ShipB.transform.position = Vector3.zero;
                IFrameUntil.SetValue(w.ShipB.Damage, 1e9f); // the ship takes no hits while we measure
                w.V4.Tick(Dt, w.ShipB.Position, w.ShipB.Damage);
            }
        }

        [Test]
        public void ASlow_IsCapped_AndTheStrongestHolds()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            var rules = config.Single<WeaponRulesConfig>();
            var (w, slot) = OneEnemy(config, "shark", new Vector3(30f, 0f, 0f));
            w.V4.ApplySlow(slot, 0.9f, 2f);
            Assert.AreEqual(1f - rules.maxSlow, w.V4.SpeedMultiplier(slot), 1e-4f, "an ordinary enemy keeps at least 40% of its speed");
            w.V4.ApplySlow(slot, 0.3f, 10f);
            Assert.AreEqual(1f - rules.maxSlow, w.V4.SpeedMultiplier(slot), 1e-4f, "a weaker slow does not replace a stronger one");
            Step(w, 130); // > 2 s
            Assert.AreEqual(1f, w.V4.SpeedMultiplier(slot), 1e-4f, "it wears off");

            var (b, boss) = OneEnemy(config, "barnacle_king", new Vector3(30f, 0f, 0f));
            b.V4.ApplySlow(boss, 0.6f, 2f);
            Assert.AreEqual(1f - Mathf.Min(rules.bossMaxSlow, 0.6f * rules.bossSlowShare), b.V4.SpeedMultiplier(boss), 1e-4f, "a boss takes half, at most 20%");

            // V5 M8z (7 Oct, D21, Q24): the Reef Crab takes slows like the boss (half, at most bossMaxSlow).
            var (m, crab) = OneEnemy(config, "reef_crab_miniboss", new Vector3(30f, 0f, 0f));
            m.V4.ApplySlow(crab, 0.5f, 2f);
            Assert.AreEqual(1f - Mathf.Min(rules.bossMaxSlow, 0.5f * rules.bossSlowShare), m.V4.SpeedMultiplier(crab), 1e-4f, "a miniboss takes the boss's share of a slow");
            m.V4.ApplySlow(crab, 0.3f, 2f);
            Assert.AreEqual(1f - Mathf.Min(rules.bossMaxSlow, 0.5f * rules.bossSlowShare), m.V4.SpeedMultiplier(crab), 1e-4f, "a weaker slow on the crab does not replace it");
        }

        [Test]
        public void AnEqualSlow_Extends_AndAStrongerOne_Replaces()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            var (w, slot) = OneEnemy(config, "shark", new Vector3(30f, 0f, 0f));
            w.V4.ApplySlow(slot, 0.3f, 1f);
            w.V4.ApplySlow(slot, 0.3f, 3f);
            Step(w, 120); // 2 s
            Assert.AreEqual(0.7f, w.V4.SpeedMultiplier(slot), 1e-4f, "an equal slow extended it to 3 s");
            w.V4.ApplySlow(slot, 0.5f, 0.5f);
            Assert.AreEqual(0.5f, w.V4.SpeedMultiplier(slot), 1e-4f, "a stronger one replaces it");
            Step(w, 40);
            Assert.AreEqual(1f, w.V4.SpeedMultiplier(slot), 1e-4f, "and ends with its own time (one slow slot: the weaker one is gone)");
        }

        [Test]
        public void TheWeaponSink_ReachesTheSimulation_AndReportsTheSlowedVelocity()
        {
            // The real path a weapon uses: IEnemyDamageSink = EnemyWorldContracts → EnemySimulation (review: tests that
            // called the simulation directly would pass if the forwarding fell back to the interface's no-op).
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            var (w, slot) = OneEnemy(config, "shark", new Vector3(30f, 0f, 0f));
            Step(w, 30);
            var contracts = new EnemyWorldContracts(w.V4, w.V4Statuses);
            RoguePirates.V4.Weapons.IEnemyDamageSink sink = contracts;
            RoguePirates.V4.Weapons.IEnemyQuery query = contracts;
            var fullVelocity = query.Velocity(slot);
            Assert.Greater(fullVelocity.magnitude, 0.5f, "it moves");
            sink.ApplySlow(slot, 0.5f, 3f);
            Assert.AreEqual(0.5f, w.V4.SpeedMultiplier(slot), 1e-4f, "the sink's slow reached the simulation");
            Assert.AreEqual(fullVelocity.magnitude * 0.5f, query.Velocity(slot).magnitude, 1e-3f, "aim leads see the slowed velocity");
            Assert.AreEqual(w.V4.Agents[slot].id, query.UnitId(slot), "unit ids for pierce and per-target memory");
            sink.Knockback(slot, Vector3.zero, 2f);
            Assert.Greater(w.V4.Agents[slot].pushTimer, 0f, "the sink's knockback reached the simulation");
        }

        [Test]
        public void ASlowedEnemy_MovesSlower_ByItsShare()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            float Travel(bool slowed)
            {
                var (w, slot) = OneEnemy(config, "shark", new Vector3(40f, 0f, 0f));
                Step(w, 30); // up to speed
                if (slowed) w.V4.ApplySlow(slot, 0.5f, 5f);
                var from = w.V4.Agents[slot].position;
                Step(w, 30);
                var d = w.V4.Agents[slot].position - from;
                d.y = 0f;
                return d.magnitude;
            }
            var normal = Travel(false);
            var slow = Travel(true);
            Assert.Greater(normal, 1f, "it moved");
            Assert.AreEqual(0.5f, slow / normal, 0.08f, "half speed under a 50% slow");
        }

        [Test]
        public void AKnockback_PushesItsDistance_ElitesHalf_BossesNot()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            float Pushed(string id, EliteAffix affix, bool knock)
            {
                var (w, slot) = OneEnemy(config, id, new Vector3(12f, 0f, 0f), affix);
                if (knock) w.V4.Knockback(slot, Vector3.zero, 2f);
                Step(w, 15); // 0.25 s > the 0.15 s push
                return w.V4.Agents[slot].position.x;
            }
            Assert.AreEqual(2f, Pushed("shark", EliteAffix.None, true) - Pushed("shark", EliteAffix.None, false), 0.35f, "2 m away from the ship");
            Assert.AreEqual(1f, Pushed("shark", EliteAffix.Rich, true) - Pushed("shark", EliteAffix.Rich, false), 0.35f, "an elite half");
            Assert.AreEqual(Pushed("barnacle_king", EliteAffix.None, false), Pushed("barnacle_king", EliteAffix.None, true), 1e-3f, "a boss is not moved");
            // V5 M8z (7 Oct, D21, Q24): nor the Reef Crab (a miniboss, by its definition's flag).
            Assert.AreEqual(Pushed("reef_crab_miniboss", EliteAffix.None, false), Pushed("reef_crab_miniboss", EliteAffix.None, true), 1e-3f, "a miniboss is not moved");
        }

        [Test]
        public void AKnockback_IsCappedAt3Meters()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            float Pushed(float meters)
            {
                var (w, slot) = OneEnemy(config, "shark", new Vector3(12f, 0f, 0f));
                if (meters > 0f) w.V4.Knockback(slot, Vector3.zero, meters);
                Step(w, 15);
                return w.V4.Agents[slot].position.x;
            }
            Assert.AreEqual(config.Single<WeaponRulesConfig>().maxKnockbackMeters, Pushed(8f) - Pushed(0f), 0.4f, "an 8 m push (stacked upgrades) is capped at 3 m");
        }

        [Test]
        public void APull_DragsTowardItsPoint_NeverPastIt_BossesNot()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            float Pulled(string id, Vector3 toward, float meters)
            {
                var (w, slot) = OneEnemy(config, id, new Vector3(12f, 0f, 0f));
                if (meters > 0f) w.V4.Pull(slot, toward, meters);
                Step(w, 15);
                return w.V4.Agents[slot].position.x;
            }
            var still = Pulled("shark", Vector3.zero, 0f);
            Assert.AreEqual(2f, Pulled("shark", new Vector3(30f, 0f, 0f), 2f) - still, 0.35f, "2 m toward the whirlpool (away from the ship here)");
            Assert.Less(Pulled("shark", new Vector3(12.5f, 0f, 0f), 3f) - still, 0.6f, "never past the centre");
            Assert.AreEqual(Pulled("barnacle_king", Vector3.zero, 0f), Pulled("barnacle_king", new Vector3(30f, 0f, 0f), 2f), 1e-3f, "a boss is not moved");
            Assert.AreEqual(Pulled("reef_crab_miniboss", Vector3.zero, 0f), Pulled("reef_crab_miniboss", new Vector3(30f, 0f, 0f), 2f), 1e-3f, "nor a miniboss (D21, Q24)");
        }

        /// <summary>V5 M8z (7 Oct, D21, Q32): elite damage (the Admiral Logbook) reaches elites, the boss and minibosses.</summary>
        [Test]
        public void EliteDamage_ReachesElites_TheBoss_AndMinibosses_NotOrdinaryEnemies()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            bool Takes(string id, EliteAffix affix)
            {
                var (w, slot) = OneEnemy(config, id, new Vector3(30f, 0f, 0f), affix);
                RoguePirates.V4.Weapons.IEnemyQuery query = new EnemyWorldContracts(w.V4, w.V4Statuses);
                return query.IsEliteDamageTarget(slot);
            }
            Assert.IsFalse(Takes("shark", EliteAffix.None), "an ordinary enemy");
            Assert.IsTrue(Takes("shark", EliteAffix.Rich), "an elite");
            Assert.IsTrue(Takes("barnacle_king", EliteAffix.None), "the boss");
            Assert.IsTrue(Takes("reef_crab_miniboss", EliteAffix.None), "the Reef Crab");
        }

        [Test]
        public void M8z_AttackWarnings_HaveTheirOwnSlots_SoADamagingHazardIsNeverDropped()
        {
            // V5 M8z (6 Oct, owner): a screen full of warnings used to fill all 48 shared slots and silently drop the next
            // damaging circle or mine
            var hazards = new HazardTelegraphs(null, null);
            for (var i = 0; i < 200; i++) hazards.Warn(new Vector3(i, 0f, 0f), 3f, 5f);
            for (var i = 0; i < 200; i++) hazards.WarnLine(Vector3.zero, Vector3.forward, 10f, 1f, 5f);
            hazards.SpawnCircle(new Vector3(5f, 0f, 5f), 4f, 2f, 30f);
            hazards.SpawnMine(new Vector3(-5f, 0f, 5f), 1f, 10f, 20f);
            hazards.SpawnNetSegment(new Vector3(0f, 0f, -5f), 6f, 3f, 0.5f, 2f);

            int Count(HazardKind kind)
            {
                var n = 0;
                for (var i = 0; i < hazards.Capacity; i++) if (hazards.TryGet(i, out var h) && h.kind == kind) n++;
                return n;
            }
            Assert.AreEqual(HazardTelegraphs.DamagingSlots + HazardTelegraphs.WarningSlots, hazards.Capacity);
            Assert.AreEqual(HazardTelegraphs.WarningSlots, Count(HazardKind.Warning) + Count(HazardKind.WarningLine), "the warnings fill only their own slots");
            Assert.AreEqual(1, Count(HazardKind.Explosion));
            Assert.AreEqual(1, Count(HazardKind.Mine));
            Assert.AreEqual(1, Count(HazardKind.NetSlow));

            // and the other way round: damaging hazards filling their slots leave the warnings alone
            for (var i = 0; i < 100; i++) hazards.SpawnCircle(new Vector3(i, 0f, 0f), 2f, 2f, 10f);
            Assert.AreEqual(HazardTelegraphs.DamagingSlots, Count(HazardKind.Explosion) + Count(HazardKind.Mine) + Count(HazardKind.NetSlow));
            Assert.AreEqual(HazardTelegraphs.WarningSlots, Count(HazardKind.Warning) + Count(HazardKind.WarningLine));

            // a unit's warning follows it in its own slot; clearing it frees a warning slot
            hazards.ClearAll();
            hazards.Warn(Vector3.zero, 3f, 2f, owner: 7);
            hazards.Warn(new Vector3(4f, 0f, 0f), 3f, 2f, owner: 7);
            Assert.AreEqual(1, Count(HazardKind.Warning));
            hazards.ClearWarnings(7);
            Assert.AreEqual(0, Count(HazardKind.Warning));
        }
}
}
