using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RoguePirates.V4.Foundation.Platform
{
    /// <summary>
    /// The touch stick. Fixed: the background stays where it is authored. Floating (demo, 30 Sep 2026): this object is an
    /// invisible touch zone (the left half of the screen); the background appears where the thumb lands, the handle
    /// follows the drag, and both hide on release. The zone stops taking touches while the run is paused (level-up
    /// cards and results sit under it).
    /// </summary>
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IInitializePotentialDragHandler
    {
        [SerializeField] RectTransform background;
        [SerializeField] RectTransform handle;
        [SerializeField] float maxRadius = 80f;
        [Tooltip("The stick appears under the thumb anywhere in this object's rect, and hides on release.")]
        [SerializeField] bool floating;

        Vector2 _input;
        bool _active;
        int _pointerId = -1;
        Graphic _zone;

        public Vector2 ReadMove() => _input;
        /// <summary>The stick's base and knob art (the ability button reuses them).</summary>
        public Sprite BaseSprite => background != null && background.TryGetComponent<Image>(out var i) ? i.sprite : null;
        public Sprite KnobSprite => handle != null && handle.TryGetComponent<Image>(out var i) ? i.sprite : null;
        public bool Floating => floating;

        /// <summary>Touch controls on phones and tablets: the Android and iOS apps and the web build in a mobile browser
        /// (the Editor's Device Simulator counts); never on desktop.</summary>
        public static bool TouchControlsWanted => ForceTouchControls ||
            UnityEngine.Device.Application.platform == RuntimePlatform.Android ||
            UnityEngine.Device.Application.platform == RuntimePlatform.IPhonePlayer ||
            (UnityEngine.Device.Application.platform == RuntimePlatform.WebGLPlayer && UnityEngine.Device.Application.isMobilePlatform);

        /// <summary>Tests and the playable check: show touch controls on desktop too.</summary>
        public static bool ForceTouchControls;

        /// <summary>V5 M7: the touch zone on the half of the screen the settings name (left by default).</summary>
        public void ApplySide()
        {
            if (!floating || !(transform is RectTransform rt)) return;
            var right = GameSettings.Current.stickOnRight;
            rt.anchorMin = new Vector2(right ? 0.5f : 0f, 0f);
            rt.anchorMax = new Vector2(right ? 1f : 0.5f, 1f);
        }

        void OnEnable()
        {
            ApplySide();
            GameSettings.Changed += ApplySide;
        }

        void OnDisable() => GameSettings.Changed -= ApplySide;

        void Awake()
        {
            _zone = GetComponent<Graphic>();
            if (floating && background != null && background != transform) background.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!floating || _zone == null) return;
            // Paused (level-up cards, chest prompts, results): the zone lets touches through to the UI under it. The run's
            // pause state, not the time scale: a level-up pauses the run without stopping time.
            var run = RoguePirates.V4.Foundation.Core.RunManager.Instance;
            var live = Time.timeScale > 0f && (run == null || !run.IsPaused);
            if (_zone.raycastTarget != live) _zone.raycastTarget = live;
            if (!live && _active) Release();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_active) return;
            _active = true;
            _pointerId = eventData.pointerId;
            if (floating && background != null && background != transform)
            {
                var parent = (RectTransform)background.parent;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera, out var local);
                background.anchorMin = background.anchorMax = new Vector2(0.5f, 0.5f);
                background.localPosition = new Vector3(local.x, local.y, 0f);
                background.gameObject.SetActive(true);
            }
            OnDrag(eventData);
        }

        /// <summary>No drag threshold: the UI waits ~10 px before it reports a drag, which dropped small stick moves.</summary>
        public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        public void OnDrag(PointerEventData eventData)
        {
            if (!_active || eventData.pointerId != _pointerId) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                background, eventData.position, eventData.pressEventCamera, out var local);
            _input = Vector2.ClampMagnitude(local / maxRadius, 1f);
            if (handle != null) handle.anchoredPosition = _input * maxRadius;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            Release();
        }

        void Release()
        {
            _active = false;
            _pointerId = -1;
            _input = Vector2.zero;
            if (handle != null) handle.anchoredPosition = Vector2.zero;
            if (floating && background != null && background != transform) background.gameObject.SetActive(false);
        }
    }
}
