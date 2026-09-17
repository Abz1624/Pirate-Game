using UnityEngine;

namespace HighSeas.VFX
{
    public sealed class LongNineBeamVisual : MonoBehaviour
    {
        LineRenderer _line;

        void Awake()
        {
            _line = GetComponent<LineRenderer>();
        }

        public void Configure(Vector3 start, Vector3 direction, float range, float halfWidth)
        {
            if (_line == null)
                _line = GetComponent<LineRenderer>();
            if (_line == null) return;

            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.forward;
            direction.Normalize();

            start.y = 0.32f;
            var end = start + direction * range;
            end.y = 0.32f;

            _line.useWorldSpace = true;
            _line.positionCount = 2;
            _line.SetPosition(0, start);
            _line.SetPosition(1, end);
            _line.widthMultiplier = Mathf.Clamp(halfWidth * 2.4f, 0.12f, 1.4f);
        }
    }
}
