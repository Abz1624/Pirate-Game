using System.Collections.Generic;
using HighSeas.Combat;
using UnityEngine;
using UnityEngine.Events;

namespace HighSeas.VFX
{
    /// <summary>
    /// Drag-and-drop "Maelstrom Charge" weapon. Every <see cref="cooldown"/> seconds a thunder bolt slams into the
    /// densest enemy crowd within <see cref="range"/>, opening a small toon whirlpool on the water. For
    /// <see cref="whirlpoolLife"/> seconds the whirlpool drags nearby enemies inward and damages them every
    /// <see cref="tickInterval"/> seconds, then implodes with a small splash.
    ///
    /// Damage: applied directly through <see cref="IDamageable"/> when <see cref="applyDamage"/> is on, and always
    /// reported through <see cref="onHit"/> so gameplay can route it elsewhere.
    ///
    /// Rig convention (made by MaelstromChargeBuilder): child "Strike" holding "Bolt", "BoltCore", "Branch"
    /// (LineRenderers), "Whirlpool" (disc mesh, RoguePirates/MaelstromWhirlpool) and particle systems
    /// "Flash", "Ring", "Splash". "Strike" is detached to world space at runtime so the ship can sail away.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MaelstromChargeVfx : MonoBehaviour
    {
        public enum Phase { Idle, Strike, Active, Implode }

        [System.Serializable] public sealed class PointEvent : UnityEvent<Vector3> { }
        [System.Serializable] public sealed class TransformEvent : UnityEvent<Transform> { }

        [Header("Rig (filled by the builder)")]
        [SerializeField] Transform strikeRoot;
        [SerializeField] LineRenderer bolt;
        [SerializeField] LineRenderer boltCore;
        [SerializeField] LineRenderer branch;
        [SerializeField] LineRenderer branch2;
        [Tooltip("Wide soft additive layer behind the bolt, reads as glow without post bloom.")]
        [SerializeField] LineRenderer boltHalo;
        [SerializeField] ParticleSystem impactGlow;
        [SerializeField] Transform whirlpool;
        [SerializeField] Renderer whirlpoolRenderer;
        [SerializeField] ParticleSystem flash;
        [SerializeField] ParticleSystem ring;
        [SerializeField] ParticleSystem splash;

        [Header("Targeting")]
        [SerializeField] bool autoFire = true;
        [SerializeField, Min(0.1f)] float cooldown = 5f;
        [SerializeField, Min(1f)] float range = 15f;
        [SerializeField] string targetTag = "Enemy";
        [Tooltip("If set, always strike this.")]
        [SerializeField] Transform manualTarget;
        [Tooltip("Keep striking preview points when no enemies are around (testing / showcase).")]
        [SerializeField] bool fireWithoutTargets = true;
        [SerializeField, Min(1f)] float previewDistance = 8f;
        [SerializeField] float waterHeight = 0f;

        [Header("Whirlpool")]
        [SerializeField, Min(0.3f)] float whirlpoolRadius = 2.6f;
        [Tooltip("Enemies inside this radius get dragged inward.")]
        [SerializeField, Min(0.3f)] float pullRadius = 4.2f;
        [SerializeField, Min(0f)] float pullSpeed = 3.2f;
        [Tooltip("Sideways swirl speed so enemies circle the drain while being pulled.")]
        [SerializeField, Min(0f)] float swirlSpeed = 1.8f;
        [SerializeField, Min(0.1f)] float whirlpoolLife = 3f;
        [SerializeField, Min(0.05f)] float openTime = 0.3f;
        [SerializeField, Min(0.05f)] float implodeTime = 0.35f;
        [Tooltip("Spiral rotation, turns per second.")]
        [SerializeField] float spinSpeed = 0.75f;
        [SerializeField, Min(0f)] float surfaceLift = 0.04f;

        [Header("Damage")]
        [SerializeField] bool applyDamage = true;
        [SerializeField, Min(0f)] float damagePerTick = 10f;
        [SerializeField, Min(0.05f)] float tickInterval = 0.5f;
        [Tooltip("Enemies inside this radius are hurt on each tick.")]
        [SerializeField, Min(0.1f)] float damageRadius = 3f;
        [SerializeField, Min(0f)] float strikeDamage = 15f;

        [Header("Bolt")]
        [SerializeField, Min(1f)] float boltHeight = 13f;
        [Tooltip("Horizontal lean of the bolt's sky end.")]
        [SerializeField, Min(0f)] float boltLean = 4.5f;
        [SerializeField, Range(3, 16)] int boltSegments = 8;
        [SerializeField, Min(0f)] float boltJag = 1.05f;
        [SerializeField, Min(0.02f)] float boltHold = 0.28f;
        [SerializeField, Min(0.02f)] float boltFade = 0.2f;
        [SerializeField, Min(0f)] float boltFlicker = 0.3f;

        [Header("Events")]
        public PointEvent onStrike = new PointEvent();
        public PointEvent onTick = new PointEvent();
        public PointEvent onImplode = new PointEvent();
        [Tooltip("Fired for every target hurt by the strike or a whirlpool tick.")]
        public TransformEvent onHit = new TransformEvent();

        public Phase CurrentPhase => _phase;
        public bool IsBusy => _phase != Phase.Idle;
        public bool AutoFire { get => autoFire; set => autoFire = value; }
        public float Cooldown { get => cooldown; set => cooldown = Mathf.Max(0.1f, value); }
        public float Range { get => range; set => range = Mathf.Max(1f, value); }
        public float DamagePerTick { get => damagePerTick; set => damagePerTick = Mathf.Max(0f, value); }

        static readonly int FadeId = Shader.PropertyToID("_Fade");
        static readonly int SpinId = Shader.PropertyToID("_SpinOffset");
        static readonly int TwistId = Shader.PropertyToID("_Twist");

        readonly List<Transform> _candidates = new List<Transform>();
        readonly List<Transform> _cluster = new List<Transform>();
        Vector3[] _boltPts, _branchPts;
        MaterialPropertyBlock _mpb;
        Color _boltColor = Color.white, _coreColor = Color.white, _branchColor = Color.white;
        float _boltWidth, _coreWidth, _branchWidth, _baseTwist = 2.2f;
        Color _branch2Color = Color.white, _haloColor = Color.white;
        float _branch2Width, _haloWidth;
        Vector3[] _branch2Pts;
        Vector3 _boltTop;
        bool _rejagged;

        Phase _phase = Phase.Idle;
        float _t, _cooldownLeft = 0.6f, _nextTick, _spin, _candidateRefresh;
        Vector3 _center;
        bool _ownsStrikeRoot;

        // ---------------------------------------------------------------- setup

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (!strikeRoot) strikeRoot = transform.Find("Strike");
            if (strikeRoot && strikeRoot.IsChildOf(transform))
            {
                strikeRoot.SetParent(null, true);
                strikeRoot.name = name + " (Strike FX)";
                _ownsStrikeRoot = true;
            }
            if (bolt) { _boltColor = bolt.startColor; _boltWidth = bolt.widthMultiplier; }
            if (boltCore) { _coreColor = boltCore.startColor; _coreWidth = boltCore.widthMultiplier; }
            if (branch) { _branchColor = branch.startColor; _branchWidth = branch.widthMultiplier; }
            if (branch2) { _branch2Color = branch2.startColor; _branch2Width = branch2.widthMultiplier; }
            if (boltHalo) { _haloColor = boltHalo.startColor; _haloWidth = boltHalo.widthMultiplier; }
            if (whirlpoolRenderer && whirlpoolRenderer.sharedMaterial && whirlpoolRenderer.sharedMaterial.HasProperty(TwistId))
                _baseTwist = whirlpoolRenderer.sharedMaterial.GetFloat(TwistId);
            HideAll();
        }

