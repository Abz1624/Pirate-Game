using RoguePirates.V4.DevTools.Console;
using RoguePirates.V4.DevTools.Trace;
using UnityEngine;

namespace RoguePirates.V4.DevTools
{
    /// <summary>
    /// Attaches the Tuning Console to each V4 run. Lives in the DevTools assembly, which only exists in the Editor
    /// and development builds, so release builds carry no console. Skipped during trace runs so recorded runs
    /// contain nothing a normal run would not.
    /// </summary>
    static class V4DevToolsInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            V4Game.RunStarted -= OnRunStarted;
            V4Game.RunStarted += OnRunStarted;
        }

        static void OnRunStarted(V4Game game)
        {
            if (TraceBoot.Active != null || game == null) return;
            EnsurePlaytestTools(game);
            // Own object, not /Systems: the legacy EnemyViewPool uses /Systems as its pool root and deactivates it
            // around every new enemy view (R-72), which would close the console each time.
            var console = Object.FindFirstObjectByType<TuningConsole>();
            if (console == null) console = new GameObject("V4DevTools").AddComponent<TuningConsole>();
            console.Bind(game);
        }

        /// <summary>
        /// V3's run playtest tools (M14.14; was LegacyPresentationAdapter.EnsureDevAndDebugTools, in every build): the F8
        /// hotkeys and, with V3's playtest flag, the weapon-card animation demo and its toast. DevTools only now.
        /// </summary>
        static void EnsurePlaytestTools(V4Game game)
        {
            HighSeas.V3.V3RunPlaytestHotkeys.Ensure();
            if (!HighSeas.V3.V3PlaytestFlags.EnableRunPlaytestHotkeys) return;
            HighSeas.UI.WeaponCardAnimationDemo.Ensure();
            if (game.Context != null && game.Context.TryGet<RoguePirates.V4.Views.IRunViews>(out var views))
                views.Toasts?.Show("Playtest: F8 hotkeys · G weapon card scatter · H card scroll", 5f);
        }
    }
}
