using System;
using HighSeas.Core;

namespace RoguePirates.V4.Stage
{
    /// <summary>
    /// What stage content reports outside itself (M14.12; was LegacyStageRewards): the home seed lock after a boss victory
    /// (the shared home-seed store, passed in by the composition root) and the telemetry records. Chest openings are the
    /// ChestOpened event; nothing else listens.
    /// </summary>
    public sealed class StageRewards : IStageRewards
    {
        /// <summary>Locks the run's seed as the home seed (V3 V3HomeSeed.TryLockFromRun).</summary>
        public Action<int> LockSeed;
        public RoguePirates.V4.Telemetry.RunTelemetry Telemetry;

        public void LockHomeSeed(int runSeed) => LockSeed?.Invoke(runSeed);
        public void ChestOpened() { }
        public void PoiUsedTelemetry(POIType type) => Telemetry?.RecordPoiInteraction(type.ToString());
        public void BossSummonedTelemetry(int encounterId) => Telemetry?.RecordBossSummoned(encounterId);
        public void BossDefeatedTelemetry(int encounterId) => Telemetry?.RecordBossDefeated(encounterId);
    }
}