        void OnEnable() { if (_mpb != null) HideAll(); _phase = Phase.Idle; }

        void OnDisable()
        {
            if (_mpb != null) HideAll();
            _phase = Phase.Idle;
        }

        void OnDestroy()
        {
            if (_ownsStrikeRoot && strikeRoot) Destroy(strikeRoot.gameObject);
        }

        void HideAll()
        {
            SetLines(false);
            if (whirlpoolRenderer) whirlpoolRenderer.enabled = false;
        }

        // ---------------------------------------------------------------- public API

        /// <summary>Strike a world point now (ignores cooldown, refused while a whirlpool is open).</summary>
        public bool StrikeAt(Vector3 point)
        {
            if (IsBusy || !strikeRoot) return false;
            _center = new Vector3(point.x, waterHeight, point.z);
            strikeRoot.SetPositionAndRotation(_center, Quaternion.identity);
            _t = 0f;
            _spin = Random.value;
            _nextTick = openTime * 0.5f;
            _rejagged = false;
            BuildBolt(true);
            SetLines(true);
            if (flash) flash.Emit(1);
            if (impactGlow) impactGlow.Emit(1);
            if (ring) ring.Emit(1);
            if (splash) splash.Emit(16);
            RefreshCandidates();
            if (strikeDamage > 0f) HurtWithin(_center, whirlpoolRadius * 0.6f, strikeDamage);
            onStrike.Invoke(_center);
            _phase = Phase.Strike;
            return true;
        }

