using System.Collections.Generic;
using HighSeas.Enemies;
using RoguePirates.V4.Config;
using UnityEngine;

namespace RoguePirates.V4.Spawning
{
    /// <summary>
    /// Where V4 spawns go. M3: the legacy enemy world (spawns take a runtime legacy <see cref="SpawnProfile"/>, which
    /// the enemy world needs for movement, attack and elite overrides). M8: the V4 enemy world.
    /// </summary>
    public interface IEnemySpawnSink
    {
        int ActiveCount { get; }
        /// <summary>Definition index for an enemy id in the run database, or -1.</summary>
        int FindDefinition(string enemyId);
        /// <summary>Index of this exact definition, or of one with the same id (the legacy lookup), or -1.</summary>
        int IndexOf(HighSeas.Data.EnemyDefinition definition);
        string DefinitionId(int index);
        bool DefinitionIsBoss(int index);
        float DefinitionRadius(int index);
        float DefinitionHeightOffset(int index);
        int SpawnFromProfile(int definitionIndex, Vector3 position, SpawnProfile profile, int profileInstanceId,
            FormationRole role, int formationSlot, int leaderUnitId, float hpMultiplier, float damageMultiplier);
        int Spawn(int definitionIndex, Vector3 position, ConfigEnums.EliteAffix affix, float speedMultiplier, float hpMultiplier, float damageMultiplier);
        /// <summary>M11: the elite lever's spawn in <paramref name="slot"/> pays ordinary rewards if <paramref name="ordinaryRewards"/>, and belongs to <paramref name="sourceProfile"/>.</summary>
        void MarkAdaptiveElite(int slot, SpawnProfile sourceProfile, bool ordinaryRewards);
        /// <summary>Unit id of the enemy in a slot returned by a spawn call.</summary>
        int UnitIdAt(int slot);
        bool IsUnitActive(int unitId);
        int MaxActiveUnitId();
        /// <summary>Newest active unit with this definition and an id above <paramref name="afterUnitId"/>, or -1.</summary>
        int NewestUnitAfter(int afterUnitId, int definitionIndex);
        int CountActive(int definitionIndex);
        int CountActiveFromProfile(SpawnProfile profile);
        /// <summary>Threat cost of the newest active unit with an id above <paramref name="afterUnitId"/> (0 if none).</summary>
        float ThreatOfNewestAfter(int afterUnitId);
        void SetExperimentMultipliers(float hpMultiplier, float damageMultiplier);
    }

    /// <summary>Spawn positions: off-screen ring around the ship, group offsets and validity (islands, bounds, view).</summary>
    public interface ISpawnPlacement
    {
        bool Ready { get; }
        Vector3 PlayerPosition { get; }
        bool TryPick(SpawnPlacementSettings settings, float spawnRadius, bool allowNearPlayerFallback, out Vector3 position);
        void OffsetGroup(SpawnGroupLayout layout, int index, int count, float spacing, ref Vector3 position);
        bool IsFormationPositionValid(Vector3 position, float spawnRadius, SpawnPlacementSettings settings);
    }

    /// <summary>The run's seeded random streams (RunManager.Rng until V4 owns the RNG).</summary>
    public interface IRunRandom
    {
        float DirectorFloat();
        int DirectorInt(int minInclusive, int maxExclusive);
        float LootFloat();
    }

    /// <summary>Run clock and state as the legacy run manager keeps them (one source of truth until V4 owns the run).</summary>
    public interface IRunStatus
    {
        bool Exists { get; }
        float ElapsedSeconds { get; }
        bool IsPaused { get; }
        bool IsRunEnded { get; }
        /// <summary>Playing or boss fight.</summary>
        bool IsGameplayActive { get; }
        bool IsBossFight { get; }
        int PlayerLevel { get; }
        int Kills { get; }
    }

    /// <summary>
    /// Side effects that are still legacy presentation, telemetry or rewards (toasts, run telemetry records, scheduled
    /// elite Marks). Toasts move to presenters by M14 and the rest to V4 rewards in M12; the ambient spawn telegraph moved
    /// to the spawn warning presenter in M6 (Spawned events flagged ambient).
    /// </summary>
    public interface ISpawnFeedback
    {
        void Toast(string text, float seconds);
        void Telemetry(string eventName, Dictionary<string, object> fields = null);
    }

    /// <summary>The storm's safe region and its ring (shared play bounds + legacy ring visual until M14).</summary>
    public interface IStormArena
    {
        void ResetForRun();
        void SetSafeScale(float scale);
        void ShowRing();
        void RefreshRing();
        bool IsInsideSafeZone(Vector3 position);
        float Depth(Vector3 position);
    }

    /// <summary>Damage to the player ship (M3: the ship's damage receiver; M8: the V4 player damage service).</summary>
    public interface IPlayerDamageSink
    {
        bool Exists { get; }
        bool IsAlive { get; }
        Vector3 Position { get; }
        void Damage(float amount, Vector3 source);
        /// <summary>Damage over time (storm): applied steadily, without the ship's hit immunity (R-57).</summary>
        void DamageOverTime(float amount, Vector3 source);
    }

    /// <summary>Spawn-rate multipliers owned by systems that are still legacy (Dead Calm, register D4).</summary>
    public interface ILegacySpawnRateSource
    {
        float Multiplier { get; }
    }

    /// <summary>The runtime legacy spawn profile a spawner config drives (built by the M1 config bridge).</summary>
    public interface ISpawnerProfiles
    {
        SpawnProfile For(EnemySpawnerConfig spawner);
    }

    public enum PhaseState { Waiting, Warned, Active, Done, Skipped, External }

    /// <summary>One timeline phase as the console and telemetry see it.</summary>
    public readonly struct PhaseStatus
    {
        public readonly string PhaseId;
        public readonly string Label;
        public readonly PhaseState State;
        public readonly float StartedAt;

        public PhaseStatus(string phaseId, string label, PhaseState state, float startedAt)
        {
            PhaseId = phaseId;
            Label = label;
            State = state;
            StartedAt = startedAt;
        }
    }

    /// <summary>What the V4 spawn controller exposes to the console, the trace harness and (M7) stage content.</summary>
    public interface ISpawnController
    {
        IReadOnlyList<PhaseStatus> Phases { get; }
        /// <summary>Label of the most recently started phase, for the console header.</summary>
        string CurrentPhaseLabel { get; }
        bool ArenaActive { get; }
        int ArenaFocusUnitId { get; }
        float EffectiveRateMultiplier { get; }
        /// <summary>Starts a waiting phase now (its trigger is treated as met).</summary>
        bool TriggerNow(string phaseId);
        /// <summary>Marks a phase done without running it.</summary>
        bool Skip(string phaseId);
    }
}
