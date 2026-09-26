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
        public ILegacySpawnRateSource LegacyRate;
        public ISpawnerProfiles Profiles;

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
            Feedback = context.Get<ISpawnFeedback>();
            Storm = context.Get<IStormArena>();
            Player = context.Get<IPlayerDamageSink>();
            Profiles = context.Get<ISpawnerProfiles>();
            context.TryGet(out LegacyRate);
        }

        public void Publish(CombatEventKind kind, int a = 0, int b = 0, Vector3 position = default, string tag = null, int flags = 0) =>
            Context.Events.Publish(new CombatEvent(kind, Context.Clock.TickIndex, Run.ElapsedSeconds, a, b, position.x, position.y, position.z, tag, flags));

        /// <summary>{label} and {lead} in toast templates.</summary>
        public static string Format(string template, TimelinePhase phase) =>
            string.IsNullOrEmpty(template) ? string.Empty
                : template.Replace("{label}", phase.label).Replace("{lead}", phase.warning.leadSeconds.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
    }
}
