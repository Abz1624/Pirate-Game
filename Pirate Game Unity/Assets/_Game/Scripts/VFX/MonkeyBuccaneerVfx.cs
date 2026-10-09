using System.Collections.Generic;
using UnityEngine;

namespace HighSeas.VFX
{
    /// <summary>
    /// Drag-and-drop preview for the "Monkey Buccaneer" weapon. Drop the prefab into any scene and press Play:
    /// the dinghy sails a slow circle, turns its bow toward the nearest target, fires a cannonball from the
    /// bow cannon (muzzle smoke + recoil kick) and bobs on the water.
    ///
    /// Targets: <see cref="manualTarget"/>, else the nearest object tagged <see cref="targetTag"/> in range,
    /// else (when <see cref="spawnDummyTargets"/> is on) a ring of floating practice buoys it spawns itself.
    ///
    /// In a real run the ship's WeaponController spawns this prefab and disables this component — gameplay
    /// movement, targeting and damage live there. This script is for look-dev / testing only.
    ///
    /// Rig convention: child "Visuals" (bobs / recoils) with a "Muzzle" child at the cannon tip.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonkeyBuccaneerVfx : MonoBehaviour
    {
        [Header("Rig")]
        [SerializeField] Transform visuals;
        [SerializeField] Transform muzzle;

        [Header("Appear")]
        [Tooltip("Rise out of the water when the preview starts (like the raft surfacing astern in game).")]
        [SerializeField] bool surfaceOnStart = true;
        [SerializeField, Min(0.05f)] float surfaceTime = 0.45f;
        [SerializeField] float surfaceDepth = 0.9f;

        [Header("Sailing")]
        [Tooltip("Sail a slow circle around the drop position.")]
        [SerializeField] bool sailInCircle = true;
        [SerializeField, Min(0f)] float circleRadius = 6.5f;
        [Tooltip("Radians per second around the circle.")]
        [SerializeField] float circleSpeed = 0.55f;
        [SerializeField, Min(0f)] float bobHeight = 0.06f;
        [SerializeField] float bobSpeed = 2.1f;
        [SerializeField] float rollDegrees = 3f;

        [Header("Targeting")]
        [SerializeField] bool autoFire = true;
        [SerializeField, Min(0.05f)] float cooldown = 1.15f;
        [SerializeField, Min(1f)] float range = 15f;
        [Tooltip("If set, always shoots at this.")]
        [SerializeField] Transform manualTarget;
        [SerializeField] string targetTag = "Enemy";
        [Tooltip("Spawn floating practice buoys when nothing else is around (testing).")]
        [SerializeField] bool spawnDummyTargets = true;
        [SerializeField, Range(1, 8)] int dummyCount = 1;
        [SerializeField, Min(2f)] float dummyDistance = 12f;
        [SerializeField, Min(60f)] float turnSpeed = 600f;

        [Header("Cannonball")]
        [SerializeField] GameObject projectilePrefab;
        [SerializeField, Min(0.05f)] float projectileScale = 0.3f;
        [Tooltip("Tint for the shot (doc: orange).")]
        [SerializeField] Color shotColor = new Color(1f, 0.55f, 0.15f);
        [SerializeField, Min(1f)] float projectileSpeed = 18f;
        [Tooltip("Small lob so the shot reads as a cannonball, not a laser.")]
        [SerializeField, Min(0f)] float arcHeight = 0.8f;

        [Header("FX")]
        [Tooltip("Muzzle burst (e.g. DeckCannons_Muzzle_VFX). All child particle systems are played on fire.")]
        [SerializeField] GameObject muzzleVfxPrefab;
        [SerializeField, Min(0.05f)] float muzzleScale = 0.7f;
        [SerializeField] GameObject impactVfxPrefab;
        [SerializeField, Min(0.05f)] float impactScale = 0.6f;
        [SerializeField, Min(0f)] float recoilDistance = 0.45f;
        [SerializeField] float recoilPitch = 9f;

        sealed class Ball
        {
            public Transform t;
            public TrailRenderer trail;
            public Vector3 p0, p1, p2;
            public float time, duration;
            public Transform target;
            public bool active;
        }

        readonly List<Ball> _balls = new List<Ball>();
        readonly List<Transform> _dummies = new List<Transform>();
        readonly Dictionary<Transform, float> _punch = new Dictionary<Transform, float>();
        Transform _fxRoot;
        ParticleSystem[] _muzzleSystems;
        Transform _muzzleFx;
        Vector3 _center;
        float _angle;
        float _yaw;
        float _cooldownTimer;
        float _recoil;
        float _age;
        Transform _target;

        void Start()
        {
            if (!visuals) visuals = transform.Find("Visuals");
            if (!muzzle) muzzle = transform.Find("Visuals/Muzzle");
            _center = transform.position;
            _yaw = transform.eulerAngles.y;
            _angle = 0f;
            _cooldownTimer = 0.4f;
            _age = surfaceOnStart ? 0f : surfaceTime;
            if (spawnDummyTargets && !manualTarget) SpawnDummies();
        }

        void OnDestroy()
        {
            if (_fxRoot) Destroy(_fxRoot.gameObject);
        }

        void Update()
        {
            var dt = Time.deltaTime;
            var pos = transform.position;
            Vector3 moveDir = Vector3.zero;

            if (sailInCircle && circleRadius > 0.01f)
            {
                _angle += circleSpeed * dt;
                var next = _center + new Vector3(Mathf.Cos(_angle), 0f, Mathf.Sin(_angle)) * circleRadius;
                next.y = _center.y;
                moveDir = next - pos;
                moveDir.y = 0f;
                pos = Vector3.Lerp(pos, next, 1f - Mathf.Exp(-4f * dt));
            }

            _target = ResolveTarget(pos);
            Vector3 face = _target ? _target.position - pos : moveDir;
            face.y = 0f;
            if (face.sqrMagnitude > 0.0001f)
            {
                var desired = Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg;
                _yaw = Mathf.MoveTowardsAngle(_yaw, desired, (_target ? turnSpeed : 220f) * dt);
            }

            transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, _yaw, 0f));

