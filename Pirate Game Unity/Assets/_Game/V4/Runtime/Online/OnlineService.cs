using System;
using UnityEngine;

namespace RoguePirates.V4.Online
{
    /// <summary>The API as the menu shows it.</summary>
    public enum OnlineState { Unknown, Checking, Online, Offline, NotConfigured }

    /// <summary>A run's submission as the results screen shows it.</summary>
    public sealed class RunSubmission
    {
        public bool Ranked;
        public int Rank;
        public int BestKills;
        public bool NewWeeklyBest;
        public string Line;
    }

    /// <summary>
    /// The online features (guest profiles, the weekly leaderboard, run submission) as the menus and the run call them.
    /// V5 M8z (7 Oct): a facade over the <see cref="OnlineServices"/> the app's composition root owns (it held the state
    /// itself before); the menu panels keep calling it unchanged.
    /// </summary>
    public static class OnlineService
    {
        static OnlineServices S => OnlineComposition.Online;

        /// <summary>The connection or a profile changed (the menu refreshes).</summary>
        public static event Action Changed { add => S.Changed += value; remove => S.Changed -= value; }

        public static OnlineState State => S.State;
        public static bool IsOnline => S.IsOnline;
        public static string LastError => S.LastError;
        public static OnlineSettings Settings => OnlineSettings.Load();
        public static ApiClient Client => S.Client;
        public static ProfileStore Profiles => S.Profiles;
        public static LocalProfile Active => S.Active;

        public static void Reconfigure() => S.Reconfigure();
        public static Awaitable<bool> Connect() => S.Connect();
        public static Awaitable<bool> EnsureConnected() => S.EnsureConnected();
        public static Awaitable<string> SyncActive() => S.SyncActive();
        public static Awaitable<string> CreateProfile(string rawName) => S.CreateProfile(rawName);
        public static Awaitable<string> RenameActive(string rawName) => S.RenameActive(rawName);
        public static Awaitable<string> SwitchTo(string localId) => S.SwitchTo(localId);
        public static void Forget(string localId) => S.Forget(localId);
        public static Awaitable<ApiResult<LeaderboardResponse>> Leaderboard(bool previous, int limit = 0) => S.Leaderboard(previous, limit);
        public static Awaitable<RunSubmission> SubmitRun(RunSubmitRequest run) => S.SubmitRun(run);
    }
}
