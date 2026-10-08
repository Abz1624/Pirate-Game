using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HighSeas.VFX
{
    /// <summary>
    /// Drag-and-drop Chainshot visual. Two spiked iron balls joined by a chain are fired from the
    /// broadside facing the target, spin like a bola along an arc, and smack everything they pass.
    /// Motion streaks + swirl ring are unlit. Visual + motion only — damage is left to gameplay via
    /// <see cref="onHit"/> / <see cref="onStrike"/>.
    /// Drop it as a child of the ship (local 0,0,0) — muzzles are placed for the Brigg.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChainshotVfx : MonoBehaviour
    {
        public enum Phase { Idle, Flying, Fade }

        [System.Serializable] public sealed class TransformEvent : UnityEvent<Transform> { }
        [System.Serializable] public sealed class PointEvent : UnityEvent<Vector3> { }

        [Header("Rig (filled by the builder)")]
        [SerializeField] Transform projectile;
        [Tooltip("Oriented along the flight direction; spin happens around its local Y.")]
        [SerializeField] Transform spinPlane;
        [Tooltip("Rotates the balls, chain and swirl as one rigid bola.")]
        [SerializeField] Transform spin;
        [SerializeField] Transform ballA;
        [SerializeField] Transform ballB;
        [SerializeField] Transform outerRing;
        [Tooltip("Renderers hidden on impact while the trails fade out.")]
        [SerializeField] Renderer[] bodyRenderers;
        [SerializeField] TrailRenderer[] trails;
        [SerializeField] Transform muzzleLeft;
        [SerializeField] Transform muzzleRight;
        [SerializeField] GameObject muzzleFxLeft;
        [SerializeField] GameObject muzzleFxRight;
        [SerializeField] ParticleSystem impactRing;
        [SerializeField] ParticleSystem impactSparks;

        [Header("Firing")]
        [SerializeField] bool autoFire = true;
        [SerializeField, Min(0.1f)] float cooldown = 1.6f;
        [SerializeField, Min(1f)] float range = 14f;
        [SerializeField] string targetTag = "Enemy";
        [Tooltip("Keep firing in rotating directions when no target is in range (preview / showcase).")]
        [SerializeField] bool fireWithoutTargets = true;
        [SerializeField] float targetHeightOffset = 0.5f;

        [Header("Flight")]
        [SerializeField, Min(1f)] float speed = 15f;
        [Tooltip("Arc apex height = base + distance * perUnit.")]
        [SerializeField] float arcHeightBase = 0.6f;
        [SerializeField] float arcHeightPerUnit = 0.12f;
        [Tooltip("Bola spin in degrees per second.")]
        [SerializeField] float spinSpeed = 1260f;
        [Tooltip("Forward pitch of the spin plane so the swirl reads from a top-down camera.")]
        [SerializeField] float spinTilt = 14f;
        [Tooltip("Time for the chain to snap open after leaving the barrel.")]
        [SerializeField, Min(0.01f)] float openTime = 0.14f;
        [SerializeField, Range(0.05f, 1f)] float closedScale = 0.35f;
        [Tooltip("Roll of each ball around the chain axis (deg/s).")]
        [SerializeField] float ballTumble = 540f;

        [Header("Hits")]
        [Tooltip("Targets within this distance of the bola centre get hit (once per shot).")]
        [SerializeField, Min(0.1f)] float hitRadius = 1.1f;
        [SerializeField, Range(1, 30)] int sparksPerHit = 8;

        [Header("Events")]
        public TransformEvent onHit = new TransformEvent();
        public PointEvent onStrike = new PointEvent();

        public Phase CurrentPhase => _phase;
        public bool IsBusy => _phase != Phase.Idle;
        public float Range { get => range; set => range = Mathf.Max(1f, value); }
        public float Cooldown { get => cooldown; set => cooldown = Mathf.Max(0.1f, value); }
        public bool AutoFire { get => autoFire; set => autoFire = value; }

        Phase _phase;
        float _t;
        float _flightTime;
        float _apex;
        float _spinAngle;
        float _cooldownT = 0.4f;
        float _idleAimDeg;
        float _fadeTime = 0.2f;
        Vector3 _start;
        Vector3 _end;
        Vector3 _ringBaseScale = Vector3.one;

        readonly List<Transform> _candidates = new List<Transform>();
        readonly HashSet<Transform> _alreadyHit = new HashSet<Transform>();

        void OnEnable()
        {
            if (outerRing != null) _ringBaseScale = outerRing.localScale;
            if (trails != null)
                foreach (var tr in trails)
                    if (tr != null) _fadeTime = Mathf.Max(_fadeTime, tr.time);
            StopFx(muzzleFxLeft);
            StopFx(muzzleFxRight);
            if (projectile != null) projectile.gameObject.SetActive(false);
            _phase = Phase.Idle;
        }

        void Update()
        {
            if (projectile == null) return;
            var dt = Time.deltaTime;
            _t += dt;
            switch (_phase)
            {
                case Phase.Idle:
                    if (!autoFire) break;
                    _cooldownT -= dt;
                    if (_cooldownT > 0f) break;
                    var target = FindNearestTarget();
                    var fired = target != null ? FireAt(target) : fireWithoutTargets && FireAtPoint(NextIdleAimPoint());
                    if (!fired) _cooldownT = 0.15f;
                    break;

                case Phase.Flying:
                    TickFlight(dt);
                    break;

                case Phase.Fade:
                    if (_t >= _fadeTime)
                    {
                        projectile.gameObject.SetActive(false);
                        _cooldownT = cooldown;
                        SetPhase(Phase.Idle);
                    }
                    break;
            }
        }

        // ---------------------------------------------------------------- public API

        public bool FireAt(Transform target) => target != null && FireAtPoint(target.position + Vector3.up * targetHeightOffset);

        public bool FireAtPoint(Vector3 worldPoint)
        {
            if (projectile == null || IsBusy) return false;

            var right = Vector3.Dot(worldPoint - transform.position, transform.right) >= 0f;
            var muzzle = right ? muzzleRight : muzzleLeft;
            if (muzzle == null) muzzle = right ? muzzleLeft : muzzleRight;
            _start = muzzle != null ? muzzle.position : transform.position;

            var flat = worldPoint - _start;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) flat = (right ? 1f : -1f) * transform.right;
            var dist = Mathf.Min(flat.magnitude, range);
            _end = _start + flat.normalized * dist;
            _end.y = worldPoint.y;

            _flightTime = Mathf.Max(0.25f, dist / speed);
            _apex = arcHeightBase + dist * arcHeightPerUnit;
            _spinAngle = Random.Range(0f, 360f);

            projectile.gameObject.SetActive(true);
            SetBodyVisible(true);
            PoseFlight(0f, 0f);
            if (trails != null)
                foreach (var tr in trails)
                    if (tr != null) { tr.Clear(); tr.emitting = true; }

            PlayFx(right ? muzzleFxRight : muzzleFxLeft);
            GatherCandidates();
            _alreadyHit.Clear();
            SetPhase(Phase.Flying);
            return true;
        }

        [ContextMenu("Fire Now")]
        public void FireNow()
        {
            var t = FindNearestTarget();
            if (t != null) FireAt(t);
            else FireAtPoint(NextIdleAimPoint());
        }

        // ---------------------------------------------------------------- flight

        void TickFlight(float dt)
        {
            var k = Mathf.Clamp01(_t / _flightTime);
            _spinAngle = (_spinAngle + spinSpeed * dt) % 360f;
            PoseFlight(k, _t);

            var tumble = ballTumble * dt;
            if (ballA != null) ballA.Rotate(Vector3.right, tumble, Space.Self); // local X = chain axis
            if (ballB != null) ballB.Rotate(Vector3.right, -tumble * 0.8f, Space.Self);

            CheckHits(projectile.position);
            if (k >= 1f) Impact();
        }

        void PoseFlight(float k, float time)
        {
            var pos = Vector3.Lerp(_start, _end, k) + Vector3.up * (4f * _apex * k * (1f - k));
            projectile.position = pos;

            var flat = _end - _start;
            flat.y = 0f;
            if (spinPlane != null && flat.sqrMagnitude > 0.0001f)
            {
                var vy = 4f * _apex * (1f - 2f * k) / Mathf.Max(0.01f, flat.magnitude); // slope of the arc
                var pitch = -Mathf.Atan(vy) * Mathf.Rad2Deg * 0.5f;
                spinPlane.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up) * Quaternion.Euler(spinTilt + pitch, 0f, 0f);
            }

            if (spin != null)
            {
                spin.localRotation = Quaternion.Euler(0f, _spinAngle, 0f);
                var open = Mathf.Clamp01(time / openTime);
                spin.localScale = Vector3.one * Mathf.LerpUnclamped(closedScale, 1f, EaseOutBack(open));
            }

            if (outerRing != null)
                outerRing.localScale = _ringBaseScale * (1f + 0.06f * Mathf.Sin(time * 38f)) * Mathf.Clamp01(time / openTime);
        }

        void Impact()
        {
            projectile.position = _end;
            EmitHit(_end, sparksPerHit + 4);
            onStrike?.Invoke(_end);
            SetBodyVisible(false);
            if (trails != null)
                foreach (var tr in trails)
                    if (tr != null) tr.emitting = false;
            SetPhase(Phase.Fade);
        }

        void SetPhase(Phase p) { _phase = p; _t = 0f; }

        void SetBodyVisible(bool v)
        {
            if (bodyRenderers == null) return;
            foreach (var r in bodyRenderers)
                if (r != null) r.enabled = v;
        }

        // ---------------------------------------------------------------- hits

        void GatherCandidates()
        {
            _candidates.Clear();
            if (string.IsNullOrEmpty(targetTag)) return;
            GameObject[] found;
            try { found = GameObject.FindGameObjectsWithTag(targetTag); }
            catch (UnityException) { return; }
            var reach = range + hitRadius * 2f;
            var reachSqr = reach * reach;
            foreach (var go in found)
            {
                if (go == null || !go.activeInHierarchy || go.transform.IsChildOf(transform)) continue;
                var d = go.transform.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude <= reachSqr) _candidates.Add(go.transform);
            }
        }

        void CheckHits(Vector3 head)
        {
            var rSqr = hitRadius * hitRadius;
            foreach (var t in _candidates)
            {
                if (t == null || _alreadyHit.Contains(t)) continue;
                var d = t.position - head;
                d.y = 0f;
                if (d.sqrMagnitude > rSqr) continue;
                _alreadyHit.Add(t);
                EmitHit(t.position + Vector3.up * targetHeightOffset, sparksPerHit);
                onHit?.Invoke(t);
            }
        }

        void EmitHit(Vector3 p, int sparks)
        {
            if (impactSparks != null)
                impactSparks.Emit(new ParticleSystem.EmitParams { position = p, applyShapeToPosition = true }, sparks);
            if (impactRing != null)
                impactRing.Emit(new ParticleSystem.EmitParams { position = p + Vector3.up * 0.05f }, 1);
        }

        // ---------------------------------------------------------------- helpers

        Transform FindNearestTarget()
        {
            if (string.IsNullOrEmpty(targetTag)) return null;
            GameObject[] found;
            try { found = GameObject.FindGameObjectsWithTag(targetTag); }
            catch (UnityException) { return null; }
            Transform best = null;
            var bestSqr = range * range;
            foreach (var go in found)
            {
                if (go == null || !go.activeInHierarchy || go.transform.IsChildOf(transform)) continue;
                var d = go.transform.position - transform.position;
                d.y = 0f;
                var sqr = d.sqrMagnitude;
                if (sqr > bestSqr) continue;
                bestSqr = sqr;
                best = go.transform;
            }
            return best;
        }

        Vector3 NextIdleAimPoint()
        {
            _idleAimDeg = (_idleAimDeg + 137.5f) % 360f;
            var fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            var dir = Quaternion.AngleAxis(_idleAimDeg, Vector3.up) * fwd.normalized;
            return transform.position + dir * (range * 0.75f) + Vector3.up * targetHeightOffset;
        }

        static void PlayFx(GameObject root)
        {
            if (root == null) return;
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Clear(true);
                ps.Play(true);
            }
        }

        static void StopFx(GameObject root)
        {
            if (root == null) return;
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            var m = x - 1f;
            return 1f + c3 * m * m * m + c1 * m * m;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.5f, 0.6f);
            const int n = 48;
            var c = transform.position;
            var prev = c + new Vector3(range, 0f, 0f);
            for (var i = 1; i <= n; i++)
            {
                var a = i / (float)n * Mathf.PI * 2f;
                var p = c + new Vector3(Mathf.Cos(a) * range, 0f, Mathf.Sin(a) * range);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
    }
}
