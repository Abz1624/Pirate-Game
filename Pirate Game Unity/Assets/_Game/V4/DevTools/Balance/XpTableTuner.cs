using System;
using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Config;
using RoguePirates.V4.Progression;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Balance
{
    /// <summary>
    /// V5 M3c (owner, 1 Oct 2026: "1 1 2 3 5"): the level-up rhythm. The first level comes with the first kill; the gaps
    /// after it follow the Fibonacci series × 10 s (10, 10, 20, 30, 50 s), then 60 s each (about 13 level-ups by 10:00; 4
    /// in the first minute as before, fewer from minute 2 on; review: × 5 s gave minute 1 more level-ups, not fewer). <see cref="Tune"/> fits a per-level XP table
    /// (<see cref="ProgressionConfig.xpToNextTable"/>) to those gaps in the simulator: the baseline personality's median
    /// time to each level against its target, corrected on the cumulative XP a level needs, damped, a few rounds.
    /// </summary>
    public static class XpTableTuner
    {
        /// <summary>Fibonacci × 10 s for the five gaps after the first level, then +10 s a level, capped at 60 s.</summary>
        public static readonly float[] FibonacciStart = { 10f, 10f, 20f, 30f, 50f };
        public const float GapStep = 10f, GapCap = 60f;
        /// <summary>The first level comes with the first kill; its typical time (s) for the report's target column.</summary>
        public const float FirstKillSeconds = 12f;

        /// <summary>The target gap before level <paramref name="level"/> (level 1: the first kill; level 2 on: the series).</summary>
        public static float TargetGap(int level)
        {
            if (level < 1) return 0f;
            if (level == 1) return FirstKillSeconds;
            var i = level - 2;
            if (i < FibonacciStart.Length) return FibonacciStart[i];
            return Mathf.Min(GapCap, FibonacciStart[FibonacciStart.Length - 1] + GapStep * (i - FibonacciStart.Length + 1));
        }

        /// <summary>The target run time (s) of level <paramref name="level"/>, from a first level at <paramref name="first"/>.</summary>
        public static float TargetTime(int level, float first = FirstKillSeconds)
        {
            if (level < 1) return 0f;
            var t = first;
            for (var l = 2; l <= level; l++) t += TargetGap(l);
            return t;
        }

        /// <summary>
        /// The pace targets for the rhythm (path, value, value before M3c), each about ±20% (at least ±1 level) around the
        /// target times (level 1 at ~12 s, then 10, 10, 20, 30, 50, 60... s): the level at the end of each minute (4 at 1:00,
        /// 13 at 10:00), the Steady Captain's level at 10:00, the gaps in minute 1 (4 levels: 15 s) and over 0:00–10:00
        /// (13 levels: ~46 s).
        /// </summary>
        public static readonly (string path, string value, string pre)[] TargetEdits =
        {
            ("levelUpGapMinute1.min", "12", "12"), ("levelUpGapMinute1.max", "18", "18"),
            ("levelUpGapTo10.min", "37", "20"), ("levelUpGapTo10.max", "55", "30"),
            ("personalities[3].levelAt10.min", "10", "21"), ("personalities[3].levelAt10.max", "16", "35"),
            ("minutes[0].level.min", "3", "3"), ("minutes[0].level.max", "5", "9"),
            ("minutes[1].level.min", "4", "6"), ("minutes[1].level.max", "6", "13"),
            ("minutes[2].level.min", "5", "9"), ("minutes[2].level.max", "7", "17"),
            ("minutes[3].level.min", "6", "12"), ("minutes[3].level.max", "8", "20"),
            ("minutes[4].level.min", "7", "14"), ("minutes[4].level.max", "10", "23"),
            ("minutes[5].level.min", "7", "16"), ("minutes[5].level.max", "11", "26"),
            ("minutes[6].level.min", "8", "15"), ("minutes[6].level.max", "12", "26"),
            ("minutes[7].level.min", "9", "17"), ("minutes[7].level.max", "13", "29"),
            ("minutes[8].level.min", "10", "18"), ("minutes[8].level.max", "14", "31"),
            ("minutes[9].level.min", "10", "20"), ("minutes[9].level.max", "16", "33"),
            ("minutes[10].level.min", "11", "21"), ("minutes[10].level.max", "17", "35"),
            ("minutes[11].level.min", "12", "22"), ("minutes[11].level.max", "18", "37"),
            ("minutes[12].level.min", "13", "23"), ("minutes[12].level.max", "19", "39"),
        };

        public sealed class Round
        {
            public int[] Table;
            /// <summary>Per level (index = level): median time reached, or NaN when too few runs reached it.</summary>
            public float[] MedianTime;
            /// <summary>Mean |median time / target time − 1| over levels 2.. (an unreached level counts 1).</summary>
            public float MeanTimeError;
            public float WorstGapError;
        }

        /// <summary>The voyage the rhythm is fitted over (levels whose target time is later are not scored).</summary>
        public const float HorizonSeconds = 600f;

        /// <summary>The round's score: the mean relative error of each level's time against its target, over the levels
        /// due within the voyage (an unreached one counts 1).</summary>
        public static float MeanTimeError(float[] median, int levels)
        {
            var first = float.IsNaN(median[1]) ? FirstKillSeconds : median[1];
            var sum = 0f;
            var n = 0;
            for (var l = 2; l <= levels && TargetTime(l, first) <= HorizonSeconds; l++, n++)
                sum += float.IsNaN(median[l]) ? 1f : Mathf.Abs(median[l] / TargetTime(l, first) - 1f);
            return n > 0 ? sum / n : 0f;
        }

        /// <summary>Median time to each level over the baseline's runs (NaN where under 40% of runs got there).</summary>
        public static float[] MedianLevelTimes(SimContent c, PlayerPersonality p, int runs, int seed, int levels)
        {
            var times = new List<float>[levels + 1];
            for (var l = 0; l <= levels; l++) times[l] = new List<float>();
            for (var i = 0; i < runs; i++)
            {
                var captain = c.Captains.Count > 0 ? c.Captains[i % c.Captains.Count].captainKey : null;
                var run = new SimRun(c, p, SimBatch.RunSeed(seed, p.personalityId + ":xp", i), captain) { Unsinkable = true };
                var r = run.Play();
                for (var l = 1; l <= levels && l - 1 < r.LevelUpTimes.Count; l++) times[l].Add(r.LevelUpTimes[l - 1]);
            }
            var result = new float[levels + 1];
            for (var l = 1; l <= levels; l++)
                result[l] = times[l].Count >= Math.Max(3, runs * 2 / 5) ? SimBatch.Percentile(times[l], 0.5f) : float.NaN;
            return result;
        }

        /// <summary>
        /// Fits the table on the content's (runtime) progression config and returns every round. The config keeps the
        /// last table. <paramref name="levels"/> levels are fitted; the table extrapolates past them.
        /// </summary>
        public static List<Round> Tune(SimContent c, int runs = 60, int seed = 11, int rounds = 8, int levels = 18, float converged = 0.06f)
        {
            var p = c.Personalities.Find(c.Targets != null ? c.Targets.baselinePersonalityId : "steady_captain");
            var prog = c.Progression;
            // Start from the curve (whatever table the config holds), so the fit is reproducible.
            prog.xpToNextTable = Array.Empty<int>();
            var table = new int[levels];
            for (var l = 0; l < levels; l++) table[l] = ProgressionMath.XpToNext(prog, l);
            table[0] = 1; // the first level with the first kill
            var log = new List<Round>();
            for (var round = 0; round < rounds; round++)
            {
                prog.xpToNextTable = (int[])table.Clone();
                var median = MedianLevelTimes(c, p, runs, seed, levels);
                var first = float.IsNaN(median[1]) ? FirstKillSeconds : median[1];
                var worst = 0f;
                for (var l = 1; l <= levels; l++)
                    if (!float.IsNaN(median[l]) && l >= 2)
                    {
                        var gap = median[l] - median[l - 1];
                        if (!float.IsNaN(gap)) worst = Mathf.Max(worst, Mathf.Abs(gap / TargetGap(l) - 1f));
                    }
                var error = MeanTimeError(median, levels);
                log.Add(new Round { Table = (int[])table.Clone(), MedianTime = median, WorstGapError = worst, MeanTimeError = error });
                if (error <= converged) break;
                // Cumulative XP to reach each level, corrected by how early or late the level came (damped).
                var cumulative = new double[levels + 1];
                for (var l = 1; l <= levels; l++) cumulative[l] = cumulative[l - 1] + table[l - 1];
                var corrected = new double[levels + 1];
                var lastRatio = 1.0;
                for (var l = 1; l <= levels; l++)
                {
                    var ratio = l == 1 ? 1.0 : float.IsNaN(median[l]) ? lastRatio : Math.Pow(TargetTime(l, first) / Math.Max(0.5, median[l]), 0.7);
                    ratio = Math.Max(0.4, Math.Min(2.5, ratio));
                    lastRatio = ratio;
                    corrected[l] = cumulative[l] * ratio;
                }
                for (var l = 1; l <= levels; l++)
                    table[l - 1] = Math.Max(1, (int)Math.Round(corrected[l] - corrected[l - 1]));
                // XP per level never shrinks past level 2 (the gaps grow; income grows too).
                for (var l = 2; l < levels; l++) table[l] = Math.Max(table[l], table[l - 1]);
            }
            // Keep the best round (smallest mean time error; the later one on a tie).
            var best = log.OrderBy(r => r.MeanTimeError).ThenByDescending(r => log.IndexOf(r)).First();
            // Keep only the levels the runs reached (unmeasured entries would set the extrapolation).
            var measured = levels;
            while (measured > 1 && float.IsNaN(best.MedianTime[measured])) measured--;
            best.Table = best.Table.Take(measured).ToArray();
            prog.xpToNextTable = (int[])best.Table.Clone();
            return log;
        }
    }
}
