using UnityEngine;

namespace HighSeas.VFX
{
    /// <summary>
    /// Flat aqua-teal aura ring around a ship. All layers are authored at radius 1; the root is scaled
    /// to the live aura radius and eases to new sizes (so level-ups visibly expand the ring).
    /// Works standalone: drop TidalAura_VFX anywhere and set Radius in the Inspector.
    /// </summary>
    [ExecuteAlways]
    public sealed class TidalAuraVisual : MonoBehaviour
    {
        [Header("Size")]
        [Tooltip("Aura radius in metres (matches the damage radius in game).")]
        [Min(0.1f)] [SerializeField] float radius = 4f;
        [Tooltip("How fast the ring eases to a new radius (level-up expand).")]
        [Min(0.1f)] [SerializeField] float growSpeed = 3f;

        [Header("Layers (authored at radius 1)")]
        [Tooltip("Caustic water band (spins one way).")]
        [SerializeField] Transform sigil;
        [Tooltip("Second caustic band (spins the other way for shimmer).")]
        [SerializeField] Transform swirl;
        [Tooltip("Raised glassy lip with runes.")]
        [SerializeField] Transform runeWall;
        [SerializeField] Transform rim;
        [SerializeField] Transform pulseRing;
        [SerializeField] Transform[] orbs;

        [Header("Motion")]
        [SerializeField] float sigilSpinDegrees = 10f;
        [SerializeField] float swirlSpinDegrees = -18f;
        [Tooltip("Base orbit speed (deg/s). Each orb multiplies this by its own entry in Orb Speeds.")]
        [SerializeField] float orbSpinDegrees = 40f;
        [Tooltip("Per-orb speed multiplier (negative = orbit the other way). Missing entries use 1.")]
        [SerializeField] float[] orbSpeeds = { 1f, 0.65f, 1.45f, 0.85f };
        [Tooltip("Up/down bob of each orb, as a fraction of radius.")]
        [SerializeField] float orbBob = 0.02f;
        [SerializeField] float orbBobSpeed = 2.4f;
        [SerializeField] float runeSpinDegrees = 6f;
        [Tooltip("Orb height above the water, as a fraction of radius (sits on the rune lip).")]
        [SerializeField] float orbHeight = 0.075f;
        [Tooltip("Orb size in metres (kept constant as the ring grows).")]
        [Min(0.01f)] [SerializeField] float orbSize = 0.55f;
        [SerializeField] float rimBreathe = 0.08f;
        [SerializeField] float rimBreatheSpeed = 2.2f;

        [Header("Damage Pulse")]
        [Min(0.05f)] [SerializeField] float pulseDuration = 0.45f;
        [Tooltip("Pulse ring size when it starts (0 = centre, 1 = rim).")]
        [Range(0f, 1.5f)] [SerializeField] float pulseStartScale = 0.05f;
        [Tooltip("Pulse ring size when it ends (1 = rim).")]
        [Range(0f, 1.5f)] [SerializeField] float pulseEndScale = 1f;
        [Tooltip("Preview only: fire a pulse on this interval (0 = off). In game the weapon tick drives it.")]
        [Min(0f)] [SerializeField] float previewPulseInterval;

        float _current = -1f;
        float _pulseTime = -1f;
        float _previewTimer;
        float[] _orbAngles;
        Renderer _pulseRenderer;
        Renderer _rimRenderer;
        MaterialPropertyBlock _mpb;
        Color _pulseColor = Color.white;
        Color _rimColor = Color.white;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public float Radius
        {
            get => radius;
            set => radius = Mathf.Max(0.1f, value);
        }

        /// <summary>Set the target radius. instant = snap instead of easing.</summary>
        public void SetRadius(float value, bool instant = false)
        {
            Radius = value;
            if (instant) _current = radius;
        }

        /// <summary>Flash an expanding ring (call on each damage tick).</summary>
        public void Pulse() => _pulseTime = 0f;

