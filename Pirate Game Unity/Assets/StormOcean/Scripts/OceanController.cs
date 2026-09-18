using System.Collections.Generic;
using UnityEngine;

namespace StormOcean
{
    public enum OceanQuality { Low, Medium, High }

    [System.Serializable]
    public struct WaveLayer
    {
        [Tooltip("Direction on the horizontal plane, in degrees.")]
        public float directionDegrees;
        public float wavelength;
        public float amplitude;
        public float speed;
        [Range(0f, 1f)] public float steepness;

        public WaveLayer(float dir, float wl, float amp, float spd, float steep)
        {
            directionDegrees = dir;
            wavelength = wl;
            amplitude = amp;
            speed = spd;
            steepness = steep;
        }
    }

    /// <summary>
    /// Procedural storm ocean: a single non-uniform density grid (dense near the
    /// camera, sparse at the horizon) recentred under the camera every frame so
    /// the surface reads as infinite, displaced in the vertex shader by nine
    /// summed Gerstner waves split into large / medium / small octaves, plus a
    /// large-scale domain warp (turbulence) and crest/trough shaping so the wave
    /// fronts don't read as perfectly straight/periodic sine sets.
    /// This component owns the wave/foam/water parameters and pushes them into
    /// the ocean material; the actual displacement math lives in Shaders/StormOcean.shader.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class OceanController : MonoBehaviour
    {
        [Header("Quality / Mesh")]
        public OceanQuality quality = OceanQuality.High;
        public float oceanHalfSize = 500f;
        [Range(1.2f, 4f)] public float densityPower = 2.4f;

        [Header("Large Wave Settings (15-40m wavelength, 3-8m amplitude)")]
        public WaveLayer[] largeWaves = new WaveLayer[]
        {
            new WaveLayer(35f, 42f, 2.4f, 5.4f, 0.4f),
            new WaveLayer(115f, 34f, 1.9f, 4.3f, 0.38f),
            new WaveLayer(250f, 26f, 1.6f, 6.2f, 0.35f),
        };

        [Header("Medium Wave Settings (8-15m wavelength, 1-3m amplitude)")]
        public WaveLayer[] mediumWaves = new WaveLayer[]
        {
            new WaveLayer(70f, 14f, 1.0f, 3.6f, 0.3f),
            new WaveLayer(190f, 11f, 0.7f, 2.9f, 0.3f),
            new WaveLayer(300f, 8.5f, 0.5f, 4.1f, 0.28f),
        };

        [Header("Small Wave Settings (1-4m wavelength, 0.15-0.8m amplitude)")]
        public WaveLayer[] smallWaves = new WaveLayer[]
        {
            new WaveLayer(10f, 3.0f, 0.4f, 2.4f, 0.3f),
            new WaveLayer(160f, 2.0f, 0.28f, 1.9f, 0.3f),
            new WaveLayer(280f, 1.2f, 0.16f, 2.8f, 0.25f),
        };

        [Header("Wave Direction / Turbulence")]
        [Tooltip("Deterministic per-wave direction jitter so wave fronts do not all line up on exact angles.")]
        [Range(0f, 45f)] public float waveDirectionVariation = 12f;
        [Tooltip("Large-scale domain warp applied to the position waves are evaluated at, so wave fronts curve and break up instead of reading as straight repeating lines.")]
        [Range(0f, 2f)] public float waveTurbulence = 0.22f;
        [Tooltip("Multiplies wave steepness globally: >1 pulls crests narrower/sharper.")]
        [Range(0.3f, 2f)] public float crestSharpness = 0.6f;
        [Tooltip(">1 pushes troughs further down than crests rise, for the classic shallow-crest/deep-trough storm profile.")]
        [Range(0.5f, 2f)] public float troughDepth = 1.0f;
        [Tooltip("Low-frequency amplitude envelope applied to the 3 large wave layers only, so wave SETS surge unevenly (some patches tower, others go nearly flat) instead of every large wave carrying identical energy everywhere. 0 = perfectly uniform large waves.")]
        [Range(0f, 2f)] public float waveGroupiness = 0.85f;

        [Header("Surface Micro Detail / Noise")]
        public float microNoiseScale = 0.6f;
        public float microNoiseStrength = 0.6f;
        public float microNoiseSpeed = 0.12f;
        [Tooltip("Global multiplier blending the geometric (Gerstner) normal back toward flat before micro-detail is added. 1 = full geometric normal.")]
        [Range(0f, 2f)] public float normalStrength = 1.2f;
        [Tooltip("Scale of a second, higher-frequency micro-normal octave layered on top of the main micro detail, for fine wind-chop sparkle that breaks up specular/reflection.")]
        public float microDetail2Scale = 2.2f;
        [Range(0f, 2f)] public float microDetail2Strength = 0.5f;

        [Header("Foam")]
        [Range(0f, 2f)] public float foamAmount = 1.4f;
        [Range(0f, 1f)] public float foamThreshold = 0.35f;
        [Range(0.05f, 1f)] public float foamSharpness = 0.6f;
        public float foamNoiseScale = 0.16f;
        [Tooltip("How aggressively foam edges are torn into broken patches/streaks vs. a solid blob.")]
        [Range(0f, 1f)] public float foamBreakup = 0.6f;
        public float foamBreakupScale = 0.5f;
        [Range(0f, 3f)] public float foamBrightness = 0.9f;
        public Color foamTransitionColor = new Color(0.55f, 0.68f, 0.78f, 1f);
        [Tooltip("Scale of a finer noise field that punches irregular holes/gaps/bubbles into foam patches so they don't read as a flat painted shape.")]
        public float foamHoleScale = 1.4f;
        [Range(0f, 1f)] public float foamHoleStrength = 0.5f;
        [Tooltip("How strongly foam is suppressed outside of a currently-surging wave group (see waveGroupiness): only some waves break hard at any moment.")]
        [Range(0f, 1f)] public float breakingContrast = 0.6f;

        [Header("Water Color")]
        public Color deepColor = new Color(0.014f, 0.04f, 0.07f, 1f);
        public Color surfaceColor = new Color(0.05f, 0.12f, 0.18f, 1f);
        public Color highlightColor = new Color(0.55f, 0.75f, 0.82f, 1f);
        public Color foamColor = new Color(0.9f, 0.95f, 0.97f, 1f);

        [Header("Water / Reflection / Fresnel")]
        [Range(0f, 1f)] public float smoothness = 0.85f;
        [Range(0f, 4f)] public float specularStrength = 2.4f;
        [Range(0f, 2f)] public float reflectionStrength = 0.55f;
        [Range(0.5f, 8f)] public float fresnelPower = 4.5f;
        [Range(0f, 2f)] public float ambientStrength = 1.1f;
        [Tooltip("Modulates grazing-angle reflectivity with low-frequency noise so reflections scatter unevenly across the chop instead of reading as one smooth glassy sheet.")]
        [Range(0f, 1f)] public float reflectionBreakup = 0.4f;

        public Material oceanMaterial;

        MeshFilter mf;
        MeshRenderer mr;
        Transform cam;
        OceanQuality builtQuality = (OceanQuality)(-1);
        float builtHalfSize = -1f;
        float builtPower = -1f;

        static readonly int WaveAID = Shader.PropertyToID("_WaveA");
        static readonly int WaveBID = Shader.PropertyToID("_WaveB");

        // Cached copy of the last wave arrays pushed to the material (including per-
        // wave direction jitter), reused by SampleSurfaceHeight so followers can track
        // the exact same displacement the shader renders.
        Vector4[] cachedWaveA;
        Vector4[] cachedWaveB;


        void OnEnable()
        {
            mf = GetComponent<MeshFilter>();
            mr = GetComponent<MeshRenderer>();
            if (oceanMaterial != null) mr.sharedMaterial = oceanMaterial;
            BuildMeshIfNeeded();
            ApplyMaterialParams();
        }

        void OnValidate()
        {
            if (mf == null) mf = GetComponent<MeshFilter>();
            if (mr == null) mr = GetComponent<MeshRenderer>();
            BuildMeshIfNeeded();
            ApplyMaterialParams();
        }

        void LateUpdate()
        {
            if (cam == null && Camera.main != null) cam = Camera.main.transform;

            if (cam != null)
            {
                const float snap = 2f;
                Vector3 p = transform.position;
                p.x = Mathf.Round(cam.position.x / snap) * snap;
                p.z = Mathf.Round(cam.position.z / snap) * snap;
                p.y = 0f;
                transform.position = p;
            }

            BuildMeshIfNeeded();
        }

        int ResolutionForQuality()
        {
            switch (quality)
            {
                case OceanQuality.Low: return 64;
                case OceanQuality.Medium: return 128;
                default: return 220;
            }
        }

        void BuildMeshIfNeeded()
        {
            if (quality == builtQuality && Mathf.Approximately(builtHalfSize, oceanHalfSize) &&
                Mathf.Approximately(builtPower, densityPower) && mf.sharedMesh != null)
            {
                return;
            }

            mf.sharedMesh = GenerateGrid(ResolutionForQuality(), oceanHalfSize, densityPower);
            builtQuality = quality;
            builtHalfSize = oceanHalfSize;
            builtPower = densityPower;
        }

        /// <summary>
        /// Builds a single-mesh non-uniform grid: vertex density is highest near the
        /// centre (camera) and falls off toward the edges via a power-law remap of a
        /// uniform parametric grid, so there are no seams between LOD rings and only
        /// one draw call for the whole ocean.
        /// </summary>
        static Mesh GenerateGrid(int res, float halfSize, float power)
        {
            res = Mathf.Max(4, res);
            int vertsPerSide = res + 1;
            var vertices = new Vector3[vertsPerSide * vertsPerSide];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[res * res * 6];

            for (int z = 0; z <= res; z++)
            {
                float vz = (float)z / res * 2f - 1f;
                float wz = Mathf.Sign(vz) * Mathf.Pow(Mathf.Abs(vz), power) * halfSize;
                for (int x = 0; x <= res; x++)
                {
                    float vx = (float)x / res * 2f - 1f;
                    float wx = Mathf.Sign(vx) * Mathf.Pow(Mathf.Abs(vx), power) * halfSize;
                    int idx = z * vertsPerSide + x;
                    vertices[idx] = new Vector3(wx, 0f, wz);
                    uvs[idx] = new Vector2((float)x / res, (float)z / res);
                }
            }

            int t = 0;
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int i0 = z * vertsPerSide + x;
                    int i1 = i0 + 1;
                    int i2 = i0 + vertsPerSide;
                    int i3 = i2 + 1;

                    triangles[t++] = i0; triangles[t++] = i2; triangles[t++] = i1;
                    triangles[t++] = i1; triangles[t++] = i2; triangles[t++] = i3;
                }
            }

