using System;
using System.Collections.Generic;
using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.Player;
using HighSeas.V3;
using HighSeas.World;
using RoguePirates.V4.Config;
using RoguePirates.V4.Spawning;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Legacy
{

    /// <summary>Spawn placement through the shared placement service, player transform and main camera.</summary>
    [DeletedInPhase(14, "V4 world module owns placement")]
    public sealed class LegacySpawnPlacement : ISpawnPlacement
    {
        Transform _player;
        Camera _camera;

        Transform Player
        {
            get
            {
                if (_player == null) _player = Object.FindFirstObjectByType<PlayerShipController>()?.transform;
                return _player;
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
    }

    /// <summary>The run's seeded streams (RunManager.Rng) and clock/state.</summary>
    [DeletedInPhase(14, "V4 run lifecycle owns the clock and RNG")]
    public sealed class LegacyRunState : IRunRandom, IRunStatus
    {
        static RunManager Run => RunManager.Instance;

        public float DirectorFloat() => Run.Rng.DirectorFloat();
        public int DirectorInt(int minInclusive, int maxExclusive) => Run.Rng.DirectorInt(minInclusive, maxExclusive);
        public float LootFloat() => Run.Rng.LootFloat();

        public bool Exists => Run != null;
        public float ElapsedSeconds => Run != null ? Run.ElapsedSeconds : 0f;
        public bool IsPaused => Run != null && Run.IsPaused;
        public bool IsRunEnded => Run != null && Run.IsRunEnded;
        public bool IsGameplayActive => Run != null && (Run.State == RunState.Playing || Run.State == RunState.BossFight);
        public bool IsBossFight => Run != null && Run.State == RunState.BossFight;
        /// <summary>M9: the V4 progression's level (V3 LevelSystem.Level).</summary>
        public Func<int> Level;
        public int PlayerLevel => Level != null ? Level() : 0;
        public int Kills => Run != null ? Run.KillCount : 0;
    }

    /// <summary>Toasts and run telemetry (legacy until M14; the spawn telegraph moved to a presenter in M6; scheduled-elite
    /// Rum listens to the focus PhaseChanged event since M12).</summary>
    [DeletedInPhase(14, "ToastPresenter and V4 telemetry")]
    public sealed class LegacySpawnFeedback : ISpawnFeedback
    {
        public void Toast(string text, float seconds)
        {
            if (!string.IsNullOrEmpty(text)) V3ToastHud.Instance?.Show(text, seconds);
        }

        public void Telemetry(string eventName, Dictionary<string, object> fields = null) => RunCombatTelemetry.Instance?.Record(eventName, fields);

    }

    /// <summary>The storm's safe region (shared play bounds) and the V3 storm ring visual.</summary>
    [DeletedInPhase(14, "V4 world module and presenters")]
    public sealed class LegacyStormArena : IStormArena
    {
        public void ResetForRun()
        {
            PlayBounds.ResetStormSafeRegion();
            V3SaltwindStormRingVisual.Disable();
        }

        public void SetSafeScale(float scale) => PlayBounds.SetProfileStormSafeScale(scale);
        public void ShowRing() => V3SaltwindStormRingVisual.Ensure();
        public void RefreshRing() => V3SaltwindStormRingVisual.Instance?.Refresh();
        public bool IsInsideSafeZone(Vector3 position) => PlayBounds.IsInsideStormSafeZone(position);
        public float Depth(Vector3 position) => PlayBounds.GetStormDepth(position);
    }

    /// <summary>Damage to the ship through its damage receiver (V4 PlayerDamageService from M8).</summary>
    [DeletedInPhase(14, "PlayerDamageService (captain and item damage multipliers move to V4 in M9)")]
    public sealed class LegacyPlayerDamageSink : IPlayerDamageSink
    {
        PlayerShipController _ship;

        PlayerShipController Ship
        {
            get
            {
                if (_ship == null) _ship = Object.FindFirstObjectByType<PlayerShipController>();
                return _ship;
            }
        }

        public bool Exists => Ship != null && Ship.Damage != null;
        public bool IsAlive => Exists && Ship.Damage.IsAlive;
        public Vector3 Position => Ship != null ? Ship.transform.position : Vector3.zero;
        /// <summary>
        /// M9: the build's incoming-damage multiplier (Brace!, Reinforced Keel) and the item hook for steady damage. V3's
        /// DamageReceiver read the Captain and item singletons; hits through TakeDamage reach the items via its Damaged event.
        /// </summary>
        public System.Func<float> IncomingDamage;
        public System.Action TookSteadyDamage;

        float Incoming => IncomingDamage?.Invoke() ?? 1f;

        public void Damage(float amount, Vector3 source) => Ship?.Damage?.TakeDamage(amount * Incoming, source, false);

        static readonly System.Reflection.FieldInfo InvulnerableField =
            typeof(DamageReceiver).GetField("_playtestInvulnerable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        /// <summary>Seconds between reports of accumulated steady damage (feedback, telemetry, the V4 event).</summary>
        public const float SteadyReportInterval = 0.5f;

        /// <summary>Accumulated steady damage, reported at most every <see cref="SteadyReportInterval"/> seconds.</summary>
        public event System.Action<float> SteadyDamageReported;

        float _steadyPending;
        float _steadyLastReport = float.NegativeInfinity;

        void ReportSteady(bool force)
        {
            if (_steadyPending <= 0f) return;
            if (!force && Time.time - _steadyLastReport < SteadyReportInterval) return;
            var amount = _steadyPending;
            _steadyPending = 0f;
            _steadyLastReport = Time.time;
            // TriggerBus drives damage numbers, the hit flash and on-damage relics: once per report, not every frame.
            LegacyRunLifecycle.RecordDamage(amount);
            SteadyDamageReported?.Invoke(amount);
        }

        /// <summary>
        /// Steady damage (storm, R-57): the same incoming multipliers as TakeDamage (Brace!, Keel), no hit immunity
        /// given or respected, so enemy hits are not blocked by it. HP drops every frame; feedback and telemetry get
        /// the summed amount every <see cref="SteadyReportInterval"/> s. A lethal tick goes through TakeDamage so
        /// death, events and telemetry run as usual.
        /// </summary>
        public void DamageOverTime(float amount, Vector3 source)
        {
            var receiver = Ship != null ? Ship.Damage : null;
            if (receiver == null || !receiver.IsAlive) return;
            if (InvulnerableField != null && (bool)InvulnerableField.GetValue(receiver)) return; // god mode, Module Lab
            amount *= Incoming;
            if (amount <= 0f) return;
            if (amount < receiver.CurrentHP)
            {
                receiver.Heal(-amount);
                TookSteadyDamage?.Invoke(); // per frame: it only resets item timers
                _steadyPending += amount;
                ReportSteady(false);
                return;
            }
            _steadyPending += receiver.CurrentHP - 0.001f;
            ReportSteady(true);
            receiver.Heal(-(receiver.CurrentHP - 0.001f));
            receiver.TakeDamage(Incoming, source, false); // retried next frame if the ship is inside its hit immunity
        }
    }

    /// <summary>
    /// Dead Calm's spawn-rate cut (M9: the V4 powerup module, which V3 wrote into its switched-off EncounterSpawner) and
    /// the run-start resets V3 did while resetting its spawn directors. The spawner left the V4 scenes in M9.
    /// </summary>
    [DeletedInPhase(14, "Static V3 run resets go with the V3 kill path's last targets")]
    public sealed class LegacyEncounterExecutor : ILegacySpawnRateSource
    {
        /// <summary>The powerup module's Dead Calm multiplier (1 when none).</summary>
        public Func<float> DeadCalm;
        /// <summary>R-40: Cursed Compass's spawn density (1 unless the item effect is on).</summary>
        public Func<float> SpawnDensity;

        /// <summary>
        /// Run-start resets V3 did while resetting its spawn directors: the static miniboss arena (it survives a scene
        /// reload, register R-68; V4 arenas are the spawn controller's, but the kill path still notifies this one) and
        /// the per-run spawn telemetry (EncounterSpawner.ResetForNewRun).
        /// </summary>
        public static void ResetForRun()
        {
            V3MinibossArena.ResetForRun();
            SaltwindSpawnTelemetryLogic.ResetForNewRun();
        }

        /// <summary>
        /// Dead Calm's slow, 1 when none. V3 multiplied its boss-fight factor into the spawner's rate and V4 divided it
        /// back out (V4 applies its own); the same float steps are kept so traces match V3.
        /// </summary>
        public float Multiplier
        {
            get
            {
                if (DeadCalm == null) return SpawnDensity != null ? SpawnDensity() : 1f;
                var run = RunManager.Instance;
                var bossFight = run != null && run.State == RunState.BossFight && StageBalanceProfileContext.Export?.Spawn != null;
                var boss = StageBalanceProfileContext.Export?.Spawn?.BossAmbientRateMultiplier ?? 1f;
                var rate = DeadCalm();
                if (bossFight) rate *= boss;
                var density = SpawnDensity != null ? SpawnDensity() : 1f;
                return WithoutBossFactor(rate, bossFight, boss) * density;
            }
        }

        /// <summary>The kept spawner multiplies its boss-fight factor in (EncounterSpawner.GetEffectiveRateMultiplier); V4 applies its own.</summary>
        public static float WithoutBossFactor(float value, bool bossFight, float bossMultiplier) =>
            bossFight && bossMultiplier > 0f ? value / bossMultiplier : value;
    }

    /// <summary>
    /// Creates the V4 spawn ticks at the frame positions of the V3 directors they replace (see LegacyUpdateSlot) and
    /// binds the scene's ambient slot.
    /// </summary>
    [DeletedInPhase(14, "V4 owns the frame once the run clock is V4")]
    public sealed class LegacySpawnSlots
    {
        public const string AmbientRole = "ambient";
        readonly Action<float> _ambient, _overtime, _storm, _beats, _stormEdge;

        public LegacySpawnSlots(Action<float> ambient, Action<float> overtime, Action<float> storm, Action<float> beats, Action<float> stormEdge)
        {
            _ambient = ambient;
            _overtime = overtime;
            _storm = storm;
            _beats = beats;
            _stormEdge = stormEdge;
        }

        /// <summary>The scene slot on /Systems right after EncounterSpawner (EncounterSpawner.Update's position).</summary>
        public void BindAmbient()
        {
            foreach (var slot in Object.FindObjectsByType<LegacyUpdateSlot>(FindObjectsSortMode.None))
                if (slot.role == AmbientRole) { slot.Tick = _ambient; return; }
            Debug.LogWarning("V4Game: no ambient LegacyUpdateSlot in the scene (run RoguePirates/V4/Setup M3); adding one, frame order differs from V3.");
            var host = GameObject.Find("Systems") ?? new GameObject("V4AmbientSlot");
            var added = host.AddComponent<LegacyUpdateSlot>();
            added.role = AmbientRole;
            added.Tick = _ambient;
        }

        /// <summary>Where V3 created V3OvertimeDirector (its own GameObject).</summary>
        public void CreateOvertimeSlot()
        {
            if (Find("overtime") != null) return;
            var slot = new GameObject("V4OvertimeSlot").AddComponent<LegacyUpdateSlot>();
            slot.role = "overtime";
            slot.Tick = _overtime;
        }

        /// <summary>Where V3 added V3SaltwindStormDirector to /Systems.</summary>
        public void AddStormSlot() => AddToSystems("storm", _storm);

        /// <summary>Where V3 added V3PressureBeatDirector to /Systems.</summary>
        public void AddBeatSlot() => AddToSystems("beats", _beats);

        /// <summary>Where V3 added V3StormEdgeDamage to the ship.</summary>
        public void AddStormEdgeSlot(PlayerShipController ship)
        {
            if (ship == null || Find("storm-edge") != null) return;
            var slot = ship.gameObject.AddComponent<LegacyUpdateSlot>();
            slot.role = "storm-edge";
            slot.Tick = _stormEdge;
        }

        void AddToSystems(string role, Action<float> tick)
        {
            if (Find(role) != null) return;
            var systems = GameObject.Find("Systems");
            var host = systems != null ? systems : new GameObject("V4" + role + "Slot");
            var slot = host.AddComponent<LegacyUpdateSlot>();
            slot.role = role;
            slot.Tick = tick;
        }

        static LegacyUpdateSlot Find(string role)
        {
            foreach (var slot in Object.FindObjectsByType<LegacyUpdateSlot>(FindObjectsSortMode.None))
                if (slot.role == role) return slot;
            return null;
        }
    }
}
