using RoguePirates.V4.DevTools.Scenarios;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Console
{
    /// <summary>
    /// V5 M8z (7 Oct, I8z.4): the Scenario Lab by hand: every scenario asset (Assets/_Game/V4/DevTools/Scenarios) with
    /// Load (the run scene reloads with the scenario set up, as the PlayMode tests load it), the loaded one's setup
    /// problems, and a JSON box (copy the loaded scenario, paste one to load: works in development builds too).
    /// </summary>
    public sealed class ScenariosSection : IConsoleSection
    {
        public string Title => "Scenarios";
        public string SearchText => "scenario lab load seed crab elite boss beacon island kill band pvp bot json";

        GUIStyle _wrap, _rich;
        string _json = string.Empty;
        Vector2 _jsonScroll;
        System.Collections.Generic.IReadOnlyList<ScenarioConfig> _all;
        float _listedAt = -10f;

        public void Draw(TuningConsole console)
        {
            if (_wrap == null) { _wrap = new GUIStyle(GUI.skin.label) { wordWrap = true }; _rich = new GUIStyle(_wrap) { richText = true }; }
            var current = Scenario.Current;
            GUILayout.Label(current != null ? $"Loaded: {current.Config.name}" + (current.Errors.Count > 0 ? "   (problems: " + string.Join("; ", current.Errors) + ")" : string.Empty)
                : "No scenario loaded (this is a regular run).", _wrap);
            if (!string.IsNullOrEmpty(Scenario.LastError)) GUILayout.Label("Last load: " + Scenario.LastError, _wrap);
            if (Scenario.Loading) { GUILayout.Label("Loading..."); return; }

            var filter = console.SearchText;
            if (_all == null || Time.unscaledTime - _listedAt > 3f) { _all = Scenario.All(); _listedAt = Time.unscaledTime; } // the asset search is not per frame
            foreach (var scenario in _all)
            {
                if (scenario == null) continue;
                if (filter.Length > 0 && scenario.name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0 &&
                    (scenario.description ?? string.Empty).IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0 &&
                    Title.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Load", GUILayout.Width(50))) Scenario.LoadFromConsole(scenario);
                GUILayout.Label("<b>" + scenario.name + "</b>  " + scenario.description, _rich);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (current != null && GUILayout.Button("Copy loaded as JSON", GUILayout.Width(150))) _json = Scenario.ToJson(current.Config);
            if (GUILayout.Button("Load JSON", GUILayout.Width(90)) && _json.Trim().Length > 2)
            {
                try { Scenario.LoadFromConsole(Scenario.FromJson(_json)); }
                catch (System.Exception e) { Debug.LogWarning("Scenario JSON: " + e.Message); }
            }
            GUILayout.EndHorizontal();
            _jsonScroll = GUILayout.BeginScrollView(_jsonScroll, GUILayout.Height(90));
            _json = GUILayout.TextArea(_json ?? string.Empty, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
        }
    }
}