            var mesh = new Mesh();
            mesh.indexFormat = vertices.Length > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.name = "StormOceanGrid_" + res;
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(halfSize * 2.2f, 90f, halfSize * 2.2f));
            return mesh;
        }

public void ApplyMaterialParams()
        {
            if (oceanMaterial == null) return;

            var waveA = new Vector4[9];
            var waveB = new Vector4[9];
            var all = new List<WaveLayer>(9);
            all.AddRange(largeWaves);
            all.AddRange(mediumWaves);
            all.AddRange(smallWaves);

            for (int i = 0; i < 9; i++)
            {
                if (i < all.Count)
                {
                    WaveLayer w = all[i];
                    float jitter = (Mathf.PerlinNoise(i * 12.37f, 4.21f) - 0.5f) * 2f * waveDirectionVariation;
                    float rad = (w.directionDegrees + jitter) * Mathf.Deg2Rad;
                    waveA[i] = new Vector4(Mathf.Sin(rad), Mathf.Cos(rad), Mathf.Max(0.05f, w.wavelength), w.amplitude);
                    waveB[i] = new Vector4(w.speed, w.steepness, i * 17.13f, 0f);
                }
                else
                {
                    waveA[i] = Vector4.zero;
                    waveB[i] = Vector4.zero;
                }
            }

            cachedWaveA = waveA;
            cachedWaveB = waveB;

            if (mr == null) mr = GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var mpb = new MaterialPropertyBlock();
                mr.GetPropertyBlock(mpb);
                mpb.SetVectorArray(WaveAID, waveA);
                mpb.SetVectorArray(WaveBID, waveB);
                mr.SetPropertyBlock(mpb);
            }

            oceanMaterial.SetFloat("_WaveTurbulence", waveTurbulence);
            oceanMaterial.SetFloat("_CrestSharpness", crestSharpness);
            oceanMaterial.SetFloat("_TroughDepth", troughDepth);
            oceanMaterial.SetFloat("_WaveGroupiness", waveGroupiness);

            oceanMaterial.SetFloat("_MicroNoiseScale", microNoiseScale);
            oceanMaterial.SetFloat("_MicroNoiseStrength", microNoiseStrength);
            oceanMaterial.SetFloat("_MicroNoiseSpeed", microNoiseSpeed);
            oceanMaterial.SetFloat("_MicroDetail2Scale", microDetail2Scale);
            oceanMaterial.SetFloat("_MicroDetail2Strength", microDetail2Strength);
            oceanMaterial.SetFloat("_NormalStrength", normalStrength);

            oceanMaterial.SetFloat("_FoamAmount", foamAmount);
            oceanMaterial.SetFloat("_FoamThreshold", foamThreshold);
            oceanMaterial.SetFloat("_FoamSharpness", foamSharpness);
            oceanMaterial.SetFloat("_FoamNoiseScale", foamNoiseScale);
            oceanMaterial.SetFloat("_FoamBreakup", foamBreakup);
            oceanMaterial.SetFloat("_FoamBreakupScale", foamBreakupScale);
            oceanMaterial.SetFloat("_FoamBrightness", foamBrightness);
            oceanMaterial.SetFloat("_FoamHoleScale", foamHoleScale);
            oceanMaterial.SetFloat("_FoamHoleStrength", foamHoleStrength);
            oceanMaterial.SetFloat("_BreakingContrast", breakingContrast);
            oceanMaterial.SetColor("_FoamTransitionColor", foamTransitionColor);

            oceanMaterial.SetColor("_DeepColor", deepColor);
            oceanMaterial.SetColor("_SurfaceColor", surfaceColor);
            oceanMaterial.SetColor("_HighlightColor", highlightColor);
            oceanMaterial.SetColor("_FoamColor", foamColor);

            oceanMaterial.SetFloat("_Smoothness", smoothness);
            oceanMaterial.SetFloat("_SpecularStrength", specularStrength);
            oceanMaterial.SetFloat("_ReflectionStrength", reflectionStrength);
            oceanMaterial.SetFloat("_FresnelPower", fresnelPower);
            oceanMaterial.SetFloat("_AmbientStrength", ambientStrength);
            oceanMaterial.SetFloat("_ReflectionBreakup", reflectionBreakup);
        }


        /// <summary>
        /// CPU-side reproduction of the vertex shader's Gerstner-height + domain-warp +
        /// trough-depth math (hash/noise functions ported 1:1 from StormOcean.shader),
        /// so followers such as the camera height lock can track the true displaced
        /// water surface instead of a naive sine approximation that drifts arbitrarily
        /// far from reality once turbulence/crest-sharpening are applied.
        /// </summary>
