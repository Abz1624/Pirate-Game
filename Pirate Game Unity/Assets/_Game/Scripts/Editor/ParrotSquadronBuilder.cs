using System.Collections.Generic;
using System.IO;
using HighSeas.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HighSeas.EditorTools
{
    /// <summary>
    /// Builds the Parrot Squadron weapon: low-poly parrot + slung cannon meshes, materials (+ transparent fade twins),
    /// the "Parrot Squadron" prefab in Assets/GameObject/Guns and a demo scene.
    /// Menu: Tools/High Seas/Build Parrot Squadron
    /// </summary>
    public static class ParrotSquadronBuilder
    {
        const string Root = "Assets/GameObject/Guns/ParrotSquadron";
        const string PrefabPath = "Assets/GameObject/Guns/Parrot Squadron.prefab";
        const string ScenePath = Root + "/ParrotSquadron_Demo.unity";
        const string Prefix = "ParrotSquadron_";
        const int ParrotCount = 7;

        [MenuItem("Tools/High Seas/Build Parrot Squadron")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[ParrotSquadron] Exit Play mode before building.");
                return;
            }
            BuildPrefab();
            BuildDemoScene();
        }

        // =========================================================== prefab

        public static GameObject BuildPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[ParrotSquadron] Exit Play mode before building.");
                return null;
            }
            EnsureFolder(Root);
            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Meshes");

            // Opaque materials
            var red = LitMat("Red", new Color(0.86f, 0.10f, 0.07f), 0.25f, 0f);
            var darkRed = LitMat("DarkRed", new Color(0.52f, 0.05f, 0.04f), 0.2f, 0f);
            var orange = LitMat("Orange", new Color(1.00f, 0.52f, 0.06f), 0.25f, 0f);
            var black = LitMat("Bandana", new Color(0.06f, 0.06f, 0.08f), 0.35f, 0f);
            var beak = LitMat("Beak", new Color(0.16f, 0.15f, 0.17f), 0.45f, 0f);
            var white = LitMat("Eye", new Color(1f, 0.97f, 0.92f), 0.3f, 0f);
            var iron = LitMat("Iron", new Color(0.13f, 0.13f, 0.15f), 0.55f, 0.35f);
            var strap = LitMat("Strap", new Color(0.42f, 0.26f, 0.14f), 0.15f, 0f);
            var ball = LitMat("Cannonball", new Color(0.09f, 0.09f, 0.10f), 0.6f, 0.3f);
            var flashMat = ParticleMat("Flash", true);
            var smokeMat = ParticleMat("Smoke", false);

            var opaque = new[] { red, darkRed, orange, black, beak, white, iron, strap };
            var fade = new Material[opaque.Length];
            for (int i = 0; i < opaque.Length; i++) fade[i] = FadeTwin(opaque[i]);

            // Meshes
            var bodyMesh = SaveMesh(BuildBody(), "Body");
            var wingR = SaveMesh(BuildWing(false), "WingR");
            var wingL = SaveMesh(BuildWing(true), "WingL");
            var cannonMesh = SaveMesh(BuildCannon(), "Cannon");
            var strapMesh = SaveMesh(BuildStraps(), "Straps");
            var ballMesh = SaveMesh(BuildBall(), "Ball");

            var trailMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/CannonballTrail.mat");
            var splash = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/VFX/19_WaterImpactSplash.prefab");

            var rootGo = new GameObject("Parrot Squadron");
            var launch = new GameObject("LaunchPoint").transform;
            launch.SetParent(rootGo.transform, false);
            launch.localPosition = new Vector3(0f, 2.2f, -0.6f);

            for (int i = 0; i < ParrotCount; i++)
            {
                var p = BuildParrot(i, bodyMesh, wingL, wingR, cannonMesh, strapMesh,
                    new[] { red, black, beak, white, orange },
                    new[] { red, orange, darkRed },
                    new[] { iron }, strap, flashMat, smokeMat, trailMat);
                p.transform.SetParent(rootGo.transform, false);
                // Edit-mode preview pose: a wedge hovering above the deck.
                int row = (i + 1) / 2;
                float side = i == 0 ? 0f : (i % 2 == 1 ? 1f : -1f);
                p.transform.localPosition = new Vector3(side * row * 0.85f, 4.2f + Mathf.Sin(i * 2.1f) * 0.3f, 1.5f - row * 1.4f);
            }

            var vfx = rootGo.AddComponent<ParrotSquadronVfx>();
            var so = new SerializedObject(vfx);
            so.FindProperty("launchPoint").objectReferenceValue = launch;
            so.FindProperty("ballMesh").objectReferenceValue = ballMesh;
            so.FindProperty("ballMaterial").objectReferenceValue = ball;
            so.FindProperty("ballTrailMaterial").objectReferenceValue = trailMat;
            so.FindProperty("impactSplashPrefab").objectReferenceValue = splash;
            SetArray(so.FindProperty("opaqueMaterials"), opaque);
            SetArray(so.FindProperty("fadeMaterials"), fade);
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(rootGo, PrefabPath);
            Object.DestroyImmediate(rootGo);
            AssetDatabase.SaveAssets();
            Debug.Log("[ParrotSquadron] Prefab built at " + PrefabPath);
            return prefab;
        }

        static void SetArray(SerializedProperty prop, Material[] mats)
        {
            prop.arraySize = mats.Length;
            for (int i = 0; i < mats.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = mats[i];
        }

        static GameObject BuildParrot(int index, Mesh body, Mesh wingL, Mesh wingR, Mesh cannon, Mesh straps,
            Material[] bodyMats, Material[] wingMats, Material[] cannonMats, Material strapMat,
            Material flashMat, Material smokeMat, Material trailMat)
        {
            var root = new GameObject("Parrot_" + index);
            root.transform.localScale = Vector3.one * 1.15f;

            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            AddRenderer(model, body, bodyMats);

            var wl = new GameObject("WingL");
            wl.transform.SetParent(model.transform, false);
            wl.transform.localPosition = new Vector3(-0.16f, 0.08f, 0.04f);
            AddRenderer(wl, wingL, wingMats);

            var wr = new GameObject("WingR");
            wr.transform.SetParent(model.transform, false);
            wr.transform.localPosition = new Vector3(0.16f, 0.08f, 0.04f);
            AddRenderer(wr, wingR, wingMats);

            var st = new GameObject("Straps");
            st.transform.SetParent(model.transform, false);
            AddRenderer(st, straps, new[] { strapMat });

            var cn = new GameObject("Cannon");
            cn.transform.SetParent(model.transform, false);
            cn.transform.localPosition = new Vector3(0f, -0.33f, 0.08f);
            AddRenderer(cn, cannon, cannonMats);

            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(cn.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0f, 0.38f);

            var flash = new GameObject("Flash");
            flash.transform.SetParent(muzzle.transform, false);
            BuildMuzzleFlash(flash, flashMat);

            var smoke = new GameObject("Smoke");
            smoke.transform.SetParent(muzzle.transform, false);
            BuildCannonSmoke(smoke, smokeMat);

            if (trailMat)
            {
                var trail = new GameObject("Trail");
                trail.transform.SetParent(root.transform, false);
                trail.transform.localPosition = new Vector3(0f, 0f, -0.5f);
                var tr = trail.AddComponent<TrailRenderer>();
                tr.sharedMaterial = trailMat;
                tr.time = 0.55f;
                tr.minVertexDistance = 0.15f;
                tr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
                tr.widthMultiplier = 0.22f;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(0.45f, 0f), new GradientAlphaKey(0f, 1f) });
                tr.colorGradient = g;
                tr.shadowCastingMode = ShadowCastingMode.Off;
                tr.emitting = false;
            }
            return root;
        }

        static void AddRenderer(GameObject go, Mesh mesh, Material[] mats)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = ShadowCastingMode.On;
        }

        static void BuildMuzzleFlash(GameObject go, Material mat)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.45f), new Color(1f, 0.55f, 0.15f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 4) });
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 18f;
            sh.radius = 0.02f;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Very light wisp of smoke leaking from the barrel while flying.</summary>
        static void BuildCannonSmoke(GameObject go, Material mat)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.18f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.92f, 0.92f, 0.9f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.gravityModifier = -0.02f;
            main.maxParticles = 40;
            var em = ps.emission;
            em.rateOverTime = 10f;
            em.rateOverDistance = 2.5f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.03f;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 2.2f)));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.75f, 0.75f, 0.75f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.22f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.sortingFudge = 2f;
        }

        // =========================================================== demo scene

        public static void BuildDemoScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!prefab) prefab = BuildPrefab();

            // Built additively so any open, unsaved scene is left untouched.
            var prevActive = EditorSceneManager.GetActiveScene();
            var existing = EditorSceneManager.GetSceneByPath(ScenePath);
            if (existing.IsValid() && existing.isLoaded) EditorSceneManager.CloseScene(existing, true);
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
            camGo.transform.position = new Vector3(7.5f, 19.5f, -12f); // matches FollowCamera offset (18 up, 12 back)
            camGo.transform.LookAt(new Vector3(7.5f, 1.5f, 0f));

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

            var squadron = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (brig) squadron.transform.SetParent(brig.transform, false);

            var islandPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/Islands/Island_Tropical_1.prefab");
            if (islandPrefab)
            {
                var island = (GameObject)PrefabUtility.InstantiatePrefab(islandPrefab, scene);
                island.name = "Target Island";
                island.transform.position = new Vector3(15f, 0f, 1f);
                island.transform.localScale = Vector3.one * 0.6f;
                foreach (var mb in island.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
                var so = new SerializedObject(squadron.GetComponent<ParrotSquadronVfx>());
                so.FindProperty("manualTarget").objectReferenceValue = island.transform;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            if (prevActive.IsValid() && prevActive.isLoaded)
            {
                EditorSceneManager.SetActiveScene(prevActive);
                EditorSceneManager.CloseScene(scene, true);
            }
            Debug.Log("[ParrotSquadron] Demo scene saved at " + ScenePath);
        }

        // =========================================================== meshes

        static Mesh BuildBody()
        {
            // submeshes: 0 red, 1 black (bandana), 2 beak, 3 eye-white, 4 orange
            var mb = new MeshBuilder(5);
            // body + head
            mb.Ellipsoid(0, new Vector3(0f, 0f, -0.02f), new Vector3(0.21f, 0.19f, 0.40f), 8, 5, Quaternion.Euler(-8f, 0f, 0f));
            mb.Ellipsoid(0, new Vector3(0f, 0.14f, 0.38f), new Vector3(0.19f, 0.18f, 0.2f), 8, 5, Quaternion.identity);
            // orange belly patch
            mb.Ellipsoid(4, new Vector3(0f, -0.06f, 0.12f), new Vector3(0.15f, 0.12f, 0.22f), 6, 4, Quaternion.Euler(-10f, 0f, 0f));
            // pirate bandana: cap over the top of the head, a knot at the back and two long flowing tails
            mb.Ellipsoid(1, new Vector3(0f, 0.215f, 0.37f), new Vector3(0.205f, 0.135f, 0.215f), 8, 5, Quaternion.Euler(-6f, 0f, 0f));
            mb.Ellipsoid(1, new Vector3(0f, 0.25f, 0.17f), new Vector3(0.06f, 0.055f, 0.055f), 6, 3, Quaternion.identity);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 a = new Vector3(0.025f * s, 0.25f, 0.15f);
                Vector3 b = new Vector3(0.09f * s, 0.27f, -0.06f);
                Vector3 c = new Vector3(0.12f * s, 0.24f, -0.26f);
                Vector3 d = new Vector3(0.17f * s, 0.27f, -0.46f);
                mb.Ribbon(1, new[] { a, b, c, d }, new[] { 0.09f, 0.1f, 0.085f, 0.03f }, 0.25f * s);
            }
            // eyes peeking under the bandana (angry tilt) + pupils
            for (int s = -1; s <= 1; s += 2)
            {
                mb.Ellipsoid(3, new Vector3(0.12f * s, 0.15f, 0.5f), new Vector3(0.045f, 0.03f, 0.035f), 6, 3, Quaternion.Euler(0f, 0f, -18f * s));
                mb.Ellipsoid(1, new Vector3(0.135f * s, 0.15f, 0.525f), new Vector3(0.02f, 0.02f, 0.02f), 4, 2, Quaternion.identity);
            }
            // hooked beak: two cone segments bending down
            mb.Frustum(2, new Vector3(0f, 0.08f, 0.52f), new Vector3(0f, 0.06f, 0.66f), 0.075f, 0.045f, 6, true);
            mb.Frustum(2, new Vector3(0f, 0.06f, 0.66f), new Vector3(0f, -0.03f, 0.71f), 0.045f, 0.008f, 6, true);
            // tail: three long feathers, red with orange tips
            float[] yaw = { -14f, 0f, 14f };
            for (int i = 0; i < 3; i++)
            {
                var rot = Quaternion.Euler(8f, 180f + yaw[i], 0f);
                var start = new Vector3(0f, 0.01f, -0.36f);
                mb.FlatDiamond(0, start, rot, 0.11f, 0.42f, 0.0f);
                mb.FlatDiamond(4, start + rot * new Vector3(0f, -0.004f, 0.3f), rot, 0.09f, 0.28f, 0.0f);
            }
            return mb.Build(Prefix + "Body");
        }

        static Mesh BuildWing(bool left)
        {
            // submeshes: 0 red covert, 1 orange flight feathers, 2 dark red shading.
            // Built for the RIGHT wing (+X), mirrored for left.
            var mb = new MeshBuilder(3);
            mb.Transform = left ? Matrix4x4.Scale(new Vector3(-1f, 1f, 1f)) : Matrix4x4.identity;
            var cover = new[]
            {
                new Vector2(0f, 0.13f), new Vector2(0.38f, 0.12f), new Vector2(0.62f, 0.04f),
                new Vector2(0.58f, -0.12f), new Vector2(0.35f, -0.2f), new Vector2(0.1f, -0.18f), new Vector2(0f, -0.1f)
            };
            mb.FlatPolygon(0, cover, 0.01f);
            // primary feathers fanned from the wrist
            var pivot = new Vector3(0.3f, 0f, -0.02f);
            float[] ang = { 8f, -12f, -32f, -52f, -74f, -96f };
            float[] len = { 0.66f, 0.64f, 0.58f, 0.5f, 0.42f, 0.34f };
            for (int i = 0; i < ang.Length; i++)
            {
                var rot = Quaternion.Euler(0f, 90f - ang[i], 0f);
                mb.FlatDiamond(1, pivot, rot, 0.12f, len[i], -0.005f);
            }
            // a few red secondaries over the inner trailing edge
            for (int i = 0; i < 3; i++)
            {
                var rot = Quaternion.Euler(0f, 180f - 10f * i, 0f);
                mb.FlatDiamond(0, new Vector3(0.08f + i * 0.1f, 0.004f, -0.08f), rot, 0.1f, 0.24f, 0f);
            }
            // dark shading: a band along the inner covert row + a couple of dark streaks toward the wrist
            var shade = new[]
            {
                new Vector2(0.02f, -0.02f), new Vector2(0.3f, -0.04f), new Vector2(0.44f, -0.1f),
                new Vector2(0.3f, -0.17f), new Vector2(0.08f, -0.15f)
            };
            mb.FlatPolygon(2, shade, 0.022f);
            mb.FlatDiamond(2, new Vector3(0.34f, 0.022f, 0.06f), Quaternion.Euler(0f, 100f, 0f), 0.06f, 0.2f, 0f);
            mb.FlatDiamond(2, new Vector3(0.2f, 0.022f, 0.08f), Quaternion.Euler(0f, 95f, 0f), 0.05f, 0.14f, 0f);
            return mb.Build(Prefix + (left ? "WingL" : "WingR"));
        }

        static Mesh BuildCannon()
        {
            var mb = new MeshBuilder(1);
            mb.Frustum(0, new Vector3(0f, 0f, -0.22f), new Vector3(0f, 0f, 0.3f), 0.1f, 0.072f, 8, true);
            mb.Frustum(0, new Vector3(0f, 0f, 0.27f), new Vector3(0f, 0f, 0.36f), 0.095f, 0.095f, 8, true); // muzzle lip
            mb.Frustum(0, new Vector3(0f, 0f, -0.06f), new Vector3(0f, 0f, 0.0f), 0.112f, 0.112f, 8, true); // reinforce band
            mb.Ellipsoid(0, new Vector3(0f, 0f, -0.27f), new Vector3(0.055f, 0.055f, 0.055f), 6, 3, Quaternion.identity); // cascabel
            return mb.Build(Prefix + "Cannon");
        }

        static Mesh BuildStraps()
        {
            var mb = new MeshBuilder(1);
            for (int s = -1; s <= 1; s += 2)
            {
                float z = 0.08f + 0.12f * s;
                mb.Box(0, new Vector3(0f, -0.24f, z), new Vector3(0.04f, 0.2f, 0.04f), Quaternion.identity);
                mb.Frustum(0, new Vector3(0f, -0.33f, z - 0.025f), new Vector3(0f, -0.33f, z + 0.025f), 0.118f, 0.118f, 8, true);
            }
            return mb.Build(Prefix + "Straps");
        }

        static Mesh BuildBall()
        {
            var mb = new MeshBuilder(1);
            mb.Ellipsoid(0, Vector3.zero, Vector3.one * 0.5f, 8, 6, Quaternion.identity);
            return mb.Build(Prefix + "Ball");
        }

        // =========================================================== assets

        static Material LitMat(string name, Color c, float smooth, float metal)
        {
            string path = Root + "/Materials/" + Prefix + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Transparent copy of an opaque Lit material, used only while the parrots fade out.</summary>
        static Material FadeTwin(Material src)
        {
            string path = Root + "/Materials/" + src.name + "_Fade.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(src);
                AssetDatabase.CreateAsset(m, path);
            }
            m.CopyPropertiesFromMaterial(src);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 1f); // keep self-occlusion clean while fading
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material ParticleMat(string name, bool additive)
        {
            string path = Root + "/Materials/" + Prefix + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            if (tex) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = Root + "/Meshes/" + Prefix + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = Prefix + name;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mesh);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // =========================================================== mesh builder (flat shaded)

        sealed class MeshBuilder
        {
            readonly List<Vector3> _v = new List<Vector3>();
            readonly List<List<int>> _sub = new List<List<int>>();
            public Matrix4x4 Transform = Matrix4x4.identity;

            public MeshBuilder(int submeshes)
            {
                for (int i = 0; i < submeshes; i++) _sub.Add(new List<int>());
            }

            void Raw(int s, Vector3 a, Vector3 b, Vector3 c)
            {
                int i = _v.Count;
                _v.Add(a); _v.Add(b); _v.Add(c);
                _sub[s].Add(i); _sub[s].Add(i + 1); _sub[s].Add(i + 2);
            }

            /// <summary>Triangle wound so its normal faces away from 'center' (convex shapes).</summary>
            void TriOut(int s, Vector3 a, Vector3 b, Vector3 c, Vector3 center)
            {
                a = Transform.MultiplyPoint3x4(a); b = Transform.MultiplyPoint3x4(b); c = Transform.MultiplyPoint3x4(c);
                center = Transform.MultiplyPoint3x4(center);
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, (a + b + c) / 3f - center) < 0f) Raw(s, a, c, b);
                else Raw(s, a, b, c);
            }

            void TriBoth(int s, Vector3 a, Vector3 b, Vector3 c)
            {
                a = Transform.MultiplyPoint3x4(a); b = Transform.MultiplyPoint3x4(b); c = Transform.MultiplyPoint3x4(c);
                Raw(s, a, b, c);
                Raw(s, a, c, b);
            }

            public void Ellipsoid(int s, Vector3 c, Vector3 r, int lon, int lat, Quaternion rot)
            {
                var pts = new Vector3[lat + 1, lon];
                for (int y = 0; y <= lat; y++)
                {
                    float th = Mathf.PI * y / lat;
                    for (int x = 0; x < lon; x++)
                    {
                        float ph = 2f * Mathf.PI * x / lon;
                        var u = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                        pts[y, x] = c + rot * Vector3.Scale(u, r);
                    }
                }
                for (int y = 0; y < lat; y++)
                    for (int x = 0; x < lon; x++)
                    {
                        int x1 = (x + 1) % lon;
                        Vector3 a = pts[y, x], b = pts[y, x1], d = pts[y + 1, x], e = pts[y + 1, x1];
                        if (y > 0) TriOut(s, a, b, e, c);
                        if (y < lat - 1) TriOut(s, a, e, d, c);
                    }
            }

            public void Frustum(int s, Vector3 a, Vector3 b, float ra, float rb, int sides, bool caps)
            {
                Vector3 axis = (b - a).normalized;
                Vector3 up = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up;
                Vector3 u = Vector3.Cross(axis, up).normalized;
                Vector3 w = Vector3.Cross(axis, u).normalized;
                Vector3 mid = (a + b) * 0.5f;
                var ringA = new Vector3[sides];
                var ringB = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    float ang = 2f * Mathf.PI * (i + 0.5f) / sides;
                    Vector3 d = u * Mathf.Cos(ang) + w * Mathf.Sin(ang);
                    ringA[i] = a + d * ra;
                    ringB[i] = b + d * rb;
                }
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    TriOut(s, ringA[i], ringA[j], ringB[j], mid);
                    TriOut(s, ringA[i], ringB[j], ringB[i], mid);
                    if (caps)
                    {
                        TriOut(s, a, ringA[i], ringA[j], mid);
                        TriOut(s, b, ringB[j], ringB[i], mid);
                    }
                }
            }

            public void Box(int s, Vector3 c, Vector3 size, Quaternion rot)
            {
                Vector3 h = size * 0.5f;
                var p = new Vector3[8];
                for (int i = 0; i < 8; i++)
                    p[i] = c + rot * new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
                int[,] f = { { 0, 1, 3, 2 }, { 4, 6, 7, 5 }, { 0, 4, 5, 1 }, { 2, 3, 7, 6 }, { 0, 2, 6, 4 }, { 1, 5, 7, 3 } };
                for (int i = 0; i < 6; i++)
                {
                    TriOut(s, p[f[i, 0]], p[f[i, 1]], p[f[i, 2]], c);
                    TriOut(s, p[f[i, 0]], p[f[i, 2]], p[f[i, 3]], c);
                }
            }

            /// <summary>Double-sided strip through a polyline (bandana tails). 'tilt' rolls the strip about its length.</summary>
            public void Ribbon(int s, Vector3[] pts, float[] widths, float tilt)
            {
                var left = new Vector3[pts.Length];
                var right = new Vector3[pts.Length];
                for (int i = 0; i < pts.Length; i++)
                {
                    Vector3 dir = (i < pts.Length - 1 ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1]).normalized;
                    Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
                    side = Quaternion.AngleAxis(tilt * Mathf.Rad2Deg * (0.5f + i * 0.3f), dir) * side;
                    left[i] = pts[i] - side * widths[i] * 0.5f;
                    right[i] = pts[i] + side * widths[i] * 0.5f;
                }
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    TriBoth(s, left[i], right[i], right[i + 1]);
                    TriBoth(s, left[i], right[i + 1], left[i + 1]);
                }
            }

            /// <summary>Double-sided feather: a diamond starting at 'start' extending along rot*forward.</summary>
            public void FlatDiamond(int s, Vector3 start, Quaternion rot, float width, float length, float yOffset)
            {
                Vector3 o = start + Vector3.up * yOffset;
                Vector3 l = o + rot * new Vector3(-width * 0.5f, 0f, length * 0.35f);
                Vector3 r = o + rot * new Vector3(width * 0.5f, 0f, length * 0.35f);
                Vector3 tip = o + rot * new Vector3(0f, 0f, length);
                TriBoth(s, o, l, tip);
                TriBoth(s, o, tip, r);
            }

            /// <summary>Double-sided convex polygon in the XZ plane (x = span, y-of-Vector2 = z).</summary>
            public void FlatPolygon(int s, Vector2[] pts, float y)
            {
                var p0 = new Vector3(pts[0].x, y, pts[0].y);
                for (int i = 1; i < pts.Length - 1; i++)
                    TriBoth(s, p0, new Vector3(pts[i].x, y, pts[i].y), new Vector3(pts[i + 1].x, y, pts[i + 1].y));
            }

            public Mesh Build(string name)
            {
                var m = new Mesh { name = name };
                m.SetVertices(_v);
                m.subMeshCount = _sub.Count;
                for (int i = 0; i < _sub.Count; i++) m.SetTriangles(_sub[i], i);
                m.RecalculateNormals();
                m.RecalculateBounds();
                m.RecalculateTangents();
                return m;
            }
        }
    }
}
