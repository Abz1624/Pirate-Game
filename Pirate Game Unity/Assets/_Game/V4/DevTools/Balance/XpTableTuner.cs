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
    /// in the first minute as before, fewer from minute 2 on; review: × 5 s gave minute 1 more level-ups, not fewer). The fitted table is in
    /// <see cref="ProgressionConfig.xpToNextTable"/>; V5 M8z (I8z.7) retired the in-engine simulator that fitted it, so only
    /// the targets stay here (the balance model is plan/playtest/montecarlo).
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
    }
}
