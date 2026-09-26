using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Config
{
    /// <summary>One logged live edit: when it happened in the run, which field, old and new value.</summary>
    [Serializable]
    public struct ConfigChange
    {
        public float runTime;
        public string configId;
        public string path;
        public string oldValue;
        public string newValue;
        public override string ToString() => $"{runTime:0.0}s {configId}.{path}: {oldValue} -> {newValue}";
    }

    /// <summary>
    /// The per-run copy of a <see cref="ConfigSet"/>. Modules read only these copies, never the assets, so live
    /// edits (console, presets) never touch project files. Every edit is logged and raises <see cref="ConfigChanged"/>.
    /// </summary>
    public sealed class ConfigSetRuntime : IDisposable
    {
        readonly Dictionary<string, V4ConfigAsset> _copies = new Dictionary<string, V4ConfigAsset>();
        readonly Dictionary<string, V4ConfigAsset> _sources = new Dictionary<string, V4ConfigAsset>();
        readonly List<ConfigChange> _log = new List<ConfigChange>();

        public event Action<ConfigChange> ConfigChanged;
        public IReadOnlyList<ConfigChange> ChangeLog => _log;
        public string SetId { get; }
        public IEnumerable<V4ConfigAsset> Configs => _copies.Values;

        public ConfigSetRuntime(ConfigSet set) : this(set.setId, set.configs) { }

        public ConfigSetRuntime(string setId, IEnumerable<V4ConfigAsset> sources)
        {
            SetId = setId;
            foreach (var source in sources.Where(s => s != null))
            {
                if (_sources.ContainsKey(source.configId))
                    throw new InvalidOperationException($"Duplicate configId '{source.configId}' in set '{setId}'.");
                _sources[source.configId] = source;
                var copy = Object.Instantiate(source);
                copy.name = source.name;
                copy.hideFlags = HideFlags.DontSave;
                _copies[source.configId] = copy;
            }
            // References between configs point at the copies, so a module following one sees edited values.
            foreach (var copy in _copies.Values) RemapReferences(copy);
        }

        void RemapReferences(object root)
        {
            var targets = new List<(ConfigField field, V4ConfigAsset asset)>();
            ConfigFieldWalker.Walk(root, f =>
            {
                if (f.Kind == ConfigFieldKind.AssetReference && f.GetValue() is V4ConfigAsset asset && asset != null && _copies.TryGetValue(asset.configId, out var copy) && copy != asset)
                    targets.Add((f, copy));
            });
            foreach (var (field, copy) in targets)
            {
                if (field.Index >= 0) ((System.Collections.IList)field.Owner)[field.Index] = copy;
                else field.Info.SetValue(field.Owner, copy);
            }
        }

        public string Hash => ConfigSet.HashOf(_copies.Values);

        public T Get<T>(string configId) where T : V4ConfigAsset =>
            _copies.TryGetValue(configId, out var c) ? c as T : null;

        public T Single<T>() where T : V4ConfigAsset => _copies.Values.OfType<T>().FirstOrDefault();
        public IEnumerable<T> All<T>() where T : V4ConfigAsset => _copies.Values.OfType<T>();
        public V4ConfigAsset Source(string configId) => _sources.TryGetValue(configId, out var s) ? s : null;

        /// <summary>Sets one field from text (invariant format). Logs and raises ConfigChanged on success.</summary>
        public bool TryApplyEdit(string configId, string path, string valueText, float runTime, out string error)
        {
            error = null;
            if (!_copies.TryGetValue(configId, out var copy)) { error = $"unknown config '{configId}'"; return false; }
            try
            {
                var type = ConfigPath.TypeAt(copy, path);
                if (typeof(V4ConfigAsset).IsAssignableFrom(type))
                {
                    // References by configId ("" = none), resolved to this run's copies.
                    var id = valueText?.Trim() ?? string.Empty;
                    V4ConfigAsset target = null;
                    if (id.Length > 0 && (!_copies.TryGetValue(id, out target) || !type.IsInstanceOfType(target)))
                    {
                        error = $"'{id}' is not a {type.Name} in this set";
                        return false;
                    }
                    return Apply(copy, path, target, runTime);
                }
                if (!ConfigFieldWalker.TryParse(type, valueText, out var value))
                {
                    error = $"'{valueText}' is not a valid {type.Name}";
                    return false;
                }
                return Apply(copy, path, value, runTime);
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        /// <summary>Sets one field to a typed value (console widgets use this).</summary>
        public bool TrySetValue(string configId, string path, object value, float runTime, out string error)
        {
            error = null;
            if (!_copies.TryGetValue(configId, out var copy)) { error = $"unknown config '{configId}'"; return false; }
            try { return Apply(copy, path, value, runTime); }
            catch (Exception e) { error = e.Message; return false; }
        }

        bool Apply(V4ConfigAsset copy, string path, object value, float runTime)
        {
            var old = ConfigPath.Get(copy, path);
            var isList = old is System.Collections.IList || value is System.Collections.IList;
            var oldText = isList ? $"count {(old as System.Collections.IList)?.Count ?? 0}" : ConfigFieldWalker.Format(old);
            var newText = isList ? $"count {(value as System.Collections.IList)?.Count ?? 0}" : ConfigFieldWalker.Format(value);
            // Leaf values compare as text; lists (resizes) always apply unless it is the same instance.
            if (isList ? ReferenceEquals(old, value) : oldText == newText) return true;
            ConfigPath.Set(copy, path, value);
            var change = new ConfigChange { runTime = runTime, configId = copy.configId, path = path, oldValue = oldText, newValue = newText };
            _log.Add(change);
            // The value is set and logged; a failing listener must not turn that into a reported failure.
            if (ConfigChanged != null)
                foreach (Action<ConfigChange> handler in ConfigChanged.GetInvocationList())
                {
                    try { handler(change); }
                    catch (Exception e) { Debug.LogException(e); }
                }
            return true;
        }

        /// <summary>Restores one field to the asset's value.</summary>
        public bool ResetField(string configId, string path, float runTime, out string error)
        {
            error = null;
            var source = Source(configId);
            if (source == null) { error = $"unknown config '{configId}'"; return false; }
            try
            {
                var value = ConfigPath.Get(source, path);
                // A reference in the asset points at an asset; the run uses that asset's copy.
                if (value is V4ConfigAsset asset && asset != null && _copies.TryGetValue(asset.configId, out var copy)) value = copy;
                return TrySetValue(configId, path, value, runTime, out error);
            }
            catch (Exception e) { error = e.Message; return false; }
        }

        /// <summary>
        /// Restores one config to its asset: list lengths first (outer lists before inner ones, so element paths
        /// exist), then every leaf value that differs.
        /// </summary>
        public void ResetConfig(string configId, float runTime)
        {
            var source = Source(configId);
            if (source == null) return;
            var lists = new List<(string path, int count)>();
            ConfigFieldWalker.Walk(source, f =>
            {
                if (f.Index < 0 && f.Kind == ConfigFieldKind.List)
                    lists.Add((f.Path, (f.GetValue() as System.Collections.IList)?.Count ?? 0));
            });
            foreach (var (path, count) in lists)
                PresetCodec.TryResize(this, configId, path, count.ToString(System.Globalization.CultureInfo.InvariantCulture), runTime, out _);
            foreach (var (path, _) in PresetCodec.Diff(source, Get<V4ConfigAsset>(configId)))
                if (!path.EndsWith(PresetCodec.CountSuffix)) ResetField(configId, path, runTime, out _);
        }

        public void Dispose()
        {
            foreach (var copy in _copies.Values)
                if (copy != null) Object.DestroyImmediate(copy);
            _copies.Clear();
        }
    }
}
