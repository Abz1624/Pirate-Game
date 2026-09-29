using System.Globalization;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Player;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Console
{
    /// <summary>Run controls: seed and restart, time scale, god mode, jump to time (forward only), force death.</summary>
    public sealed class RunSection : IConsoleSection
    {
        /// <summary>True once the console changed Time.timeScale; the console resets it when the run scene unloads.</summary>
        internal static bool TimeScaleChanged;

        internal static void RestoreTimeScale()
        {
            if (!TimeScaleChanged) return;
            Time.timeScale = 1f;
            TimeScaleChanged = false;
        }

        string _seedText = "101";
        string _jumpText = "240";
        bool _god;

        public string Title => "Run";
        public string SearchText => "seed restart time scale god mode jump death";

        public void Draw(TuningConsole console)
        {
            var run = RunManager.Instance;
            GUILayout.Label(run != null ? $"Seed {run.RunSeed} · state {run.State} · kills {run.KillCount} · gold {run.Gold}" : "No run");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Seed", GUILayout.Width(40));
            _seedText = GUILayout.TextField(_seedText, GUILayout.Width(90));
            if (GUILayout.Button("Restart on seed") && int.TryParse(_seedText, out var seed) && seed != 0)
                V4Game.RestartRunWithSeed(seed);
            if (GUILayout.Button("Retry")) V4Game.RestartRun();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Time scale {Time.timeScale:0.##}", GUILayout.Width(110));
            var scale = GUILayout.HorizontalSlider(Time.timeScale, 0f, 4f);
            if (!Mathf.Approximately(scale, Time.timeScale)) { Time.timeScale = Mathf.Round(scale * 4f) / 4f; TimeScaleChanged = true; }
            if (GUILayout.Button("1×", GUILayout.Width(32))) Time.timeScale = 1f;
            GUILayout.EndHorizontal();

            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            var god = GUILayout.Toggle(_god, "God mode (player takes no damage)");
            if (god != _god && ship != null && ship.Damage != null)
            {
                _god = god;
                ship.Damage.SetPlaytestInvulnerable(god);
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Jump to (s)", GUILayout.Width(80));
            _jumpText = GUILayout.TextField(_jumpText, GUILayout.Width(70));
            if (GUILayout.Button("Jump") && run != null &&
                float.TryParse(_jumpText, NumberStyles.Float, CultureInfo.InvariantCulture, out var target))
                JumpTo(run, target);
            GUILayout.EndHorizontal();
            GUILayout.Label("Forward only: due beats fire at once and skipped minutes count 0 kills for Rum.");

            if (GUILayout.Button("Force death") && ship != null && ship.Damage != null && ship.Damage.IsAlive)
            {
                _god = false;
                ship.Damage.SetPlaytestInvulnerable(false);
                ship.Damage.TakeDamage(1_000_000f, ship.Position, false);
            }

            // Playtest round 1: a pilot that plays like a person (fights, dodges, collects, explores, opens chests, uses
            // landmarks, summons and fights the boss) — to watch, and for balance measurements.
            var player = Object.FindFirstObjectByType<RoguePirates.V4.DevTools.Trace.HumanPlayer>();
            GUILayout.BeginHorizontal();
            GUILayout.Label(player != null ? $"Autopilot: {player.Profile} ({player.Pilot.Goal})" : "Autopilot: off", GUILayout.Width(200));
            foreach (RoguePirates.V4.DevTools.Trace.HumanPlayer.BuildProfile profile in System.Enum.GetValues(typeof(RoguePirates.V4.DevTools.Trace.HumanPlayer.BuildProfile)))
                if (GUILayout.Button(profile.ToString(), GUILayout.Width(70)) && ship != null)
                {
                    if (player != null) player.Uninstall();
                    RoguePirates.V4.DevTools.Trace.HumanPlayer.Install(ship, profile);
                }
            if (player != null && GUILayout.Button("Off", GUILayout.Width(50))) player.Uninstall();
            GUILayout.EndHorizontal();
        }

        static void JumpTo(RunManager run, float target)
        {
            if (target <= run.ElapsedSeconds) return;
            run.JumpClock(target);
        }
    }
}
