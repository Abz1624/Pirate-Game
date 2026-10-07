using System;
using UnityEngine;

namespace RoguePirates.V4.Online
{
    /// <summary>
    /// Playtest (3 Oct, owner): the online features in one place (guest profiles, the weekly leaderboard, run
    /// submission) over <see cref="ApiClient"/> and <see cref="ProfileStore"/>. Everything degrades to "Offline": a
    /// profile made offline is kept locally and registered with the API on the next online launch.
    /// V5 M8z (7 Oct): an instance owned by the app's composition root (<see cref="OnlineComposition"/> registers one on
    /// the app root); it was a static class with mutable state. <see cref="OnlineService"/> forwards to it.
    /// </summary>
    public sealed class OnlineServices
    {
        ApiClient _client;
        ProfileStore _profiles;
        bool _syncing;
        OnlineState _state;

        /// <summary>The connection or a profile changed (the menu refreshes).</summary>
        public event Action Changed;

        public OnlineState State => _state;
        public bool IsOnline => _state == OnlineState.Online;
        public string LastError { get; private set; }
        public OnlineSettings Settings => OnlineSettings.Load();

        public ApiClient Client => _client ?? (_client = new ApiClient(Settings.ResolveBaseUrl(), Settings.requestTimeoutSeconds));

        public ProfileStore Profiles
        {
            get
            {
                if (_profiles != null) return _profiles;
                _profiles = new ProfileStore();
                _profiles.Changed += Raise;
                return _profiles;
            }
        }

        public LocalProfile Active => Profiles.Active;

        /// <summary>Re-reads the URL (after a PlayerPrefs override changed) and checks again on the next call.</summary>
        public void Reconfigure()
        {
            _client = null;
            SetState(OnlineState.Unknown);
        }

        void Raise() => Changed?.Invoke();

        void SetState(OnlineState state, string error = null)
        {
            LastError = error;
            if (_state == state) return;
            _state = state;
            Raise();
        }

        /// <summary>Health check, then the active profile is registered or refreshed. True when online.</summary>
        public async Awaitable<bool> Connect()
        {
            if (!Client.Configured) { SetState(OnlineState.NotConfigured); return false; }
            SetState(OnlineState.Checking);
            var health = await Client.Get<HealthResponse>("/health", null, Settings.healthTimeoutSeconds);
            if (!health.Ok || health.Data == null || !health.Data.ok)
            {
                SetState(OnlineState.Offline, health.Message);
                return false;
            }
            SetState(OnlineState.Online);
            await SyncActive();
            return IsOnline;
        }

        /// <summary>Connects once (later calls reuse the state; Offline retries).</summary>
        public async Awaitable<bool> EnsureConnected()
        {
            if (_state == OnlineState.Online) return true;
            if (_state == OnlineState.NotConfigured) return false;
            if (_state == OnlineState.Checking)
            {
                while (_state == OnlineState.Checking) await Awaitable.NextFrameAsync();
                return IsOnline;
            }
            return await Connect();
        }

        /// <summary>Notes a request's outcome on the connection state (offline replies flip it, a reply flips it back).</summary>
        void Observe<T>(ApiResult<T> r) where T : class
        {
            if (r.IsOffline) SetState(r.Outcome == ApiOutcome.NotConfigured ? OnlineState.NotConfigured : OnlineState.Offline, r.Message);
            else if (r.Ok || r.Outcome == ApiOutcome.Rejected) SetState(OnlineState.Online);
        }

        /// <summary>Registers the active profile if it has no token, sends an offline rename, else refreshes its name, tag
        /// and rating. Returns a player-facing error, or null.</summary>
        public async Awaitable<string> SyncActive()
        {
            var p = Active;
            if (p == null || !IsOnline || _syncing) return null;
            _syncing = true;
            try
            {
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    if (!p.Registered)
                    {
                        var created = await Client.Post<CreateProfileResponse>("/api/profiles", new NameRequest { name = p.name });
                        Observe(created);
                        if (!created.Ok || created.Data == null) return created.Message;
                        Profiles.Apply(p.localId, created.Data.playerId, created.Data.token, created.Data.name, created.Data.tag);
                        Profiles.Rename(p.localId, created.Data.name, true);
                        await RefreshMe(p); // the rating (registration does not return it)
                        return null;
                    }
                    var me = p.nameDirty
                        ? await Client.Patch<ProfileResponse>("/api/profiles/me", new NameRequest { name = p.name }, p.token)
                        : await Client.Get<ProfileResponse>("/api/profiles/me", p.token);
                    Observe(me);
                    if (me.Status == 401) { Profiles.Unregister(p.localId); continue; } // the API forgot it (a fresh database): register again
                    if (me.Outcome == ApiOutcome.Rejected && p.nameDirty)
                    {
                        // The name was refused (block list): keep the server's name.
                        Profiles.Rename(p.localId, p.name, true);
                        var error = me.Message;
                        await RefreshMe(p);
                        return error;
                    }
                    if (!me.Ok || me.Data == null) return me.Message;
                    Profiles.Apply(p.localId, me.Data.playerId, null, me.Data.name, me.Data.tag, me.Data.rating);
                    if (p.nameDirty) Profiles.Rename(p.localId, me.Data.name, true);
                    return null;
                }
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("OnlineServices.SyncActive: " + e.Message);
                return e.Message;
            }
            finally { _syncing = false; }
        }

