using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HighSeas.Core;
using RoguePirates.V4.Config;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Console
{
    /// <summary>
    /// Console rows for every serialized field of every config in the run copy, grouped by config type and asset.
    /// Edits go through ConfigSetRuntime (logged, ConfigChanged). Also: reset field/asset, presets (save, load now,
    /// restart with preset from t=0), live set hash, change log, and in the Editor "Write back" to the asset.
    /// </summary>
    public sealed class ConfigSection : IConsoleSection
    {
        public string Title => "Config";
        // Every field name of every config type, so searching for a field shows this section (rows are then filtered).
        static string _searchText;
        public string SearchText => _searchText ??= "config preset hash " + string.Join(" ",
            typeof(V4ConfigAsset).Assembly.GetTypes()
                .Where(t => typeof(V4ConfigAsset).IsAssignableFrom(t) && !t.IsAbstract)
                .SelectMany(t => ConfigFieldWalker.Schema(t).Select(s => s.path).Prepend(t.Name))
                .Distinct());

        readonly HashSet<string> _openGroups = new HashSet<string>();
        readonly HashSet<string> _openConfigs = new HashSet<string>();
        readonly Dictionary<string, string> _pending = new Dictionary<string, string>(); // "configId|path" -> typed text
        string _presetName = "my-preset";
        string _status = string.Empty;
        bool _showLog;
        // Cached between edits: hashing ~40 configs and listing preset files is too slow for every GUI event.
        ConfigSetRuntime _bound;
        string _hash;
        string _validation = string.Empty;
        string[] _presetFiles;

        static string PresetFolder => Path.Combine(Application.persistentDataPath, "V4", "Presets");

        public void Draw(TuningConsole console)
        {
            var runtime = console.Game != null ? console.Game.Config : null;
            if (runtime == null) { GUILayout.Label("No config set on V4Game."); return; }
            var runTime = RunManager.Instance != null ? RunManager.Instance.ElapsedSeconds : 0f;
            Bind(runtime);

            GUILayout.Label($"Set {runtime.SetId} · hash {_hash.Substring(0, 12)} · {runtime.ChangeLog.Count} edits", GUI.skin.box);
            if (_validation.Length > 0) GUILayout.Label(_validation);
            DrawPresets(runtime, runTime);
            if (!string.IsNullOrEmpty(_status)) GUILayout.Label(_status);

            var filter = console.SearchText.Trim();
            // While searching, only configs with a matching id or field are listed.
            var visible = filter.Length == 0 ? runtime.Configs : runtime.Configs.Where(c => Matches(c, filter));
            foreach (var group in visible.GroupBy(c => c.GetType().Name).OrderBy(g => g.Key))
            {
                var groupOpen = _openGroups.Contains(group.Key) || filter.Length > 0;
                if (GUILayout.Button((groupOpen ? "▼ " : "▶ ") + Pretty(group.Key) + $" ({group.Count()})", GUI.skin.box))
                    Toggle(_openGroups, group.Key);
                if (!groupOpen) continue;
                foreach (var config in group.OrderBy(c => c.configId, StringComparer.Ordinal))
                    DrawConfig(runtime, config, filter, runTime);
            }

            _showLog = GUILayout.Toggle(_showLog, "Show change log");
            if (_showLog)
                foreach (var change in runtime.ChangeLog.Reverse().Take(30))
                    GUILayout.Label(change.ToString());
        }

        void Bind(ConfigSetRuntime runtime)
        {
            if (_bound == runtime) return;
            if (_bound != null) _bound.ConfigChanged -= OnChanged;
            _bound = runtime;
            _bound.ConfigChanged += OnChanged;
            Refresh();
        }

        void OnChanged(ConfigChange change) => Refresh();

        /// <summary>Recomputes the set hash and validates the edited configs (invalid edits stay applied but are flagged).</summary>
        void Refresh()
        {
            _hash = _bound.Hash;
            var report = ConfigValidator.Validate(_bound.Configs);
            var errors = report.Errors.ToList();
            _validation = errors.Count == 0 ? string.Empty : $"{errors.Count} validation error(s): " + string.Join("; ", errors.Take(3));
        }

        void DrawPresets(ConfigSetRuntime runtime, float runTime)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Preset", GUILayout.Width(44));
            _presetName = GUILayout.TextField(_presetName ?? string.Empty, GUILayout.Width(120));
            if (GUILayout.Button("Save")) SavePreset(runtime);
            if (GUILayout.Button("Reset all"))
            {
                foreach (var c in runtime.Configs.ToList()) runtime.ResetConfig(c.configId, runTime);
                _status = "all configs reset to defaults";
            }
            GUILayout.EndHorizontal();

            if (_presetFiles == null)
                _presetFiles = Directory.Exists(PresetFolder) ? Directory.GetFiles(PresetFolder, "*.json").OrderBy(f => f).ToArray() : new string[0];
            foreach (var file in _presetFiles)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(Path.GetFileNameWithoutExtension(file), GUILayout.Width(140));
                if (GUILayout.Button("Load now")) LoadPreset(runtime, file, runTime);
                if (GUILayout.Button("Restart with it"))
                {
                    RunRequest.RequestPreset(File.ReadAllText(file));
                    V4Game.RestartRunWithSeed(RunManager.Instance != null ? RunManager.Instance.RunSeed : 0);
                }
                GUILayout.EndHorizontal();
            }
        }

        void SavePreset(ConfigSetRuntime runtime)
        {
            var name = string.Concat((_presetName ?? "preset").Split(Path.GetInvalidFileNameChars()));
            if (name.Length == 0) name = "preset";
            var json = PresetCodec.ToJson(PresetCodec.Capture(runtime, name, runtime.Hash));
            Directory.CreateDirectory(PresetFolder);
            File.WriteAllText(Path.Combine(PresetFolder, name + ".json"), json);
#if UNITY_EDITOR
            // Editor: also keep it with the project data so it can be committed.
            var projectFolder = Path.Combine(Application.dataPath, "_Game", "V4", "Data", "Presets");
            Directory.CreateDirectory(projectFolder);
            File.WriteAllText(Path.Combine(projectFolder, name + ".json"), json);
#endif
            _presetFiles = null;
            _status = $"saved preset '{name}'";
        }

        void LoadPreset(ConfigSetRuntime runtime, string file, float runTime)
        {
            var errors = new List<string>();
            var ok = PresetCodec.Apply(runtime, PresetCodec.FromJson(File.ReadAllText(file)), runTime, errors);
            _status = ok ? $"loaded '{Path.GetFileNameWithoutExtension(file)}'" : "loaded with errors: " + string.Join("; ", errors.Take(3));
        }

        void DrawConfig(ConfigSetRuntime runtime, V4ConfigAsset config, string filter, float runTime)
        {
            var id = config.configId;
            var open = _openConfigs.Contains(id) || filter.Length > 0;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button((open ? "  ▼ " : "  ▶ ") + id, GUI.skin.label)) Toggle(_openConfigs, id);
            if (GUILayout.Button("Reset", GUILayout.Width(48))) runtime.ResetConfig(id, runTime);
