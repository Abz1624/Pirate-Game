using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ribbon trail stretched between two points (e.g. blade base + tip) instead of
/// Unity's built-in TrailRenderer, which only follows a single transform and
/// therefore always looks like it comes from a point.
/// Attach to the weapon root, assign PointA/PointB to two child transforms
/// placed at the two ends of the edge you want the trail to sweep from.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class EdgeRibbonTrail : MonoBehaviour
{
    [Header("Edge points (line the trail is drawn from)")]
    public Transform pointA;
    public Transform pointB;

    [Header("Trail")]
    [Tooltip("How long (seconds) a sample stays alive.")]
    public float trailTime = 0.25f;
    [Tooltip("Minimum movement (world units) before a new sample is recorded.")]
    public float minVertexDistance = 0.02f;
    [Tooltip("Fade alpha out over the trail's lifetime via vertex color.")]
    public bool fadeAlpha = true;
    public Material trailMaterial;

    private struct Sample
    {
        public Vector3 a, b;
        public float time;
    }

    private readonly List<Sample> samples = new List<Sample>();
    private Mesh mesh;
    private MeshFilter mf;
    private MeshRenderer mr;

    private readonly List<Vector3> verts = new List<Vector3>();
    private readonly List<Color> colors = new List<Color>();
    private readonly List<Vector2> uvs = new List<Vector2>();
    private readonly List<int> tris = new List<int>();

    void OnEnable()
    {
        mf = GetComponent<MeshFilter>();
        mr = GetComponent<MeshRenderer>();
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "EdgeRibbonTrail";
            mesh.MarkDynamic();
        }
        mf.sharedMesh = mesh;
        if (trailMaterial != null) mr.sharedMaterial = trailMaterial;
        samples.Clear();
        mesh.Clear();
    }

    void LateUpdate()
    {
        if (pointA == null || pointB == null) return;

        float now = Time.time;

        // Record a new sample if we've moved enough since the last one.
        if (samples.Count == 0)
        {
            samples.Add(new Sample { a = pointA.position, b = pointB.position, time = now });
        }
        else
        {
            Sample last = samples[samples.Count - 1];
            float moved = Vector3.Distance(last.a, pointA.position) + Vector3.Distance(last.b, pointB.position);
            if (moved >= minVertexDistance)
                samples.Add(new Sample { a = pointA.position, b = pointB.position, time = now });
            else
            {
                // keep the head sample's position current so the trail doesn't lag while barely moving
                last.a = pointA.position;
                last.b = pointB.position;
                last.time = now;
                samples[samples.Count - 1] = last;
            }
        }

        // Drop samples older than trailTime.
        while (samples.Count > 0 && now - samples[0].time > trailTime)
            samples.RemoveAt(0);

        BuildMesh(now);
    }

    void BuildMesh(float now)
    {
        verts.Clear();
        colors.Clear();
        uvs.Clear();
        tris.Clear();

        if (samples.Count < 2)
        {
            mesh.Clear();
            return;
        }

        // Build in local space so the mesh follows this transform's own movement too.
        Matrix4x4 worldToLocal = transform.worldToLocalMatrix;
        int count = samples.Count;

        for (int i = 0; i < count; i++)
        {
            Sample s = samples[i];
            float age = trailTime <= 0f ? 1f : Mathf.Clamp01((now - s.time) / trailTime);
            float alpha = fadeAlpha ? 1f - age : 1f;
            float u = (float)i / (count - 1);

            verts.Add(worldToLocal.MultiplyPoint3x4(s.a));
            verts.Add(worldToLocal.MultiplyPoint3x4(s.b));
            colors.Add(new Color(1f, 1f, 1f, alpha));
            colors.Add(new Color(1f, 1f, 1f, alpha));
            uvs.Add(new Vector2(u, 0f));
            uvs.Add(new Vector2(u, 1f));

            if (i > 0)
            {
                int baseIdx = (i - 1) * 2;
                // two triangles per quad segment
                tris.Add(baseIdx + 0);
                tris.Add(baseIdx + 1);
                tris.Add(baseIdx + 2);

                tris.Add(baseIdx + 1);
                tris.Add(baseIdx + 3);
                tris.Add(baseIdx + 2);
            }
        }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetColors(colors);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
    }
}