        async Awaitable RefreshMe(LocalProfile p)
        {
            var me = await Client.Get<ProfileResponse>("/api/profiles/me", p.token);
            Observe(me);
            if (me.Ok && me.Data != null) Profiles.Apply(p.localId, me.Data.playerId, null, me.Data.name, me.Data.tag, me.Data.rating);
        }

        /// <summary>A new guest profile, made active. Online it is registered first (a refused name is not kept); offline
        /// it is kept locally and registered later. Returns a player-facing error, or null.</summary>
        public async Awaitable<string> CreateProfile(string rawName)
        {
            var name = OnlineRules.CleanName(rawName, out var error);
            if (name == null) return error;
            if (await EnsureConnected())
            {
                var created = await Client.Post<CreateProfileResponse>("/api/profiles", new NameRequest { name = name });
                Observe(created);
                if (created.Ok && created.Data != null)
                {
                    var p = Profiles.Add(created.Data.name);
                    Profiles.Apply(p.localId, created.Data.playerId, created.Data.token, created.Data.name, created.Data.tag, 1000);
                    Profiles.Rename(p.localId, created.Data.name, true);
                    return null;
                }
                if (created.Outcome == ApiOutcome.Rejected) return created.Message ?? "That name was refused.";
            }
            Profiles.Add(name); // offline: registered on the next online launch
            return null;
        }

        /// <summary>Renames the active profile (sent now when online, else on the next online launch).</summary>
        public async Awaitable<string> RenameActive(string rawName)
        {
            var p = Active;
            if (p == null) return "No profile.";
            var name = OnlineRules.CleanName(rawName, out var error);
            if (name == null) return error;
            Profiles.Rename(p.localId, name, false);
            if (!await EnsureConnected()) return null;
            return await SyncActive();
        }

        public async Awaitable<string> SwitchTo(string localId)
        {
            if (!Profiles.SetActive(localId)) return "No such profile.";
            if (!IsOnline) return null;
            return await SyncActive();
        }

        /// <summary>Forgets a profile on this device (the server keeps it; its token is gone from here).</summary>
        public void Forget(string localId) => Profiles.Remove(localId);

        /// <summary>This week's board (or last week's final one); the player's row when the active profile is registered.</summary>
        public async Awaitable<ApiResult<LeaderboardResponse>> Leaderboard(bool previous, int limit = 0)
        {
            await EnsureConnected();
            var s = Settings;
            var n = Mathf.Clamp(limit > 0 ? limit : s.leaderboardLimit, 1, 100);
            var path = (previous ? "/api/leaderboard/weekly/previous" : "/api/leaderboard/weekly") +
                       "?mapId=" + Uri.EscapeDataString(string.IsNullOrEmpty(s.mapId) ? "saltwind" : s.mapId) + "&limit=" + n;
            var p = Active;
            var r = await Client.Get<LeaderboardResponse>(path, p != null && p.Registered ? p.token : null);
            Observe(r);
            return r;
        }

        /// <summary>Submits a finished run for the weekly board and reads the player's rank. Never throws.</summary>
        public async Awaitable<RunSubmission> SubmitRun(RunSubmitRequest run)
        {
            var outcome = new RunSubmission { Line = "Not ranked: offline" };
            try
            {
                if (run == null) return outcome;
                if (string.IsNullOrEmpty(run.mapId)) run.mapId = string.IsNullOrEmpty(Settings.mapId) ? "saltwind" : Settings.mapId;
                if (string.IsNullOrEmpty(run.clientVersion)) run.clientVersion = Application.version;
                if (!await EnsureConnected()) return outcome;
                if (Active == null) Profiles.Add(OnlineRules.DefaultName(Environment.TickCount));
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    var p = Active;
                    if (!p.Registered)
                    {
                        var error = await SyncActive();
                        if (!p.Registered) { outcome.Line = IsOnline ? "Not ranked: " + (error ?? "no profile") : "Not ranked: offline"; return outcome; }
                    }
                    var r = await Client.Post<RunSubmitResponse>("/api/runs", new RawJsonBody(run.ToJson()), p.token); // V5 M9a: with the run log
                    Observe(r);
                    if (r.Status == 401) { Profiles.Unregister(p.localId); continue; }
                    if (r.IsOffline) return outcome;
                    if (!r.Ok || r.Data == null)
                    {
                        outcome.Line = r.Status == 422
                            ? (run.runSeconds < 10 ? "Not ranked: run too short" : "Not ranked: run not accepted")
                            : "Not ranked: " + (r.Message ?? "server error");
                        return outcome;
                    }
                    outcome.NewWeeklyBest = r.Data.isNewWeeklyBest;
                    outcome.BestKills = r.Data.weeklyBest != null ? r.Data.weeklyBest.kills : run.kills;
                    var board = await Leaderboard(false, 1);
                    if (board.Ok && board.Data != null && board.Data.HasMe)
                    {
                        outcome.Ranked = true;
                        outcome.Rank = board.Data.me.rank;
                        outcome.BestKills = board.Data.me.kills;
                        outcome.Line = $"Weekly rank #{outcome.Rank} (best {outcome.BestKills} kills)" + (outcome.NewWeeklyBest ? ". New weekly best!" : string.Empty);
                    }
                    else outcome.Line = $"Run saved (best {outcome.BestKills} kills this week)";
                    return outcome;
                }
                return outcome;
            }
            catch (Exception e)
            {
                Debug.LogWarning("OnlineServices.SubmitRun: " + e.Message);
                return outcome;
            }
        }
    }
}
