using System.Collections.Generic;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Presentation.Effects;
using RoguePirates.V4.Presentation.Weapons;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// Hackathon (13 Oct, owner: tweak every weapon's look in the Editor with real prefabs): saves the weapon visuals that
    /// were built in code at run time as prefabs (with their materials and textures as assets), exactly as the code built
    /// them, under Assets/_Game/V4/Demo/Art/WeaponPrefabs/&lt;weapon_id&gt;/, and links them in the weapon prefab library
    /// (WeaponPrefabs/Resources/V4WeaponPrefabs.asset), which the presenters pool and prewarm. A prefab already on disk is
    /// kept (the owner's edits stay); the Reset menu rebuilds them all from code. Presentation only: no config changes.
    /// The full per-weapon list of prefabs is plan/hackathon/WEAPON-PREFABS.md.
    /// </summary>
    public static class HackathonWeaponPrefabs
    {
        public const string Folder = "Assets/_Game/V4/Demo/Art/WeaponPrefabs";
        public const string SharedFolder = Folder + "/_Shared";
        public const string LibraryPath = Folder + "/Resources/" + WeaponPrefabLibrary.ResourcePath + ".asset";

        [MenuItem("Tools/Rogue Pirates/Demo/Build Weapon Prefabs (hackathon)")]
        public static void BuildMissing() => Build(false);

        [MenuItem("Tools/Rogue Pirates/Demo/Reset Weapon Prefabs To Code Look (hackathon)")]
        public static void ResetAll()
        {
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Reset weapon prefabs",
                    "Rebuild every prefab under " + Folder + " from the code-built look? Edits to them are lost.", "Reset", "Cancel")) return;
            Build(true);
        }

        /// <summary>Builds the prefabs (missing ones, or all with <paramref name="overwrite"/>) and links the library;
        /// returns a log line per prefab.</summary>
        public static List<string> Build(bool overwrite)
        {
            var log = new List<string>();
            EnsureFolder(SharedFolder);
            EnsureFolder(Folder + "/Resources");

            var library = AssetDatabase.LoadAssetAtPath<WeaponPrefabLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<WeaponPrefabLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            var entries = new List<WeaponPrefabEntry>(); // entries added by hand stay
            if (library.weapons != null) foreach (var e in library.weapons) if (e != null) entries.Add(e);
            WeaponPrefabEntry Entry(string id)
            {
                foreach (var e in entries) if (e.weaponId == id) return e;
                var entry = new WeaponPrefabEntry { weaponId = id };
                entries.Add(entry);
                return entry;
            }

            // The summons' shots: the coloured capsules WeaponPlaceholderModels built.
            Entry("monkey_gunners").projectile = PlaceholderShot("monkey_gunners", WeaponPlaceholderLook.MonkeyGunners, overwrite, log);
            Entry("parrot_squadron").projectile = PlaceholderShot("parrot_squadron", WeaponPlaceholderLook.Parrots, overwrite, log);
            // The pulse rings: the Siren Bell's aura, the Squid Ink Pots' landing and slick ticks.
            Entry("siren_bell").pulseRing = PulseRing("siren_bell", overwrite, log);
            Entry("squid_ink_pots").pulseRing = PulseRing("squid_ink_pots", overwrite, log);
            // The Anchor Flail's chain line (the curve its links are laid along).
            Entry("anchor_flail").chain = Chain("anchor_flail", overwrite, log);

            library.weapons = entries.ToArray();
            library.landingRing = LandingRing(overwrite, log);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            foreach (var line in log) Debug.Log("[WeaponPrefabs] " + line);
            return log;
        }

        /// <summary>Every weapon's visuals as Markdown (role, prefab path, what code overrides), from the weapon visual
        /// catalog (the source of the tables in plan/hackathon/WEAPON-PREFABS.md).</summary>
        public static string ListMarkdown()
        {
            var settings = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
            var library = AssetDatabase.LoadAssetAtPath<WeaponPrefabLibrary>(LibraryPath);
            var sb = new System.Text.StringBuilder();
            var weapons = new List<WeaponConfig>();
            foreach (var guid in AssetDatabase.FindAssets("t:WeaponConfig", new[] { "Assets/_Game/V4/Data/Weapons" }))
            {
                var w = AssetDatabase.LoadAssetAtPath<WeaponConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (w != null && !string.IsNullOrEmpty(w.weaponId)) weapons.Add(w);
            }
            weapons.Sort((a, b) => string.CompareOrdinal(a.weaponId, b.weaponId));
            foreach (var w in weapons)
            {
                sb.AppendLine("### " + w.weaponId + " (" + w.displayName + ", " + w.behavior + ")").AppendLine();
                sb.AppendLine("| Visual | Prefab | Code sets at run time |").AppendLine("|---|---|---|");
                foreach (var e in WeaponVisualCatalog.Of(w, settings, library))
                {
                    var path = e.Prefab != null ? AssetDatabase.GetAssetPath(e.Prefab) : "(built in code)";
                    sb.AppendLine("| " + e.Role + " | `" + path + "`" + (e.Shared ? " (shared)" : "") + " | " + e.Overrides + " |");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        static string WeaponFolder(string id)
        {
            var folder = Folder + "/" + id;
            EnsureFolder(folder);
            return folder;
        }

        static GameObject Existing(string path, bool overwrite, List<string> log)
        {
            if (overwrite) return null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) log.Add("kept " + path);
            return prefab;
        }

        static GameObject PlaceholderShot(string id, WeaponPlaceholderLook look, bool overwrite, List<string> log)
        {
            var folder = WeaponFolder(id);
            var path = folder + "/" + id + "_Projectile.prefab";
            var existing = Existing(path, overwrite, log);
            if (existing != null) return existing;

            var template = WeaponPlaceholderModels.Projectile(look, null);
            var root = Object.Instantiate(template);
            root.name = id + "_Projectile";
            root.SetActive(true);
            var n = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.hideFlags = HideFlags.None;
                var renderer = t.GetComponent<Renderer>();
                if (renderer == null || renderer.sharedMaterial == null) continue;
                var material = SaveMaterial(new Material(renderer.sharedMaterial), folder + "/" + id + "_Projectile" + (n++ == 0 ? "" : "_" + n) + ".mat", renderer.sharedMaterial);
                renderer.sharedMaterial = material;
            }
            return SavePrefab(root, path, log);
        }

        static GameObject PulseRing(string id, bool overwrite, List<string> log)
        {
            var folder = WeaponFolder(id);
            var path = folder + "/" + id + "_Pulse.prefab";
            var existing = Existing(path, overwrite, log);
            if (existing != null) return existing;

            var root = new GameObject(id + "_Pulse");
            var line = root.AddComponent<LineRenderer>();
            WeaponVfxPresenter.ConfigurePulseLine(line);
            line.sharedMaterial = SaveMaterial(new Material(Shader.Find("Sprites/Default")), folder + "/" + id + "_Pulse.mat", null);
            return SavePrefab(root, path, log);
        }

        static GameObject Chain(string id, bool overwrite, List<string> log)
        {
            var folder = WeaponFolder(id);
            var path = folder + "/" + id + "_Chain.prefab";
            var existing = Existing(path, overwrite, log);
            if (existing != null) return existing;

            // The link texture as a PNG (point for point what the code generates), imported uncompressed.
            var texturePath = folder + "/" + id + "_ChainLinks.png";
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

            var root = new GameObject(id + "_Chain");
            var line = root.AddComponent<LineRenderer>();
            WeaponVfxPresenter.ConfigureChainLine(line);
            var material = new Material(Shader.Find("Sprites/Default")) { mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath) };
            line.sharedMaterial = SaveMaterial(material, folder + "/" + id + "_Chain.mat", null);
            return SavePrefab(root, path, log);
        }

        static GameObject LandingRing(bool overwrite, List<string> log)
        {
            var path = SharedFolder + "/WeaponLandingRing.prefab";
            var existing = Existing(path, overwrite, log);
            if (existing != null) return existing;

            var root = new GameObject("WeaponLandingRing");
            var line = root.AddComponent<LineRenderer>();
            HazardRingPresenter.ConfigureRingLine(line);
            line.sharedMaterial = SaveMaterial(new Material(Shader.Find("Sprites/Default")), SharedFolder + "/WeaponLandingRing.mat", null);
            return SavePrefab(root, path, log);
        }

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

        static GameObject SavePrefab(GameObject root, string path, List<string> log)
        {
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            log.Add("built " + path);
            return saved;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
    }
}
