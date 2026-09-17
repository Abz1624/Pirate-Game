using UnityEngine;

namespace StormOcean
{
    /// <summary>
    /// Global storm wind system. Drives rain angle, ocean ripple direction and spray drift
    /// via global shader properties and direct references from other controllers.
    /// </summary>
    [ExecuteAlways]
    public class StormWindController : MonoBehaviour
    {
        [Header("Wind")]
        [Tooltip("Wind direction on the horizontal plane, in degrees (0 = +Z).")]
        public float windDirectionDegrees = 35f;
        [Range(0f, 3f)] public float windStrength = 1.2f;
        [Range(0f, 1f)] public float windTurbulence = 0.5f;

        public Vector3 WindDirection { get; private set; } = Vector3.forward;

        void OnEnable() { Apply(); }
        void Update() { Apply(); }
        void OnValidate() { Apply(); }

        void Apply()
        {
            float rad = windDirectionDegrees * Mathf.Deg2Rad;
            WindDirection = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));

            Shader.SetGlobalVector("_StormWindDir", WindDirection);
            Shader.SetGlobalFloat("_StormWindStrength", windStrength);
            Shader.SetGlobalFloat("_StormWindTurbulence", windTurbulence);
        }
    }
}
