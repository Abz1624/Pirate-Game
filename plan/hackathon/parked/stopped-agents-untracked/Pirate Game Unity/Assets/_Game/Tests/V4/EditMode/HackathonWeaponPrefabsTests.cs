using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Editor;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Presentation.Pooling;
using RoguePirates.V4.Presentation.Weapons;
using RoguePirates.V4.Views;
using RoguePirates.V4.Weapons;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// Hackathon (13 Oct, owner: tweak every weapon's look in the Editor with real prefabs): every weapon's visuals come
    /// from prefabs (none still built in code, none with a missing script); the visuals that were built in code are now
    /// prefabs that look exactly as the code built them, and the presenters pool and draw them.
    /// </summary>
    [Category("V4")]
    public sealed class HackathonWeaponPrefabsTests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        const string SettingsPath = "Assets/_Game/Resources/DemoJuiceSettings.asset";

        static readonly string[] WeaponIds =
        {
            "anchor_flail", "broadside_battery", "chainshot", "cursed_doubloons", "deck_cannons", "flamethrower", "flying_cutlasses",
            "harpoon_gun", "lighthouse_lantern", "lightning_mast", "long_nine", "maelstrom_charge", "monkey_gunners", "mortar",
            "parrot_squadron", "powder_kegs", "siren_bell", "squid_ink_pots", "swivel_guns",
        };

        static DemoJuiceSettings Settings => AssetDatabase.LoadAssetAtPath<DemoJuiceSettings>(SettingsPath);
        static WeaponPrefabLibrary Library => AssetDatabase.LoadAssetAtPath<WeaponPrefabLibrary>(HackathonWeaponPrefabs.LibraryPath);

        [Test]
        public void TheLibrary_IsTheOneTheGameLoads()
        {
            Assert.IsNotNull(Library, HackathonWeaponPrefabs.LibraryPath);
            Assert.AreSame(Library, WeaponPrefabLibrary.Load(), "loaded from Resources at run time");
        }

        [Test]
        public void EveryWeapon_DrawsOnlyPrefabs_WithNoMissingScripts()
        {
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);
            using var config = new ConfigSetRuntime(set);
            var weapons = config.All<WeaponConfig>().ToDictionary(w => w.weaponId);
            CollectionAssert.IsSubsetOf(WeaponIds, weapons.Keys, "the 19 weapons are in the config set");
            var problems = new List<string>();
            foreach (var id in WeaponIds)
            {
                var entries = WeaponVisualCatalog.Of(weapons[id], Settings, Library);
                if (entries.Count < 2) problems.Add(id + ": too few visuals (" + entries.Count + ")");
                foreach (var e in entries)
                {
                    if (e.Prefab == null) { problems.Add(id + ": " + e.Role + " has no prefab (built in code)"); continue; }
                    if (!EditorUtility.IsPersistent(e.Prefab)) problems.Add(id + ": " + e.Role + " is not an asset");
                    var missing = e.Prefab.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                    if (missing > 0) problems.Add(id + ": " + e.Role + " (" + AssetDatabase.GetAssetPath(e.Prefab) + ") has " + missing + " missing scripts");
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void TheCodeBuiltVisuals_ArePrefabsInTheWeaponFolders()
        {
            var lib = Library;
            void Under(GameObject prefab, string folder)
            {
                Assert.IsNotNull(prefab, folder);
                StringAssert.StartsWith(HackathonWeaponPrefabs.Folder + "/" + folder + "/", AssetDatabase.GetAssetPath(prefab));
            }
            Under(lib.Projectile("monkey_gunners"), "monkey_gunners");
            Under(lib.Projectile("parrot_squadron"), "parrot_squadron");
            Under(lib.PulseRing("siren_bell"), "siren_bell");
            Under(lib.PulseRing("squid_ink_pots"), "squid_ink_pots");
            Under(lib.Chain("anchor_flail"), "anchor_flail");
            Under(lib.landingRing, "_Shared");
            foreach (var line in new[] { lib.PulseRing("siren_bell"), lib.PulseRing("squid_ink_pots"), lib.Chain("anchor_flail"), lib.landingRing })
                Assert.IsNotNull(line.GetComponent<LineRenderer>(), line.name + ": a LineRenderer at its root");
        }

        [Test]
        public void TheShotPrefabs_LookExactlyLikeTheCodeBuiltPlaceholders()
        {
            foreach (var (id, look) in new[] { ("monkey_gunners", WeaponPlaceholderLook.MonkeyGunners), ("parrot_squadron", WeaponPlaceholderLook.Parrots) })
            {
                var prefab = Library.Projectile(id);
                var code = WeaponPlaceholderModels.Projectile(look, null);
                var a = prefab.GetComponentsInChildren<MeshRenderer>(true);
                var b = code.GetComponentsInChildren<MeshRenderer>(true);
                Assert.AreEqual(b.Length, a.Length, id);
                for (var i = 0; i < a.Length; i++)
                {
                    Assert.AreEqual(b[i].GetComponent<MeshFilter>().sharedMesh, a[i].GetComponent<MeshFilter>().sharedMesh, id + " mesh");
                    Assert.AreEqual(b[i].transform.localPosition, a[i].transform.localPosition, id);
                    Assert.AreEqual(b[i].transform.localRotation, a[i].transform.localRotation, id);
                    Assert.AreEqual(b[i].transform.localScale, a[i].transform.localScale, id);
                    Assert.AreEqual(b[i].shadowCastingMode, a[i].shadowCastingMode, id);
                    var ma = a[i].sharedMaterial;
                    var mb = b[i].sharedMaterial;
                    Assert.IsTrue(EditorUtility.IsPersistent(ma), id + ": the material is an asset");
                    Assert.AreEqual(mb.shader, ma.shader, id);
                    foreach (var p in new[] { "_BaseColor", "_Color", "_EmissionColor" })
                        if (mb.HasProperty(p)) Assert.AreEqual(mb.GetColor(p), ma.GetColor(p), id + " " + p);
                    if (mb.HasProperty("_Smoothness")) Assert.AreEqual(mb.GetFloat("_Smoothness"), ma.GetFloat("_Smoothness"), id);
                    Assert.AreEqual(mb.IsKeywordEnabled("_EMISSION"), ma.IsKeywordEnabled("_EMISSION"), id + " emission");
                }
                Assert.IsEmpty(prefab.GetComponentsInChildren<Collider>(true), id + ": no colliders");
            }
        }

        [Test]
        public void TheLinePrefabs_HaveTheCodeBuiltSettings()
        {
            var host = new GameObject("CodeLines");
            try
            {
                var pulse = host.AddComponent<LineRenderer>();
                WeaponVfxPresenter.ConfigurePulseLine(pulse);
                foreach (var id in new[] { "siren_bell", "squid_ink_pots" })
                    SameLine(pulse, Library.PulseRing(id).GetComponent<LineRenderer>(), id);
                Object.DestroyImmediate(pulse);

                var ring = host.AddComponent<LineRenderer>();
                RoguePirates.V4.Presentation.Effects.HazardRingPresenter.ConfigureRingLine(ring);
                SameLine(ring, Library.landingRing.GetComponent<LineRenderer>(), "landing ring");
                Object.DestroyImmediate(ring);

                var chain = host.AddComponent<LineRenderer>();
                WeaponVfxPresenter.ConfigureChainLine(chain);
                var prefabChain = Library.Chain("anchor_flail").GetComponent<LineRenderer>();
                SameLine(chain, prefabChain, "chain");
                Assert.AreEqual(chain.widthMultiplier, prefabChain.widthMultiplier, 1e-5f);
                Assert.AreEqual(chain.textureMode, prefabChain.textureMode);
                Assert.AreEqual(chain.startColor, prefabChain.startColor);
                var code = WeaponVfxPresenter.ChainLinkTexture();
                var saved = (Texture2D)prefabChain.sharedMaterial.mainTexture;
                Assert.IsNotNull(saved, "the link texture is an asset");
                Assert.AreEqual(code.width, saved.width);
                Assert.AreEqual(code.height, saved.height);
                Assert.AreEqual(TextureWrapMode.Repeat, saved.wrapMode);
                Object.DestroyImmediate(code);
            }
            finally { Object.DestroyImmediate(host); }
        }

        static void SameLine(LineRenderer code, LineRenderer prefab, string what)
        {
            Assert.AreEqual(code.useWorldSpace, prefab.useWorldSpace, what);
            Assert.AreEqual(code.loop, prefab.loop, what);
            Assert.AreEqual(code.positionCount, prefab.positionCount, what);
            Assert.AreEqual(code.shadowCastingMode, prefab.shadowCastingMode, what);
            Assert.AreEqual(code.receiveShadows, prefab.receiveShadows, what);
            Assert.AreEqual(code.alignment, prefab.alignment, what);
            Assert.AreEqual(code.numCapVertices, prefab.numCapVertices, what);
            Assert.AreEqual(code.widthCurve.keys.Length, prefab.widthCurve.keys.Length, what);
            Assert.IsNotNull(prefab.sharedMaterial, what);
            Assert.IsTrue(EditorUtility.IsPersistent(prefab.sharedMaterial), what + ": the material is an asset");
            Assert.AreEqual("Sprites/Default", prefab.sharedMaterial.shader.name, what);
        }

        // ---- The presenters draw the prefabs ------------------------------------------------------------------------

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
            public IReadOnlyList<ChainedMace> Maces => System.Array.Empty<ChainedMace>();
        }

        sealed class FakeShip : IShipView
        {
            public Transform Anchor => null;
            public bool Exists => true;
            public Vector3 Position => Vector3.zero;
            public Vector3 Velocity => Vector3.zero;
            public Vector3 MovementForward => Vector3.forward;
            public float Hp => 100f;
            public float MaxHp => 100f;
            public float BoostNormalized => 0f;
        }

        [Test]
        public void ThePulseRings_AndTheChains_ComeFromThePrefabs_PooledBeforeTheRun()
        {
            var root = new GameObject("PrefabTestPools");
            var host = new GameObject("PrefabTestPresenter");
            var pools = new PoolService(root.transform);
            var owned = new List<Object>();
            try
            {
                var presenter = host.AddComponent<WeaponVfxPresenter>();
                var weapons = new FakeWeapons();
                WeaponConfig Weapon(string id, ConfigEnums.WeaponBehavior behavior)
                {
                    var w = ScriptableObject.CreateInstance<WeaponConfig>();
                    w.weaponId = id;
                    w.behavior = behavior;
                    w.aimOverlayColor = new Color(0.2f, 0.4f, 1f, 1f);
                    owned.Add(w);
                    return w;
                }
                var bell = Weapon("siren_bell", ConfigEnums.WeaponBehavior.Aura);
                var flail = Weapon("anchor_flail", ConfigEnums.WeaponBehavior.ChainedMace);
                weapons.List.Add(new WeaponSlot { Config = bell });
                presenter.Bind(pools, weapons, new FakeShip(), Settings);
                presenter.PrewarmModels(new[] { bell, flail });

                var ringPrefab = Library.PulseRing("siren_bell");
                var key = WeaponVfxPresenter.KeyPrefix + "pulse/" + ringPrefab.name;
                Assert.IsTrue(pools.Has(key), "the bell's ring is pooled at bind");
                var before = root.GetComponentsInChildren<LineRenderer>(true).Length;
                Assert.GreaterOrEqual(before, 2);

                presenter.WeaponPulse("siren_bell", new Vector3(3f, 0f, 4f), 5f);
                Assert.AreEqual(2, presenter.PulseCount);
                presenter.Tick(0.1f);
                Assert.AreEqual(before, root.GetComponentsInChildren<LineRenderer>(true).Length, "no line made mid-run");
                var live = root.GetComponentsInChildren<LineRenderer>(false).Where(l => l.gameObject.activeInHierarchy).ToList();
                Assert.AreEqual(2, live.Count, "both rings are pool instances of the prefab");
                Assert.AreSame(ringPrefab.GetComponent<LineRenderer>().sharedMaterial, live[0].sharedMaterial, "the prefab's material");
                Assert.AreEqual(0.2f, live[0].startColor.r, 1e-4f, "coloured with the weapon's colour (code)");
                presenter.Tick(1f);
                Assert.AreEqual(0, presenter.PulseCount);
                Assert.AreEqual(0, root.GetComponentsInChildren<LineRenderer>(false).Count(l => l.gameObject.activeInHierarchy), "back in the pool");

                var chainMaterial = Library.Chain("anchor_flail").GetComponent<LineRenderer>().sharedMaterial;
                var chains = host.GetComponentsInChildren<LineRenderer>(true).Where(l => l.name.StartsWith("MaceChain")).ToList();
                Assert.AreEqual(flail.mace.maxPerAttack + 2, chains.Count, "the chains are made at bind");
                Assert.IsTrue(chains.All(c => c.sharedMaterial == chainMaterial), "from the chain prefab");
                presenter.Clear();
            }
            finally
            {
                pools.Dispose();
                foreach (var o in owned) if (o != null) Object.DestroyImmediate(o);
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheSummonsShots_AreTheWeaponsProjectilePrefab_PrewarmedByTheCatalog()
        {
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);
            using var config = new ConfigSetRuntime(set);
            var defs = PoolCatalog.Build(config, moduleLab: false);
            foreach (var id in new[] { "monkey_gunners", "parrot_squadron" })
            {
                var prefab = Library.Projectile(id);
                var def = defs.FirstOrDefault(d => d.Key == ProjectileViewPresenter.KeyFor(prefab));
                Assert.IsNotNull(def.Prefab, id + ": its shots are a catalog pool");
                Assert.AreSame(prefab, def.Prefab, id);
                Assert.Greater(def.Prewarm, 0, id + ": prewarmed");
            }

            var root = new GameObject("ShotPools");
            var pools = new PoolService(root.transform);
            try
            {
                foreach (var d in defs.Where(d => d.Key.StartsWith(ProjectileViewPresenter.KeyPrefix))) pools.Register(d);
                var views = new ProjectileViewPresenter(pools, config, Settings, Library);
                var handle = views.Acquire("monkey_gunners", new Vector3(1f, 0.5f, 2f), Color.red, 0.3f);
                Assert.GreaterOrEqual(handle, 0);
                var live = root.GetComponentsInChildren<MeshRenderer>(false);
                Assert.AreEqual(1, live.Length, "one shot drawn");
                Assert.AreEqual(ProjectileViewPresenter.KeyFor(Library.Projectile("monkey_gunners")), live[0].transform.parent.name,
                    "the monkey's shot is an instance of its prefab (its pool)");
                Assert.AreSame(Library.Projectile("monkey_gunners").GetComponentInChildren<MeshRenderer>().sharedMaterial, live[0].sharedMaterial, "in its own colours (not tinted)");
                views.Release(handle);
            }
            finally
            {
                pools.Dispose();
                Object.DestroyImmediate(root);
            }
        }
    }
}
