using System;
using HighSeas.Balance;
using HighSeas.Combat;
using UnityEngine;
using HighSeas.Save;

namespace HighSeas.Core
{
    public sealed class RunManager : MonoBehaviour
    {
        public static RunManager Instance { get; private set; }

        public RunState State { get; private set; } = RunState.StageIntro;
        public PauseReason PauseReason { get; private set; } = PauseReason.None;
        public RunRng Rng { get; private set; }
        public PlayerStats Stats { get; private set; } = new();
        public int RunSeed { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public int StageIndex { get; private set; }
        public int Tier { get; private set; } = 1;
        public int BountyLevel { get; private set; }
        public int Gold { get; private set; }
        public int GrossGoldEarned { get; private set; }
        public int GrossGoldSpent { get; private set; }
        public int KillCount { get; private set; }
        public string RunId { get; private set; } = string.Empty;
        public int BossKills { get; private set; }
        public bool ObjectiveSecured { get; private set; }
        public bool LastRunEndedByDeath { get; private set; }
        public int BankedRunGold { get; private set; }
        public bool IsOvertime
        {
            get
            {
                var export = StageBalanceProfileContext.Export;
                if (export != null)
                    return ElapsedSeconds > SaltwindFinaleLogic.ResolveStageDeadlineSeconds(export);
                return ElapsedSeconds > GameConstants.StageDurationSeconds;
            }
        }
        public bool IsPaused => PauseReason != PauseReason.None;
        public bool IsRunEnded => State == RunState.PlayerDead || State == RunState.RunComplete;
        public bool IsGameplayActive => State == RunState.Playing || State == RunState.BossFight;
        public bool IsBossFightState() => State == RunState.BossFight;

        public event Action<RunState> StateChanged;
        public event Action<PauseReason> PauseChanged;
        public event Action<int> GoldChanged;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void BeginRun(int seed, int stageIndex = 0, int tier = 1)
        {
            RunSeed = seed == 0 ? Environment.TickCount : seed;
            RunId = Guid.NewGuid().ToString("N");
            Rng = new RunRng(RunSeed);
            Stats = new PlayerStats();
            StageIndex = stageIndex;
            Tier = tier;
            BountyLevel = 0;
            Gold = 0;
            GrossGoldEarned = 0;
            GrossGoldSpent = 0;
            KillCount = 0;
            BossKills = 0;
            ObjectiveSecured = false;
            BankedRunGold = 0;
            LastRunEndedByDeath = false;
            ElapsedSeconds = 0f;
            SetState(RunState.StageIntro);
            SetPause(PauseReason.None);
        }

        void Update()
        {
            if (State != RunState.Playing && State != RunState.BossFight) return;
            if (IsPaused) return;
            ElapsedSeconds += Time.deltaTime;
            RunMarksTracker.Tick(ElapsedSeconds);
        }

        public void EnterPlaying()
        {
            SetState(RunState.Playing);
            SetPause(PauseReason.None);
        }

        public void SetPause(PauseReason reason)
        {
            PauseReason = reason;
            PauseChanged?.Invoke(reason);
        }

        public void SetState(RunState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }

        public void AddGold(int amount)
        {
            if (IsRunEnded || amount <= 0) return;
            Gold += amount;
            GrossGoldEarned += amount;
            RunCombatTelemetry.Instance?.RecordGoldEarned(amount);
            GoldChanged?.Invoke(Gold);
        }

        public bool TrySpendGold(int amount)
        {
            if (IsRunEnded) return false;
            if (Gold < amount) return false;
            Gold -= amount;
            GrossGoldSpent += amount;
            RunCombatTelemetry.Instance?.RecordGoldSpent(amount);
            GoldChanged?.Invoke(Gold);
            return true;
        }

        public void RegisterKill(bool isBoss = false)
        {
            KillCount++;
            if (isBoss) BossKills++;
            RunMarksTracker.NotifyKill();
        }

        public float ThreatPerSecond => GameMath.ComputeThreatPerSecond(
            ElapsedSeconds, Tier, StageIndex, BountyLevel, IsOvertime);

        public void SecureObjective()
        {
            if (ObjectiveSecured) return;
            ObjectiveSecured = true;
            BossKills = Mathf.Max(BossKills, 1);
        }

        /// <summary>Menu suspend restore — does not recreate RNG or settle Marks.</summary>
        public void RestoreSuspendSnapshot(RunSnapshotData data)
        {
            if (data == null) return;
            if (!string.IsNullOrEmpty(data.runId))
                RunId = data.runId;
            ElapsedSeconds = data.elapsedSeconds;
            Gold = data.gold;
            KillCount = data.kills;
            ObjectiveSecured = data.objectiveSecured;
        }

        /// <summary>V3: bank all unspent run gold once at death; then show results.</summary>
        public void FinalizeRunOnDeath()
        {
            if (State == RunState.RunComplete) return;
            LastRunEndedByDeath = State == RunState.PlayerDead;
            BankedRunGold = Gold;
            var bankToTreasury = StageBalanceProfileContext.Export?.Economy?.BankRunGoldToDoubloons ?? true;
            if (BankedRunGold > 0 && bankToTreasury)
                AppRoot.Instance?.Save.AddDoubloons(BankedRunGold);
            if (StageBalanceProfileContext.IsActive)
                SaltwindRunSettlement.TrySettleCurrentRun(AppRoot.Instance?.Save);
            SetState(RunState.RunComplete);
        }

        public void CompleteRun(bool victory)
        {
            if (victory)
                SecureObjective();
            SetState(RunState.RunComplete);
            var payout = victory ? 25 + BossKills * 10 : 5 + KillCount / 20;
            if (BankedRunGold <= 0)
                AppRoot.Instance?.Save.AddDoubloons(payout);
        }
    }
}
