using UnityEngine;

namespace RoguePirates.FeelIntegration
{
    /// <summary>
    /// Quick springy squash-and-pop on enemy models when they are hit (driven by EnemyHitFeel, which adds this to its own object). One component handles
    /// every enemy: a small fixed pool of springs (mobile friendly, no allocations). The squash is applied in LateUpdate
    /// and taken back off first thing in FixedUpdate/Update, so nothing that reads an enemy's transform ever sees it,
    /// and a pooled view that gets reused simply starts from the scale its spawner gave it.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class EnemyPunchSystem : MonoBehaviour
    {
        const int Capacity = 16;

        struct Spring
        {
            public Transform View;
            public float Value, Velocity, Omega, Zeta;
            public Vector3 Base, Final;
            public bool Applied;
        }

        readonly Spring[] _springs = new Spring[Capacity];
        int _next;

        [Tooltip("Most squash (0.2 = 20%) however many hits stack up.")]
        public float MaxPunch = 0.22f;

        /// <summary>Squash <paramref name="view"/> by about <paramref name="amount"/> (0.1 = 10% flatter and wider at the
        /// peak), springing back over <paramref name="duration"/> s. Hits on an enemy already squashing add to it.</summary>
        public void Punch(Transform view, float amount, float duration, float frequency)
        {
            if (view == null || amount <= 0f || duration <= 0f) return;
            var index = -1;
            for (var i = 0; i < Capacity; i++) if (_springs[i].View == view) { index = i; break; }
            if (index < 0)
            {
                // A free slot, else the oldest one (it is restored first).
                for (var i = 0; i < Capacity; i++) if (_springs[i].View == null) { index = i; break; }
                if (index < 0) { index = _next; _next = (_next + 1) % Capacity; Restore(ref _springs[index]); }
                _springs[index] = new Spring { View = view };
            }
            ref var s = ref _springs[index];
            s.Omega = Mathf.Max(0.5f, frequency) * Mathf.PI * 2f;
            s.Zeta = Mathf.Clamp(4f / (s.Omega * duration), 0.05f, 0.95f);
            var root = Mathf.Sqrt(1f - s.Zeta * s.Zeta);
            var wd = s.Omega * root;
            var tPeak = Mathf.Atan2(root, s.Zeta) / wd;
            var peakPerVelocity = Mathf.Exp(-s.Zeta * s.Omega * tPeak) * Mathf.Sin(wd * tPeak) / wd;
            s.Velocity += amount / Mathf.Max(1e-4f, peakPerVelocity);
        }

        void FixedUpdate() => RestoreAll();
        void Update() => RestoreAll();

        void RestoreAll()
        {
            for (var i = 0; i < Capacity; i++) Restore(ref _springs[i]);
        }

        static void Restore(ref Spring s)
        {
            if (!s.Applied) return;
            if (s.View != null && s.View.localScale == s.Final) s.View.localScale = s.Base;
            s.Applied = false;
        }

        void LateUpdate()
        {
            var dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            if (dt <= 0f) return;
            for (var i = 0; i < Capacity; i++)
            {
                ref var s = ref _springs[i];
                if (s.View == null) continue;
                if (s.Applied) Restore(ref s);
                if (!s.View.gameObject.activeInHierarchy) { s = default; continue; } // released to the pool

                for (var k = 0; k < 2; k++)
                {
                    var h = dt * 0.5f;
                    s.Velocity += (-s.Omega * s.Omega * s.Value - 2f * s.Zeta * s.Omega * s.Velocity) * h;
                    s.Value += s.Velocity * h;
                }
                s.Value = Mathf.Clamp(s.Value, -MaxPunch, MaxPunch);
                if (Mathf.Abs(s.Value) < 0.0005f && Mathf.Abs(s.Velocity) < 0.01f) { s = default; continue; }

                s.Base = s.View.localScale;
                s.View.localScale = Vector3.Scale(s.Base, new Vector3(1f + s.Value, 1f - s.Value * 0.7f, 1f + s.Value));
                s.Final = s.View.localScale;
                s.Applied = true;
            }
        }

        void OnDisable()
        {
            RestoreAll();
            for (var i = 0; i < Capacity; i++) _springs[i] = default;
        }
    }
}
