using System.Collections.Generic;
using HighSeas.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HighSeas.EditorTools
{
    /// <summary>
    /// Generates the Anchor Flail art (anchor + chain-link meshes, pink-glow materials, hook VFX)
    /// and assembles the drag-and-drop prefab + a preview scene. Re-run any time to rebuild.
    /// </summary>
    public static class AnchorFlailBuilder
    {
        const string Root = "Assets/GameObject/Guns/AnchorFlail";
        const string MeshDir = Root + "/Meshes";
        const string MatDir = Root + "/Materials";
        public const string PrefabPath = "Assets/GameObject/Guns/Anchor Flail.prefab";
        public const string DemoScenePath = Root + "/AnchorFlail_Demo.unity";

        // ---- palette (pink glow, dark iron) ----
        static readonly Color IronBase = new(0.30f, 0.27f, 0.34f);
        static readonly Color IronEmission = new(0.1f, 0.018f, 0.08f);
        static readonly Color ChainBase = new(0.56f, 0.4f, 0.56f);
        static readonly Color ChainEmission = new(0.42f, 0.08f, 0.33f);
        static readonly Color GlowPink = new(1f, 0.28f, 0.82f);
        static readonly Color GlowSoft = new(0.85f, 0.35f, 0.75f);

        // anchor proportions (built upright, Y up, then laid flat)
        const float CrownSpike = 0.22f;
        const float RingCenterY = 2.3f;
        const float RingRadius = 0.2f;
        const float RingTube = 0.06f;

        // sizing — anchor ≈ 2 m long next to the ~8 m Brigg
        const float AnchorScale = 0.66f;
        const float WrapLinkScale = 1.5f;
        const float LinkPitch = 0.2f; // link inner length → links interlock exactly

        // rig placement relative to the Brigg root (bow, starboard side)
        static readonly Vector3 ChainOriginLocal = new(0.62f, 1.2f, 2.0f);
        static readonly Vector3 RestLocalPos = new(1.4f, 0.35f, 0.35f);
        static readonly Vector3 RestLocalFwd = new(0.3f, -0.5f, -1f);
        static readonly Vector3 RestLocalUp = new(1f, 0.8f, 0f);
        public const string BrigPrefabPath = "Assets/GameObject/Brigg.prefab";

        [MenuItem("Tools/Rogue Pirates/Anchor Flail/Build Prefab")]
        public static void BuildPrefab()
        {
            EnsureFolders();
            var anchorMesh = SaveMesh(BuildAnchorMesh(), "AnchorFlail_Anchor");
            var anchorGlowMesh = SaveMesh(SmoothedCopy(anchorMesh, "AnchorFlail_Anchor_Glow"), "AnchorFlail_Anchor_Glow");
            var linkMesh = SaveMesh(BuildLinkMesh(), "AnchorFlail_ChainLink");
            var linkGlowMesh = SaveMesh(SmoothedCopy(linkMesh, "AnchorFlail_ChainLink_Glow"), "AnchorFlail_ChainLink_Glow");
            var wrapMesh = SaveMesh(BuildWrapMesh(linkMesh, LayFlat), "AnchorFlail_ChainWrap");
            var wrapGlowMesh = SaveMesh(BuildWrapMesh(linkGlowMesh, LayFlat), "AnchorFlail_ChainWrap_Glow");

            var iron = LitMaterial("AnchorFlail_Iron", IronBase, 0.6f, 0.45f, IronEmission);
            var chain = LitMaterial("AnchorFlail_Chain", ChainBase, 0.25f, 0.8f, ChainEmission);
            var anchorGlow = GlowMaterial("AnchorFlail_AnchorGlow", GlowSoft, 0.035f, 2.5f, 0.6f, 0f);
            var chainGlow = GlowMaterial("AnchorFlail_ChainGlow", GlowSoft, 0.018f, 2.3f, 0.52f, 0f);
            var trailMat = AdditiveParticleMaterial("AnchorFlail_Trail", "Assets/Texture2D/line.png", new Color(GlowSoft.r, GlowSoft.g, GlowSoft.b, 0.45f));
            var sparkMat = AdditiveParticleMaterial("AnchorFlail_Spark", "Assets/Texture2D/glow.png", new Color(GlowSoft.r, GlowSoft.g, GlowSoft.b, 0.6f));
            var ringMat = AdditiveParticleMaterial("AnchorFlail_Ring", "Assets/_Game/VFX/Healing/Textures/T_Heal_Ring.png", new Color(GlowSoft.r, GlowSoft.g, GlowSoft.b, 0.5f));

            var root = new GameObject("Anchor Flail");
            try
            {
                var flail = root.AddComponent<AnchorFlailVfx>();

                var origin = new GameObject("ChainOrigin").transform;
                origin.SetParent(root.transform, false);
                origin.localPosition = ChainOriginLocal;

                var anchor = new GameObject("Anchor").transform;
                anchor.SetParent(root.transform, false);
                var restPos = RestLocalPos;
                var restFwd = RestLocalFwd;
                anchor.localPosition = restPos;
                anchor.localRotation = Quaternion.LookRotation(restFwd.normalized, RestLocalUp.normalized);
                anchor.localScale = Vector3.one * AnchorScale;

                var model = new GameObject("Model");
                model.transform.SetParent(anchor, false);
                model.AddComponent<MeshFilter>().sharedMesh = anchorMesh;
                var mr = model.AddComponent<MeshRenderer>();
                mr.sharedMaterial = iron;
                mr.shadowCastingMode = ShadowCastingMode.On;

                var glow = new GameObject("Glow");
                glow.transform.SetParent(anchor, false);
                glow.AddComponent<MeshFilter>().sharedMesh = anchorGlowMesh;
                var gr = glow.AddComponent<MeshRenderer>();
                gr.sharedMaterial = anchorGlow;
                gr.shadowCastingMode = ShadowCastingMode.Off;
                gr.receiveShadows = false;

                var wrap = new GameObject("ChainWrap");
                wrap.transform.SetParent(anchor, false);
                wrap.AddComponent<MeshFilter>().sharedMesh = wrapMesh;
                var wr = wrap.AddComponent<MeshRenderer>();
                wr.sharedMaterial = chain;
                wr.shadowCastingMode = ShadowCastingMode.On;

                var wrapGlow = new GameObject("ChainWrapGlow");
                wrapGlow.transform.SetParent(anchor, false);
                wrapGlow.AddComponent<MeshFilter>().sharedMesh = wrapGlowMesh;
                var wgr = wrapGlow.AddComponent<MeshRenderer>();
                wgr.sharedMaterial = chainGlow;
                wgr.shadowCastingMode = ShadowCastingMode.Off;
                wgr.receiveShadows = false;

                var attach = new GameObject("ChainAttach").transform;
                attach.SetParent(anchor, false);
                attach.localPosition = new Vector3(0f, 0f, -(RingCenterY + CrownSpike + RingRadius * 0.6f));

                var trail = BuildTrail(anchor, trailMat);
                var burst = BuildHookBurst(root.transform, sparkMat, ringMat);

                var so = new SerializedObject(flail);
                so.FindProperty("anchor").objectReferenceValue = anchor;
                so.FindProperty("chainAttach").objectReferenceValue = attach;
                so.FindProperty("chainOrigin").objectReferenceValue = origin;
                so.FindProperty("linkMesh").objectReferenceValue = linkMesh;
                so.FindProperty("linkGlowMesh").objectReferenceValue = linkGlowMesh;
                so.FindProperty("linkMaterial").objectReferenceValue = chain;
                so.FindProperty("linkGlowMaterial").objectReferenceValue = chainGlow;
                so.FindProperty("hitSparks").objectReferenceValue = burst;
                so.FindProperty("hitRing").objectReferenceValue = burst.transform.Find("Ring").GetComponent<ParticleSystem>();
                so.FindProperty("lashTrail").objectReferenceValue = trail;
                so.FindProperty("restLocalPosition").vector3Value = restPos;
                so.FindProperty("restLocalForward").vector3Value = restFwd;
                so.FindProperty("restLocalUp").vector3Value = RestLocalUp;
                so.FindProperty("restSag").floatValue = 0.2f;
                so.FindProperty("linkPitch").floatValue = LinkPitch;
                so.FindProperty("linkScale").floatValue = WrapLinkScale * AnchorScale;
                so.FindProperty("sweepWidth").floatValue = 2.2f;
                so.FindProperty("whipBend").floatValue = 0.8f;
                so.FindProperty("fireWithoutTargets").boolValue = true; // swings even with no enemies around
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[AnchorFlail] Prefab built at {PrefabPath}");
        }

        [MenuItem("Tools/Rogue Pirates/Anchor Flail/Build Demo Scene")]
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
            light.color = new Color(1f, 0.96f, 0.9f);
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 45f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.09f, 0.2f);
            camGo.transform.position = new Vector3(0f, 24f, -16f);
            camGo.transform.LookAt(new Vector3(0f, 0f, 1f));

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.36f, 0.5f);

            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Water";
            water.transform.localScale = new Vector3(8f, 1f, 8f);
            water.GetComponent<Renderer>().sharedMaterial = LitMaterial("AnchorFlail_DemoWater", new Color(0.09f, 0.22f, 0.45f), 0f, 0.6f, Color.black);

            // Brigg as the owner ship (preview only: no agent / physics).
            var brigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BrigPrefabPath);
            var brig = (GameObject)PrefabUtility.InstantiatePrefab(brigPrefab);
            brig.name = "Brigg (preview)";
            var agent = brig.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) Object.DestroyImmediate(agent);
            var body = brig.GetComponent<Rigidbody>();
            if (body != null) body.isKinematic = true;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var flail = (GameObject)PrefabUtility.InstantiatePrefab(prefab, brig.transform);
            flail.transform.localPosition = Vector3.zero;
            flail.transform.localRotation = Quaternion.identity;
            var so = new SerializedObject(flail.GetComponent<AnchorFlailVfx>());
            so.FindProperty("fireWithoutTargets").boolValue = true; // no enemies in the preview → strike in rotating directions
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, DemoScenePath);
            Debug.Log($"[AnchorFlail] Demo scene saved at {DemoScenePath}");
        }

        // =====================================================================================
        // Anchor mesh
        // =====================================================================================

        static Mesh BuildAnchorMesh()
        {
            var mb = new MeshBuilder();

            // Shaft: chunky, slightly tapered toward the head.
            mb.Tube(new[] { new Vector3(0f, 0.05f, 0f), new Vector3(0f, 1.0f, 0f), new Vector3(0f, 2.05f, 0f) },
                new[] { 0.18f, 0.15f, 0.125f }, 12, Vector3.forward, true, true);

            // Crown hub where the arms meet.
            mb.Sphere(new Vector3(0f, 0.1f, 0f), new Vector3(0.24f, 0.24f, 0.19f), 12, 8);

            // Crown spike (points down → becomes the leading bite point).
            mb.BladePrism(new[]
            {
                new Vector2(0f, -CrownSpike), new Vector2(0.16f, 0.02f), new Vector2(0f, 0.12f), new Vector2(-0.16f, 0.02f)
            }, Matrix4x4.identity, 0.09f, new Vector2(0f, -0.02f), 0.5f);

            // Arms: crescent sweep, thick at the crown and tapering to the flukes.
            const float armR = 1.0f;
            var armCenter = new Vector3(0f, armR, 0f);
            const int armSteps = 14;
            const float armMaxDeg = 74f;
            for (var side = -1; side <= 1; side += 2)
            {
                var pts = new Vector3[armSteps + 1];
                var radii = new float[armSteps + 1];
                for (var i = 0; i <= armSteps; i++)
                {
                    var k = i / (float)armSteps;
                    var a = k * armMaxDeg * Mathf.Deg2Rad;
                    pts[i] = armCenter + new Vector3(side * Mathf.Sin(a) * armR, -Mathf.Cos(a) * armR, 0f);
                    radii[i] = Mathf.Lerp(0.16f, 0.09f, k);
                }
                mb.Tube(pts, radii, 10, Vector3.forward, false, true);

                // Fluke: arrowhead blade at the arm tip, pointing along the arm, barbs swept back.
                var tipA = armMaxDeg * Mathf.Deg2Rad;
                var tip = armCenter + new Vector3(side * Mathf.Sin(tipA) * armR, -Mathf.Cos(tipA) * armR, 0f);
                var tangent = new Vector3(side * Mathf.Cos(tipA), Mathf.Sin(tipA), 0f).normalized;
                var basis = Matrix4x4.TRS(tip - tangent * 0.12f, Quaternion.LookRotation(Vector3.forward, tangent), Vector3.one);
                // 2D: x across the arm, y along the arm (outward)
                mb.BladePrism(new[]
                {
                    new Vector2(0f, 0.6f),
                    new Vector2(0.33f, -0.24f),
                    new Vector2(0.1f, -0.07f),
                    new Vector2(-0.1f, -0.07f),
                    new Vector2(-0.33f, -0.24f)
                }, basis, 0.13f, new Vector2(0f, 0.06f), 0.45f);
            }

            // Stock: metal bar with knobbed ends (across the shaft, near the head).
            const float stockY = 1.72f;
            mb.Tube(new[] { new Vector3(-0.66f, stockY, 0f), new Vector3(0.66f, stockY, 0f) }, new[] { 0.085f, 0.085f }, 10, Vector3.forward, false, false);
            for (var side = -1; side <= 1; side += 2)
            {
                mb.Sphere(new Vector3(side * 0.6f, stockY, 0f), new Vector3(0.12f, 0.12f, 0.12f), 10, 8);
                mb.Sphere(new Vector3(side * 0.74f, stockY, 0f), new Vector3(0.08f, 0.1f, 0.1f), 10, 8);
            }
            mb.Sphere(new Vector3(0f, stockY, 0f), new Vector3(0.14f, 0.12f, 0.14f), 10, 8);

            // Head collar + shackle ring.
            mb.Sphere(new Vector3(0f, 2.08f, 0f), new Vector3(0.13f, 0.1f, 0.13f), 10, 8);
            mb.Torus(new Vector3(0f, RingCenterY, 0f), RingRadius, RingTube, 0f, Vector3.forward, 20, 8);

            var mesh = mb.ToMesh("AnchorFlail_Anchor");

            // Lay flat for the top-down camera: +Y (ring) → -Z, crown spike tip becomes the pivot at origin.
            TransformMesh(mesh, LayFlat);
            // Pivot sits at the crown spike tip: after rotation the tip is at z = +CrownSpike; shifted to 0.
            return mesh;
        }

        static Matrix4x4 LayFlat => Matrix4x4.Translate(new Vector3(0f, 0f, -CrownSpike)) * Matrix4x4.Rotate(Quaternion.Euler(-90f, 0f, 0f));

        /// <summary>Decorative chain coiled down the shank from the ring (like the references), baked into one mesh.</summary>
        static Mesh BuildWrapMesh(Mesh link, Matrix4x4 layFlat)
        {
            const float pitch = LinkPitch;
            const float scale = WrapLinkScale;
            // helix samples: ring → down the shank → swings across the crown
            var pts = new List<Vector3>();
            const int samples = 220;
            for (var i = 0; i <= samples; i++)
            {
                var k = i / (float)samples;
                var y = Mathf.Lerp(RingCenterY - 0.1f, 0.38f, k);
                var turns = 1.45f;
                var a = k * turns * Mathf.PI * 2f + 0.4f;
                var r = Mathf.Lerp(0.3f, 0.38f, Mathf.SmoothStep(0f, 1f, k)) * (1f + 0.08f * Mathf.Sin(k * 17f));
                pts.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r * 0.85f));
            }
            // tail loop: hangs off the crown and out across one arm
            var tail = new[] { new Vector3(0.38f, 0.3f, 0.1f), new Vector3(0.62f, 0.26f, 0.08f), new Vector3(0.8f, 0.4f, 0.02f), new Vector3(0.78f, 0.62f, -0.05f) };
            pts.AddRange(tail);

            var cum = new float[pts.Count];
            for (var i = 1; i < pts.Count; i++) cum[i] = cum[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var tris = new List<int>();
            var lv = link.vertices;
            var ln = link.normals;
            var lt = link.triangles;
            var step = pitch * scale;
            var seg = 1;
            var idx = 0;
            for (var s = step * 0.5f; s < cum[^1]; s += step, idx++)
            {
                while (seg < pts.Count - 1 && cum[seg] < s) seg++;
                var f = Mathf.InverseLerp(cum[seg - 1], cum[seg], s);
                var p = Vector3.Lerp(pts[seg - 1], pts[seg], f);
                var tan = (pts[seg] - pts[seg - 1]).normalized;
                var up = Vector3.ProjectOnPlane(new Vector3(p.x, 0f, p.z), tan);
                if (up.sqrMagnitude < 1e-5f) up = Vector3.forward;
                var rot = Quaternion.LookRotation(tan, up.normalized) * Quaternion.Euler(0f, 0f, (idx & 1) == 0 ? 0f : 90f);
                var m = layFlat * Matrix4x4.TRS(p, rot, Vector3.one * scale);
                var b = verts.Count;
                for (var v = 0; v < lv.Length; v++)
                {
                    verts.Add(m.MultiplyPoint3x4(lv[v]));
                    norms.Add(m.MultiplyVector(ln[v]).normalized);
                }
                for (var tI = 0; tI < lt.Length; tI++) tris.Add(b + lt[tI]);
            }
            var mesh = new Mesh();
            if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh BuildLinkMesh()
        {
            var mb = new MeshBuilder();
            // Stadium-shaped link, long axis Z, flat in XZ. Sized to interlock at ~0.235 pitch.
            mb.Torus(Vector3.zero, 0.085f, 0.045f, 0.06f, Vector3.up, 24, 8);
            return mb.ToMesh("AnchorFlail_ChainLink");
        }

        // =====================================================================================
        // VFX
        // =====================================================================================

        static TrailRenderer BuildTrail(Transform anchor, Material mat)
        {
            var go = new GameObject("LashTrail");
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.6f);
            var tr = go.AddComponent<TrailRenderer>();
            tr.sharedMaterial = mat;
            tr.time = 0.22f;
            tr.minVertexDistance = 0.05f;
            tr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 0f));
            tr.widthMultiplier = 1f;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.75f, 0.97f), 0f), new GradientColorKey(GlowPink, 0.4f), new GradientColorKey(new Color(0.6f, 0.15f, 0.9f), 1f) },
                new[] { new GradientAlphaKey(0.4f, 0f), new GradientAlphaKey(0.2f, 0.4f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;
            tr.alignment = LineAlignment.View;
            tr.shadowCastingMode = ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.emitting = false;
            tr.numCapVertices = 2;
            return tr;
        }

        static ParticleSystem BuildHookBurst(Transform parent, Material sparkMat, Material ringMat)
        {
            var go = new GameObject("HitSparks");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.4f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.42f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.7f, 0.97f), GlowPink);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 24;
            main.gravityModifier = 0f;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
            var vol = ps.limitVelocityOverLifetime;
            vol.enabled = true;
            vol.dampen = 0.25f;
            vol.limit = 1.5f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 2.2f;
            r.velocityScale = 0.04f;
            r.sharedMaterial = sparkMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;

            // Ground ring pop — the clear "grabbed!" read from the top-down camera.
            var ringGo = new GameObject("Ring");
            ringGo.transform.SetParent(go.transform, false);
            var ring = ringGo.AddComponent<ParticleSystem>();
            ring.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var rm = ring.main;
            rm.duration = 0.3f;
            rm.loop = false;
            rm.playOnAwake = false;
            rm.startLifetime = 0.22f;
            rm.startSpeed = 0f;
            rm.startSize = 1f;
            rm.startColor = new Color(0.9f, 0.5f, 0.85f, 0.45f);
            rm.simulationSpace = ParticleSystemSimulationSpace.World;
            rm.maxParticles = 2;
            var rem = ring.emission;
            rem.rateOverTime = 0f;
            rem.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var rshape = ring.shape;
            rshape.enabled = false;
            var rsol = ring.sizeOverLifetime;
            rsol.enabled = true;
            rsol.size = new ParticleSystem.MinMaxCurve(2.8f, new AnimationCurve(new Keyframe(0f, 0.25f, 0f, 3f), new Keyframe(1f, 1f, 0f, 0f)));
            var rcol = ring.colorOverLifetime;
            rcol.enabled = true;
            var rg = new Gradient();
            rg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
            rcol.color = rg;
            var rr = ringGo.GetComponent<ParticleSystemRenderer>();
            rr.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            rr.sharedMaterial = ringMat;
            rr.shadowCastingMode = ShadowCastingMode.Off;
            rr.receiveShadows = false;
            return ps;
        }

        // =====================================================================================
        // Assets
        // =====================================================================================

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets/GameObject/Guns", "AnchorFlail");
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

        static Material GlowMaterial(string name, Color color, float width, float power, float intensity, float core)
        {
            var mat = GetOrCreateMaterial(name, Shader.Find("RoguePirates/AnchorFlailGlow"));
            mat.SetColor("_GlowColor", color);
            mat.SetFloat("_Width", width);
            mat.SetFloat("_Power", power);
            mat.SetFloat("_Intensity", intensity);
            mat.SetFloat("_Core", core);
            mat.SetFloat("_PulseAmount", 0.08f);
            mat.SetFloat("_PulseSpeed", 2f);
            mat.enableInstancing = true;
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
            mat.SetFloat("_Blend", 2f); // additive
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
        // Mesh helpers
        // =====================================================================================

        static void TransformMesh(Mesh mesh, Matrix4x4 m)
        {
            var v = mesh.vertices;
            var n = mesh.normals;
            for (var i = 0; i < v.Length; i++)
            {
                v[i] = m.MultiplyPoint3x4(v[i]);
                n[i] = m.MultiplyVector(n[i]).normalized;
            }
            mesh.vertices = v;
            mesh.normals = n;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
        }

        /// <summary>Copy with normals averaged across split vertices so the glow shell extrudes without cracks.</summary>
        static Mesh SmoothedCopy(Mesh src, string name)
        {
            var v = src.vertices;
            var n = src.normals;
            var sums = new Dictionary<Vector3Int, Vector3>();
            Vector3Int Key(Vector3 p) => new(Mathf.RoundToInt(p.x * 2000f), Mathf.RoundToInt(p.y * 2000f), Mathf.RoundToInt(p.z * 2000f));
            for (var i = 0; i < v.Length; i++)
            {
                var k = Key(v[i]);
                sums.TryGetValue(k, out var s);
                sums[k] = s + n[i];
            }
            var outN = new Vector3[v.Length];
            for (var i = 0; i < v.Length; i++)
                outN[i] = sums[Key(v[i])].normalized;
            var mesh = new Mesh { name = name };
            mesh.vertices = v;
            mesh.normals = outN;
            mesh.triangles = src.triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        sealed class MeshBuilder
        {
            readonly List<Vector3> _v = new();
            readonly List<Vector3> _n = new();
            readonly List<int> _t = new();

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                if (_v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
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
                _n.Add(n.normalized);
                return _v.Count - 1;
            }

            void Tri(int a, int b, int c) { _t.Add(a); _t.Add(b); _t.Add(c); }

            /// <summary>Swept circle along a polyline. refAxis must not be parallel to the path.</summary>
            public void Tube(Vector3[] path, float[] radii, int seg, Vector3 refAxis, bool capStart, bool capEnd)
            {
                var rings = new int[path.Length];
                for (var i = 0; i < path.Length; i++)
                {
                    var tan = i == 0 ? path[1] - path[0]
                        : i == path.Length - 1 ? path[i] - path[i - 1]
                        : path[i + 1] - path[i - 1];
                    tan.Normalize();
                    var b = Vector3.ProjectOnPlane(refAxis, tan);
                    if (b.sqrMagnitude < 1e-6f) b = Vector3.ProjectOnPlane(Vector3.right, tan);
                    b.Normalize();
                    var c = Vector3.Cross(tan, b);
                    rings[i] = _v.Count;
                    for (var s = 0; s <= seg; s++)
                    {
                        var a = s / (float)seg * Mathf.PI * 2f;
                        var dir = b * Mathf.Cos(a) + c * Mathf.Sin(a);
                        Add(path[i] + dir * radii[i], dir);
                    }
                }
                for (var i = 0; i < path.Length - 1; i++)
                for (var s = 0; s < seg; s++)
                {
                    var a0 = rings[i] + s;
                    var a1 = rings[i] + s + 1;
                    var b0 = rings[i + 1] + s;
                    var b1 = rings[i + 1] + s + 1;
                    Tri(a0, a1, b0);
                    Tri(a1, b1, b0);
                }
                if (capStart) Cap(path[0], path[0] - path[1], rings[0], seg, radii[0], true);
                if (capEnd) Cap(path[^1], path[^1] - path[^2], rings[path.Length - 1], seg, radii[^1], false);
            }

            void Cap(Vector3 center, Vector3 outward, int ringStart, int seg, float r, bool reverse)
            {
                outward.Normalize();
                // little dome so ends read rounded
                var tip = Add(center + outward * r * 0.6f, outward);
                var first = _v.Count;
                for (var s = 0; s <= seg; s++)
                {
                    var p = _v[ringStart + s];
                    var dir = (p - center).normalized;
                    Add(p, (dir + outward).normalized);
                }
                for (var s = 0; s < seg; s++)
                {
                    if (reverse) Tri(tip, first + s + 1, first + s);
                    else Tri(tip, first + s, first + s + 1);
                }
            }

            public void Sphere(Vector3 c, Vector3 radii, int lon, int lat)
            {
                var start = _v.Count;
                for (var y = 0; y <= lat; y++)
                {
                    var v = y / (float)lat;
                    var phi = v * Mathf.PI;
                    for (var x = 0; x <= lon; x++)
                    {
                        var u = x / (float)lon;
                        var th = u * Mathf.PI * 2f;
                        var d = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                        var p = Vector3.Scale(d, radii);
                        var n = new Vector3(d.x / radii.x, d.y / radii.y, d.z / radii.z);
                        Add(c + p, n);
                    }
                }
                for (var y = 0; y < lat; y++)
                for (var x = 0; x < lon; x++)
                {
                    var a = start + y * (lon + 1) + x;
                    var b = a + lon + 1;
                    Tri(a, a + 1, b);
                    Tri(a + 1, b + 1, b);
                }
            }

            /// <summary>Torus / stadium ring. Lies in the plane perpendicular to <paramref name="normal"/>; straightHalf stretches it along the in-plane long axis.</summary>
            public void Torus(Vector3 c, float radius, float tube, float straightHalf, Vector3 normal, int majorSeg, int minorSeg)
            {
                normal.Normalize();
                var longAxis = Mathf.Abs(Vector3.Dot(normal, Vector3.forward)) > 0.9f ? Vector3.up : Vector3.forward;
                longAxis = Vector3.ProjectOnPlane(longAxis, normal).normalized;
                var wideAxis = Vector3.Cross(normal, longAxis).normalized;
                var start = _v.Count;
                for (var i = 0; i <= majorSeg; i++)
                {
                    var a = i / (float)majorSeg * Mathf.PI * 2f;
                    var outDir = wideAxis * Mathf.Cos(a) + longAxis * Mathf.Sin(a);
                    var center = c + outDir * radius + longAxis * (Mathf.Sign(Mathf.Sin(a)) * straightHalf);
                    for (var j = 0; j <= minorSeg; j++)
                    {
                        var b = j / (float)minorSeg * Mathf.PI * 2f;
                        var dir = outDir * Mathf.Cos(b) + normal * Mathf.Sin(b);
                        Add(center + dir * tube, dir);
                    }
                }
                for (var i = 0; i < majorSeg; i++)
                for (var j = 0; j < minorSeg; j++)
                {
                    var a0 = start + i * (minorSeg + 1) + j;
                    var b0 = a0 + minorSeg + 1;
                    Tri(a0, a0 + 1, b0);
                    Tri(a0 + 1, b0 + 1, b0);
                }
            }

            /// <summary>
            /// Flat star-shaped polygon (fan from vertex 0) extruded into a diamond-section blade:
            /// full outline at the mid plane, shrunken faces on both sides → sharp, highlight-catching edges.
            /// </summary>
            public void BladePrism(Vector2[] poly, Matrix4x4 xf, float thickness, Vector2 center, float faceScale)
            {
                var n = poly.Length;
                var rim = new Vector3[n];
                var top = new Vector3[n];
                var bot = new Vector3[n];
                var h = thickness * 0.5f;
                for (var i = 0; i < n; i++)
                {
                    var p = poly[i];
                    var inner = center + (p - center) * faceScale;
                    rim[i] = xf.MultiplyPoint3x4(new Vector3(p.x, p.y, 0f));
                    top[i] = xf.MultiplyPoint3x4(new Vector3(inner.x, inner.y, h));
                    bot[i] = xf.MultiplyPoint3x4(new Vector3(inner.x, inner.y, -h));
                }
                var up = xf.MultiplyVector(Vector3.forward).normalized;
                var mid = xf.MultiplyPoint3x4(new Vector3(center.x, center.y, 0f));
                // faces
                for (var i = 1; i < n - 1; i++)
                {
                    FlatTri(top[0], top[i], top[i + 1], up);
                    FlatTri(bot[0], bot[i + 1], bot[i], -up);
                }
                // bevel sides
                for (var i = 0; i < n; i++)
                {
                    var j = (i + 1) % n;
                    FlatQuad(rim[i], rim[j], top[j], top[i], mid);
                    FlatQuad(rim[j], rim[i], bot[i], bot[j], mid);
                }
            }

            void FlatTri(Vector3 a, Vector3 b, Vector3 c, Vector3 hint)
            {
                var n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, hint) < 0f) { (b, c) = (c, b); n = -n; }
                var i0 = Add(a, n);
                var i1 = Add(b, n);
                var i2 = Add(c, n);
                Tri(i0, i1, i2);
            }

            void FlatQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 inside)
            {
                var n = Vector3.Cross(b - a, d - a).normalized;
                if (Vector3.Dot(n, (a + b + c + d) * 0.25f - inside) < 0f)
                {
                    (b, d) = (d, b);
                    n = -n;
                }
                var i0 = Add(a, n);
                var i1 = Add(b, n);
                var i2 = Add(c, n);
                var i3 = Add(d, n);
                Tri(i0, i1, i2);
                Tri(i0, i2, i3);
            }
        }
    }
}
