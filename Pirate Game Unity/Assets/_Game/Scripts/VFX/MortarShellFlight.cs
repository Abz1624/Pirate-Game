using System;
using HighSeas.Core;
using UnityEngine;

namespace HighSeas.VFX
{
    /// <summary>
    /// Visual-only mortar shell: waits an optional launch delay (hidden), then lobs from the ship
    /// to its landing point on a high arc. Used singly or as a barrage of fire shells.
    /// </summary>
    public sealed class MortarShellFlight : MonoBehaviour
    {
        public const string MountName = "MortarMount";

        [Tooltip("Arc apex height per meter of horizontal distance.")]
        [SerializeField] float apexPerMeter = 0.55f;
        [SerializeField] float minApex = 6f;
        [SerializeField] float maxApex = 20f;
        [SerializeField] float spinDegreesPerSecond = 420f;
        [Tooltip("Scale over normalized flight (pops out of the barrel, swells at apex for readability).")]
        [SerializeField] AnimationCurve scaleOverFlight = new(
            new Keyframe(0f, 0.55f),
            new Keyframe(0.12f, 1f),
            new Keyframe(0.5f, 1.2f),
            new Keyframe(1f, 1f));

        Vector3 _from;
        Vector3 _to;
        Vector3 _baseScale;
        Vector3 _spinAxis;
        float _delay;
        float _duration;
        float _time;
        float _apex;
        bool _flying;
        bool _launched;
        Action<GameObject> _onLaunch;
        Action<GameObject> _onLand;
        TrailRenderer[] _trails;
        Renderer[] _renderers;
        ParticleSystem[] _particles;

        /// <summary>World launch point on a ship: its MortarMount child if present, otherwise the deck above the pivot.</summary>
        public static Vector3 FindLaunchPoint(Transform ship, float fallbackDeckHeight = 1.2f)
        {
            if (ship == null) return Vector3.zero;
            var mount = FindDeep(ship, MountName);
            return mount != null ? mount.position : ship.position + Vector3.up * fallbackDeckHeight;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var hit = FindDeep(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        void CacheParts()
        {
            _trails ??= GetComponentsInChildren<TrailRenderer>(true);
            _renderers ??= GetComponentsInChildren<Renderer>(true);
            _particles ??= GetComponentsInChildren<ParticleSystem>(true);
        }

        public void Launch(Vector3 from, Vector3 to, float duration, Action<GameObject> onLand) =>
            Launch(from, to, 0f, duration, null, onLand);

        public void Launch(Vector3 from, Vector3 to, float delay, float duration,
            Action<GameObject> onLaunch, Action<GameObject> onLand)
        {
            CacheParts();
            _from = from;
            _to = to;
            _delay = Mathf.Max(0f, delay);
            _duration = Mathf.Max(0.1f, duration);
            _time = 0f;
            _onLaunch = onLaunch;
            _onLand = onLand;
            _baseScale = transform.localScale;
            _launched = false;

            var flat = to - from;
            flat.y = 0f;
            _apex = Mathf.Clamp(flat.magnitude * apexPerMeter, minApex, maxApex);
            _spinAxis = flat.sqrMagnitude > 0.0001f
                ? Vector3.Cross(Vector3.up, flat.normalized)
                : Vector3.right;

            transform.position = from;
            gameObject.SetActive(true);
            SetVisible(false);
            _flying = true;
            enabled = true;
            if (_delay <= 0f) BeginFlight();
        }

        void BeginFlight()
        {
            _launched = true;
            transform.position = _from;
            SetVisible(true);
            for (var i = 0; i < _trails.Length; i++)
            {
                if (_trails[i] == null) continue;
                _trails[i].Clear();
                _trails[i].emitting = true;
            }
            for (var i = 0; i < _particles.Length; i++)
            {
                if (_particles[i] == null) continue;
                _particles[i].Clear(true);
                _particles[i].Play(true);
            }

            var cb = _onLaunch;
            _onLaunch = null;
            cb?.Invoke(gameObject);
        }

        void SetVisible(bool visible)
        {
            for (var i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].enabled = visible;
        }

        void Update()
        {
            if (!_flying) return;
            if (RunManager.Instance != null && RunManager.Instance.IsPaused) return;

            if (!_launched)
            {
                _delay -= Time.deltaTime;
                if (_delay > 0f) return;
                BeginFlight();
            }

            _time += Time.deltaTime;
            var u = Mathf.Clamp01(_time / _duration);

            var p = Vector3.Lerp(_from, _to, u);
            p.y += 4f * _apex * u * (1f - u);
            transform.position = p;
            transform.Rotate(_spinAxis, spinDegreesPerSecond * Time.deltaTime, Space.World);
            transform.localScale = _baseScale * scaleOverFlight.Evaluate(u);

            if (u < 1f) return;

            _flying = false;
            enabled = false;
            transform.localScale = _baseScale;
            for (var i = 0; i < _trails.Length; i++)
                if (_trails[i] != null) _trails[i].emitting = false;
            for (var i = 0; i < _particles.Length; i++)
                if (_particles[i] != null) _particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var cb = _onLand;
            _onLand = null;
            if (cb != null) cb(gameObject);
            else Destroy(gameObject);
        }
    }
}
