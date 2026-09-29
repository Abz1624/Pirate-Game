using System.Collections.Generic;
using System.Reflection;
using HighSeas.Core;
using HighSeas.Data;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Presentation.Effects;
using RoguePirates.V4.Presentation.Pooling;
using RoguePirates.V4.Presentation.Weapons;
using RoguePirates.V4.Presentation.World;
using RoguePirates.V4.Views;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// M14.9b: the world views V3 drew with its own pools and components are V4 presenters on the pool service (pickups,
    /// enemy shots, floor powerups, the ship health bar, hazard/timed rings, the storm ring, the edge walls, splashes and
    /// blasts), and the simulation reaches them only through forwarders that draw nothing without a presenter.
    /// </summary>
    [Category("V4")]
    public sealed class V4WorldViewsM14Tests
    {
        readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        GameObject Host(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        PoolService Pools() => new PoolService(Host("pools").transform);

        /// <summary>Edit mode logs an error for renderer.material (it copies the material); play mode does not.</summary>
        static void IgnoreEditModeMaterialWarning() => UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

        static DemoJuiceSettings Juice() => Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");

        static void Call(Object target, string method) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, null);

        sealed class FakeShip : IShipView
        {
            public Transform Model;
            public bool Exists => true;
            public Vector3 Position { get; set; }
            public Vector3 Velocity { get; set; }
            public Vector3 MovementForward => Vector3.forward;
            public float Hp { get; set; } = 50f;
            public float MaxHp { get; set; } = 100f;
            public float BoostNormalized => 0f;
            public Transform Anchor => Model;
        }

        sealed class FakeWorld : IWorldView
        {
            public bool Visible;
            public Vector3 BoundsCenter => Vector3.zero;
            public float BoundsHalfX => 100f;
            public float BoundsHalfZ => 80f;
            public bool StormRingVisible => Visible;
            public void StormRingCorners(out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d)
            {
                a = new Vector3(-40f, 0.2f, -30f);
                b = new Vector3(40f, 0.2f, -30f);
                c = new Vector3(40f, 0.2f, 30f);
                d = new Vector3(-40f, 0.2f, 30f);
            }
        }

        [Test]
        public void Pickups_ComeFromPrewarmedPools_AndAreReused()
        {
            var pools = Pools();
            var presenter = new PickupPresenter(pools, Juice());
            try
            {
                var before = pools.TotalCreatedAfterPrewarm;
                var views = new List<object>();
                for (var round = 0; round < 3; round++)
                {
                    foreach (var type in new[] { PickupType.Infamy, PickupType.FlyingXp, PickupType.Gold, PickupType.FlyingGold,
                                 PickupType.GoldBarrel, PickupType.XpBarrel, PickupType.Powerup, PickupType.FlyingHealth })
                        views.Add(presenter.Acquire(type, new Vector3(3f, 0f, 4f), 1));
                    Assert.IsTrue(views.TrueForAll(v => v is Transform), "every pickup type has a model");
                    for (var i = 0; i < views.Count; i++) presenter.Release(views[i], PickupType.Gold, 1);
                    views.Clear();
                }
                Assert.AreEqual(before, pools.TotalCreatedAfterPrewarm, "churn within the prewarm creates nothing");
                Assert.AreEqual(0, presenter.LiveCount);

                var gem = (Transform)presenter.Acquire(PickupType.FlyingXp, Vector3.zero, 1);
                Assert.IsNotNull(gem.Find("CollectSparkle"), "XP gems carry the coin's collect sparkle (V3 EnsureFlyingLootSparkle)");
                Assert.AreEqual(Juice().xpFlyingGemScale, gem.localScale.x, 1e-4f, "the gem's uniform crystal scale");
                Assert.IsNull(gem.GetComponentInChildren<Collider>(true), "pickup models carry no colliders");
                var barrel = (Transform)presenter.Acquire(PickupType.GoldBarrel, Vector3.zero, 1);
                Assert.AreEqual(Juice().goldBarrelPickupScale * Juice().barrelVerticalScale, barrel.localScale.y, 1e-4f, "barrels squashed as V3");
            }
            finally { presenter.Dispose(); pools.Dispose(); }
        }

        [Test]
        public void HostileShots_ArePooledTintedViews_AndSplashThroughTheSink()
        {
            IgnoreEditModeMaterialWarning(); // the shot tint uses renderer.material, as V3 did
            var pools = Pools();
            var splashes = new List<Vector3>();
            var presenter = new HostileShotPresenter(pools, Juice(), splashes.Add);
            try
            {
                var before = pools.TotalCreatedAfterPrewarm;
                var shots = new List<Transform>();
                for (var i = 0; i < 20; i++) shots.Add(presenter.Acquire(new Vector3(i, 1f, 0f), new Color(0.42f, 0.22f, 0.1f), 0.3f));
                Assert.AreEqual(20, presenter.LiveCount);
                Assert.IsTrue(shots.TrueForAll(s => s != null && s.gameObject.activeSelf));
                foreach (var s in shots) presenter.Release(s);
                Assert.AreEqual(0, presenter.LiveCount);
                var again = presenter.Acquire(Vector3.zero, Color.red, 0.3f);
                Assert.IsTrue(shots.Contains(again), "a released model is reused");
                Assert.AreEqual(before, pools.TotalCreatedAfterPrewarm, "20 shots fit the prewarm");
                presenter.Splash(new Vector3(1f, 0f, 2f));
                Assert.AreEqual(1, splashes.Count, "the impact splash goes to the shared splash");
            }
            finally { presenter.Dispose(); pools.Dispose(); }
        }

        [Test]
        public void Powerups_ArePooledPlaceholders_PerKind()
        {
            IgnoreEditModeMaterialWarning(); // V3's placeholder factory tints with renderer.material
            var pools = Pools();
            var presenter = new PowerupPresenter(pools);
            try
            {
                var before = pools.TotalCreatedAfterPrewarm;
                var a = (Transform)presenter.Create(PowerupKind.RepairSupplies, new Vector3(5f, 0f, 5f));
                var b = (Transform)presenter.Create(PowerupKind.Tailwind, new Vector3(-5f, 0f, 5f));
                Assert.IsNotNull(a);
                Assert.IsNotNull(b);
                Assert.AreNotSame(a, b);
                presenter.Move(a, new Vector3(6f, 0f, 6f));
                Assert.AreEqual(new Vector3(6f, 0f, 6f), a.position);
                presenter.Destroy(a);
                Assert.IsFalse(a.gameObject.activeSelf, "a collected powerup goes back to its pool");
                Assert.AreSame(a, presenter.Create(PowerupKind.RepairSupplies, Vector3.zero), "and is reused");
                Assert.AreEqual(before, pools.TotalCreatedAfterPrewarm);
            }
            finally { presenter.Dispose(); pools.Dispose(); }
        }

        [Test]
        public void WaterSplash_MergesNearbySplashes_AndBlastsPlayAtSeaLevel()
        {
            var pools = Pools();
            var presenter = Host("fx").AddComponent<WeaponVfxPresenter>();
            presenter.Bind(pools, null, null, Juice());
            presenter.WaterSplash(new Vector3(0f, 2f, 0f));
            presenter.WaterSplash(new Vector3(0.5f, 2f, 0.5f)); // same instant, 0.7 m away: merged (V3 1.35 m / 0.14 s)
            Assert.AreEqual(1, presenter.SplashesPlayed);
            presenter.WaterSplash(new Vector3(5f, 0f, 0f)); // far enough: its own splash
            Assert.AreEqual(2, presenter.SplashesPlayed);
            var timed = presenter.LiveTimedCount;
            presenter.PlayExplosion(new Vector3(1f, 3f, 1f), 3f);
            Assert.Greater(presenter.LiveTimedCount, timed, "the enemy blast plays the keg blast");
            presenter.Clear();
            pools.Dispose();
        }

        [Test]
        public void TimedAndHazardRings_AreDrawnFromTheViews()
        {
            var rings = Host("rings").AddComponent<HazardRingPresenter>();
            var paused = false;
            rings.Bind(null, null, () => paused);
            var weapon = Host("fx").AddComponent<WeaponVfxPresenter>();
            weapon.HazardRings = rings;
            weapon.TimedRing(new Vector3(2f, 0f, 3f), 4f, 0.6f);
            Assert.AreEqual(1, rings.TimedRingCount, "the weapon presenter's timed ring reaches the ring presenter");
            Call(rings, "LateUpdate");
            var outline = rings.transform.Find("TimedOutline0");
            Assert.IsNotNull(outline, "timed rings are prewarmed at bind");
            Assert.IsTrue(outline.gameObject.activeSelf);
            Assert.AreEqual(6f, outline.GetComponent<LineRenderer>().GetPosition(0).x, 1e-3f, "the ring is the blast radius around the landing");
        }

        [Test]
        public void ShipWake_OnWhileTheShipMoves()
        {
            var model = Host("ship-model").transform;
            var wake = new GameObject("Boat Wake (Trail based)");
            wake.transform.SetParent(model, false);
            wake.SetActive(false); // as in the player ship prefab
            var foam = new GameObject("Engine Foam");
            foam.transform.SetParent(wake.transform, false);
            var trail = foam.AddComponent<TrailRenderer>();
            var other = new GameObject("Smoke");
            other.transform.SetParent(model, false);
            var otherTrail = other.AddComponent<TrailRenderer>();
            var ship = new FakeShip { Model = model };
            var presenter = Host("wake").AddComponent<ShipWakePresenter>();
            presenter.Bind(ship);

            presenter.Tick();
            Assert.IsFalse(wake.activeSelf, "no wake at rest");
            ship.Velocity = new Vector3(0f, 0f, 6f);
            presenter.Tick();
            Assert.IsTrue(wake.activeSelf, "the wake switches on while the ship moves");
            Assert.IsTrue(trail.emitting, "and its foam trail emits");
            ship.Velocity = Vector3.zero;
            presenter.Tick();
            Assert.IsFalse(trail.emitting, "stopped: the trail stops emitting");
            Assert.IsTrue(otherTrail.emitting, "a trail that is not a wake is left alone");
        }

        [Test]
        public void ShipHealthBar_FollowsTheShipView()
        {
            var pools = Pools();
            var model = Host("ship-model").transform;
            var ship = new FakeShip { Model = model };
            var bar = Host("bar").AddComponent<ShipHealthBarPresenter>();
            bar.Bind(pools, ship, Juice());
            Call(bar, "LateUpdate");
            Assert.IsTrue(bar.HasBar, "the bar comes from the pool");
            Assert.AreEqual(0.5f, bar.Fill, 1e-4f);
            ship.Hp = 25f;
            Call(bar, "LateUpdate");
            Assert.AreEqual(0.25f, bar.Fill, 1e-4f, "hull / max hull, every frame");
            Object.DestroyImmediate(bar);
            pools.Dispose();
        }

        [Test]
        public void StormRing_And_EdgeWalls_ReadTheWorldView()
        {
            var world = new FakeWorld();
            var ring = Host("ring").AddComponent<StormRingPresenter>();
            ring.Bind(world);
            Call(ring, "LateUpdate");
            Assert.IsFalse(ring.RingShown, "no ring before the storm");
            world.Visible = true;
            Call(ring, "LateUpdate");
            Assert.IsTrue(ring.RingShown);
            var line = ring.GetComponentInChildren<LineRenderer>();
            Assert.AreEqual(new Vector3(40f, 0.2f, 30f), line.GetPosition(2), "the ring is the storm's safe rectangle");

            var ship = new FakeShip { Position = new Vector3(0f, 0f, 0f) };
            var walls = Host("walls").AddComponent<BoundsWallPresenter>();
            walls.Bind(world, ship);
            Call(walls, "Update");
            Assert.IsFalse(walls.WallShown(0) || walls.WallShown(1) || walls.WallShown(2) || walls.WallShown(3), "far from every edge");
            ship.Position = new Vector3(-90f, 0f, 0f); // 10 m from the west edge
            Call(walls, "Update");
            Assert.IsTrue(walls.WallShown(0), "the west wall fades in near the west edge");
            Assert.IsFalse(walls.WallShown(1), "the east wall stays hidden");
        }

        /// <summary>
        /// 14.10 (intended change 2): enemy models come from the view pool with their colliders removed at once (V3 left
        /// them until the end of the frame, R-81) and nothing toggles a shared root (R-72); bars are pooled too.
        /// </summary>
        [Test]
        public void EnemyViews_ArePooled_WithoutColliders_AndBarsFollow()
        {
            IgnoreEditModeMaterialWarning(); // the placeholder cube's tint uses renderer.material, as V3 did
            var pools = Pools();
            var presenter = new EnemyViewPresenter(pools, Juice());
            var database = HighSeas.Core.GameDatabaseLoader.Load();
            try
            {
                HighSeas.Data.EnemyDefinition modelled = null;
                foreach (var d in database.enemies) if (d != null && d.visualPrefab != null) { modelled = d; break; }
                Assert.IsNotNull(modelled, "an enemy with a model");
                var view = presenter.CreateView(modelled, new Vector3(4f, 0f, 2f), 0.88f, false, 7);
                Assert.IsNotNull(view);
                Assert.IsNull(view.GetComponentInChildren<Collider>(true), "no island collider on a new model, not even for a frame");
                Assert.IsNull(view.GetComponentInChildren<Rigidbody>(true));
                Assert.AreEqual(new Vector3(4f, 0f, 2f), view.position);
                var bar = presenter.AttachHealthBar(modelled, view, false);
                Assert.IsNotNull(bar, "the health bar comes from its pool");
                presenter.SetHealthFill(bar, 0.4f);
                Assert.AreEqual(1, presenter.LiveCount);
                presenter.Release(modelled, view, bar);
                Assert.AreEqual(0, presenter.LiveCount);
                Assert.IsFalse(view.gameObject.activeSelf, "a released model goes back to its pool");
                var again = presenter.CreateView(modelled, Vector3.zero, 0.88f, true, 8);
                Assert.AreSame(view, again, "and is reused");

                var placeholder = ScriptableObject.CreateInstance<HighSeas.Data.EnemyDefinition>();
                _created.Add(placeholder);
                placeholder.id = "test_placeholder";
                placeholder.radius = 1.5f;
                placeholder.placeholderColor = Color.cyan;
                var cube = presenter.CreateView(placeholder, Vector3.zero, 1f, true, 1);
                Assert.IsNotNull(cube, "no model: V3's placeholder cube");
                Assert.AreEqual(3f, cube.localScale.x, 1e-4f, "the enemy's size");
                Assert.IsNull(cube.GetComponent<Collider>());

                var forwarder = new EnemyViewForwarder(null);
                Assert.IsNull(forwarder.CreateView(modelled, Vector3.zero, 1f, false, 0), "no presenter: no model");
                Assert.AreEqual(presenter.FallbackVisualScale(placeholder), forwarder.FallbackVisualScale(placeholder),
                    "the fallback scale (it sets the radius) is the same without a presenter");
            }
            finally { presenter.Dispose(); pools.Dispose(); }
        }

        /// <summary>14.11: the level-up card texts from the V4 configs equal what V3's card builders wrote.</summary>
        [Test]
        public void OfferCards_EqualV3sCardTexts()
        {
            using var set = new ConfigSetRuntime(UnityEditor.AssetDatabase.LoadAssetAtPath<ConfigSet>("Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset"));
            using var runtime = new RoguePirates.V4.Legacy.V4RuntimeDatabase(set);
            var db = runtime.Build(RoguePirates.V4.Editor.V3ConfigMigrator.LoadCleanDatabase());
            var checkedWeapons = 0;
            foreach (var w in set.All<WeaponConfig>())
            {
                var def = db.GetWeapon(w.weaponId);
                if (def == null) continue;
                var v3 = HighSeas.UI.LevelOfferGenerator.BuildNewWeaponOffer(def);
                var v4 = RoguePirates.V4.Presentation.Hud.OfferCards.ToCard(new RoguePirates.V4.Progression.Offer
                    { Kind = RoguePirates.V4.Progression.OfferKind.NewWeapon, Id = w.weaponId, IsWeapon = true, IsNew = true, NextLevel = 1 }, set);
                Assert.AreEqual(v3.title, v4.title, w.weaponId);
                // M14.11 text difference: the stat line shows the weapon config's range, the one the weapon uses (V3's card
                // showed the definition's range, which the flamethrower's stream ignores: 14 on the card, 9.5 in play).
                Assert.AreEqual(v3.description.Replace($"range {def.range:0.#}", $"range {w.range:0.#}"), v4.description, w.weaponId);
                Assert.AreEqual(v3.behaviorLine, v4.behaviorLine, w.weaponId);
                Assert.AreEqual(HighSeas.V3.V3ContentDescriptions.GetWeaponDescription(def),
                    RoguePirates.V4.Presentation.Hud.OfferCards.ToCard(new RoguePirates.V4.Progression.Offer
                        { Kind = RoguePirates.V4.Progression.OfferKind.WeaponUpgrade, Id = w.weaponId, IsWeapon = true }, set).description.Split('\n')[0], w.weaponId);
                checkedWeapons++;
            }
            Assert.GreaterOrEqual(checkedWeapons, 6);
            foreach (var (kind, v3) in new[]
                     {
                         (RoguePirates.V4.Progression.OfferKind.FallbackHeal, HighSeas.UI.LevelOfferGenerator.BuildFallbackHealOffer()),
                         (RoguePirates.V4.Progression.OfferKind.FallbackDamage, HighSeas.UI.LevelOfferGenerator.BuildFallbackDamageOffer()),
                         (RoguePirates.V4.Progression.OfferKind.FallbackMoveSpeed, HighSeas.UI.LevelOfferGenerator.BuildFallbackMoveSpeedOffer()),
                         (RoguePirates.V4.Progression.OfferKind.Continue, HighSeas.UI.LevelOfferGenerator.BuildContinueOffer()),
                     })
            {
                var v4 = RoguePirates.V4.Presentation.Hud.OfferCards.ToCard(new RoguePirates.V4.Progression.Offer { Kind = kind, Id = v3.id }, set);
                Assert.AreEqual(v3.title, v4.title, kind.ToString());
                Assert.AreEqual(v3.description, v4.description, kind.ToString());
                Assert.AreEqual(v3.categoryLabel, v4.categoryLabel, kind.ToString());
            }
        }

        sealed class FakeMarkers : RoguePirates.V4.Stage.IPoiMarkerView
        {
            public HighSeas.Core.POIType[] Types = { HighSeas.Core.POIType.SalvageChest, HighSeas.Core.POIType.MerchantSkiff, HighSeas.Core.POIType.TrialBuoy };
            public bool[] Spawned = { true, true, true }, Hidden = new bool[3], Used = new bool[3];
            public HighSeas.Core.POIType[] Visual = { HighSeas.Core.POIType.SalvageChest, HighSeas.Core.POIType.MerchantSkiff, HighSeas.Core.POIType.TrialBuoy };
            public bool Charge;
            public int LayoutVersion => 1;
            public int PoiCount => 3;
            public bool UsePlaceholderVisuals => true;
            public HighSeas.Core.POIType PoiType(int i) => Types[i];
            public Vector3 PoiPosition(int i) => new Vector3(i * 10f, 0f, 5f);
            public bool IsMarkerSpawned(int i) => Spawned[i];
            public HighSeas.Core.POIType VisualType(int i) => Visual[i];
            public bool IsHidden(int i) => Hidden[i];
            public bool IsConsumed(int i) => Used[i];
            public bool ChargeRingShown(int i) => Charge && i == 2;
            public float SignalBuoyCharge01(int i) => 0.5f;
            public float ChargeRingRadius => 4f;
            public bool TelegraphRingShown(int i) => false;
        }

        /// <summary>14.12: markers are drawn from the stage module's marker state, all made at bind.</summary>
        [Test]
        public void PoiMarkers_FollowTheMarkerView()
        {
            IgnoreEditModeMaterialWarning(); // placeholder and ring tints use renderer.material, as V3 did
            var view = new FakeMarkers();
            var presenter = Host("markers").AddComponent<RoguePirates.V4.Presentation.Stage.PoiMarkerPresenter>();
            presenter.Bind(view);
            Call(presenter, "LateUpdate");
            Assert.AreEqual(3, presenter.MarkerCount);
            Assert.IsNotNull(presenter.Shown(0), "a spawned chest shows");
            var objects = presenter.GetComponentsInChildren<Transform>(true).Length;

            view.Used[0] = true;
            Call(presenter, "LateUpdate");
            Assert.IsNull(presenter.Shown(0), "a used chest is retired");

            var cache = presenter.Shown(1);
            Assert.IsNotNull(cache);
            view.Visual[1] = HighSeas.Core.POIType.SalvageChest; // the Powder Cache's Rare Treasure Chest
            Call(presenter, "LateUpdate");
            Assert.IsNotNull(presenter.Shown(1));
            Assert.AreNotSame(cache, presenter.Shown(1), "the cache shows its Rare Treasure Chest look");
            Assert.IsFalse(cache.activeSelf);
            view.Hidden[1] = true; // the elites spring: the cache hides its children
            view.Visual[1] = HighSeas.Core.POIType.MerchantSkiff; // "Clear all" ends the challenge: the cache comes back
            Call(presenter, "LateUpdate");
            view.Hidden[1] = false;
            Call(presenter, "LateUpdate");
            Assert.AreSame(cache, presenter.Shown(1), "the cache's own marker again");
            for (var c = 0; c < cache.transform.childCount; c++)
                Assert.IsTrue(cache.transform.GetChild(c).gameObject.activeSelf, "its children shown again");

            view.Charge = true;
            Call(presenter, "LateUpdate");
            Assert.IsTrue(presenter.ChargeRingShown(2), "the Signal Buoy's charge ring");
            view.Hidden[2] = true;
            Call(presenter, "LateUpdate");
            Assert.IsFalse(presenter.ChargeRingShown(2), "hidden with its marker");
            Assert.AreEqual(objects, presenter.GetComponentsInChildren<Transform>(true).Length, "nothing is created after bind");
        }

        [Test]
        public void Forwarders_DrawNothingWithoutAPresenter()
        {
            var shots = new HostileShotViewsForwarder(null);
            Assert.IsNull(shots.Acquire(Vector3.zero, Color.white, 1f));
            shots.Splash(Vector3.zero);
            var pickups = new PickupViewsForwarder(() => null);
            Assert.IsNull(pickups.Acquire(PickupType.Gold, Vector3.zero, 1));
            var powerups = new PowerupViewsForwarder(null);
            Assert.IsNull(powerups.Create(PowerupKind.Tailwind, Vector3.zero));
            IEnemyEffects self = null;
            var effects = new EnemyEffectsForwarder(() => self);
            self = effects;
            effects.PlayExplosion(Vector3.zero, 3f); // forwarding to itself is ignored (the context's placeholder)

            var counter = Host("fx").AddComponent<CountingEffects>();
            var forward = new EnemyEffectsForwarder(() => counter);
            forward.PlayExplosion(Vector3.zero, 3f);
            Assert.AreEqual(1, counter.Calls, "a live presenter is called");
            var destroyed = counter;
            Object.DestroyImmediate(counter);
            forward.PlayExplosion(Vector3.zero, 3f); // a destroyed presenter is skipped, not called (it would throw)
            Assert.IsTrue(destroyed == null, "destroyed");

            var toasts = new ToastForwarder();
            toasts.Show("one", 1f);
            toasts.Show("two", 1f);
            var sink = new RecordingToasts();
            toasts.SetTarget(sink);
            CollectionAssert.AreEqual(new[] { "one", "two" }, sink.Shown, "toasts before the presenter are queued, in order");
        }

        public sealed class CountingEffects : MonoBehaviour, IEnemyEffects
        {
            public int Calls;
            public void PlayExplosion(Vector3 position, float blastRadius) => Calls++;
        }

        sealed class RecordingToasts : IToastSink
        {
            public readonly List<string> Shown = new List<string>();
            public void Show(string message, float seconds) => Shown.Add(message);
        }
    }
}
