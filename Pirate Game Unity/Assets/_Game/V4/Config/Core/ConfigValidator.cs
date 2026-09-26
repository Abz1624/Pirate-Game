using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RoguePirates.V4.Config
{
    public sealed class ConfigValidationReport
    {
        public readonly struct Issue
        {
            public readonly string ConfigId;
            public readonly string Path;
            public readonly string Message;
            public readonly bool IsError;
            public Issue(string configId, string path, string message, bool isError) { ConfigId = configId; Path = path; Message = message; IsError = isError; }
            public override string ToString() => $"{(IsError ? "error" : "warning")} {ConfigId}{(string.IsNullOrEmpty(Path) ? "" : "." + Path)}: {Message}";
        }

        readonly List<Issue> _issues = new List<Issue>();
        public IReadOnlyList<Issue> Issues => _issues;
        public bool IsValid => _issues.All(i => !i.IsError);
        public IEnumerable<Issue> Errors => _issues.Where(i => i.IsError);

        public void Error(V4ConfigAsset config, string path, string message) => _issues.Add(new Issue(config != null ? config.configId : "?", path, message, true));
        public void Warning(V4ConfigAsset config, string path, string message) => _issues.Add(new Issue(config != null ? config.configId : "?", path, message, false));

        // Shared rule helpers for config types.
        public void NotNegative(V4ConfigAsset c, string path, float value) { if (value < 0f) Error(c, path, $"must not be negative ({value})"); }
        public void Positive(V4ConfigAsset c, string path, float value) { if (!(value > 0f)) Error(c, path, $"must be greater than 0 ({value})"); }
        public void StatId(V4ConfigAsset c, string path, int statId) { if (statId < 0 || statId > 20) Error(c, path, $"stat id {statId} is outside 0-20 (GameEnums StatId)"); }
        public void WeightsNotZero(V4ConfigAsset c, string path, IEnumerable<float> weights)
        {
            var list = weights?.ToList();
            if (list != null && list.Count > 0 && list.Sum() <= 0f) Error(c, path, "weights add up to 0, nothing can be picked");
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine(IsValid ? $"valid ({_issues.Count} warnings)" : $"INVALID ({Errors.Count()} errors)");
            foreach (var i in _issues) sb.AppendLine(i.ToString());
            return sb.ToString();
        }
    }

    /// <summary>
    /// Validates a set of configs: ids present and unique, references resolvable inside the set, then each config's
    /// own rules (weights, caps vs bursts, timings, stat ids, trigger cycles).
    /// </summary>
    public static class ConfigValidator
    {
        public static ConfigValidationReport Validate(IEnumerable<V4ConfigAsset> configs)
        {
            var report = new ConfigValidationReport();
            var list = configs.Where(c => c != null).ToList();
            var ids = new HashSet<string>();
            foreach (var c in list)
            {
                if (string.IsNullOrWhiteSpace(c.configId)) report.Error(c, "configId", $"missing id on asset '{c.name}'");
                else if (!ids.Add(c.configId)) report.Error(c, "configId", "duplicate id");
            }

            foreach (var c in list)
            {
                ConfigFieldWalker.Walk(c, f =>
                {
                    if (f.Kind != ConfigFieldKind.AssetReference) return;
                    var target = f.GetValue() as V4ConfigAsset;
                    if (target != null && !ids.Contains(target.configId))
                        report.Error(c, f.Path, $"refers to '{target.configId}', which is not in this config set");
                });
                c.Validate(report);
            }
            return report;
        }

        public static ConfigValidationReport Validate(ConfigSet set) => Validate(set.configs);
    }
}
