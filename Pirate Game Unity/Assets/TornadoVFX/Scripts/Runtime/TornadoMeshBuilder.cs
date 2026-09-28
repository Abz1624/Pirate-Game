using System.Collections.Generic;
using UnityEngine;

namespace TornadoFX
{
    /// <summary>
    /// Procedural meshes for the tornado: a funnel shell (wide base -> narrow waist -> flared top)
    /// and a few helical swirl bands that hug the funnel. Meshes are cached per shape so pooled
    /// instances with the same dimensions share them (no per-instance allocations).
    /// </summary>
    public static class TornadoMeshBuilder
    {
        public struct Shape
        {
            public float height, baseRadius, waistRadius, topRadius, lean;

            public Shape(float h, float b, float w, float t, float l)
            { height = h; baseRadius = b; waistRadius = w; topRadius = t; lean = l; }

            public string Key => $"{height:F2}|{baseRadius:F2}|{waistRadius:F2}|{topRadius:F2}|{lean:F2}";
        }

        public class MeshSet
        {
            public Mesh funnel;
            public Mesh[] swirls;
        }

        public const float WaistHeight = 0.42f; // normalized height of the narrowest point
        public const float SwirlGap = 1.05f;    // swirl radius vs funnel radius: hug the surface

        static readonly Dictionary<string, MeshSet> cache = new Dictionary<string, MeshSet>();

        public static MeshSet GetOrBuild(Shape s)
        {
            string key = s.Key;
            if (cache.TryGetValue(key, out var set) && set.funnel != null) return set;
            set = new MeshSet
            {
                funnel = BuildFunnel(s),
                swirls = new[]
                {
                    //                h0     h1    span(turns) radiusMul  seed
                    BuildSwirl(s, 0.03f, 0.36f, 0.85f, SwirlGap, 11),
                    BuildSwirl(s, 0.30f, 0.64f, 0.80f, SwirlGap, 23),
                    BuildSwirl(s, 0.58f, 0.95f, 0.72f, SwirlGap, 37),
                }
            };
            cache[key] = set;
            return set;
        }

        /// <summary>Funnel radius at normalized height h (0 = ground, 1 = top).</summary>
        public static float Radius(in Shape s, float h)
        {
            if (h < WaistHeight)
            {
                float t = 1f - h / WaistHeight;
                return s.waistRadius + (s.baseRadius - s.waistRadius) * Mathf.Pow(t, 1.6f); // fuller, wider base
            }
            float u = (h - WaistHeight) / (1f - WaistHeight);
            return s.waistRadius + (s.topRadius - s.waistRadius) * Mathf.Pow(u, 1.7f);
        }

        static Vector3 CenterOffset(in Shape s, float h)
        {
            // gentle S-bend so the column is never a perfectly straight axis
            return new Vector3(s.lean * Mathf.Sin(h * Mathf.PI), 0f, s.lean * 0.4f * Mathf.Sin(h * Mathf.PI * 2f));
        }

        public static Mesh BuildFunnel(Shape s, int segments = 24, int rings = 14)
        {
            int vCount = (segments + 1) * (rings + 1);
            var verts = new Vector3[vCount];
            var norms = new Vector3[vCount];
            var uvs = new Vector2[vCount];
            var tris = new int[segments * rings * 6];

            int vi = 0;
            for (int r = 0; r <= rings; r++)
            {
                float h = r / (float)rings;
                float rad = Radius(s, h);
                // slope of the profile -> tilt normals so fresnel follows the silhouette
                float dr = (Radius(s, Mathf.Min(1f, h + 0.01f)) - Radius(s, Mathf.Max(0f, h - 0.01f))) / (0.02f * s.height);
                Vector3 c = CenterOffset(s, h);
                for (int i = 0; i <= segments; i++)
                {
                    float u = i / (float)segments;
                    float a = u * Mathf.PI * 2f;
                    // irregular silhouette: low-frequency wobble that changes with height
                    float wobble = 1f + 0.07f * Mathf.Sin(a * 3f + h * 7f) + 0.04f * Mathf.Sin(a * 5f - h * 11f);
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    verts[vi] = c + dir * (rad * wobble) + Vector3.up * (h * s.height);
                    norms[vi] = (dir - Vector3.up * dr).normalized;
                    uvs[vi] = new Vector2(u, h);
                    vi++;
                }
            }

            int ti = 0;
            for (int r = 0; r < rings; r++)
            for (int i = 0; i < segments; i++)
            {
                int a = r * (segments + 1) + i;
                int b = a + segments + 1;
                tris[ti++] = a; tris[ti++] = b; tris[ti++] = a + 1;
                tris[ti++] = a + 1; tris[ti++] = b; tris[ti++] = b + 1;
            }

            var m = new Mesh { name = "Tornado_Funnel" };
            m.vertices = verts; m.normals = norms; m.uv = uvs; m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// A torn helical band that wraps around the funnel. u runs along the band (0..1), v across it.
        /// Width grows with height so upper swirls are wider, matching the flared top.
        /// </summary>
        public static Mesh BuildSwirl(Shape s, float h0, float h1, float turns, float radiusMul, int seed, int steps = 28)
        {
            var verts = new Vector3[(steps + 1) * 2];
            var norms = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];
            var tris = new int[steps * 6];
            float span = turns * Mathf.PI * 2f;
            float scale = s.height / 7f;
            float phase = seed * 1.37f;

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float h = Mathf.Lerp(h0, h1, t);
                float a = t * span + phase;
                // slight random distortion so the curves are never perfect circles
                float n = 1f + 0.035f * Mathf.Sin(t * 7f + seed) + 0.02f * Mathf.Sin(t * 17f + seed * 2.1f);
                float rad = Radius(s, Mathf.Clamp01(h)) * radiusMul * n;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 center = CenterOffset(s, Mathf.Clamp01(h)) + dir * rad + Vector3.up * (h * s.height);

                float width = Mathf.Lerp(0.45f, 1.25f, h) * scale * (0.75f + 0.25f * Mathf.Sin(t * 5f + seed));
                // the band's top edge leans outward like the funnel wall
                // follow the funnel wall slope so the band lies on the surface
                float slope = (Radius(s, Mathf.Clamp01(h + 0.02f)) - Radius(s, Mathf.Clamp01(h - 0.02f))) / (0.04f * s.height);
                Vector3 up = (Vector3.up + dir * slope).normalized;

                verts[i * 2] = center - up * width * 0.5f;
                verts[i * 2 + 1] = center + up * width * 0.5f;
                norms[i * 2] = norms[i * 2 + 1] = dir;
                uvs[i * 2] = new Vector2(t, 0f);
                uvs[i * 2 + 1] = new Vector2(t, 1f);
            }

            int ti = 0;
            for (int i = 0; i < steps; i++)
            {
                int a = i * 2;
                tris[ti++] = a; tris[ti++] = a + 1; tris[ti++] = a + 2;
                tris[ti++] = a + 2; tris[ti++] = a + 1; tris[ti++] = a + 3;
            }

            var m = new Mesh { name = $"Tornado_Swirl_{seed}" };
            m.vertices = verts; m.normals = norms; m.uv = uvs; m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }
    }
}
