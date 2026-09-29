using System.Collections.Generic;
using System.IO;
using System.Linq;
using HighSeas.Combat;
using HighSeas.Data;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Presentation.Pooling;
using RoguePirates.V4.Presentation.Weapons;
using RoguePirates.V4.Views;
using RoguePirates.V4.Weapons;
using UnityEngine;
using UnityEngine.TestTools;

namespace RoguePirates.V4.Tests
{
    /// <summary>M6 weapon effects presenter and aim overlay: pooling, handles, blades, no random draws, no throws.</summary>
    [Category("V4")]
    public sealed class V4WeaponVfxM6Tests
    {
        // ---- Fakes ----------------------------------------------------------------------------------------------

        sealed class FakeWeapons : IWeaponManager
        {
            public readonly List<WeaponSlot> List = new List<WeaponSlot>();
            public IReadOnlyList<WeaponSlot> Slots => List;
            public PlayerProjectiles Projectiles => null;
            public void FireAllOnce() { }
            public void ArmAllKegs() { }
            public void FireLightningSalvo() { }
            public bool FireSlot(int index) => false;
            public void ClearInFlight() { }
            public IReadOnlyList<OrbitBehavior.HitSphere> OrbitHits => System.Array.Empty<OrbitBehavior.HitSphere>();
        }

        sealed class FakeShipView : IShipView
        {
            public Transform Anchor => null;
            public Vector3 Pos, Forward = Vector3.forward;
            public bool Exists => true;
            public Vector3 Position => Pos;
            public Vector3 Velocity => Vector3.zero;
            public Vector3 MovementForward => Forward;
            public float Hp => 100f;
            public float MaxHp => 100f;
            public float BoostNormalized => 0f;
        }

        sealed class FakeShipState : IShipState
        {
            public Vector3 Pos, Forward = Vector3.forward;
            public bool Exists => true;
            public Vector3 Position => Pos;
            public Vector3 Velocity => Vector3.zero;
            public Vector3 MovementForward => Forward;
            public Vector3 TransformPoint(Vector3 local) => Pos + Quaternion.LookRotation(Forward) * local;
        }

        // ---- Fixture --------------------------------------------------------------------------------------------

