using UnityEngine;
using UnityEngine.UI;

namespace HighSeas.UI
{
    public static class UiCanvasConfigurator
    {
        public static readonly Vector2 ReferenceResolution = new(1920f, 1080f);
        public const float MatchWidthOrHeight = 0.5f;

        public static void Configure(Canvas canvas)
        {
            if (canvas == null) return;

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = MatchWidthOrHeight;
            scaler.referencePixelsPerUnit = 100f;
        }

        public static void ConfigureFromComponent(Component component)
        {
            if (component == null) return;
            Configure(component.GetComponentInParent<Canvas>());
        }
    }

    [DisallowMultipleComponent]
    public sealed class UiScaleToFitParent : MonoBehaviour
    {
        [SerializeField] float horizontalPadding = 64f;
        [SerializeField] float minScale = 0.55f;
        [SerializeField] float contentWidth = 860f;

        RectTransform _rt;
        RectTransform _parentRt;

        void Awake()
        {
            _rt = transform as RectTransform;
            _parentRt = transform.parent as RectTransform;
        }

        void LateUpdate() => ApplyScale();

        public void SetContentWidth(float width) => contentWidth = width;

        public void ApplyScale()
        {
            if (_rt == null) _rt = transform as RectTransform;
            if (_parentRt == null) _parentRt = transform.parent as RectTransform;
            if (_rt == null) return;

            var available = _parentRt != null ? _parentRt.rect.width : 0f;
            if (available <= 0f)
            {
                var canvas = GetComponentInParent<Canvas>();
                if (canvas != null)
                    available = canvas.pixelRect.width / Mathf.Max(canvas.scaleFactor, 0.01f);
            }

            available -= horizontalPadding;
            if (available <= 0f) return;

            var scale = Mathf.Clamp(available / contentWidth, minScale, 1f);
            _rt.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
