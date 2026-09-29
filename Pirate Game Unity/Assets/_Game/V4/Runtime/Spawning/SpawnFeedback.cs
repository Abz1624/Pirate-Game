using System.Collections.Generic;

namespace RoguePirates.V4.Spawning
{
    /// <summary>The spawn controller's toasts and telemetry records (M14; was LegacySpawnFeedback over V3's toast and telemetry).</summary>
    public sealed class SpawnFeedback : ISpawnFeedback
    {
        readonly Views.IToastSink _toasts;
        readonly Telemetry.RunTelemetry _telemetry;

        public SpawnFeedback(Views.IToastSink toasts, Telemetry.RunTelemetry telemetry)
        {
            _toasts = toasts;
            _telemetry = telemetry;
        }

        public void Toast(string text, float seconds)
        {
            if (!string.IsNullOrEmpty(text)) _toasts?.Show(text, seconds);
        }

        public void Telemetry(string eventName, Dictionary<string, object> fields = null) => _telemetry?.Record(eventName, fields);
    }
}
