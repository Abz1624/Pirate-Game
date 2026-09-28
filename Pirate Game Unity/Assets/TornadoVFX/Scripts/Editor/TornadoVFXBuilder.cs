using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using TornadoFX;

/// <summary>
/// One-click generator: Tools > Tornado VFX > Build Prefab.
/// Creates 3 small textures, 5 shared materials, the funnel/swirl meshes and Tornado_VFX.prefab
/// with all 7 particle layers fully configured. Re-running overwrites the generated assets.
/// </summary>
public static class TornadoVFXBuilder
{
    const string Root = "Assets/TornadoVFX";
    const string Gen = Root + "/Generated";
    const string PrefabPath = Root + "/Tornado_VFX.prefab";
    const string ShaderName = "TornadoFX/StylizedParticle";
    const float TAU = Mathf.PI * 2f;

    static readonly List<ParticleSystemVertexStream> Streams = new List<ParticleSystemVertexStream>
    {
        ParticleSystemVertexStream.Position,
        ParticleSystemVertexStream.Normal,
        ParticleSystemVertexStream.Color,
        ParticleSystemVertexStream.UV,
        ParticleSystemVertexStream.Custom1XY,
    };

    // Default dimensions baked into the saved meshes (the controller rebuilds at runtime if changed)
    static readonly TornadoMeshBuilder.Shape DefaultShape = new TornadoMeshBuilder.Shape(7f, 2.0f, 1.0f, 3.5f, 0.12f);

