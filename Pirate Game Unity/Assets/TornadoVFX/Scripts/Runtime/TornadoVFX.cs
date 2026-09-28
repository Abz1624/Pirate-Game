using System;
using UnityEngine;

namespace TornadoFX
{
    /// <summary>
    /// Runtime controller for the stylized tornado (Shuriken, URP, mobile).
    /// Drives 7 lightweight particle layers, the staged start/stop animation, and exposes
    /// intensity / scale / colour / rotation controls. Pool-friendly: no Instantiate/Destroy,
    /// no per-frame allocations, shared materials (per-instance values go through MaterialPropertyBlocks).
    /// </summary>
    [DisallowMultipleComponent]
    public class TornadoVFX : MonoBehaviour
    {
        public enum State { Idle, Starting, Active, Stopping, Draining }

        [Header("Layers")]
        public ParticleSystem core;
        public ParticleSystem swirls;
        public ParticleSystem outerRibbons;
        public ParticleSystem sparks;
        public ParticleSystem debris;
        public ParticleSystem groundVortex;
        public ParticleSystem glow;

        [Header("Shape (meters, before Scale)")]
        [Min(1f)] public float tornadoHeight = 7f;
        [Min(0.2f)] public float tornadoBaseRadius = 1.75f;
        [Min(0.1f)] public float waistRadius = 0.85f;
        [Min(0.2f)] public float tornadoTopRadius = 3.5f;
        [Range(0f, 1f)] public float lean = 0.15f;

        [Header("Motion")]
        [Tooltip("Main rotation in revolutions per second (negative = counter-clockwise).")]
        [Range(-4f, 4f)] public float rotationSpeed = 2f;
        [Tooltip("Multiplier on upward particle speed and texture scroll.")]
        [Range(0f, 3f)] public float verticalSpeed = 1f;
        [Tooltip("Multiplier on every layer's Noise strength.")]
        [Range(0f, 3f)] public float turbulence = 1f;

        [Header("Look")]
        [Tooltip("0 = invisible, 0.5 = medium, 1 = normal, 1.5 = extreme.")]
        [Range(0f, 1.5f)] public float tornadoIntensity = 1f;
        [Tooltip("Global multiplier on all emission rates.")]
        [Range(0f, 2f)] public float emissionRate = 1f;
        [ColorUsage(false, false)] public Color tornadoColor = new Color(1f, 0.42f, 0.08f);
        [Range(0f, 3f)] public float glowIntensity = 1f;
        [Range(0f, 2f)] public float sparkAmount = 1f;
        [Range(0f, 2f)] public float debrisAmount = 1f;

        [Header("Timing")]
        [Tooltip("Multiplier on particle lifetimes.")]
        [Range(0.3f, 2f)] public float lifetime = 1f;
        [Min(0.01f)] public float scale = 1f;
        [Tooltip("Seconds at full strength before auto-stopping. 0 = play until StopTornado().")]
        [Min(0f)] public float duration = 0f;
        [Min(0.1f)] public float startupTime = 1.6f;
        [Min(0.1f)] public float shutdownTime = 1.2f;
        public bool playOnEnable = false;

        /// <summary>Raised once every particle has died after a stop. Pools listen to this.</summary>
        public event Action<TornadoVFX> Finished;

        public State CurrentState => state;
        public bool IsPlaying => state != State.Idle;

        // ------------------------------------------------------------------------------------
        enum Spin { None, RotationY, Orbital, Rotation2D }

        class Layer
        {
            public ParticleSystem ps;
            public ParticleSystemRenderer r;
            public Transform tr;
            public float baseRate;
            public int baseMax;
            public ParticleSystem.MinMaxCurve baseLife;
            public float baseNoise;
            public Vector2 baseVelY;
            public Vector4 baseScroll;
            public float bodyAlpha, bodyGain, bodyWhiten, hlGain;
            public Spin spin;
            public float spinRatio, spinJitter;
            public bool dimWithWeight;   // fade existing particles' brightness with the stage weight
            public bool isGlow;
            public float amount = 1f;    // spark/debris amount hook
            public float weight;         // 0..1 stage weight (start/stop sequencing)
        }

        Layer lCore, lSwirls, lRibbons, lSparks, lDebris, lGround, lGlow;
        Layer[] layers;
        MaterialPropertyBlock mpb;
        bool initialized, dirty = true, stageDirty;
        State state = State.Idle;
        float stateTime, activeTime;
        float coreHeightScale = 1f, coreWidthScale = 1f;

