using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoguePirates.V4.Difficulty
{
    /// <summary>
    /// One finished run for the kill leaderboard (M11, gameplay doc §5.3): kills beside the run's average and peak
    /// Pressure, the director version and mode, and the config-set hash. Ranked only in Adaptive mode with the published
    /// ranked version, no forced Pressure and no live tuning edits.
    /// </summary>
    [Serializable]
    public sealed class RunScoreRecord
    {
        public int seed;
        public int kills;
        public float runSeconds;
        public float averagePressure = 1f;
        public float peakPressure = 1f;
        public string directorVersion = string.Empty;
        public string directorMode = string.Empty;
        public string configHash = string.Empty;
        public bool ranked;
        public bool pressureForced;
        public int tuningEdits;
        public string finishedUtc = string.Empty;
    }

    public interface IRunRecordStore
    {
        void Add(RunScoreRecord record);
        IReadOnlyList<RunScoreRecord> Records { get; }
    }

    /// <summary>The last <see cref="MaxRecords"/> runs in a V4-owned PlayerPrefs key (JSON).</summary>
    public sealed class PlayerPrefsRunRecordStore : IRunRecordStore
    {
        public const string Key = "v4_run_records";
        public const int MaxRecords = 50;

        [Serializable] sealed class Wrapper { public List<RunScoreRecord> records = new List<RunScoreRecord>(); }

        public IReadOnlyList<RunScoreRecord> Records => Load().records;

        public void Add(RunScoreRecord record)
        {
            if (record == null) return;
            var w = Load();
            w.records.Add(record);
            while (w.records.Count > MaxRecords) w.records.RemoveAt(0);
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(w));
            PlayerPrefs.Save();
        }

        static Wrapper Load()
        {
            var json = PlayerPrefs.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(json)) return new Wrapper();
            try { return JsonUtility.FromJson<Wrapper>(json) ?? new Wrapper(); }
            catch (Exception) { return new Wrapper(); } // a corrupt key never blocks the run end
        }
    }
}
