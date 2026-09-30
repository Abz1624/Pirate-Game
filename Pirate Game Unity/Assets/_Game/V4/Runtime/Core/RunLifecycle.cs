using System;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Config;

namespace RoguePirates.V4
{
    /// <summary>The run facts the lifecycle reads and the state it sets (implemented over the shared run manager until M15).</summary>
    public interface IRunLifecycleHost
    {
        bool Exists { get; }
        RunState State { get; }
        string RunId { get; }
        bool ObjectiveSecured { get; }
        void SetState(RunState state);
        /// <summary>Records the first conquest (the shared unlock key the menu and the Captain locks read).</summary>
        void MarkFirstConquestComplete();
    }

    /// <summary>
    /// The run's start tracking, damage record and death flow (M14; was LegacyRunLifecycle, V3RunBootstrap.OnPlayerDied's
    /// solo branch). Death: pending level-ups cancelled, the ship stopped, a same-moment boss kill secured, the conquest
    /// recorded, PlayerDead → Rum settled once (the reward module) → RunComplete, then the telemetry and the toasts. V4 runs
    /// are solo: V3's PvP branch is gone. The V3 statics V3 cleared here are only read by V3 systems the V4 scenes no
    /// longer contain.
    /// </summary>
    public sealed class RunLifecycle
    {
        readonly IRunLifecycleHost _run;
        readonly Telemetry.RunTelemetry _telemetry;
        readonly Rewards.RewardModule _rewards;

        public RunLifecycle(IRunLifecycleHost run, Telemetry.RunTelemetry telemetry, Rewards.RewardModule rewards)
        {
            _run = run;
            _telemetry = telemetry;
            _rewards = rewards;
        }

        public Action<string, float> Toast;
        /// <summary>Player damage feedback (damage numbers, hit flash, on-damage relics): once per hit or steady report.</summary>
        public Action<float> DamageFeedback;
        public Action<bool> SetShipMovement;
        /// <summary>The stage profile the run started with (id, balance version, content hash) for the telemetry start event.</summary>
        public Func<(string id, string version, string hash)?> StageProfile;

        /// <summary>The voyage toast at run start (M14.14; was LegacyPresentationAdapter.ShowStartToasts, V3's wording).</summary>
        public void ShowStartToast(bool firstRun, int seed, bool homeSeedLocked)
        {
            if (RoguePirates.V4.Foundation.Core.DemoMode.HideVoyageToast) return; // demo: no seed or chest/Beacon message
            var seedLabel = firstRun && homeSeedLocked ? "locked" : "random";
            Toast?.Invoke(
                firstRun
                    ? $"Saltwind voyage ({seedLabel} seed {seed}).\nFind salvage chests as you bank gold, secure the Beacon."
                    : $"New voyage (seed {seed}).\nIslands, chests, and spawn rerolled.",
                firstRun ? 6f : 5f);
        }

        public void BeginRunTracking()
        {
            var profile = StageProfile?.Invoke();
            if (profile.HasValue) _telemetry?.RecordStageProfileStart(profile.Value.id, profile.Value.version, profile.Value.hash, _run?.RunId);
        }

        public void ApplyConquestUnlockIfSecured()
        {
            if (_run != null && _run.Exists && _run.ObjectiveSecured) _run.MarkFirstConquestComplete();
        }

        public void RecordDamage(float amount)
        {
            DamageFeedback?.Invoke(amount);
            _telemetry?.RecordDamageTaken(amount);
        }

        public void OnPlayerDied(Action trySecureIfBossDead, Action cancelPendingLevelUps)
        {
            cancelPendingLevelUps?.Invoke();
            SetShipMovement?.Invoke(false);
            trySecureIfBossDead?.Invoke();
            ApplyConquestUnlockIfSecured();
            if (_run == null || !_run.Exists || _run.State == RunState.RunComplete) return; // V3: FinalizeRunOnDeath ran once
            _run.SetState(RunState.PlayerDead);
            var rum = _rewards != null ? _rewards.Settle(_run.RunId, true) : 0;
            if (_rewards != null && _rewards.SettlementApplied)
                _telemetry?.RecordMarksSettlement(rum, true, rum,
                    _rewards.SettledRumOf(RumSource.QualifyingMinute), _rewards.SettledRumOf(RumSource.SpecialLandmark),
                    _rewards.SettledRumOf(RumSource.ScheduledElite), _rewards.SettledRumOf(RumSource.BossVictory),
                    _rewards.SettledRumOf(RumSource.OvertimeMinute)); // (the export has no slot for the gold row: the total includes it)
            _run.SetState(RunState.RunComplete); // run gold itself is not kept; the "gold" Rum row converts what is left
            _telemetry?.RecordPlayerDeath(_run.ObjectiveSecured);
            Toast?.Invoke("Run ended.", 4f);
            if (_rewards != null && _rewards.SettlementApplied) Toast?.Invoke($"{_rewards.CurrencyName} settled: +{rum}", 4f);
        }
    }
}
