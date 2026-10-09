using System.Collections.Generic;
using HighSeas.Combat;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace HighSeas.VFX
{
    /// <summary>
    /// Drag-and-drop "Ink Pot" weapon. Every <see cref="cooldown"/> seconds a round cream clay pot with a cork
    /// stopper is lobbed along a projectile arc toward the nearest enemy. Just above the water it bursts open in
    /// mid-air: the cork pops, the clay shards open like petals, glossy black-violet ink gushes out, a small
    /// cartoon squid tentacle curls out of the opening and a few ink droplets (max 12 particles) fly outward.
    /// The ink lands as a flat black-violet slick with a pulsing violet rim and code-generated water rings,
    /// which damages enemies inside it every <see cref="tickInterval"/> seconds, then fades and cleans itself up.
    ///
    /// Rig convention (made by InkPotBuilder): child "Shot" holding "Pot" (shards "Shard_0..3", "Cork",
    /// "InkSpill", "Tentacle") and particle system "Splash"; child "Slick" (disc, RoguePirates/InkSlick).
    /// "Shot" and "Slick" are detached to world space at runtime so the ship can sail away; extra slicks are
    /// pooled clones and everything is destroyed with this component.
    ///
    /// Hero pose: tick <see cref="heroPose"/> to freeze the burst at <see cref="heroTime"/> above this transform
    /// and slowly turntable it (showcase / look-dev).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InkPotVfx : MonoBehaviour
    {
        public enum Phase { Idle, Flight, Burst }

        [System.Serializable] public sealed class PointEvent : UnityEvent<Vector3> { }
        [System.Serializable] public sealed class TransformEvent : UnityEvent<Transform> { }

        [Header("Rig (filled by the builder)")]
        [SerializeField] Transform launcher;
        [SerializeField] Transform shotRoot;
        [SerializeField] Transform pot;
        [SerializeField] Transform[] shards;
        [SerializeField] Transform cork;
        [SerializeField] Transform inkSpill;
        [SerializeField] MeshFilter tentacleFilter;
        [SerializeField] ParticleSystem splash;
        [SerializeField] Transform slick;
        [Tooltip("Static baked burst pose shown in edit mode so the hero look is visible when the prefab is dropped in a scene. Hidden in Play mode; tagged EditorOnly so it is stripped from builds.")]
        [SerializeField] GameObject editorPreview;

        [Header("Targeting")]
        [SerializeField] bool autoFire = true;
        [SerializeField, Min(0.1f)] float cooldown = 3f;
        [SerializeField, Min(1f)] float range = 15f;
        [SerializeField] string targetTag = "Enemy";
        [Tooltip("If set, always lob at this.")]
        [SerializeField] Transform manualTarget;
        [Tooltip("Keep lobbing at preview points when no enemies are around (testing / showcase).")]
        [SerializeField] bool fireWithoutTargets = true;
        [SerializeField, Min(1f)] float previewDistance = 8f;
        [SerializeField] float waterHeight = 0f;

        [Header("Pot & flight")]
        [Tooltip("World height of the pot.")]
        [SerializeField, Min(0.1f)] float potScale = 0.9f;
        [SerializeField, Min(0.1f)] float flightTime = 0.95f;
        [SerializeField, Min(0f)] float arcHeight = 4.5f;
        [Tooltip("The pot bursts this high above the water (mid-air burst).")]
        [SerializeField, Min(0f)] float burstHeight = 1.1f;
        [SerializeField, Min(0f)] float rockDegrees = 26f;
        [Tooltip("Spin around the pot's own axis while flying, degrees per second.")]
        [SerializeField] float spinSpeed = 240f;

        [Header("Burst timing")]
        [Tooltip("Slow-mo / speed-up for the whole sequence (1 = normal).")]
        [SerializeField, Range(0.05f, 2f)] float playbackSpeed = 1f;
        [SerializeField, Min(0.2f)] float burstDuration = 0.95f;
        [SerializeField, Min(0f)] float corkSpeed = 5f;
        [SerializeField] float corkSpin = 900f;
        [Tooltip("How fast the clay shards fly apart (pot units / s).")]
        [SerializeField, Min(0f)] float shardSpeed = 0.7f;
        [SerializeField, Range(0f, 120f)] float shardOpenDegrees = 38f;
        [SerializeField, Min(0.02f)] float spillGrowTime = 0.16f;
        [Tooltip("Tentacle length in pot units (pot is 1 tall).")]
        [SerializeField, Min(0.1f)] float tentacleLength = 0.95f;
        [SerializeField, Range(0f, 1.5f)] float tentacleCurl = 1f;
        [Tooltip("Ink lands and the slick starts spreading this long after the burst.")]
        [SerializeField, Min(0f)] float slickDelay = 0.3f;

        [Header("Ink pool (forms wherever the pot lands)")]
        [Tooltip("Pool radius in metres. The pool is a circle, so its width is 2 x this.")]
        [FormerlySerializedAs("slickRadius")]
        [SerializeField, Min(0.2f)] float poolRadius = 1.8f;
        [Tooltip("Width of the violet rim band, in metres.")]
        [SerializeField, Min(0.01f)] float rimWidth = 0.12f;
        [Tooltip("Seconds the pool stays at full strength (and keeps damaging) after it has spread.")]
        [FormerlySerializedAs("slickLife")]
        [SerializeField, Min(0f)] float poolDuration = 3.5f;
        [Tooltip("Seconds the pool takes to fade and drain away at the end.")]
        [FormerlySerializedAs("slickFade")]
        [SerializeField, Min(0.05f)] float fadeTime = 0.6f;
        [Tooltip("Seconds the pool takes to spread out to full size when the ink lands.")]
        [SerializeField, Min(0.05f)] float spreadTime = 0.35f;
        [Tooltip("0 = perfect circle, 1 = wobbly splat edge.")]
        [SerializeField, Range(0f, 1f)] float edgeWobble = 0f;

        [Header("Ink pool look")]
        [SerializeField] Color inkColor = new Color(0.012f, 0.006f, 0.02f, 1f);
        [SerializeField] Color rimColor = new Color(0.62f, 0.3f, 1f, 1f);
        [SerializeField] Color sheenColor = new Color(0.22f, 0.08f, 0.38f, 1f);
        [Tooltip("How much violet sheen shows inside the black ink (0 = pure black).")]
        [SerializeField, Range(0f, 1f)] float sheenStrength = 0.2f;
        [SerializeField, Min(0f)] float rimPulseSpeed = 5f;
        [SerializeField, Range(0f, 1f)] float rimPulseAmount = 0.5f;
        [Tooltip("Expanding water rings around the pool (only when it lands on water).")]
        [SerializeField] bool waterRings = true;
        [SerializeField, Range(0f, 1f)] float ringAlpha = 0.6f;
        [Tooltip("Ring waves per second.")]
        [SerializeField, Min(0f)] float ringSpeed = 0.55f;

        [Header("Ink pool placement")]
        [Tooltip("Raycast down from the burst point and lay the pool on whatever it hits (deck, island, rocks). Falls back to Water Height.")]
        [SerializeField] bool snapToSurface = true;
        [SerializeField] LayerMask surfaceMask = ~0;
        [SerializeField, Min(0.5f)] float maxDropDistance = 40f;
        [Tooltip("Tilt the pool to match sloped ground.")]
        [SerializeField] bool alignToSurface = true;
        [SerializeField, Min(0f)] float surfaceLift = 0.03f;

        [Header("Damage")]
        [SerializeField] bool applyDamage = true;
        [SerializeField, Min(0f)] float impactDamage = 20f;
        [SerializeField, Min(0.1f)] float impactRadius = 2f;
        [SerializeField, Min(0f)] float tickDamage = 5f;
        [SerializeField, Min(0.05f)] float tickInterval = 0.5f;

        [Header("Hero pose (showcase)")]
        [SerializeField] bool heroPose;
        [SerializeField, Range(0f, 0.9f)] float heroTime = 0.3f;
        [SerializeField] float heroSpin = 18f;

        [Header("Events")]
        public PointEvent onLaunch = new PointEvent();
        public PointEvent onBurst = new PointEvent();
        public PointEvent onSlickEnd = new PointEvent();
        [Tooltip("Fired for every target hurt by the impact or a slick tick.")]
        public TransformEvent onHit = new TransformEvent();

        public Phase CurrentPhase => _phase;
        public bool IsBusy => _phase != Phase.Idle;
        public bool AutoFire { get => autoFire; set => autoFire = value; }
        public float Cooldown { get => cooldown; set => cooldown = Mathf.Max(0.1f, value); }
        public float PlaybackSpeed { get => playbackSpeed; set => playbackSpeed = Mathf.Clamp(value, 0.05f, 2f); }
        public float PoolRadius { get => poolRadius; set => poolRadius = Mathf.Max(0.2f, value); }
        public float PoolDuration { get => poolDuration; set => poolDuration = Mathf.Max(0f, value); }
        public float RimWidth { get => rimWidth; set => rimWidth = Mathf.Max(0.01f, value); }

        static readonly int SpreadId = Shader.PropertyToID("_Spread");
        static readonly int FadeId = Shader.PropertyToID("_Fade");
        static readonly int RingTimeId = Shader.PropertyToID("_RingTime");
        static readonly int SeedId = Shader.PropertyToID("_Seed");
        static readonly int InkColorId = Shader.PropertyToID("_InkColor");
        static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        static readonly int SheenColorId = Shader.PropertyToID("_SheenColor");
        static readonly int SheenStrengthId = Shader.PropertyToID("_SheenStrength");
        static readonly int RimWidthId = Shader.PropertyToID("_RimWidth");
        static readonly int PulseSpeedId = Shader.PropertyToID("_PulseSpeed");
        static readonly int PulseAmountId = Shader.PropertyToID("_PulseAmount");
        static readonly int WobbleId = Shader.PropertyToID("_Wobble");
        static readonly int RingAlphaId = Shader.PropertyToID("_RingAlpha");
        static readonly int RingSpeedId = Shader.PropertyToID("_RingSpeed");

        // pot-local landmarks (pot mesh is 1 unit tall, origin at the bottom centre)
        static readonly Vector3 PotCenter = new Vector3(0f, 0.45f, 0f);
        static readonly Vector3 CorkSeat = new Vector3(0f, 0.8f, 0f);
        static readonly Vector3 Opening = new Vector3(0f, 0.86f, 0f);
        const float SlickDiscFraction = 0.62f; // matches InkSlick _SlickRadius

        sealed class SlickInstance
        {
            public Transform t;
            public Renderer r;
            public float time, seed, nextTick, radius;
            public bool onWater;
            public Vector3 center;
            public bool active, owned;
        }

        readonly List<SlickInstance> _slicks = new List<SlickInstance>();
        readonly List<Transform> _candidates = new List<Transform>();
        readonly RaycastHit[] _hits = new RaycastHit[16];
        MaterialPropertyBlock _mpb;

        Phase _phase = Phase.Idle;
        float _t, _cooldownLeft = 0.6f, _yaw, _heroYaw;
        Vector3 _p0, _p1, _p2;
        Quaternion _burstRot = Quaternion.identity;
        Vector3 _corkDir, _corkAxis;
        bool _ownsShot, _slickSpawned;
        Transform _target;

        // shards
        Vector3[] _shardDir;
        Quaternion[] _shardBaseRot;
        Vector3[] _shardBasePos;

        // tentacle
        const int TRings = 12, TSides = 7;
        Mesh _tentMesh;
        Vector3[] _tv, _tn;
        Vector2[] _tuv;
        Color[] _tc;
        int[] _tt;
        bool _flipWinding, _windingChecked;
        Transform _tentacle;
        Vector3 _tentBasePos;

        // ---------------------------------------------------------------- setup

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (!launcher) launcher = transform.Find("Launcher");
            if (!shotRoot) shotRoot = transform.Find("Shot");
            if (!slick) slick = transform.Find("Slick");

            if (!editorPreview) { var ep = transform.Find("BurstPreview"); if (ep) editorPreview = ep.gameObject; }
            if (editorPreview) editorPreview.SetActive(false);
            InitRig();

            if (!heroPose && shotRoot && shotRoot.IsChildOf(transform))
            {
                shotRoot.SetParent(null, false);
                shotRoot.name = name + " (Shot FX)";
                _ownsShot = true;
            }
            if (slick)
            {
                if (slick.IsChildOf(transform))
                {
                    slick.SetParent(null, false);
                    slick.name = name + " (Ink Slick)";
                }
                _slicks.Add(new SlickInstance { t = slick, r = slick.GetComponent<Renderer>(), owned = true });
                slick.gameObject.SetActive(false);
            }

            if (pot) pot.localScale = Vector3.one * potScale;
            if (shotRoot) shotRoot.gameObject.SetActive(false);
        }

        bool _rigReady;

        void InitRig()
        {
            if (_rigReady) return;
            _rigReady = true;
            if (shards != null)
            {
                _shardDir = new Vector3[shards.Length];
                _shardBaseRot = new Quaternion[shards.Length];
                _shardBasePos = new Vector3[shards.Length];
                for (int i = 0; i < shards.Length; i++)
                {
                    if (!shards[i]) continue;
                    _shardBaseRot[i] = shards[i].localRotation;
                    _shardBasePos[i] = shards[i].localPosition;
                    var mf = shards[i].GetComponent<MeshFilter>();
                    Vector3 c = mf && mf.sharedMesh ? mf.sharedMesh.bounds.center : Vector3.forward;
                    c.y = 0f;
                    _shardDir[i] = c.sqrMagnitude > 1e-5f ? c.normalized : Vector3.forward;
                }
            }

            if (tentacleFilter)
            {
                _tentacle = tentacleFilter.transform;
                _tentBasePos = _tentacle.localPosition;
                BuildTentacleBuffers();
            }
        }

        /// <summary>
        /// Editor / bake helper: pose the rig at burst time <paramref name="t"/> in place (no detaching, no particles).
        /// Used by InkPotBuilder to bake the static hero preview.
        /// </summary>
        public void PoseBurstPreview(float t, int seed, Quaternion potTilt, float scale, float openDegrees)
        {
            potScale = scale;
            shardOpenDegrees = openDegrees;
            shardSpeed = 0f;
            InitRig();
            ResetPotPose();
            _burstRot = potTilt;
            SeedBurst(seed);
            EvaluateBurst(t, true);
        }

        /// <summary>The generated tentacle mesh (valid after <see cref="PoseBurstPreview"/>).</summary>
        public Mesh PreviewTentacleMesh => _tentMesh;

        void Start()
        {
            if (heroPose) StartHero();
        }

        void OnDisable()
        {
            if (heroPose) return;
            if (shotRoot) shotRoot.gameObject.SetActive(false);
            foreach (var s in _slicks) { s.active = false; if (s.t) s.t.gameObject.SetActive(false); }
            _phase = Phase.Idle;
        }

        void OnDestroy()
        {
            if (_ownsShot && shotRoot) Destroy(shotRoot.gameObject);
            foreach (var s in _slicks) if (s.t) Destroy(s.t.gameObject);
            if (_tentMesh) Destroy(_tentMesh);
        }

        // ---------------------------------------------------------------- public API

        /// <summary>Lob a pot at a world point now (ignores cooldown, refused while a pot is in the air).</summary>
        public bool LaunchAt(Vector3 point)
        {
            if (IsBusy || !shotRoot || !pot || heroPose) return false;
            Vector3 from = launcher ? launcher.position : transform.position + Vector3.up * 1.5f;
            _p0 = from;
            _p2 = new Vector3(point.x, waterHeight + burstHeight, point.z);
            _p1 = (_p0 + _p2) * 0.5f + Vector3.up * arcHeight;
            _t = 0f;
            _yaw = Random.Range(0f, 360f);
            shotRoot.SetPositionAndRotation(_p0, Quaternion.identity);
            ResetPotPose();
            shotRoot.gameObject.SetActive(true);
            _phase = Phase.Flight;
            onLaunch.Invoke(_p0);
            return true;
        }

        public void Fire()
        {
            if (TryPickTarget(out var p)) LaunchAt(p);
            else _cooldownLeft = 0.5f;
        }

        // ---------------------------------------------------------------- loop

        void Update()
        {
            float dt = Time.deltaTime * playbackSpeed;
            if (dt <= 0f) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            if (heroPose) { StepHero(dt); return; }

            StepSlicks(dt);

            switch (_phase)
            {
                case Phase.Idle:
                    if (!autoFire) return;
                    _cooldownLeft -= Time.deltaTime;
                    if (_cooldownLeft <= 0f) Fire();
                    return;

                case Phase.Flight:
                    _t += dt;
                    StepFlight();
                    if (_t >= flightTime) BeginBurst();
                    return;

                case Phase.Burst:
                    _t += dt;
                    EvaluateBurst(_t, false);
                    if (!_slickSpawned && _t >= slickDelay)
                    {
                        _slickSpawned = true;
                        SpawnSlick(shotRoot.position);
                    }
                    if (_t >= burstDuration)
                    {
                        if (!_slickSpawned)
                        {
                            _slickSpawned = true;
                            SpawnSlick(shotRoot.position);
                        }
                        shotRoot.gameObject.SetActive(false);
                        _phase = Phase.Idle;
                        _cooldownLeft = Mathf.Max(0.1f, cooldown - (flightTime + burstDuration) / playbackSpeed);
                    }
                    return;
            }
        }

        // ---------------------------------------------------------------- flight

        void StepFlight()
        {
            float k = Mathf.Clamp01(_t / flightTime);
            float u = 1f - k;
            Vector3 pos = u * u * _p0 + 2f * u * k * _p1 + k * k * _p2;
            shotRoot.SetPositionAndRotation(pos, Quaternion.identity);

            _yaw += spinSpeed * Time.deltaTime * playbackSpeed;
            float rock = Mathf.Sin(_t * 7.5f) * rockDegrees;
            // settle upright-ish toward the burst so the opening faces up when it pops
            rock *= Mathf.Lerp(1f, 0.55f, k * k);
            var rot = Quaternion.AngleAxis(_yaw, Vector3.up) * Quaternion.AngleAxis(rock, Vector3.forward);
            pot.localRotation = rot;
            pot.localPosition = -(rot * PotCenter) * potScale;
        }

        void BeginBurst()
        {
            _phase = Phase.Burst;
            _t = 0f;
            _slickSpawned = false;
            _burstRot = pot.localRotation;
            SeedBurst(Random.Range(0, 100000));
            if (splash)
            {
                var m = splash.main;
                m.simulationSpace = ParticleSystemSimulationSpace.World;
                splash.useAutoRandomSeed = true;
                splash.transform.position = shotRoot.TransformPoint(OpeningLocal());
                splash.transform.rotation = shotRoot.rotation * _burstRot;
                splash.Play(true);
            }
            EvaluateBurst(0f, false);
            onBurst.Invoke(shotRoot.position);
        }

        void SeedBurst(int seed)
        {
            var rnd = new System.Random(seed);
            float ang = (float)(rnd.NextDouble() * Mathf.PI * 2.0);
            Vector3 lateral = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 0.35f;
            _corkDir = (_burstRot * Vector3.up + lateral).normalized;
            _corkAxis = Vector3.Cross(_corkDir, Vector3.up + lateral * 0.5f);
            if (_corkAxis.sqrMagnitude < 1e-4f) _corkAxis = Vector3.right;
            _corkAxis.Normalize();
        }

        Vector3 OpeningLocal() => pot.localPosition + pot.localRotation * Opening * potScale;

        void ResetPotPose()
        {
            pot.gameObject.SetActive(true);
            pot.localScale = Vector3.one * potScale;
            if (shards != null)
                for (int i = 0; i < shards.Length; i++)
                {
                    if (!shards[i]) continue;
                    shards[i].localPosition = _shardBasePos[i];
                    shards[i].localRotation = _shardBaseRot[i];
                    shards[i].localScale = Vector3.one;
                }
            if (cork)
            {
                cork.localPosition = CorkSeat;
                cork.localRotation = Quaternion.identity;
                cork.localScale = Vector3.one;
                cork.gameObject.SetActive(true);
            }
            if (inkSpill) inkSpill.gameObject.SetActive(false);
            if (_tentacle) _tentacle.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- burst (deterministic for a given t)

        void EvaluateBurst(float t, bool hero)
        {
            float d = burstDuration;
            float g = 9.81f;

            // pot body: hangs for a beat, then drops toward the water
            float fall = hero ? 0f : 0.5f * g * 0.45f * Mathf.Max(0f, t - 0.08f) * Mathf.Max(0f, t - 0.08f);
            pot.localRotation = _burstRot;
            pot.localPosition = -(_burstRot * PotCenter) * potScale + Vector3.down * fall;

            // clay shards open like petals and drift apart, then shrink away
            if (shards != null)
            {
                float open = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.32f), 3f);
                float shrink = 1f - Smooth01((t - d * 0.6f) / (d * 0.4f));
                for (int i = 0; i < shards.Length; i++)
                {
                    var s = shards[i];
                    if (!s) continue;
                    Vector3 dir = _shardDir[i];
                    Vector3 axis = Vector3.Cross(Vector3.up, dir);
                    s.localRotation = Quaternion.AngleAxis(shardOpenDegrees * open * (0.85f + 0.15f * i / Mathf.Max(1, shards.Length - 1)), axis) * _shardBaseRot[i];
                    float sf = hero ? 0f : 0.5f * 3.5f * t * t;
                    s.localPosition = _shardBasePos[i] + dir * (shardSpeed * t * (0.6f + 0.4f * open) + 0.06f * open) + Vector3.down * sf;
                    s.localScale = Vector3.one * Mathf.Max(0.001f, shrink);
                }
            }

            // cork pops out of the neck with spin and gravity
            if (cork)
            {
                float ct = Mathf.Max(0f, t);
                Vector3 seat = (_burstRot * (CorkSeat - PotCenter)) * potScale; // where the cork sat at burst time
                Vector3 local = seat
                                + _corkDir * (corkSpeed * ct * potScale) + Vector3.down * (0.5f * g * 0.6f * ct * ct);
                cork.position = shotRoot.TransformPoint(local);
                cork.rotation = shotRoot.rotation * Quaternion.AngleAxis(corkSpin * ct, _corkAxis) * _burstRot;
                float cs = 1f - Smooth01((t - d * 0.7f) / (d * 0.3f));
                cork.localScale = Vector3.one * Mathf.Max(0.001f, cs);
                cork.gameObject.SetActive(cs > 0.002f);
            }

            // glossy ink gushes out of the opening, then droops and drains away
            if (inkSpill)
            {
                float grow = BackOut(Mathf.Clamp01(t / spillGrowTime));
                float drain = 1f - Smooth01((t - d * 0.35f) / (d * 0.5f));
                float wob = 1f + Mathf.Sin(t * 26f) * 0.05f * (1f - Mathf.Clamp01(t / (d * 0.5f)));
                float sxz = Mathf.Max(0.001f, grow * Mathf.Lerp(1.1f, 1f, drain) * wob);
                float sy = Mathf.Max(0.001f, grow * drain / wob);
                inkSpill.localPosition = Opening + Vector3.down * 0.06f;
                inkSpill.localRotation = Quaternion.identity;
                inkSpill.localScale = new Vector3(sxz, sy, sxz);
                inkSpill.gameObject.SetActive(grow * drain > 0.003f);
            }

            // cartoon tentacle curls out, waves, then slips back down
            if (_tentacle)
            {
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01((t - 0.02f) / 0.2f), 3f);
                float back = 1f - Smooth01((t - d * 0.62f) / (d * 0.33f));
                float ext = e * back;
                float curl = tentacleCurl * Mathf.Lerp(0.15f, 1f, Smooth01((t - 0.04f) / 0.3f));
                float wave = Mathf.Sin(t * 11f) * 0.35f * e;
                _tentacle.gameObject.SetActive(ext > 0.01f);
                if (ext > 0.01f) RebuildTentacle(ext, curl, wave);
            }
        }

        // ---------------------------------------------------------------- hero pose

        void StartHero()
        {
            if (!shotRoot || !pot) { heroPose = false; return; }
            autoFire = false;
            shotRoot.gameObject.SetActive(true);
            shotRoot.localPosition = Vector3.zero;
            shotRoot.localRotation = Quaternion.identity;
            ResetPotPose();
            _burstRot = Quaternion.AngleAxis(-18f, Vector3.forward) * Quaternion.AngleAxis(12f, Vector3.right);
            SeedBurst(1337);
            EvaluateBurst(heroTime, true);
            if (splash)
            {
                var m = splash.main;
                m.simulationSpace = ParticleSystemSimulationSpace.Local;
                splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                splash.useAutoRandomSeed = false;
                splash.randomSeed = 7u;
                splash.transform.localPosition = OpeningLocal();
                splash.transform.localRotation = _burstRot;
                splash.Simulate(Mathf.Max(0.01f, heroTime), true, true);
                splash.Pause(true);
            }
        }

        void StepHero(float dt)
        {
            if (!shotRoot) return;
            _heroYaw += heroSpin * dt;
            shotRoot.localRotation = Quaternion.AngleAxis(_heroYaw, Vector3.up);
            // keep the tentacle gently alive in the frozen pose
            float t = heroTime;
            if (_tentacle && _tentacle.gameObject.activeSelf)
            {
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01((t - 0.02f) / 0.2f), 3f);
                float curl = tentacleCurl * Mathf.Lerp(0.15f, 1f, Smooth01((t - 0.04f) / 0.3f));
                RebuildTentacle(e, curl, Mathf.Sin(Time.time * 2.2f) * 0.25f);
            }
        }

        // ---------------------------------------------------------------- slick

        /// <summary>Where the ink lands below <paramref name="burstPoint"/>: first solid surface (not an enemy, not this rig), else the water.</summary>
        Vector3 FindPoolPoint(Vector3 burstPoint, out Vector3 normal, out bool onWater)
        {
            normal = Vector3.up;
            onWater = true;
            Vector3 water = new Vector3(burstPoint.x, waterHeight, burstPoint.z);
            if (!snapToSurface) return water;

            Vector3 origin = burstPoint + Vector3.up * 0.25f;
            int n = Physics.RaycastNonAlloc(origin, Vector3.down, _hits, maxDropDistance, surfaceMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            Vector3 point = water;
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (h.distance >= best) continue;
                var col = h.collider;
                if (!col || col.transform.IsChildOf(transform)) continue;
                if (!string.IsNullOrEmpty(targetTag) && col.CompareTag(targetTag)) continue;
                if (col.GetComponentInParent<IDamageable>() != null) continue;
                best = h.distance;
                point = h.point;
                normal = h.normal;
                found = true;
            }
            // anything below the sea surface (seabed, submerged hull) -> the pool floats on the water
            if (!found || point.y < waterHeight) { normal = Vector3.up; return water; }
            onWater = false;
            if (!alignToSurface || Vector3.Dot(normal, Vector3.up) < 0.5f) normal = Vector3.up;
            return point;
        }

        void SpawnSlick(Vector3 burstPoint)
        {
            SlickInstance s = null;
            foreach (var x in _slicks) if (!x.active && x.t) { s = x; break; }
            if (s == null && _slicks.Count > 0 && _slicks[0].t)
            {
                var clone = Instantiate(_slicks[0].t.gameObject);
                clone.name = name + " (Ink Pool)";
                s = new SlickInstance { t = clone.transform, r = clone.GetComponent<Renderer>() };
                _slicks.Add(s);
            }
            if (s == null) return;

            Vector3 center = FindPoolPoint(burstPoint, out var normal, out var onWater);
            s.onWater = onWater;
            s.active = true;
            s.time = 0f;
            s.seed = Random.Range(0f, 50f);
            s.center = center;
            s.nextTick = tickInterval;
            s.radius = poolRadius;
            float discScale = poolRadius / SlickDiscFraction;
            var rot = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up);
            s.t.SetPositionAndRotation(center + normal * surfaceLift, rot);
            s.t.localScale = new Vector3(discScale, 1f, discScale);
            s.t.gameObject.SetActive(true);
            ApplySlick(s, 0f, 1f);

            RefreshCandidates();
            if (impactDamage > 0f) HurtWithin(center, impactRadius, impactDamage);
        }

        void StepSlicks(float dt)
        {
            float life = spreadTime + poolDuration;
            foreach (var s in _slicks)
            {
                if (!s.active) continue;
                s.time += dt;
                float spread = BackOut(Mathf.Clamp01(s.time / spreadTime));
                float fade = 1f - Mathf.Clamp01((s.time - life) / fadeTime);
                // shrink slightly while fading so it drains instead of just vanishing
                float drain = Mathf.Lerp(0.85f, 1f, fade);
                ApplySlick(s, spread * drain, fade);

                if (s.time <= life && tickDamage > 0f)
                {
                    while (s.time >= s.nextTick)
                    {
                        RefreshCandidates();
                        HurtWithin(s.center, s.radius, tickDamage);
                        s.nextTick += tickInterval;
                    }
                }

                if (s.time >= life + fadeTime)
                {
                    s.active = false;
                    s.t.gameObject.SetActive(false);
                    onSlickEnd.Invoke(s.center);
                }
            }
        }

        void ApplySlick(SlickInstance s, float spread, float fade)
        {
            if (!s.r) return;
            float discScale = Mathf.Max(0.01f, s.radius / SlickDiscFraction);
            s.r.GetPropertyBlock(_mpb);
            _mpb.SetFloat(SpreadId, spread);
            _mpb.SetFloat(FadeId, fade);
            _mpb.SetFloat(RingTimeId, s.time);
            _mpb.SetFloat(SeedId, s.seed);
            _mpb.SetColor(InkColorId, inkColor);
            _mpb.SetColor(RimColorId, rimColor);
            _mpb.SetColor(SheenColorId, sheenColor);
            _mpb.SetFloat(SheenStrengthId, sheenStrength);
            _mpb.SetFloat(RimWidthId, Mathf.Clamp(rimWidth / discScale, 0.002f, 0.3f));
            _mpb.SetFloat(PulseSpeedId, rimPulseSpeed);
            _mpb.SetFloat(PulseAmountId, rimPulseAmount);
            _mpb.SetFloat(WobbleId, edgeWobble);
            _mpb.SetFloat(RingAlphaId, waterRings && s.onWater ? ringAlpha : 0f);
            _mpb.SetFloat(RingSpeedId, ringSpeed);
            s.r.SetPropertyBlock(_mpb);
        }

        // ---------------------------------------------------------------- damage / targeting

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
            _candidates.Clear();
            if (string.IsNullOrEmpty(targetTag)) return;
            GameObject[] found;
            try { found = GameObject.FindGameObjectsWithTag(targetTag); }
            catch (UnityException) { return; }
            foreach (var g in found)
                if (!g.transform.IsChildOf(transform)) _candidates.Add(g.transform);
        }

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
            float best = range * range;
            _target = null;
            foreach (var c in _candidates)
            {
                if (!c.gameObject.activeInHierarchy) continue;
                Vector3 d = c.position - origin;
                d.y = 0f;
                float d2 = d.sqrMagnitude;
                if (d2 <= best) { best = d2; _target = c; }
            }
            if (_target) { point = _target.position; return true; }

            if (!fireWithoutTargets) return false;
            float ang = Mathf.Sin(Time.time * 0.37f) * 60f;
            Vector3 fwd = transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            point = origin + Quaternion.AngleAxis(ang, Vector3.up) * fwd.normalized * previewDistance;
            return true;
        }

        // ---------------------------------------------------------------- tentacle mesh

        void BuildTentacleBuffers()
        {
            int cols = TSides + 1;
            int vc = TRings * cols + 1;
            _tv = new Vector3[vc];
            _tn = new Vector3[vc];
            _tuv = new Vector2[vc];
            _tc = new Color[vc];
            for (int i = 0; i < vc; i++) _tc[i] = Color.white;
            _tt = new int[(TRings - 1) * TSides * 6 + TSides * 3];
            int k = 0;
            for (int r = 0; r < TRings - 1; r++)
            for (int j = 0; j < TSides; j++)
            {
                int a = r * cols + j, b = a + 1, c = a + cols, d = c + 1;
                _tt[k++] = a; _tt[k++] = c; _tt[k++] = b;
                _tt[k++] = b; _tt[k++] = c; _tt[k++] = d;
            }
            int tip = vc - 1, last = (TRings - 1) * cols;
            for (int j = 0; j < TSides; j++)
            {
                _tt[k++] = last + j; _tt[k++] = tip; _tt[k++] = last + j + 1;
            }
            _tentMesh = new Mesh { name = "InkPot_Tentacle (runtime)" };
            _tentMesh.MarkDynamic();
            tentacleFilter.sharedMesh = _tentMesh;
        }

        void RebuildTentacle(float extend, float curl, float wave)
        {
            if (_tentMesh == null) return;
            int cols = TSides + 1;
            float L = tentacleLength * Mathf.Max(0.02f, extend);
            float ds = L / (TRings - 1);
            float r0 = 0.14f * Mathf.Lerp(0.55f, 1f, extend);
            float phi = 0f;
            Vector3 p = Vector3.zero;
            Vector3 tipDir = Vector3.up;
            for (int i = 0; i < TRings; i++)
            {
                float s = i / (float)(TRings - 1);
                if (i > 0)
                {
                    float kappa = curl * (1.6f + 9f * Mathf.Pow(s, 1.6f)) / Mathf.Max(0.3f, tentacleLength) * 0.95f
                                  + wave * Mathf.Sin(s * 5f) * 2f;
                    phi += kappa * ds;
                    p += new Vector3(Mathf.Sin(phi), Mathf.Cos(phi), 0f) * ds;
                }
                var T = new Vector3(Mathf.Sin(phi), Mathf.Cos(phi), 0f);
                var N = new Vector3(Mathf.Cos(phi), -Mathf.Sin(phi), 0f); // inner side of the curl (suckers)
                var B = Vector3.forward;
                tipDir = T;
                float rad = r0 * (1f - 0.8f * s) * (1f + 0.3f * Mathf.Pow(1f - s, 4f));
                for (int j = 0; j < cols; j++)
                {
                    float u = j / (float)TSides;
                    float a = u * Mathf.PI * 2f;
                    Vector3 off = N * Mathf.Cos(a) + B * Mathf.Sin(a);
                    int idx = i * cols + j;
                    _tv[idx] = p + off * rad;
                    _tn[idx] = off;
                    _tuv[idx] = new Vector2(u, s);
                }
            }
            int tip = _tv.Length - 1;
            _tv[tip] = p + tipDir * (r0 * 0.25f);
            _tn[tip] = tipDir;
            _tuv[tip] = new Vector2(0.5f, 1f);

            if (!_windingChecked)
            {
                // make sure triangles face outward (normals point away from the tube axis)
                Vector3 a0 = _tv[_tt[0]], b0 = _tv[_tt[1]], c0 = _tv[_tt[2]];
                Vector3 fn = Vector3.Cross(b0 - a0, c0 - a0);
                _flipWinding = Vector3.Dot(fn, _tn[_tt[0]] + _tn[_tt[1]] + _tn[_tt[2]]) < 0f;
                if (_flipWinding)
                    for (int i = 0; i < _tt.Length; i += 3) { int x = _tt[i + 1]; _tt[i + 1] = _tt[i + 2]; _tt[i + 2] = x; }
                _windingChecked = true;
            }

            _tentMesh.Clear(true);
            _tentMesh.vertices = _tv;
            _tentMesh.normals = _tn;
            _tentMesh.uv = _tuv;
            _tentMesh.colors = _tc;
            _tentMesh.triangles = _tt;
            _tentMesh.RecalculateBounds();
        }

        // ---------------------------------------------------------------- utils

        static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        static float BackOut(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float t = x - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.3f, 1f, 0.6f);
            Vector3 c = transform.position;
            Vector3 prev = c + new Vector3(range, 0f, 0f);
            for (int i = 1; i <= 48; i++)
            {
                float a = i / 48f * Mathf.PI * 2f;
                Vector3 q = c + new Vector3(Mathf.Cos(a) * range, 0f, Mathf.Sin(a) * range);
                Gizmos.DrawLine(prev, q);
                prev = q;
            }
        }
#endif
    }
}
