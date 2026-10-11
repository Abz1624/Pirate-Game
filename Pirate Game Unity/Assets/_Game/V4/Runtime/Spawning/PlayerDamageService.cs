using System;
using UnityEngine;

namespace RoguePirates.V4.Spawning
{
    /// <summary>The player's hull as damage reaches it (the shared ship's damage receiver, through the ship port).</summary>
    public interface IPlayerHull
    {
        bool HullExists { get; }
        bool HullAlive { get; }
        Vector3 HullPosition { get; }
        float HullHp { get; }
        /// <summary>God mode / Module Lab: no damage at all.</summary>
        bool HullInvulnerable { get; }
        /// <summary>A hit (the receiver's hit immunity applies; its Damaged and Died events fire).</summary>
        void HitHull(float amount, Vector3 source);
        /// <summary>Takes hull points without a hit (no immunity, no event).</summary>
        void DrainHull(float amount);
        /// <summary>V5: a knock (island impact): the Damaged and Died events fire, the hit immunity is neither checked nor started.</summary>
        void KnockHull(float amount, Vector3 source) => HitHull(amount, source);
    }

    /// <summary>
    /// Damage to the player ship from stage content and the storm (M14.14; was LegacyPlayerDamageSink): the build's
    /// incoming-damage multiplier (Brace!, Reinforced Keel) on every source; steady damage (the storm, R-57) drains the
    /// hull each frame without the hit immunity and reports the summed amount every half second (feedback, telemetry,
    /// the steady PlayerDamaged event); a lethal steady tick becomes a hit so death runs as usual.
    /// </summary>
    public sealed class PlayerDamageService : IPlayerDamageSink
    {
        /// <summary>Seconds between reports of accumulated steady damage.</summary>
        public const float SteadyReportInterval = 0.5f;

        readonly IPlayerHull _hull;
        readonly Func<float> _time;
        float _steadyPending;
        float _steadyLastReport = float.NegativeInfinity;

        public PlayerDamageService(IPlayerHull hull, Func<float> time = null)
        {
            _hull = hull;
            // V5 M9b close (determinism): the run clock by default (was the frame clock, Time.time)
            _time = time ?? (Func<float>)(() => RoguePirates.V4.Foundation.Core.RunManager.Instance != null ? RoguePirates.V4.Foundation.Core.RunManager.Instance.ElapsedSeconds : 0f);
        }

        /// <summary>The build's incoming-damage multiplier (Brace!, Reinforced Keel).</summary>
        public Func<float> IncomingDamage;
        /// <summary>Each frame of steady damage (the items reset their no-hit timers on it).</summary>
        public Action TookSteadyDamage;
        /// <summary>The run lifecycle's damage record (feedback and telemetry).</summary>
        public Action<float> RecordDamage;
        /// <summary>Accumulated steady damage, reported at most every <see cref="SteadyReportInterval"/> seconds.</summary>
        public event Action<float> SteadyDamageReported;

        float Incoming => IncomingDamage?.Invoke() ?? 1f;

        public bool Exists => _hull != null && _hull.HullExists;
        public bool IsAlive => Exists && _hull.HullAlive;
        public Vector3 Position => _hull != null ? _hull.HullPosition : Vector3.zero;

        /// <summary>Playtest (6 Oct, owner): who is hitting the ship (the session recorder's damage by source).</summary>
        public RoguePirates.V4.Events.PlayerDamageSource DamageSource;

        public void Damage(float amount, Vector3 source)
        {
            if (!Exists) return;
            var before = Tag("stage_hit"); // boss slams and marks, the storm in hit mode
            _hull.HitHull(amount * Incoming, source);
            if (DamageSource != null) DamageSource.Current = before;
        }

        /// <summary>Sets the damage source when none is set; returns the one to restore.</summary>
        string Tag(string fallback)
        {
            var before = DamageSource?.Current;
            if (DamageSource != null && before == null) DamageSource.Current = fallback;
            return before;
        }

        /// <summary>
        /// V5 island impact: one knock through the build's incoming multiplier (Brace!, Reinforced Keel) that does not use
        /// the contact hit immunity, so an enemy hit right after a bump still lands; the hit feedback and items see it.
        /// </summary>
        /// <summary>Hull lost to island impacts this run, after the multiplier (tests and telemetry).</summary>
        public float ImpactDamageTotal { get; private set; }

        public void Impact(float amount, Vector3 source)
        {
            if (!IsAlive || _hull.HullInvulnerable) return;
            amount *= Incoming;
            if (amount <= 0f) return;
            var before = Tag("island");
            _hull.KnockHull(amount, source); // its Damaged event reaches the run's damage record and feedback
            if (DamageSource != null) DamageSource.Current = before;
            ImpactDamageTotal += amount;
        }

        /// <summary>Playtest (3 Oct, owner): a 1v1 hit whose hull loss the PvP rules already decided (their own invulnerability
        /// window, the build's multiplier included): taken as is, with the usual Damaged and Died events.</summary>
        public void ExactHit(float amount, Vector3 source)
        {
            if (!IsAlive || _hull.HullInvulnerable || amount <= 0f) return;
            _hull.KnockHull(amount, source);
        }

        /// <summary>Playtest (3 Oct, owner): the 1v1 storm, exactly its hp per second (no build multiplier).</summary>
        public void ExactOverTime(float amount, Vector3 source) => DrainSteady(amount, source, 1f);

        public void DamageOverTime(float amount, Vector3 source) => DrainSteady(amount * Incoming, source, Incoming);

        void DrainSteady(float amount, Vector3 source, float lethalHit)
        {
            if (!IsAlive || _hull.HullInvulnerable) return;
            if (amount <= 0f) return;
            var hp = _hull.HullHp;
            if (amount < hp)
            {
                _hull.DrainHull(amount);
                TookSteadyDamage?.Invoke();
                _steadyPending += amount;
                ReportSteady(false);
                return;
            }
            _steadyPending += hp - 0.001f;
            ReportSteady(true);
            _hull.DrainHull(hp - 0.001f);
            var before = Tag("storm");
            _hull.HitHull(lethalHit, source); // retried next frame if the ship is inside its hit immunity
            if (DamageSource != null) DamageSource.Current = before;
        }

        void ReportSteady(bool force)
        {
            if (_steadyPending <= 0f) return;
            var now = _time();
            if (!force && now - _steadyLastReport < SteadyReportInterval) return;
            var amount = _steadyPending;
            _steadyPending = 0f;
            _steadyLastReport = now;
            RecordDamage?.Invoke(amount);
            SteadyDamageReported?.Invoke(amount);
        }
    }
}
