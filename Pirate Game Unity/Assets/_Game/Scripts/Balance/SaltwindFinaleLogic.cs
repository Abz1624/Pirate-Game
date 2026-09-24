using System;
using RoguePirates.Simulation.Balance;
using UnityEngine;

namespace HighSeas.Balance
{
    /// <summary>Deterministic Saltwind overtime/storm timing and safe-region helpers (C05).</summary>
    public static class SaltwindFinaleLogic
    {
        public static float ResolveStageDeadlineSeconds(StageBalanceProfileExport export) =>
            export?.Timing?.StageDeadlineSeconds ?? 600f;

        public static bool IsOvertime(float elapsedSeconds, StageBalanceProfileExport export) =>
            elapsedSeconds > ResolveStageDeadlineSeconds(export);

        public static bool IsStormDamageActive(float elapsedSeconds, StageBalanceProfileExport export)
        {
            if (export?.Timing == null) return false;
            return elapsedSeconds >= export.Timing.StormStartSeconds;
        }

        public static bool ShouldShowStormWarning(
            float elapsedSeconds,
            StageBalanceProfileExport export,
            bool warned)
        {
            if (export?.Timing == null || warned) return false;
            return elapsedSeconds >= export.Timing.StormStartSeconds - export.Timing.StormWarningLeadSeconds;
        }

        public static float GetStormSafeRegionScale(float elapsedSeconds, StageBalanceProfileExport export)
        {
            if (export?.Overtime == null) return 1f;
            var ot = export.Overtime;
            if (elapsedSeconds < ot.StormContractionStartSeconds)
                return ot.StormSafeRegionStartFraction;

            if (elapsedSeconds >= ot.StormContractionEndSeconds)
                return ot.StormSafeRegionEndFraction;

            var t = (elapsedSeconds - ot.StormContractionStartSeconds) /
                    Mathf.Max(0.001f, ot.StormContractionEndSeconds - ot.StormContractionStartSeconds);
            return Mathf.Lerp(ot.StormSafeRegionStartFraction, ot.StormSafeRegionEndFraction, t);
        }

        public static float GetOvertimeSpawnInterval(float elapsedSeconds, StageBalanceProfileExport export)
        {
            if (export == null) return 1.4f;
            var deadline = ResolveStageDeadlineSeconds(export);
            var overtimeSeconds = Mathf.Max(0f, elapsedSeconds - deadline);
            return StageBalanceProfileExportCodec.OvertimeSpawnInterval(export, overtimeSeconds);
        }

        public static float GetOvertimePursuerWeight(float elapsedSeconds, StageBalanceProfileExport export)
        {
            if (export?.Overtime == null) return 0.8f;
            var deadline = ResolveStageDeadlineSeconds(export);
            var overtimeMinutes = (elapsedSeconds - deadline) / 60f;
            return overtimeMinutes < 2f
                ? export.Overtime.PursuerWeightFirstTwoMinutes
                : export.Overtime.PursuerWeightAfterTwoMinutes;
        }

        const float OrdinaryScalingDelaySeconds = 120f;

        public static float GetOrdinaryTimeHpMultiplier(float elapsedSeconds)
        {
            if (elapsedSeconds < OrdinaryScalingDelaySeconds)
                return 1f;
            var minute = (elapsedSeconds - OrdinaryScalingDelaySeconds) / 60f;
            return 1f + 0.11f * minute + 0.022f * minute * minute;
        }

        /// <summary>Saltwind ordinary-phase contact/ranged threat (player out-scales base <see cref="GameMath.EnemyDamage"/> alone).</summary>
        public static float GetOrdinaryTimeDamageMultiplier(float elapsedSeconds)
        {
            if (elapsedSeconds < OrdinaryScalingDelaySeconds)
                return 1f;
            var minute = (elapsedSeconds - OrdinaryScalingDelaySeconds) / 60f;
            return 1f + 0.14f * minute + 0.028f * minute * minute;
        }

        public static float GetOvertimeEnemyHpMultiplier(float elapsedSeconds, StageBalanceProfileExport export)
        {
            if (export?.Overtime == null) return 1f;
            var deadline = ResolveStageDeadlineSeconds(export);
            var overtimeMinutes = Mathf.Max(0f, (elapsedSeconds - deadline) / 60f);
            return export.Overtime.HpMultiplierBase *
                   Mathf.Pow(2f, overtimeMinutes * export.Overtime.HpMultiplierExponentPerMinute);
        }

