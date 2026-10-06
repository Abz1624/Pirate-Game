using UnityEngine;

namespace RoguePirates.V4.Events
{
    /// <summary>
    /// Publishes the run's facts into CombatEvents, stamped with the V4 clock (M14; was LegacyEventFeed, which republished
    /// TriggerBus, RunManager, DamageReceiver and progression events). Producers call it at the moment the fact happens:
    /// the enemy simulation (hits), the kill fan-out (kills), the run and ship ports (run state, gold, player damage over the
    /// shared run manager and damage receiver), the player damage sink (steady damage) and the progression module.
    /// </summary>
    public sealed class RunEventPublisher
    {
        readonly CombatEvents _events;
        readonly RunClock _clock;
        Progression.ProgressionModule _levels;

        public RunEventPublisher(CombatEvents events, RunClock clock)
        {
            _events = events;
            _clock = clock;
        }

        void Publish(CombatEventKind kind, int a = 0, int b = 0, float x = 0f, float y = 0f, float z = 0f, string tag = null) =>
            _events.Publish(new CombatEvent(kind, _clock.TickIndex, _clock.SimTime, a, b, x, y, z, tag));

        /// <summary>Hit — A crit (1), X/Y position, Z damage, tag weapon.</summary>
        public void Hit(float damage, bool crit, Vector3 position, string weapon) => Publish(CombatEventKind.Hit, crit ? 1 : 0, 0, position.x, position.z, damage, weapon);

        /// <summary>Killed — A unit id, B flags (1 elite, 2 boss, 4 ordinary rewards), X/Y position, Z run-clock time, tag weapon.</summary>
        public void Killed(int unitId, int flags, Vector3 position, float runTime, string weapon) =>
            Publish(CombatEventKind.Killed, unitId, flags, position.x, position.z, runTime, weapon);

        public void RunState(int state) => Publish(CombatEventKind.RunState, state);
        public void GoldChanged(int gold) => Publish(CombatEventKind.GoldChanged, gold);

        /// <summary>PlayerDamaged — A crit (1), Z amount, tag the source (<see cref="PlayerDamageSource"/>; null = unknown).</summary>
        public void PlayerDamaged(float amount, bool crit, string source = null) => Publish(CombatEventKind.PlayerDamaged, crit ? 1 : 0, 0, 0f, 0f, amount, source);

        /// <summary>Steady damage (storm, R-57) bypasses the damage receiver's event; B = 1 marks it.</summary>
        public void SteadyDamage(float amount) => Publish(CombatEventKind.PlayerDamaged, 0, 1, 0f, 0f, amount, "storm"); // Playtest (6 Oct, owner): tagged for the session recorder

        /// <summary>Level-ups and offers from the V4 progression module.</summary>
        public void AttachProgression(Progression.ProgressionModule levels)
        {
            if (_levels != null || levels == null) return;
            _levels = levels;
            _levels.LevelChanged += OnLevel;
            _levels.OffersReady += OnOffers;
        }

        public void Detach()
        {
            if (_levels == null) return;
            _levels.LevelChanged -= OnLevel;
            _levels.OffersReady -= OnOffers;
            _levels = null;
        }

        void OnLevel(int level) => Publish(CombatEventKind.LevelUp, level);
        void OnOffers() => Publish(CombatEventKind.OffersShown, _levels != null ? _levels.Level : 0);
    }
}
