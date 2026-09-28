using HighSeas.Core;
using HighSeas.Meta;
using HighSeas.Player;
using HighSeas.POI;
using HighSeas.UI;
using HighSeas.V3;
using RoguePirates.V4.Events;
using UnityEngine;

namespace RoguePirates.V4.Legacy
{
    /// <summary>
    /// Republishes legacy events (TriggerBus, RunManager, DamageReceiver, LevelSystem) into CombatEvents until the
    /// native V4 producers exist. Only subscribes; never changes legacy state. Chests, landmarks, the Beacon and the boss
    /// have native producers since M7 (the stage content module). OfferChosen and PlayerHealed have no legacy signal to
    /// feed from; their native producers arrive with the progression module (M9) and the player damage service (M8).
    /// </summary>
    [DeletedInPhase(14, "Every event has a native V4 producer after switch-over")]
    public sealed class LegacyEventFeed
    {
        readonly CombatEvents _events;
        readonly RunClock _clock;
        TriggerBus _bus;
        RunManager _run;
        PlayerShipController _player;
        RoguePirates.V4.Progression.ProgressionModule _levels;

        public LegacyEventFeed(CombatEvents events, RunClock clock)
        {
            _events = events;
            _clock = clock;
        }

        // Stamped with the V4 clock (tick index and sim time) so legacy and native events share one timeline.
        float Now => _clock.SimTime;

        public void Attach(PlayerShipController player)
        {
            Detach();
            _bus = TriggerBus.Instance;
            if (_bus != null)
            {
                _bus.Hit += OnHit;
                _bus.Kill += OnKill;
            }

            _run = RunManager.Instance;
            if (_run != null)
            {
                _run.StateChanged += OnRunState;
                _run.GoldChanged += OnGold;
                OnRunState(_run.State); // the feed attaches after EnterPlaying, so publish the state it missed
            }

            _player = player;
            if (_player != null && _player.Damage != null) _player.Damage.Damaged += OnPlayerDamaged;
        }

        /// <summary>M9: level-ups from the V4 progression module (was LevelSystem).</summary>
        public void AttachLevels(RoguePirates.V4.Progression.ProgressionModule levels)
        {
            if (_levels != null || levels == null) return;
            _levels = levels;
            _levels.LevelChanged += OnLevel;
            _levels.OffersReady += OnOffers;
        }

        public void Detach()
        {
            if (_bus != null) { _bus.Hit -= OnHit; _bus.Kill -= OnKill; _bus = null; }
            if (_run != null) { _run.StateChanged -= OnRunState; _run.GoldChanged -= OnGold; _run = null; }
            if (_player != null && _player.Damage != null) _player.Damage.Damaged -= OnPlayerDamaged;
            _player = null;
            if (_levels != null) { _levels.LevelChanged -= OnLevel; _levels.OffersReady -= OnOffers; _levels = null; }
        }

        void Publish(CombatEventKind kind, int a = 0, int b = 0, float x = 0f, float y = 0f, float z = 0f, string tag = null) =>
            _events.Publish(new CombatEvent(kind, _clock.TickIndex, Now, a, b, x, y, z, tag));

        void OnHit(float damage, bool crit, Vector3 pos, string weapon) =>
            Publish(CombatEventKind.Hit, crit ? 1 : 0, 0, pos.x, pos.z, damage, weapon);

        // M12: kills are published by the V4 kill fan-out (unit id and run-clock time); TriggerBus kills are not republished.
        void OnKill(bool elite, bool boss, Vector3 pos, string weapon) { }

        /// <summary>M12: Killed — A unit id, B flags (1 elite, 2 boss, 4 ordinary rewards), X/Y position, Z run-clock time, tag weapon.</summary>
        public void OnEnemyKilled(int unitId, int flags, Vector3 pos, float runTime, string weapon) =>
            Publish(CombatEventKind.Killed, unitId, flags, pos.x, pos.z, runTime, weapon);

        void OnRunState(RunState state) => Publish(CombatEventKind.RunState, (int)state);

        void OnGold(int gold) => Publish(CombatEventKind.GoldChanged, gold);

        void OnPlayerDamaged(float amount, bool crit) => Publish(CombatEventKind.PlayerDamaged, crit ? 1 : 0, 0, 0f, 0f, amount);

        /// <summary>Steady damage (storm, R-57) bypasses DamageReceiver.Damaged; B = 1 marks it.</summary>
        public void OnSteadyDamage(float amount) => Publish(CombatEventKind.PlayerDamaged, 0, 1, 0f, 0f, amount);

        void OnLevel(int level) => Publish(CombatEventKind.LevelUp, level);

        void OnOffers() => Publish(CombatEventKind.OffersShown, _levels != null ? _levels.Level : 0);
    }
}
