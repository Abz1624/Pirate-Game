using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace HighSeas.VFX
{
    /// <summary>
    /// Drag-and-drop Anchor Flail visual. A glowing anchor on a glowing chain whips out in a
    /// sweeping arc (tentacle-strike style), smacks everything it passes, then reels back.
    /// Visual + motion only — damage is left to gameplay via <see cref="onHit"/> / <see cref="onStrike"/>.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class AnchorFlailVfx : MonoBehaviour
    {
        public enum Phase { Idle, Windup, Extend, Impact, Retract }

        [System.Serializable] public sealed class TransformEvent : UnityEvent<Transform> { }
        [System.Serializable] public sealed class PointEvent : UnityEvent<Vector3> { }

        [Header("Rig (filled by the builder)")]
        [Tooltip("Anchor pivot sits at the crown tip; +Z points from the ring toward the crown.")]
        [SerializeField] Transform anchor;
        [Tooltip("Ring on the anchor where the chain attaches.")]
        [SerializeField] Transform chainAttach;
        [Tooltip("Where the chain leaves the ship. Defaults to this transform.")]
        [SerializeField] Transform chainOrigin;
        [SerializeField] Mesh linkMesh;
        [SerializeField] Mesh linkGlowMesh;
        [SerializeField] Material linkMaterial;
        [SerializeField] Material linkGlowMaterial;
        [Tooltip("Sparks emitted at every hit / at the strike point.")]
        [SerializeField] ParticleSystem hitSparks;
        [Tooltip("Flat ring pop emitted at every hit / at the strike point.")]
        [SerializeField] ParticleSystem hitRing;
        [SerializeField] TrailRenderer lashTrail;

        [Header("Firing")]
        [SerializeField] bool autoFire = true;
        [SerializeField, Min(0.1f)] float cooldown = 1.9f;
        [SerializeField, Min(1f)] float range = 9f;
        [SerializeField] string targetTag = "Enemy";
        [Tooltip("Keep striking in rotating directions when no target is in range (preview / showcase).")]
        [SerializeField] bool fireWithoutTargets = true;
        [Tooltip("Height above the target pivot the strike aims at.")]
        [SerializeField] float targetHeightOffset = 0.4f;
        [Tooltip("How far past the aimed target the anchor travels.")]
        [SerializeField, Min(0f)] float overshoot = 0.8f;

        [Header("Motion")]
        [Tooltip("Short pull-back before the swing, sells the weight.")]
        [SerializeField, Min(0f)] float windupTime = 0.24f;
        [SerializeField, Min(0f)] float windupPullback = 0.7f;
        [Tooltip("Swing time. Accelerates into the target like a heavy weight.")]
        [SerializeField, Min(0.01f)] float extendTime = 0.46f;
        [SerializeField, Min(0f)] float impactTime = 0.24f;
        [Tooltip("How far the anchor's momentum carries it past the strike point before settling.")]
        [SerializeField, Min(0f)] float impactSettle = 0.35f;
        [SerializeField, Min(0.01f)] float retractTime = 0.7f;
        [Tooltip("Sideways bulge of the strike path, so the anchor sweeps across instead of poking straight.")]
        [SerializeField] float sweepWidth = 2.2f;
        [Tooltip("Sideways S-curl of the chain while it whips out.")]
        [SerializeField] float whipBend = 1f;
        [SerializeField] float whipLift = 0.5f;
        [Tooltip("Roll of the anchor while it flies (degrees).")]
        [SerializeField] float lashRoll = 30f;
        [Tooltip("Scale pop at full extension.")]
        [SerializeField] float impactPunch = 0.2f;

        [Header("Hits")]
        [Tooltip("Targets within this distance of the flying anchor get hit (once per strike).")]
        [SerializeField, Min(0.1f)] float hitRadius = 1.3f;
        [SerializeField, Range(1, 30)] int sparksPerHit = 6;

        [Header("Rest pose (local to this object)")]
        [SerializeField] Vector3 restLocalPosition = new(2.3f, 0.6f, 1.6f);
        [SerializeField] Vector3 restLocalForward = new(0.1f, -0.15f, -1f);
        [Tooltip("Which way the flat face of the anchor points while stowed (tilts it against the hull).")]
        [SerializeField] Vector3 restLocalUp = new(1f, 1f, 0f);
        [SerializeField] float restSwayDegrees = 6f;
        [SerializeField] float restSag = 0.25f;

        [Header("Chain")]
        [Tooltip("Centre-to-centre link spacing at scale 1 (0.2 = links interlock exactly).")]
        [SerializeField, Min(0.05f)] float linkPitch = 0.2f;
        [SerializeField, Min(0.05f)] float linkScale = 0.8f;
        [SerializeField, Range(8, 256)] int maxLinks = 120;

        [Header("Events")]
        [Tooltip("Fired once for every target the anchor passes through.")]
        public TransformEvent onHit = new();
        [Tooltip("Fired when the anchor reaches full extension.")]
        public PointEvent onStrike = new();

        public Phase CurrentPhase => _phase;
        public bool IsBusy => _phase != Phase.Idle;
        public float Range { get => range; set => range = Mathf.Max(1f, value); }
        public float Cooldown { get => cooldown; set => cooldown = Mathf.Max(0.1f, value); }
        public bool AutoFire { get => autoFire; set => autoFire = value; }

        const int CurveSamples = 28;

        Phase _phase;
        float _phaseT;
        float _cooldownT = 0.5f;
        float _side = 1f;
        float _bend;
        float _sag;
        float _idleAimDeg;

        Vector3 _from;
        Vector3 _strike;
        Vector3 _sweepDir;
        Vector3 _strikeDir;
        Vector3 _windupPos;
        Quaternion _windupStartRot;
        Quaternion _windupRot;
        Quaternion _windupMidRot;
        Vector3 _retractFrom;
        Quaternion _retractFromRot;
        Vector3 _anchorBaseScale = Vector3.one;

        readonly List<Transform> _candidates = new();
        readonly HashSet<Transform> _alreadyHit = new();

        Matrix4x4[] _matrices;
        int _linkCount;
        int _builtFrame = -1;
        readonly Vector3[] _curvePts = new Vector3[CurveSamples + 1];
        readonly float[] _curveLen = new float[CurveSamples + 1];

        void OnEnable()
        {
            if (anchor != null)
                _anchorBaseScale = anchor.localScale;
            _phase = Phase.Idle;
            _sag = restSag;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            if (lashTrail != null) lashTrail.emitting = false;
            if (anchor != null)
                anchor.localScale = _anchorBaseScale;
        }

        void Update()
        {
            if (anchor == null || !Application.isPlaying) return; // edit mode: leave the rig where the builder posed it
            Tick(Time.deltaTime);
        }

        // ---------------------------------------------------------------- public API

        /// <summary>Strike at <paramref name="target"/>. Returns false if already busy.</summary>
        public bool StrikeAt(Transform target)
        {
            if (target == null) return false;
            return BeginStrike(TargetPoint(target));
        }

        /// <summary>Strike toward a world point (clamped to range). Returns false if already busy.</summary>
        public bool StrikeAtPoint(Vector3 worldPoint) => BeginStrike(worldPoint);

        [ContextMenu("Strike Now")]
        public void StrikeNow()
        {
            var t = FindNearestTarget();
            if (t != null) StrikeAt(t);
            else StrikeAtPoint(NextIdleAimPoint());
        }

        // ---------------------------------------------------------------- state machine

        bool BeginStrike(Vector3 aimPoint)
        {
            if (anchor == null || IsBusy) return false;
            var origin = ChainStart();
            var flat = aimPoint - origin;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) flat = transform.forward;
            var dist = Mathf.Min(flat.magnitude + overshoot, range + overshoot);
            var dir = flat.normalized;
            _strike = new Vector3(origin.x, aimPoint.y, origin.z) + dir * dist;

            _from = anchor.position;
            _side = -_side;
            _strikeDir = dir;
            _sweepDir = Vector3.Cross(Vector3.up, dir) * _side;
            // wind-up: drag back toward the hull and away from the swing side
            _windupPos = _from - dir * windupPullback - _sweepDir * (windupPullback * 0.5f) + Vector3.up * (windupPullback * 0.25f);
            _windupStartRot = anchor.rotation;
            // cocked pose: faces the strike, yawed back and rolled — the swing unwinds from exactly this
            _windupRot = FlatLook(dir) * Quaternion.Euler(0f, -_side * 35f, _side * lashRoll);
            // only turn part-way during the wind-up; the swing finishes the turn, so no fast spin
            _windupMidRot = Quaternion.Slerp(_windupStartRot, _windupRot, 0.45f);
            anchor.localScale = _anchorBaseScale;

            GatherCandidates();
            _alreadyHit.Clear();

            if (lashTrail != null)
            {
                lashTrail.Clear();
                lashTrail.emitting = false; // starts with the swing, not the pull-back
            }
            SetPhase(windupTime > 0f ? Phase.Windup : Phase.Extend);
            if (_phase == Phase.Extend) { _windupMidRot = anchor.rotation; BeginSwing(); }
            return true;
        }

        void Tick(float dt)
        {
            _phaseT += dt;
            switch (_phase)
            {
                case Phase.Idle:
                    PoseRest(Time.time);
                    _bend = Mathf.MoveTowards(_bend, 0f, dt * 4f);
                    _sag = Mathf.MoveTowards(_sag, restSag, dt * 2f);
                    if (autoFire)
                    {
                        _cooldownT -= dt;
                        if (_cooldownT <= 0f)
                        {
                            var t = FindNearestTarget();
                            var fired = t != null ? StrikeAt(t) : fireWithoutTargets && StrikeAtPoint(NextIdleAimPoint());
                            if (!fired) _cooldownT = 0.15f;
                        }
                    }
                    break;

                case Phase.Windup:
                {
                    var k = Mathf.Clamp01(_phaseT / windupTime);
                    var e = EaseInOutCubic(k); // eases out of rest and into the cocked pose — no jolt
                    anchor.position = Vector3.LerpUnclamped(_from, _windupPos, e);
                    anchor.rotation = Quaternion.Slerp(_windupStartRot, _windupMidRot, e);
                    _bend = -0.3f * e;
                    _sag = restSag;
                    if (k >= 1f) BeginSwing();
                    break;
                }

                case Phase.Extend:
                {
                    var k = Mathf.Clamp01(_phaseT / extendTime);
                    var e = HeavySwing(k);
                    var arc = Mathf.Sin(e * Mathf.PI);
                    var pos = Vector3.LerpUnclamped(_from, _strike, e)
                              + _sweepDir * (sweepWidth * arc)
                              + Vector3.up * (whipLift * 0.5f * Mathf.Sin(k * Mathf.PI));
                    anchor.position = pos;
                    // unwinds continuously from the cocked pose to facing the strike (no snap on frame one)
                    var swingRot = FlatLook(_strikeDir) * Quaternion.Euler(0f, -_side * 35f * (1f - e), _side * lashRoll * (1f - e));
                    anchor.rotation = Quaternion.Slerp(_windupMidRot, swingRot, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 1.5f)));
                    _bend = Mathf.Lerp(-0.3f, 0.2f, Mathf.SmoothStep(0f, 1f, k)) + Mathf.Sin(k * Mathf.PI) * 0.9f;
                    _sag = 0f;
                    CheckHits(pos);
                    if (k >= 1f) EnterImpact();
                    break;
                }

                case Phase.Impact:
                {
                    var k = impactTime <= 0f ? 1f : Mathf.Clamp01(_phaseT / impactTime);
                    anchor.localScale = _anchorBaseScale * (1f + impactPunch * (1f - EaseOutCubic(k)));
                    // momentum carries it a little further, then it settles back (damped)
                    anchor.position = _strike + _strikeDir * (impactSettle * Mathf.Sin(k * Mathf.PI) * (1f - k * 0.5f));
                    _bend = 0.2f * (1f - k) * Mathf.Sin(k * Mathf.PI * 3f);
                    CheckHits(anchor.position);
                    if (k >= 1f) EnterRetract();
                    break;
                }

                case Phase.Retract:
                {
                    var k = Mathf.Clamp01(_phaseT / retractTime);
                    var e = EaseInOutCubic(k);
                    RestPose(Time.time, out var restPos, out var restRot);
                    anchor.position = Vector3.LerpUnclamped(_retractFrom, restPos, e)
                                      - _sweepDir * (sweepWidth * 0.35f * Mathf.Sin(e * Mathf.PI))
                                      + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.3f);
                    anchor.rotation = Quaternion.Slerp(_retractFromRot, restRot, e);
                    _bend = -0.35f * Mathf.Sin(k * Mathf.PI);
                    _sag = restSag * e;
                    if (k >= 1f)
                    {
                        _cooldownT = cooldown;
                        SetPhase(Phase.Idle);
                    }
                    break;
                }
            }
        }

        void BeginSwing()
        {
            _from = anchor.position;
            if (lashTrail != null)
            {
                lashTrail.Clear();
                lashTrail.emitting = true;
            }
            SetPhase(Phase.Extend);
        }

        void EnterImpact()
        {
            anchor.position = _strike;
            if (lashTrail != null) lashTrail.emitting = false;
            EmitHit(_strike, sparksPerHit + 4);
            onStrike?.Invoke(_strike);
            SetPhase(Phase.Impact);
        }

        void EnterRetract()
        {
            anchor.localScale = _anchorBaseScale;
            _retractFrom = anchor.position;
            _retractFromRot = anchor.rotation;
            SetPhase(Phase.Retract);
        }

        void SetPhase(Phase p)
        {
            _phase = p;
            _phaseT = 0f;
        }

        // ---------------------------------------------------------------- hits

        void GatherCandidates()
        {
            _candidates.Clear();
            if (string.IsNullOrEmpty(targetTag)) return;
            GameObject[] found;
            try { found = GameObject.FindGameObjectsWithTag(targetTag); }
            catch (UnityException) { return; }
            var origin = transform.position;
            var reach = range + overshoot + sweepWidth + hitRadius;
            var reachSqr = reach * reach;
            for (var i = 0; i < found.Length; i++)
            {
                var go = found[i];
                if (go == null || !go.activeInHierarchy || go.transform.IsChildOf(transform)) continue;
                var d = go.transform.position - origin;
                d.y = 0f;
                if (d.sqrMagnitude <= reachSqr) _candidates.Add(go.transform);
            }
        }

        void CheckHits(Vector3 head)
        {
            var rSqr = hitRadius * hitRadius;
            for (var i = 0; i < _candidates.Count; i++)
            {
                var t = _candidates[i];
                if (t == null || _alreadyHit.Contains(t)) continue;
                var d = t.position - head;
                d.y = 0f;
                if (d.sqrMagnitude > rSqr) continue;
                _alreadyHit.Add(t);
                EmitHit(TargetPoint(t), sparksPerHit);
                onHit?.Invoke(t);
            }
        }

        void EmitHit(Vector3 p, int sparks)
        {
            if (hitSparks != null)
                hitSparks.Emit(new ParticleSystem.EmitParams { position = p, applyShapeToPosition = true }, sparks);
            if (hitRing != null)
                hitRing.Emit(new ParticleSystem.EmitParams { position = p + Vector3.up * 0.05f }, 1);
        }

        // ---------------------------------------------------------------- helpers

        Transform FindNearestTarget()
        {
            if (string.IsNullOrEmpty(targetTag)) return null;
            GameObject[] found;
            try { found = GameObject.FindGameObjectsWithTag(targetTag); }
            catch (UnityException) { return null; }

            var origin = transform.position;
            Transform best = null;
            var bestSqr = range * range;
            for (var i = 0; i < found.Length; i++)
            {
                var go = found[i];
                if (go == null || !go.activeInHierarchy || go.transform.IsChildOf(transform)) continue;
                var d = go.transform.position - origin;
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
            _idleAimDeg = (_idleAimDeg + 137.5f) % 360f; // golden-angle spread so strikes cover all sides
            var fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            var dir = Quaternion.AngleAxis(_idleAimDeg, Vector3.up) * fwd.normalized;
            return transform.position + dir * (range * 0.85f) + Vector3.up * targetHeightOffset;
        }

        Vector3 TargetPoint(Transform t) => t.position + Vector3.up * targetHeightOffset;

        Vector3 ChainStart() => chainOrigin != null ? chainOrigin.position : transform.position;

        void PoseRest(float time)
        {
            RestPose(time, out var pos, out var rot);
            anchor.position = pos;
            anchor.rotation = rot;
        }

        void RestPose(float time, out Vector3 pos, out Quaternion rot)
        {
            pos = transform.TransformPoint(restLocalPosition) + Vector3.up * (Mathf.Sin(time * 2.1f) * 0.05f);
            var fwd = transform.TransformDirection(restLocalForward);
            if (fwd.sqrMagnitude < 0.0001f) fwd = -transform.forward;
            var upHint = transform.TransformDirection(restLocalUp);
            if (upHint.sqrMagnitude < 0.0001f) upHint = transform.up;
            rot = Quaternion.LookRotation(fwd.normalized, upHint.normalized) * Quaternion.Euler(0f, Mathf.Sin(time * 1.4f) * restSwayDegrees, 0f);
        }

        static Quaternion FlatLook(Vector3 dir)
        {
            dir.y *= 0.25f;
            return dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : Quaternion.identity;
        }

        static float EaseOutCubic(float x) { x = 1f - x; return 1f - x * x * x; }
        // slow start, builds speed, arrives fast — a heavy weight swung on a chain
        static float HeavySwing(float x) => Mathf.Lerp(x * x * (3f - 2f * x), x * x, 0.6f);
        static float EaseInOutCubic(float x) => x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) * 0.5f;

        // ---------------------------------------------------------------- chain rendering

        void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (anchor == null || linkMesh == null || linkMaterial == null) return;
            if ((cam.cullingMask & (1 << gameObject.layer)) == 0) return;
            if (_builtFrame != Time.frameCount || !Application.isPlaying)
            {
                BuildChain();
                _builtFrame = Time.frameCount;
            }
            if (_linkCount == 0) return;

            var rp = new RenderParams(linkMaterial)
            {
                camera = cam,
                layer = gameObject.layer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(transform.position, Vector3.one * (range * 2.5f + 10f))
            };
            Graphics.RenderMeshInstanced(rp, linkMesh, 0, _matrices, _linkCount);

            if (linkGlowMesh != null && linkGlowMaterial != null)
            {
                rp.material = linkGlowMaterial;
                Graphics.RenderMeshInstanced(rp, linkGlowMesh, 0, _matrices, _linkCount);
            }
        }

        void BuildChain()
        {
            if (_matrices == null || _matrices.Length != maxLinks)
                _matrices = new Matrix4x4[maxLinks];

            var a = ChainStart();
            var b = chainAttach != null ? chainAttach.position : anchor.position;
            var d = b - a;
            var len = d.magnitude;
            if (len < 0.01f) { _linkCount = 0; return; }

            var flat = new Vector3(d.x, 0f, d.z);
            var perp = flat.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, flat.normalized) : Vector3.right;
            var bend = Application.isPlaying ? _bend : 0f;
            var sag = Application.isPlaying ? _sag : restSag;
            var curl = perp * (-_side * bend * whipBend * len * 0.4f);
            var lift = Vector3.up * (Mathf.Abs(bend) * whipLift);
            var droop = Vector3.down * sag;

            // S-shaped cubic: first third curls one way, second third the other — tentacle whip.
            var c1 = a + d * 0.33f + curl + lift + droop;
            var c2 = a + d * 0.66f - curl * 0.45f + lift * 0.5f + droop;

            _curvePts[0] = a;
            _curveLen[0] = 0f;
            for (var i = 1; i <= CurveSamples; i++)
            {
                var t = i / (float)CurveSamples;
                var u = 1f - t;
                _curvePts[i] = u * u * u * a + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * b;
                _curveLen[i] = _curveLen[i - 1] + Vector3.Distance(_curvePts[i - 1], _curvePts[i]);
            }

            var total = _curveLen[CurveSamples];
            var scale = linkScale * transform.lossyScale.x;
            var pitch = linkPitch * scale;
            var count = Mathf.Min(maxLinks, Mathf.CeilToInt(total / pitch));
            var seg = 1;
            for (var i = 0; i < count; i++)
            {
                var s = Mathf.Min((i + 0.5f) * pitch, total);
                while (seg < CurveSamples && _curveLen[seg] < s) seg++;
                var l0 = _curveLen[seg - 1];
                var l1 = _curveLen[seg];
                var f = l1 > l0 ? (s - l0) / (l1 - l0) : 0f;
                var p = Vector3.Lerp(_curvePts[seg - 1], _curvePts[seg], f);
                var tan = _curvePts[seg] - _curvePts[seg - 1];
                if (tan.sqrMagnitude < 1e-8f) tan = d;
                var rot = Quaternion.LookRotation(tan.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, (i & 1) == 0 ? 0f : 90f);
                _matrices[i] = Matrix4x4.TRS(p, rot, Vector3.one * scale);
            }
            _linkCount = count;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.35f, 0.85f, 0.6f);
            DrawCircle(transform.position, range);
        }

        static void DrawCircle(Vector3 c, float r)
        {
            const int n = 48;
            var prev = c + new Vector3(r, 0f, 0f);
            for (var i = 1; i <= n; i++)
            {
                var a = i / (float)n * Mathf.PI * 2f;
                var p = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
    }
}
