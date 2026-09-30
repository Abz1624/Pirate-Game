using UnityEngine;

/// <summary>
/// Slowly pulses a renderer's emission (URP Lit _EmissionColor) using a MaterialPropertyBlock.
/// No material instances are created, and nothing is allocated per frame. Mobile friendly.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
public class MapEmissionPulse : MonoBehaviour
{
    [ColorUsage(false, true)]
    [Tooltip("Base HDR emission color (tinted by the material's emission map).")]
    public Color emissionColor = new Color(1f, 0.78f, 0.38f) * 0.9f;

    [Tooltip("Emission multiplier at the dimmest point of the pulse.")]
    [Range(0f, 3f)] public float minIntensity = 0.65f;

    [Tooltip("Emission multiplier at the brightest point of the pulse.")]
    [Range(0f, 3f)] public float maxIntensity = 1.25f;

    [Tooltip("Seconds for one full dim -> bright -> dim cycle.")]
    [Min(0.1f)] public float period = 3.6f;

    [Tooltip("Randomize the starting phase so multiple maps don't pulse in sync.")]
    public bool randomPhase = true;

    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    Renderer _renderer;
    MaterialPropertyBlock _mpb;
    float _phase;

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
        _phase = randomPhase ? Random.value * period : 0f;
    }

    void Update()
    {
        // Smooth 0..1 sine wave, eased so it lingers softly at both ends.
        float t = 0.5f - 0.5f * Mathf.Cos((Time.time + _phase) * (2f * Mathf.PI / period));
        t = t * t * (3f - 2f * t);
        float k = Mathf.Lerp(minIntensity, maxIntensity, t);

        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetColor(EmissionColorId, emissionColor * k);
        _renderer.SetPropertyBlock(_mpb);
    }

    void OnDisable()
    {
        if (_renderer == null) return;
        _renderer.GetPropertyBlock(_mpb);
        _mpb.Clear();
        _renderer.SetPropertyBlock(_mpb);
    }
}