            _age += dt;
            var surfaced = _age >= surfaceTime;
            if (autoFire && surfaced)
            {
                _cooldownTimer -= dt;
                if (_cooldownTimer <= 0f && _target)
                {
                    _cooldownTimer = cooldown;
                    Fire(_target);
                }
            }

            _recoil = Mathf.MoveTowards(_recoil, 0f, dt * 4.5f);
            UpdateVisuals();
            StepBalls(dt);
            StepDummies(dt);
        }

        /// <summary>Fire one shot at the current/nearest target (also callable from UI or a test button).</summary>
        [ContextMenu("Fire Now")]
        public void FireNow()
        {
            var t = _target ? _target : ResolveTarget(transform.position);
            if (t) Fire(t);
        }

        Transform ResolveTarget(Vector3 from)
        {
            if (manualTarget) return manualTarget;

            Transform best = null;
            var bestSqr = range * range;
            if (!string.IsNullOrEmpty(targetTag))
            {
                GameObject[] tagged = null;
                try { tagged = GameObject.FindGameObjectsWithTag(targetTag); }
                catch (UnityException) { /* tag not defined in this project */ }
                if (tagged != null)
                {
                    for (var i = 0; i < tagged.Length; i++)
                    {
                        var d = tagged[i].transform.position - from;
                        d.y = 0f;
                        if (d.sqrMagnitude < bestSqr) { bestSqr = d.sqrMagnitude; best = tagged[i].transform; }
                    }
                }
            }

            if (best) return best;
            bestSqr = float.MaxValue;
            for (var i = 0; i < _dummies.Count; i++)
            {
                if (!_dummies[i]) continue;
                var d = _dummies[i].position - from;
                d.y = 0f;
                if (d.sqrMagnitude < bestSqr) { bestSqr = d.sqrMagnitude; best = _dummies[i]; }
            }
            return best;
        }

        void Fire(Transform target)
        {
            var aim = target.position - transform.position;
            aim.y = 0f;
            if (aim.sqrMagnitude < 0.0001f) return;
            _yaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            UpdateVisuals();

            var start = muzzle ? muzzle.position : transform.position + transform.forward * 1.6f + Vector3.up * 0.6f;
            var end = target.position;
            end.y = Mathf.Max(end.y, 0.3f);
            var dist = Vector3.Distance(start, end);

            var b = GetBall();
            b.p0 = start;
            b.p2 = end;
            b.p1 = (start + end) * 0.5f + Vector3.up * arcHeight;
            b.time = 0f;
            b.duration = Mathf.Max(0.12f, dist / projectileSpeed);
            b.target = target;
            b.active = true;
            b.t.position = start;
            b.t.gameObject.SetActive(true);
            if (b.trail) b.trail.Clear();

            _recoil = 1f;
            PlayMuzzle(start, aim.normalized);
        }

        void UpdateVisuals()
        {
            if (!visuals || visuals == transform) return;
            var time = Time.time;
            var kick = _recoil * _recoil;
            var rise = 0f;
            if (_age < surfaceTime)
            {
                var k = Mathf.Clamp01(_age / surfaceTime);
                rise = -surfaceDepth * (1f - k) * (1f - k) + Mathf.Sin(k * Mathf.PI) * 0.12f;
            }
            visuals.localPosition = new Vector3(0f, Mathf.Sin(time * bobSpeed) * bobHeight + rise, -recoilDistance * kick);
            visuals.localRotation = Quaternion.Euler(-recoilPitch * kick, 0f, Mathf.Sin(time * 1.6f + 1.3f) * rollDegrees);
        }

        // ---------------------------------------------------------------- shots

        void StepBalls(float dt)
        {
            for (var i = 0; i < _balls.Count; i++)
            {
                var b = _balls[i];
                if (!b.active) continue;
                b.time += dt;
                var k = Mathf.Clamp01(b.time / b.duration);
                var u = 1f - k;
                b.t.position = u * u * b.p0 + 2f * u * k * b.p1 + k * k * b.p2;
                if (k >= 1f)
                {
                    b.active = false;
                    b.t.gameObject.SetActive(false);
                    Impact(b.p2, b.target);
                }
            }
        }

        void Impact(Vector3 point, Transform target)
        {
            if (impactVfxPrefab)
            {
                var fx = Instantiate(impactVfxPrefab, point, Quaternion.identity, FxRoot());
                fx.transform.localScale = Vector3.one * impactScale;
                PlayAll(fx);
                Destroy(fx, 3f);
            }
            if (target && _dummies.Contains(target)) _punch[target] = 1f;
        }

        void PlayMuzzle(Vector3 origin, Vector3 dir)
        {
            if (!muzzleVfxPrefab) return;
            if (!_muzzleFx)
            {
                var go = Instantiate(muzzleVfxPrefab, FxRoot());
                go.name = "MonkeyBuccaneer_Muzzle";
                _muzzleFx = go.transform;
                _muzzleSystems = go.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            }
            _muzzleFx.SetPositionAndRotation(origin, Quaternion.LookRotation(dir, Vector3.up));
            _muzzleFx.localScale = Vector3.one * muzzleScale;
            for (var i = 0; i < _muzzleSystems.Length; i++)
            {
                _muzzleSystems[i].Clear(false);
                _muzzleSystems[i].Play(false);
            }
        }

        static void PlayAll(GameObject go)
        {
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) ps.Play(false);
        }

        Ball GetBall()
        {
            for (var i = 0; i < _balls.Count; i++) if (!_balls[i].active) return _balls[i];
            GameObject go;
            if (projectilePrefab)
            {
                go = Instantiate(projectilePrefab, FxRoot());
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
                foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.SetParent(FxRoot(), false);
                Destroy(go.GetComponent<Collider>());
            }
            go.name = "MonkeyBuccaneer_Cannonball";
            go.transform.localScale = Vector3.one * projectileScale;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", shotColor);
            block.SetColor("_Color", shotColor);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true)) r.SetPropertyBlock(block);
            var ball = new Ball { t = go.transform, trail = go.GetComponentInChildren<TrailRenderer>(true) };
            _balls.Add(ball);
            return ball;
        }

        Transform FxRoot()
        {
            if (!_fxRoot) _fxRoot = new GameObject(name + " (FX)").transform;
            return _fxRoot;
        }

        // ---------------------------------------------------------------- practice buoys

        void SpawnDummies()
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = new Color(0.85f, 0.25f, 0.18f);
            for (var i = 0; i < dummyCount; i++)
            {
                var a = (i + 0.5f) * Mathf.PI * 2f / dummyCount;
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "PracticeBuoy_" + i;
                go.transform.SetParent(FxRoot(), false);
                go.transform.position = _center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * dummyDistance + Vector3.up * 0.4f;
                go.transform.localScale = new Vector3(1f, 0.5f, 1f);
                go.GetComponent<Renderer>().sharedMaterial = mat;
                Destroy(go.GetComponent<Collider>());
                _dummies.Add(go.transform);
            }
        }

        void StepDummies(float dt)
        {
            for (var i = 0; i < _dummies.Count; i++)
            {
                var d = _dummies[i];
                if (!d) continue;
                _punch.TryGetValue(d, out var p);
                p = Mathf.MoveTowards(p, 0f, dt * 5f);
                _punch[d] = p;
                var s = 1f + p * 0.35f;
                d.localScale = new Vector3(s, 0.5f * (1f - p * 0.25f), s);
                var pos = d.position;
                pos.y = 0.4f + Mathf.Sin(Time.time * 1.8f + i) * 0.08f;
                d.position = pos;
            }
        }
    }
}
