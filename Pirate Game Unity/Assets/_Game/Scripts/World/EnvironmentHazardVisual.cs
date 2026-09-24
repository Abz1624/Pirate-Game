using UnityEngine;
using UnityEngine.Rendering;

namespace HighSeas.World
{
    /// <summary>Ground readout for ocean currents and whirlpools (Saltwind environment hazards).</summary>
    public sealed class EnvironmentHazardVisual : MonoBehaviour
    {
        public enum HazardKind
        {
            OceanCurrent,
            Whirlpool
        }

        const float GroundY = 0.35f;

        [SerializeField] HazardKind kind;
        [SerializeField] Vector3 flowDirection = Vector3.forward;
        [SerializeField] Vector3 laneSize = new(28f, 4f, 12f);
        [SerializeField] float whirlpoolRadius = 10f;

        Material _material;
        MaterialPropertyBlock _mpb;
        Transform _root;
        float _spin;

        public static void AttachOceanCurrent(GameObject host, Vector3 worldFlow, Vector3 boxSize)
        {
            if (EnvironmentalHazardsDisabled.Value) return;
            var visual = host.GetComponent<EnvironmentHazardVisual>();
            if (visual == null)
                visual = host.AddComponent<EnvironmentHazardVisual>();
            visual.kind = HazardKind.OceanCurrent;
            visual.flowDirection = worldFlow;
            visual.laneSize = boxSize;
            visual.Build();
        }

        public static void AttachWhirlpool(GameObject host, float radius)
        {
            if (EnvironmentalHazardsDisabled.Value) return;
            var visual = host.GetComponent<EnvironmentHazardVisual>();
            if (visual == null)
                visual = host.AddComponent<EnvironmentHazardVisual>();
            visual.kind = HazardKind.Whirlpool;
            visual.whirlpoolRadius = radius;
            visual.Build();
        }

        void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }

        void Update()
        {
            if (kind != HazardKind.Whirlpool || _root == null) return;
            _spin += Time.deltaTime * 55f;
            _root.localRotation = Quaternion.Euler(0f, _spin, 0f);
        }

        void Awake()
        {
            if (EnvironmentalHazardsDisabled.Value)
                Destroy(this);
        }

        void Build()
        {
            if (EnvironmentalHazardsDisabled.Value) return;
            if (_root != null)
                Destroy(_root.gameObject);

            _mpb ??= new MaterialPropertyBlock();
            _material ??= CreateUnlitMaterial();
            _root = new GameObject("HazardVisual").transform;
            _root.SetParent(transform, false);
            _root.localPosition = Vector3.zero;

            switch (kind)
            {
                case HazardKind.OceanCurrent:
                    BuildCurrentVisual();
                    break;
                case HazardKind.Whirlpool:
                    BuildWhirlpoolVisual();
                    break;
            }
        }

        void BuildCurrentVisual()
        {
            var flow = flowDirection;
            flow.y = 0f;
            if (flow.sqrMagnitude < 0.01f)
                flow = Vector3.forward;
            flow.Normalize();

            var width = Mathf.Max(laneSize.x, laneSize.z);
            var depth = Mathf.Min(laneSize.x, laneSize.z);
            var rot = Quaternion.LookRotation(flow, Vector3.up);
            _root.localRotation = rot;

            AddQuad(
                "CurrentLane",
                new Vector3(0f, GroundY, 0f),
                new Vector2(width, depth),
                new Color(0.15f, 0.55f, 0.95f, 0.28f));

            var arrowSpacing = 7f;
            var count = Mathf.Max(2, Mathf.FloorToInt(width / arrowSpacing));
            for (var i = 0; i < count; i++)
            {
                var t = (i + 0.5f) / count - 0.5f;
                AddQuad(
                    $"Arrow{i}",
                    new Vector3(t * width * 0.85f, GroundY + 0.02f, 0f),
                    new Vector2(4.5f, 2.2f),
                    new Color(0.35f, 0.82f, 1f, 0.42f));
            }
        }

        void BuildWhirlpoolVisual()
        {
            var r = Mathf.Max(4f, whirlpoolRadius);
            AddDisc("OuterRing", Vector3.zero, r * 1.05f, new Color(0.08f, 0.35f, 0.55f, 0.35f), 48);
            AddRing("DangerRing", Vector3.up * GroundY, r, r * 0.72f, new Color(0.95f, 0.35f, 0.12f, 0.5f), 40);
            AddDisc("Core", Vector3.up * (GroundY + 0.03f), r * 0.35f, new Color(0.05f, 0.12f, 0.2f, 0.55f), 24);
        }

        void AddQuad(string name, Vector3 localPos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = localPos;
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildQuad(size.x, size.y);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            SetColor(renderer, color);
        }

        void AddDisc(string name, Vector3 localPos, float radius, Color color, int segments)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = localPos;
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildDisc(radius, segments);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            SetColor(renderer, color);
        }

        void AddRing(string name, Vector3 localPos, float outer, float inner, Color color, int segments)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = localPos;
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildRing(outer, inner, segments);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            SetColor(renderer, color);
        }

        void SetColor(MeshRenderer renderer, Color color)
        {
            _mpb.SetColor("_BaseColor", color);
            _mpb.SetColor("_Color", color);
            renderer.SetPropertyBlock(_mpb);
        }

        static Material CreateUnlitMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            mat.SetFloat("_Surface", 1f);
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3050;
            return mat;
        }

        static Mesh BuildQuad(float width, float depth)
        {
            var mesh = new Mesh { name = "HazardQuad" };
            var hw = width * 0.5f;
            var hd = depth * 0.5f;
            mesh.vertices = new[]
            {
                new Vector3(-hw, 0f, -hd),
                new Vector3(-hw, 0f, hd),
                new Vector3(hw, 0f, hd),
                new Vector3(hw, 0f, -hd)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            return mesh;
        }

        static Mesh BuildDisc(float radius, int segments)
        {
            var mesh = new Mesh { name = "HazardDisc" };
            var verts = new Vector3[segments + 1];
            var tris = new int[segments * 3];
            verts[0] = Vector3.zero;
            for (var i = 0; i < segments; i++)
            {
                var a = i / (float)segments * Mathf.PI * 2f;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            }

            var t = 0;
            for (var i = 0; i < segments; i++)
            {
                tris[t++] = 0;
                tris[t++] = i + 1;
                tris[t++] = i + 1 == segments ? 1 : i + 2;
            }

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            return mesh;
        }

        static Mesh BuildRing(float outer, float inner, int segments)
        {
            var mesh = new Mesh { name = "HazardRing" };
            inner = Mathf.Max(0.1f, inner);
            var verts = new Vector3[segments * 2];
            var tris = new int[segments * 6];
            for (var i = 0; i < segments; i++)
            {
                var a = i / (float)segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts[i * 2] = dir * inner;
                verts[i * 2 + 1] = dir * outer;
            }

            var t = 0;
            for (var i = 0; i < segments; i++)
            {
                var i0 = i * 2;
                var i1 = i * 2 + 1;
                var i2 = ((i + 1) % segments) * 2;
                var i3 = ((i + 1) % segments) * 2 + 1;
                tris[t++] = i0;
                tris[t++] = i1;
                tris[t++] = i3;
                tris[t++] = i0;
                tris[t++] = i3;
                tris[t++] = i2;
            }

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