        static readonly int ID_Body = Shader.PropertyToID("_BodyColor");
        static readonly int ID_Highlight = Shader.PropertyToID("_HighlightColor");
        static readonly int ID_Intensity = Shader.PropertyToID("_Intensity");
        static readonly int ID_Scroll = Shader.PropertyToID("_Scroll");

        // ============================ PUBLIC API ============================================

        public void PlayTornado()
        {
            Init();
            ApplyAll();
            foreach (var l in layers)
            {
                l.weight = 0f;
                l.ps.Clear(true);
                var em = l.ps.emission; em.enabled = true;
                l.ps.Play(false);
            }
            SetState(State.Starting);
            UpdateStage(0f);
        }

        public void StopTornado()
        {
            if (state == State.Idle || state == State.Stopping || state == State.Draining) return;
            SetState(State.Stopping);
        }

        /// <summary>Hard stop: clears all particles instantly (used when returning to a pool).</summary>
        public void StopImmediate()
        {
            if (!initialized) return;
            foreach (var l in layers) l.ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            SetState(State.Idle);
        }

        public void SetIntensity(float value) { tornadoIntensity = Mathf.Clamp(value, 0f, 1.5f); dirty = true; }
        public void SetScale(float value) { scale = Mathf.Max(0.01f, value); transform.localScale = Vector3.one * scale; }
        public void SetColor(Color color) { tornadoColor = color; dirty = true; }
        public void SetRotationSpeed(float value) { rotationSpeed = value; dirty = true; }

        /// <summary>Change dimensions at runtime. Meshes are cached per shape.</summary>
        public void SetShape(float height, float baseRadius, float topRadius)
        {
            tornadoHeight = height; tornadoBaseRadius = baseRadius; tornadoTopRadius = topRadius;
            if (initialized) ApplyShape();
        }

        // ============================ LIFECYCLE =============================================

        void Awake() => Init();

        void OnEnable()
        {
            if (playOnEnable) PlayTornado();
        }

        void OnDisable()
        {
            if (initialized && state != State.Idle) StopImmediate();
        }

        void OnValidate()
        {
            dirty = true;
            if (Application.isPlaying && initialized)
            {
                transform.localScale = Vector3.one * scale;
                ApplyShape();
            }
        }

        void Update()
        {
            if (state == State.Idle) return;
            stateTime += Time.deltaTime;

            switch (state)
            {
                case State.Starting:
                    if (stateTime >= startupTime) SetState(State.Active);
                    break;
                case State.Active:
                    activeTime += Time.deltaTime;
                    if (duration > 0f && activeTime >= duration) SetState(State.Stopping);
                    break;
                case State.Stopping:
                    if (stateTime >= shutdownTime)
                    {
                        foreach (var l in layers) l.ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                        SetState(State.Draining);
                    }
                    break;
                case State.Draining:
                    bool alive = false;
                    foreach (var l in layers) alive |= l.ps.IsAlive(false);
                    if (!alive)
                    {
                        StopImmediate();
                        Finished?.Invoke(this);
                        return;
                    }
                    break;
            }

            UpdateStage(stateTime);
        }

        // ============================ INTERNALS =============================================

        void SetState(State s) { state = s; stateTime = 0f; stageDirty = true; if (s == State.Starting) activeTime = 0f; }

