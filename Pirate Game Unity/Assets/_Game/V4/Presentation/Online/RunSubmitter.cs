using System;
using System.Collections.Generic;
using RoguePirates.V4.Foundation.Progression;
using RoguePirates.V4.Online;
using RoguePirates.V4.Views;
using UnityEngine;

namespace RoguePirates.V4.Presentation.Online
{
    /// <summary>
    /// Playtest (3 Oct, owner): sends a finished Saltwind run to the weekly leaderboard (kills, run time, level, Captain,
    /// a build hash; V5 M9a: and the run log) and reports the line the results screen shows ("Weekly rank #N (best K kills)" or "Not ranked:
    /// offline"). Never blocks: the line arrives when the API answers.
    /// </summary>
    public static class RunSubmitter
    {
        public const string Pending = "Weekly rank: sending...";

        /// <summary>The request for the run the views show (null when there is no run).</summary>
        public static RunSubmitRequest Build(IRunViews views)
        {
            var run = views?.Run;
            if (run == null) return null;
            var parts = new List<string>();
            var loadout = views.Loadout;
            if (loadout != null)
                for (var i = 0; i < loadout.WeaponCount; i++) parts.Add("w:" + loadout.WeaponId(i) + ":" + loadout.WeaponLevel(i));
            var progression = views.Progression;
            if (progression != null)
                for (var i = 0; i < progression.LogbookCount; i++) parts.Add("l:" + progression.LogbookId(i) + ":" + progression.LogbookRank(i));
            var request = new RunSubmitRequest
            {
                kills = Mathf.Max(0, run.Kills),
                runSeconds = Mathf.Max(0, Mathf.FloorToInt(run.ElapsedSeconds)),
                level = Mathf.Clamp(progression != null ? progression.Level : 1, 1, 200),
                captain = CaptainKey(views.Captain),
                mapId = OnlineService.Settings.mapId,
                buildHash = OnlineRules.BuildHash(parts),
                clientVersion = Application.version,
            };
            AttachLog(request, views.RunLog);
            return request;
        }

        /// <summary>
        /// V5 M9a (I9a.1): sends the run log the runtime closed at the run's end with the run. Read only (presentation never
        /// changes run state): the summary's kills, run time and level are taken from the closed log, so the API finds them
        /// equal to the log's sums. No closed log (the run has not ended, or it kept none): the run goes without one.
        /// </summary>
        public static void AttachLog(RunSubmitRequest request, RoguePirates.V4.Telemetry.IRunLogSource source)
        {
            if (request == null || source == null) return;
            RoguePirates.V4.Telemetry.RunLogResult log;
            try { log = source.Closed; }
            catch (Exception e)
            {
                Debug.LogWarning("RunSubmitter: run log not read: " + e.Message);
                return;
            }
            if (log == null || string.IsNullOrEmpty(log.Json)) return;
            request.kills = Mathf.Max(0, log.Kills);
            request.runSeconds = Mathf.Max(0, Mathf.FloorToInt(log.Seconds));
            request.level = Mathf.Clamp(log.Level, 1, 200);
            request.logJson = log.Json;
        }

        /// <summary>The Captain's save key (rook, brine, ...): the Foundation Captain id's name, lower case.</summary>
        static string CaptainKey(ICaptainView captain)
        {
            if (captain == null) return string.Empty;
            var id = captain.CaptainId;
            return Enum.IsDefined(typeof(CaptainId), id) ? ((CaptainId)id).ToString().ToLowerInvariant() : id.ToString();
        }

        /// <summary>Submits the run in the background and calls <paramref name="line"/> with the text to show.</summary>
        public static async void Submit(IRunViews views, Action<string> line)
        {
            RunSubmission result;
            try { result = await OnlineService.SubmitRun(Build(views)); }
            catch (Exception e)
            {
                Debug.LogWarning("RunSubmitter: " + e.Message);
                result = new RunSubmission { Line = "Not ranked: offline" };
            }
            try { line?.Invoke(result.Line); }
            catch (Exception e) { Debug.LogWarning("RunSubmitter: " + e.Message); }
        }
    }
}
