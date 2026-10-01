using System;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Enemies;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Foundation.PlayerSystems;
using RoguePirates.V4.Foundation.World;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Spawning;
using RoguePirates.V4.Stage;
using RoguePirates.V4.Views;
using RoguePirates.V4.Weapons;
using UnityEngine;
using RoguePirates.V4.Foundation.Progression;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Ports
{
    // M14: the only V4 classes that touch the shared infrastructure the plan keeps until M15 (plan §4.2: RunManager and
    // its RunRng streams, PlayerShipController / DamageReceiver, SpawnPlacementService). They replace nine small adapters
    // (LegacyRunState, LegacyStageRun, LegacyCombatRandom, LegacyEnemyRunState, LegacyRunStatusView, LegacyEnemyTarget,
    // LegacyWeaponShip, LegacyStageShip, LegacyShipView, LegacySpawnPlacement) member for member. M15 moves that shared
    // code into V4 and deletes these ports.

    /// <summary>The run as the kept RunManager holds it: clock, state, pause, gold, kills and the seeded streams.</summary>
    public sealed class RunPort : IRunRandom, IRunStatus, IStageRun, ICombatRandom, IEnemyRunState, IRunStatusView, IStageRandom, IRunLifecycleHost
    {
        static RunManager Run => RunManager.Instance;

        /// <summary>The V4 progression's level (V3 LevelSystem.Level).</summary>
        public Func<int> Level;
        /// <summary>From the V4 Rum settlement (V3's flag was set by FinalizeRunOnDeath, which V4 no longer calls).</summary>
        public Func<bool> EndedByDeathSource;
        /// <summary>The stage deadline (overtime starts strictly after it), from the run's wave timeline config.</summary>
        public Func<WaveTimelineConfig> Timeline;

        // ---- streams ----
        public float DirectorFloat() => Run.Rng.DirectorFloat();
        public int DirectorInt(int minInclusive, int maxExclusive) => Run.Rng.DirectorInt(minInclusive, maxExclusive);
        public float LootFloat() => Run.Rng.LootFloat();
        public float CombatFloat() => Run.Rng.CombatFloat();
        public int LootInt(int minInclusive, int maxExclusive) => Run.Rng.LootInt(minInclusive, maxExclusive);
        public int MapInt(int minInclusive, int maxExclusive) => Run.Rng.MapInt(minInclusive, maxExclusive);

        // ---- clock and state ----
        public bool Exists => Run != null;
        public float ElapsedSeconds => Run != null ? Run.ElapsedSeconds : 0f;
        public bool IsPaused => Run != null && Run.IsPaused;
        public bool IsRunEnded => Run != null && Run.IsRunEnded;
        public bool IsGameplayActive => Run != null && (Run.State == RunState.Playing || Run.State == RunState.BossFight);
        public bool IsBossFight => Run != null && Run.State == RunState.BossFight;
        public int PlayerLevel => Level != null ? Level() : 0;
        public int Kills => Run != null ? Run.KillCount : 0;
        public PauseReason PauseReason => Run != null ? Run.PauseReason : PauseReason.None;
        public RunState State => Run != null ? Run.State : RunState.StageIntro;
        public int Gold => Run != null ? Run.Gold : 0;
        public int GrossGoldEarned => Run != null ? Run.GrossGoldEarned : 0;
        public int GrossGoldSpent => Run != null ? Run.GrossGoldSpent : 0;
        public string RunId => Run != null ? Run.RunId : string.Empty;
        public int RunSeed => Run != null ? Run.RunSeed : 0;
        public float MaxHull => Run != null ? Run.Stats.MaxHull : 0f;
        public float Luck => Run != null ? Run.Stats.Luck : 0f;
        public int Tier => Run != null ? Run.Tier : 1;
        public int StageIndex => Run != null ? Run.StageIndex : 0;
        /// <summary>The run enemy model scale (0.88) while a run is active, 1 elsewhere (was V3RunContext's).</summary>
        public float EnemyVisualScale => ActiveRun.IsActive ? ActiveRun.EnemyVisualScale : 1f;

        // ---- run status view ----
        /// <summary>Strictly after the stage deadline, as V3's RunManager.IsOvertime (the deadline is config-owned since M1).</summary>
        public bool IsOvertime => Run != null && (Timeline != null && Timeline() != null
            ? OvertimePhase.IsOvertime(Timeline(), Run.ElapsedSeconds)
            : Run.IsOvertime);
        public bool IsRunComplete => Run != null && Run.State == RunState.RunComplete;
        public bool ObjectiveSecured => Run != null && Run.ObjectiveSecured;
        public bool EndedByDeath => EndedByDeathSource != null && EndedByDeathSource();
        /// <summary>V3's weapon/enemy playtest scene flag; never set in V4 scenes.</summary>
        public bool WeaponPlaytestActive => false;

        // ---- commands ----
        public void SetState(RunState state) => Run?.SetState(state);
        public void SetPause(PauseReason reason) => Run?.SetPause(reason);
        public void AddGold(int amount) => Run?.AddGold(amount);
        public bool TrySpendGold(int amount) => Run != null && Run.TrySpendGold(amount);
        public void SecureObjective() => Run?.SecureObjective();
        /// <summary>The shared first-conquest key (ContentUnlocks is on the M15 audit list).</summary>
        public void MarkFirstConquestComplete() => PlayerPrefs.SetInt(RoguePirates.V4.Foundation.Progression.ContentUnlocks.FirstConquestKey, 1);
        /// <summary>The run's kill count (the kill fan-out's first step).</summary>
        public void RegisterKill(bool isBoss) => Run?.RegisterKill(isBoss);
        /// <summary>M12: V3's CompleteRun paid Doubloons; V4 secures the objective and ends the run. Rum is paid only at death
        /// (playtest round 1), so a victory that ends the run (continue-after-boss off, not the default) pays nothing.</summary>
        public void CompleteRun(bool victory)
        {
            if (Run == null) return;
            if (victory) Run.SecureObjective();
            Run.SetState(RunState.RunComplete);
        }
        public void AddStatModifier(int stat, float add, float multiply) => Run?.Stats.AddModifier(new StatModifier((StatId)stat, add, multiply));

        // ---- events (M14: was LegacyEventFeed's RunManager half) ----
        RunManager _attached;
        RoguePirates.V4.Events.RunEventPublisher _publisher;

        /// <summary>Publishes the run's state and gold changes; the current state at once (the run entered Playing first).</summary>
        public void AttachEvents(RoguePirates.V4.Events.RunEventPublisher publisher)
        {
            DetachEvents();
            _publisher = publisher;
            _attached = Run;
            if (_attached == null || _publisher == null) return;
            _attached.StateChanged += OnRunState;
            _attached.GoldChanged += OnGold;
            OnRunState(_attached.State);
        }

        public void DetachEvents()
        {
            if (_attached != null) { _attached.StateChanged -= OnRunState; _attached.GoldChanged -= OnGold; }
            _attached = null;
        }

        /// <summary>M14.11: the run manager's state changes, after the RunState event is published (results telemetry reads it).</summary>
        public event Action<RunState> StateChanged;

        void OnRunState(RunState state)
        {
            _publisher?.RunState((int)state);
            StateChanged?.Invoke(state);
        }
        void OnGold(int gold) => _publisher?.GoldChanged(gold);
    }

    /// <summary>The player ship as the kept PlayerShipController / DamageReceiver hold it, for every V4 reader.</summary>
    public sealed class ShipPort : IEnemyTarget, IHostileShotTarget, IShipState, IStageShip, IShipView, IPlayerHull
    {
        readonly Func<PlayerShipController> _find;
        PlayerShipController _ship;
        ShipBoostController _boost;
        int _nextLookupFrame;

        public ShipPort(PlayerShipController ship) { _ship = ship; }
        /// <summary>The run's ship, looked up when first needed (the bootstrap finds it); falls back to a scene search at most
        /// every 30 frames while missing (R-63).</summary>
        public ShipPort(Func<PlayerShipController> find) { _find = find; }

        public PlayerShipController Ship
        {
            get
            {
                if (_ship != null) return _ship;
                _ship = _find?.Invoke();
                if (_ship == null && _find != null && Time.frameCount >= _nextLookupFrame)
                {
                    _ship = Object.FindFirstObjectByType<PlayerShipController>();
                    if (_ship == null) _nextLookupFrame = Time.frameCount + 30;
                }
                return _ship;
            }
        }

        /// <summary>M9: the build's incoming-damage multiplier (V3's DamageReceiver read the Captain and item singletons).</summary>
        public Func<float> IncomingDamage;

        public bool Exists => Ship != null;
        public Vector3 Position => Ship != null ? Ship.Position : Vector3.zero;
        /// <summary>The ship's velocity (weapons and views read it as is).</summary>
        public Vector3 Velocity => Ship != null ? Ship.Velocity : Vector3.zero;
        /// <summary>Enemies read the horizontal velocity (y = 0).</summary>
        Vector3 IEnemyTarget.Velocity
        {
            get
            {
                if (Ship == null) return Vector3.zero;
                var v = Ship.Velocity;
                v.y = 0f;
                return v;
            }
        }
        public Vector3 MovementForward => Ship != null ? Ship.MovementForward : Vector3.forward;
        public Vector3 TransformPoint(Vector3 local) => Ship.transform.TransformPoint(local);

        public float ContactRadius => Ship != null ? Ship.IslandCollisionRadius : 1.15f;
        public bool HasDamageReceiver => Ship != null && Ship.Damage != null;
        public bool IsAlive => HasDamageReceiver && Ship.Damage.IsAlive;
        public Vector3 HullPosition => HasDamageReceiver ? Ship.Damage.transform.position : Position;
        public float Hp => HasDamageReceiver ? Ship.Damage.CurrentHP : 0f;
        public float MaxHp => HasDamageReceiver ? Ship.Damage.MaxHP : 0f;
        public float CurrentHp => Hp;

        public void TakeDamage(float amount, Vector3 source, bool isCrit)
        {
            if (HasDamageReceiver) Ship.Damage.TakeDamage(amount * (IncomingDamage?.Invoke() ?? 1f), source, isCrit);
        }
        public void ApplySlow(float speedMultiplier, float seconds) { if (Ship != null) Ship.ApplySlow(speedMultiplier, seconds); }
        public void Heal(float amount) { if (HasDamageReceiver) Ship.Damage.Heal(amount); }

        ShipBoostController Boost
        {
            get
            {
                if (_boost == null && Ship != null) _boost = Ship.GetComponent<ShipBoostController>();
                return _boost;
            }
        }
        public float BoostNormalized => Boost != null ? Boost.BoostNormalized : 0f;
        public bool IsBoosting => Boost != null && Boost.IsBoosting;
        public Transform Anchor => Ship != null ? Ship.transform : null;
        public Transform VisualModel => Ship == null ? null : Ship.VisualModel != null ? Ship.VisualModel : Ship.transform;
        public void RequestBoost() => Boost?.RequestBoost();

        // ---- IPlayerHull (M14.14: the player damage service's hull, was LegacyPlayerDamageSink's direct receiver use) ----
        static readonly System.Reflection.FieldInfo InvulnerableField =
            typeof(RoguePirates.V4.Foundation.Combat.DamageReceiver).GetField("_playtestInvulnerable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        public bool HullExists => HasDamageReceiver;
        public bool HullAlive => IsAlive;
        Vector3 IPlayerHull.HullPosition => Ship != null ? Ship.transform.position : Vector3.zero;
        public float HullHp => Hp;
        public bool HullInvulnerable => HasDamageReceiver && InvulnerableField != null && (bool)InvulnerableField.GetValue(Ship.Damage);
        public void HitHull(float amount, Vector3 source) { if (HasDamageReceiver) Ship.Damage.TakeDamage(amount, source, false); }
        public void DrainHull(float amount) { if (HasDamageReceiver) Ship.Damage.Heal(-amount); }
        public void KnockHull(float amount, Vector3 source) { if (HasDamageReceiver) Ship.Damage.TakeImpact(amount, source); }
        public void SetMovementEnabled(bool enabled) { if (Ship != null) Ship.SetMovementEnabled(enabled); }

        // ---- events (M14: was LegacyEventFeed's damage receiver half) ----
        RoguePirates.V4.Foundation.Combat.DamageReceiver _attachedDamage;
        RoguePirates.V4.Events.RunEventPublisher _publisher;

        public void AttachEvents(RoguePirates.V4.Events.RunEventPublisher publisher)
        {
            DetachEvents();
            _publisher = publisher;
            _attachedDamage = Ship != null ? Ship.Damage : null;
            if (_attachedDamage != null && _publisher != null) _attachedDamage.Damaged += OnDamaged;
        }

        public void DetachEvents()
        {
            if (_attachedDamage != null) _attachedDamage.Damaged -= OnDamaged;
            _attachedDamage = null;
        }

        void OnDamaged(float amount, bool crit) => _publisher?.PlayerDamaged(amount, crit);
    }

    /// <summary>
    /// The storm's safe region and the play area through the shared PlayBounds (M14.9b; was LegacyStormArena, which also
    /// switched V3's storm ring visual on and off). The ring is now a V4 presenter reading <see cref="IWorldView"/>.
    /// </summary>
    public sealed class PlayBoundsPort : IStormArena, IWorldView
    {
        public bool StormRingVisible { get; private set; }

        public void ResetForRun()
        {
            PlayBounds.ResetStormSafeRegion();
            StormRingVisible = false;
        }

        public void SetSafeScale(float scale) => PlayBounds.SetProfileStormSafeScale(scale);
        public void ShowRing() => StormRingVisible = true;
        public void RefreshRing() { } // the presenter redraws from the safe rectangle each frame
        public bool IsInsideSafeZone(Vector3 position) => PlayBounds.IsInsideStormSafeZone(position);
        public float Depth(Vector3 position) => PlayBounds.GetStormDepth(position);
        public bool IsInsideBounds(Vector3 position) => PlayBounds.IsInside(position);
        public float BoundsDepth(Vector3 position)
        {
            var local = position - PlayBounds.Center;
            return Mathf.Max(Mathf.Abs(local.x) - PlayBounds.HalfExtentX, Mathf.Abs(local.z) - PlayBounds.HalfExtentZ);
        }

        public Vector3 BoundsCenter => PlayBounds.Center;
        public float BoundsHalfX => PlayBounds.HalfExtentX;
        public float BoundsHalfZ => PlayBounds.HalfExtentZ;
        public void StormRingCorners(out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d) =>
            PlayBounds.GetStormRectCorners(PlayBounds.StormEdgeMargin, out a, out b, out c, out d);
    }

    /// <summary>Spawn placement through the shared placement service, the ship's transform and the main camera.</summary>
    public sealed class SpawnPlacementPort : ISpawnPlacement
    {
        readonly Func<PlayerShipController> _ship;
        Camera _camera;

        public SpawnPlacementPort(Func<PlayerShipController> ship) { _ship = ship; }

        Transform Player
        {
            get
            {
                var ship = _ship?.Invoke();
                if (ship == null) ship = Object.FindFirstObjectByType<PlayerShipController>();
                return ship != null ? ship.transform : null;
            }
        }

        /// <summary>EncounterSpawner.GetCamera: the main camera, re-fetched when it goes inactive.</summary>
        Camera Camera
        {
            get
            {
                if (_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
                return _camera;
            }
        }

        public bool Ready => Player != null && RunManager.Instance != null && RunManager.Instance.Rng != null;
        public Vector3 PlayerPosition => Player != null ? Player.position : Vector3.zero;

        public bool TryPick(SpawnPlacementSettings settings, float spawnRadius, bool allowNearPlayerFallback, out Vector3 position) =>
            SpawnPlacementService.TryPickSpawnPosition(Player, Camera, settings, spawnRadius, RunManager.Instance.Rng, allowNearPlayerFallback, out position);

        public void OffsetGroup(SpawnGroupLayout layout, int index, int count, float spacing, ref Vector3 position) =>
            SpawnPlacementService.OffsetGroupPosition(layout, index, count, spacing, Player, ref position);

        public bool IsFormationPositionValid(Vector3 position, float spawnRadius, SpawnPlacementSettings settings) =>
            SpawnPlacementService.IsFormationPositionValid(position, spawnRadius, Camera, settings);

        public float ViewEdgeDistance =>
            Camera != null && Player != null ? SpawnPlacementService.GetMinSpawnDistanceBeyondView(Camera, Player, 0.08f) + 1.5f : 0f;
    }
}
