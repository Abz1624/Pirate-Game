using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    [Serializable]
    public sealed class ConfigPreset
    {
        public int presetSchema = 1;
        public string setId;
        public string baseSetHash;
        public string name;
        public List<ConfigPresetEntry> configs = new List<ConfigPresetEntry>();
    }

    [Serializable]
    public sealed class ConfigPresetEntry
    {
        public string configId;
        public int schemaVersion;
        public List<ConfigPresetEdit> edits = new List<ConfigPresetEdit>();
    }

    [Serializable]
    public sealed class ConfigPresetEdit
    {
        public string path;   // leaf path, or "<list path>#count" for a list length
        public string value;
    }

    /// <summary>
    /// Presets are JSON diffs against the default assets: only fields that differ are stored, so a preset survives
    /// unrelated default changes. List length changes are stored as "path#count" edits and applied first.
    /// </summary>
    public static class PresetCodec
    {
        public const string CountSuffix = "#count";

        /// <summary>Leaf differences between two instances of the same config type (paths + new value text).</summary>
        public static List<(string path, string value)> Diff(object baseline, object current)
        {
            var result = new List<(string, string)>();
            if (baseline == null || current == null) return result;
            var baseValues = Leaves(baseline);
            foreach (var (path, value, isCount) in LeavesWithCounts(current))
            {
                if (!baseValues.TryGetValue(path, out var b) || b != value)
                    result.Add((path, value));
            }
            return result;
        }

        static Dictionary<string, string> Leaves(object root) =>
            LeavesWithCounts(root).ToDictionary(l => l.path, l => l.value);

        static List<(string path, string value, bool isCount)> LeavesWithCounts(object root)
        {
            var list = new List<(string, string, bool)>();
            ConfigFieldWalker.Walk(root, f =>
            {
                if (f.Index < 0 && f.Kind == ConfigFieldKind.List)
                {
                    var count = f.GetValue() is IList l ? l.Count : 0;
                    list.Add((f.Path + CountSuffix, count.ToString(CultureInfo.InvariantCulture), true));
                }
                else if (f.Kind != ConfigFieldKind.Nested && f.Kind != ConfigFieldKind.List &&
                         f.Kind != ConfigFieldKind.Curve && f.Kind != ConfigFieldKind.UnityReference && f.Kind != ConfigFieldKind.AssetReference)
                {
                    list.Add((f.Path, ConfigFieldWalker.Format(f.GetValue()), false));
                }
            });
            return list;
        }

        public static ConfigPreset Capture(ConfigSetRuntime runtime, string presetName, string baseSetHash)
        {
            var preset = new ConfigPreset { setId = runtime.SetId, name = presetName, baseSetHash = baseSetHash };
            foreach (var copy in runtime.Configs.OrderBy(c => c.configId, StringComparer.Ordinal))
            {
                var edits = Diff(runtime.Source(copy.configId), copy);
                if (edits.Count == 0) continue;
                var entry = new ConfigPresetEntry { configId = copy.configId, schemaVersion = copy.schemaVersion };
                // Counts first so element paths exist when their values are applied.
                foreach (var (path, value) in edits.OrderBy(e => e.path.EndsWith(CountSuffix) ? 0 : 1).ThenBy(e => e.path, StringComparer.Ordinal))
                    entry.edits.Add(new ConfigPresetEdit { path = path, value = value });
                preset.configs.Add(entry);
            }
            return preset;
        }

        public static string ToJson(ConfigPreset preset) => JsonUtility.ToJson(preset, true);
        public static ConfigPreset FromJson(string json) => JsonUtility.FromJson<ConfigPreset>(json);

        /// <summary>Applies a preset to a runtime set (defaults first, so the result equals default + preset).</summary>
        public const int CurrentPresetSchema = 1;

        public static bool Apply(ConfigSetRuntime runtime, ConfigPreset preset, float runTime, List<string> errors)
        {
            if (preset == null || preset.presetSchema > CurrentPresetSchema)
            {
                errors?.Add($"preset format {preset?.presetSchema} is newer than this build ({CurrentPresetSchema})");
                return false;
            }
            foreach (var copy in runtime.Configs.ToList()) runtime.ResetConfig(copy.configId, runTime);
            var ok = true;
            foreach (var entry in preset.configs)
            {
                var copy = runtime.Get<V4ConfigAsset>(entry.configId);
                if (copy == null) { errors?.Add($"{entry.configId}: not in this config set"); ok = false; continue; }
                var edits = entry.edits;
                if (entry.schemaVersion != copy.schemaVersion &&
                    !ConfigMigrations.TryMigrate(copy.GetType(), entry.schemaVersion, copy.schemaVersion, edits, out edits, out var why))
                {
                    errors?.Add($"{entry.configId}: preset schema {entry.schemaVersion} cannot be used with schema {copy.schemaVersion} ({why})");
                    ok = false;
                    continue;
                }
                foreach (var edit in edits)
                {
                    string error;
                    var applied = edit.path.EndsWith(CountSuffix)
                        ? TryResize(runtime, entry.configId, edit.path.Substring(0, edit.path.Length - CountSuffix.Length), edit.value, runTime, out error)
                        : runtime.TryApplyEdit(entry.configId, edit.path, edit.value, runTime, out error);
                    if (!applied) { errors?.Add($"{entry.configId}.{edit.path}: {error}"); ok = false; }
                }
            }
            return ok;
        }

        /// <summary>Sets a list's length; new elements copy the last element (or default when empty).</summary>
        public static bool TryResize(ConfigSetRuntime runtime, string configId, string listPath, string countText, float runTime, out string error)
        {
            error = null;
            var copy = runtime.Get<V4ConfigAsset>(configId);
            if (copy == null) { error = "unknown config"; return false; }
            if (!int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) || count < 0) { error = "bad count"; return false; }
            try
            {
                var listType = ConfigPath.TypeAt(copy, listPath);
                var current = ConfigPath.Get(copy, listPath) as IList;
                var element = ConfigFieldWalker.ElementType(listType);
                var items = new List<object>();
                if (current != null) foreach (var o in current) items.Add(o);
                if (items.Count == count) return true;
                while (items.Count > count) items.RemoveAt(items.Count - 1);
                while (items.Count < count) items.Add(items.Count > 0 ? CloneElement(items[items.Count - 1], element) : DefaultElement(element));
                IList resized;
                if (listType.IsArray)
                {
                    var array = Array.CreateInstance(element, count);
                    for (var i = 0; i < count; i++) array.SetValue(items[i], i);
                    resized = array;
                }
                else
                {
                    resized = (IList)Activator.CreateInstance(listType);
                    foreach (var o in items) resized.Add(o);
                }
                return runtime.TrySetValue(configId, listPath, resized, runTime, out error);
            }
            catch (Exception e) { error = e.Message; return false; }
        }

        static object DefaultElement(Type type) =>
            type == typeof(string) ? string.Empty : type.IsValueType || type.GetConstructor(Type.EmptyTypes) != null ? Activator.CreateInstance(type) : null;

        static object CloneElement(object source, Type type)
        {
            if (source == null || type.IsValueType || type == typeof(string) || typeof(UnityEngine.Object).IsAssignableFrom(type)) return source;
            return JsonUtility.FromJson(JsonUtility.ToJson(source), type);
        }
    }

    /// <summary>
    /// Upgrades preset edits written against an older schema of a config type. Each config type registers a step per
    /// version bump (renamed/moved paths); without one, older presets are rejected with a message.
    /// </summary>
    public static class ConfigMigrations
    {
        public delegate List<ConfigPresetEdit> Step(List<ConfigPresetEdit> edits);
        static readonly Dictionary<(Type, int), Step> Steps = new Dictionary<(Type, int), Step>();

        public static void Register(Type configType, int fromVersion, Step step) => Steps[(configType, fromVersion)] = step;

        public static bool TryMigrate(Type configType, int from, int to, List<ConfigPresetEdit> edits, out List<ConfigPresetEdit> migrated, out string error)
        {
            migrated = edits;
            error = null;
            if (from > to) { error = "preset is newer than this build"; return false; }
            for (var v = from; v < to; v++)
            {
                if (!Steps.TryGetValue((configType, v), out var step)) { error = $"no migration from schema {v}"; return false; }
                migrated = step(migrated);
            }
            return true;
        }
    }
}