#if UNITY_EDITOR
            if (GUILayout.Button("Write back", GUILayout.Width(76))) _status = WriteBack(runtime, config);
#endif
            GUILayout.EndHorizontal();
            if (!open) return;

            var rows = new List<ConfigField>();
            ConfigFieldWalker.Walk(config, rows.Add);
            foreach (var field in rows)
            {
                if (IsIdentity(field)) continue;
                if (filter.Length > 0 && field.Path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                    id.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                DrawField(runtime, config, field, runTime);
            }
        }

        static bool Matches(V4ConfigAsset config, string filter)
        {
            if (config.configId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            var found = false;
            ConfigFieldWalker.Walk(config, f => { if (!found && f.Path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) found = true; });
            return found;
        }

        static bool IsIdentity(ConfigField f) => f.Index < 0 && (f.Name == "configId" || f.Name == "schemaVersion");

        void DrawField(ConfigSetRuntime runtime, V4ConfigAsset config, ConfigField field, float runTime)
        {
            var depth = field.Path.Count(ch => ch == '.' || ch == '[');
            var tag = AppliesFrom(config, field.Path);
            var label = field.Name + (tag > 0 ? $"  (M{tag})" : tag == 0 ? "  (restart)" : string.Empty);
            GUILayout.BeginHorizontal();
            GUILayout.Space(16 + depth * 10);
            GUILayout.Label(new GUIContent(label, field.Tooltip), GUILayout.Width(190 - Mathf.Min(depth * 10, 80)));
            var key = config.configId + "|" + field.Path;
            var value = field.GetValue();
            if (field.Index < 0 && field.Info.GetCustomAttribute<ConfigIdentityAttribute>() != null)
            {
                GUILayout.Label(ConfigFieldWalker.Format(value)); // ids other data refers to: not editable here
                GUILayout.EndHorizontal();
                return;
            }
            switch (field.Kind)
            {
                case ConfigFieldKind.Bool:
                    var b = GUILayout.Toggle((bool)value, string.Empty);
                    if (b != (bool)value) runtime.TrySetValue(config.configId, field.Path, b, runTime, out _);
                    break;
                case ConfigFieldKind.Enum:
                    if (GUILayout.Button(value.ToString()))
                    {
                        var values = Enum.GetValues(field.Type);
                        var next = values.GetValue((Array.IndexOf(values, value) + 1) % values.Length);
                        runtime.TrySetValue(config.configId, field.Path, next, runTime, out _);
                    }
                    break;
                case ConfigFieldKind.Float:
                case ConfigFieldKind.Int:
                case ConfigFieldKind.String:
                case ConfigFieldKind.Vector2:
                case ConfigFieldKind.Vector3:
                case ConfigFieldKind.Color:
                    DrawTextEditor(runtime, config, field, key, ConfigFieldWalker.Format(value), runTime);
                    break;
                case ConfigFieldKind.List:
                    if (field.Index >= 0) { GUILayout.Label(string.Empty); break; } // element header; children follow
                    var count = (value as IList)?.Count ?? 0;
                    GUILayout.Label($"{count} items");
                    if (GUILayout.Button("+", GUILayout.Width(22))) PresetCodec.TryResize(runtime, config.configId, field.Path, (count + 1).ToString(), runTime, out _);
                    if (count > 0 && GUILayout.Button("-", GUILayout.Width(22))) PresetCodec.TryResize(runtime, config.configId, field.Path, (count - 1).ToString(), runTime, out _);
                    break;
                case ConfigFieldKind.Nested:
                    GUILayout.Label(string.Empty);
                    break;
                case ConfigFieldKind.Curve:
                    var curve = value as AnimationCurve;
                    GUILayout.Label(curve == null ? "none" : $"curve, {curve.length} keys (edit in the asset)");
                    break;
                default: // references
                    GUILayout.Label(ConfigFieldWalker.Format(value));
                    break;
            }
            if (field.Kind != ConfigFieldKind.Nested && field.Kind != ConfigFieldKind.List &&
                GUILayout.Button("↺", GUILayout.Width(22)))
            {
                runtime.ResetField(config.configId, field.Path, runTime, out _);
                _pending.Remove(key);
            }
            GUILayout.EndHorizontal();
        }

        /// <summary>Typed text is applied on Enter (or with the ✓ button), so half-typed numbers never reach the game.</summary>
        void DrawTextEditor(ConfigSetRuntime runtime, V4ConfigAsset config, ConfigField field, string key, string current, float runTime)
        {
            var text = _pending.TryGetValue(key, out var p) ? p : current;
            GUI.SetNextControlName(key);
            var edited = GUILayout.TextField(text, GUILayout.MinWidth(80));
            if (edited != current) _pending[key] = edited; else _pending.Remove(key);
            var enter = Event.current.type == EventType.KeyUp && Event.current.keyCode == KeyCode.Return && GUI.GetNameOfFocusedControl() == key;
            var dirty = _pending.ContainsKey(key);
            if (dirty && (GUILayout.Button("✓", GUILayout.Width(22)) || enter))
            {
                if (runtime.TryApplyEdit(config.configId, field.Path, edited, runTime, out var error)) _pending.Remove(key);
                else _status = $"{field.Path}: {error}";
            }
        }

        /// <summary>The nearest [AppliesFrom] on the field or a containing field: the phase, 0 for "restart", -1 when live.</summary>
        static int AppliesFrom(V4ConfigAsset config, string path)
        {
            var type = config.GetType();
            foreach (var segment in path.Split('.'))
            {
                var name = segment.Contains('[') ? segment.Substring(0, segment.IndexOf('[')) : segment;
                if (name.Length == 0) continue;
                var field = ConfigFieldWalker.SerializedFields(type).FirstOrDefault(f => f.Name == name);
                if (field == null) return -1;
                var attr = field.GetCustomAttribute<AppliesFromAttribute>();
                if (attr != null) return attr.Phase;
                type = ConfigFieldWalker.KindOf(field.FieldType) == ConfigFieldKind.List ? ConfigFieldWalker.ElementType(field.FieldType) : field.FieldType;
            }
            return -1;
        }

#if UNITY_EDITOR
        /// <summary>Copies this config's edited values into its asset (leaf values and list lengths; not references).</summary>
        static string WriteBack(ConfigSetRuntime runtime, V4ConfigAsset copy)
        {
            var source = runtime.Source(copy.configId);
            if (source == null) return "no source asset";
            var edits = PresetCodec.Diff(source, copy);
            using (var scratch = new ConfigSetRuntime("writeback", new[] { source }))
            {
                // Apply the same edits to a scratch copy, then copy its serialized data onto the asset.
                foreach (var (path, value) in edits.OrderBy(e => e.path.EndsWith(PresetCodec.CountSuffix) ? 0 : 1))
                {
                    if (path.EndsWith(PresetCodec.CountSuffix))
                        PresetCodec.TryResize(scratch, source.configId, path.Substring(0, path.Length - PresetCodec.CountSuffix.Length), value, 0f, out _);
                    else scratch.TryApplyEdit(source.configId, path, value, 0f, out _);
                }
                var edited = scratch.Get<V4ConfigAsset>(source.configId);
                edited.hideFlags = HideFlags.None;
                var name = source.name;
                UnityEditor.EditorUtility.CopySerialized(edited, source);
                source.name = name;
            }
            UnityEditor.EditorUtility.SetDirty(source);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(source);
            return $"wrote {edits.Count} values into {source.name}.asset";
        }
#endif

        static void Toggle(HashSet<string> set, string key) { if (!set.Remove(key)) set.Add(key); }
        static string Pretty(string typeName) => typeName.EndsWith("Config") ? typeName.Substring(0, typeName.Length - 6) : typeName;
    }
}
