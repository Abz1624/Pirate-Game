using HighSeas.Core;
using HighSeas.Data;
using RoguePirates.Simulation.Balance;

namespace HighSeas.Balance
{
    /// <summary>Tracks Marks settlement inputs during a solo Saltwind run.</summary>
    public static class RunMarksTracker
    {
        static bool _active;
        static string _runId;
        static int _currentMinute;
        static int _killsThisMinute;
        static int _overtimeMinuteIndex;
        static int _killsThisOvertimeMinute;
        static int _qualifyingOrdinaryMinutes;
        static int _specialPoiCredits;
        static int _scheduledEliteKillCredits;
        static bool _bossVictoryCredited;
        static int _qualifyingOvertimeMinutes;
        static readonly System.Collections.Generic.HashSet<int> _specialPoiIndices = new();
        static readonly System.Collections.Generic.HashSet<string> _scheduledEliteKillKeys = new();

        public static bool IsActive => _active;
        public static string RunId => _runId;

        public static void BeginRun(string runId)
        {
            Reset();
            _active = StageBalanceProfileContext.IsActive;
            _runId = runId ?? string.Empty;
        }

        public static void Reset()
        {
            _active = false;
            _runId = string.Empty;
            _currentMinute = 0;
            _killsThisMinute = 0;
            _overtimeMinuteIndex = -1;
            _killsThisOvertimeMinute = 0;
            _qualifyingOrdinaryMinutes = 0;
            _specialPoiCredits = 0;
            _scheduledEliteKillCredits = 0;
            _bossVictoryCredited = false;
            _qualifyingOvertimeMinutes = 0;
            _specialPoiIndices.Clear();
            _scheduledEliteKillKeys.Clear();
        }

        public static void NotifyKill()
        {
            if (!_active) return;
            _killsThisMinute++;
            if (IsInOvertime())
                _killsThisOvertimeMinute++;
        }

        public static void Tick(float elapsedSeconds)
        {
            if (!_active) return;
            var rm = RunManager.Instance;
            if (rm != null && (rm.IsPaused || !rm.IsGameplayActive)) return;

            var minute = (int)(elapsedSeconds / 60f);
            while (_currentMinute < minute)
            {
                CloseMinute(_currentMinute);
                _currentMinute++;
                _killsThisMinute = 0;
            }

            if (!IsInOvertime()) return;

            var export = StageBalanceProfileContext.Export;
            var deadline = export != null
                ? SaltwindFinaleLogic.ResolveStageDeadlineSeconds(export)
                : GameConstants.StageDurationSeconds;
            var otMinute = (int)((elapsedSeconds - deadline) / 60f);
            if (otMinute < 0) return;

            while (_overtimeMinuteIndex < otMinute)
            {
                if (_overtimeMinuteIndex >= 0)
                    TryCreditOvertimeMinute();
                _overtimeMinuteIndex++;
                _killsThisOvertimeMinute = 0;
            }
        }

        static void CloseMinute(int completedMinute)
        {
            if (completedMinute < 0) return;

            var export = StageBalanceProfileContext.Export;
            var deadline = export != null
                ? SaltwindFinaleLogic.ResolveStageDeadlineSeconds(export)
                : GameConstants.StageDurationSeconds;
            var minuteEndSeconds = (completedMinute + 1) * 60f;
            if (minuteEndSeconds > deadline)
                return;

            var minKills = export?.Economy?.MarksPerQualifyingMinuteMinKills ?? 10;
            if (SaltwindStageSettlementLogic.QualifiesOrdinaryMinute(_killsThisMinute, minKills))
                _qualifyingOrdinaryMinutes++;
        }

        static void TryCreditOvertimeMinute()
        {
            var export = StageBalanceProfileContext.Export;
            var minKills = export?.Economy?.MarksPerQualifyingMinuteMinKills ?? 10;
            if (!SaltwindStageSettlementLogic.QualifiesOvertimeMinute(_killsThisOvertimeMinute, minKills))
                return;

            var capMinutes = export?.Economy?.MarksOvertimeCap ?? 15;
            var perMinute = export?.Economy?.MarksPerOvertimeMinute ?? 3;
            var maxMinutes = perMinute > 0 ? capMinutes / perMinute : 0;
            if (_qualifyingOvertimeMinutes >= maxMinutes)
                return;

            _qualifyingOvertimeMinutes++;
        }

        static bool IsInOvertime()
        {
            var rm = RunManager.Instance;
            if (rm == null) return false;
            return rm.IsOvertime;
        }

        public static bool TryRegisterSpecialPoi(int poiIndex, POIType type)
        {
            if (!_active) return false;
            if (type is POIType.SalvageChest or POIType.BossBeacon) return false;
            if (!_specialPoiIndices.Add(poiIndex)) return false;
            _specialPoiCredits++;
            return true;
        }

        /// <summary>Credits Marks for a scheduled pressure elite when it is killed (not when spawned).</summary>
        public static bool TryCreditScheduledEliteKill(string enemyDefinitionId)
        {
            if (!_active || string.IsNullOrEmpty(enemyDefinitionId)) return false;
            if (enemyDefinitionId != "hammerhead" && enemyDefinitionId != "kraken_spawn") return false;
            if (!_scheduledEliteKillKeys.Add(enemyDefinitionId)) return false;
            if (_scheduledEliteKillCredits >= 2) return false;
            _scheduledEliteKillCredits++;
            return true;
        }

        public static bool TryRegisterBossVictory()
        {
            if (!_active || _bossVictoryCredited) return false;
            _bossVictoryCredited = true;
            return true;
        }

        /// <summary>Read-only snapshot for settlement and UI. Does not mutate credit state.</summary>
        public static RunMarksRecord BuildRecord()
        {
            return new RunMarksRecord(
                _runId,
                _qualifyingOrdinaryMinutes,
                _specialPoiCredits,
                _scheduledEliteKillCredits,
                _bossVictoryCredited,
                _qualifyingOvertimeMinutes);
        }

        public static void WriteToSnapshot(HighSeas.Save.RunSnapshotData data)
        {
            if (data == null) return;
            var record = BuildRecord();
            data.runId = record.RunId;
            data.marksQualifyingOrdinaryMinutes = record.QualifyingOrdinaryMinutes;
            data.marksSpecialPoiCredits = record.SpecialPoiCredits;
            data.marksScheduledEliteKillCredits = record.ScheduledEliteKillCredits;
            data.marksBossVictoryCredited = record.BossVictoryCredited;
            data.marksQualifyingOvertimeMinutes = record.QualifyingOvertimeMinutes;
        }
    }
}