        GameObject _root;
        PoolService _pools;
        DemoJuiceSettings _settings;
        WeaponVfxPresenter _presenter;
        readonly List<Object> _owned = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("VfxTestPoolRoot");
            _pools = new PoolService(_root.transform);
            _settings = ScriptableObject.CreateInstance<DemoJuiceSettings>();
            _owned.Add(_settings);
            var host = new GameObject("VfxTestPresenter");
            _owned.Add(host);
            _presenter = host.AddComponent<WeaponVfxPresenter>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_presenter != null) _presenter.Clear();
            _pools.Dispose();
            foreach (var o in _owned) if (o != null) Object.DestroyImmediate(o);
            _owned.Clear();
            Object.DestroyImmediate(_root);
            var hazards = GameObject.Find("V4HazardTelegraphs"); // created by TimedRing (the kept legacy ring view)
            if (hazards != null) Object.DestroyImmediate(hazards);
        }

        /// <summary>A loadout whose weapons use the cannon muzzle (M6: weapon config, was a weapon-id table).</summary>
        FakeWeapons CannonLoadout(params string[] ids)
        {
            var weapons = new FakeWeapons();
            foreach (var id in ids)
            {
                var config = ScriptableObject.CreateInstance<WeaponConfig>();
                config.weaponId = id;
                config.visual.cannonMuzzle = true;
                _owned.Add(config);
                weapons.List.Add(new WeaponSlot { Config = config });
            }
            return weapons;
        }

        GameObject Prefab(string name, params string[] children)
        {
            var go = new GameObject(name);
            foreach (var c in children) new GameObject(c).transform.SetParent(go.transform, false);
            _owned.Add(go);
            return go;
        }

        GameObject ParticlePrefab(string name)
        {
            var go = Prefab(name);
            go.AddComponent<ParticleSystem>();
            return go;
        }

        WeaponConfig OrbitConfig()
        {
            var config = ScriptableObject.CreateInstance<WeaponConfig>();
            config.weaponId = "flying_cutlasses";
            config.behavior = ConfigEnums.WeaponBehavior.Orbit;
            config.pattern = ConfigEnums.WeaponPattern.Orbit;
            _owned.Add(config);
            return config;
        }

        int InUse(string keyPrefix) => _pools.Stats().Where(s => s.Key.StartsWith(keyPrefix)).Sum(s => s.InUse);

        IEnumerable<GameObject> ActiveInstances(string keyPrefix)
        {
            foreach (Transform child in _root.transform)
                if (child.gameObject.activeSelf && child.name.StartsWith(keyPrefix)) yield return child.gameObject;
        }

        static void CallEverything(IWeaponEffects fx)
        {
            var p = new Vector3(2f, 0f, 3f);
            fx.Fired("deck_cannons", p, Vector3.forward);
            fx.Fired(null, p, Vector3.zero);
            fx.Aimed("lightning_mast", Vector3.forward, p, true);
            fx.Beam(p, Vector3.forward, 20f, 0.32f);
            fx.SideSalvo("broadside_battery", p, Vector3.right);
            var keg = fx.MineDropped(p);
            fx.MineRemoved(keg);
            fx.MineRemoved(-1);
            fx.Explosion(p, 3.2f);
            fx.LightningStrike(p, 3.5f);
            fx.FlameBurst(p, Vector3.forward);
            fx.AreaTelegraph(p, 4f);
            fx.TimedRing(p, 4f, 0.6f);
            fx.AreaImpact(p, 4f);
            fx.BurningPatch(p, 2.8f);
            fx.WaterSplash(p);
            var skiff = fx.SkiffSpawned(p, Vector3.forward);
            fx.SkiffMoved(skiff, p + Vector3.right, Vector3.right);
            fx.SkiffRemoved(skiff);
            fx.SkiffRemoved(12345);
        }

        // ---- Tests ----------------------------------------------------------------------------------------------

        [Test]
        public void MineAndSkiffHandles_AreUnique_AndRemovalReleasesViewsToThePool()
        {
            _settings.mineBarrelPrefab = Prefab("TestKeg");
            _settings.mineBarrelPrefab.AddComponent<BoxCollider>();
            var skiffModel = Prefab("TestSkiff");
            skiffModel.AddComponent<BoxCollider>();
            _presenter.Bind(_pools, null, null, _settings, skiffModel);

            var handles = new List<int>
            {
                _presenter.MineDropped(Vector3.zero), _presenter.MineDropped(Vector3.one), _presenter.MineDropped(Vector3.right),
                _presenter.SkiffSpawned(Vector3.zero, Vector3.forward), _presenter.SkiffSpawned(Vector3.left, Vector3.back),
            };
            Assert.IsTrue(handles.All(h => h >= 0));
            Assert.AreEqual(handles.Count, handles.Distinct().Count(), "handles are unique across kegs and skiffs");
            Assert.AreEqual(3, InUse(WeaponVfxPresenter.KeyPrefix + "keg/"));
            Assert.AreEqual(2, InUse(WeaponVfxPresenter.KeyPrefix + "skiff/"));
            Assert.IsTrue(ActiveInstances(WeaponVfxPresenter.KeyPrefix + "keg/").All(g => g.GetComponentsInChildren<Collider>(true).Length == 0), "keg physics stripped (V3)");
            Assert.IsTrue(ActiveInstances(WeaponVfxPresenter.KeyPrefix + "skiff/").All(g => g.GetComponentsInChildren<Collider>(true).Length == 0), "skiff colliders removed (V3)");

            var skiff = ActiveInstances(WeaponVfxPresenter.KeyPrefix + "skiff/").First();
            Assert.AreEqual(Vector3.one * 0.42f, skiff.transform.localScale);

            _presenter.SkiffMoved(handles[3], new Vector3(4f, 0.2f, 1f), Vector3.right);
            Assert.IsTrue(ActiveInstances(WeaponVfxPresenter.KeyPrefix + "skiff/").Any(g => g.transform.position == new Vector3(4f, 0.2f, 1f)));

            for (var i = 0; i < 3; i++) _presenter.MineRemoved(handles[i]);
            _presenter.SkiffRemoved(handles[3]);
            _presenter.SkiffRemoved(handles[4]);
            _presenter.MineRemoved(handles[0]); // twice: ignored
            Assert.AreEqual(0, InUse(WeaponVfxPresenter.KeyPrefix));
            Assert.AreEqual(0, _presenter.LiveHandleCount);
            Assert.AreEqual(0, _pools.TotalCreatedAfterPrewarm);

            var again = _presenter.MineDropped(Vector3.zero);
            Assert.IsFalse(handles.Contains(again), "handles are not reused");
        }

        [Test]
        public void RepeatedEffects_ComeFromThePool_WithoutNewObjectsAfterPrewarm()
        {
            var gun = Prefab("TestGun", "Barrel", "CannonSmoke");
            gun.transform.Find("Barrel").gameObject.AddComponent<MeshRenderer>();
            gun.transform.Find("CannonSmoke").gameObject.AddComponent<ParticleSystem>();
            _settings.cannonMuzzleVfxPrefab = gun;
            _settings.shootingVfxPrefab = ParticlePrefab("TestShot");
            _settings.powderKegExplosionPrefab = Prefab("TestKegBlast", "Impact small", "Ring", "Smoke", "Debris");
            _settings.lightningStrikePrefab = ParticlePrefab("TestStrike");
            _settings.mortarExplosionPrefab = ParticlePrefab("TestMortarBurst");
            _settings.burningTrailPrefab = ParticlePrefab("TestOil");
            var beam = Prefab("TestBeam");
            beam.AddComponent<LineRenderer>();
            _settings.laserBeamPrefab = beam;
            _presenter.Bind(_pools, CannonLoadout("deck_cannons", "broadside_battery"), null, _settings);

            for (var round = 0; round < 2; round++)
            {
                var p = new Vector3(round, 0f, 1f);
                _presenter.Fired("deck_cannons", p, Vector3.forward);
                _presenter.Fired("harpoon_gun", p, Vector3.forward);
                _presenter.SideSalvo("broadside_battery", p, Vector3.left);
                _presenter.Explosion(p, 3.2f);
                _presenter.LightningStrike(p, 3.5f);
                _presenter.AreaImpact(p, 4f);
                _presenter.BurningPatch(p, 2.8f);
                _presenter.Beam(p, Vector3.forward, 20f, 0.32f);
                Assert.AreEqual(2, InUse(WeaponVfxPresenter.KeyPrefix + "muzzle/"), "muzzle and salvo use the gun's smoke");
                Assert.AreEqual(4, InUse(WeaponVfxPresenter.KeyPrefix + "burst/TestMortarBurst"), "4 mortar bursts (V3 fallback spread)");
                Assert.AreEqual(1, InUse(WeaponVfxPresenter.KeyPrefix + "blast/"));
                Assert.AreEqual(1, InUse(WeaponVfxPresenter.KeyPrefix + "lightning/"));
                _presenter.Tick(5f);
                Assert.AreEqual(0, InUse(WeaponVfxPresenter.KeyPrefix), "timed effects return to their pools");
                Assert.AreEqual(0, _presenter.LiveTimedCount);
            }
            Assert.AreEqual(0, _pools.TotalCreatedAfterPrewarm, "no objects created after prewarm");
        }

        [Test]
        public void TimedEffects_EndAfterTheirV3Lifetimes()
        {
            _settings.lightningStrikePrefab = ParticlePrefab("TestStrike");
            _presenter.Bind(_pools, null, null, _settings);
            _presenter.LightningStrike(Vector3.zero, 3.5f);
            _presenter.Tick(1.1f);
            Assert.AreEqual(1, InUse(WeaponVfxPresenter.KeyPrefix + "lightning/"), "still showing at 1.1 s");
            _presenter.Tick(0.1f);
            Assert.AreEqual(0, InUse(WeaponVfxPresenter.KeyPrefix + "lightning/"), "gone after 1.15 s");
        }

        [Test]
        public void MuzzleGun_ShowsOnlyItsCannonSmoke()
        {
            var gun = Prefab("TestGun", "Barrel", "CannonSmoke");
            gun.transform.Find("Barrel").gameObject.AddComponent<MeshRenderer>();
            gun.transform.Find("CannonSmoke").gameObject.AddComponent<ParticleSystem>();
            _settings.cannonMuzzleVfxPrefab = gun;
            _presenter.Bind(_pools, CannonLoadout("long_nine"), null, _settings);

            _presenter.Fired("long_nine", Vector3.zero, Vector3.forward);
            var live = ActiveInstances(WeaponVfxPresenter.KeyPrefix + "muzzle/").Single();
            Assert.IsFalse(live.transform.Find("Barrel").GetComponent<MeshRenderer>().enabled, "the gun model is hidden");
            Assert.IsTrue(live.transform.Find("CannonSmoke").gameObject.activeSelf);
            Assert.AreEqual(Vector3.one * _settings.shootingVfxScale, live.transform.localScale);
        }

        [Test]
        public void EveryEffect_WithNothingBoundOrNoPrefabs_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => CallEverything(_presenter), "unbound");
            _presenter.Bind(null, null, null, null);
            Assert.DoesNotThrow(() => CallEverything(_presenter), "no pools, no settings");
            _presenter.Bind(_pools, new FakeWeapons(), new FakeShipView(), _settings);
            Assert.DoesNotThrow(() => CallEverything(_presenter), "settings without prefabs");
            Assert.DoesNotThrow(() =>
            {
                _presenter.Tick(10f);
                _presenter.SyncBlades();
                _presenter.Clear();
            });
            Assert.AreEqual(0, _presenter.LiveHandleCount);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Presenter_DrawsNoRandomNumbers_AndReadsNoLegacyHub()
        {
            _settings.mineBarrelPrefab = Prefab("TestKeg");
            _settings.shootingVfxPrefab = Prefab("TestShot");
            _settings.lightningStrikePrefab = Prefab("TestStrike", "Visuals");
            _settings.weaponExplosionPrefab = Prefab("TestBlast", "Ring");
            _settings.burningTrailPrefab = Prefab("TestOil");
            var state = UnityEngine.Random.state;
            _presenter.Bind(_pools, null, null, _settings, Prefab("TestSkiff"));
            CallEverything(_presenter);
            _presenter.Tick(10f);
            Assert.AreEqual(state, UnityEngine.Random.state, "no draws from Unity's random state");

            // Source check: no run random stream and no legacy hubs in the presenter code (comments excluded).
            string[] forbidden = { "RunRng", "CombatFloat", "ICombatRandom", "Random.", "RunManager", "FindFirstObjectByType", "FindObjectOfType",
                "WeaponController", "EnemyWorld", "PlayerShipController", "LevelSystem" };
            foreach (var file in new[] { "WeaponVfxPresenter.cs", "WeaponAimOverlay.cs" })
            {
                var path = Path.Combine(Application.dataPath, "_Game", "V4", "Presentation", "Weapons", file);
                var code = string.Join("\n", File.ReadAllLines(path).Select(l => { var i = l.IndexOf("//"); return i >= 0 ? l.Substring(0, i) : l; }));
                foreach (var token in forbidden) StringAssert.DoesNotContain(token, code, $"{file} uses {token}");
            }
        }

        [Test]
        public void Blades_AreDrawnAtTheSimulationsHitSpheres_AndReturnToThePool()
        {
            _settings.cutlassPrefab = ParticlePrefab("TestCutlass");
            var weapons = new FakeWeapons();
            weapons.List.Add(new WeaponSlot { Config = OrbitConfig() });
            var hits = new List<OrbitBehavior.HitSphere>
            {
                new OrbitBehavior.HitSphere { Position = new Vector3(5f, 1.75f, 0f), Radius = 2.75f },
                new OrbitBehavior.HitSphere { Position = new Vector3(-5f, 1.75f, 0f), Radius = 2.75f },
                new OrbitBehavior.HitSphere { Position = new Vector3(0f, 1.75f, 5f), Radius = 2.75f },
            };
            var ship = new FakeShipView { Forward = Vector3.right };
            _presenter.Bind(_pools, weapons, ship, _settings, bladeHits: () => hits);

            _presenter.SyncBlades();
            Assert.AreEqual(3, _presenter.BladeCount);
            for (var i = 0; i < 3; i++) Assert.Less(Vector3.Distance(hits[i].Position, _presenter.BladePosition(i)), 1e-4f);
            var blade = ActiveInstances(WeaponVfxPresenter.KeyPrefix + "cutlass/").First();
            Assert.Less(Quaternion.Angle(Quaternion.LookRotation(Vector3.right) * Quaternion.Euler(90f, 0f, 0f), blade.transform.rotation), 0.01f,
                "blades lie flat along the ship's heading (V3)");
            Assert.AreEqual(Vector3.one * _settings.cutlassScale, blade.transform.localScale);

            hits.RemoveAt(2);
            _presenter.SyncBlades();
            Assert.AreEqual(2, _presenter.BladeCount);
            Assert.AreEqual(2, InUse(WeaponVfxPresenter.KeyPrefix + "cutlass/"));

            hits.Clear(); // paused: the blades stay where they are
            _presenter.SyncBlades();
            Assert.AreEqual(2, _presenter.BladeCount);

            weapons.List.Clear(); // weapon gone
            _presenter.SyncBlades();
            Assert.AreEqual(0, _presenter.BladeCount);
            Assert.AreEqual(0, InUse(WeaponVfxPresenter.KeyPrefix + "cutlass/"));
            Assert.AreEqual(0, _pools.TotalCreatedAfterPrewarm);
        }

        [Test]
        public void FallbackBladeFormula_MatchesTheSimulationsHitSpheres()
        {
            var config = OrbitConfig();
            config.orbit.orbitRadius = 6.5f;
            config.orbit.angularSpeedDegrees = 200f;
            config.orbit.heightOffset = 0.4f;
            var slot = new WeaponSlot { Config = config, QuantityBonus = 1, SizeMult = 1.2f };
            var ship = new FakeShipState { Pos = new Vector3(3f, 0f, -2f), Forward = new Vector3(1f, 0f, 1f).normalized };
            var orbit = new OrbitBehavior(new WeaponCombat { Ship = ship });
            orbit.ComputeHits(new List<WeaponSlot> { slot }, 12.34f);

            var drawn = new List<Vector3>();
            WeaponVfxPresenter.ComputeBladePositions(slot, 12.34f, ship.Pos, Quaternion.LookRotation(ship.Forward), drawn);
            Assert.AreEqual(orbit.Hits.Count, drawn.Count);
            Assert.AreEqual(3, drawn.Count);
            for (var i = 0; i < drawn.Count; i++)
                Assert.Less(Vector3.Distance(orbit.Hits[i].Position, drawn[i]), 1e-4f);
        }

        [Test]
        public void AimOverlay_ShowsSlotShapes_OnlyWhileTheDebugOverlaysAreOn()
        {
            var host = new GameObject("VfxTestAimOverlay");
            _owned.Add(host);
            var overlay = host.AddComponent<WeaponAimOverlay>();
            var weapons = new FakeWeapons();
            weapons.List.Add(new WeaponSlot { Config = OrbitConfig() });
            overlay.Bind(weapons, new FakeShipView(), null, null, null);
            var wasVisible = CombatDebugOverlays.Visible;
            var savedPref = CombatExperimentContext.ShowAimVisuals; // SetVisible writes this player pref
            try
            {
                CombatDebugOverlays.SetVisible(false);
                overlay.Draw(0.016f);
                Assert.IsFalse(overlay.IsSlotAreaVisible(0));

                CombatDebugOverlays.SetVisible(true);
                overlay.NotifyAimed("flying_cutlasses");
                overlay.Draw(0.016f);
                Assert.IsTrue(overlay.IsSlotAreaVisible(0), "the orbit ring shows");
                Assert.IsFalse(overlay.IsSlotMarkerVisible(0), "no target disc for orbit weapons (V3)");
                Assert.IsFalse(overlay.IsSlotAreaVisible(1), "empty slots are hidden");

                CombatDebugOverlays.SetVisible(false);
                overlay.Draw(0.016f);
                Assert.IsFalse(overlay.IsSlotAreaVisible(0));
            }
            finally
            {
                CombatDebugOverlays.SetVisible(wasVisible);
                CombatExperimentContext.ShowAimVisuals = savedPref;
            }
        }
    }
}
