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
    /// <summary>The V4 spawn controller's enemy sink over the legacy enemy world (M3–M7).</summary>
    [DeletedInPhase(8, "V4 Enemy World")]
    public sealed class LegacyEnemySpawnSink : IEnemySpawnSink
    {
        EnemyWorld _world;

        EnemyWorld World
        {
            get
            {
                if (_world == null) _world = Object.FindFirstObjectByType<EnemyWorld>();
                return _world;
            }
        }

        EnemyDefinition[] Definitions => World != null ? World.Definitions : null;

        public int ActiveCount => World != null ? World.ActiveCount : 0;

        public int FindDefinition(string enemyId)
        {
            var defs = Definitions;
            if (defs == null || string.IsNullOrEmpty(enemyId)) return -1;
            for (var i = 0; i < defs.Length; i++) if (defs[i] != null && defs[i].id == enemyId) return i;
            return -1;
        }

        /// <summary>EncounterSpawner.FindDefinitionIndex: same object, or same id.</summary>
        public int IndexOf(EnemyDefinition definition)
        {
            var defs = Definitions;
            if (defs == null || definition == null) return -1;
            for (var i = 0; i < defs.Length; i++) if (defs[i] == definition || defs[i]?.id == definition.id) return i;
            return -1;
        }

        EnemyDefinition At(int index)
        {
            var defs = Definitions;
            return defs != null && index >= 0 && index < defs.Length ? defs[index] : null;
        }

        public string DefinitionId(int index) => At(index)?.id;
        public bool DefinitionIsBoss(int index) => At(index) != null && At(index).isBoss;
        public float DefinitionRadius(int index) => At(index) != null ? At(index).radius : 1f;
        public float DefinitionHeightOffset(int index) => At(index) != null ? At(index).visualHeightOffset : 0f;

        public int SpawnFromProfile(int definitionIndex, Vector3 position, SpawnProfile profile, int profileInstanceId,
            FormationRole role, int formationSlot, int leaderUnitId, float hpMultiplier, float damageMultiplier) =>
            World != null ? World.SpawnFromProfile(definitionIndex, position, profile, profileInstanceId, role, formationSlot, leaderUnitId, hpMultiplier, damageMultiplier) : -1;

        public int Spawn(int definitionIndex, Vector3 position, ConfigEnums.EliteAffix affix, float speedMultiplier, float hpMultiplier, float damageMultiplier) =>
            World != null ? World.Spawn(definitionIndex, position, (EliteAffix)(int)affix, speedMultiplier, hpMultiplier, damageMultiplier) : -1;

        public int UnitIdAt(int slot) => World != null && slot >= 0 && slot < World.Agents.Length ? World.Agents[slot].id : -1;

        public bool IsUnitActive(int unitId) => unitId > 0 && World != null && World.TryGetAgentByUnitId(unitId, out _);

        public int MaxActiveUnitId()
        {
            if (World == null) return 0;
            var max = 0;
            foreach (var a in World.Agents) if (a.active && a.id > max) max = a.id;
            return max;
        }

        public int NewestUnitAfter(int afterUnitId, int definitionIndex)
        {
            if (World == null || definitionIndex < 0) return -1;
            var best = -1;
            foreach (var a in World.Agents)
                if (a.active && a.definitionIndex == definitionIndex && a.id > afterUnitId && a.id > best) best = a.id;
            return best;
        }

        public int CountActive(int definitionIndex)
        {
            if (World == null || definitionIndex < 0) return 0;
            var count = 0;
            foreach (var a in World.Agents) if (a.active && a.definitionIndex == definitionIndex) count++;
            return count;
        }

        public int CountActiveFromProfile(SpawnProfile profile)
        {
            if (World == null || profile == null) return 0;
            var count = 0;
            foreach (var a in World.Agents) if (a.active && a.spawnProfile == profile) count++;
            return count;
        }

        public float ThreatOfNewestAfter(int afterUnitId)
        {
            if (World == null) return 0f;
            var best = -1;
            var threat = 0f;
            foreach (var a in World.Agents)
                if (a.active && a.id > afterUnitId && a.id > best) { best = a.id; threat = At(a.definitionIndex)?.threatCost ?? 0; }
            return threat;
        }

        public void SetExperimentMultipliers(float hpMultiplier, float damageMultiplier) => World?.SetExperimentMultipliers(hpMultiplier, damageMultiplier);
    }

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
        public int PlayerLevel => HighSeas.UI.LevelSystem.Instance != null ? HighSeas.UI.LevelSystem.Instance.Level : 0;
        public int Kills => Run != null ? Run.KillCount : 0;
    }

    /// <summary>Toasts, run telemetry, scheduled-elite Marks and the ambient spawn telegraph (legacy until M6/M12).</summary>
    [DeletedInPhase(12, "Presenters (M6) and V4 rewards (M12)")]
    public sealed class LegacySpawnFeedback : ISpawnFeedback
    {
        EnemyWorld _world;

        public void Toast(string text, float seconds)
        {
            if (!string.IsNullOrEmpty(text)) V3ToastHud.Instance?.Show(text, seconds);
        }

        public void Telemetry(string eventName, Dictionary<string, object> fields = null) => RunCombatTelemetry.Instance?.Record(eventName, fields);

        public void RegisterScheduledElite(int unitId, int scheduledEliteIndex) => RunMarksTracker.RegisterScheduledEliteAgent(unitId, scheduledEliteIndex);

        public void AmbientSpawnTelegraph(Vector3 position, int definitionIndex)
        {
            if (_world == null) _world = Object.FindFirstObjectByType<EnemyWorld>();
            var defs = _world != null ? _world.Definitions : null;
            if (defs == null || definitionIndex < 0 || definitionIndex >= defs.Length) return;
            V3SpawnTelegraph.NotifyAmbientSpawn(position, defs[definitionIndex]);
        }
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
    [DeletedInPhase(8, "PlayerDamageService")]
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
        public void Damage(float amount, Vector3 source) => Ship?.Damage?.TakeDamage(amount, source, false);

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
            LegacyRewardsAdapter.RecordDamage(amount);
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
            if (V3CaptainActiveController.Instance != null) amount *= V3CaptainActiveController.Instance.IncomingDamageMultiplier;
            if (V3RunItemEffects.Instance != null) amount *= V3RunItemEffects.Instance.GetIncomingDamageMultiplier(Ship);
            if (amount <= 0f) return;
            if (amount < receiver.CurrentHP)
            {
                receiver.Heal(-amount);
                V3RunItemEffects.Instance?.NotifyPlayerTookDamage(); // per frame: it only resets item timers
                _steadyPending += amount;
                ReportSteady(false);
                return;
            }
            _steadyPending += receiver.CurrentHP - 0.001f;
            ReportSteady(true);
            receiver.Heal(-(receiver.CurrentHP - 0.001f));
            receiver.TakeDamage(1f, source, false); // retried next frame if the ship is inside its hit immunity
        }
    }

    /// <summary>
    /// The legacy EncounterSpawner kept in the scene only for the Beacon boss and Powder Cache (register D4): it gets
    /// a schedule with no ambient entries, and its spawn-rate base is read back because Dead Calm writes it.
    /// </summary>
    [DeletedInPhase(9, "Stage content (M7) and the V4 powerup module (M9)")]
    public sealed class LegacyEncounterExecutor : ILegacySpawnRateSource
    {
        EncounterSpawner _spawner;

        EncounterSpawner Spawner
        {
            get
            {
                if (_spawner == null) _spawner = Object.FindFirstObjectByType<EncounterSpawner>();
                return _spawner;
            }
        }

        public bool InstallSchedule(EncounterSpawnSchedule encounterOnly)
        {
            if (Spawner == null || encounterOnly == null) return false;
            Spawner.SetSchedule(encounterOnly);
            return true;
        }

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

        public void EnterModuleLab() => Spawner?.EnterManualPlaytestMode();

        /// <summary>The kept spawner's rate multiplier without its boss-fight factor: 1, or Dead Calm's slow.</summary>
        public float Multiplier
        {
            get
            {
                if (Spawner == null) return 1f;
                var run = RunManager.Instance;
                var bossFight = run != null && run.State == RunState.BossFight && StageBalanceProfileContext.Export?.Spawn != null;
                return WithoutBossFactor(Spawner.DebugSpawnRateMultiplier, bossFight, StageBalanceProfileContext.Export?.Spawn?.BossAmbientRateMultiplier ?? 1f);
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
    [DeletedInPhase(8, "V4 owns the frame once the enemy world is V4")]
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
