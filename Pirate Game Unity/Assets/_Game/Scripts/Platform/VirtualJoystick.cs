using UnityEngine;
using UnityEngine.EventSystems;

namespace HighSeas.Platform
{
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        [SerializeField] RectTransform background;
        [SerializeField] RectTransform handle;
        [SerializeField] float maxRadius = 80f;

        Vector2 _input;
        bool _active;
        int _pointerId = -1;

        public Vector2 ReadMove() => _input;

        public void OnPointerDown(PointerEventData eventData)
        {
            _active = true;
            _pointerId = eventData.pointerId;
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
            _active = false;
            _pointerId = -1;
            _input = Vector2.zero;
            if (handle != null) handle.anchoredPosition = Vector2.zero;
        }
    }
}
