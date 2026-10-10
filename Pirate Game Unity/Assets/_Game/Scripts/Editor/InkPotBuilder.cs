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
    /// Builds the Ink Pot weapon: low-poly square black glass ink bottle (max 150 tris, split into 4 glass shards),
    /// wooden cork, ink gush and droplet meshes, toon materials, the "Ink Pot" prefab in Assets/GameObject/Guns and a
    /// demo scene (ship lobbing pots at a dummy crowd + a turntable hero pose of the burst).
    /// Menu: Tools/High Seas/Build Ink Pot
    /// </summary>
    public static class InkPotBuilder
    {
        const string Root = "Assets/GameObject/Guns/InkPot";
        const string MatDir = Root + "/Materials";
        const string MeshDir = Root + "/Meshes";
        const string TexDir = Root + "/Textures";
        const string ToonShaderPath = Root + "/InkPotToon.shader";
        const string SlickShaderPath = Root + "/InkSlick.shader";
        public const string PrefabPath = "Assets/GameObject/Guns/Ink Pot.prefab";
        public const string DemoScenePath = Root + "/InkPot_Demo.unity";
        public const string HeroPrefabPath = Root + "/Ink Pot Hero.prefab";

        const int BottleSegments = 8;
        const int MaxPotTris = 150;

        // (radius to the flat face, height) — origin at the bottom centre, bottle is ~0.9 tall at the lip.
        // Square ink bottle with chamfered corners (rings 0-3), sloped shoulder, round neck and lip.
        static readonly Vector2[] BottleProfile =
        {
            new Vector2(0f, 0f),
            new Vector2(0.4f, 0f),
            new Vector2(0.44f, 0.04f),
            new Vector2(0.44f, 0.5f),
            new Vector2(0.3f, 0.6f),
            new Vector2(0.15f, 0.66f),
            new Vector2(0.15f, 0.8f),
            new Vector2(0.185f, 0.82f),
            new Vector2(0.185f, 0.88f),
            new Vector2(0.12f, 0.9f),
            new Vector2(0f, 0.9f),
        };

        // 1 = square cross-section, 0 = round
        static readonly float[] BottleSquareness = { 1f, 1f, 1f, 1f, 0.55f, 0f, 0f, 0f, 0f, 0f, 0f };

        static readonly Vector2[] CorkProfile =
        {
            new Vector2(0f, 0f),
            new Vector2(0.125f, 0f),
            new Vector2(0.14f, 0.14f),
            new Vector2(0.17f, 0.16f),
            new Vector2(0.175f, 0.24f),
            new Vector2(0.15f, 0.27f),
            new Vector2(0f, 0.27f),
        };

        // 4 glass shards, each = one chamfered corner + one flat side
        static readonly int[][] ShardSegments =
        {
            new[] { 0, 1 }, new[] { 2, 3 }, new[] { 4, 5 }, new[] { 6, 7 },
        };

        [MenuItem("Tools/High Seas/Build Ink Pot")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[InkPot] Exit Play mode before building.");
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

            // ---- meshes
            float[] potV = ProfileV(BottleProfile);
            int potTris = 0;
            var shardMeshes = new Mesh[ShardSegments.Length];
            for (int i = 0; i < ShardSegments.Length; i++)
            {
                var m = Lathe(BottleProfile, potV, BottleSquareness, BottleSegments, ShardSegments[i][0], ShardSegments[i][1], true);
                potTris += m.triangles.Length / 3;
                shardMeshes[i] = SaveMesh(m, "InkPot_Shard" + i);
            }
            if (potTris > MaxPotTris) Debug.LogWarning($"[InkPot] Bottle mesh is {potTris} tris (budget {MaxPotTris}).");
            var cork = SaveMesh(Lathe(CorkProfile, ProfileV(CorkProfile), null, 8, 0, 7, false), "InkPot_Cork");
            var spill = SaveMesh(BuildInkGush(), "InkPot_InkGush");
            var drop = SaveMesh(BuildDroplet(), "InkPot_Droplet");
            var disc = SaveMesh(BuildDisc(64, 22), "InkPot_SlickDisc");

            // ---- textures
            var corkTex = BuildCorkTexture();

            // ---- materials
            var toon = AssetDatabase.LoadAssetAtPath<Shader>(ToonShaderPath);
            var glassMat = ToonMat("InkPot_BlackGlass", toon, null, new Color(0.05f, 0.045f, 0.062f), new Color(0.55f, 0.55f, 0.65f),
                0.2f, 1f, new Color(0.88f, 0.9f, 1f), new Color(0.55f, 0.52f, 0.72f), 2.4f, 0.4f, new Color(0.02f, 0.015f, 0.03f));
            var corkMat = ToonMat("InkPot_Cork", toon, corkTex, Color.white, new Color(0.62f, 0.5f, 0.45f),
                0.05f, 0.12f, new Color(1f, 0.95f, 0.85f), new Color(1f, 0.85f, 0.6f), 3f, 0.15f, new Color(0.45f, 0.32f, 0.2f));
            var inkMat = ToonMat("InkPot_Ink", toon, null, new Color(0.02f, 0.008f, 0.035f), new Color(0.6f, 0.5f, 0.75f),
                0.3f, 1.1f, new Color(0.88f, 0.82f, 1f), new Color(0.5f, 0.22f, 0.9f), 3.6f, 0.32f, new Color(0.02f, 0.008f, 0.035f));
            var dropMat = ToonMat("InkPot_Droplet", toon, null, new Color(0.02f, 0.008f, 0.035f), new Color(0.6f, 0.5f, 0.75f),
                0.38f, 1.15f, new Color(0.88f, 0.82f, 1f), new Color(0.5f, 0.22f, 0.9f), 3.2f, 0.38f, new Color(0.02f, 0.008f, 0.035f));

            var slickMat = GetOrCreateMaterial("InkPot_Slick", AssetDatabase.LoadAssetAtPath<Shader>(SlickShaderPath));
            slickMat.SetColor("_InkColor", new Color(0.012f, 0.006f, 0.02f));
            slickMat.SetColor("_SheenColor", new Color(0.22f, 0.08f, 0.38f));
            slickMat.SetColor("_RimColor", new Color(0.62f, 0.3f, 1f));
            slickMat.SetColor("_RingColor", new Color(0.82f, 0.93f, 1f));
            slickMat.SetFloat("_SlickRadius", 0.62f);
            // draw after transparent sea shaders (e.g. Stylized Water at 2998) so the pool is never painted over
            slickMat.renderQueue = 3005;
            slickMat.enableInstancing = true;
            EditorUtility.SetDirty(slickMat);

            // ---- gameplay hierarchy
            var root = new GameObject("Ink Pot");
            var launcher = new GameObject("Launcher").transform;
            launcher.SetParent(root.transform, false);
            launcher.localPosition = new Vector3(0f, 1.6f, 0f);

            var shot = new GameObject("Shot").transform;
            shot.SetParent(root.transform, false);
            var rig = BuildPotRig(shot, shardMeshes, cork, spill, glassMat, corkMat, inkMat);
            var splash = BuildSplash(shot, drop, dropMat);
            shot.gameObject.SetActive(false); // only shown while a pot is in the air

            var slick = MeshChild("Slick", root.transform, disc, slickMat);
            slick.SetActive(false);

            // ---- edit-mode preview: baked mid-air burst so the hero look is visible as soon as the prefab is dropped
            var preview = BuildBurstPreview(shardMeshes, cork, spill, drop, glassMat, corkMat, inkMat, dropMat);
            preview.name = "BurstPreview";
            preview.tag = "EditorOnly";
            preview.transform.SetParent(root.transform, false);
            preview.transform.localPosition = new Vector3(0f, 2.4f, 0f);

            var vfx = root.AddComponent<InkPotVfx>();
            var so = new SerializedObject(vfx);
            so.FindProperty("launcher").objectReferenceValue = launcher;
            so.FindProperty("shotRoot").objectReferenceValue = shot;
            AssignRig(so, rig);
            so.FindProperty("splash").objectReferenceValue = splash;
            so.FindProperty("slick").objectReferenceValue = slick.transform;
            so.FindProperty("editorPreview").objectReferenceValue = preview;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            // ---- standalone static hero asset (no script) for showcase / thumbnails
            var hero = BuildBurstPreview(shardMeshes, cork, spill, drop, glassMat, corkMat, inkMat, dropMat);
            hero.name = "Ink Pot Hero";
            PrefabUtility.SaveAsPrefabAsset(hero, HeroPrefabPath);
            Object.DestroyImmediate(hero);

            AssetDatabase.SaveAssets();
            Debug.Log($"[InkPot] Prefab built at {PrefabPath} (pot mesh {potTris} tris across {shardMeshes.Length} shards), hero at {HeroPrefabPath}.");
            return prefab;
        }

        sealed class PotRig
        {
            public Transform pot, cork, spill;
            public Transform[] shards;
        }

        static PotRig BuildPotRig(Transform parent, Mesh[] shardMeshes, Mesh cork, Mesh spill,
            Material glassMat, Material corkMat, Material inkMat)
        {
            var r = new PotRig();
            r.pot = new GameObject("Pot").transform;
            r.pot.SetParent(parent, false);
            r.shards = new Transform[shardMeshes.Length];
            for (int i = 0; i < shardMeshes.Length; i++)
                r.shards[i] = MeshChild("Shard_" + i, r.pot, shardMeshes[i], glassMat).transform;
            r.cork = MeshChild("Cork", r.pot, cork, corkMat).transform;
            r.cork.localPosition = new Vector3(0f, 0.78f, 0f);
            r.spill = MeshChild("InkSpill", r.pot, spill, inkMat).transform;
            r.spill.localPosition = new Vector3(0f, 0.84f, 0f);
            r.spill.gameObject.SetActive(false);
            return r;
        }

        static void AssignRig(SerializedObject so, PotRig rig)
        {
            so.FindProperty("pot").objectReferenceValue = rig.pot;
            var sp = so.FindProperty("shards");
            sp.arraySize = rig.shards.Length;
            for (int i = 0; i < rig.shards.Length; i++) sp.GetArrayElementAtIndex(i).objectReferenceValue = rig.shards[i];
            so.FindProperty("cork").objectReferenceValue = rig.cork;
            so.FindProperty("inkSpill").objectReferenceValue = rig.spill;
        }

        /// <summary>
        /// Static, script-free mid-air shatter: black glass bottle cracking apart, cork popping, glossy ink gush
        /// and a few teardrop ink droplets flying outward. Posed by the same code the runtime uses.
        /// </summary>
        static GameObject BuildBurstPreview(Mesh[] shardMeshes, Mesh cork, Mesh spill, Mesh drop,
            Material glassMat, Material corkMat, Material inkMat, Material dropMat)
        {
            const float scale = 1.2f;
            const float t = 0.24f;
            var go = new GameObject("Ink Pot Hero");
            var rig = BuildPotRig(go.transform, shardMeshes, cork, spill, glassMat, corkMat, inkMat);

            var tmp = go.AddComponent<InkPotVfx>();
            var so = new SerializedObject(tmp);
            so.FindProperty("shotRoot").objectReferenceValue = go.transform;
            AssignRig(so, rig);
            so.ApplyModifiedPropertiesWithoutUndo();
            var tilt = Quaternion.AngleAxis(-16f, Vector3.forward) * Quaternion.AngleAxis(10f, Vector3.right);
            tmp.PoseBurstPreview(t, 1337, tilt, scale, 18f);
            Object.DestroyImmediate(tmp);

            // a few individually readable droplets flying out of the opening (deterministic)
            Vector3 opening = rig.pot.localPosition + rig.pot.localRotation * new Vector3(0f, 0.9f, 0f) * scale;
            Vector3 up = rig.pot.localRotation * Vector3.up;
            var drops = new GameObject("Droplets").transform;
            drops.SetParent(go.transform, false);
            var rnd = new System.Random(4242);
            const int count = 7;
            for (int i = 0; i < count; i++)
            {
                float az = (i + (float)rnd.NextDouble() * 0.5f) / count * 360f;
                float el = Mathf.Lerp(20f, 55f, (float)rnd.NextDouble());
                Vector3 dir = Quaternion.AngleAxis(az, up) * Quaternion.AngleAxis(el, Vector3.Cross(up, Vector3.forward).normalized) * up;
                if (Vector3.Dot(dir, up) < 0.15f) dir = (dir + up * 0.6f).normalized;
                float dist = Mathf.Lerp(0.5f, 1.15f, (float)rnd.NextDouble()) * scale;
                float size = Mathf.Lerp(0.09f, 0.22f, (float)rnd.NextDouble()) * scale;
                Vector3 pos = opening + dir * dist + Vector3.down * (0.18f * dist * dist);
                Vector3 vel = (dir * 4f + Vector3.down * (2.2f * dist)).normalized;
                var d = MeshChild("Droplet_" + i, drops, drop, dropMat).transform;
                d.localPosition = pos;
                d.localRotation = Quaternion.LookRotation(vel, Vector3.up);
                d.localScale = new Vector3(size, size, size * 1.35f);
            }
            return go;
        }

        static ParticleSystem BuildSplash(Transform parent, Mesh drop, Material mat)
        {
            var go = new GameObject("Splash");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.playOnAwake = false;
            m.loop = false;
            m.duration = 1f;
            m.maxParticles = 12;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.85f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(2.4f, 5.2f);
            m.startSize3D = true;
            m.startSizeX = new ParticleSystem.MinMaxCurve(0.09f, 0.3f);
            m.startSizeY = new ParticleSystem.MinMaxCurve(0.09f, 0.3f);
            m.startSizeZ = new ParticleSystem.MinMaxCurve(0.12f, 0.42f);
            m.startColor = Color.white;
            m.gravityModifier = 1.5f;

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 58f;
            sh.radius = 0.12f;
            sh.rotation = new Vector3(-90f, 0f, 0f);

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.6f), new Keyframe(0.12f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f)));

            var r = (ParticleSystemRenderer)go.GetComponent<Renderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = drop;
            r.alignment = ParticleSystemRenderSpace.Velocity;
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
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
            camGo.transform.position = new Vector3(3.5f, 6.8f, -11.5f);
            camGo.transform.LookAt(new Vector3(4f, 1.4f, 0.5f));

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
                brig.transform.SetPositionAndRotation(new Vector3(-1f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
                foreach (var mb in brig.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
                var rb = brig.GetComponent<Rigidbody>();
                if (rb) rb.isKinematic = true;
            }

            // gameplay instance: lobs pots at the crowd
            var weapon = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            weapon.name = "Ink Pot (gameplay)";
            if (brig) weapon.transform.SetParent(brig.transform, false);
            else weapon.transform.position = new Vector3(-1f, 0f, 0f);

            // hero instance: frozen mid-air burst on a slow turntable, close to camera
            var hero = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            hero.name = "Ink Pot (hero pose)";
            hero.transform.position = new Vector3(0.6f, 2.3f, -5.2f);
            var hso = new SerializedObject(hero.GetComponent<InkPotVfx>());
            hso.FindProperty("heroPose").boolValue = true;
            hso.FindProperty("heroTime").floatValue = 0.3f;
            hso.FindProperty("potScale").floatValue = 1.35f;
            hso.FindProperty("autoFire").boolValue = false;
            hso.ApplyModifiedPropertiesWithoutUndo();

            var dummyMat = GetOrCreateMaterial("InkPot_DemoDummy", Shader.Find("Universal Render Pipeline/Lit"));
            dummyMat.SetColor("_BaseColor", new Color(0.55f, 0.18f, 0.12f));
            EditorUtility.SetDirty(dummyMat);
            var crowds = new GameObject("Dummy Enemies").transform;
            SpawnCrowd(crowds, new Vector3(7.5f, 0f, 1.5f), 5, 1.8f, dummyMat, 3);

            EditorSceneManager.SaveScene(scene, DemoScenePath);
            if (prevActive.IsValid() && prevActive.isLoaded)
            {
                EditorSceneManager.SetActiveScene(prevActive);
                EditorSceneManager.CloseScene(scene, true);
            }
            Debug.Log("[InkPot] Demo scene saved at " + DemoScenePath);
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

        // =========================================================== meshes

        static float[] ProfileV(Vector2[] prof)
        {
            var v = new float[prof.Length];
            float total = 0f;
            for (int i = 1; i < prof.Length; i++) { total += Vector2.Distance(prof[i], prof[i - 1]); v[i] = total; }
            for (int i = 0; i < v.Length; i++) v[i] /= total;
            return v;
        }

        /// <summary>
        /// Lathe a (radius, height) profile around Y for segments seg0..seg1 (inclusive) of <paramref name="segs"/>.
        /// Each segment gets its own vertices so the sides read as flat glass facets. With <paramref name="chamferedSquare"/>
        /// the 8 columns sit at 45°±12° so rings with squareness 1 form a square with small chamfered corners; profile
        /// radius is then the distance to the flat face. Points with radius 0 become poles (fans).
        /// UV.x = angle 0..1, UV.y = profile length 0..1.
        /// </summary>
        static Mesh Lathe(Vector2[] prof, float[] profV, float[] squareness, int segs, int seg0, int seg1, bool chamferedSquare)
        {
            int rings = prof.Length;
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var cols = new List<Color>();
            var tris = new List<int>();

            var n2 = new Vector2[rings];
            for (int k = 0; k < rings; k++)
            {
                Vector2 t = prof[Mathf.Min(k + 1, rings - 1)] - prof[Mathf.Max(k - 1, 0)];
                n2[k] = new Vector2(t.y, -t.x).normalized;
            }

            System.Func<int, float> angleOf = col =>
            {
                if (!chamferedSquare) return col / (float)segs * Mathf.PI * 2f;
                int corner = col / 2;
                float deg = 90f * corner + 45f + (col % 2 == 0 ? -12f : 12f);
                return deg * Mathf.Deg2Rad;
            };
            System.Func<int, int, Vector3> pointOf = (k, col) =>
            {
                float a = angleOf(col);
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                float sq = squareness != null ? squareness[k] : 0f;
                float f = Mathf.Lerp(1f, 1f / Mathf.Max(Mathf.Abs(ca), Mathf.Abs(sa)), sq);
                float r = prof[k].x * f;
                return new Vector3(ca * r, prof[k].y, sa * r);
            };

            for (int j = seg0; j <= seg1; j++)
            {
                int baseIdx = verts.Count;
                float amid = (angleOf(j) + angleOf(j + 1)) * 0.5f;
                Vector3 mid = new Vector3(Mathf.Cos(amid), 0f, Mathf.Sin(amid));
                bool chamfer = chamferedSquare && j % 2 == 0;
                var tint = chamferedSquare ? (chamfer ? Color.white : new Color(0.8f, 0.8f, 0.84f, 1f)) : Color.white;
                for (int k = 0; k < rings; k++)
                {
                    Vector3 p0 = pointOf(k, j), p1 = pointOf(k, j + 1);
                    Vector3 edge = p1 - p0; edge.y = 0f;
                    Vector3 face = edge.sqrMagnitude > 1e-8f ? new Vector3(edge.z, 0f, -edge.x).normalized : mid;
                    if (Vector3.Dot(face, mid) < 0f) face = -face;
                    Vector3 n = (face * n2[k].x + Vector3.up * n2[k].y).normalized;
                    if (prof[k].x <= 1e-5f) { p0 = p1 = new Vector3(0f, prof[k].y, 0f); }
                    verts.Add(p0); verts.Add(p1);
                    norms.Add(n); norms.Add(n);
                    uvs.Add(new Vector2(j / (float)segs, profV[k])); uvs.Add(new Vector2((j + 1) / (float)segs, profV[k]));
                    cols.Add(tint); cols.Add(tint);
                }
                for (int k = 0; k < rings - 1; k++)
                {
                    bool poleA = prof[k].x <= 1e-5f, poleB = prof[k + 1].x <= 1e-5f;
                    int a = baseIdx + k * 2, b = a + 1, d0 = baseIdx + (k + 1) * 2, d1 = d0 + 1;
                    if (poleA) { tris.Add(a); tris.Add(d0); tris.Add(d1); }
                    else if (poleB) { tris.Add(a); tris.Add(d0); tris.Add(b); }
                    else
                    {
                        tris.Add(a); tris.Add(d0); tris.Add(b);
                        tris.Add(b); tris.Add(d0); tris.Add(d1);
                    }
                }
            }

            FixWinding(verts, norms, tris);
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Fat glossy ink gush with a 6-point splash crown, origin at the pot opening.</summary>
        static Mesh BuildInkGush()
        {
            const int segs = 14;
            // (radius, height) along the gush; crown at the top ring
            var prof = new[]
            {
                new Vector2(0.17f, 0f), new Vector2(0.19f, 0.1f), new Vector2(0.21f, 0.22f),
                new Vector2(0.24f, 0.32f), new Vector2(0.29f, 0.4f),
            };
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int k = 0; k < prof.Length; k++)
            for (int s = 0; s < segs; s++)
            {
                float a = s / (float)segs * Mathf.PI * 2f;
                float crown = k == prof.Length - 1 ? Mathf.Pow(Mathf.Max(0f, Mathf.Cos(a * 3f)), 3f) : 0f;
                float bulge = k > 0 && k < prof.Length - 1 ? Mathf.Sin(a * 2f + k) * 0.02f : 0f;
                float r = prof[k].x + crown * 0.1f + bulge;
                float y = prof[k].y + crown * 0.16f;
                verts.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
            }
            for (int k = 0; k < prof.Length - 1; k++)
            for (int s = 0; s < segs; s++)
            {
                int a = k * segs + s, b = k * segs + (s + 1) % segs, c = a + segs, d = b + segs;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
            // concave top cap (ink surface dipping into the pot)
            int top = verts.Count;
            verts.Add(new Vector3(0f, 0.3f, 0f));
            int last = (prof.Length - 1) * segs;
            for (int s = 0; s < segs; s++) { tris.Add(last + s); tris.Add(top); tris.Add(last + (s + 1) % segs); }

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetColors(Whites(verts.Count));
            mesh.SetUVs(0, new List<Vector2>(new Vector2[verts.Count]));
            mesh.RecalculateNormals();
            EnsureOutward(mesh, new Vector3(0f, 0.2f, 0f));
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Teardrop droplet, round head along +Z (velocity), tail toward -Z. Unit-ish size.</summary>
        static Mesh BuildDroplet()
        {
            const int lon = 8, lat = 5;
            var verts = new List<Vector3> { new Vector3(0f, 0f, 0.5f) };
            for (int i = 1; i < lat; i++)
            {
                float th = i / (float)lat * Mathf.PI;
                for (int j = 0; j < lon; j++)
                {
                    float ph = j / (float)lon * Mathf.PI * 2f;
                    var p = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Sin(th) * Mathf.Sin(ph), Mathf.Cos(th)) * 0.5f;
                    if (p.z < 0f)
                    {
                        float t = -p.z / 0.5f;
                        p.x *= 1f - 0.5f * t * t;
                        p.y *= 1f - 0.5f * t * t;
                        p.z *= 1.7f;
                    }
                    verts.Add(p);
                }
            }
            verts.Add(new Vector3(0f, 0f, -0.5f * 1.9f));
            int tail = verts.Count - 1;
            var tris = new List<int>();
            for (int j = 0; j < lon; j++) { tris.Add(0); tris.Add(1 + j); tris.Add(1 + (j + 1) % lon); }
            for (int i = 0; i < lat - 2; i++)
            for (int j = 0; j < lon; j++)
            {
                int a = 1 + i * lon + j, b = 1 + i * lon + (j + 1) % lon, c = a + lon, d = b + lon;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
            int lastRing = 1 + (lat - 2) * lon;
            for (int j = 0; j < lon; j++) { tris.Add(lastRing + j); tris.Add(tail); tris.Add(lastRing + (j + 1) % lon); }

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetColors(Whites(verts.Count));
            mesh.SetUVs(0, new List<Vector2>(new Vector2[verts.Count]));
            mesh.RecalculateNormals();
            EnsureOutward(mesh, Vector3.zero);
            mesh.RecalculateBounds();
            return mesh;
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
            var mesh = new Mesh();
            mesh.vertices = v;
            mesh.uv = uv;
            mesh.normals = n;
            mesh.triangles = t;
            mesh.RecalculateBounds();
            return mesh;
        }

        static void FixWinding(List<Vector3> v, List<Vector3> n, List<int> t)
        {
            float score = 0f;
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector3 fn = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                score += Vector3.Dot(fn, n[t[i]] + n[t[i + 1]] + n[t[i + 2]]);
            }
            if (score >= 0f) return;
            for (int i = 0; i < t.Count; i += 3) { int x = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = x; }
        }

        static void EnsureOutward(Mesh m, Vector3 center)
        {
            var v = m.vertices; var n = m.normals;
            float score = 0f;
            for (int i = 0; i < v.Length; i++) score += Vector3.Dot(n[i], v[i] - center);
            if (score >= 0f) return;
            var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3) { int x = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = x; }
            m.triangles = t;
            m.RecalculateNormals();
        }

        static List<Color> Whites(int n)
        {
            var l = new List<Color>(n);
            for (int i = 0; i < n; i++) l.Add(Color.white);
            return l;
        }

        // =========================================================== hand-painted textures

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        /// <summary>Smooth value noise, tileable in x with period px.</summary>
        static float Noise(float x, float y, int px, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            int xa = ((x0 % px) + px) % px, xb = (xa + 1) % px;
            float a = Hash(xa, y0, seed), b = Hash(xb, y0, seed), c = Hash(xa, y0 + 1, seed), d = Hash(xb, y0 + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Fbm(float u, float v, int baseFreq, int seed)
        {
            float s = 0f, amp = 0.5f, norm = 0f;
            int f = baseFreq;
            for (int o = 0; o < 3; o++)
            {
                s += Noise(u * f, v * f, f, seed + o * 17) * amp;
                norm += amp; amp *= 0.5f; f *= 2;
            }
            return s / norm;
        }

        /// <summary>GLSL-style smoothstep(edge0, edge1, x).</summary>
        static float SS(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        static Texture2D BuildCorkTexture()
        {
            const int w = 64, h = 64;
            var px = new Color[w * h];
            var baseC = new Color(0.74f, 0.56f, 0.36f);
            var dark = new Color(0.45f, 0.31f, 0.18f);
            var light = new Color(0.86f, 0.7f, 0.48f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w, v = y / (float)(h - 1);
                float n = Fbm(u, v, 5, 41);
                var c = Color.Lerp(baseC, light, SS(0.45f, 0.8f, n) * 0.6f);
                float pore = Hash(x / 3, y / 3, 77);
                if (pore > 0.86f) c = Color.Lerp(c, dark, 0.7f);
                c = Color.Lerp(c, light, SS(0.8f, 0.95f, v) * 0.5f); // lighter cut top
                c.a = 1f;
                px[y * w + x] = c;
            }
            return SaveTexture("InkPot_Cork_BaseColor", w, h, px, TextureWrapMode.Repeat);
        }

        static Texture2D SaveTexture(string name, int w, int h, Color[] px, TextureWrapMode wrap)
        {
            string path = $"{TexDir}/{name}.png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapModeU = wrap;
            imp.wrapModeV = TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.sRGBTexture = true;
            imp.maxTextureSize = 256;
            imp.textureCompression = TextureImporterCompression.Compressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // =========================================================== assets

        static GameObject MeshChild(string name, Transform parent, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            if (mesh) mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        static Material ToonMat(string name, Shader shader, Texture2D tex, Color tint, Color shade, float glossSize,
            float glossIntensity, Color gloss, Color rim, float rimPower, float rimIntensity, Color inside)
        {
            var m = GetOrCreateMaterial(name, shader);
            m.SetTexture("_BaseMap", tex ? tex : Texture2D.whiteTexture);
            if (!tex) m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", tint);
            m.SetColor("_ShadeColor", shade);
            m.SetFloat("_GlossSize", glossSize);
            m.SetFloat("_GlossIntensity", glossIntensity);
            m.SetColor("_GlossColor", gloss);
            m.SetColor("_RimColor", rim);
            m.SetFloat("_RimPower", rimPower);
            m.SetFloat("_RimIntensity", rimIntensity);
            m.SetColor("_BackfaceColor", inside);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

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
    }
}