    [MenuItem("Tools/Tornado VFX/Build Prefab")]
    public static void Build()
    {
        var shader = Shader.Find(ShaderName);
        if (shader == null) { Debug.LogError($"[TornadoVFX] Shader '{ShaderName}' not found. Is URP installed and the shader imported?"); return; }

        EnsureFolder(Root); EnsureFolder(Gen);

        // ---------- textures ----------
        var texBody = SaveTexture("T_Tornado_BodyNoise", 256, 256, BodyNoise, TextureWrapMode.Repeat, true);
        var texStreak = SaveTexture("T_Tornado_Streak", 256, 64, Streak, TextureWrapMode.Repeat, true);
        var texAtlas = SaveTexture("T_Tornado_Atlas", 128, 128, Atlas, TextureWrapMode.Clamp, false);

        // ---------- materials (5 shared) ----------
        var mBody = SaveMat("M_Tornado_Body", shader, texBody, m =>
        {
            m.SetColor("_BodyColor", new Color(1f, 0.36f, 0.05f, 0.82f));
            m.SetColor("_HighlightColor", new Color(1.35f, 0.9f, 0.35f, 1f));
            m.SetFloat("_HighlightThreshold", 0.86f);
            m.SetFloat("_HighlightSoftness", 0.06f);
            m.SetFloat("_EdgeSoftness", 0.08f);
            m.SetTextureScale("_MainTex", new Vector2(2f, 1f));
            m.SetVector("_Scroll", new Vector4(0.18f, -0.5f, 0, 0));
            m.SetFloat("_Additive", 0.12f);
            m.SetFloat("_RimAmount", 0.55f);
            m.SetFloat("_CenterOpacity", 0.6f);
            m.SetFloat("_RimPower", 1.6f);
            m.SetFloat("_VFade", 1f);
        });
        var mSwirl = SaveMat("M_Tornado_Swirl", shader, texStreak, m =>
        {
            m.SetColor("_BodyColor", new Color(1f, 0.55f, 0.1f, 0.85f));
            m.SetColor("_HighlightColor", new Color(1.7f, 1.4f, 0.75f, 1f));
            m.SetFloat("_HighlightThreshold", 0.78f);
            m.SetFloat("_EdgeSoftness", 0.05f);
            m.SetFloat("_RandomOffset", 0f);
            m.SetFloat("_Additive", 1f);
        });
        var mTrail = SaveMat("M_Tornado_Trail", shader, texStreak, m =>
        {
            m.SetColor("_BodyColor", new Color(1f, 0.6f, 0.15f, 1f));
            m.SetColor("_HighlightColor", new Color(1.4f, 1.1f, 0.5f, 1f));
            m.SetFloat("_HighlightThreshold", 0.75f);
            m.SetFloat("_RandomOffset", 0f);
            m.SetFloat("_Additive", 1f);
        });
        var mGround = SaveMat("M_Tornado_Ground", shader, texStreak, m =>
        {
            m.SetColor("_BodyColor", new Color(1f, 0.45f, 0.08f, 0.9f));
            m.SetColor("_HighlightColor", new Color(1.5f, 1.15f, 0.55f, 1f));
            m.SetFloat("_HighlightThreshold", 0.8f);
            m.SetFloat("_EdgeSoftness", 0.1f);
            m.SetTextureScale("_MainTex", new Vector2(3f, 0.8f));
            m.SetTextureOffset("_MainTex", new Vector2(0f, -0.12f)); // strokes sit at ~75% of the disc radius
            m.SetVector("_Scroll", new Vector4(0f, 0f, 0, 0));
            m.SetFloat("_Additive", 1f);
            m.SetFloat("_Polar", 1f); m.EnableKeyword("_POLAR");
            m.SetFloat("_PolarTwist", 1.1f); // curved, spiralling streaks
            m.SetVector("_PolarRing", new Vector4(0.2f, 0.25f, 1f, 0.4f));
        });
        var mAtlasAdd = SaveMat("M_Tornado_AtlasAdditive", shader, texAtlas, m =>
        {
            m.SetColor("_BodyColor", new Color(1f, 0.5f, 0.1f, 1f));
            m.SetColor("_HighlightColor", new Color(1.6f, 1.35f, 0.8f, 1f));
            m.SetFloat("_HighlightThreshold", 0.6f);
            m.SetFloat("_HighlightSoftness", 0.25f);
            m.SetFloat("_EdgeSoftness", 0.3f);
            m.SetFloat("_RandomOffset", 0f);
            m.SetFloat("_Additive", 1f);
        });
        var mAtlasAlpha = SaveMat("M_Tornado_AtlasAlpha", shader, texAtlas, m =>
        {
            m.SetColor("_BodyColor", new Color(0.35f, 0.12f, 0.04f, 1f));
            m.SetColor("_HighlightColor", new Color(0.35f, 0.12f, 0.04f, 1f));
            m.SetFloat("_HighlightThreshold", 1.2f);
            m.SetFloat("_EdgeSoftness", 0.15f);
            m.SetFloat("_RandomOffset", 0f);
            m.SetFloat("_Additive", 0f);
        });

        // ---------- meshes ----------
        var funnel = SaveMesh(TornadoMeshBuilder.BuildFunnel(DefaultShape), "Mesh_Tornado_Funnel");
        var swirlMeshes = new[]
        {
            // 3 bands (low / mid / top) that hug the funnel wall; must match TornadoMeshBuilder.GetOrBuild
            SaveMesh(TornadoMeshBuilder.BuildSwirl(DefaultShape, 0.03f, 0.36f, 0.85f, TornadoMeshBuilder.SwirlGap, 11), "Mesh_Tornado_Swirl_A"),
            SaveMesh(TornadoMeshBuilder.BuildSwirl(DefaultShape, 0.30f, 0.64f, 0.80f, TornadoMeshBuilder.SwirlGap, 23), "Mesh_Tornado_Swirl_B"),
            SaveMesh(TornadoMeshBuilder.BuildSwirl(DefaultShape, 0.58f, 0.95f, 0.72f, TornadoMeshBuilder.SwirlGap, 37), "Mesh_Tornado_Swirl_C"),
        };

        // ---------- hierarchy ----------
        var rootGO = new GameObject("Tornado_VFX");
        var ctrl = rootGO.AddComponent<TornadoVFX>();

        ctrl.core         = BuildCore(rootGO.transform, mBody, funnel);
        ctrl.swirls       = BuildSwirls(rootGO.transform, mSwirl, swirlMeshes);
        ctrl.outerRibbons = BuildRibbons(rootGO.transform, mTrail, funnel);
        ctrl.sparks       = BuildSparks(rootGO.transform, mAtlasAdd, funnel);
        ctrl.debris       = BuildDebris(rootGO.transform, mAtlasAlpha);
        ctrl.groundVortex = BuildGround(rootGO.transform, mGround);
        ctrl.glow         = BuildGlow(rootGO.transform, mAtlasAdd);

        var prefab = PrefabUtility.SaveAsPrefabAsset(rootGO, PrefabPath);
        Object.DestroyImmediate(rootGO);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(prefab);
        Selection.activeObject = prefab;
        Debug.Log($"[TornadoVFX] Built {PrefabPath}");
    }

