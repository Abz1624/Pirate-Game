using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StormOcean
{
    /// <summary>
    /// Procedural storm lightning: random unpredictable flashes that briefly spike
    /// the main light, drive global shader illumination terms read by the ocean
    /// and cloud shaders, and occasionally draw a jagged bolt using a LineRenderer
    /// built with recursive midpoint displacement.
    /// </summary>
    public class LightningController : MonoBehaviour
    {
        [Header("Timing")]
        public float minTimeBetweenFlashes = 4f;
        public float maxTimeBetweenFlashes = 14f;
        [Range(0f, 1f)] public float flashProbability = 0.85f;
        [Range(0f, 1f)] public float secondaryFlashChance = 0.35f;

        [Header("Flash")]
        public float flashDuration = 0.18f;
        public float flashIntensityMin = 2.5f;
        public float flashIntensityMax = 6f;
        public Color flashColor = new Color(0.75f, 0.85f, 1f, 1f);

        [Header("Illumination Targets")]
        public Light mainLight;
        public Light skyFlashLight;
        [Range(0f, 1f)] public float cloudIllumination = 1f;
        [Range(0f, 1f)] public float oceanIllumination = 0.8f;
        [Range(0f, 1f)] public float rainIllumination = 0.6f;
        [Range(0f, 1f)] public float atmosphereIllumination = 0.7f;

        [Header("Bolt Visual")]
        public LineRenderer boltLine;
        [Range(0f, 1f)] public float boltChance = 0.5f;
        public float boltHeight = 60f;
        public float boltHorizontalRange = 90f;

        float mainLightBaseIntensity = 1f;
        Color baseFogColor = Color.black;
        Coroutine loopRoutine;

        void OnEnable()
        {
            if (mainLight != null) mainLightBaseIntensity = mainLight.intensity;
            baseFogColor = RenderSettings.fogColor;

            if (boltLine != null) boltLine.enabled = false;

            if (Application.isPlaying)
            {
                loopRoutine = StartCoroutine(FlashLoop());
            }
        }

        void OnDisable()
        {
            if (loopRoutine != null) StopCoroutine(loopRoutine);
            ApplyFlash(0f);
        }

        IEnumerator FlashLoop()
        {
            while (true)
            {
                float wait = Random.Range(minTimeBetweenFlashes, maxTimeBetweenFlashes);
                yield return new WaitForSeconds(wait);

                if (Random.value <= flashProbability)
                {
                    yield return StartCoroutine(DoFlash());

                    if (Random.value <= secondaryFlashChance)
                    {
                        yield return new WaitForSeconds(Random.Range(0.05f, 0.25f));
                        yield return StartCoroutine(DoFlash());
                    }
                }
            }
        }

        IEnumerator DoFlash()
        {
            float peak = Random.Range(flashIntensityMin, flashIntensityMax);
            float riseTime = Mathf.Max(0.01f, flashDuration * 0.15f);
            float fallTime = Mathf.Max(0.01f, flashDuration * 0.85f);

            if (Random.value <= boltChance) SpawnBolt();

            float t = 0f;
            while (t < riseTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / riseTime);
                ApplyFlash(k * peak);
                yield return null;
            }

            t = 0f;
            while (t < fallTime)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / fallTime);
                ApplyFlash(k * k * peak);
                yield return null;
            }

            ApplyFlash(0f);
        }

        void ApplyFlash(float intensity)
        {
            Shader.SetGlobalFloat("_StormLightningIntensity", intensity * oceanIllumination);
            Shader.SetGlobalColor("_StormLightningColor", flashColor);
            Shader.SetGlobalFloat("_StormLightningCloud", intensity * cloudIllumination);
            Shader.SetGlobalFloat("_StormLightningRain", intensity * rainIllumination);
            Shader.SetGlobalFloat("_StormLightningAtmosphere", intensity * atmosphereIllumination);

            if (mainLight != null)
                mainLight.intensity = mainLightBaseIntensity + intensity * 2.5f;

            if (skyFlashLight != null)
            {
                skyFlashLight.intensity = intensity * 8f;
                skyFlashLight.enabled = intensity > 0.01f;
            }

            if (RenderSettings.fog)
            {
                RenderSettings.fogColor = Color.Lerp(baseFogColor, flashColor, Mathf.Clamp01(intensity * atmosphereIllumination * 0.5f));
            }
        }

        void SpawnBolt()
        {
            if (boltLine == null) return;
            StartCoroutine(BoltRoutine());
        }

        IEnumerator BoltRoutine()
        {
            Vector3 origin = transform.position;
            Vector3 start = origin + new Vector3(Random.Range(-boltHorizontalRange, boltHorizontalRange), boltHeight, Random.Range(20f, boltHorizontalRange));
            Vector3 end = start + new Vector3(Random.Range(-15f, 15f), -Random.Range(boltHeight * 0.5f, boltHeight * 0.8f), Random.Range(-10f, 10f));

            Vector3[] points = GenerateBolt(start, end, 6);
            boltLine.positionCount = points.Length;
            boltLine.SetPositions(points);
            boltLine.enabled = true;

            yield return new WaitForSeconds(0.1f);
            boltLine.enabled = false;
        }

        static Vector3[] GenerateBolt(Vector3 start, Vector3 end, int iterations)
        {
            var pts = new List<Vector3> { start, end };
            float displaceAmount = Vector3.Distance(start, end) * 0.5f;

            for (int it = 0; it < iterations; it++)
            {
                var next = new List<Vector3>();
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    Vector3 a = pts[i];
                    Vector3 b = pts[i + 1];
                    Vector3 mid = (a + b) * 0.5f;

                    Vector3 dir = (b - a);
                    if (dir.sqrMagnitude < 0.0001f) dir = Vector3.down;
                    dir.Normalize();

                    Vector3 perp = Vector3.Cross(dir, Vector3.forward);
                    if (perp.sqrMagnitude < 0.001f) perp = Vector3.Cross(dir, Vector3.right);
                    perp.Normalize();

                    mid += perp * Random.Range(-displaceAmount, displaceAmount);

                    next.Add(a);
                    next.Add(mid);
                }
                next.Add(pts[pts.Count - 1]);
                pts = next;
                displaceAmount *= 0.55f;
            }
            return pts.ToArray();
        }
    }
}