        public void Fire()
        {
            if (TryPickTarget(out var p)) StrikeAt(p);
            else _cooldownLeft = 0.5f;
        }

        // ---------------------------------------------------------------- loop

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock(); // survives script reload in Play mode

            if (_phase == Phase.Idle)
            {
                if (!autoFire) return;
                _cooldownLeft -= dt;
                if (_cooldownLeft <= 0f) Fire();
                return;
            }

            _t += dt;
            StepBolt();

            float openStart = 0.05f;
            float implodeStart = openStart + whirlpoolLife;
            float end = implodeStart + implodeTime;

            if (_phase == Phase.Strike && _t >= openStart)
            {
                _phase = Phase.Active;
                if (whirlpoolRenderer) whirlpoolRenderer.enabled = true;
            }
            if (_phase == Phase.Active && _t >= implodeStart) _phase = Phase.Implode;

            if (_phase == Phase.Active || _phase == Phase.Implode)
            {
                float open01 = Mathf.Clamp01((_t - openStart) / openTime);
                float scale = BackOut(open01);
                float fade = Mathf.Clamp01(open01 * 3f);
                float spinMul = 1f;
                float twist = _baseTwist;

                if (_phase == Phase.Implode)
                {
                    float k = Mathf.Clamp01((_t - implodeStart) / implodeTime);
                    float e = k * k * k;
                    scale *= 1f - e;
                    spinMul = 1f + 4f * k;
                    twist = _baseTwist * (1f + 1.5f * k);
                    fade *= 1f - k * k;
                }
                else
                {
                    // gentle breathing while open
                    scale *= 1f + Mathf.Sin(_t * 5.5f) * 0.025f;
                    StepPull(dt);
                    StepTicks();
                }

                _spin -= spinSpeed * spinMul * dt;
                if (whirlpool)
                {
                    whirlpool.position = _center + Vector3.up * surfaceLift;
                    whirlpool.localScale = new Vector3(whirlpoolRadius * scale, 1f, whirlpoolRadius * scale);
                }
                if (whirlpoolRenderer)
                {
                    whirlpoolRenderer.GetPropertyBlock(_mpb);
                    _mpb.SetFloat(FadeId, fade);
                    _mpb.SetFloat(SpinId, _spin);
                    _mpb.SetFloat(TwistId, twist);
                    whirlpoolRenderer.SetPropertyBlock(_mpb);
                }
            }

