using UnityEngine;

namespace RoguePirates.V4.Foundation.VFX
{
    /// <summary>
    /// Hackathon (12 Oct): the small idle motion the art team's weapon models had from their own (V3-era) scripts, written
    /// fresh for V4: a steady spin about a local axis (the chainshot's balls, the ink pot's tumble) or a swing back and
    /// forth (the parrots' wings). Presentation only: scaled game time (it stops when paused), no random numbers, no allocation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArtMotion : MonoBehaviour
    {
        public enum Mode { Spin, Swing }

        public Mode mode = Mode.Spin;
        public Vector3 axis = Vector3.up;
        [Tooltip("Spin: degrees per second. Swing: the swing's half-angle in degrees.")]
        public float degrees = 360f;
        [Tooltip("Swing: full swings per second.")]
        public float frequency = 2f;
        [Tooltip("Swing: phase offset in turns (a mirrored wing uses 0.5).")]
        public float phase;

        Quaternion _rest;

        void Awake() => _rest = transform.localRotation;

        void Update()
        {
            if (mode == Mode.Spin)
                transform.localRotation *= Quaternion.AngleAxis(degrees * Time.deltaTime, axis);
            else
                transform.localRotation = _rest * Quaternion.AngleAxis(degrees * Mathf.Sin((Time.time * frequency + phase) * Mathf.PI * 2f), axis);
        }
    }
}
