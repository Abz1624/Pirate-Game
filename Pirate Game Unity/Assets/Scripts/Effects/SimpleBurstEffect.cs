using System.Collections;
using UnityEngine;

namespace PirateGame.Effects
{
    /// <summary>
    /// Lightweight procedural burst — no particle assets required.
    /// </summary>
    public static class SimpleBurstEffect
    {
        public static void Play(Vector3 position, Color color, float size = 1.2f, float duration = 0.45f, int shards = 8)
        {
            var host = new GameObject("BurstEffect");
            host.transform.position = position;
            var runner = host.AddComponent<BurstRunner>();
            runner.Run(color, size, duration, shards);
        }

        public static void PlayCrystalSpawn(Vector3 position, Color color)
        {
            Play(position, color, 0.6f, 0.3f, 6);
        }

        sealed class BurstRunner : MonoBehaviour
        {
            public void Run(Color color, float size, float duration, int shards)
            {
                StartCoroutine(Animate(color, size, duration, shards));
            }

            IEnumerator Animate(Color color, float size, float duration, int shards)
            {
                var pieces = new Transform[shards];
                var velocities = new Vector3[shards];
                var startScales = new float[shards];

                for (var i = 0; i < shards; i++)
                {
                    var piece = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    piece.name = "Shard";
                    Object.Destroy(piece.GetComponent<Collider>());
                    piece.transform.SetParent(transform, false);
                    piece.transform.localScale = Vector3.one * (size * 0.12f);
                    startScales[i] = piece.transform.localScale.x;

                    var renderer = piece.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                        renderer.material.color = color;
                        if (renderer.material.HasProperty("_EmissionColor"))
                            renderer.material.EnableKeyword("_EMISSION");
                    }

                    var angle = (Mathf.PI * 2f * i) / shards;
                    velocities[i] = new Vector3(Mathf.Cos(angle), 0.35f + Random.Range(0f, 0.25f), Mathf.Sin(angle)).normalized * (size * 2.2f);
                    pieces[i] = piece.transform;
                }

                var elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    var t = elapsed / duration;
                    for (var i = 0; i < shards; i++)
                    {
                        if (pieces[i] == null) continue;
                        pieces[i].position += velocities[i] * Time.deltaTime;
                        velocities[i] *= 0.92f;
                        var scale = Mathf.Lerp(startScales[i], 0f, t);
                        pieces[i].localScale = Vector3.one * scale;
                    }

                    yield return null;
                }

                Destroy(gameObject);
            }
        }
    }
}