            if (_t >= end)
            {
                if (whirlpoolRenderer) whirlpoolRenderer.enabled = false;
                if (splash) splash.Emit(12);
                if (ring) ring.Emit(1);
                onImplode.Invoke(_center);
                _cooldownLeft = Mathf.Max(0.1f, cooldown - end);
                _phase = Phase.Idle;
            }
        }

        // ---------------------------------------------------------------- whirlpool gameplay

        void StepPull(float dt)
        {
            _candidateRefresh -= dt;
            if (_candidateRefresh <= 0f) RefreshCandidates();

            float pr2 = pullRadius * pullRadius;
            foreach (var c in _candidates)
            {
                if (!c || !c.gameObject.activeInHierarchy) continue;
                Vector3 to = _center - c.position;
                to.y = 0f;
                float d2 = to.sqrMagnitude;
                if (d2 > pr2 || d2 < 0.0001f) continue;
                float d = Mathf.Sqrt(d2);
                Vector3 dir = to / d;
                float near01 = 1f - d / pullRadius;

                float radial = Mathf.Min(pullSpeed * (0.55f + 0.45f * near01) * dt, Mathf.Max(0f, d - 0.35f));
                Vector3 tangent = new Vector3(dir.z, 0f, -dir.x); // matches the spiral's spin
                Vector3 delta = dir * radial + tangent * (swirlSpeed * near01 * dt);

                var rb = c.GetComponent<Rigidbody>();
                if (rb && !rb.isKinematic) rb.MovePosition(rb.position + delta);
                else c.position += delta;
            }
        }

        void StepTicks()
        {
            while (_t >= _nextTick && _nextTick <= 0.05f + whirlpoolLife)
            {
                HurtWithin(_center, damageRadius, damagePerTick);
                onTick.Invoke(_center);
                _nextTick += tickInterval;
            }
        }

        void HurtWithin(Vector3 center, float radius, float amount)
        {
            float r2 = radius * radius;
            for (int i = 0; i < _candidates.Count; i++)
            {
                var c = _candidates[i];
                if (!c || !c.gameObject.activeInHierarchy) continue;
                Vector3 d = c.position - center;
                d.y = 0f;
                if (d.sqrMagnitude > r2) continue;
                if (applyDamage && amount > 0f)
                {
                    var dmg = c.GetComponentInParent<IDamageable>();
                    if (dmg != null && dmg.IsAlive) dmg.TakeDamage(amount, c.position, false);
                }
                onHit.Invoke(c);
            }
        }

        void RefreshCandidates()
        {
            _candidateRefresh = 0.15f;
            _candidates.Clear();
            if (string.IsNullOrEmpty(targetTag)) return;
            GameObject[] found;
            try { found = GameObject.FindGameObjectsWithTag(targetTag); }
            catch (UnityException) { return; }
            foreach (var g in found)
                if (!g.transform.IsChildOf(transform)) _candidates.Add(g.transform);
        }

        // ---------------------------------------------------------------- targeting

        /// <summary>Centre of the densest crowd within range (enemies counted inside the whirlpool radius).</summary>
        bool TryPickTarget(out Vector3 point)
        {
            point = Vector3.zero;
            if (manualTarget && manualTarget.gameObject.activeInHierarchy)
            {
                point = manualTarget.position;
                return true;
            }

            RefreshCandidates();
            Vector3 origin = transform.position;
            float range2 = range * range;
            _cluster.Clear();
            foreach (var c in _candidates)
                if (c.gameObject.activeInHierarchy && Flat(c.position - origin).sqrMagnitude <= range2) _cluster.Add(c);

            if (_cluster.Count > 0)
            {
                float cr2 = whirlpoolRadius * whirlpoolRadius * 1.4f;
                int bestCount = -1;
                float bestDist = float.MaxValue;
                Vector3 best = Vector3.zero;
                foreach (var a in _cluster)
                {
                    int n = 0;
                    Vector3 sum = Vector3.zero;
                    foreach (var b in _cluster)
                    {
                        if (Flat(b.position - a.position).sqrMagnitude > cr2) continue;
                        n++;
                        sum += b.position;
                    }
                    float dist = Flat(a.position - origin).sqrMagnitude;
                    if (n > bestCount || (n == bestCount && dist < bestDist))
                    {
                        bestCount = n;
                        bestDist = dist;
                        best = sum / n;
                    }
                }
                point = best;
                return true;
            }

            if (!fireWithoutTargets) return false;
            float ang = Mathf.Sin(Time.time * 0.41f) * 70f;
            Vector3 dir = Quaternion.AngleAxis(ang, Vector3.up) * Flat(transform.forward).normalized;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            point = origin + dir * previewDistance;
            return true;
        }

        // ---------------------------------------------------------------- bolt

        void BuildBolt(bool newTop)
        {
            int n = boltSegments + 1;
            if (_boltPts == null || _boltPts.Length != n) _boltPts = new Vector3[n];

            if (newTop)
            {
                Vector3 lean = Quaternion.AngleAxis(Random.Range(-35f, 35f), Vector3.up) * new Vector3(0.7f, 0f, 0.7f);
                _boltTop = _center + Vector3.up * boltHeight + lean.normalized * boltLean;
            }
            Vector3 top = _boltTop;
            Vector3 axis = (_center - top).normalized;
            Vector3 side = Vector3.Cross(axis, Vector3.up);
            if (side.sqrMagnitude < 0.01f) side = Vector3.right;
            side.Normalize();

            float sign = Random.value < 0.5f ? -1f : 1f;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)(n - 1);
                Vector3 p = Vector3.Lerp(top, _center, k);
                if (i > 0 && i < n - 1)
                {
                    // chunky alternating zigzag, calmer near the ground so it plugs into the drain
                    float amp = boltJag * Random.Range(0.55f, 1f) * Mathf.Lerp(1f, 0.45f, k);
                    p += side * (sign * amp);
                    sign = -sign;
                }
                _boltPts[i] = p;
            }

            if (bolt) { bolt.positionCount = n; bolt.SetPositions(_boltPts); }
            if (boltCore) { boltCore.positionCount = n; boltCore.SetPositions(_boltPts); }
            if (boltHalo) { boltHalo.positionCount = n; boltHalo.SetPositions(_boltPts); }

            int mid = n / 2;
            BuildBranch(branch, ref _branchPts, Mathf.Clamp(mid - 2 + Random.Range(0, 2), 1, n - 2), axis, side, 1f);
            BuildBranch(branch2, ref _branch2Pts, Mathf.Clamp(mid + 1 + Random.Range(0, 2), 1, n - 2), axis, side, -1f);
        }

        void BuildBranch(LineRenderer lr, ref Vector3[] pts, int from, Vector3 axis, Vector3 side, float dirSign)
        {
            if (!lr) return;
            if (pts == null) pts = new Vector3[4];
            Vector3 b = _boltPts[from];
            float bs = dirSign * (Random.value < 0.75f ? 1f : -1f);
            for (int i = 0; i < pts.Length; i++)
            {
                pts[i] = b;
                b += axis * Random.Range(0.8f, 1.3f) + side * (bs * Random.Range(0.45f, 0.9f));
                if (Random.value < 0.35f) bs = -bs;
            }
            lr.positionCount = pts.Length;
            lr.SetPositions(pts);
        }

        void StepBolt()
        {
            float total = boltHold + boltFade;
            if (_t > total) { SetLines(false); return; }

            // second crackle: re-jag the bolt mid-hold and re-flash so it reads as a heavy double strike
            if (!_rejagged && _t >= boltHold * 0.45f)
            {
                _rejagged = true;
                BuildBolt(false);
                if (flash) flash.Emit(1);
            }

            bool hold = _t <= boltHold;
            float a = hold ? 1f : 1f - (_t - boltHold) / boltFade;
            float flick = 1f + Mathf.Sin(_t * 95f) * boltFlicker * (hold ? 1f : 0.3f);
            float grow = Mathf.Clamp01(_t / 0.035f);
            // punch: extra thick for the first frames of each crackle
            float punch = 1f + 0.45f * Mathf.Clamp01(1f - _t / 0.06f) + 0.3f * Mathf.Clamp01(1f - Mathf.Abs(_t - boltHold * 0.45f) / 0.05f);

            SetLine(boltHalo, _haloColor, _haloWidth * punch * Mathf.Lerp(0.7f, 1f, a), a, grow);
            SetLine(bolt, _boltColor, _boltWidth * flick * punch * Mathf.Lerp(0.6f, 1f, a), a, grow);
            SetLine(boltCore, _coreColor, _coreWidth * punch * Mathf.Lerp(0.5f, 1f, a), a, grow);
            float branchA = a * (_t < boltHold * 0.85f ? 1f : 0f);
            SetLine(branch, _branchColor, _branchWidth * flick, branchA, grow);
            SetLine(branch2, _branch2Color, _branch2Width * flick, branchA * (_rejagged ? 1f : 0f), grow);
        }

        static void SetLine(LineRenderer lr, Color c, float width, float alpha, float grow)
        {
            if (!lr) return;
            c.a *= alpha;
            lr.startColor = c;
            lr.endColor = c;
            lr.widthMultiplier = width * grow;
            lr.enabled = alpha > 0.001f;
        }

        void SetLines(bool on)
        {
            if (bolt) bolt.enabled = on;
            if (boltCore) boltCore.enabled = on;
            if (boltHalo) boltHalo.enabled = on;
            if (branch) branch.enabled = on;
            if (branch2) branch2.enabled = on;
        }

        // ---------------------------------------------------------------- utils

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        static float BackOut(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float t = x - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            DrawCircle(transform.position, range);
            if (IsBusy)
            {
                Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.9f);
                DrawCircle(_center, pullRadius);
                Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.9f);
                DrawCircle(_center, damageRadius);
            }
        }

        static void DrawCircle(Vector3 c, float r)
        {
            Vector3 prev = c + new Vector3(r, 0f, 0f);
            for (int i = 1; i <= 48; i++)
            {
                float a = i / 48f * Mathf.PI * 2f;
                Vector3 p = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
#endif
    }
}
