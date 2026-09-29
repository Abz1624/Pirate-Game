using RoguePirates.V4.Config;
using RoguePirates.V4.Events;
using UnityEngine;

namespace RoguePirates.V4.Spawning
{
    /// <summary>The contracts and config the spawn controller's parts share (resolved once per run).</summary>
    internal sealed class SpawnServices
    {
        public RunContext Context;
        public IEnemySpawnSink Sink;
        public ISpawnPlacement Placement;
        public IRunRandom Random;
        public IRunStatus Run;
        public ISpawnFeedback Feedback;
        public IStormArena Storm;
        public IPlayerDamageSink Player;
        public ISpawnRateSource ExternalRate;
        public ISpawnerProfiles Profiles;
        /// <summary>Required encounters (M7; optional so earlier tests without it still run).</summary>
        public IEncounterTracker Encounters;
        /// <summary>M11: the adaptive levers (neutral unless the director is Adaptive). Only ambient spawns read them.</summary>
        public RoguePirates.V4.Difficulty.IDifficultyLevers Levers;

        public ConfigSetRuntime Config => Context?.Config;
        public WaveTimelineConfig Timeline => Config?.Single<WaveTimelineConfig>();
        public DifficultyCurveConfig Difficulty => Config?.Single<DifficultyCurveConfig>();
        public ExperimentConfig Experiment => Config?.Single<ExperimentConfig>();

        public void Resolve(RunContext context)
        {
            Context = context;
            Sink = context.Get<IEnemySpawnSink>();
            Placement = context.Get<ISpawnPlacement>();
            Random = context.Get<IRunRandom>();
            Run = context.Get<IRunStatus>();
            context.TryGet(out Levers);
            Feedback = context.Get<ISpawnFeedback>();
            Storm = context.Get<IStormArena>();
            Player = context.Get<IPlayerDamageSink>();
            Profiles = context.Get<ISpawnerProfiles>();
            context.TryGet(out ExternalRate);
            context.TryGet(out Encounters);
        }

        public void Publish(CombatEventKind kind, int a = 0, int b = 0, Vector3 position = default, string tag = null, int flags = 0) =>
            Context.Events.Publish(new CombatEvent(kind, Context.Clock.TickIndex, Run.ElapsedSeconds, a, b, position.x, position.y, position.z, tag, flags));

        /// <summary>{label} and {lead} in toast templates.</summary>
        public static string Format(string template, TimelinePhase phase) =>
            string.IsNullOrEmpty(template) ? string.Empty
                : template.Replace("{label}", phase.label).Replace("{lead}", phase.warning.leadSeconds.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
    }
}
