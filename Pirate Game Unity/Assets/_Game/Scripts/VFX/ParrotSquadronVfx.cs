using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HighSeas.VFX
{
    /// <summary>
    /// Drag-and-drop "Parrot Squadron" weapon visual. A wedge of red pirate parrots pops off the brig,
    /// each with a small cannon slung under its belly, flies to the target, fires cannonball volleys,
    /// then keeps flying past, climbs away and fades out. The next sortie launches fresh from the brig.
    /// Visual + motion only — damage is left to gameplay via <see cref="onHit"/> / <see cref="onImpact"/>.
    ///
    /// Rig convention (made by ParrotSquadronBuilder): children "Parrot_N", each with
    /// Model/WingL, Model/WingR, Model/Cannon/Muzzle (Flash + Smoke particle systems) and a "Trail" TrailRenderer.
    /// Child "LaunchPoint" marks where they leave the ship.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParrotSquadronVfx : MonoBehaviour
    {
        public enum Phase { Idle, Launch, Approach, Attack, Exit }

        [System.Serializable] public sealed class PointEvent : UnityEvent<Vector3> { }
        [System.Serializable] public sealed class TransformEvent : UnityEvent<Transform> { }

        [Header("Origin")]
        [Tooltip("Where the parrots take off from the brig. Defaults to child 'LaunchPoint' or this transform.")]
        [SerializeField] Transform launchPoint;

        [Header("Targeting")]
        [SerializeField] bool autoFire = true;
        [SerializeField, Min(0f)] float cooldown = 1.5f;
        [Tooltip("If set, the squadron always attacks this.")]
        [SerializeField] Transform manualTarget;
        [SerializeField] string targetTag = "Enemy";
        [SerializeField, Min(1f)] float range = 22f;
        [Tooltip("Keep doing sorties at preview points when no target is around (testing / showcase).")]
        [SerializeField] bool fireWithoutTargets = true;
        [SerializeField, Min(1f)] float previewDistance = 14f;

        [Header("Flight")]
        [SerializeField] float cruiseHeight = 3.2f;
        [SerializeField, Min(0.5f)] float flySpeed = 9f;
        [Tooltip("How far short of the target the squadron stops to shoot.")]
        [SerializeField, Min(0f)] float attackStandoff = 6.5f;
        [Tooltip("Slow forward creep while shooting.")]
        [SerializeField] float attackDrift = 0.6f;
        [SerializeField, Min(0.01f)] float followSmoothing = 0.22f;
        [SerializeField, Min(0f)] float launchStagger = 0.07f;
        [SerializeField, Min(0.01f)] float launchTime = 0.45f;

        [Header("Fly away & fade")]
        [SerializeField, Min(0.5f)] float exitSpeed = 13f;
        [Tooltip("Upward speed while flying away.")]
        [SerializeField] float exitClimb = 2.2f;
        [Tooltip("Max sideways turn (degrees) as they peel off.")]
        [SerializeField] float exitTurn = 25f;
        [Tooltip("Time flying away at full opacity before the fade starts.")]
        [SerializeField, Min(0f)] float exitFadeDelay = 0.45f;
        [SerializeField, Min(0.05f)] float exitFadeTime = 0.8f;
        [Tooltip("Scale at the end of the fade.")]
        [SerializeField, Range(0f, 1f)] float exitEndScale = 0.75f;
        [Tooltip("Opaque materials on the parrots and their transparent twins, same order. Filled by the builder.")]
        [SerializeField] Material[] opaqueMaterials = new Material[0];
        [SerializeField] Material[] fadeMaterials = new Material[0];

        [Header("Formation (wedge)")]
        [SerializeField, Min(0.2f)] float slotSpacing = 1.7f;
        [SerializeField, Min(0.2f)] float rowDepth = 1.4f;
        [SerializeField] float heightJitter = 0.35f;

        [Header("Wings & body")]
        [SerializeField] float flapSpeed = 16f;
        [SerializeField] float flapAngle = 38f;
        [SerializeField] float hoverFlapBoost = 1.35f;
        [SerializeField] float bobAmount = 0.12f;
        [SerializeField] float bankAngle = 28f;

        [Header("Cannons")]
        [SerializeField, Range(1, 10)] int shotsPerParrot = 3;
        [SerializeField, Min(0.05f)] float shotInterval = 0.42f;
        [Tooltip("Random spread of the impact points around the target.")]
        [SerializeField, Min(0f)] float shotSpread = 2.2f;
        [SerializeField, Min(1f)] float ballSpeed = 13f;
        [Tooltip("How much the ball keeps flying along the barrel before dropping on the target.")]
        [SerializeField, Range(0f, 0.8f)] float ballCarry = 0.4f;
        [SerializeField] float ballArcHeight = 0.6f;
        [SerializeField, Min(0f)] float recoilDistance = 0.12f;
        [SerializeField, Min(0.01f)] float recoilRecover = 0.18f;

        [Header("Projectile look")]
        [SerializeField] Mesh ballMesh;
        [SerializeField] Material ballMaterial;
        [SerializeField, Min(0.01f)] float ballScale = 0.26f;
        [SerializeField] Material ballTrailMaterial;
        [SerializeField, Min(0f)] float ballTrailTime = 0.4f;
        [SerializeField, Min(0f)] float ballTrailWidth = 0.16f;
        [SerializeField] GameObject impactSplashPrefab;
        [SerializeField, Min(0.01f)] float impactSplashScale = 0.45f;

        [Header("Hits")]
        [SerializeField, Min(0.1f)] float hitRadius = 1.4f;

        [Header("Events")]
        public PointEvent onFire = new PointEvent();
        public PointEvent onImpact = new PointEvent();
        [Tooltip("Fired for every tagged collider inside hitRadius of an impact.")]
        public TransformEvent onHit = new TransformEvent();

        public Phase CurrentPhase => _phase;
        public bool IsBusy => _phase != Phase.Idle;

        sealed class Parrot
        {
            public Transform root, model, wingL, wingR, cannon, muzzle;
            public ParticleSystem flash, smoke;
            public TrailRenderer trail;
            public Renderer[] renderers;
            public Material[][] opaqueSets, fadeSets;
            public Vector3 cannonRest, vel, lastVel;
            public Quaternion wingLRest, wingRRest;
            public float baseScale, phaseOffset, heightOffset, recoil, nextShot, roll;
            public int shotsLeft;
            public bool fading;
        }

        sealed class Ball
        {
            public Transform t;
            public TrailRenderer trail;
            public Vector3 p0, p1, p2;
            public float time, duration;
            public bool active;
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        readonly List<Parrot> _parrots = new List<Parrot>();
        readonly List<Ball> _balls = new List<Ball>();
        readonly List<ParticleSystem[]> _splashes = new List<ParticleSystem[]>();
        readonly List<Transform> _splashRoots = new List<Transform>();
        MaterialPropertyBlock _mpb;
        int _splashCursor;
        Transform _fxRoot;

        Phase _phase = Phase.Idle;
        float _phaseTime, _cooldownLeft, _exitTurnSign = 1f;
        Vector3 _anchor, _heading = Vector3.forward, _targetPoint;
        Transform _target;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (!launchPoint)
            {
                var lp = transform.Find("LaunchPoint");
                launchPoint = lp ? lp : transform;
            }
            CollectRig();
            foreach (var p in _parrots) HideParrot(p);
        }

        void OnDestroy()
        {
            if (_fxRoot) Destroy(_fxRoot.gameObject);
        }

        void CollectRig()
        {
            _parrots.Clear();
            int i = 0;
            foreach (Transform child in transform)
            {
                if (!child.name.StartsWith("Parrot")) continue;
                var p = new Parrot
                {
                    root = child,
                    model = child.Find("Model") ?? child,
                    baseScale = child.localScale.x,
                    phaseOffset = i * 1.37f,
                    heightOffset = Mathf.Sin(i * 2.1f) * heightJitter
                };
                p.wingL = p.model.Find("WingL");
                p.wingR = p.model.Find("WingR");
                p.cannon = p.model.Find("Cannon");
                p.muzzle = p.cannon ? p.cannon.Find("Muzzle") : null;
                if (p.muzzle)
                {
                    var f = p.muzzle.Find("Flash");
                    var s = p.muzzle.Find("Smoke");
                    if (f) p.flash = f.GetComponent<ParticleSystem>();
                    if (s) p.smoke = s.GetComponent<ParticleSystem>();
                }
                var tr = child.Find("Trail");
                if (tr) p.trail = tr.GetComponent<TrailRenderer>();
                if (p.wingL) p.wingLRest = p.wingL.localRotation;
                if (p.wingR) p.wingRRest = p.wingR.localRotation;
                if (p.cannon) p.cannonRest = p.cannon.localPosition;

                // Mesh renderers + their opaque/fade material sets.
                var list = new List<Renderer>();
                foreach (var r in child.GetComponentsInChildren<MeshRenderer>(true)) list.Add(r);
                p.renderers = list.ToArray();
                p.opaqueSets = new Material[p.renderers.Length][];
                p.fadeSets = new Material[p.renderers.Length][];
                for (int r = 0; r < p.renderers.Length; r++)
                {
                    var mats = p.renderers[r].sharedMaterials;
                    p.opaqueSets[r] = mats;
                    var fade = new Material[mats.Length];
                    for (int m = 0; m < mats.Length; m++) fade[m] = FadeTwin(mats[m]);
                    p.fadeSets[r] = fade;
                }
                _parrots.Add(p);
                i++;
            }
        }

        Material FadeTwin(Material m)
        {
            for (int i = 0; i < opaqueMaterials.Length && i < fadeMaterials.Length; i++)
                if (opaqueMaterials[i] == m && fadeMaterials[i]) return fadeMaterials[i];
            return m;
        }

        // ---------------------------------------------------------------- public API

        /// <summary>Send the squadron at a world point (optionally tracking a transform).</summary>
        public bool Launch(Vector3 point, Transform track = null)
        {
            if (IsBusy || _parrots.Count == 0) return false;
            _target = track;
            _targetPoint = point;
            _anchor = LaunchPos() + Vector3.up * cruiseHeight;
            Vector3 d = Flat(point - _anchor);
            _heading = d.sqrMagnitude > 0.01f ? d.normalized : transform.forward;
            _exitTurnSign = Random.value < 0.5f ? -1f : 1f;
            foreach (var p in _parrots)
            {
                SetOpaque(p);
                p.root.gameObject.SetActive(true);
                p.root.position = LaunchPos();
                p.root.rotation = Quaternion.LookRotation(_heading, Vector3.up);
                p.root.localScale = Vector3.zero;
                p.vel = Vector3.up * 2f;
                p.lastVel = p.vel;
                p.roll = 0f;
                p.shotsLeft = shotsPerParrot;
                p.recoil = 0f;
                if (p.trail) { p.trail.Clear(); p.trail.emitting = true; }
                if (p.smoke) { p.smoke.Clear(); p.smoke.Play(); }
            }
            SetPhase(Phase.Launch);
            return true;
        }

        public void Fire()
        {
            Transform t;
            Vector3 point;
            if (PickTarget(out t, out point)) Launch(point, t);
        }

        // ---------------------------------------------------------------- loop

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _phaseTime += dt;

            if (_phase == Phase.Idle)
            {
                if (autoFire)
                {
                    _cooldownLeft -= dt;
                    if (_cooldownLeft <= 0f) Fire();
                }
            }
            else
            {
                StepSquadron(dt);
            }
            StepBalls(dt);
        }

        void StepSquadron(float dt)
        {
            if (_phase != Phase.Exit && _target && _target.gameObject.activeInHierarchy) _targetPoint = _target.position;

            float cruiseY = LaunchPos().y + cruiseHeight;
            Vector3 toTarget = Flat(_targetPoint - _anchor);
            Vector3 standoff = _targetPoint - (toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : _heading) * attackStandoff;
            standoff.y = cruiseY;

            float fade01 = 0f;
            switch (_phase)
            {
                case Phase.Launch:
                    if (_phaseTime >= launchTime + launchStagger * _parrots.Count) SetPhase(Phase.Approach);
                    break;
                case Phase.Approach:
                    _anchor = Vector3.MoveTowards(_anchor, standoff, flySpeed * dt);
                    TurnHeading(Flat(standoff - _anchor), 5f, dt);
                    if ((_anchor - standoff).sqrMagnitude < 0.04f)
                    {
                        for (int i = 0; i < _parrots.Count; i++)
                            _parrots[i].nextShot = 0.1f + i * (shotInterval / Mathf.Max(1, _parrots.Count)) + Random.value * 0.08f;
                        SetPhase(Phase.Attack);
                    }
                    break;
                case Phase.Attack:
                    _anchor += Flat(_targetPoint - _anchor).normalized * attackDrift * dt;
                    TurnHeading(Flat(_targetPoint - _anchor), 5f, dt);
                    bool anyLeft = false;
                    foreach (var p in _parrots)
                    {
                        if (p.shotsLeft <= 0) continue;
                        anyLeft = true;
                        if (_phaseTime >= p.nextShot)
                        {
                            Shoot(p);
                            p.shotsLeft--;
                            p.nextShot = _phaseTime + shotInterval * Random.Range(0.85f, 1.15f);
                        }
                    }
                    if (!anyLeft && _phaseTime > 0.25f + shotInterval * shotsPerParrot) SetPhase(Phase.Exit);
                    break;
                case Phase.Exit:
                {
                    // Accelerate past the target, peel off to one side and climb.
                    float speed = Mathf.Lerp(attackDrift, exitSpeed, Mathf.Clamp01(_phaseTime / 0.6f));
                    float turn = exitTurn * _exitTurnSign * Mathf.Clamp01(_phaseTime / (exitFadeDelay + exitFadeTime));
                    Vector3 dir = Quaternion.AngleAxis(turn * dt * 2f, Vector3.up) * _heading;
                    _heading = Flat(dir).normalized;
                    _anchor += _heading * speed * dt + Vector3.up * exitClimb * dt;

                    fade01 = Mathf.Clamp01((_phaseTime - exitFadeDelay) / exitFadeTime);
                    if (fade01 >= 1f)
                    {
                        foreach (var p in _parrots) HideParrot(p);
                        _cooldownLeft = cooldown;
                        SetPhase(Phase.Idle);
                        return;
                    }
                    break;
                }
            }

            Vector3 fwd = _heading;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            float t = Time.time;

            for (int i = 0; i < _parrots.Count; i++)
            {
                var p = _parrots[i];
                if (!p.root.gameObject.activeSelf) continue;

                Vector3 slot = _anchor + SlotOffset(i, fwd, right) + Vector3.up * p.heightOffset;
                slot += Vector3.up * Mathf.Sin(t * 3.1f + p.phaseOffset) * bobAmount;

                float scale01 = 1f;
                Vector3 pos;
                if (_phase == Phase.Launch)
                {
                    float k = Mathf.Clamp01((_phaseTime - i * launchStagger) / launchTime);
                    float e = 1f - (1f - k) * (1f - k);
                    pos = Vector3.Lerp(LaunchPos(), slot, e) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.8f;
                    scale01 = BackOut(k);
                    p.vel = (pos - p.root.position) / dt;
                }
                else
                {
                    pos = Vector3.SmoothDamp(p.root.position, slot, ref p.vel, followSmoothing, 60f, dt);
                }

                if (_phase == Phase.Exit)
                {
                    // Stagger the fade a little per bird so they don't vanish in lockstep.
                    float f = Mathf.Clamp01(fade01 * 1.15f - i * 0.025f);
                    if (f > 0f && !p.fading) BeginFade(p);
                    if (p.fading) ApplyFade(p, 1f - f);
                    scale01 = Mathf.Lerp(1f, exitEndScale, f);
                }

                p.root.position = pos;
                p.root.localScale = Vector3.one * p.baseScale * scale01;

                // Facing: along velocity while travelling, at the target while shooting.
                Vector3 look = _phase == Phase.Attack ? Flat(_targetPoint - pos) : Flat(p.vel);
                if (look.sqrMagnitude < 0.04f) look = fwd;
                Vector3 accel = (p.vel - p.lastVel) / dt;
                p.lastVel = p.vel;
                float targetRoll = -Mathf.Clamp(Vector3.Dot(accel, Vector3.Cross(Vector3.up, look.normalized)) * 0.06f, -1f, 1f) * bankAngle;
                p.roll = Mathf.Lerp(p.roll, targetRoll, 1f - Mathf.Exp(-8f * dt));
                float pitch = Mathf.Clamp(-p.vel.y * 3f, -30f, 30f);
                Quaternion want = Quaternion.LookRotation(look.normalized, Vector3.up) * Quaternion.Euler(pitch, 0f, p.roll);
                p.root.rotation = Quaternion.Slerp(p.root.rotation, want, 1f - Mathf.Exp(-10f * dt));

                // Wings
                float boost = _phase == Phase.Attack || _phase == Phase.Exit ? hoverFlapBoost : 1f;
                float flap = Mathf.Sin(t * flapSpeed * boost + p.phaseOffset) * flapAngle;
                if (p.wingL) p.wingL.localRotation = p.wingLRest * Quaternion.Euler(0f, 0f, -flap);
                if (p.wingR) p.wingR.localRotation = p.wingRRest * Quaternion.Euler(0f, 0f, flap);

                // Cannon recoil
                if (p.cannon)
                {
                    p.recoil = Mathf.MoveTowards(p.recoil, 0f, dt / recoilRecover);
                    p.cannon.localPosition = p.cannonRest - Vector3.forward * (recoilDistance * p.recoil);
                }
            }
        }

        void TurnHeading(Vector3 dir, float rate, float dt)
        {
            if (dir.sqrMagnitude < 0.01f) return;
            _heading = Vector3.Slerp(_heading, dir.normalized, 1f - Mathf.Exp(-rate * dt)).normalized;
        }

        Vector3 SlotOffset(int i, Vector3 fwd, Vector3 right)
        {
            if (i == 0) return Vector3.zero;
            int row = (i + 1) / 2;
            float side = (i % 2 == 1) ? 1f : -1f;
            return -fwd * (row * rowDepth) + right * (side * row * slotSpacing * 0.5f);
        }

        // ---------------------------------------------------------------- fade

        void BeginFade(Parrot p)
        {
            p.fading = true;
            if (p.trail) p.trail.emitting = false;
            if (p.smoke) p.smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            for (int r = 0; r < p.renderers.Length; r++) p.renderers[r].sharedMaterials = p.fadeSets[r];
        }

        void ApplyFade(Parrot p, float alpha)
        {
            for (int r = 0; r < p.renderers.Length; r++)
            {
                var rend = p.renderers[r];
                var mats = p.fadeSets[r];
                for (int m = 0; m < mats.Length; m++)
                {
                    if (!mats[m] || !mats[m].HasProperty(BaseColorId)) continue;
                    Color c = mats[m].GetColor(BaseColorId);
                    c.a *= alpha;
                    _mpb.Clear();
                    _mpb.SetColor(BaseColorId, c);
                    rend.SetPropertyBlock(_mpb, m);
                }
            }
        }

        void SetOpaque(Parrot p)
        {
            p.fading = false;
            for (int r = 0; r < p.renderers.Length; r++)
            {
                var rend = p.renderers[r];
                rend.sharedMaterials = p.opaqueSets[r];
                for (int m = 0; m < p.opaqueSets[r].Length; m++) rend.SetPropertyBlock(null, m);
            }
        }

        // ---------------------------------------------------------------- shooting

        void Shoot(Parrot p)
        {
            if (!p.muzzle) return;
            Vector2 r = Random.insideUnitCircle * shotSpread;
            Vector3 aim = _targetPoint + new Vector3(r.x, 0f, r.y);
            Vector3 start = p.muzzle.position;
            Vector3 dir = p.muzzle.forward;
            float dist = Vector3.Distance(start, aim);

            var b = GetBall();
            b.p0 = start;
            b.p1 = start + dir * (dist * ballCarry) + Vector3.up * ballArcHeight;
            b.p2 = aim;
            b.time = 0f;
            b.duration = Mathf.Max(0.15f, dist / ballSpeed);
            b.active = true;
            b.t.position = start;
            b.t.gameObject.SetActive(true);
            if (b.trail) b.trail.Clear();

            p.recoil = 1f;
            if (p.flash) { p.flash.Clear(true); p.flash.Play(true); }
            onFire.Invoke(start);
        }

        void StepBalls(float dt)
        {
            foreach (var b in _balls)
            {
                if (!b.active) continue;
                b.time += dt;
                float k = Mathf.Clamp01(b.time / b.duration);
                float u = 1f - k;
                b.t.position = u * u * b.p0 + 2f * u * k * b.p1 + k * k * b.p2;
                b.t.Rotate(Vector3.right, 720f * dt, Space.Self);
                if (k >= 1f)
                {
                    b.active = false;
                    b.t.gameObject.SetActive(false);
                    Impact(b.p2);
                }
            }
        }

        void Impact(Vector3 point)
        {
            PlaySplash(point);
            onImpact.Invoke(point);
            if (string.IsNullOrEmpty(targetTag)) return;
            var hits = Physics.OverlapSphere(point, hitRadius, ~0, QueryTriggerInteraction.Collide);
            var seen = new HashSet<Transform>();
            foreach (var h in hits)
            {
                Transform t = h.attachedRigidbody ? h.attachedRigidbody.transform : h.transform;
                if (t.IsChildOf(transform) || !seen.Add(t)) continue;
                if (!t.CompareTag(targetTag)) continue;
                onHit.Invoke(t);
            }
        }

        // ---------------------------------------------------------------- pools

        Transform FxRoot()
        {
            if (!_fxRoot) _fxRoot = new GameObject(name + " (FX)").transform;
            return _fxRoot;
        }

        Ball GetBall()
        {
            foreach (var b in _balls) if (!b.active) return b;
            var go = new GameObject("ParrotCannonball");
            go.transform.SetParent(FxRoot(), false);
            go.transform.localScale = Vector3.one * ballScale;
            if (ballMesh)
            {
                go.AddComponent<MeshFilter>().sharedMesh = ballMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = ballMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            TrailRenderer tr = null;
            if (ballTrailMaterial && ballTrailTime > 0f)
            {
                tr = go.AddComponent<TrailRenderer>();
                tr.sharedMaterial = ballTrailMaterial;
                tr.time = ballTrailTime;
                tr.minVertexDistance = 0.1f;
                tr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
                tr.widthMultiplier = ballTrailWidth;
                var g = new Gradient();
                g.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
                tr.colorGradient = g;
                tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var ball = new Ball { t = go.transform, trail = tr };
            _balls.Add(ball);
            return ball;
        }

        void PlaySplash(Vector3 point)
        {
            if (!impactSplashPrefab) return;
            if (_splashRoots.Count < 12)
            {
                var go = Instantiate(impactSplashPrefab, FxRoot());
                go.transform.localScale = Vector3.one * impactSplashScale;
                var systems = go.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var s in systems)
                {
                    var m = s.main;
                    m.playOnAwake = false;
                    m.stopAction = ParticleSystemStopAction.None;
                    m.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
                _splashRoots.Add(go.transform);
                _splashes.Add(systems);
                _splashCursor = _splashRoots.Count - 1;
            }
            else _splashCursor = (_splashCursor + 1) % _splashRoots.Count;

            _splashRoots[_splashCursor].position = point;
            foreach (var s in _splashes[_splashCursor]) { s.Clear(); s.Play(); }
        }

        // ---------------------------------------------------------------- helpers

        bool PickTarget(out Transform t, out Vector3 point)
        {
            t = null;
            point = Vector3.zero;
            if (manualTarget && manualTarget.gameObject.activeInHierarchy)
            {
                t = manualTarget;
                point = manualTarget.position;
                return true;
            }
            Vector3 origin = LaunchPos();
            if (!string.IsNullOrEmpty(targetTag))
            {
                GameObject[] candidates;
                try { candidates = GameObject.FindGameObjectsWithTag(targetTag); }
                catch (UnityException) { candidates = new GameObject[0]; }
                float best = range * range;
                foreach (var c in candidates)
                {
                    if (c.transform.IsChildOf(transform)) continue;
                    float d = Flat(c.transform.position - origin).sqrMagnitude;
                    if (d < best) { best = d; t = c.transform; }
                }
                if (t) { point = t.position; return true; }
            }
            if (!fireWithoutTargets) return false;
            float ang = Mathf.Sin(Time.time * 0.37f) * 75f;
            Vector3 dir = Quaternion.AngleAxis(ang, Vector3.up) * Flat(transform.forward).normalized;
            point = origin + dir * previewDistance;
            point.y = 0f;
            return true;
        }

        void HideParrot(Parrot p)
        {
            if (p.trail) { p.trail.emitting = false; p.trail.Clear(); }
            if (p.smoke) p.smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            SetOpaque(p);
            p.root.gameObject.SetActive(false);
        }

        void SetPhase(Phase ph)
        {
            _phase = ph;
            _phaseTime = 0f;
        }

        Vector3 LaunchPos() => launchPoint ? launchPoint.position : transform.position;

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        static float BackOut(float k)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float x = k - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(launchPoint ? launchPoint.position : transform.position, 0.3f);
            if (Application.isPlaying && IsBusy) Gizmos.DrawWireSphere(_targetPoint, shotSpread);
        }
    }
}
