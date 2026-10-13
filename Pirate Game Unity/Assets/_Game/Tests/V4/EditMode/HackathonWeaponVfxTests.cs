using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Presentation.Pooling;
using RoguePirates.V4.Presentation.Weapons;
using RoguePirates.V4.Views;
using RoguePirates.V4.Weapons;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// Hackathon (12 Oct, owner): the Cursed Doubloons' strikes throw the keg pickup's gold coins from the ship to the
    /// enemies; the Anchor Flail's chained maces are drawn as a ball with a chain from the ship.
    /// </summary>
    [Category("V4")]
    public sealed class HackathonWeaponVfxTests
    {
        sealed class FakeWeapons : IWeaponManager
        {
            public readonly List<WeaponSlot> List = new List<WeaponSlot>();
            public readonly List<ChainedMace> MaceList = new List<ChainedMace>();
            public IReadOnlyList<WeaponSlot> Slots => List;
            public PlayerProjectiles Projectiles => null;
            public void FireAllOnce() { }
            public void ArmAllKegs() { }
            public void FireLightningSalvo() { }
            public bool FireSlot(int index) => false;
            public void ClearInFlight() { }
            public IReadOnlyList<OrbitBehavior.HitSphere> OrbitHits => System.Array.Empty<OrbitBehavior.HitSphere>();
            public IReadOnlyList<ChainedMace> Maces => MaceList;
        }

        sealed class FakeShipView : IShipView
        {
            public Transform Anchor => null;
            public Vector3 Pos;
            public bool Exists => true;
            public Vector3 Position => Pos;
            public Vector3 Velocity => Vector3.zero;
            public Vector3 MovementForward => Vector3.forward;
            public float Hp => 100f;
            public float MaxHp => 100f;
            public float BoostNormalized => 0f;
        }

        GameObject _root, _host;
        PoolService _pools;
        WeaponVfxPresenter _presenter;
        readonly List<Object> _owned = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("HackathonVfxPoolRoot");
            _pools = new PoolService(_root.transform);
            _host = new GameObject("HackathonVfxPresenter");
            _presenter = _host.AddComponent<WeaponVfxPresenter>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_presenter != null) _presenter.Clear();
            _pools.Dispose();
            foreach (var o in _owned) if (o != null) Object.DestroyImmediate(o);
            _owned.Clear();
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_root);
        }

        WeaponConfig Weapon(string id, WeaponPlaceholderLook look)
        {
            var config = ScriptableObject.CreateInstance<WeaponConfig>();
            config.weaponId = id;
            config.visual.placeholderLook = look;
            _owned.Add(config);
            return config;
        }

        [Test]
        public void TheDoubloonsStrike_ThrowsTheKegsGoldCoins_FromTheShipToTheEnemy()
        {
            var settings = AssetDatabase.LoadAssetAtPath<DemoJuiceSettings>("Assets/_Game/Resources/DemoJuiceSettings.asset");
            Assert.IsNotNull(settings);
            var weapons = new FakeWeapons();
            weapons.List.Add(new WeaponSlot { Config = Weapon("cursed_doubloons", WeaponPlaceholderLook.Doubloons) });
            var ship = new FakeShipView { Pos = new Vector3(0f, 0f, 0f) };
            _presenter.Bind(_pools, weapons, ship, settings);

            var enemy = new Vector3(0f, 0f, 10f);
            _presenter.Aimed("cursed_doubloons", Vector3.forward, enemy, true);
            _presenter.LightningStrike(enemy, 1f);
            Assert.AreEqual(3, _presenter.CoinCount, "a few coins a strike");
            Assert.AreEqual(3, _presenter.CoinsInFlight);
            for (var i = 0; i < _presenter.CoinCount; i++)
                Assert.Less(Vector3.Distance(_presenter.CoinPosition(i), ship.Pos), 1.5f, "they start at the ship");
            // b7e3ec86 (13 Oct, owner): the coins are the gold barrel's coin particle (its coin material) on a camera-facing
            // quad, no longer the keg pickup's coin model.
            var coinMaterial = RoguePirates.V4.Presentation.Weapons.WeaponVisualSources.CoinSpriteMaterial(settings);
            Assert.IsNotNull(coinMaterial, "the gold barrel's coin particle material");
            Assert.IsTrue(_pools.Has(WeaponVfxPresenter.KeyPrefix + "coin/Doubloon_CoinSprite"), "the coins are the coin sprite");
            if (settings.goldPickupPrefab != null) Assert.IsFalse(_pools.Has(WeaponVfxPresenter.KeyPrefix + "coin/" + settings.goldPickupPrefab.name), "not the gold pickup model");
            var sprites = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(r => r.sharedMaterial == coinMaterial).ToList();
            Assert.AreEqual(3, sprites.Count, "three coin quads in the barrel's coin material");

            // b7e3ec86 (13 Oct, owner: "the enemies are dying before the coins hit them"): a fast, flat throw, at most ~0.1 s.
            _presenter.Tick(0.02f);
            Assert.AreEqual(3, _presenter.CoinsInFlight, "still flying");
            var mid = _presenter.CoinPosition(0);
            Assert.Greater(mid.z, 2f, "on their way");
            Assert.Less(mid.z, 10f);
            Assert.Greater(mid.y, 0.8f, "in an arc");

            for (var f = 0; f < 10; f++) _presenter.Tick(0.05f);
            Assert.AreEqual(0, _presenter.CoinsInFlight, "they reached the enemy");
            for (var f = 0; f < 20; f++) _presenter.Tick(0.05f);
            Assert.AreEqual(0, _presenter.CoinCount, "then fall away and go back to the pool");
        }

        [Test]
        public void AnotherWeaponsStrike_DrawsNoCoins()
        {
            var weapons = new FakeWeapons();
            weapons.List.Add(new WeaponSlot { Config = Weapon("lightning_mast", WeaponPlaceholderLook.None) });
            var settings = ScriptableObject.CreateInstance<DemoJuiceSettings>();
            _owned.Add(settings);
            _presenter.Bind(_pools, weapons, new FakeShipView(), settings);
            _presenter.Aimed("lightning_mast", Vector3.forward, Vector3.forward * 5f, true);
            _presenter.LightningStrike(Vector3.forward * 5f, 1f);
            Assert.AreEqual(0, _presenter.CoinCount);
        }

        [Test]
        public void EachMaceInFlight_IsDrawn_WithAChainFromTheShip()
        {
            var weapons = new FakeWeapons();
            var settings = ScriptableObject.CreateInstance<DemoJuiceSettings>();
            _owned.Add(settings);
            var ship = new FakeShipView { Pos = new Vector3(2f, 0f, 0f) };
            _presenter.Bind(_pools, weapons, ship, settings);
            var flail = ScriptableObject.CreateInstance<WeaponConfig>();
            flail.weaponId = "anchor_flail";
            flail.behavior = ConfigEnums.WeaponBehavior.ChainedMace;
            _owned.Add(flail);
            _presenter.PrewarmModels(new[] { flail });
            Assert.AreEqual(flail.mace.maxPerAttack + 2, _presenter.ChainCount, "the chains are made at bind, not mid-run (review)");
            weapons.MaceList.Add(new ChainedMace { Position = new Vector3(2f, 0.6f, 6f) });
            weapons.MaceList.Add(new ChainedMace { Position = new Vector3(-3f, 0.6f, 2f) });
            _presenter.SyncMaces();
            Assert.AreEqual(2, _presenter.MaceCount);
            Assert.Less(Vector3.Distance(_presenter.MacePosition(0), weapons.MaceList[0].Position), 0.001f);
            var (shipEnd, ballEnd) = _presenter.ChainEnds(1);
            Assert.Less(Vector3.Distance(new Vector3(shipEnd.x, 0f, shipEnd.z), ship.Pos), 0.001f, "the chain starts at the ship");
            Assert.Less(Vector3.Distance(ballEnd, weapons.MaceList[1].Position), 0.001f, "and ends at the ball");

            weapons.MaceList.RemoveAt(1);
            _presenter.SyncMaces();
            Assert.AreEqual(1, _presenter.MaceCount, "a mace back at the ship is no longer drawn");
            weapons.MaceList.Clear();
            _presenter.SyncMaces();
            Assert.AreEqual(0, _presenter.MaceCount);
        }
            [Test]
        public void TheArtTeamsAnchor_FliesOnItsChainLinks_FromTheShip()
        {
            var settings = AssetDatabase.LoadAssetAtPath<DemoJuiceSettings>("Assets/_Game/Resources/DemoJuiceSettings.asset");
            Assert.IsNotNull(settings.maceArtModel, "the art team's anchor (HackathonArtWiring)");
            Assert.IsNotNull(settings.maceArtModel.transform.Find("ChainAttach"), "with the point its chain meets");
            Assert.IsNotNull(settings.maceChainLinkModel, "and their chain link");
            var weapons = new FakeWeapons();
            var ship = new FakeShipView { Pos = Vector3.zero };
            _presenter.Bind(_pools, weapons, ship, settings);
            var flail = ScriptableObject.CreateInstance<WeaponConfig>();
            flail.weaponId = "test_flail";
            flail.behavior = ConfigEnums.WeaponBehavior.ChainedMace;
            _owned.Add(flail);
            _presenter.PrewarmModels(new[] { flail });
            var pooled = _pools.Has(WeaponVfxPresenter.KeyPrefix + "mace/" + settings.maceArtModel.name);
            Assert.IsTrue(pooled, "the anchor is pooled at bind");

            weapons.MaceList.Add(new ChainedMace { Position = new Vector3(0f, 0.6f, 8f) });
            _presenter.SyncMaces();
            Assert.AreEqual(1, _presenter.MaceCount);
            Assert.Less(Vector3.Distance(_presenter.MacePosition(0), weapons.MaceList[0].Position), 0.001f, "the anchor is where the simulation put the mace");
            var (shipEnd, anchorEnd) = _presenter.ChainEnds(0);
            Assert.Less(Vector3.Distance(new Vector3(shipEnd.x, 0f, shipEnd.z), ship.Pos), 0.001f, "the chain starts at the ship");
            Assert.Less(anchorEnd.z, weapons.MaceList[0].Position.z, "and ends at the anchor's ring, on the ship's side of it");
            if (SystemInfo.supportsInstancing)
                Assert.Greater(_presenter.ChainLinksDrawn, 10, "drawn as the art team's links");
        }

        [Test]
        public void AnInkPotShell_LobsTheArtTeamsPot_LandingWithTheShell()
        {
            var settings = ScriptableObject.CreateInstance<DemoJuiceSettings>();
            _owned.Add(settings);
            var pot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _owned.Add(pot);
            settings.weaponArtModels = new[] { new ArtWeaponModel { weaponId = "test_ink", lobModel = pot, lobArcHeight = 5f, lobPrewarm = 3 } };
            var weapons = new FakeWeapons();
            var ship = new FakeShipView { Pos = Vector3.zero };
            _presenter.Bind(_pools, weapons, ship, settings);
            _presenter.PrewarmModels(new[] { Weapon("test_ink", WeaponPlaceholderLook.None), Weapon("test_mortar", WeaponPlaceholderLook.None) });
            Assert.IsTrue(_pools.Has(WeaponVfxPresenter.KeyPrefix + "lob/" + pot.name), "the pot is pooled before the run");

            _presenter.Aimed("test_mortar", Vector3.forward, Vector3.forward * 10f, true);
            _presenter.TimedRing(Vector3.forward * 10f, 2f, 1f);
            Assert.AreEqual(0, _presenter.LobCount, "a weapon without a lob model throws nothing");

            _presenter.Aimed("test_ink", Vector3.forward, Vector3.forward * 10f, true);
            _presenter.TimedRing(Vector3.forward * 10f, 2f, 1f);
            Assert.AreEqual(1, _presenter.LobCount);
            _presenter.Tick(0.5f);
            var mid = _presenter.LobPosition(0);
            Assert.AreEqual(5f, mid.z, 0.6f, "halfway there at half the shell's time");
            Assert.Greater(mid.y, 4f, "in an arc");
            _presenter.Tick(0.6f);
            Assert.AreEqual(0, _presenter.LobCount, "gone when the shell lands");
        }
    }
}
