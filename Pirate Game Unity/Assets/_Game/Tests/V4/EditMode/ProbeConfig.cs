using System;
using System.Collections.Generic;
using RoguePirates.V4.Config;

namespace RoguePirates.V4.Tests
{
    // Test-only config type covering the field kinds the config core must handle.
    [Serializable] public struct ProbeRange { public float min; public float max; }
    [Serializable] public sealed class ProbeEntry { public string id = "a"; public float weight = 1f; public ProbeRange range; }
    public enum ProbeMode { Off, On }

    public sealed class ProbeConfig : V4ConfigAsset
    {
        public float rate = 0.6f;
        public int cap = 72;
        public bool enabled = true;
        public ProbeMode mode = ProbeMode.On;
        public ProbeRange window = new ProbeRange { min = 1f, max = 2f };
        public List<ProbeEntry> entries = new List<ProbeEntry> { new ProbeEntry(), new ProbeEntry { id = "b", weight = 2f } };
        public float[] shares = { 0.25f, 0.75f };
        public ProbeConfig other;
        [NonSerialized] public float ignored = 5f;

        public override void Validate(ConfigValidationReport report)
        {
            report.WeightsNotZero(this, "entries", entries.ConvertAll(e => e.weight));
            report.NotNegative(this, "rate", rate);
        }
    }
}
