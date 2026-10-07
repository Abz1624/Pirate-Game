using System;
using System.Collections.Generic;
using HighSeas.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace HighSeas.VFX
{
    /// <summary>
    /// Self-contained "raining fire" mortar barrage rig. Drop MortarBarrage_Rig anywhere (or inside any ship),
    /// drag its MortarMount (start) and MortarTarget children where you want, press Play.
    /// Code: Fire() at the Target, or Fire(worldPos). Visual only: hook VolleyImpact for damage.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MortarBarrageEmitter : MonoBehaviour
    {
        [Header("Points")]
        [Tooltip("Where the shells leave from. Empty = this object.")]
        [SerializeField] Transform launchPoint;
        [Tooltip("Default target for Fire() / preview.")]
        [FormerlySerializedAs("testTarget")]
        [SerializeField] Transform target;

        [Header("Prefabs")]
        [SerializeField] GameObject shellPrefab;
        [SerializeField] GameObject muzzlePrefab;
        [SerializeField] GameObject impactPrefab;
        [SerializeField] GameObject splashPrefab;
        [SerializeField] GameObject telegraphPrefab;

        [Header("Volley")]
        [Range(1, 16)] [SerializeField] int shellCount = 8;
        [Tooltip("Seconds from firing to the volley's main impact.")]
        [Range(0.6f, 3f)] [SerializeField] float flightSeconds = 1.5f;
        [Tooltip("Seconds over which the shells leave the deck (ripple fire).")]
        [Min(0f)] [SerializeField] float launchWindow = 0.45f;
        [Tooltip("Shells land spread over this many seconds, centred on the main impact.")]
        [Min(0f)] [SerializeField] float landingSpread = 0.5f;
        [Tooltip("Radius of the target area (m).")]
        [Min(0.5f)] [SerializeField] float blastRadius = 4f;
        [Tooltip("Fraction of the blast radius the shells scatter over.")]
        [Range(0f, 1f)] [SerializeField] float scatter = 0.8f;
        [Tooltip("Sideways spread of the barrels around this point (m).")]
        [Min(0f)] [SerializeField] float deckSpread = 0.9f;
        [Tooltip("Muzzle smoke every N shells (1 = every shell).")]
        [Min(1)] [SerializeField] int muzzleEvery = 2;

        [Header("Sizes")]
        [Min(0.05f)] [SerializeField] float shellScale = 1f;
        [Min(0.05f)] [SerializeField] float muzzleScale = 1f;
        [Min(0.05f)] [SerializeField] float impactScale = 0.45f;
        [Min(0.05f)] [SerializeField] float splashScale = 0.85f;
        [Tooltip("Height of the water / ground the shells land on.")]
        [SerializeField] float impactHeight = 0.2f;

        [Header("Telegraph")]
        [SerializeField] bool showTelegraph = true;
        [SerializeField] Color telegraphColor = new(0.92f, 0.12f, 0.08f, 1f);
        [Range(0f, 1f)] [SerializeField] float telegraphSaturation = 0.19f;
        [Range(0f, 1f)] [SerializeField] float telegraphAlpha = 0.21f;

        [Header("Preview")]
        [Tooltip("Keep firing at the Target on a timer.")]
        [SerializeField] bool autoFire = true;
        [Min(0.2f)] [SerializeField] float autoFireInterval = 2.5f;
        [Tooltip("Press Space in Play mode to fire at the Target.")]
        [SerializeField] bool fireOnSpace = true;

        /// <summary>Fires once per shell as it lands (world position).</summary>
        public event Action<Vector3> ShellLanded;
        /// <summary>Fires once per volley at the main impact time (target centre) — use for damage.</summary>
        public event Action<Vector3, float> VolleyImpact;

        public float FlightSeconds => flightSeconds;
        public float BlastRadius => blastRadius;
        public Transform LaunchPoint => launchPoint != null ? launchPoint : transform;
        public Transform Target { get => target; set => target = value; }

        struct PendingImpact
        {
            public Vector3 position;
            public float timeLeft;
        }

        readonly List<PendingImpact> _pending = new();
        float _autoFireTimer;

        void OnEnable() => _autoFireTimer = 0.5f;

        void Update()
        {
            if (RunManager.Instance != null && RunManager.Instance.IsPaused) return;
            var dt = Time.deltaTime;

            for (var i = _pending.Count - 1; i >= 0; i--)
            {
                var p = _pending[i];
                p.timeLeft -= dt;
                if (p.timeLeft > 0f)
                {
                    _pending[i] = p;
                    continue;
                }
                _pending.RemoveAt(i);
                VolleyImpact?.Invoke(p.position, blastRadius);
            }

            if (target == null) return;
            if (fireOnSpace && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                Fire();
                _autoFireTimer = autoFireInterval;
                return;
            }
            if (!autoFire) return;
            _autoFireTimer -= dt;
            if (_autoFireTimer > 0f) return;
            _autoFireTimer = autoFireInterval;
            Fire();
        }

        /// <summary>Fire at the assigned Target.</summary>
        [ContextMenu("Fire At Target")]
        public void Fire()
        {
            if (!Application.isPlaying || target == null) return;
            Fire(target.position);
        }

        public void Fire(Transform target)
        {
            if (target != null) Fire(target.position);
        }

        /// <summary>Launch a volley from this transform at a world position.</summary>
        public void Fire(Vector3 target)
        {
            target.y = impactHeight;
            var origin = LaunchPoint.position;
            var flat = target - origin;
            flat.y = 0f;
            var forward = flat.sqrMagnitude > 0.0001f ? flat.normalized : transform.forward;
            var side = Vector3.Cross(Vector3.up, forward);
            var launchDir = (forward + Vector3.up * 1.4f).normalized;

            if (showTelegraph) PlayTelegraph(target);
            _pending.Add(new PendingImpact { position = target, timeLeft = flightSeconds });

            var count = Mathf.Max(1, shellCount);
            var area = blastRadius * scatter;
            for (var i = 0; i < count; i++)
            {
                var k = count > 1 ? i / (float)(count - 1) : 0.5f;
                var delay = count > 1 ? k * launchWindow : 0f;
                var landAt = flightSeconds + (k - 0.5f) * (count > 1 ? landingSpread : 0f)
                    + (count > 1 ? UnityEngine.Random.Range(-0.04f, 0.04f) : 0f);
                var flight = Mathf.Max(0.35f, landAt - delay);

                var from = origin;
                var to = target;
                if (count > 1)
                {
                    from += side * UnityEngine.Random.Range(-deckSpread, deckSpread)
                            + forward * UnityEngine.Random.Range(-0.3f, 0.3f);
                    var angle = UnityEngine.Random.value * Mathf.PI * 2f;
                    var dist = area * Mathf.Sqrt(UnityEngine.Random.value);
                    to += new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                }

                SpawnShell(from, to, delay, flight, i % Mathf.Max(1, muzzleEvery) == 0, forward, launchDir);
            }
        }

        void SpawnShell(Vector3 from, Vector3 to, float delay, float flight, bool muzzle, Vector3 forward, Vector3 launchDir)
        {
            if (shellPrefab == null)
            {
                if (muzzle) PlayMuzzle(from, forward);
                return;
            }

            var shell = Instantiate(shellPrefab, from, Quaternion.LookRotation(launchDir, Vector3.up));
            shell.transform.localScale = shellPrefab.transform.localScale * shellScale;
            var mover = shell.GetComponent<MortarShellFlight>();
            if (mover == null) mover = shell.AddComponent<MortarShellFlight>();

            Action<GameObject> onLaunch = null;
            if (muzzle) onLaunch = _ => PlayMuzzle(from, forward);

            mover.Launch(from, to, delay, flight, onLaunch, go =>
            {
                PlayImpact(to);
                ShellLanded?.Invoke(to);
                Destroy(go);
            });
        }

        void PlayMuzzle(Vector3 at, Vector3 forward)
        {
            if (muzzlePrefab == null) return;
            var go = Instantiate(muzzlePrefab, at, Quaternion.LookRotation(forward, Vector3.up) * muzzlePrefab.transform.localRotation);
            go.transform.localScale = muzzlePrefab.transform.localScale * muzzleScale;
            VfxParticleUtil.RestartParticles(go);
            Destroy(go, 1.2f);
        }

        void PlayImpact(Vector3 at)
        {
            if (impactPrefab != null)
            {
                var burst = Instantiate(impactPrefab, at + Vector3.up * 0.3f, Quaternion.identity);
                burst.transform.localScale = Vector3.one * impactScale;
                VfxParticleUtil.RestartParticles(burst);
                Destroy(burst, 1.2f);
            }

            if (splashPrefab != null)
            {
                var pos = at;
                pos.y = impactHeight + 0.15f;
                var splash = Instantiate(splashPrefab, pos, Quaternion.identity);
                splash.transform.localScale = Vector3.one * splashScale;
                VfxParticleUtil.RestartParticles(splash);
                Destroy(splash, 1.2f);
            }
        }

        void PlayTelegraph(Vector3 at)
        {
            if (telegraphPrefab == null) return;
            var pos = at;
            pos.y = impactHeight - 0.05f;
            var go = Instantiate(telegraphPrefab, pos, Quaternion.identity);
            go.transform.localScale = Vector3.one * WeaponJuiceController.MortarTelegraphScaleForRadius(blastRadius, 1f);
            VfxParticleUtil.SetRenderersEnabled(go, true);
            VfxParticleUtil.ApplyTint(go, telegraphColor, telegraphSaturation, telegraphAlpha);
            VfxParticleUtil.RestartParticles(go);
            Destroy(go, flightSeconds + 0.12f);
        }

        void OnDrawGizmos()
        {
            var from = LaunchPoint.position;
            Gizmos.color = new Color(1f, 0.45f, 0.2f, 1f);
            Gizmos.DrawSphere(from, 0.3f);
            if (target == null) return;
            var to = target.position;
            to.y = impactHeight;
            var flat = to - from;
            flat.y = 0f;
            var apex = Mathf.Clamp(flat.magnitude * 0.55f, 6f, 20f);
            var prev = from;
            for (var i = 1; i <= 24; i++)
            {
                var u = i / 24f;
                var p = Vector3.Lerp(from, to, u);
                p.y += 4f * apex * u * (1f - u);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
            Gizmos.DrawWireSphere(to, blastRadius);
        }
    }
}