        public static float GetOvertimeEnemyDamageMultiplier(float elapsedSeconds, StageBalanceProfileExport export)
        {
            if (export?.Overtime == null) return 1f;
            var deadline = ResolveStageDeadlineSeconds(export);
            var overtimeMinutes = Mathf.Max(0f, (elapsedSeconds - deadline) / 60f);
            return 1f + overtimeMinutes * export.Overtime.DamageBonusPerMinute;
        }
    }

    public enum BarnacleKingAttackKind
    {
        None,
        AimedSpread,
        CommittedCharge,
        MarkedBombardment,
        ShellSlam
    }

    public enum BarnacleKingBossPhase
    {
        Recovery,
        Telegraph,
        Execute,
    }

    public struct BarnacleKingAttackState
    {
        public BarnacleKingAttackKind Attack;
        public BarnacleKingBossPhase Phase;
        public float PhaseTimer;
        public Vector3 CommittedDirection;
        public Vector3 MarkedPoint;
        public bool Enraged;
        public BarnacleKingAttackKind QueuedFollowUp;
        public int PatternIndex;
        public int ScriptPhase;
        public int RotationStep;
        public float PendingHpPhase;
        public bool Phase3ChargePassPending;
    }

    public static class BarnacleKingBossAttackLogic
    {
        public const float SpreadTelegraphSeconds = 1.2f;
        public const float ChargeTelegraphSeconds = 1.5f;
        public const float BombardTelegraphSeconds = 1.5f;
        public const float ChargeCommitSeconds = 0.4f;
        public const float SpreadRecoverySeconds = 2f;
        public const float ChargeRecoverySeconds = 2f;
        public const float BombardRecoverySeconds = 2.5f;
        public const float EnragedRecoveryScale = 0.75f;
        public const float MarkedBombardmentRadius = 5.5f;

        public static bool IsPlayerInsideMarkedBlast(Vector3 markedPoint, Vector3 playerPosition, float radius = MarkedBombardmentRadius)
        {
            var flat = playerPosition - markedPoint;
            flat.y = 0f;
            return flat.sqrMagnitude <= radius * radius;
        }

        public static bool ShouldApplyMarkedAreaDamage(in BarnacleKingAttackState state) =>
            state.Attack == BarnacleKingAttackKind.MarkedBombardment &&
            state.Phase == BarnacleKingBossPhase.Execute &&
            state.PhaseTimer > 0f;

        public static bool CanDealDamage(in BarnacleKingAttackState state) =>
            state.Phase == BarnacleKingBossPhase.Execute && state.PhaseTimer > 0f;

        public static bool IsMovementPaused(in BarnacleKingAttackState state) =>
            state.Phase == BarnacleKingBossPhase.Telegraph ||
            (state.Phase == BarnacleKingBossPhase.Recovery && state.PhaseTimer > 0.05f);

        public static bool IsChargeCommit(in BarnacleKingAttackState state) =>
            state.Attack == BarnacleKingAttackKind.CommittedCharge &&
            state.Phase == BarnacleKingBossPhase.Execute;

        public static void BeginRun(ref BarnacleKingAttackState state)
        {
            state = default;
            state.Phase = BarnacleKingBossPhase.Recovery;
            state.PhaseTimer = 0.5f;
        }

        public static void SetEnraged(ref BarnacleKingAttackState state, bool enraged) =>
            state.Enraged = enraged;

        public static void ApplyHealthPhase(ref BarnacleKingAttackState state, float hpRatio)
        {
            var target = hpRatio > 0.65f ? 1 : hpRatio > 0.3f ? 2 : 3;
            if (target > state.ScriptPhase && state.Phase == BarnacleKingBossPhase.Recovery)
            {
                state.PendingHpPhase = target;
                state.RotationStep = 0;
            }
            else if (state.ScriptPhase == 0)
                state.ScriptPhase = 1;
        }