        void OnEnable()
        {
            _mpb ??= new MaterialPropertyBlock();
            if (pulseRing != null)
            {
                _pulseRenderer = pulseRing.GetComponentInChildren<Renderer>();
                if (_pulseRenderer != null && _pulseRenderer.sharedMaterial != null
                    && _pulseRenderer.sharedMaterial.HasProperty(BaseColorId))
                    _pulseColor = _pulseRenderer.sharedMaterial.GetColor(BaseColorId);
            }
            if (rim != null)
            {
                _rimRenderer = rim.GetComponent<Renderer>();
                if (_rimRenderer != null && _rimRenderer.sharedMaterial != null
                    && _rimRenderer.sharedMaterial.HasProperty(BaseColorId))
                    _rimColor = _rimRenderer.sharedMaterial.GetColor(BaseColorId);
            }
            _current = radius;
            SetPulseAlpha(0f);
        }

        void LateUpdate()
        {
            var dt = Application.isPlaying ? Time.deltaTime : 0f;

            if (_current < 0f || !Application.isPlaying) _current = radius;
            else _current = Mathf.Lerp(_current, radius, 1f - Mathf.Exp(-growSpeed * dt));
            transform.localScale = Vector3.one * _current;

            if (sigil != null) sigil.Rotate(0f, sigilSpinDegrees * dt, 0f, Space.Self);
            if (swirl != null) swirl.Rotate(0f, swirlSpinDegrees * dt, 0f, Space.Self);
            if (runeWall != null) runeWall.Rotate(0f, runeSpinDegrees * dt, 0f, Space.Self);

            if (_rimRenderer != null)
            {
                var k = 1f - rimBreathe * 0.5f + Mathf.Sin(Time.time * rimBreatheSpeed) * rimBreathe * 0.5f;
                _mpb.SetColor(BaseColorId, new Color(_rimColor.r, _rimColor.g, _rimColor.b, _rimColor.a * k));
                _rimRenderer.SetPropertyBlock(_mpb);
            }

            UpdateOrbs(dt);
            UpdatePulse(dt);
        }

        void UpdateOrbs(float dt)
        {
            if (orbs == null || orbs.Length == 0) return;
            if (_orbAngles == null || _orbAngles.Length != orbs.Length)
            {
                _orbAngles = new float[orbs.Length];
                for (var i = 0; i < orbs.Length; i++) _orbAngles[i] = i * 360f / orbs.Length;
            }
            var cam = Camera.main;
            var inv = 1f / Mathf.Max(0.01f, _current);
            for (var i = 0; i < orbs.Length; i++)
            {
                var orb = orbs[i];
                if (orb == null) continue;
                var mult = orbSpeeds != null && i < orbSpeeds.Length ? orbSpeeds[i] : 1f;
                _orbAngles[i] += orbSpinDegrees * mult * dt;
                var a = _orbAngles[i] * Mathf.Deg2Rad;
                var bob = Mathf.Sin(Time.time * orbBobSpeed + i * 1.7f) * orbBob;
                orb.localPosition = new Vector3(Mathf.Cos(a), orbHeight + bob, Mathf.Sin(a));
                orb.localScale = Vector3.one * orbSize * inv;
                if (cam != null) orb.rotation = Quaternion.LookRotation(orb.position - cam.transform.position, cam.transform.up);
            }
        }

        void UpdatePulse(float dt)
        {
            if (previewPulseInterval > 0f && Application.isPlaying)
            {
                _previewTimer -= dt;
                if (_previewTimer <= 0f)
                {
                    _previewTimer = previewPulseInterval;
                    Pulse();
                }
            }

            if (pulseRing == null || _pulseTime < 0f) return;
            _pulseTime += dt;
            var u = Mathf.Clamp01(_pulseTime / pulseDuration);
            pulseRing.localScale = Vector3.one * Mathf.Lerp(pulseStartScale, pulseEndScale, 1f - (1f - u) * (1f - u));
            SetPulseAlpha(Mathf.Clamp01(u * 6f) * (1f - u * u));
            if (u >= 1f)
            {
                _pulseTime = -1f;
                SetPulseAlpha(0f);
            }
        }

        void SetPulseAlpha(float k)
        {
            if (_pulseRenderer == null) return;
            _mpb ??= new MaterialPropertyBlock();
            _mpb.SetColor(BaseColorId, new Color(_pulseColor.r, _pulseColor.g, _pulseColor.b, _pulseColor.a * k));
            _pulseRenderer.SetPropertyBlock(_mpb);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.25f, 1f, 0.85f, 1f);
            var c = transform.position;
            var prev = c + new Vector3(radius, 0f, 0f);
            for (var i = 1; i <= 48; i++)
            {
                var a = i / 48f * Mathf.PI * 2f;
                var p = c + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
    }
}
