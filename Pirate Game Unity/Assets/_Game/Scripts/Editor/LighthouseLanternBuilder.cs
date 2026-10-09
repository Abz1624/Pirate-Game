using System.Collections.Generic;
using HighSeas.Combat;
using HighSeas.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HighSeas.EditorTools
{
    /// <summary>
    /// Generates the Lighthouse Lantern art (stern lantern meshes, warm-light materials, burn VFX)
    /// and assembles the drag-and-drop prefab + a preview scene with three dummies. Re-run any time.
    /// </summary>
    public static class LighthouseLanternBuilder
    {
        const string Root = "Assets/GameObject/Guns/LighthouseLantern";
        const string MeshDir = Root + "/Meshes";
        const string MatDir = Root + "/Materials";
        const string ShaderName = "RoguePirates/LighthouseBeam";
        public const string PrefabPath = "Assets/GameObject/Guns/Lighthouse Lantern.prefab";
        public const string DemoScenePath = Root + "/LighthouseLantern_Demo.unity";
        const string BrigPrefabPath = "Assets/GameObject/Brigg.prefab";

        // ---- placement relative to the Brigg root (stern quarterdeck) ----
        static readonly Vector3 MountLocalPos = new(0f, 5.2f, -0.15f); // mast top (crow's nest)
        const float MountScale = 0.55f; // mast-top mount: smaller so the lantern doesn't dwarf the ship
        const float HeadScale = 1.7f;
        const float HeadBaseY = 0.59f;  // top of the mount (mount space)
        const float GlassCenterY = 0.33f; // head space
        const float LensZ = 0.23f;

        // ---- palette (warm, controlled) ----
        static readonly Color Wood = new(0.40f, 0.25f, 0.14f);
        static readonly Color Iron = new(0.17f, 0.14f, 0.13f);
        static readonly Color Frame = new(0.95f, 0.48f, 0.14f);
        static readonly Color FrameEmission = new(0.6f, 0.22f, 0.03f);
        static readonly Color GlassBase = new(0.35f, 0.15f, 0.03f);
        static readonly Color GlassEmission = new(1f, 0.76f, 0.32f); // unlit glass colour
        // lens removed: the hexagonal lantern glows on every side
        static readonly Color Ember = new(1f, 0.55f, 0.16f);

[MenuItem("Tools/Rogue Pirates/Lighthouse Lantern/Build Prefab")]
        public static void BuildPrefab()
        {
            EnsureFolders();

            var mountMesh = SaveMesh(BuildMountMesh(), "LighthouseLantern_Mount");
            var collarMesh = SaveMesh(BuildCollarMesh(), "LighthouseLantern_Collar");
            var frameMesh = SaveMesh(BuildFrameMesh(), "LighthouseLantern_Frame");
            var glassMesh = SaveMesh(BuildGlassMesh(), "LighthouseLantern_Glass");

            var woodMat = LitMaterial("LighthouseLantern_Wood", Wood, 0f, 0.3f, Color.black);
            var ironMat = LitMaterial("LighthouseLantern_Iron", Iron, 0.55f, 0.45f, Color.black);
            var frameMat = LitMaterial("LighthouseLantern_Frame", Frame, 0.2f, 0.4f, FrameEmission);
            var glassMat = UnlitMaterial("LighthouseLantern_Glass", GlassEmission);

            var beamMat = BeamMaterial("LighthouseLantern_Beam", Color.white, 1f, 0.15f, BlendMode.OneMinusSrcAlpha, 3002);
            var trailMat = BeamMaterial("LighthouseLantern_Trail", new Color(1f, 0.62f, 0.22f, 1f), 1.5f, 0f, BlendMode.OneMinusSrcAlpha, 3001);
            var sweepMat = BeamMaterial("LighthouseLantern_Sweep", Color.white, 1f, 0f, BlendMode.OneMinusSrcAlpha, 3000);
            beamMat.SetFloat("_ZTest", (float)CompareFunction.Always);   // beam draws over the hull, like a light shaft
            trailMat.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
            sweepMat.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
            var glowMat = AdditiveParticleMaterial("LighthouseLantern_Glow", "Assets/Texture2D/glow.png", new Color(1f, 0.62f, 0.25f, 0.55f));
            var puffMat = AdditiveParticleMaterial("LighthouseLantern_BurnPuff", "Assets/Texture2D/glow.png", new Color(1f, 0.6f, 0.25f, 0.8f));
            var ringMat = AdditiveParticleMaterial("LighthouseLantern_BurnRing", "Assets/_Game/VFX/Healing/Textures/T_Heal_Ring.png", new Color(1f, 0.6f, 0.25f, 0.55f));

            var root = new GameObject("Lighthouse Lantern");
            try
            {
                var vfx = root.AddComponent<LighthouseLanternVfx>();

                var mount = new GameObject("Mount").transform;
                mount.SetParent(root.transform, false);
                mount.localPosition = MountLocalPos;
                mount.localScale = Vector3.one * MountScale;
                AddRenderer(mount, "Post", mountMesh, woodMat, true);
                AddRenderer(mount, "Collar", collarMesh, ironMat, true);

                var head = new GameObject("Head").transform;
                head.SetParent(root.transform, false);
                head.localPosition = MountLocalPos + Vector3.up * (HeadBaseY * MountScale);
                head.localScale = Vector3.one * HeadScale;
                AddRenderer(head, "Frame", frameMesh, frameMat, true);
                var glass = AddRenderer(head, "Glass", glassMesh, glassMat, false);

                var source = new GameObject("LightSource").transform;
                source.SetParent(head, false);
                source.localPosition = new Vector3(0f, GlassCenterY, LensZ);

                BuildGlow(head, glowMat);
                var puff = BuildBurnPuff(root.transform, puffMat);
                var ring = BuildBurnRing(root.transform, ringMat);

                var so = new SerializedObject(vfx);
                so.FindProperty("lanternHead").objectReferenceValue = head;
                so.FindProperty("lightSource").objectReferenceValue = source;
                so.FindProperty("glassRenderer").objectReferenceValue = glass;
                so.FindProperty("beamMaterial").objectReferenceValue = beamMat;
                so.FindProperty("trailMaterial").objectReferenceValue = trailMat;
                so.FindProperty("sweepMaterial").objectReferenceValue = sweepMat;
                so.FindProperty("burnPuff").objectReferenceValue = puff;
                so.FindProperty("burnRing").objectReferenceValue = ring;
                so.FindProperty("range").floatValue = 9f;
                so.FindProperty("burnInterval").floatValue = 0.5f;
                // beam shape and colour live on the component; defaults are used
                so.FindProperty("glassEmission").colorValue = GlassEmission;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[LighthouseLantern] Prefab built at {PrefabPath}");
        }

        [MenuItem("Tools/Rogue Pirates/Lighthouse Lantern/Build Demo Scene")]
        public static void BuildDemoScene()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                BuildPrefab();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 45f;
            cam.farClipPlane = 300f;
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(-1.5f, 25f, -15f);
            camGo.transform.LookAt(new Vector3(-1.5f, 0f, 0.5f));

            var waterMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/GameObject/Guns/AnchorFlail/Materials/AnchorFlail_DemoWater.mat");
            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Water";
            water.transform.localScale = new Vector3(12f, 1f, 12f);
            Object.DestroyImmediate(water.GetComponent<Collider>());
            if (waterMat) water.GetComponent<Renderer>().sharedMaterial = waterMat;

            var brigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BrigPrefabPath);
            GameObject brig = null;
            if (brigPrefab)
            {
                brig = (GameObject)PrefabUtility.InstantiatePrefab(brigPrefab, scene);
                brig.name = "Brigg (preview)";
                brig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 90f, 0f));
                foreach (var mb in brig.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
                var rb = brig.GetComponent<Rigidbody>();
                if (rb) rb.isKinematic = true;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var weapon = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (brig) weapon.transform.SetParent(brig.transform, false);

            // No dummies: drop tagged "Enemy" objects into the scene to test burns.
            
            
            
            
            

            EditorSceneManager.SaveScene(scene, DemoScenePath);
            Debug.Log($"[LighthouseLantern] Demo scene saved at {DemoScenePath}");
        }



        // =====================================================================================
        // Lantern meshes (head faces +Z, origin at the bottom of each part)
        // =====================================================================================

        static Mesh BuildMountMesh()
        {
            var mb = new MeshBuilder();
            mb.Box(new Vector3(0f, 0.09f, 0f), new Vector3(0.7f, 0.18f, 0.7f), Quaternion.identity);
            mb.Prism(Matrix4x4.Translate(new Vector3(0f, 0.18f, 0f)), 0.13f, 0.35f, 8);
            return mb.ToMesh("LighthouseLantern_Mount");
        }

        static Mesh BuildCollarMesh()
        {
            var mb = new MeshBuilder();
            mb.Prism(Matrix4x4.Translate(new Vector3(0f, 0.53f, 0f)), 0.2f, 0.06f, 8);
            // corner bolts on the deck block
            for (var i = 0; i < 4; i++)
            {
                var a = (45f + i * 90f) * Mathf.Deg2Rad;
                mb.Prism(Matrix4x4.Translate(new Vector3(Mathf.Sin(a) * 0.4f, 0.18f, Mathf.Cos(a) * 0.4f)), 0.045f, 0.03f, 6);
            }
            return mb.ToMesh("LighthouseLantern_Collar");
        }

/// <summary>Hexagonal paned lantern frame: base, corner posts, window mullions, roof band with small windows, roof, hook.</summary>
        static Mesh BuildFrameMesh()
        {
            var mb = new MeshBuilder();
            const float r = 0.27f;
            const float y0 = 0.07f, y1 = 0.59f;
            var mid = (y0 + y1) * 0.5f;
            var h = y1 - y0;

            mb.Prism(Matrix4x4.Translate(new Vector3(0f, -0.04f, 0f)), 0.2f, 0.04f, 6);
            mb.Prism(Matrix4x4.identity, 0.32f, y0, 6);
            for (var i = 0; i < 6; i++)
            {
                var corner = 30f + i * 60f;
                var ca = corner * Mathf.Deg2Rad;
                mb.Box(new Vector3(Mathf.Sin(ca) * r, mid, Mathf.Cos(ca) * r), new Vector3(0.05f, h, 0.05f), Quaternion.Euler(0f, corner, 0f));

                var face = i * 60f;
                var fa = face * Mathf.Deg2Rad;
                var ap = 0.255f * 0.866f + 0.006f;
                var fc = new Vector3(Mathf.Sin(fa) * ap, 0f, Mathf.Cos(fa) * ap);
                var rot = Quaternion.Euler(0f, face, 0f);
                mb.Box(fc + Vector3.up * mid, new Vector3(0.03f, h, 0.03f), rot);
                mb.Box(fc + Vector3.up * (y0 + h * 0.68f), new Vector3(r, 0.03f, 0.03f), rot);
            }
            mb.Prism(Matrix4x4.Translate(new Vector3(0f, y1, 0f)), 0.33f, 0.06f, 6);

            const float b0 = 0.65f, b1 = 0.78f;
            for (var i = 0; i < 6; i++)
            {
                var corner = 30f + i * 60f;
                var ca = corner * Mathf.Deg2Rad;
                mb.Box(new Vector3(Mathf.Sin(ca) * 0.235f, (b0 + b1) * 0.5f, Mathf.Cos(ca) * 0.235f), new Vector3(0.04f, b1 - b0, 0.04f), Quaternion.Euler(0f, corner, 0f));
                var face = i * 60f;
                var fa = face * Mathf.Deg2Rad;
                var ap = 0.22f * 0.866f + 0.005f;
                mb.Box(new Vector3(Mathf.Sin(fa) * ap, (b0 + b1) * 0.5f, Mathf.Cos(fa) * ap), new Vector3(0.025f, b1 - b0, 0.025f), Quaternion.Euler(0f, face, 0f));
            }
            mb.Prism(Matrix4x4.Translate(new Vector3(0f, b1, 0f)), 0.26f, 0.035f, 6);
            mb.Cone(Matrix4x4.Translate(new Vector3(0f, b1 + 0.035f, 0f)), 0.3f, 0.22f, 6);
            mb.Prism(Matrix4x4.Translate(new Vector3(0f, 1.02f, 0f)), 0.05f, 0.08f, 6);

            const int loopSegs = 10;
            var loopC = new Vector3(0f, 1.17f, 0f);
            for (var i = 0; i < loopSegs; i++)
            {
                var a = (i + 0.5f) / loopSegs * Mathf.PI * 2f;
                var p = loopC + new Vector3(Mathf.Cos(a) * 0.07f, Mathf.Sin(a) * 0.07f, 0f);
                mb.Box(p, new Vector3(0.05f, 0.03f, 0.03f), Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f));
            }
            return mb.ToMesh("LighthouseLantern_Frame");
        }



