using System;
using HighSeas.Core;
using RoguePirates.V4.Rewards;

namespace RoguePirates.V4.Telemetry
{
    /// <summary>
    /// "Results shown" in the run telemetry and the telemetry export file, at the run's RunComplete state change, before
    /// the death record that follows it (M14.11; was LegacyResultsTelemetry, which subscribed to the run manager itself).
    /// V3's results screen did this, twice (it had two screens, R-63); once here.
    /// </summary>
    public sealed class ResultsTelemetry
    {
        readonly RunTelemetry _telemetry;
        readonly IRumView _rum;
        readonly Func<bool> _objectiveSecured;

        public ResultsTelemetry(RunTelemetry telemetry, IRumView rum, Func<bool> objectiveSecured)
        {
            _telemetry = telemetry;
            _rum = rum;
            _objectiveSecured = objectiveSecured;
        }

        public void OnRunState(RunState state)
        {
            if (state != RunState.RunComplete) return;
            _telemetry?.RecordRunResultsShown(_objectiveSecured != null && _objectiveSecured(), _rum != null ? _rum.SettledTotal : 0);
            _telemetry?.WriteExportFile();
        }
    }
}
