using System.Collections.Generic;

namespace RoguePirates.Presentation.Telemetry
{
    public sealed class GameplayTelemetry
    {
        readonly List<Dictionary<string, object>> _buffer = new();
        readonly bool _enabled;

        public GameplayTelemetry(bool enabled = true) => _enabled = enabled;

        public void Record(string eventName, Dictionary<string, object> fields)
        {
            if (!_enabled) return;
            fields["event"] = eventName;
            fields["timestampUtc"] = System.DateTime.UtcNow.ToString("o");
            _buffer.Add(new Dictionary<string, object>(fields));
        }

        public IReadOnlyList<Dictionary<string, object>> FlushBuffer()
        {
            var copy = new List<Dictionary<string, object>>(_buffer);
            _buffer.Clear();
            return copy;
        }
    }
}
