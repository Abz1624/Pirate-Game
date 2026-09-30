using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RoguePirates.V4.Foundation.Platform
{
    /// <summary>
    /// The touch stick. Fixed: the background stays where it is authored. Floating (demo, 30 Sep 2026): this object is an
    /// invisible touch zone (the left half of the screen); the background appears where the thumb lands, the handle
    /// follows the drag, and both hide on release. The zone stops taking touches while the game is paused (level-up
    /// cards and results sit under it).
    /// </summary>
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
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
        public bool Floating => floating;

        /// <summary>Touch controls belong to the mobile app only (Android and iOS players; the Editor's Device
        /// Simulator counts, a mobile web browser does not).</summary>
        public static bool TouchControlsWanted =>
            UnityEngine.Device.Application.platform == RuntimePlatform.Android ||
            UnityEngine.Device.Application.platform == RuntimePlatform.IPhonePlayer;

        void Awake()
        {
            _zone = GetComponent<Graphic>();
            if (floating && background != null && background != transform) background.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!floating || _zone == null) return;
            var live = Time.timeScale > 0f;
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