        static float S(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        /// <summary>Staged sequencing. Start: ground -> dust -> core grows -> swirls -> sparks.
        /// Stop: swirls/ribbons vanish, sparks thin out, core thins, ground fades last.</summary>
        void UpdateStage(float time)
        {
            switch (state)
            {
                case State.Starting:
                {
                    float t = time / startupTime;
                    lGround.weight  = S(t / 0.25f);
                    lDebris.weight  = S((t - 0.10f) / 0.30f);
                    lCore.weight    = S((t - 0.20f) / 0.40f);
                    lGlow.weight    = S((t - 0.30f) / 0.50f);
                    lSwirls.weight  = S((t - 0.45f) / 0.35f);
                    lRibbons.weight = S((t - 0.55f) / 0.30f);
                    lSparks.weight  = S((t - 0.60f) / 0.40f);
                    coreHeightScale = Mathf.Lerp(0.08f, 1f, S((t - 0.2f) / 0.6f));
                    coreWidthScale  = Mathf.Lerp(0.6f, 1f, S((t - 0.2f) / 0.6f));
                    break;
                }
                case State.Active:
                    foreach (var l in layers) l.weight = 1f;
                    coreHeightScale = coreWidthScale = 1f;
                    break;
                case State.Stopping:
                {
                    float t = time / shutdownTime;
                    lSwirls.weight  = 1f - S(t / 0.35f);
                    lRibbons.weight = 1f - S(t / 0.40f);
                    lSparks.weight  = 1f - S(t / 0.60f);
                    lDebris.weight  = 1f - S(t / 0.50f);
                    lCore.weight    = 1f - S((t - 0.20f) / 0.50f);
                    lGlow.weight    = 1f - S((t - 0.20f) / 0.60f);
                    lGround.weight  = 1f - S((t - 0.40f) / 0.60f);
                    coreWidthScale  = Mathf.Lerp(1f, 0.35f, S(t / 0.7f));
                    break;
                }
                case State.Draining:
                    foreach (var l in layers) l.weight = 0f;
                    break;
            }

            // Core grows upward / thins out by scaling its (Local-space, Hierarchy-scaled) transform.
            lCore.tr.localScale = new Vector3(coreWidthScale, coreHeightScale, coreWidthScale);
            lSwirls.tr.localScale = new Vector3(coreWidthScale, Mathf.Max(0.3f, coreHeightScale), coreWidthScale);

            // While fully active nothing changes per frame, so skip the work unless something was edited.
            if (dirty) ApplyAll();
            else if (state != State.Active || stageDirty) { ApplyEmission(); ApplyRenderValues(); }
            stageDirty = false;
        }

        void Init()
        {
            if (initialized) return;
            if (!core || !swirls || !outerRibbons || !sparks || !debris || !groundVortex || !glow)
            {
                Debug.LogError($"[TornadoVFX] Missing layer references on {name}.", this);
                enabled = false;
                return;
            }
            mpb = new MaterialPropertyBlock();

            //                     ps            spin              ratio  jitter dim    whiten gain   hlGain
            lCore    = Make(core,         Spin.RotationY,  0.60f, 0.15f, false, 0f,   1f,    1f);
            lSwirls  = Make(swirls,       Spin.RotationY,  1.00f, 0.12f, true,  0f,   1f,    1f);
            lRibbons = Make(outerRibbons, Spin.Orbital,    1.25f, 0.20f, true,  0.1f, 1f,    1f);
            lSparks  = Make(sparks,       Spin.Orbital,    1.10f, 0.30f, false, 0.55f,1f,    1f);
            lDebris  = Make(debris,       Spin.Orbital,    0.70f, 0.25f, false, 0f,   0.35f, 0f);
            lGround  = Make(groundVortex, Spin.Rotation2D,-0.25f, 0.10f, true,  0f,   1f,    1f);
            lGlow    = Make(glow,         Spin.None,       0f,    0f,    true,  0f,   1f,    1f);
            lGlow.isGlow = lGround.isGlow = true;
            layers = new[] { lCore, lSwirls, lRibbons, lSparks, lDebris, lGround, lGlow };

            initialized = true;
            transform.localScale = Vector3.one * scale;
            ApplyShape();
        }

        Layer Make(ParticleSystem ps, Spin spin, float ratio, float jitter, bool dim, float whiten, float gain, float hlGain)
        {
            var l = new Layer { ps = ps, tr = ps.transform, r = ps.GetComponent<ParticleSystemRenderer>(),
                                spin = spin, spinRatio = ratio, spinJitter = jitter, dimWithWeight = dim, bodyWhiten = whiten };
            var main = ps.main;
            l.baseRate = ps.emission.rateOverTimeMultiplier;
            l.baseMax = main.maxParticles;
            l.baseLife = main.startLifetime;
            l.baseNoise = ps.noise.strengthMultiplier;
            var vy = ps.velocityOverLifetime.y;
            l.baseVelY = new Vector2(vy.constantMin, vy.constantMax);

            Material mat = l.r.renderMode == ParticleSystemRenderMode.None ? l.r.trailMaterial : l.r.sharedMaterial;
            if (mat != null)
            {
                l.baseScroll = mat.GetVector(ID_Scroll);
                Color body = mat.GetColor(ID_Body);
                l.bodyAlpha = body.a;
                l.bodyGain = body.maxColorComponent * gain;
                l.hlGain = mat.GetColor(ID_Highlight).maxColorComponent * hlGain;
            }
            return l;
        }

        void ApplyShape()
        {
            var shape = new TornadoMeshBuilder.Shape(tornadoHeight, tornadoBaseRadius,
                Mathf.Min(waistRadius, tornadoBaseRadius, tornadoTopRadius), tornadoTopRadius, lean);
            var set = TornadoMeshBuilder.GetOrBuild(shape);

            lCore.r.mesh = set.funnel;
            lSwirls.r.SetMeshes(set.swirls);

            var sh = outerRibbons.shape; sh.mesh = set.funnel;
            sh = sparks.shape; sh.mesh = set.funnel;

            sh = debris.shape; sh.radius = tornadoBaseRadius * 1.5f;

            var gm = groundVortex.main;
            float d = tornadoBaseRadius * 2.5f * 2f; // ground vortex ~2.5x the base radius
            gm.startSize = new ParticleSystem.MinMaxCurve(d * 0.9f, d * 1.05f);

            sh = glow.shape;
            sh.position = new Vector3(0f, tornadoHeight * 0.5f, 0f);
            sh.scale = new Vector3(0.6f, tornadoHeight * 0.75f, 0.6f);
            var glm = glow.main;
            float g = (tornadoBaseRadius + tornadoTopRadius) * 0.9f;
            glm.startSize = new ParticleSystem.MinMaxCurve(g, g * 1.4f);
        }

        void ApplyAll()
        {
            dirty = false;
            lSparks.amount = sparkAmount;
            lDebris.amount = debrisAmount;
            float i = tornadoIntensity;
            float turb = turbulence * (1f + Mathf.Max(0f, i - 1f)); // extreme = more unstable
            float omega = rotationSpeed * Mathf.PI * 2f;               // rad/s

            foreach (var l in layers)
            {
                var main = l.ps.main;
                main.maxParticles = Mathf.CeilToInt(l.baseMax * Mathf.Max(1f, i * emissionRate * l.amount));
                main.startLifetime = ScaleCurve(l.baseLife, lifetime);

                var noise = l.ps.noise;
                noise.strengthMultiplier = l.baseNoise * turb;

                var vel = l.ps.velocityOverLifetime;
                if (vel.enabled)
                    vel.y = new ParticleSystem.MinMaxCurve(l.baseVelY.x * verticalSpeed, l.baseVelY.y * verticalSpeed);

                float w = omega * l.spinRatio;
                var spin = new ParticleSystem.MinMaxCurve(w * (1f - l.spinJitter), w * (1f + l.spinJitter));
                switch (l.spin)
                {
                    case Spin.RotationY: { var rol = l.ps.rotationOverLifetime; rol.y = spin; break; }
                    case Spin.Rotation2D: { var rol = l.ps.rotationOverLifetime; rol.z = spin; break; }
                    case Spin.Orbital: vel.orbitalY = spin; break;
                }
            }
            ApplyEmission();
            ApplyRenderValues();
        }

        static ParticleSystem.MinMaxCurve ScaleCurve(ParticleSystem.MinMaxCurve c, float k)
        {
            if (c.mode == ParticleSystemCurveMode.TwoConstants)
                return new ParticleSystem.MinMaxCurve(c.constantMin * k, c.constantMax * k);
            if (c.mode == ParticleSystemCurveMode.Constant)
                return new ParticleSystem.MinMaxCurve(c.constant * k);
            c.curveMultiplier *= k;
            return c;
        }

        void ApplyEmission()
        {
            float i = tornadoIntensity;
            foreach (var l in layers)
            {
                var em = l.ps.emission;
                em.rateOverTimeMultiplier = l.baseRate * l.weight * i * emissionRate * l.amount;
            }
        }

        void ApplyRenderValues()
        {
            float i = tornadoIntensity;
            float brightness = i <= 1f ? i : 1f + (i - 1f) * 0.8f;
            Color c = tornadoColor;
            Color hlBase = Color.Lerp(c, new Color(1f, 0.95f, 0.75f), 0.6f);

            foreach (var l in layers)
            {
                if (l.r == null) continue;
                float b = brightness;
                if (l.isGlow) b *= glowIntensity;
                if (l.dimWithWeight) b *= l.weight;

                Color body = Color.Lerp(c, Color.white, l.bodyWhiten) * l.bodyGain;
                body.a = l.bodyAlpha;
                Color hl = hlBase * l.hlGain; hl.a = 1f;

                l.r.GetPropertyBlock(mpb);
                mpb.SetColor(ID_Body, body);
                if (l.hlGain > 0f) mpb.SetColor(ID_Highlight, hl);
                mpb.SetFloat(ID_Intensity, b);
                mpb.SetVector(ID_Scroll, new Vector4(l.baseScroll.x, l.baseScroll.y * verticalSpeed, 0f, 0f));
                l.r.SetPropertyBlock(mpb);
            }
        }

        [ContextMenu("Play Tornado")] void CtxPlay() => PlayTornado();
        [ContextMenu("Stop Tornado")] void CtxStop() => StopTornado();
    }
}
