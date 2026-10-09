using HighSeas.Combat;
using HighSeas.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HighSeas.EditorTools
{
    /// <summary>
    /// Builds the Maelstrom Charge weapon: whirlpool disc mesh, toon whirlpool + bolt materials, a soft bolt-glow
    /// texture, the "Maelstrom Charge" prefab in Assets/GameObject/Guns and a demo scene with dummy enemy crowds.
    /// Menu: Tools/High Seas/Build Maelstrom Charge
    /// </summary>
    public static class MaelstromChargeBuilder
    {
        const string Root = "Assets/GameObject/Guns/MaelstromCharge";
        const string MatDir = Root + "/Materials";
        const string MeshDir = Root + "/Meshes";
        const string TexDir = Root + "/Textures";
        const string ShaderPath = Root + "/MaelstromWhirlpool.shader";
        public const string PrefabPath = "Assets/GameObject/Guns/Maelstrom Charge.prefab";
        public const string DemoScenePath = Root + "/MaelstromCharge_Demo.unity";

        static readonly Color BoltGlow = new Color(0.22f, 0.72f, 1f, 0.85f);
        static readonly Color BoltCore = new Color(0.9f, 0.98f, 1f, 1f);

        [MenuItem("Tools/High Seas/Build Maelstrom Charge")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[MaelstromCharge] Exit Play mode before building.");
                return;
            }
            var prefab = BuildPrefab();
            if (prefab) BuildDemoScene(prefab);
        }

        // =========================================================== prefab

        public static GameObject BuildPrefab()
        {
            EnsureFolder(Root);
            EnsureFolder(MatDir);
            EnsureFolder(MeshDir);
            EnsureFolder(TexDir);

            var disc = SaveMesh(BuildDisc(72, 28), "Maelstrom_Disc");
            var glowTex = BuildGlowTexture();

            var whirlMat = GetOrCreateMaterial("Maelstrom_Whirlpool", AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath));
            whirlMat.SetColor("_AbyssColor", new Color(0.0f, 0.015f, 0.06f));
            whirlMat.SetColor("_DeepColor", new Color(0.01f, 0.10f, 0.30f));
            whirlMat.SetColor("_MidColor", new Color(0.07f, 0.42f, 0.82f));
            whirlMat.SetColor("_LightColor", new Color(0.42f, 0.80f, 1.0f));
            whirlMat.SetColor("_FoamColor", new Color(0.78f, 0.95f, 1.0f));
            whirlMat.SetFloat("_Steps", 5f);
            whirlMat.SetFloat("_HoleSize", 0.2f);
            whirlMat.SetFloat("_DepthPower", 1.8f);
            whirlMat.SetFloat("_LipShadow", 0.35f);
            whirlMat.SetFloat("_Twist", 2.4f);
            whirlMat.enableInstancing = true;
            EditorUtility.SetDirty(whirlMat);

            var boltMat = ParticleMaterial("Maelstrom_BoltGlow", glowTex, Color.white, true);
            var coreMat = ParticleMaterial("Maelstrom_BoltCore", glowTex, Color.white, false);
            var flashMat = ParticleMaterial("Maelstrom_Flash", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Texture2D/glow.png"), Color.white, true);
            var ringMat = ParticleMaterial("Maelstrom_Ring", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/VFX/Healing/Textures/T_Heal_Ring.png"), Color.white, true);
            var dropMat = ParticleMaterial("Maelstrom_Droplet", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Texture2D/glow.png"), Color.white, false);

            var root = new GameObject("Maelstrom Charge");
            var strike = new GameObject("Strike").transform;
            strike.SetParent(root.transform, false);

            var halo = Line("BoltHalo", strike, boltMat, new Color(0.15f, 0.55f, 1f, 0.35f), 2.6f);
            halo.sortingOrder = 0;
            var bolt = Line("Bolt", strike, boltMat, BoltGlow, 1.25f);
            var core = Line("BoltCore", strike, coreMat, BoltCore, 0.5f);
            var branch = Line("Branch", strike, coreMat, new Color(0.7f, 0.92f, 1f, 0.95f), 0.17f);
            var branch2 = Line("Branch2", strike, coreMat, new Color(0.7f, 0.92f, 1f, 0.9f), 0.13f);
            branch2.sortingOrder = 2;
            bolt.sortingOrder = 1;
            core.sortingOrder = 2;
            branch.sortingOrder = 2;

            var whirl = new GameObject("Whirlpool");
            whirl.transform.SetParent(strike, false);
            whirl.AddComponent<MeshFilter>().sharedMesh = disc;
            var wr = whirl.AddComponent<MeshRenderer>();
            wr.sharedMaterial = whirlMat;
            wr.shadowCastingMode = ShadowCastingMode.Off;
            wr.receiveShadows = false;
            wr.lightProbeUsage = LightProbeUsage.Off;
            wr.reflectionProbeUsage = ReflectionProbeUsage.Off;

            var flash = Particles("Flash", strike, flashMat, 4);
            {
                var m = flash.main;
                m.startLifetime = 0.2f;
                m.startSize = 6f;
                m.startColor = new Color(0.55f, 0.88f, 1f, 0.9f);
                var col = flash.colorOverLifetime;
                col.enabled = true;
                col.color = Fade(new Color(0.8f, 0.96f, 1f), new Color(0.2f, 0.6f, 1f));
                var sol = flash.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.2f));
                flash.transform.localPosition = Vector3.up * 0.4f;
            }

            var ring = Particles("Ring", strike, ringMat, 4);
            {
                var m = ring.main;
                m.startLifetime = 0.5f;
                m.startSize = 2f;
                m.startColor = new Color(0.6f, 0.9f, 1f, 0.7f);
                var col = ring.colorOverLifetime;
                col.enabled = true;
                col.color = Fade(Color.white, Color.white);
                var sol = ring.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f, 0f, 6f), new Keyframe(1f, 3.2f, 0.5f, 0f)));
                ((ParticleSystemRenderer)ring.GetComponent<Renderer>()).renderMode = ParticleSystemRenderMode.HorizontalBillboard;
                ring.transform.localPosition = Vector3.up * 0.06f;
            }

            var impactGlow = Particles("ImpactGlow", strike, flashMat, 4);
            {
                var m = impactGlow.main;
                m.startLifetime = 0.4f;
                m.startSize = 5.5f;
                m.startColor = new Color(0.45f, 0.85f, 1f, 0.85f);
                var col = impactGlow.colorOverLifetime;
                col.enabled = true;
                col.color = Fade(Color.white, new Color(0.3f, 0.7f, 1f), 0.15f);
                var sol = impactGlow.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.7f, 1f, 1.15f));
                ((ParticleSystemRenderer)impactGlow.GetComponent<Renderer>()).renderMode = ParticleSystemRenderMode.HorizontalBillboard;
                impactGlow.transform.localPosition = Vector3.up * 0.08f;
            }

            var splash = Particles("Splash", strike, dropMat, 64);
            {
                var m = splash.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 7f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.32f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.93f, 1f, 1f), new Color(0.4f, 0.78f, 1f, 1f));
                m.gravityModifier = 1.8f;
                var sh = splash.shape;
                sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 32f;
                sh.radius = 0.7f;
                sh.rotation = new Vector3(-90f, 0f, 0f);
                var col = splash.colorOverLifetime;
                col.enabled = true;
                col.color = Fade(Color.white, Color.white, 0.6f);
                var r = (ParticleSystemRenderer)splash.GetComponent<Renderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.06f;
                r.lengthScale = 1.6f;
            }

            var vfx = root.AddComponent<MaelstromChargeVfx>();
            var so = new SerializedObject(vfx);
            so.FindProperty("strikeRoot").objectReferenceValue = strike;
            so.FindProperty("bolt").objectReferenceValue = bolt;
            so.FindProperty("boltCore").objectReferenceValue = core;
            so.FindProperty("branch").objectReferenceValue = branch;
            so.FindProperty("branch2").objectReferenceValue = branch2;
            so.FindProperty("boltHalo").objectReferenceValue = halo;
            so.FindProperty("impactGlow").objectReferenceValue = impactGlow;
            so.FindProperty("whirlpool").objectReferenceValue = whirl.transform;
            so.FindProperty("whirlpoolRenderer").objectReferenceValue = wr;
            so.FindProperty("flash").objectReferenceValue = flash;
            so.FindProperty("ring").objectReferenceValue = ring;
            so.FindProperty("splash").objectReferenceValue = splash;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("[MaelstromCharge] Prefab built at " + PrefabPath);
            return prefab;
        }

        // =========================================================== demo scene

        public static void BuildDemoScene(GameObject prefab)
        {
            var prevActive = EditorSceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SetActiveScene(scene);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 45f;
            cam.farClipPlane = 300f;
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(6f, 19.5f, -12f); // matches FollowCamera offset (18 up, 12 back)
            camGo.transform.LookAt(new Vector3(6f, 1.5f, 0f));

            var waterMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/GameObject/Guns/AnchorFlail/Materials/AnchorFlail_DemoWater.mat");
            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Water";
            water.transform.localScale = new Vector3(12f, 1f, 12f);
            Object.DestroyImmediate(water.GetComponent<Collider>());
            if (waterMat) water.GetComponent<Renderer>().sharedMaterial = waterMat;

            var brigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/Brigg.prefab");
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

            var weapon = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (brig) weapon.transform.SetParent(brig.transform, false);

            var islandPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/Islands/Island_Tropical_1.prefab");
            if (islandPrefab)
            {
                var island = (GameObject)PrefabUtility.InstantiatePrefab(islandPrefab, scene);
                island.name = "Island (scenery)";
                island.transform.position = new Vector3(-2f, 0f, 9f);
                island.transform.localScale = Vector3.one * 0.5f;
                foreach (var mb in island.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
            }

            // Dummy crowds: a dense one (should get picked) and a sparse one.
            var dummyMat = GetOrCreateMaterial("Maelstrom_DemoDummy", Shader.Find("Universal Render Pipeline/Lit"));
            dummyMat.SetColor("_BaseColor", new Color(0.55f, 0.18f, 0.12f));
            EditorUtility.SetDirty(dummyMat);
            var crowds = new GameObject("Dummy Enemies").transform;
            SpawnCrowd(crowds, new Vector3(10f, 0f, 2f), 7, 2.2f, dummyMat, 1);
            SpawnCrowd(crowds, new Vector3(4f, 0f, -7f), 2, 2.5f, dummyMat, 2);

            EditorSceneManager.SaveScene(scene, DemoScenePath);
            if (prevActive.IsValid() && prevActive.isLoaded)
            {
                EditorSceneManager.SetActiveScene(prevActive);
                EditorSceneManager.CloseScene(scene, true);
            }
            Debug.Log("[MaelstromCharge] Demo scene saved at " + DemoScenePath);
        }

        static void SpawnCrowd(Transform parent, Vector3 center, int count, float radius, Material mat, int seed)
        {
            var rnd = new System.Random(seed * 7919);
            for (int i = 0; i < count; i++)
            {
                float a = (float)(rnd.NextDouble() * Mathf.PI * 2.0);
                float r = Mathf.Sqrt((float)rnd.NextDouble()) * radius;
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = "Dummy Enemy";
                go.tag = "Enemy";
                go.transform.SetParent(parent, false);
                go.transform.position = center + new Vector3(Mathf.Cos(a) * r, 0.5f, Mathf.Sin(a) * r);
                go.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);
                go.GetComponent<Renderer>().sharedMaterial = mat;
                go.AddComponent<DamageReceiver>().Initialize(100000f);
            }
        }

        // =========================================================== pieces

        static LineRenderer Line(string name, Transform parent, Material mat, Color color, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.sharedMaterial = mat;
            lr.alignment = LineAlignment.View;
            lr.textureMode = LineTextureMode.Stretch;
            lr.numCornerVertices = 1;
            lr.numCapVertices = 2;
            lr.widthMultiplier = width;
            lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(0.85f, 1f), new Keyframe(1f, 1.1f));
            lr.startColor = color;
            lr.endColor = color;
            lr.positionCount = 2;
            lr.SetPositions(new[] { Vector3.up * 10f, Vector3.zero });
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.enabled = false;
            return lr;
        }

        static ParticleSystem Particles(string name, Transform parent, Material mat, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.playOnAwake = false;
            m.loop = false;
            m.duration = 1f;
            m.maxParticles = max;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            m.startSpeed = 0f;
            var em = ps.emission;
            em.enabled = false;
            var sh = ps.shape;
            sh.enabled = false;
            var r = (ParticleSystemRenderer)go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        static ParticleSystem.MinMaxGradient Fade(Color a, Color b, float holdUntil = 0.25f)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, holdUntil), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(g);
        }

        /// <summary>Disc of radius 1 on XZ. UV.x = angle 0..1 (seam duplicated), UV.y = radius 0..1.</summary>
        static Mesh BuildDisc(int angular, int radial)
        {
            int cols = angular + 1;
            var v = new Vector3[cols * (radial + 1)];
            var uv = new Vector2[v.Length];
            var n = new Vector3[v.Length];
            for (int ri = 0; ri <= radial; ri++)
            {
                float r = ri / (float)radial;
                r = r * r * 0.35f + r * 0.65f; // a bit more density near the rim foam
                for (int ai = 0; ai < cols; ai++)
                {
                    float u = ai / (float)angular;
                    float a = u * Mathf.PI * 2f;
                    int idx = ri * cols + ai;
                    v[idx] = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    uv[idx] = new Vector2(u, r);
                    n[idx] = Vector3.up;
                }
            }
            var t = new int[angular * radial * 6];
            int k = 0;
            for (int ri = 0; ri < radial; ri++)
            for (int ai = 0; ai < angular; ai++)
            {
                int a0 = ri * cols + ai, a1 = a0 + 1, b0 = a0 + cols, b1 = b0 + 1;
                t[k++] = a0; t[k++] = b0; t[k++] = a1;
                t[k++] = a1; t[k++] = b0; t[k++] = b1;
            }
            var mesh = new Mesh { name = "Maelstrom_Disc" };
            mesh.vertices = v;
            mesh.uv = uv;
            mesh.normals = n;
            mesh.triangles = t;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Soft across-the-width glow strip for the bolt LineRenderers (bright core, soft falloff).</summary>
        static Texture2D BuildGlowTexture()
        {
            string path = TexDir + "/Maelstrom_BoltGlow.png";
            const int w = 8, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                float d = Mathf.Abs(y / (float)(h - 1) * 2f - 1f);
                float core = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.45f, d));
                float glow = Mathf.Pow(1f - d, 2.2f);
                float a = Mathf.Clamp01(Mathf.Max(core, glow * 0.75f));
                for (int x = 0; x < w; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // =========================================================== assets

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int i = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, i));
            AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
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

        static Material ParticleMaterial(string name, Texture2D tex, Color tint, bool additive)
        {
            var mat = GetOrCreateMaterial(name, Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            if (tex != null) mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", tint);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", additive ? 2f : 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
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
    }
}