public float SampleSurfaceHeight(float worldX, float worldZ, float t)
        {
            if (cachedWaveA == null || cachedWaveB == null) return 0f;

            Vector3 windDir3 = Shader.GetGlobalVector("_StormWindDir");
            float windStrength = Shader.GetGlobalFloat("_StormWindStrength");

            float warpUVx = worldX * 0.012f + windDir3.x * t * 0.03f;
            float warpUVz = worldZ * 0.012f + windDir3.z * t * 0.03f;
            float warpX = (Fbm5(warpUVx, warpUVz) - 0.5f) * waveTurbulence * 14f;
            float warpZ = (Fbm5(warpUVx + 19.7f, warpUVz + 19.7f) - 0.5f) * waveTurbulence * 14f;
            float waveX = worldX + warpX;
            float waveZ = worldZ + warpZ;

            float height = 0f;
            int count = Mathf.Min(cachedWaveA.Length, cachedWaveB.Length);
            for (int i = 0; i < count; i++)
            {
                Vector4 a = cachedWaveA[i];
                Vector4 b = cachedWaveB[i];
                float amp = a.w;
                if (amp <= 0f) continue;
                float wavelength = Mathf.Max(0.05f, a.z);
                float dirX = a.x, dirZ = a.y;
                float speed = b.x;
                float phaseOff = b.z;

                if (i < 3)
                {
                    amp *= WaveEnvelope(i, worldX, worldZ, t, wavelength, windDir3);
                }

                float w = 6.2831853f / wavelength;
                float phase = w * (dirX * waveX + dirZ * waveZ) + t * speed * w + phaseOff;
                height += amp * Mathf.Sin(phase);
            }

            if (height < 0f) height *= troughDepth;

            float windPx = worldX * 0.08f + windDir3.x * t * (0.6f + windStrength * 0.4f);
            float windPz = worldZ * 0.08f + windDir3.z * t * (0.6f + windStrength * 0.4f);
            float windRipple = (Fbm5(windPx, windPz) - 0.5f) * 0.18f * windStrength;
            height += windRipple;

            return height;
        }


        /// <summary>
        /// A smoother "broad swell" height estimate used for camera/rig placement:
        /// only the large + medium wave layers, no turbulence domain-warp, no
        /// trough-depth exaggeration and no small high-frequency ripple. Sampling the
        /// full SampleSurfaceHeight (which intentionally produces near-vertical
        /// foreground wave faces for visual drama) at a single exact point can plant
        /// a follower at the bottom of an extreme, highly local trough right beside a
        /// towering nearby crest; averaging this calmer field over a small footprint
        /// keeps a camera riding the big rollers instead of getting jammed against a
        /// sheer local wall.
        /// </summary>
        public float SampleSwellHeight(float worldX, float worldZ, float t)
        {
            if (cachedWaveA == null || cachedWaveB == null) return 0f;

            float height = 0f;
            int count = Mathf.Min(cachedWaveA.Length, cachedWaveB.Length);
            int swellCount = Mathf.Min(count, largeWaves.Length + mediumWaves.Length);
            for (int i = 0; i < swellCount; i++)
            {
                Vector4 a = cachedWaveA[i];
                Vector4 b = cachedWaveB[i];
                float amp = a.w;
                if (amp <= 0f) continue;
                float wavelength = Mathf.Max(0.05f, a.z);
                float dirX = a.x, dirZ = a.y;
                float speed = b.x;
                float phaseOff = b.z;

                float w = 6.2831853f / wavelength;
                float phase = w * (dirX * worldX + dirZ * worldZ) + t * speed * w + phaseOff;
                height += amp * Mathf.Sin(phase);
            }
            return height;
        }

        /// <summary>
        /// SampleSwellHeight averaged over a small cross-shaped footprint, for an even
        /// steadier camera-follow baseline that rides the broad swell rather than
        /// reacting to a single sample point.
        /// </summary>
        public float SampleSwellHeightSmoothed(float worldX, float worldZ, float t, float radius)
        {
            float sum = SampleSwellHeight(worldX, worldZ, t);
            sum += SampleSwellHeight(worldX + radius, worldZ, t);
            sum += SampleSwellHeight(worldX - radius, worldZ, t);
            sum += SampleSwellHeight(worldX, worldZ + radius, t);
            sum += SampleSwellHeight(worldX, worldZ - radius, t);
            return sum / 5f;
        }


        static float Frac(float x) { return x - Mathf.Floor(x); }

        static float Hash21(float px, float py)
        {
            px = Frac(px * 123.34f);
            py = Frac(py * 456.21f);
            float d = px * (px + 45.32f) + py * (py + 45.32f);
            px += d;
            py += d;
            return Frac(px * py);
        }

        static float ValueNoise(float px, float py)
        {
            float ix = Mathf.Floor(px);
            float iy = Mathf.Floor(py);
            float fx = px - ix;
            float fy = py - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash21(ix, iy);
            float b = Hash21(ix + 1f, iy);
            float c = Hash21(ix, iy + 1f);
            float d = Hash21(ix + 1f, iy + 1f);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Fbm5(float px, float py)
        {
            float v = 0f;
            float amp = 0.5f;
            for (int i = 0; i < 5; i++)
            {
                v += amp * ValueNoise(px, py);
                float nx = 1.6f * px + 1.2f * py;
                float nz = -1.2f * px + 1.6f * py;
                px = nx;
                py = nz;
                amp *= 0.5f;
            }
            return v;
        }

static float Fbm2(float px, float py)
        {
            float v = 0f;
            float amp = 0.5f;
            for (int i = 0; i < 2; i++)
            {
                v += amp * ValueNoise(px, py);
                float nx = 1.6f * px + 1.2f * py;
                float nz = -1.2f * px + 1.6f * py;
                px = nx;
                py = nz;
                amp *= 0.5f;
            }
            return v;
        }


    

float WaveEnvelope(int idx, float worldX, float worldZ, float t, float wavelength, Vector3 windDir3)
        {
            float scale = 1f / Mathf.Max(20f, wavelength * 3f);
            float px = worldX * scale + windDir3.x * t * 0.015f + idx * 41.7f;
            float py = worldZ * scale + windDir3.z * t * 0.015f;
            float n = Fbm2(px, py);
            return Mathf.Lerp(1f, 0.35f + 1.5f * n, Mathf.Clamp01(waveGroupiness));
        }
}
}
