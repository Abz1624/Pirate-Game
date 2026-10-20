using RoguePirates.V4.Telemetry;
using UnityEngine;

namespace RoguePirates.Hackathon
{
    /// <summary>
    /// V5 M9j: a raid's or race's end is the voyage's end (CompleteGoal); the run log closes on the tick that carries it, a
    /// frame or so later. The result waits for it (real time, at most <paramref name="seconds"/>) so the server can check
    /// the result against the log; without it the result is sent anyway (the server's log checks are shadow mode).
    /// </summary>
    public static class RunLogWait
    {
        public static async Awaitable<RunLogResult> Closed(RunLogRecorder recorder, float seconds = 3f)
        {
            if (recorder == null) return null;
            var until = Time.realtimeSinceStartup + seconds;
            while (recorder.Closed == null && Time.realtimeSinceStartup < until) await Awaitable.NextFrameAsync();
            return recorder.Closed;
        }
    }
}