static Mesh BuildGlassMesh()
        {
            var mb = new MeshBuilder();
            mb.Prism(Matrix4x4.Translate(new Vector3(0f, 0.07f, 0f)), 0.255f, 0.52f, 6);
            mb.Prism(Matrix4x4.Translate(new Vector3(0f, 0.65f, 0f)), 0.22f, 0.13f, 6);
            return mb.ToMesh("LighthouseLantern_Glass");
        }



        // =====================================================================================
        // VFX
        // =====================================================================================

        static void BuildGlow(Transform head, Material mat)
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(head, false);
            go.transform.localPosition = new Vector3(0f, GlassCenterY, 0.05f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 100000f;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 100000f;
            main.startSpeed = 0f;
            main.startSize = 2.4f;
            main.startColor = new Color(1f, 0.62f, 0.25f, 0.6f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 1;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shape = ps.shape;
            shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = -2f;
        }

        static ParticleSystem BuildBurnPuff(Transform parent, Material mat)
        {
            var go = new GameObject("BurnPuff");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 0.95f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.35f), Ember);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.4f;
            main.maxParticles = 300;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.3f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.2f)));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.9f, 0.55f), 0f), new GradientColorKey(Ember, 0.4f), new GradientColorKey(new Color(0.85f, 0.22f, 0.08f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        static ParticleSystem BuildBurnRing(Transform parent, Material mat)
        {
            var go = new GameObject("BurnRing");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.25f;
            main.startSpeed = 0f;
            main.startSize = 1f;
            main.startColor = new Color(1f, 0.62f, 0.25f, 0.5f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 16;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(2f, new AnimationCurve(new Keyframe(0f, 0.3f, 0f, 3f), new Keyframe(1f, 1f, 0f, 0f)));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        // =====================================================================================
        // Assets
        // =====================================================================================

        static MeshRenderer AddRenderer(Transform parent, string name, Mesh mesh, Material mat, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return mr;
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets/GameObject/Guns", "LighthouseLantern");
            if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder(Root, "Meshes");
            if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder(Root, "Materials");
        }

        static Mesh SaveMesh(Mesh mesh, string name)
        {
            var path = $"{MeshDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            mesh.name = name;
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = name;
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static Material GetOrCreateMaterial(string name, Shader shader)
        {
            var path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            return mat;
        }

        static Material LitMaterial(string name, Color baseColor, float metallic, float smoothness, Color emission)
        {
            var mat = GetOrCreateMaterial(name, Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            if (emission.maxColorComponent > 0.001f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
            }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

static Material UnlitMaterial(string name, Color color)
        {
            var mat = GetOrCreateMaterial(name, Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetColor("_BaseColor", color);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }


        static Material BeamMaterial(string name, Color tint, float intensity, float bands, BlendMode dst, int queue)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[LighthouseLantern] Shader '{ShaderName}' not found.");
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            var mat = GetOrCreateMaterial(name, shader);
            mat.SetColor("_Color", tint);
            mat.SetFloat("_Intensity", intensity);
            mat.SetFloat("_BandStrength", bands);
            mat.SetFloat("_BandFreq", 2.5f);
            mat.SetFloat("_BandSpeed", 0.8f);
            mat.SetFloat("_DstBlend", (float)dst);
            mat.renderQueue = queue;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material AdditiveParticleMaterial(string name, string texturePath, Color tint)
        {
            var mat = GetOrCreateMaterial(name, Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (tex != null) mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", tint);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_ColorMode", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.DisableKeyword("_ALPHAMODULATE_ON");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // =====================================================================================
        // Mesh builder (flat-shaded, chunky)
        // =====================================================================================

        sealed class MeshBuilder
        {
            readonly List<Vector3> _v = new();
            readonly List<Vector3> _n = new();
            readonly List<int> _t = new();

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                m.SetVertices(_v);
                m.SetNormals(_n);
                m.SetTriangles(_t, 0);
                m.RecalculateBounds();
                m.RecalculateTangents();
                return m;
            }

            int Add(Vector3 p, Vector3 n)
            {
                _v.Add(p);
                _n.Add(n);
                return _v.Count - 1;
            }

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 inside)
            {
                var n = Vector3.Cross(b - a, d - a).normalized;
                if (Vector3.Dot(n, (a + b + c + d) * 0.25f - inside) < 0f)
                {
                    (b, d) = (d, b);
                    n = -n;
                }
                var i0 = Add(a, n); var i1 = Add(b, n); var i2 = Add(c, n); var i3 = Add(d, n);
                _t.Add(i0); _t.Add(i1); _t.Add(i2);
                _t.Add(i0); _t.Add(i2); _t.Add(i3);
            }

            void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
            {
                var n = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(n, (a + b + c) / 3f - inside) < 0f)
                {
                    (b, c) = (c, b);
                    n = -n;
                }
                var i0 = Add(a, n); var i1 = Add(b, n); var i2 = Add(c, n);
                _t.Add(i0); _t.Add(i1); _t.Add(i2);
            }

            public void Box(Vector3 c, Vector3 size, Quaternion rot)
            {
                var h = size * 0.5f;
                var p = new Vector3[8];
                for (var i = 0; i < 8; i++)
                    p[i] = c + rot * new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
                Quad(p[0], p[1], p[3], p[2], c); // -z
                Quad(p[4], p[5], p[7], p[6], c); // +z
                Quad(p[0], p[2], p[6], p[4], c); // -x
                Quad(p[1], p[3], p[7], p[5], c); // +x
                Quad(p[0], p[1], p[5], p[4], c); // -y
                Quad(p[2], p[3], p[7], p[6], c); // +y
            }

            /// <summary>N-sided prism along local +Y from 0 to h, flat faces facing ±Z.</summary>
            public void Prism(Matrix4x4 xf, float r, float h, int sides)
            {
                var off = Mathf.PI / sides;
                var bot = new Vector3[sides];
                var top = new Vector3[sides];
                for (var i = 0; i < sides; i++)
                {
                    var a = off + i * Mathf.PI * 2f / sides;
                    var d = new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
                    bot[i] = xf.MultiplyPoint3x4(d);
                    top[i] = xf.MultiplyPoint3x4(d + Vector3.up * h);
                }
                var centre = xf.MultiplyPoint3x4(Vector3.up * h * 0.5f);
                var cb = xf.MultiplyPoint3x4(Vector3.zero);
                var ct = xf.MultiplyPoint3x4(Vector3.up * h);
                for (var i = 0; i < sides; i++)
                {
                    var j = (i + 1) % sides;
                    Quad(bot[i], bot[j], top[j], top[i], centre);
                    Tri(ct, top[i], top[j], centre);
                    Tri(cb, bot[j], bot[i], centre);
                }
            }

            public void Cone(Matrix4x4 xf, float r, float h, int sides)
            {
                var off = Mathf.PI / sides;
                var apex = xf.MultiplyPoint3x4(Vector3.up * h);
                var cb = xf.MultiplyPoint3x4(Vector3.zero);
                var centre = xf.MultiplyPoint3x4(Vector3.up * h * 0.3f);
                var ring = new Vector3[sides];
                for (var i = 0; i < sides; i++)
                {
                    var a = off + i * Mathf.PI * 2f / sides;
                    ring[i] = xf.MultiplyPoint3x4(new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r));
                }
                for (var i = 0; i < sides; i++)
                {
                    var j = (i + 1) % sides;
                    Tri(apex, ring[i], ring[j], centre);
                    Tri(cb, ring[j], ring[i], centre);
                }
            }
        }
    }
}
