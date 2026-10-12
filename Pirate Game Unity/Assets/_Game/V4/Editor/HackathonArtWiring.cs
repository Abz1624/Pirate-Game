using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.VFX;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// Hackathon (12 Oct, owner: "based on it please update things"): wires the art team's gameplay-branch models into V4,
    /// presentation only, so it can be re-run after the artists change them. It builds art-only prefabs (no scripts but
    /// V4's own <see cref="ArtMotion"/>) under Assets/_Game/V4/Demo/Art and sets them in the juice settings:
    /// <list type="bullet">
    /// <item>enemy ships and the crowned elites (3D Models/Enemies): each model scaled to the footprint of the model its
    /// enemy's config draws now (length for ships, largest size for creatures), centred on it, the hull's keel at the old
    /// model's draft, facing +Z; the old model's wake trails and cannon smoke come along; the creatures get a swim
    /// controller (state "Armature_Swim", the clip the spawn variation phases);</item>
    /// <item>weapons (GameObject/Guns): the Anchor Flail's anchor and chain link, the chainshot, the ink pot, the
    /// lighthouse lantern, a parrot and the monkey buccaneer, cut from the art team's showcase prefabs.</item>
    /// </list>
    /// The art team's showcase prefabs carried V3-era scripts that were not brought over (project rule: no V3 code); their
    /// missing-script components are removed. The configs (and so the config hash) are not touched.
    /// </summary>
    public static class HackathonArtWiring
    {
        const string ArtFolder = "Assets/_Game/V4/Demo/Art";
        const string Enemies = "Assets/3D Models/Enemies";
        const string Guns = "Assets/GameObject/Guns";

        /// <summary>Which content id draws which model: Demo/Art/ArtTeamModels.txt (data, so V4 code holds no content ids).</summary>
        const string MappingFile = ArtFolder + "/ArtTeamModels.txt";

        static List<string[]> Mapping(string kind) =>
            System.IO.File.ReadAllLines(MappingFile)
                .Where(l => l.Length > 0 && !l.StartsWith("#"))
                .Select(l => l.Split(';'))
                .Where(f => f[0] == kind)
                .ToList();

        [MenuItem("Tools/Rogue Pirates/Demo/Wire Art Team Models (hackathon)")]
        public static void WireAll()
        {
            var log = new List<string>();
            Folder(ArtFolder);
            Folder(ArtFolder + "/Enemies");
            Folder(ArtFolder + "/Weapons");
            log.Add("Missing scripts removed from " + StripMissingScripts(Guns) + " art prefabs (V3-era VFX scripts, not imported)");
            var juice = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
            if (juice == null) { Debug.LogError("[HackathonArtWiring] no DemoJuiceSettings"); return; }
            var config = ConfigModels();
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var enemies = new List<ArtEnemyModel>();
                foreach (var f in Mapping("enemy"))
                {
                    var (id, model, creature, swim) = (f[1], f[2], f[3] == "creature", f.Length > 4 ? f[4] : null);
                    if (!config.TryGetValue(id, out var old) || old == null) { log.Add("Enemy " + id + ": no config model, skipped"); continue; }
                    var prefab = BuildEnemy(scene, id, model, old, creature, swim, log);
                    if (prefab != null) enemies.Add(new ArtEnemyModel { enemyId = id, model = prefab });
                }
                juice.enemyArtModels = enemies.ToArray();

                juice.maceArtModel = BuildAnchor(scene, log);
                juice.maceChainLinkModel = BuildChainLink(scene, log);
                juice.lanternArtModel = BuildLantern(scene, log);
                juice.parrotArtModel = BuildParrot(scene, log);
                juice.monkeyArtModel = BuildMonkey(scene, log);
                var weapons = new List<ArtWeaponModel>();
                var chainshot = BuildChainshot(scene, log);
                if (chainshot != null)
                    foreach (var f in Mapping("projectile")) weapons.Add(new ArtWeaponModel { weaponId = f[1], projectileModel = chainshot, projectileScale = 1.8f });
                var inkPot = BuildInkPot(scene, log);
                if (inkPot != null)
                    foreach (var f in Mapping("lob")) weapons.Add(new ArtWeaponModel { weaponId = f[1], lobModel = inkPot, lobScale = 1.6f, lobArcHeight = 5f, lobPrewarm = 8 });
                juice.weaponArtModels = weapons.ToArray();
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
            EditorUtility.SetDirty(juice);
            AssetDatabase.SaveAssets();
            Debug.Log("[HackathonArtWiring]\n" + string.Join("\n", log));
        }

        // ---- Enemies --------------------------------------------------------------------------------------------------

        /// <summary>Each enemy and variant id with the model its config draws now (the footprint to match).</summary>
        static Dictionary<string, GameObject> ConfigModels()
        {
            var result = new Dictionary<string, GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyArchetypeConfig"))
            {
                var a = AssetDatabase.LoadAssetAtPath<EnemyArchetypeConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null && !string.IsNullOrEmpty(a.enemyId)) result[a.enemyId] = EnemyStatResolver.Resolve(a).visualPrefab;
            }
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyVariantConfig"))
            {
                var v = AssetDatabase.LoadAssetAtPath<EnemyVariantConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (v != null && v.baseArchetype != null && !string.IsNullOrEmpty(v.variantId)) result[v.variantId] = EnemyStatResolver.Resolve(v).visualPrefab;
            }
            return result;
        }

        static GameObject BuildEnemy(UnityEngine.SceneManagement.Scene scene, string id, string modelPath, GameObject old, bool creature, string swimClip, List<string> log)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (source == null) { log.Add("Enemy " + id + ": missing " + modelPath); return null; }
            var oldCopy = Object.Instantiate(old);
            Place(scene, oldCopy);
            var oldBounds = Bounds(oldCopy.transform, oldCopy.transform);

            var root = new GameObject("Art_" + Pascal(id));
            Place(scene, root);
            // A model file stays linked (the artists' re-exports flow in); a prefab is copied (its physics parts go).
            var model = modelPath.EndsWith(".fbx") ? (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform) : null;
            if (model == null) model = Object.Instantiate(source, root.transform);
            model.name = source.name;
            StripRuntimeOnly(model);
            var b = Bounds(root.transform, model.transform);
            var ratio = creature
                ? Mathf.Max(oldBounds.size.x, oldBounds.size.y, oldBounds.size.z) / Mathf.Max(0.001f, Mathf.Max(b.size.x, b.size.y, b.size.z))
                : oldBounds.size.z / Mathf.Max(0.001f, b.size.z);
            model.transform.localScale *= ratio;
            model.transform.localPosition *= ratio;
            b = Bounds(root.transform, model.transform);
            var offset = new Vector3(oldBounds.center.x - b.center.x, creature ? oldBounds.center.y - b.center.y : oldBounds.min.y - b.min.y, oldBounds.center.z - b.center.z);
            model.transform.localPosition += offset;

            // The old model's wake (trails, foam) and cannon smoke, where they were on it (the presenters drive them).
            var hasOwnWake = model.GetComponentsInChildren<TrailRenderer>(true).Length > 0;
            var copied = 0;
            foreach (var t in oldCopy.GetComponentsInChildren<Transform>(true))
            {
                if (t == oldCopy.transform) continue;
                var trail = t.GetComponent<TrailRenderer>() != null;
                var smoke = t.name == "CannonSmoke" && t.GetComponent<ParticleSystem>() != null;
                if (!(trail && !hasOwnWake) && !smoke) continue;
                if (t.GetComponentsInChildren<MeshRenderer>(true).Length > 0 || t.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0) continue;
                var part = Object.Instantiate(t.gameObject, root.transform);
                part.name = t.name;
                part.transform.SetPositionAndRotation(t.position, t.rotation);
                part.transform.localScale = t.lossyScale;
                copied++;
            }

            // A trail without a material (the art team's CutthroatSkiff wake) takes the old model's wake material.
            var oldTrail = oldCopy.GetComponentsInChildren<TrailRenderer>(true).FirstOrDefault(t => t.sharedMaterial != null);
            foreach (var trail in root.GetComponentsInChildren<TrailRenderer>(true))
                if (trail.sharedMaterial == null && oldTrail != null)
                {
                    trail.sharedMaterial = oldTrail.sharedMaterial;
                    log.Add("Enemy " + id + ": " + trail.name + " had no material, given " + oldTrail.sharedMaterial.name + " (art team: please set one)");
                }

            if (!string.IsNullOrEmpty(swimClip)) AddSwim(model, id, modelPath, swimClip, log);
            var path = ArtFolder + "/Enemies/" + root.name + ".prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            var after = Bounds(root.transform, model.transform);
            log.Add($"Enemy {id}: {source.name} x{ratio:F2}, size {after.size:F2} (was {oldBounds.size:F2}, {old.name}), {copied} wake/smoke parts, {Tris(model)} tris -> {path}");
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(oldCopy);
            return saved;
        }

        /// <summary>A swim controller for a crowned creature: one looping state "Armature_Swim" with the model's swim clip.</summary>
        static void AddSwim(GameObject model, string id, string modelPath, string clipName, List<string> log)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            var changed = false;
            foreach (var c in clips)
                if (c.name == clipName && !c.loopTime) { c.loopTime = true; changed = true; }
            if (changed)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
            var clip = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().FirstOrDefault(c => c.name == clipName);
            if (clip == null) { log.Add("Enemy " + id + ": no clip " + clipName); return; }
            var path = ArtFolder + "/Enemies/Art_" + Pascal(id) + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path); // kept on a re-run (its GUID stays)
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            foreach (var old in controller.layers[0].stateMachine.states.ToArray()) controller.layers[0].stateMachine.RemoveState(old.state);
            var state = controller.layers[0].stateMachine.AddState("Armature_Swim");
            state.motion = clip;
            controller.layers[0].stateMachine.defaultState = state;
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }

        // ---- Weapons --------------------------------------------------------------------------------------------------

        /// <summary>The anchor (shank along Z, flukes at +Z), 1.9 m long, centred; ChainAttach at its ring.</summary>
        static GameObject BuildAnchor(UnityEngine.SceneManagement.Scene scene, List<string> log)
        {
            var src = Showcase(scene, Guns + "/Anchor Flail.prefab");
            if (src == null) return null;
            var anchor = src.transform.Find("Anchor_BowStarboard/Anchor");
            var root = new GameObject("Art_AnchorFlail_Anchor");
            Place(scene, root);
            var body = new GameObject("Anchor").transform;
            body.SetParent(root.transform, false);
            foreach (var name in new[] { "Model", "Glow" })
            {
                var part = anchor.Find(name);
                if (part == null) continue;
                var copy = Object.Instantiate(part.gameObject, body);
                copy.name = name;
                copy.transform.localPosition = part.localPosition;
                copy.transform.localRotation = part.localRotation;
                copy.transform.localScale = part.localScale;
            }
            var attachLocal = anchor.Find("ChainAttach") != null ? anchor.Find("ChainAttach").localPosition : new Vector3(0f, 0f, -2.6f);
            Normalize(root.transform, body, 1.9f, Axis.Z, true);
            var attach = new GameObject("ChainAttach").transform;
            attach.SetParent(root.transform, false);
            attach.position = body.TransformPoint(attachLocal);
            Object.DestroyImmediate(src);
            return Save(root, "Weapons/Art_AnchorFlail_Anchor", log);
        }

        /// <summary>One chain link, unit length along +Z, centred (the mace chain lays them instanced).</summary>
        static GameObject BuildChainLink(UnityEngine.SceneManagement.Scene scene, List<string> log)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Guns + "/AnchorFlail/Meshes/AnchorFlail_ChainLink.asset");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(Guns + "/AnchorFlail/Materials/AnchorFlail_Chain.mat");
            if (mesh == null || mat == null) { log.Add("Chain link: missing mesh or material"); return null; }
            var root = new GameObject("Art_AnchorFlail_ChainLink");
            Place(scene, root);
            var link = new GameObject("Link");
            link.transform.SetParent(root.transform, false);
            link.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = link.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // The link's long side along Z.
            var s = mesh.bounds.size;
            if (s.x >= s.y && s.x >= s.z) link.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            else if (s.y >= s.x && s.y >= s.z) link.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Normalize(root.transform, link.transform, 1f, Axis.Z, true);
            return Save(root, "Weapons/Art_AnchorFlail_ChainLink", log);
        }

        /// <summary>The chainshot in flight (two spiked balls on a chain, its swirl), 1.5 m across, spinning.</summary>
        static GameObject BuildChainshot(UnityEngine.SceneManagement.Scene scene, List<string> log)
        {
            var src = Showcase(scene, Guns + "/Chainshot.prefab");
            if (src == null) return null;
            var root = new GameObject("Art_Chainshot");
            Place(scene, root);
            var projectile = Object.Instantiate(src.transform.Find("Projectile").gameObject, root.transform);
            projectile.name = "Projectile";
            projectile.transform.localPosition = Vector3.zero;
            var spin = projectile.transform.Find("SpinPlane/Spin");
            if (spin != null) Motion(spin.gameObject, ArtMotion.Mode.Spin, Vector3.up, 900f, 0f, 0f);
            Normalize(root.transform, projectile.transform, 1.5f, Axis.Max, true);
            Object.DestroyImmediate(src);
            return Save(root, "Weapons/Art_Chainshot", log);
        }

        /// <summary>The ink pot (its glass shards, cork and ink), 1 m tall, centred, tumbling.</summary>
        static GameObject BuildInkPot(UnityEngine.SceneManagement.Scene scene, List<string> log)
        {
            var src = Showcase(scene, Guns + "/Ink Pot.prefab");
            if (src == null) return null;
            var root = new GameObject("Art_InkPot");
            Place(scene, root);
            var pot = Object.Instantiate(src.transform.Find("Shot/Pot").gameObject, root.transform);
            pot.name = "Pot";
            pot.SetActive(true);
            var spill = pot.transform.Find("InkSpill");
            if (spill != null) spill.gameObject.SetActive(true);
            var tumble = new GameObject("Tumble").transform;
            tumble.SetParent(root.transform, false);
            pot.transform.SetParent(tumble, false);
            Normalize(root.transform, pot.transform, 1f, Axis.Y, true);
            Motion(tumble.gameObject, ArtMotion.Mode.Spin, new Vector3(1f, 0.25f, 0.4f).normalized, 420f, 0f, 0f);
            Object.DestroyImmediate(src);
            return Save(root, "Weapons/Art_InkPot", log);
        }

        /// <summary>The lighthouse lantern (post, collar, iron head, glass and its glow), 1.2 m tall, standing on y 0.</summary>
        static GameObject BuildLantern(UnityEngine.SceneManagement.Scene scene, List<string> log)
        {
            var src = Showcase(scene, Guns + "/Lighthouse Lantern.prefab");
            if (src == null) return null;
            var root = new GameObject("Art_LighthouseLantern");
            Place(scene, root);
            var body = new GameObject("Lantern").transform;
            body.SetParent(root.transform, false);
            foreach (var name in new[] { "Mount", "Head" })
            {
                var part = src.transform.Find(name);
                if (part != null) part.SetParent(body, true); // bakes the showcase root's scale
            }
            Normalize(root.transform, body, 1.2f, Axis.Y, false);
            Object.DestroyImmediate(src);
            return Save(root, "Weapons/Art_LighthouseLantern", log);
        }

        /// <summary>One parrot of the squadron (body, wings, straps, its little cannon), 2.6 m across at 1.1 m, flapping.</summary>
        static GameObject BuildParrot(UnityEngine.SceneManagement.Scene scene, List<string> log)
        {
            var src = Showcase(scene, Guns + "/Parrot Squadron.prefab");
            if (src == null) return null;
            var root = new GameObject("Art_ParrotSquadron_Parrot");
            Place(scene, root);
            var bird = new GameObject("Bird").transform;
            bird.SetParent(root.transform, false);
            var model = Object.Instantiate(src.transform.Find("Parrot_0/Model").gameObject, bird);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            foreach (var (wing, sign) in new[] { ("WingL", 1f), ("WingR", -1f) })
            {
                var w = model.transform.Find(wing);
                if (w != null) Motion(w.gameObject, ArtMotion.Mode.Swing, Vector3.forward, 32f * sign, 3.2f, 0f);
            }
            Normalize(root.transform, bird, 2.6f, Axis.Max, true);
            bird.localPosition += Vector3.up * 1.1f;
            Object.DestroyImmediate(src);
            return Save(root, "Weapons/Art_ParrotSquadron_Parrot", log);
        }

        /// <summary>The monkey buccaneer, 1.3 m tall, standing on y 0.</summary>
        static GameObject BuildMonkey(UnityEngine.SceneManagement.Scene scene, List<string> log)
        {
            var src = Showcase(scene, Guns + "/Monkey Buccaneer.prefab");
            if (src == null) return null;
            var root = new GameObject("Art_MonkeyBuccaneer");
            Place(scene, root);
            var body = new GameObject("Monkey").transform;
            body.SetParent(root.transform, false);
            var model = src.transform.Find("Visuals/Model");
            if (model != null) model.SetParent(body, true);
            Normalize(root.transform, body, 1.3f, Axis.Y, false);
            Object.DestroyImmediate(src);
            return Save(root, "Weapons/Art_MonkeyBuccaneer", log);
        }

        // ---- Helpers --------------------------------------------------------------------------------------------------

        enum Axis { X, Y, Z, Max }

        /// <summary>Scales <paramref name="part"/> so the model's size along <paramref name="axis"/> is
        /// <paramref name="size"/>, then centres it on the root (or stands it on y 0 when not <paramref name="centre"/>).</summary>
        static void Normalize(Transform root, Transform part, float size, Axis axis, bool centre)
        {
            var b = Bounds(root, part);
            var current = axis == Axis.X ? b.size.x : axis == Axis.Y ? b.size.y : axis == Axis.Z ? b.size.z : Mathf.Max(b.size.x, b.size.y, b.size.z);
            var k = size / Mathf.Max(0.0001f, current);
            part.localScale *= k;
            part.localPosition *= k;
            b = Bounds(root, part);
            part.localPosition += new Vector3(-b.center.x, centre ? -b.center.y : -b.min.y, -b.center.z);
        }

        /// <summary>The renderers' bounds (no particles, trails or lines) in <paramref name="space"/>'s space.</summary>
        static Bounds Bounds(Transform space, Transform part)
        {
            var found = false;
            var b = new Bounds();
            foreach (var r in part.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || !r.gameObject.activeInHierarchy && !r.gameObject.activeSelf) continue;
                Bounds mb;
                Matrix4x4 m;
                if (r is SkinnedMeshRenderer)
                {
                    // Skinned: the posed world bounds (the mesh's own bounds ignore the bones' scale).
                    mb = r.bounds;
                    m = space.worldToLocalMatrix;
                }
                else
                {
                    var filter = r.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    mb = filter.sharedMesh.bounds;
                    m = space.worldToLocalMatrix * r.transform.localToWorldMatrix;
                }
                for (var i = 0; i < 8; i++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(c);
                    if (!found) { b = new Bounds(p, Vector3.zero); found = true; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        static int Tris(GameObject go)
        {
            var n = 0;
            foreach (var f in go.GetComponentsInChildren<MeshFilter>(true)) if (f.sharedMesh != null) n += (int)f.sharedMesh.GetIndexCount(0) / 3 * Mathf.Max(1, f.sharedMesh.subMeshCount);
            foreach (var s in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (s.sharedMesh != null) n += s.sharedMesh.triangles.Length / 3;
            return n;
        }

        static GameObject Showcase(UnityEngine.SceneManagement.Scene scene, string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) { Debug.LogWarning("[HackathonArtWiring] missing " + path); return null; }
            var copy = Object.Instantiate(asset);
            Place(scene, copy);
            copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            StripRuntimeOnly(copy);
            return copy;
        }

        /// <summary>No missing scripts, colliders, rigidbodies or NavMesh parts: the views are drawn only.</summary>
        static void StripRuntimeOnly(GameObject root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var b in root.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(b);
            foreach (var c in root.GetComponentsInChildren<Component>(true))
                if (c != null && (c.GetType().Name == "NavMeshAgent" || c.GetType().Name == "NavMeshObstacle")) Object.DestroyImmediate(c);
        }

        static void Motion(GameObject go, ArtMotion.Mode mode, Vector3 axis, float degrees, float frequency, float phase)
        {
            var m = go.GetComponent<ArtMotion>();
            if (m == null) m = go.AddComponent<ArtMotion>();
            m.mode = mode;
            m.axis = axis;
            m.degrees = degrees;
            m.frequency = frequency;
            m.phase = phase;
        }

        static GameObject Save(GameObject root, string name, List<string> log)
        {
            StripRuntimeOnly(root);
            var path = ArtFolder + "/" + name + ".prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            var b = Bounds(root.transform, root.transform);
            log.Add($"Weapon art {root.name}: size {b.size:F2}, {Tris(root)} tris -> {path}");
            Object.DestroyImmediate(root);
            return saved;
        }

        /// <summary>Removes the missing-script components from every prefab under a folder; returns how many changed.</summary>
        static int StripMissingScripts(string folder)
        {
            var changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) continue;
                var missing = asset.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                if (missing == 0) continue;
                var contents = PrefabUtility.LoadPrefabContents(path);
                foreach (var t in contents.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                PrefabUtility.UnloadPrefabContents(contents);
                changed++;
            }
            return changed;
        }

        static void Place(UnityEngine.SceneManagement.Scene scene, GameObject go) => UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);

        static string Pascal(string id) => string.Concat(id.Split('_').Select(w => w.Length > 0 ? char.ToUpperInvariant(w[0]) + w.Substring(1) : w));

        // ---- Showcase scene ---------------------------------------------------------------------------------------------

        public const string ShowcaseScene = "Assets/_Game/Scenes/V4/Hackathon_ArtShowcase.unity";

        /// <summary>A scene (not in the build) laying out every wired art model with a label: the ships and crowned
        /// elites as each enemy draws them (the old model beside each for scale), the weapon models, and the point-of-interest
        /// art the POI prefabs use. Open it and press Play to see the swims, spins and flaps.</summary>
        [MenuItem("Tools/Rogue Pirates/Demo/Build Art Showcase Scene (hackathon)")]
        public static void BuildShowcase()
        {
            var juice = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
            if (juice == null) return;
            var config = ConfigModels();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Water";
            water.transform.localScale = new Vector3(14f, 1f, 12f);
            water.transform.position = new Vector3(30f, 0f, 10f);
            var waterMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.12f, 0.42f, 0.66f) };
            AssetDatabase.CreateAsset(waterMat, ArtFolder + "/Showcase_Water.mat");
            water.GetComponent<Renderer>().sharedMaterial = waterMat;

            var row = 0;
            void Row(string title, IEnumerable<(string label, GameObject prefab, float scale)> items, float spacing)
            {
                var z = row * 12f;
                Label(title, new Vector3(-6f, 0.2f, z + 4.5f), 1.2f, Color.yellow);
                var x = 0f;
                foreach (var (label, prefab, scale) in items)
                {
                    if (prefab == null) continue;
                    // An old model is a plain copy without its physics and NavMesh parts (no agent warnings on Play).
                    var old = label.StartsWith("was:");
                    var go = old ? Object.Instantiate(prefab) : (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    if (old) { go.name = prefab.name + " (old)"; StripRuntimeOnly(go); }
                    go.transform.position = new Vector3(x, 0f, z);
                    go.transform.localScale = Vector3.one * scale;
                    Label(label, new Vector3(x, 0.1f, z - 3.2f), 0.9f, Color.white);
                    x += spacing;
                }
                row++;
            }
            const float shipScale = 1.3f;
            var ships = juice.enemyArtModels.Where(e => !e.enemyId.StartsWith("elite")).ToList();
            Row("Enemy ships (art team)", ships.Take(7).Select(e => (e.enemyId, e.model, shipScale)), 8f);
            Row("Enemy ships (art team)", ships.Skip(7).Select(e => (e.enemyId, e.model, shipScale)), 8f);
            Row("Old models they replace", ships.Take(7).Select(e => ("was: " + (config.TryGetValue(e.enemyId, out var m) && m != null ? m.name : "?"), config.TryGetValue(e.enemyId, out var m2) ? m2 : null, shipScale)), 8f);
            Row("Crowned elites (art team)", juice.enemyArtModels.Where(e => e.enemyId.StartsWith("elite")).Select(e => (e.enemyId, e.model, 0.9f))
                .Concat(juice.enemyArtModels.Where(e => e.enemyId.StartsWith("elite")).Select(e => ("was: " + (config.TryGetValue(e.enemyId, out var m) && m != null ? m.name : "?"), config.TryGetValue(e.enemyId, out var m2) ? m2 : null, 0.9f))), 9f);
            Row("Weapons (art team)", new (string, GameObject, float)[]
            {
                ("Anchor Flail anchor", juice.maceArtModel, 1.5f),
                ("Anchor Flail chain link", juice.maceChainLinkModel, 1.5f),
                ("Chainshot", juice.weaponArtModels.FirstOrDefault(w => w.projectileModel != null)?.projectileModel, 1.8f),
                ("Squid Ink Pot", juice.weaponArtModels.FirstOrDefault(w => w.lobModel != null)?.lobModel, 1.6f),
                ("Lighthouse Lantern", juice.lanternArtModel, 1.5f),
                ("Parrot Squadron", juice.parrotArtModel, 1f),
                ("Monkey Gunners", juice.monkeyArtModel, 1.5f),
            }, 6f);
            Row("Points of interest (art team; already wired, updated from gameplay)", new (string, GameObject, float)[]
            {
                ("Salvage Chest", juice.salvageChestPrefab, 1f),
                ("Repair Wreck", juice.gildedWreckPrefab, 1f),
                ("Treasure Chart", juice.cursedIdolPrefab, 1f),
                ("Powder Cache (tombstone)", juice.merchantSkiffPrefab, 1f),
                ("fish bell (not used yet)", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/fish_bell.prefab"), 1f),
            }, 8f);

            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(22f, 30f, -20f);
                cam.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
                cam.farClipPlane = 300f;
            }
            EditorSceneManager.SaveScene(scene, ShowcaseScene);
            Debug.Log("[HackathonArtWiring] showcase saved: " + ShowcaseScene);
        }

        static void Label(string text, Vector3 position, float size, Color color)
        {
            var go = new GameObject("Label " + text);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var t = go.AddComponent<TextMesh>();
            t.text = text;
            t.characterSize = size * 0.25f;
            t.fontSize = 40;
            t.anchor = color == Color.yellow ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            t.color = color;
        }

        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = path.Substring(0, path.LastIndexOf('/'));
            Folder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
    }
}