    [MenuItem("Tools/Tornado VFX/Add Pool To Scene")]
    public static void AddPool()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<TornadoVFX>(PrefabPath);
        if (prefab == null) { Debug.LogError("[TornadoVFX] Build the prefab first."); return; }
        var go = new GameObject("TornadoVFX_Pool");
        var pool = go.AddComponent<TornadoVFXPool>();
        pool.prefab = prefab;
        Undo.RegisterCreatedObjectUndo(go, "Add Tornado Pool");
        Selection.activeObject = go;
    }

    // =============================== LAYERS ==================================================

    // 1. CORE: 4-5 overlapping funnel shells, each spinning at its own speed, rising slightly,
    //    dissolving in/out with erosion. Scrolling noise texture + fresnel = bright edges, soft centre.
    static ParticleSystem BuildCore(Transform parent, Material mat, Mesh funnel)
    {
        var ps = NewPS("Tornado_Core", parent, 6, out var r);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.0f);
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(0.96f, 1.03f);
        main.startSizeY = new ParticleSystem.MinMaxCurve(0.97f, 1.03f);
        main.startSizeZ = new ParticleSystem.MinMaxCurve(0.96f, 1.03f);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 0f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, TAU);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, 0f);
        main.startColor = Color.white;

        Emission(ps, 3f);
        Velocity(ps, 0.1f, 0.25f);
        SpinY(ps, 7.5f);
        SizeOverLife(ps, Curve(0f, 0.94f, 0.5f, 1f, 1f, 1.03f));
        ColorOverLife(ps, Grad(Color.white, Color.white), Alpha(0f, 0f, 0.2f, 1f, 0.7f, 1f, 1f, 0f));
        Erosion(ps, Curve(0f, 0.35f, 0.25f, 0.02f, 0.7f, 0.04f, 1f, 0.55f));
        Noise(ps, 0.05f, 0.25f, 0.15f);

        r.renderMode = ParticleSystemRenderMode.Mesh;
        r.mesh = funnel;
        r.alignment = ParticleSystemRenderSpace.Local;
        r.sharedMaterial = mat;
        r.sortingFudge = 0f;
        return ps;
    }

    // 2. SWIRLS: 5-8 torn helical bands (4 mesh variants at different heights), spinning fast,
    //    rising, widening toward the top. These make the strong spiral lines.
    static ParticleSystem BuildSwirls(Transform parent, Material mat, Mesh[] meshes)
    {
        var ps = NewPS("Tornado_Swirls", parent, 5, out var r);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.97f, 1.03f);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 0f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, TAU);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, 0f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.75f), Color.white);

        Emission(ps, 3.2f);
        Velocity(ps, 0.25f, 0.5f);
        SpinY(ps, 12.5f);
        SizeOverLife(ps, Curve(0f, 0.96f, 1f, 1.06f));
        ColorOverLife(ps, Grad(Color.white, Color.white), Alpha(0f, 0f, 0.15f, 1f, 0.65f, 1f, 1f, 0f));
        Erosion(ps, Curve(0f, 0.5f, 0.2f, 0.03f, 0.6f, 0.06f, 1f, 0.8f));
        Noise(ps, 0.04f, 0.3f, 0.2f);

        r.renderMode = ParticleSystemRenderMode.Mesh;
        r.SetMeshes(meshes);
        r.meshDistribution = ParticleSystemMeshDistribution.UniformRandom;
        r.alignment = ParticleSystemRenderSpace.Local;
        r.sharedMaterial = mat;
        r.sortingFudge = -5f;
        return ps;
    }

    // 3. OUTER RIBBONS: 10-20 fast energy trails orbiting on the funnel surface, rising,
    //    some peeling off outward late in life. Particle quads are not rendered - only trails.
    static ParticleSystem BuildRibbons(Transform parent, Material trailMat, Mesh funnel)
    {
        var ps = NewPS("Tornado_Outer_Ribbons", parent, 6, out var r);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.1f);

        Emission(ps, 4f);
        MeshShape(ps, funnel, 0.05f);
        var vel = Velocity(ps, 0.6f, 1.0f);
        vel.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.orbitalY = new ParticleSystem.MinMaxCurve(13f, 17f);
        vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
        // Random between "stay on the funnel" and "detach and fly outward late in life"
        vel.radial = new ParticleSystem.MinMaxCurve(0f, 0f); // stay on the surface
        ColorOverLife(ps,
            Grad(new Color(1f, 0.5f, 0.08f), new Color(1f, 0.8f, 0.35f)),
            Alpha(0f, 0f, 0.15f, 1f, 0.75f, 1f, 1f, 0f));
        Noise(ps, 0.08f, 0.4f, 0.3f);

        var tr = ps.trails;
        tr.enabled = true;
        tr.mode = ParticleSystemTrailMode.PerParticle;
        tr.ratio = 1f;
        tr.lifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f); // random length
        tr.minVertexDistance = 0.15f;
        tr.textureMode = ParticleSystemTrailTextureMode.Stretch;
        tr.worldSpace = false;
        tr.dieWithParticles = true;
        tr.sizeAffectsWidth = true;
        tr.inheritParticleColor = true;
        tr.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 0.2f, 0.3f, 1f, 1f, 0f));

        r.renderMode = ParticleSystemRenderMode.None; // trails only
        r.trailMaterial = trailMat;
        r.sortingFudge = -8f;
        return ps;
    }

    // 5. SPARKS: 10-30 tiny stretched glowing dots spiralling up, some shooting outward.
    static ParticleSystem BuildSparks(Transform parent, Material mat, Mesh funnel)
    {
        var ps = NewPS("Tornado_Sparks", parent, 8, out var r);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.4f), new Color(1f, 0.9f, 0.65f));

        Emission(ps, 6f);
        MeshShape(ps, funnel, 0.08f);
        var vel = Velocity(ps, 1.0f, 1.8f);
        vel.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.orbitalY = new ParticleSystem.MinMaxCurve(10f, 17f);
        vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.radial = new ParticleSystem.MinMaxCurve(-0.15f, 0.45f); // mostly hug the wall, rarely drift out
        ColorOverLife(ps, Grad(Color.white, new Color(1f, 0.7f, 0.3f)), Alpha(0f, 0f, 0.15f, 0.9f, 0.6f, 0.9f, 1f, 0f));
        Noise(ps, 0.25f, 0.8f, 0.4f);
        Frame(ps, 1, 1);

        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.03f;
        r.lengthScale = 1.2f;
        r.sharedMaterial = mat;
        r.sortingFudge = -10f;
        return ps;
    }

    // 6. DEBRIS: small dark rock/leaf silhouettes orbiting the base, pulled in and up.
    static ParticleSystem BuildDebris(Transform parent, Material mat)
    {
        var ps = NewPS("Tornado_Debris", parent, 10, out var r);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.16f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, TAU);
        main.gravityModifier = -0.1f; // slightly negative
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.8f, 0.8f), Color.white);

        Emission(ps, 5.5f);
        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = 2.3f;
        sh.radiusThickness = 0.35f;
        sh.rotation = new Vector3(90f, 0f, 0f);
        sh.position = new Vector3(0f, 0.1f, 0f);
        sh.randomPositionAmount = 0.1f;

        var vel = Velocity(ps, 0.25f, 0.7f);           // low: stays near the ground
        vel.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.orbitalY = new ParticleSystem.MinMaxCurve(7f, 11f); // orbital: medium/high
        vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.radial = new ParticleSystem.MinMaxCurve(-1.4f, -0.9f); // pulled inward

        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.separateAxes = false;
        rol.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
        ColorOverLife(ps, Grad(Color.white, Color.white), Alpha(0f, 0f, 0.15f, 1f, 0.8f, 1f, 1f, 0f));
        Noise(ps, 0.12f, 0.5f, 0.3f);
        Frame(ps, 2, 3); // rock or leaf

        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = mat;
        r.sortingFudge = -3f;
        return ps;
    }

    // 7. GROUND VORTEX: 2-3 flat polar-UV swirl discs, counter-rotating, pulsing.
    static ParticleSystem BuildGround(Transform parent, Material mat)
    {
        var ps = NewPS("Tornado_Ground_Vortex", parent, 2, out var r);
        ps.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.4f, 2.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(8.5f, 9.4f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, TAU);

        Emission(ps, 0.6f);
        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.separateAxes = false;
        rol.z = new ParticleSystem.MinMaxCurve(-3.5f, -2.8f);
        SizeOverLife(ps, Curve(0f, 1.05f, 1f, 0.9f)); // shrinks inward = pulled in
        // pulsing opacity
        ColorOverLife(ps, Grad(Color.white, Color.white), Alpha(0f, 0f, 0.25f, 0.9f, 0.5f, 0.7f, 0.75f, 0.9f, 1f, 0f));
        Erosion(ps, Curve(0f, 0.3f, 0.3f, 0.02f, 0.7f, 0.04f, 1f, 0.5f));

        r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        r.sharedMaterial = mat;
        r.maxParticleSize = 10f;
        r.sortingFudge = 20f;
        return ps;
    }

    // 8. GLOW: one soft warm disc on the ground under the funnel (anchors it; no column haze, no bloom).
    static ParticleSystem BuildGlow(Transform parent, Material mat)
    {
        var ps = NewPS("Tornado_Glow", parent, 2, out var r);
        ps.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 2.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(9.6f, 10.6f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, TAU);
        main.startColor = new Color(1f, 0.55f, 0.2f, 1f);

        Emission(ps, 0.7f);
                ColorOverLife(ps, Grad(Color.white, Color.white), Alpha(0f, 0f, 0.3f, 0.45f, 0.7f, 0.45f, 1f, 0f));
        Frame(ps, 0, 0);

        r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        r.sharedMaterial = mat;
        r.maxParticleSize = 10f;
        r.sortingFudge = 25f;
        return ps;
    }

    // =============================== MODULE HELPERS ==========================================

    static ParticleSystem NewPS(string name, Transform parent, int maxParticles, out ParticleSystemRenderer r)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 2f;
        main.loop = true;
        main.playOnAwake = false;
        main.startSpeed = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = maxParticles;
        main.stopAction = ParticleSystemStopAction.None;

        var sh = ps.shape; sh.enabled = false;

        r = go.GetComponent<ParticleSystemRenderer>();
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        r.allowOcclusionWhenDynamic = false;
        r.SetActiveVertexStreams(Streams);
        return ps;
    }

    static void Emission(ParticleSystem ps, float rate)
    {
        var em = ps.emission; em.enabled = true; em.rateOverTime = rate;
    }

    static void MeshShape(ParticleSystem ps, Mesh mesh, float randomPos)
    {
        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Mesh;
        sh.meshShapeType = ParticleSystemMeshShapeType.Vertex;
        sh.mesh = mesh;
        sh.randomPositionAmount = randomPos;
    }

    static ParticleSystem.VelocityOverLifetimeModule Velocity(ParticleSystem ps, float yMin, float yMax)
    {
        var v = ps.velocityOverLifetime;
        v.enabled = true;
        v.space = ParticleSystemSimulationSpace.Local;
        // x/y/z must share the same curve mode
        v.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        v.y = new ParticleSystem.MinMaxCurve(yMin, yMax);
        v.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        return v;
    }

    static void SpinY(ParticleSystem ps, float radPerSec)
    {
        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.separateAxes = true;
        rol.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        rol.y = new ParticleSystem.MinMaxCurve(radPerSec * 0.85f, radPerSec * 1.15f);
        rol.z = new ParticleSystem.MinMaxCurve(0f, 0f);
    }

    static void SizeOverLife(ParticleSystem ps, AnimationCurve c)
    {
        var s = ps.sizeOverLifetime; s.enabled = true; s.size = new ParticleSystem.MinMaxCurve(1f, c);
    }

    static void ColorOverLife(ParticleSystem ps, GradientColorKey[] cols, GradientAlphaKey[] alphas)
    {
        var g = new Gradient(); g.SetKeys(cols, alphas);
        var col = ps.colorOverLifetime; col.enabled = true; col.color = new ParticleSystem.MinMaxGradient(g);
    }

    static void Erosion(ParticleSystem ps, AnimationCurve erosion)
    {
        var cd = ps.customData;
        cd.enabled = true;
        cd.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
        cd.SetVectorComponentCount(ParticleSystemCustomData.Custom1, 2);
        cd.SetVector(ParticleSystemCustomData.Custom1, 0, new ParticleSystem.MinMaxCurve(1f, erosion));
        cd.SetVector(ParticleSystemCustomData.Custom1, 1, new ParticleSystem.MinMaxCurve(0f, 1f)); // random UV offset
    }

    static void Noise(ParticleSystem ps, float strength, float frequency, float scroll)
    {
        var n = ps.noise;
        n.enabled = true;
        n.strength = strength;
        n.frequency = frequency;
        n.scrollSpeed = scroll;
        n.octaveCount = 1;
        n.damping = true;
        n.quality = ParticleSystemNoiseQuality.Low;
    }

    /// <summary>2x2 atlas: 0 glow, 1 spark, 2 rock, 3 leaf. Random frame between a and b.</summary>
    static void Frame(ParticleSystem ps, int a, int b)
    {
        var t = ps.textureSheetAnimation;
        t.enabled = true;
        t.mode = ParticleSystemAnimationMode.Grid;
        t.numTilesX = 2; t.numTilesY = 2;
        t.animation = ParticleSystemAnimationType.WholeSheet;
        // frameOverTime is normalized (0..1 across the 4 frames); a constant pins one frame.
        t.frameOverTime = new ParticleSystem.MinMaxCurve(a / 4f + 0.01f, (b + 1) / 4f - 0.01f);
        t.cycleCount = 1;
    }

    static AnimationCurve Curve(params float[] tv)
    {
        var keys = new Keyframe[tv.Length / 2];
        for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(tv[i * 2], tv[i * 2 + 1]);
        var c = new AnimationCurve(keys);
        for (int i = 0; i < keys.Length; i++) AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
        for (int i = 0; i < keys.Length; i++) AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
        return c;
    }

    static GradientColorKey[] Grad(params Color[] cols)
    {
        var k = new GradientColorKey[cols.Length];
        for (int i = 0; i < cols.Length; i++) k[i] = new GradientColorKey(cols[i], cols.Length == 1 ? 0f : i / (float)(cols.Length - 1));
        return k;
    }

    static GradientAlphaKey[] Alpha(params float[] ta)
    {
        var k = new GradientAlphaKey[ta.Length / 2];
        for (int i = 0; i < k.Length; i++) k[i] = new GradientAlphaKey(ta[i * 2 + 1], ta[i * 2]);
        return k;
    }

    // =============================== ASSET HELPERS ===========================================

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static Material SaveMat(string name, Shader shader, Texture tex, System.Action<Material> setup)
    {
        string path = $"{Gen}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        mat.shader = shader;
        mat.SetTexture("_MainTex", tex);
        mat.renderQueue = (int)RenderQueue.Transparent;
        setup(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Mesh SaveMesh(Mesh mesh, string name)
    {
        string path = $"{Gen}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            existing.Clear();
            existing.vertices = mesh.vertices; existing.normals = mesh.normals;
            existing.uv = mesh.uv; existing.triangles = mesh.triangles;
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }
        mesh.name = name;
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    delegate float PixelFn(float u, float v);

    static Texture2D SaveTexture(string name, int w, int h, PixelFn fn, TextureWrapMode wrap, bool mips)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(fn((x + 0.5f) / w, (y + 0.5f) / h)) * 255f);
            px[y * w + x] = new Color32(b, b, b, 255);
        }
        tex.SetPixels32(px);
        string path = $"{Gen}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.SingleChannel;
        var s = new TextureImporterSettings();
        imp.ReadTextureSettings(s);
        s.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
        imp.SetTextureSettings(s);
        imp.sRGBTexture = false;
        imp.alphaSource = TextureImporterAlphaSource.None;
        imp.mipmapEnabled = mips;
        imp.wrapMode = wrap;
        imp.filterMode = FilterMode.Bilinear;
        imp.maxTextureSize = Mathf.Max(w, h);
        imp.textureCompression = TextureImporterCompression.Uncompressed; // R8: 64 KB max, crisp masks
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // =============================== PROCEDURAL TEXTURES =====================================

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int n = x * 374761393 + y * 668265263 + seed * 144269504;
            n = (n ^ (n >> 13)) * 1274126177;
            return ((n ^ (n >> 16)) & 0xFFFF) / 65535f;
        }
    }

    static int Mod(int a, int m) => ((a % m) + m) % m;

    // tileable value noise with integer periods px/py
    static float VNoise(float x, float y, int px, int py, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        float a = Hash(Mod(x0, px), Mod(y0, py), seed), b = Hash(Mod(x0 + 1, px), Mod(y0, py), seed);
        float c = Hash(Mod(x0, px), Mod(y0 + 1, py), seed), d = Hash(Mod(x0 + 1, px), Mod(y0 + 1, py), seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float Fbm(float u, float v, int fx, int fy, int oct, int seed)
    {
        float sum = 0, amp = 0.5f, norm = 0;
        for (int o = 0; o < oct; o++)
        {
            sum += VNoise(u * fx, v * fy, fx, fy, seed + o * 17) * amp;
            norm += amp; amp *= 0.5f; fx *= 2; fy *= 2;
        }
        return sum / norm;
    }

    // Horizontal, domain-warped streaky noise: tiles in U and V. Wraps around the funnel.
    static float BodyNoise(float u, float v)
    {
        float warp = Fbm(u, v, 3, 3, 2, 7) - 0.5f;
        float n = Fbm(u + warp * 0.15f, v + warp * 0.35f, 4, 10, 4, 3);
        float bands = 0.5f + 0.5f * Mathf.Sin((v * 6f + warp * 1.5f + Fbm(u, v, 2, 2, 1, 9)) * Mathf.PI * 2f);
        float m = Mathf.Lerp(n, n * (0.55f + 0.45f * bands), 0.6f);
        return Mathf.Clamp01((m - 0.3f) * 2.4f);
    }

    // Tapered, torn brush stroke (u = along, v = across). Used by swirls and trails.
    static float Streak(float u, float v)
    {
        float center = 0.5f + 0.18f * (Fbm(u, 0.3f, 3, 1, 2, 5) - 0.5f);
        float taper = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 0.6f);
        float thick = 0.3f * (0.55f + 0.9f * Fbm(u, 0.7f, 5, 1, 2, 8)) * taper + 0.001f;
        float d = Mathf.Abs(v - center) / thick;
        float body = Mathf.Clamp01(1f - d);
        float torn = Fbm(u, v, 12, 4, 3, 13);
        body = Mathf.Clamp01(body * 1.7f - torn * 0.55f);
        body *= Mathf.SmoothStep(0f, 1f, u / 0.08f) * Mathf.SmoothStep(0f, 1f, (1f - u) / 0.15f);
        return body;
    }

    // 2x2 atlas (frame 0 = top-left): glow, spark, rock, leaf
    static float Atlas(float u, float v)
    {
        int col = u < 0.5f ? 0 : 1;
        int row = v >= 0.5f ? 0 : 1; // row 0 = top
        int frame = row * 2 + col;
        float lu = (u - col * 0.5f) * 2f, lv = (v - (1 - row) * 0.5f) * 2f;
        float dx = lu - 0.5f, dy = lv - 0.5f;
        float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f; // 0 centre, 1 edge
        switch (frame)
        {
            case 0: // soft glow, peak kept below the highlight threshold
                return Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f) * 0.5f;
            case 1: // spark: hot core + soft halo
                return Mathf.Clamp01(Mathf.Exp(-r * r * 22f) + 0.45f * Mathf.Pow(Mathf.Clamp01(1f - r), 3f));
            case 2: // irregular rock chunk
            {
                float a = Mathf.Atan2(dy, dx);
                float rad = 0.62f + 0.12f * Mathf.Sin(a * 3f + 1.3f) + 0.08f * Mathf.Sin(a * 7f + 0.4f);
                return 1f - Mathf.SmoothStep(0f, 1f, (r - rad + 0.06f) / 0.08f);
            }
            default: // pointed leaf, rotated 45 degrees
            {
                float c = 0.7071f;
                float along = (dx * c + dy * c) * 2f;   // -1..1
                float across = (-dx * c + dy * c) * 2f;
                if (Mathf.Abs(along) > 0.85f) return 0f;
                float half = 0.3f * Mathf.Sin(Mathf.PI * (along + 0.85f) / 1.7f);
                return 1f - Mathf.SmoothStep(0f, 1f, (Mathf.Abs(across) - half + 0.05f) / 0.08f);
            }
        }
    }
}
