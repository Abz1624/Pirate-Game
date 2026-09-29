using UnityEngine;

namespace RoguePirates.V4.Foundation.World
{
    /// <summary>
    /// Single boundary wall panel. Assign on <see cref="V3PlayBoundsWallVisual"/> prefab to tune color, alpha, texture, and scroll.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayBoundsWallSegment : MonoBehaviour
    {
        [Header("Color & transparency")]
        public Color tint = new(0.78f, 0.18f, 0.22f, 0.28f);

        [Header("Texture")]
        [Tooltip("Optional. Uses material template base map when empty.")]
        public Texture2D texture;

        public Vector2 textureTiling = Vector2.one;

        [Tooltip("UV scroll direction in texture space (e.g. (0,1) = upward).")]
        public Vector2 textureScrollDirection = new(0f, 1f);

        public float textureScrollSpeed = 0.25f;

        [Header("Material")]
        [Tooltip("Optional URP Unlit transparent material template. A unique instance is created at runtime.")]
        public Material materialTemplate;

        Renderer _renderer;
        Material _material;
        Vector2 _scrollOffset;
        float _proximityFade;

        void Awake() => EnsureMaterial();

        void OnValidate()
        {
            if (!Application.isPlaying && _material != null)
                ApplyAppearance();
        }

        void Update()
        {
            if (_material == null || textureScrollSpeed <= 0f)
                return;

            if (textureScrollDirection.sqrMagnitude < 0.0001f)
                return;

            var dir = textureScrollDirection.normalized;
            _scrollOffset += dir * (textureScrollSpeed * Time.deltaTime);
            ApplyTextureOffset();
        }

        public void SetProximityFade(float fade01)
        {
            _proximityFade = Mathf.Clamp01(fade01);
            if (_material == null)
                EnsureMaterial();
            else
                ApplyAppearance();

            gameObject.SetActive(_proximityFade > 0.01f);
        }

        public void FaceOutward(Vector3 outwardNormal)
        {
            if (outwardNormal.sqrMagnitude < 0.0001f)
                return;
            transform.rotation = Quaternion.LookRotation(outwardNormal.normalized, Vector3.up);
        }

        void EnsureMaterial()
        {
            if (_material != null)
                return;

            _renderer = GetComponent<Renderer>();
            if (_renderer == null)
                _renderer = gameObject.AddComponent<MeshRenderer>();

            if (materialTemplate != null)
                _material = new Material(materialTemplate);
            else if (_renderer.sharedMaterial != null)
                _material = new Material(_renderer.sharedMaterial);
            else
                _material = CreateDefaultMaterial();

            PlayBoundsWallMaterial.ConfigureTransparent(_material);
            _renderer.material = _material;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            ApplyAppearance();
        }

        void ApplyAppearance()
        {
            if (_material == null)
                return;

            var color = tint;
            color.a *= _proximityFade;
            _material.color = color;
            if (_material.HasProperty("_BaseColor"))
                _material.SetColor("_BaseColor", color);

            var tex = texture;
            if (tex == null && materialTemplate != null && materialTemplate.HasProperty("_BaseMap"))
                tex = materialTemplate.GetTexture("_BaseMap") as Texture2D;

            if (tex != null)
            {
                _material.mainTexture = tex;
                if (_material.HasProperty("_BaseMap"))
                    _material.SetTexture("_BaseMap", tex);
            }

            if (_material.HasProperty("_BaseMap_ST"))
            {
                var st = new Vector4(textureTiling.x, textureTiling.y, _scrollOffset.x, _scrollOffset.y);
                _material.SetVector("_BaseMap_ST", st);
            }
            else
            {
                _material.mainTextureScale = textureTiling;
                ApplyTextureOffset();
            }
        }

        void ApplyTextureOffset()
        {
            if (_material == null)
                return;

            if (_material.HasProperty("_BaseMap_ST"))
            {
                var st = _material.GetVector("_BaseMap_ST");
                st.z = _scrollOffset.x;
                st.w = _scrollOffset.y;
                _material.SetVector("_BaseMap_ST", st);
            }
            else
                _material.mainTextureOffset = _scrollOffset;
        }

        static Material CreateDefaultMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            return new Material(shader);
        }
    }

    public static class PlayBoundsWallMaterial
    {
        public static void ConfigureTransparent(Material material)
        {
            if (material == null)
                return;

            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }

            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
        }
    }
}