        public static void Tick(
            ref BarnacleKingAttackState state,
            float dt,
            Vector3 bossPosition,
            Vector3 playerPosition)
        {
            if (state.PhaseTimer > 0f)
                state.PhaseTimer -= dt;

            if (state.PhaseTimer > 0f)
                return;

            switch (state.Phase)
            {
                case BarnacleKingBossPhase.Recovery:
                    if (state.PendingHpPhase > state.ScriptPhase)
                        state.ScriptPhase = Mathf.RoundToInt(state.PendingHpPhase);
                    BeginTelegraph(ref state, PickNextAttack(ref state), bossPosition, playerPosition);
                    break;
                case BarnacleKingBossPhase.Telegraph:
                    state.Phase = BarnacleKingBossPhase.Execute;
                    state.PhaseTimer = state.Attack switch
                    {
                        BarnacleKingAttackKind.CommittedCharge => ChargeCommitSeconds,
                        BarnacleKingAttackKind.MarkedBombardment => 0.35f,
                        _ => 0.25f
                    };
                    break;
                case BarnacleKingBossPhase.Execute:
                    EnterRecovery(ref state);
                    break;
            }
        }

        static readonly BarnacleKingAttackKind[] Phase1 =
        {
            BarnacleKingAttackKind.AimedSpread,
            BarnacleKingAttackKind.ShellSlam,
            BarnacleKingAttackKind.AimedSpread,
            BarnacleKingAttackKind.MarkedBombardment
        };

        static readonly BarnacleKingAttackKind[] Phase2 =
        {
            BarnacleKingAttackKind.AimedSpread,
            BarnacleKingAttackKind.MarkedBombardment,
            BarnacleKingAttackKind.CommittedCharge,
            BarnacleKingAttackKind.ShellSlam
        };

        static readonly BarnacleKingAttackKind[] Phase3 =
        {
            BarnacleKingAttackKind.CommittedCharge,
            BarnacleKingAttackKind.AimedSpread,
            BarnacleKingAttackKind.MarkedBombardment,
            BarnacleKingAttackKind.ShellSlam
        };

        static BarnacleKingAttackKind PickNextAttack(ref BarnacleKingAttackState state)
        {
            if (state.QueuedFollowUp != BarnacleKingAttackKind.None)
            {
                var follow = state.QueuedFollowUp;
                state.QueuedFollowUp = BarnacleKingAttackKind.None;
                return follow;
            }

            if (state.Phase3ChargePassPending)
            {
                state.Phase3ChargePassPending = false;
                return BarnacleKingAttackKind.CommittedCharge;
            }

            var script = state.ScriptPhase <= 1 ? Phase1 : state.ScriptPhase == 2 ? Phase2 : Phase3;
            var attack = script[state.RotationStep % script.Length];
            state.RotationStep++;

            if (state.ScriptPhase >= 3 && attack == BarnacleKingAttackKind.CommittedCharge)
                state.Phase3ChargePassPending = true;

            return attack;
        }

        static void BeginTelegraph(
            ref BarnacleKingAttackState state,
            BarnacleKingAttackKind attack,
            Vector3 bossPosition,
            Vector3 playerPosition)
        {
            state.Attack = attack;
            state.Phase = BarnacleKingBossPhase.Telegraph;
            state.PhaseTimer = attack switch
            {
                BarnacleKingAttackKind.AimedSpread => SpreadTelegraphSeconds,
                BarnacleKingAttackKind.CommittedCharge => ChargeTelegraphSeconds,
                BarnacleKingAttackKind.MarkedBombardment => BombardTelegraphSeconds,
                BarnacleKingAttackKind.ShellSlam => SpreadTelegraphSeconds,
                _ => 1f
            };

            var toPlayer = playerPosition - bossPosition;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.01f)
                toPlayer = Vector3.forward;
            state.CommittedDirection = toPlayer.normalized;
            state.MarkedPoint = playerPosition;
        }

        static void EnterRecovery(ref BarnacleKingAttackState state)
        {
            state.Phase = BarnacleKingBossPhase.Recovery;
            var recovery = state.Attack switch
            {
                BarnacleKingAttackKind.AimedSpread => SpreadRecoverySeconds,
                BarnacleKingAttackKind.CommittedCharge => ChargeRecoverySeconds,
                BarnacleKingAttackKind.MarkedBombardment => BombardRecoverySeconds,
                BarnacleKingAttackKind.ShellSlam => SpreadRecoverySeconds,
                _ => 1.5f
            };
            if (state.Attack != BarnacleKingAttackKind.ShellSlam
                && (state.Enraged || state.ScriptPhase >= 3))
                recovery *= EnragedRecoveryScale;
            state.PhaseTimer = recovery;
            state.Attack = BarnacleKingAttackKind.None;
        }
    }
}
