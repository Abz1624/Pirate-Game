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
    /// native V4 producers exist. Only subscribes (and, for the beacon, boss and landmarks, polls once a frame); never changes
    /// legacy state. OfferChosen and PlayerHealed have no legacy signal to feed from; their native producers arrive
    /// with the progression module (M9) and the player damage service (M8).
    /// </summary>
    [DeletedInPhase(14, "Every event has a native V4 producer after switch-over")]
    public sealed class LegacyEventFeed
    {
        readonly CombatEvents _events;
        readonly RunClock _clock;
        TriggerBus _bus;
        RunManager _run;
        PlayerShipController _player;
        LevelSystem _levels;
        POIManager _poi;
        int _nextPoiLookupFrame;
        int _beaconState = -1;
        bool _bossSummoned, _bossDefeated;
        HighSeas.World.StageLayout _landmarkLayout;
        bool[] _landmarkUsed;

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
                _bus.ChestOpened += OnChest;
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
            AttachLevels();
        }

        /// <summary>LevelSystem may be created during bootstrap; call again after it exists.</summary>
        public void AttachLevels()
        {
            if (_levels != null || LevelSystem.Instance == null) return;
            _levels = LevelSystem.Instance;
            _levels.LevelChanged += OnLevel;
            _levels.OffersReady += OnOffers;
        }

        /// <summary>
        /// Beacon reveal and boss state have no legacy events (V3's HUD polled them), so they are compared once a frame:
        /// BeaconState (A = reveal state: 0 hidden, 1 sector hint, 2 exact marker, 3 discovered) and BossState
        /// (A = 1 summoned, 2 defeated).
        /// </summary>
        public void Poll()
        {
            var discovery = V3DiscoverySystem.Instance;
            var beacon = discovery != null ? (int)discovery.BeaconState : 0;
            if (beacon != _beaconState)
            {
                // The first poll only records the starting state (hidden) instead of reporting it as a change.
                if (_beaconState >= 0) Publish(CombatEventKind.BeaconState, beacon);
                _beaconState = beacon;
            }

            if (_poi == null && Time.frameCount >= _nextPoiLookupFrame)
            {
                _poi = Object.FindFirstObjectByType<POIManager>();
                if (_poi == null) _nextPoiLookupFrame = Time.frameCount + 30;
            }
            if (_poi != null)
            {
                if (_poi.BossSummoned && !_bossSummoned) { _bossSummoned = true; Publish(CombatEventKind.BossState, 1); }
                if (_poi.BossDefeated && !_bossDefeated) { _bossDefeated = true; Publish(CombatEventKind.BossState, 2); }
            }
            PollLandmarks();
        }

        /// <summary>
        /// LandmarkUsed (A = POI index, Tag = POI type): a landmark's layout slot turning used. Chests (ChestOpened) and the
        /// Beacon (BossState) have their own events. V3 has no event for landmarks; polled until the stage module (M7).
        /// </summary>
        void PollLandmarks()
        {
            var layout = V3RunContext.Layout;
            if (layout?.pois == null) return;
            if (!ReferenceEquals(layout, _landmarkLayout) || _landmarkUsed == null || _landmarkUsed.Length != layout.pois.Count)
            {
                // A new layout (run start): record its state without reporting it.
                _landmarkLayout = layout;
                _landmarkUsed = new bool[layout.pois.Count];
                for (var i = 0; i < layout.pois.Count; i++) _landmarkUsed[i] = layout.pois[i].used;
                return;
            }
            for (var i = 0; i < layout.pois.Count; i++)
            {
                var poi = layout.pois[i];
                if (!poi.used || _landmarkUsed[i]) continue;
                _landmarkUsed[i] = true;
                if (poi.type == POIType.SalvageChest || poi.type == POIType.BossBeacon) continue;
                Publish(CombatEventKind.LandmarkUsed, i, 0, poi.position.x, poi.position.z, 0f, poi.type.ToString());
            }
        }

        public void Detach()
        {
            if (_bus != null) { _bus.Hit -= OnHit; _bus.Kill -= OnKill; _bus.ChestOpened -= OnChest; _bus = null; }
            if (_run != null) { _run.StateChanged -= OnRunState; _run.GoldChanged -= OnGold; _run = null; }
            if (_player != null && _player.Damage != null) _player.Damage.Damaged -= OnPlayerDamaged;
            _player = null;
            if (_levels != null) { _levels.LevelChanged -= OnLevel; _levels.OffersReady -= OnOffers; _levels = null; }
        }

        void Publish(CombatEventKind kind, int a = 0, int b = 0, float x = 0f, float y = 0f, float z = 0f, string tag = null) =>
            _events.Publish(new CombatEvent(kind, _clock.TickIndex, Now, a, b, x, y, z, tag));

        void OnHit(float damage, bool crit, Vector3 pos, string weapon) =>
            Publish(CombatEventKind.Hit, crit ? 1 : 0, 0, pos.x, pos.z, damage, weapon);

        void OnKill(bool elite, bool boss, Vector3 pos, string weapon) =>
            Publish(CombatEventKind.Killed, elite ? 1 : 0, boss ? 1 : 0, pos.x, pos.z, 0f, weapon);

        void OnChest() => Publish(CombatEventKind.ChestOpened);

        void OnRunState(RunState state) => Publish(CombatEventKind.RunState, (int)state);

        void OnGold(int gold) => Publish(CombatEventKind.GoldChanged, gold);

        void OnPlayerDamaged(float amount, bool crit) => Publish(CombatEventKind.PlayerDamaged, crit ? 1 : 0, 0, 0f, 0f, amount);

        /// <summary>Steady damage (storm, R-57) bypasses DamageReceiver.Damaged; B = 1 marks it.</summary>
        public void OnSteadyDamage(float amount) => Publish(CombatEventKind.PlayerDamaged, 0, 1, 0f, 0f, amount);

        void OnLevel(int level) => Publish(CombatEventKind.LevelUp, level);

        void OnOffers() => Publish(CombatEventKind.OffersShown, _levels != null ? _levels.Level : 0);
    }
}
