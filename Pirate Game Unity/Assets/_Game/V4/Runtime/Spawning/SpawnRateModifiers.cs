using System;
using RoguePirates.V4.Config;

namespace RoguePirates.V4.Spawning
{
    /// <summary>
    /// Spawn-rate multipliers from other modules (M14; was LegacyEncounterExecutor): the powerup module's Dead Calm slow and
    /// Cursed Compass's spawn density (R-40). V3 multiplied its boss-fight factor into the spawner's rate and V4 divided it
    /// back out (the spawn controller applies its own); those float steps are kept so rates round exactly as before. The boss
    /// factor is the timeline's (was read back from the stage-profile export, which copied it).
    /// </summary>
    public sealed class SpawnRateModifiers : ISpawnRateSource
    {
        readonly IRunStatus _run;
        readonly Func<WaveTimelineConfig> _timeline;

        public SpawnRateModifiers(IRunStatus run, Func<WaveTimelineConfig> timeline)
        {
            _run = run;
            _timeline = timeline;
        }

        /// <summary>The powerup module's Dead Calm multiplier (1 when none).</summary>
        public Func<float> DeadCalm;
        /// <summary>R-40: Cursed Compass's spawn density (1 unless the item effect is on).</summary>
        public Func<float> SpawnDensity;

        public float Multiplier
        {
            get
            {
                if (DeadCalm == null) return SpawnDensity != null ? SpawnDensity() : 1f;
                var timeline = _timeline?.Invoke();
                var bossFight = _run != null && _run.IsBossFight && timeline != null;
                var boss = timeline != null ? timeline.bossAmbientRateMultiplier : 1f;
                var rate = DeadCalm();
                if (bossFight) rate *= boss;
                var density = SpawnDensity != null ? SpawnDensity() : 1f;
                return WithoutBossFactor(rate, bossFight, boss) * density;
            }
        }

        /// <summary>V3's spawner multiplied its boss-fight factor in (EncounterSpawner.GetEffectiveRateMultiplier); V4 applies its own.</summary>
        public static float WithoutBossFactor(float value, bool bossFight, float bossMultiplier) =>
            bossFight && bossMultiplier > 0f ? value / bossMultiplier : value;
    }
}
