using UnityEngine;

namespace StormOcean
{
    /// <summary>
    /// Cinematic water-spray particles concentrated around approximated wave-crest
    /// positions (a cheap CPU-side noise crest estimate, independent of the ocean
    /// shader's exact Jacobian foam so it stays lightweight).
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class OceanSprayController : MonoBehaviour
    {
        [Header("Spray")]
        [Range(0f, 1f)] public float sprayAmount = 0.6f;
        public float spraySize = 0.6f;
        public float sprayLifetime = 1.1f;
        public float sprayDistance = 25f;
        [Range(0f, 1f)] public float sprayOpacity = 0.5f;
        public float noiseScale = 0.05f;
        [Range(0f, 1f)] public float noiseThreshold = 0.62f;
        public int samplesPerSecond = 40;

        public StormWindController wind;
        public Transform oceanReference;

        ParticleSystem ps;
        ParticleSystem.EmitParams emitParams;
        float spawnTimer;

        void OnEnable()
        {
            ps = GetComponent<ParticleSystem>();

            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = sprayLifetime;
            main.startSize = new ParticleSystem.MinMaxCurve(spraySize * 0.5f, spraySize);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            main.gravityModifier = 1.1f;
            main.playOnAwake = false;
            main.maxParticles = 2000;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null) renderer.renderMode = ParticleSystemRenderMode.Billboard;

            if (Application.isPlaying) ps.Play();
        }

        void Update()
        {
            if (ps == null || !Application.isPlaying) return;
            if (sprayAmount <= 0.001f) return;

            spawnTimer += Time.deltaTime;
            float interval = 1f / Mathf.Max(1, samplesPerSecond);

            Vector3 windDir = wind != null ? wind.WindDirection : Vector3.forward;
            float windStrength = wind != null ? wind.windStrength : 1f;

            while (spawnTimer >= interval)
            {
                spawnTimer -= interval;
                TrySpawn(windDir, windStrength);
            }
        }

        void TrySpawn(Vector3 windDir, float windStrength)
        {
            Vector3 center = oceanReference != null ? oceanReference.position : transform.position;
            Vector2 rnd = Random.insideUnitCircle * sprayDistance;
            Vector3 pos = center + new Vector3(rnd.x, 0f, rnd.y);

            float n = Mathf.PerlinNoise(pos.x * noiseScale, pos.z * noiseScale + Time.time * 0.15f);
            float n2 = Mathf.PerlinNoise(pos.x * noiseScale * 2.7f + 91f, pos.z * noiseScale * 2.7f - Time.time * 0.22f);
            n = Mathf.Max(n, n2 * 0.6f);

            if (n < noiseThreshold) return;
            if (Random.value > sprayAmount) return;

            emitParams.position = pos;
            emitParams.velocity = Vector3.up * Random.Range(2f, 4f) + windDir * windStrength;
            emitParams.startColor = new Color(0.85f, 0.92f, 0.96f, sprayOpacity);
            emitParams.startSize = spraySize * Random.Range(0.6f, 1.2f);
            ps.Emit(emitParams, 1);
        }
    }
}
