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
            // Own object, not /Systems: the legacy EnemyViewPool uses /Systems as its pool root and deactivates it
            // around every new enemy view (R-72), which would close the console each time.
            var console = Object.FindFirstObjectByType<TuningConsole>();
            if (console == null) console = new GameObject("V4DevTools").AddComponent<TuningConsole>();
            console.Bind(game);
        }
    }
}
