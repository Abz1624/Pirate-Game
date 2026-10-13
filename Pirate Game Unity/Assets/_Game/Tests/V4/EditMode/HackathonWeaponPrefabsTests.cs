using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Editor;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Presentation.Effects;
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
    /// Hackathon (13 Oct, owner: every weapon visual as an editable prefab): every weapon has a prefab for each visual it
    /// draws, in its own folder, linked in the weapon visual catalog the game loads, with no missing or V3 script; the
    /// copies look like what the game drew before (same meshes, materials, particles, scales); the presenters draw and
    /// pool the catalog's prefabs (prewarmed, nothing made mid-run) and apply their tuning.
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
        static WeaponVisualCatalog Catalog => AssetDatabase.LoadAssetAtPath<WeaponVisualCatalog>(HackathonWeaponPrefabs.CatalogPath);
        static Dictionary<string, WeaponConfig> Configs => HackathonWeaponPrefabs.Weapons().ToDictionary(w => w.weaponId);

        [Test]
        public void TheCatalog_IsTheOneTheGameLoads()
        {
            Assert.IsNotNull(Catalog, HackathonWeaponPrefabs.CatalogPath);
            Assert.AreSame(Catalog, WeaponVisualCatalog.Load(), "loaded from Resources at run time");
        }

        [Test]
        public void EveryWeapon_HasAPrefabForEachVisual_InItsFolder_WithNoMissingOrV3Scripts()
        {
            var configs = Configs;
            CollectionAssert.IsSubsetOf(WeaponIds, configs.Keys, "the 19 weapons");
            var problems = new List<string>();
            foreach (var id in WeaponIds)
            {
                var sources = WeaponVisualSources.For(configs[id], Settings);
                if (sources.Count < 2) problems.Add(id + ": too few visuals (" + sources.Count + ")");
                var slots = sources.Select(s => s.Slot).Concat(HackathonWeaponPrefabs.ArtMapping().Where(a => a.weaponId == id).Select(a => a.slot));
                foreach (var slot in slots)
                {
                    var prefab = Catalog.Get(id, slot);
                    if (prefab == null) { problems.Add(id + ": no " + slot + " prefab in the catalog"); continue; }
                    var path = AssetDatabase.GetAssetPath(prefab);
                    if (path != HackathonWeaponPrefabs.PrefabPath(id, slot)) problems.Add(id + ": " + slot + " is " + path);
                    if (prefab.GetComponent<WeaponVisualTuning>() == null) problems.Add(id + ": " + slot + " has no WeaponVisualTuning");
                    foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                    {
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0) problems.Add(path + ": missing script on " + t.name);
                        foreach (var b in t.GetComponents<MonoBehaviour>())
                        {
                            var ns = b != null ? b.GetType().Namespace ?? "" : "";
                            if (b != null && !ns.StartsWith("RoguePirates.V4") && !ns.StartsWith("Unity") && !ns.StartsWith("TMPro"))
                                problems.Add(path + ": " + b.GetType().FullName + " (not V4) on " + t.name);
                        }
                    }
                    if (prefab.GetComponentsInChildren<Component>(true).Any(c => c != null && c.GetType().Name == "VisualEffect")) problems.Add(path + ": VFX Graph (not WebGL safe)");
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void TheCopies_LookLikeWhatTheGameDrewBefore()
        {
            var configs = Configs;
            var problems = new List<string>();
            foreach (var id in WeaponIds)
            foreach (var source in WeaponVisualSources.For(configs[id], Settings).Where(s => s.How == WeaponVisualSources.Build.Copy))
            {
                var copy = Catalog.Get(id, source.Slot);
                if (copy == null) { problems.Add(id + " " + source.Slot + ": none"); continue; }
                var what = id + " " + source.Slot + " (" + source.Prefab.name + ")";
                if ((copy.transform.localScale - source.Prefab.transform.localScale).sqrMagnitude > 1e-8f) problems.Add(what + ": root scale");
                var a = source.Prefab.GetComponentsInChildren<Renderer>(true);
                var b = copy.GetComponentsInChildren<Renderer>(true);
                if (a.Length != b.Length) { problems.Add(what + ": " + b.Length + " renderers, was " + a.Length); continue; }
                for (var i = 0; i < a.Length; i++)
                {
                    if (!a[i].sharedMaterials.SequenceEqual(b[i].sharedMaterials)) problems.Add(what + ": materials of " + b[i].name);
                    if (a[i].enabled != b[i].enabled || a[i].gameObject.activeSelf != b[i].gameObject.activeSelf) problems.Add(what + ": shown state of " + b[i].name);
                    var ma = a[i].GetComponent<MeshFilter>();
                    var mb = b[i].GetComponent<MeshFilter>();
                    if (ma != null && (mb == null || ma.sharedMesh != mb.sharedMesh)) problems.Add(what + ": mesh of " + b[i].name);
                    if ((a[i].transform.localScale - b[i].transform.localScale).sqrMagnitude > 1e-8f) problems.Add(what + ": scale of " + b[i].name);
                }
                if (source.Prefab.GetComponentsInChildren<ParticleSystem>(true).Length != copy.GetComponentsInChildren<ParticleSystem>(true).Length)
                    problems.Add(what + ": particle systems");
                if (source.Slot == WeaponVisualSlot.Projectile && copy.GetComponent<WeaponVisualTuning>().keepOwnColours != source.OwnColours)
                    problems.Add(what + ": tinted as before");
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void TheCodeBuiltVisuals_ArePrefabsWithTheCodeBuiltSettings()
        {
            var host = new GameObject("CodeLines");
            try
            {
                var pulse = host.AddComponent<LineRenderer>();
                WeaponVfxPresenter.ConfigurePulseLine(pulse);
                foreach (var id in new[] { "siren_bell", "squid_ink_pots" }) SameLine(pulse, Catalog.Get(id, WeaponVisualSlot.PulseRing), id + " pulse");
                Object.DestroyImmediate(pulse);

                var ring = host.AddComponent<LineRenderer>();
                HazardRingPresenter.ConfigureRingLine(ring);
                foreach (var id in new[] { "mortar", "maelstrom_charge", "squid_ink_pots" }) SameLine(ring, Catalog.Get(id, WeaponVisualSlot.LandingRing), id + " landing ring");
                SameLine(ring, Catalog.Get("flamethrower", WeaponVisualSlot.PatchRing), "flamethrower patch ring");
                Object.DestroyImmediate(ring);

                var chain = host.AddComponent<LineRenderer>();
                WeaponVfxPresenter.ConfigureChainLine(chain);
                var prefabChain = Catalog.Get("anchor_flail", WeaponVisualSlot.Chain);
                SameLine(chain, prefabChain, "chain");
                var line = prefabChain.GetComponent<LineRenderer>();
                Assert.AreEqual(chain.widthMultiplier, line.widthMultiplier, 1e-5f);
                Assert.AreEqual(chain.textureMode, line.textureMode);
                Assert.AreEqual(chain.startColor, line.startColor);
                Assert.IsNotNull(line.sharedMaterial.mainTexture, "the link texture is an asset");
                Assert.AreEqual(TextureWrapMode.Repeat, line.sharedMaterial.mainTexture.wrapMode);
            }
            finally { Object.DestroyImmediate(host); }

            // The monkey's shot: the coloured capsule the code built, its material an asset with the same colours.
            var shot = Catalog.Get("monkey_gunners", WeaponVisualSlot.Projectile);
            var code = WeaponPlaceholderModels.Projectile(WeaponPlaceholderLook.MonkeyGunners, null);
            var pa = shot.GetComponentsInChildren<MeshRenderer>(true);
            var pb = code.GetComponentsInChildren<MeshRenderer>(true);
            Assert.AreEqual(pb.Length, pa.Length);
            for (var i = 0; i < pa.Length; i++)
            {
                Assert.AreEqual(pb[i].GetComponent<MeshFilter>().sharedMesh, pa[i].GetComponent<MeshFilter>().sharedMesh);
                Assert.AreEqual(pb[i].transform.localScale, pa[i].transform.localScale);
                Assert.IsTrue(EditorUtility.IsPersistent(pa[i].sharedMaterial), "the material is an asset");
                Assert.AreEqual(pb[i].sharedMaterial.shader, pa[i].sharedMaterial.shader);
                foreach (var p in new[] { "_BaseColor", "_EmissionColor" })
                    if (pb[i].sharedMaterial.HasProperty(p)) Assert.AreEqual(pb[i].sharedMaterial.GetColor(p), pa[i].sharedMaterial.GetColor(p), p);
            }
            Assert.IsTrue(shot.GetComponent<WeaponVisualTuning>().keepOwnColours, "the placeholder shot keeps its colours");
            Assert.IsTrue(Catalog.Get("cursed_doubloons", WeaponVisualSlot.Coin).GetComponent<WeaponVisualTuning>().faceCamera, "the coin sprite faces the camera");
        }

        static void SameLine(LineRenderer code, GameObject prefab, string what)
        {
            Assert.IsNotNull(prefab, what);
            var line = prefab.GetComponent<LineRenderer>();
            Assert.IsNotNull(line, what + ": a LineRenderer at its root");
            Assert.AreEqual(code.useWorldSpace, line.useWorldSpace, what);
            Assert.AreEqual(code.loop, line.loop, what);
            Assert.AreEqual(code.positionCount, line.positionCount, what);
            Assert.AreEqual(code.shadowCastingMode, line.shadowCastingMode, what);
            Assert.AreEqual(code.receiveShadows, line.receiveShadows, what);
            Assert.IsTrue(EditorUtility.IsPersistent(line.sharedMaterial), what + ": the material is an asset");
            Assert.AreEqual("Sprites/Default", line.sharedMaterial.shader.name, what);
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
        public void TheEffects_AreDrawnFromEachWeaponsPrefabs_PrewarmedBeforeTheRun_WithTheirTuning()
        {
            var root = new GameObject("PrefabTestPools");
            var host = new GameObject("PrefabTestPresenter");
            var rings = new GameObject("PrefabTestRings").AddComponent<HazardRingPresenter>();
            var pools = new PoolService(root.transform);
            var configs = Configs;
            var tuning = Catalog.Get("siren_bell", WeaponVisualSlot.PulseRing).GetComponent<WeaponVisualTuning>();
            var width = tuning.width;
            try
            {
                var presenter = host.AddComponent<WeaponVfxPresenter>();
                presenter.Visuals = Catalog;
                var weapons = new FakeWeapons();
                foreach (var id in new[] { "siren_bell", "mortar", "maelstrom_charge", "lightning_mast", "powder_kegs" }) weapons.List.Add(new WeaponSlot { Config = configs[id] });
                rings.Bind(null, null, () => false);
                presenter.Bind(pools, weapons, new FakeShip(), Settings);
                presenter.HazardRings = rings;
                presenter.PrewarmModels(configs.Values);
                var made = root.GetComponentsInChildren<Transform>(true).Length + rings.GetComponentsInChildren<Transform>(true).Length;

                // The Siren Bell's pulse: two rings from its pulse ring prefab; the tuning's width applies.
                tuning.width = 2f;
                presenter.WeaponPulse("siren_bell", new Vector3(3f, 0f, 4f), 5f);
                Assert.AreEqual(2, presenter.PulseCount);
                presenter.Tick(0.05f);
                var ringMaterial = Catalog.Get("siren_bell", WeaponVisualSlot.PulseRing).GetComponent<LineRenderer>().sharedMaterial;
                var live = root.GetComponentsInChildren<LineRenderer>(false).Where(l => l.gameObject.activeInHierarchy).ToList();
                Assert.AreEqual(2, live.Count(l => l.sharedMaterial == ringMaterial), "both rings are instances of the bell's prefab");
                Assert.Greater(live.Max(l => l.widthMultiplier), 0.5f * 1.2f * 1.5f, "the tuning's width doubles the code's");

                // A mortar shell: the landing ring is the mortar's prefab; its impact at the same spot is the mortar's.
                presenter.Aimed("mortar", Vector3.forward, new Vector3(10f, 0f, 0f), true);
                presenter.TimedRing(new Vector3(10f, 0.15f, 0f), 3f, 0.6f);
                Assert.AreEqual(1, rings.TimedRingCount);
                presenter.AreaImpact(new Vector3(10f, 0.15f, 0f), 3f);
                var impactName = Catalog.Get("mortar", WeaponVisualSlot.Impact).name;
                Assert.Greater(root.GetComponentsInChildren<Transform>(false).Count(t => t.gameObject.activeInHierarchy && t.name.EndsWith("/" + impactName)), 0,
                    "the mortar's impact prefab plays");
                Assert.AreEqual(1, presenter.LobCount, "the mortar shell is thrown (the art team's fire shell)");

                // A Maelstrom shell lands and its whirlpool opens there: the art team's whirlpool is drawn for its seconds.
                presenter.Aimed("maelstrom_charge", Vector3.forward, new Vector3(-12f, 0f, 6f), true);
                presenter.TimedRing(new Vector3(-12f, 0.15f, 6f), 3f, 0.6f);
                Assert.AreEqual(0, presenter.WhirlpoolCount, "no whirlpool while the shell flies");
                presenter.AreaImpact(new Vector3(-12f, 0.15f, 6f), 3f);
                presenter.TimedRing(new Vector3(-12f, 0.15f, 6f), 3f, 3f);
                Assert.AreEqual(1, presenter.WhirlpoolCount, "the whirlpool opens where the shell landed");

                // A keg: dropped as the kegs' prefab, its blast the kegs' blast prefab.
                var keg = presenter.MineDropped(new Vector3(-4f, 0f, 2f));
                Assert.GreaterOrEqual(keg, 0);
                presenter.Explosion(new Vector3(-4f, 0f, 2f), 2.8f);
                var blastName = Catalog.Get("powder_kegs", WeaponVisualSlot.Blast).name;
                Assert.Greater(root.GetComponentsInChildren<Transform>(false).Count(t => t.gameObject.activeInHierarchy && t.name.EndsWith("/" + blastName)), 0, "the kegs' blast prefab plays");

                presenter.Tick(0.05f);
                Assert.AreEqual(made, root.GetComponentsInChildren<Transform>(true).Length + rings.GetComponentsInChildren<Transform>(true).Length,
                    "every instance came from the prewarm: nothing was made mid-run");
                Assert.AreEqual(0, pools.TotalCreatedAfterPrewarm, "no pool grew past its prewarm");
                presenter.Clear();
            }
            finally
            {
                tuning.width = width;
                pools.Dispose();
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(rings.gameObject);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheShots_AreTheWeaponsProjectilePrefabs_PrewarmedByThePoolCatalog()
        {
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);
            using var config = new ConfigSetRuntime(set);
            var defs = PoolCatalog.Build(config, moduleLab: false);
            foreach (var id in new[] { "monkey_gunners", "chainshot", "deck_cannons", "harpoon_gun" })
            {
                var prefab = Catalog.Get(id, WeaponVisualSlot.Projectile);
                var def = defs.FirstOrDefault(d => d.Key == ProjectileViewPresenter.KeyFor(prefab));
                Assert.AreSame(prefab, def.Prefab, id + ": its shots are a catalog pool");
                Assert.Greater(def.Prewarm, 0, id + ": prewarmed");
            }

            var root = new GameObject("ShotPools");
            var pools = new PoolService(root.transform);
            try
            {
                foreach (var d in defs.Where(d => d.Key.StartsWith(ProjectileViewPresenter.KeyPrefix))) pools.Register(d);
                pools.PrewarmAll();
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true; // the cannonball's tint copies its material (edit mode warns)
                var views = new ProjectileViewPresenter(pools, config, Settings) { Visuals = Catalog };
                var monkey = views.Acquire("monkey_gunners", new Vector3(1f, 0.5f, 2f), Color.red, 0.3f);
                var shotMaterial = Catalog.Get("monkey_gunners", WeaponVisualSlot.Projectile).GetComponentInChildren<MeshRenderer>().sharedMaterial;
                Assert.IsTrue(root.GetComponentsInChildren<MeshRenderer>(false).Any(r => r.sharedMaterial == shotMaterial), "the monkey's shot in its own colours");
                var cannon = views.Acquire("deck_cannons", new Vector3(3f, 0.5f, 2f), Color.red, 0.3f);
                Assert.GreaterOrEqual(cannon, 0);
                views.Release(cannon);
                Assert.AreEqual("deck_cannons", views.EndedNear(new Vector3(3f, 0.5f, 2.5f)), "a splash where the shot ended is that weapon's");
                views.Release(monkey);
            }
            finally
            {
                pools.Dispose();
                Object.DestroyImmediate(root);
            }
        }
    }
}
