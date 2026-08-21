using System;
using UnityEngine;

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
        public int KillCount { get; private set; }
        public int BossKills { get; private set; }
        public bool IsOvertime => ElapsedSeconds > GameConstants.StageDurationSeconds;
        public bool IsPaused => PauseReason != PauseReason.None;

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
            Rng = new RunRng(RunSeed);
            Stats = new PlayerStats();
            StageIndex = stageIndex;
            Tier = tier;
            BountyLevel = 0;
            Gold = 0;
            KillCount = 0;
            BossKills = 0;
            ElapsedSeconds = 0f;
            SetState(RunState.StageIntro);
            SetPause(PauseReason.None);
        }

        void Update()
        {
            if (State != RunState.Playing && State != RunState.BossFight) return;
            if (IsPaused) return;
            ElapsedSeconds += Time.deltaTime;
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
            Gold += amount;
            GoldChanged?.Invoke(Gold);
        }

        public bool TrySpendGold(int amount)
        {
            if (Gold < amount) return false;
            Gold -= amount;
            GoldChanged?.Invoke(Gold);
            return true;
        }

        public void RegisterKill(bool isBoss = false)
        {
            KillCount++;
            if (isBoss) BossKills++;
        }

        public float ThreatPerSecond => GameMath.ComputeThreatPerSecond(
            ElapsedSeconds, Tier, StageIndex, BountyLevel, IsOvertime);

        public void CompleteRun(bool victory)
        {
            SetState(RunState.RunComplete);
            var payout = victory ? 25 + BossKills * 10 : 5 + KillCount / 20;
            AppRoot.Instance?.Save.AddDoubloons(payout);
        }
    }
}
