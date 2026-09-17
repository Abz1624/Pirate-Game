using UnityEngine;

namespace StormOcean
{
    /// <summary>
    /// Camera-relative heavy rain system built on Unity's built-in Particle System,
    /// using stretched billboards for long streaks and wind-driven velocity.
    /// Rain Intensity (0-1) scales density, size and speed.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    [ExecuteAlways]
    public class RainController : MonoBehaviour
    {
        [Header("Rain")]
        [Range(0f, 1f)] public float rainIntensity = 0.85f;
        public float maxEmissionRate = 5500f;
        public Vector2 speedRange = new Vector2(28f, 40f);
        public Vector2 sizeRange = new Vector2(0.03f, 0.06f);
        public float streakLength = 1.4f;
        public float spawnRadius = 22f;
        public float spawnHeight = 16f;
        public Color rainColor = new Color(0.72f, 0.8f, 0.86f, 0.22f);

        [Header("Wind")]
        public StormWindController wind;

        ParticleSystem ps;
        bool modulesReady;
        Transform cam;

        void OnEnable()
        {
            ps = GetComponent<ParticleSystem>();
            SetupModules();
        }

        void SetupModules()
        {
            if (ps == null) return;

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 24000;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeRange.x, sizeRange.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedRange.x, speedRange.y);
            main.startColor = rainColor;
            main.gravityModifier = 0.4f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(spawnRadius * 2f, 0.1f, spawnRadius * 2f);

            var velOverLife = ps.velocityOverLifetime;
            velOverLife.enabled = true;
            velOverLife.space = ParticleSystemSimulationSpace.World;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = streakLength * 0.12f;
                renderer.lengthScale = streakLength;
                renderer.alignment = ParticleSystemRenderSpace.World;
            }

            modulesReady = true;
        }

        void LateUpdate()
        {
            if (ps == null) return;
            if (!modulesReady) SetupModules();

            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            Vector3 followPos = cam != null ? cam.position : transform.position;
            transform.position = new Vector3(followPos.x, followPos.y + spawnHeight, followPos.z);

            var emission = ps.emission;
            emission.rateOverTime = maxEmissionRate * rainIntensity;

            Vector3 windDir = wind != null ? wind.WindDirection : Vector3.forward;
            float windStrength = wind != null ? wind.windStrength : 1f;

            var velOverLife = ps.velocityOverLifetime;
            velOverLife.x = windDir.x * windStrength * 3f;
            velOverLife.y = -Mathf.Lerp(speedRange.x, speedRange.y, rainIntensity);
            velOverLife.z = windDir.z * windStrength * 3f;

            var main = ps.main;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeRange.x * Mathf.Lerp(0.6f, 1f, rainIntensity), sizeRange.y);

            float lightning = Shader.GetGlobalFloat("_StormLightningRain");
            Color col = rainColor;
            col = Color.Lerp(col, Color.white, lightning * 0.5f);
            main.startColor = col;

            if (Application.isPlaying)
            {
                if (!ps.isPlaying && rainIntensity > 0.001f) ps.Play();
                if (ps.isPlaying && rainIntensity <= 0.001f) ps.Stop();
            }
        }
    }
}
