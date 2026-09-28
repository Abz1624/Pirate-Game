using RoguePirates.V4.Config;
using RoguePirates.V4.Narrative;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Console
{
    /// <summary>
    /// Barks and Tutorial (M13): play any bark now; the tutorial's steps with their state (done, skipped this run,
    /// current, waiting), and reset or advance it. Lines, cooldowns, priorities and prompts are Config rows (narrative.*).
    /// </summary>
    public sealed class NarrativeSection : IConsoleSection
    {
        public string Title => "Barks & Tutorial";
        public string SearchText => "barks tutorial narrative penny chipmunk barnacle onboarding first conquest prompt";

        const float Width = 440f;
        GUIStyle _wrap;
        Vector2 _scroll;

        public void Draw(TuningConsole console)
        {
            var game = console.Game;
            if (game?.Context == null || !game.Context.TryGet<IBarkControl>(out var barks) || !game.Context.TryGet<ITutorialControl>(out var tutorial))
            {
                GUILayout.Label("No V4 narrative in this scene.");
                return;
            }
            if (_wrap == null) _wrap = new GUIStyle(GUI.skin.label) { wordWrap = true };
            var config = game.Config?.Single<NarrativeConfig>();
            if (config == null) return;

            GUILayout.Label($"Tutorial: {(tutorial.Active ? $"step {tutorial.Index + 1} of {tutorial.Count}" : tutorial.Index >= tutorial.Count ? "done" : "off")}", _wrap, GUILayout.Width(Width));
            for (var i = 0; i < config.tutorial.Count; i++)
            {
                var s = config.tutorial[i];
                var state = System.Linq.Enumerable.Contains(tutorial.Skipped, s.stepId) ? "skipped" : i < tutorial.Index ? "done" : i == tutorial.Index && tutorial.Active ? "current" : "waiting";
                GUILayout.Label($"{i + 1}. {s.stepId} — {state} (on {s.completesOn})", _wrap, GUILayout.Width(Width));
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset tutorial", GUILayout.Width(120))) tutorial.ResetProgress();
            if (tutorial.Current != null && GUILayout.Button("Complete current", GUILayout.Width(140))) tutorial.Complete(tutorial.Current.completesOn);
            GUILayout.EndHorizontal();

            GUILayout.Label("Barks (play now):", _wrap, GUILayout.Width(Width));
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Width(Width), GUILayout.Height(160));
            foreach (var b in config.barks)
            {
                if (b == null) continue;
                if (GUILayout.Button($"{b.barkId} ({b.speaker}, {b.trigger})", GUILayout.Width(Width - 30))) barks.Play(b.barkId);
            }
            GUILayout.EndScrollView();
        }
    }
}
