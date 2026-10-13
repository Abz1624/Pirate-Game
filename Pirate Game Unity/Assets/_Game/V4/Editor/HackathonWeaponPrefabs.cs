using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Presentation.Effects;
using RoguePirates.V4.Presentation.Weapons;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// Hackathon (13 Oct, owner: "tweak all weapons and their VFX in the Unity Editor without code"): builds one prefab per
    /// visual per weapon under Assets/_Game/V4/Weapons/Prefabs/&lt;weapon_id&gt;/&lt;weapon_id&gt;_&lt;Visual&gt;.prefab and links
    /// them in the weapon visual catalog (Weapons/Resources/V4WeaponVisuals.asset), which the presenters pool, prewarm and
    /// draw. Each prefab is a full copy (unpacked, so it can be edited on its own) of what the game drew for that weapon
    /// before (<see cref="WeaponVisualSources"/>: the juice settings' shared prefabs, the art team's models), or, for the
    /// visuals that were built in code (coloured stand-ins, pulse and landing rings, the chain line, the coin sprite), the
    /// same object saved with its materials and texture as assets; so the look is unchanged. Each gets a
    /// <see cref="WeaponVisualTuning"/> for the numbers the code applies. Missing scripts are stripped, and any script that is
    /// not V4 or Unity's (V3) too. Also the art team's weapon art listed as "weaponvisual" in Demo/Art/ArtTeamModels.txt is
    /// copied in for its visual. A prefab already on disk is kept (the owner's edits stay); Rebuild remakes them all.
    /// Presentation only: no config changes. The per-weapon list is plan/hackathon/WEAPON-PREFABS.md (<see cref="ListMarkdown"/>).
    /// </summary>
    public static class HackathonWeaponPrefabs
    {
        public const string Root = "Assets/_Game/V4/Weapons";
        public const string Folder = Root + "/Prefabs";
        public const string CatalogPath = Root + "/Resources/" + WeaponVisualCatalog.ResourcePath + ".asset";
        public const string WeaponConfigs = "Assets/_Game/V4/Data/Weapons";
        const string SettingsPath = "Assets/_Game/Resources/DemoJuiceSettings.asset";
        const string MappingFile = "Assets/_Game/V4/Demo/Art/ArtTeamModels.txt";
        /// <summary>The sea's render queue (Stylized Water 3); flat art on the water is queued after it.</summary>
        const int WaterQueue = 2998;

        [MenuItem("Tools/Rogue Pirates/Weapons/Build Weapon Prefabs (missing ones)")]
        public static void BuildMissing() => Build(false);

        [MenuItem("Tools/Rogue Pirates/Weapons/Rebuild All Weapon Prefabs (discards edits)")]
        public static void RebuildAll()
        {
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Rebuild weapon prefabs",
                    "Remake every prefab under " + Folder + " from the current shared look? Edits to them are lost.", "Rebuild", "Cancel")) return;
            Build(true);
        }

        /// <summary>The 19 weapon configs (sorted by id).</summary>
        public static List<WeaponConfig> Weapons()
        {
            var list = new List<WeaponConfig>();
            foreach (var guid in AssetDatabase.FindAssets("t:WeaponConfig", new[] { WeaponConfigs }))
            {
                var w = AssetDatabase.LoadAssetAtPath<WeaponConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (w != null && !string.IsNullOrEmpty(w.weaponId)) list.Add(w);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.weaponId, b.weaponId));
            return list;
        }

        public static string PrefabPath(string weaponId, WeaponVisualSlot slot) => Folder + "/" + weaponId + "/" + weaponId + "_" + slot + ".prefab";

        /// <summary>Builds the prefabs (missing ones, or all with <paramref name="overwrite"/>) and links the catalog;
        /// returns a log line per prefab.</summary>
        public static List<string> Build(bool overwrite)
        {
            var log = new List<string>();
            var settings = AssetDatabase.LoadAssetAtPath<DemoJuiceSettings>(SettingsPath);
            if (settings == null) { Debug.LogError("[WeaponPrefabs] no " + SettingsPath); return log; }
            EnsureFolder(Folder);
            EnsureFolder(Root + "/Resources");

            var catalog = AssetDatabase.LoadAssetAtPath<WeaponVisualCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<WeaponVisualCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var sets = new List<WeaponVisualSet>();
            if (catalog.weapons != null) sets.AddRange(catalog.weapons.Where(e => e != null && !string.IsNullOrEmpty(e.weaponId)));
            WeaponVisualSet SetOf(string id)
            {
                var set = sets.FirstOrDefault(e => e.weaponId == id);
                if (set == null) sets.Add(set = new WeaponVisualSet { weaponId = id });
                return set;
            }

            var art = ArtMapping();
            foreach (var w in Weapons())
            {
                var set = SetOf(w.weaponId);
                EnsureFolder(Folder + "/" + w.weaponId);
                foreach (var source in WeaponVisualSources.For(w, settings))
                    set.Set(source.Slot, Make(w.weaponId, source.Slot, overwrite, log, root => BuildFrom(source, w, settings, root)));
                foreach (var (slot, assetPath, child) in art.Where(a => a.weaponId == w.weaponId).Select(a => (a.slot, a.asset, a.child)))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    if (asset == null) { log.Add("MISSING art " + assetPath + " for " + w.weaponId + " " + slot); continue; }
                    set.Set(slot, Make(w.weaponId, slot, overwrite, log, root => CopyOf(asset, child, root)));
                }
            }
            catalog.weapons = sets.OrderBy(e => e.weaponId, StringComparer.Ordinal).ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            foreach (var line in log) Debug.Log("[WeaponPrefabs] " + line);
            return log;
        }

        // ---- The art team's weapon art (Demo/Art/ArtTeamModels.txt "weaponvisual" lines) --------------------------------

        public static List<(string weaponId, WeaponVisualSlot slot, string asset, string child)> ArtMapping()
        {
            var list = new List<(string, WeaponVisualSlot, string, string)>();
            if (!System.IO.File.Exists(MappingFile)) return list;
            foreach (var line in System.IO.File.ReadAllLines(MappingFile))
            {
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var f = line.Split(';');
                if (f[0] != "weaponvisual" || f.Length < 4 || !Enum.TryParse<WeaponVisualSlot>(f[2], out var slot)) continue;
                list.Add((f[1], slot, f[3], f.Length > 4 && f[4].Length > 0 ? f[4] : null));
            }
            return list;
        }

        // ---- Making one prefab ----------------------------------------------------------------------------------------

        /// <summary>The weapon's prefab for a visual: kept when on disk (unless <paramref name="overwrite"/>), else built.</summary>
        static GameObject Make(string weaponId, WeaponVisualSlot slot, bool overwrite, List<string> log, Func<string, GameObject> build)
        {
            var path = PrefabPath(weaponId, slot);
            if (!overwrite)
            {
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (existing != null) { log.Add("kept " + path); return existing; }
            }
            var root = build(path);
            if (root == null) { log.Add("SKIPPED " + path + " (no source)"); return null; }
            root.name = System.IO.Path.GetFileNameWithoutExtension(path);
            Clean(root, log, path);
            if (root.GetComponent<WeaponVisualTuning>() == null) root.AddComponent<WeaponVisualTuning>();
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            log.Add("built " + path);
            return saved;
        }

        static GameObject BuildFrom(WeaponVisualSources.Source source, WeaponConfig w, DemoJuiceSettings s, string path)
        {
            GameObject root;
            switch (source.How)
            {
                case WeaponVisualSources.Build.Copy:
                    root = CopyOf(source.Prefab, null, path);
                    break;
                case WeaponVisualSources.Build.MonkeyShot:
                    root = Placeholder(WeaponPlaceholderModels.Projectile(WeaponPlaceholderLook.MonkeyGunners, null), path);
                    break;
                case WeaponVisualSources.Build.ParrotShot:
                    root = Placeholder(WeaponPlaceholderModels.Projectile(WeaponPlaceholderLook.Parrots, null), path);
                    break;
                case WeaponVisualSources.Build.Coin:
                    root = Placeholder(WeaponPlaceholderModels.Coin(null), path);
                    break;
                case WeaponVisualSources.Build.Mace:
                    root = Placeholder(WeaponPlaceholderModels.Mace(null), path);
                    break;
                case WeaponVisualSources.Build.Parrot:
                case WeaponVisualSources.Build.Monkey:
                    root = Placeholder(WeaponPlaceholderModels.Summon(w.visual.placeholderLook, w.visual.summonModel, null), path);
                    break;
                case WeaponVisualSources.Build.Lantern:
                case WeaponVisualSources.Build.Anchor:
                    root = Placeholder(WeaponPlaceholderModels.Orbit(w.visual.placeholderLook, w.visual.orbitModel, null), path);
                    break;
                case WeaponVisualSources.Build.CoinSprite:
                    root = CoinSprite(s, path);
                    break;
                case WeaponVisualSources.Build.PulseRing:
                    root = Line(path, WeaponVfxPresenter.ConfigurePulseLine, null);
                    break;
                case WeaponVisualSources.Build.Ring:
                    root = Line(path, HazardRingPresenter.ConfigureRingLine, null);
                    break;
                case WeaponVisualSources.Build.Chain:
                    root = Line(path, WeaponVfxPresenter.ConfigureChainLine, ChainTexture(path));
                    break;
                default:
                    root = null;
                    break;
            }
            if (root == null) return null;
            var tuning = root.GetComponent<WeaponVisualTuning>();
            if (tuning == null) tuning = root.AddComponent<WeaponVisualTuning>();
            tuning.keepOwnColours = source.OwnColours;
            tuning.faceCamera = source.How == WeaponVisualSources.Build.CoinSprite;
            return root;
        }

        /// <summary>An unpacked copy of an asset (or of one of its children), at the asset's own root pose; a flat model
        /// the code sizes to a radius records that radius (at scale 1) in its tuning.</summary>
        static GameObject CopyOf(GameObject asset, string childPath, string path)
        {
            if (asset == null) return null;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            if (instance == null) instance = Object.Instantiate(asset);
            if (PrefabUtility.IsPartOfPrefabInstance(instance))
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var content = instance;
            if (childPath != null)
            {
                var child = instance.transform.Find(childPath);
                if (child == null) { Object.DestroyImmediate(instance); return null; }
                child.SetParent(null, true); // keeps its world rotation and scale (as the art drew it)
                child.gameObject.SetActive(true);
                Object.DestroyImmediate(instance);
                content = child.gameObject;
            }
            var slot = SlotOf(path);
            if (slot != WeaponVisualSlot.Aura && slot != WeaponVisualSlot.Whirlpool) return content;

            // A flat model the code sizes to a radius (radius / base radius) and poses itself: the art goes under a plain
            // root, as the art drew it (rotation and scale kept), centred; the base radius is measured at root scale 1.
            var root = new GameObject();
            content.transform.position = Vector3.zero;
            content.transform.SetParent(root.transform, true);
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (!(r is ParticleSystemRenderer)) bounds.Encapsulate(r.bounds);
            var tuning = root.AddComponent<WeaponVisualTuning>();
            tuning.baseRadius = Mathf.Max(0.01f, Mathf.Max(bounds.extents.x, bounds.extents.z));
            // Drawn on the water: a transparent material queued before the water (Stylized Water, 2998) is hidden under it,
            // so the prefab gets its own copy queued just after the water (the art's material is left as it is).
            var n = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                var mats = r.sharedMaterials;
                for (var i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || mats[i].renderQueue < 2500 || mats[i].renderQueue > WaterQueue) continue;
                    mats[i] = SaveMaterial(new Material(mats[i]) { renderQueue = WaterQueue + 2 }, MaterialPath(path, n++), mats[i]);
                }
                r.sharedMaterials = mats;
            }
            return root;
        }

        static WeaponVisualSlot SlotOf(string path)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            return Enum.TryParse<WeaponVisualSlot>(name.Substring(name.LastIndexOf('_') + 1), out var slot) ? slot : WeaponVisualSlot.Projectile;
        }

        /// <summary>A copy of a coloured stand-in template, its shared runtime materials saved as assets beside the prefab.</summary>
        static GameObject Placeholder(GameObject template, string path)
        {
            if (template == null) return null;
            var root = Object.Instantiate(template);
            root.SetActive(true);
            var saved = new Dictionary<Material, Material>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.hideFlags = HideFlags.None;
                var renderer = t.GetComponent<Renderer>();
                if (renderer == null || renderer.sharedMaterial == null) continue;
                var m = renderer.sharedMaterial;
                if (!saved.TryGetValue(m, out var asset))
                {
                    asset = SaveMaterial(new Material(m), MaterialPath(path, saved.Count), m);
                    saved[m] = asset;
                }
                renderer.sharedMaterial = asset;
            }
            return root;
        }

        /// <summary>The Cursed Doubloons' coin sprite: a unit quad with the gold barrel's coin particle material (a copy).</summary>
        static GameObject CoinSprite(DemoJuiceSettings s, string path)
        {
            var source = WeaponVisualSources.CoinSpriteMaterial(s);
            if (source == null) return null;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = SaveMaterial(new Material(source), MaterialPath(path, 0), source);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        /// <summary>A line renderer as the code builds it, with a Sprites/Default material asset (and texture).</summary>
        static GameObject Line(string path, Action<LineRenderer> configure, Texture2D texture)
        {
            var go = new GameObject();
            var line = go.AddComponent<LineRenderer>();
            configure(line);
            var material = new Material(Shader.Find("Sprites/Default"));
            if (texture != null) material.mainTexture = texture;
            line.sharedMaterial = SaveMaterial(material, MaterialPath(path, 0), null);
            return go;
        }

        /// <summary>The chain's link texture as a PNG beside the prefab (pixel for pixel what the code generates).</summary>
        static Texture2D ChainTexture(string prefabPath)
        {
            var texturePath = prefabPath.Replace(".prefab", "Links.png");
            var tex = WeaponVfxPresenter.ChainLinkTexture();
            System.IO.File.WriteAllBytes(texturePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        }

        static string MaterialPath(string prefabPath, int n) => prefabPath.Replace(".prefab", n == 0 ? ".mat" : "_" + n + ".mat");

        static Material SaveMaterial(Material material, string path, Material source)
        {
            material.hideFlags = HideFlags.None;
            material.name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (source != null)
            {
                material.shaderKeywords = source.shaderKeywords;
                material.globalIlluminationFlags = source.globalIlluminationFlags;
            }
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>Strips missing scripts and any script that is not V4's or Unity's (V3 code) from a prefab copy.</summary>
        static void Clean(GameObject root, List<string> log, string path)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var missing = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                if (missing > 0) log.Add("  " + path + ": removed " + missing + " missing script(s) on " + t.name);
                foreach (var b in t.GetComponents<MonoBehaviour>())
                {
                    if (b == null) continue;
                    var ns = b.GetType().Namespace ?? "";
                    if (ns.StartsWith("RoguePirates.V4") || ns.StartsWith("UnityEngine") || ns.StartsWith("Unity") || ns.StartsWith("TMPro")) continue;
                    log.Add("  " + path + ": removed " + b.GetType().FullName + " on " + t.name + " (not V4)");
                    Object.DestroyImmediate(b);
                }
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }

        // ---- The list (plan/hackathon/WEAPON-PREFABS.md) --------------------------------------------------------------

        /// <summary>Every weapon's prefabs as Markdown tables: visual, prefab, made from, what the code still sets.</summary>
        public static string ListMarkdown()
        {
            var settings = AssetDatabase.LoadAssetAtPath<DemoJuiceSettings>(SettingsPath);
            var catalog = AssetDatabase.LoadAssetAtPath<WeaponVisualCatalog>(CatalogPath);
            var art = ArtMapping();
            var sb = new StringBuilder();
            foreach (var w in Weapons())
            {
                sb.AppendLine("### " + w.weaponId + " (" + w.displayName + ", " + w.behavior + ")").AppendLine();
                sb.AppendLine("| Visual | Prefab | Made from | Code sets at run time |").AppendLine("|---|---|---|---|");
                foreach (var source in WeaponVisualSources.For(w, settings))
                {
                    var prefab = catalog != null ? catalog.Get(w.weaponId, source.Slot) : null;
                    var from = source.How == WeaponVisualSources.Build.Copy ? "copy of `" + AssetDatabase.GetAssetPath(source.Prefab) + "`" : "built as the code built it (" + source.How + ")";
                    sb.AppendLine("| " + source.Slot + " | `" + (prefab != null ? AssetDatabase.GetAssetPath(prefab) : "(none)") + "` | " + from + " | " + source.Code + " |");
                }
                foreach (var a in art.Where(a => a.weaponId == w.weaponId))
                {
                    var prefab = catalog != null ? catalog.Get(w.weaponId, a.slot) : null;
                    sb.AppendLine("| " + a.slot + " | `" + (prefab != null ? AssetDatabase.GetAssetPath(prefab) : "(none)") + "` | art team: `" + a.asset + "`" + (a.child != null ? " (its " + a.child + " child)" : "") + " | " + ArtCode(a.slot) + " |");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        static string ArtCode(WeaponVisualSlot slot) => slot switch
        {
            WeaponVisualSlot.Lob => "thrown from the ship's rail (1.6 m up) to each shell's ring in an arc of 5 m (x tuning arc), landing as the shell lands; scale 1 (x tuning scale)",
            WeaponVisualSlot.Launch => "played at the ship's rail facing the target when a shell is thrown; scale 1; 1.2 s",
            WeaponVisualSlot.Whirlpool => "at the whirlpool's centre (0.12 m up) for its seconds; scale = whirlpool radius / baseRadius; spins 90 deg/s (shader _SpinOffset) and fades in and out (_Fade)",
            WeaponVisualSlot.Aura => "follows the ship (0.05 m up) while the weapon is equipped, from its first pulse; scale = pulse radius / baseRadius; turns 15 deg/s",
            _ => "as its slot",
        };
    }
}
