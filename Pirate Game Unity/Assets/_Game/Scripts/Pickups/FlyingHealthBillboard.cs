using UnityEngine;

namespace HighSeas.Pickups
{
    [DisallowMultipleComponent]
    public sealed class FlyingHealthBillboard : MonoBehaviour
    {
        const float Lifetime = 1.2f;

        static Camera _cachedCamera;
        static float _nextCameraLookupTime;

        Renderer _renderer;
        Material _material;
        Color _baseColor;
        float _lifetime;

        public void Configure(Material sourceMaterial, Color color)
        {
            _baseColor = color;
            _baseColor.a = 0f;
            EnsureRenderer();
            if (sourceMaterial != null)
            {
                _material = new Material(sourceMaterial);
                _material.SetColor("_BaseColor", _baseColor);
                _renderer.sharedMaterial = _material;
            }
        }

        public void ResetLifetime() => _lifetime = 0f;

        void EnsureRenderer()
        {
            if (_renderer != null) return;

            var meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = CreateQuadMesh();
            }

            _renderer = gameObject.GetComponent<MeshRenderer>();
            if (_renderer == null)
                _renderer = gameObject.AddComponent<MeshRenderer>();
        }

        void LateUpdate()
        {
            if (_material == null) return;

            _lifetime += Time.deltaTime;
            var t = Mathf.Clamp01(_lifetime / Lifetime);
            var alpha = t < 0.5f ? t * 2f : (1f - t) * 2f;
            var color = _baseColor;
            color.a = alpha;
            _material.SetColor("_BaseColor", color);

            var cam = GetCamera();
            if (cam == null) return;
            var toCam = transform.position - cam.transform.position;
            if (toCam.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(toCam, Vector3.up);
        }

        static Camera GetCamera()
        {
            if (_cachedCamera != null && Time.unscaledTime < _nextCameraLookupTime)
                return _cachedCamera;

            _cachedCamera = Camera.main;
            _nextCameraLookupTime = Time.unscaledTime + 0.5f;
            return _cachedCamera;
        }

        static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh { name = "HealthPlusQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            return mesh;
        }

        void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }
    }
}
