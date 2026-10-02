using UnityEngine;

namespace RoguePirates.V4.DevTools.Trailer
{
    /// <summary>Moves a stand-in rival ship around the player for a trailer shot (no physics, no gameplay).</summary>
    public sealed class TrailerPuppet : MonoBehaviour
    {
        Transform _anchor;
        string _behaviour;
        float _radius;
        float _angle;
        float _speed;
        float _t;
        Vector3 _holdPosition;

        public void Begin(Transform anchor, string behaviour, float radius, float angleDeg, float speed)
        {
            _anchor = anchor;
            _behaviour = behaviour;
            _radius = radius;
            _angle = angleDeg;
            _speed = speed;
            _holdPosition = Target(0f);
            transform.position = _holdPosition;
            Face(anchor.position - transform.position);
        }

        Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward; }

        Vector3 Target(float dt)
        {
            var p = _anchor.position;
            switch (_behaviour)
            {
                case "escort": // alongside, matching the player's heading
                    var fwd = Flat(_anchor.forward);
                    var right = Vector3.Cross(Vector3.up, fwd);
                    var side = Quaternion.AngleAxis(_angle, Vector3.up) * right;
                    return p + side * _radius + fwd * (Mathf.Sin(_t * 0.6f) * 2f + _speed);
                case "hold":
                    return _holdPosition;
                default: // orbit
                    _angle += (_speed != 0f ? _speed : 14f) * dt;
                    var rad = _angle * Mathf.Deg2Rad;
                    return p + new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * _radius;
            }
        }

        void Face(Vector3 dir)
        {
            dir = Flat(dir);
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        void LateUpdate()
        {
            if (_anchor == null) return;
            var dt = Time.deltaTime;
            _t += dt;
            var before = transform.position;
            var target = Target(dt);
            var next = Vector3.Lerp(before, target, 1f - Mathf.Exp(-3f * dt));
            next.y = _anchor.position.y + Mathf.Sin(_t * 1.7f) * 0.08f;
            transform.position = next;
            var move = next - before;
            if (_behaviour == "hold") Face(_anchor.position - next);
            else if (_behaviour == "escort") Face(_anchor.forward);
            else if (move.sqrMagnitude > 1e-6f) Face(move);
            // A gentle roll so the hull reads as sailing.
            transform.rotation *= Quaternion.AngleAxis(Mathf.Sin(_t * 1.3f) * 3f, Vector3.forward);
        }
    }
}
