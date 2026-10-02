using UnityEngine;

namespace RoguePirates.V4.DevTools.Trailer
{
    /// <summary>
    /// Film camera moves around the player ship for trailer takes. Added to the main camera by
    /// <see cref="TrailerDirector.Camera"/>, which disables the game's FollowCamera while it runs.
    /// Camera time ignores the time scale, so slow motion slows the world, not the camera move. It is derived from
    /// Time.deltaTime, which follows the Recorder's fixed capture rate, so moves match the recorded footage.
    /// </summary>
    public sealed class TrailerCamera : MonoBehaviour
    {
        Transform _target;
        Transform _focus;
        string _mode = "chase";
        float _distance;
        float _speed;
        float _t;
        float _angle;
        Vector3 _flatForward = Vector3.forward;

        public void Begin(Transform target, string mode, float distance, float speed, Transform focus = null)
        {
            _target = target;
            _focus = focus;
            _mode = mode;
            _distance = distance;
            _speed = speed;
            _t = 0f;
            _angle = target != null ? target.eulerAngles.y + 180f : 0f;
            if (target != null) _flatForward = Flat(target.forward);
            Apply(1f);
        }

        static float Dt => Time.timeScale > 0.01f ? Time.deltaTime / Time.timeScale : Time.unscaledDeltaTime;

        void LateUpdate() => Apply(1f - Mathf.Exp(-4f * Dt));

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward;
        }

        void Apply(float blend)
        {
            if (_target == null) return;
            _t += Dt;
            // Heading follows the ship slowly so turns read as sweeping, not jerky.
            _flatForward = Vector3.Slerp(_flatForward, Flat(_target.forward), 1f - Mathf.Exp(-1.5f * Dt));
            var right = Vector3.Cross(Vector3.up, _flatForward);
            var p = _target.position;
            Vector3 pos;
            Vector3 look;
            switch (_mode)
            {
                case "low": // skimming the water behind the ship
                    pos = p - _flatForward * D(15f) + Vector3.up * 2.4f + right * 3f;
                    look = p + _flatForward * 10f + Vector3.up * 1.5f;
                    break;
                case "hero": // in front and to the side, looking back at the bow
                    pos = p + _flatForward * D(10f) + right * 6f + Vector3.up * 2.5f;
                    look = p + Vector3.up * 1.5f;
                    break;
                case "orbit":
                    _angle += (_speed != 0f ? _speed : 18f) * Dt;
                    var rad = _angle * Mathf.Deg2Rad;
                    pos = p + new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * D(16f) + Vector3.up * 7f;
                    look = p + Vector3.up * 1f;
                    blend = 1f;
                    break;
                case "crane": // drops from high above into the chase position over ~4 s
                    var k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_t / (_speed > 0f ? _speed : 4f)));
                    var high = p + Vector3.up * 70f - _flatForward * 8f;
                    var low = p - _flatForward * D(14f) + Vector3.up * 7f;
                    pos = Vector3.Lerp(high, low, k);
                    look = p + _flatForward * Mathf.Lerp(0f, 8f, k);
                    blend = 1f;
                    break;
                case "wide": // high and far: shows the crowd
                    pos = p + Vector3.up * D(60f) - _flatForward * D(60f) * 0.6f;
                    look = p;
                    break;
                case "top": // closer than the game camera, straight down-ish
                    pos = p + Vector3.up * D(22f) - _flatForward * 5f;
                    look = p;
                    break;
                case "pair": // frames the ship and a focus (the boss, a rival) from behind the ship
                    var f = _focus != null ? _focus.position : p + _flatForward * 20f;
                    var mid = (p + f) * 0.5f;
                    var span = Mathf.Max(12f, (f - p).magnitude);
                    var away = Flat(p - f);
                    pos = p + away * (span * 0.55f + D(8f)) + Vector3.up * (span * 0.45f + 6f);
                    look = mid;
                    break;
                default: // chase
                    pos = p - _flatForward * D(13f) + Vector3.up * 5.5f;
                    look = p + _flatForward * 8f + Vector3.up * 1f;
                    break;
            }
            transform.position = Vector3.Lerp(transform.position, pos, blend);
            var rot = Quaternion.LookRotation(look - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, blend);
        }

        float D(float fallback) => _distance > 0f ? _distance : fallback;
    }
}
