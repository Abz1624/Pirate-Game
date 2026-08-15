using System;
using System.Collections.Generic;
using UnityEngine;

namespace PirateGame.Enemies
{
    public enum WaveSpawnPattern
    {
        /// <summary>One spawn every interval until duration ends.</summary>
        Trickle,
        /// <summary>Spawn count enemies immediately, then wait duration.</summary>
        Burst,
        /// <summary>Spawn count increases each tick (survivor density ramp).</summary>
        Ramp,
        /// <summary>No spawns — breathing room between waves.</summary>
        Calm,
        /// <summary>Fast ring spawns around the player for a short window.</summary>
        Swarm,
        /// <summary>Spawn a world boss once at step start.</summary>
        Boss
    }

    [Serializable]
    public class WaveStep
    {
        [Tooltip("Seconds from run start when this step begins.")]
        public float startTime;

        [Tooltip("How long this step lasts. Ignored for Burst (fires once).")]
        public float duration = 30f;

        public WaveSpawnPattern pattern = WaveSpawnPattern.Trickle;

        [Min(0.5f)]
        public float spawnInterval = 2.5f;

        [Min(1)]
        public int spawnCount = 1;

        [Tooltip("Added to spawner count per Ramp tick.")]
        public int rampIncrement = 1;

        [Tooltip("Max enemies alive cap override for this step. 0 = use spawner default.")]
        public int maxAliveOverride;

        [Tooltip("Multiplies spawner health for this step.")]
        public float healthMultiplier = 1f;

        [Tooltip("Optional label for HUD / debug.")]
        public string label = "Wave";

        [Tooltip("Boss model key without extension, e.g. boss_kraken. Used when pattern is Boss.")]
        public string bossId = "boss_kraken";
    }

    /// <summary>
    /// Ordered survivor-style wave schedule. Create via Assets → Pirate Game → Wave Timeline.
    /// </summary>
    [CreateAssetMenu(fileName = "WaveTimeline", menuName = "Pirate Game/Wave Timeline", order = 1)]
    public class WaveTimeline : ScriptableObject
    {
        [Tooltip("Total run length in seconds (for HUD timer).")]
        public float runDurationSeconds = 480f;

        public List<WaveStep> steps = new();

        public WaveStep GetStepAtTime(float runTime)
        {
            WaveStep active = null;
            foreach (var step in steps)
            {
                if (step == null) continue;
                if (runTime >= step.startTime)
                    active = step;
                else
                    break;
            }
            return active;
        }

        public static WaveTimeline CreateDefaultShallows()
        {
            var timeline = CreateInstance<WaveTimeline>();
            timeline.runDurationSeconds = 600f;
            timeline.steps = new List<WaveStep>
            {
                new() { startTime = 0f, duration = 60f, pattern = WaveSpawnPattern.Trickle, spawnInterval = 4f, spawnCount = 2, maxAliveOverride = 14, label = "Calm waters" },
                new() { startTime = 60f, duration = 60f, pattern = WaveSpawnPattern.Trickle, spawnInterval = 3.2f, spawnCount = 2, maxAliveOverride = 18, label = "First scouts" },
                new() { startTime = 120f, duration = 60f, pattern = WaveSpawnPattern.Trickle, spawnInterval = 2.8f, spawnCount = 3, maxAliveOverride = 24, label = "Gathering tide" },
                new() { startTime = 180f, duration = 20f, pattern = WaveSpawnPattern.Burst, spawnCount = 6, spawnInterval = 1f, maxAliveOverride = 28, label = "Skirmish" },
                new() { startTime = 200f, duration = 25f, pattern = WaveSpawnPattern.Calm, label = "Breather" },
                new() { startTime = 225f, duration = 90f, pattern = WaveSpawnPattern.Ramp, spawnInterval = 3f, spawnCount = 2, rampIncrement = 1, maxAliveOverride = 32, label = "Rising swell" },
                new() { startTime = 315f, duration = 60f, pattern = WaveSpawnPattern.Trickle, spawnInterval = 2.2f, spawnCount = 3, maxAliveOverride = 36, healthMultiplier = 1.15f, label = "Hunting pack" },
                new() { startTime = 375f, duration = 30f, pattern = WaveSpawnPattern.Swarm, spawnInterval = 1f, spawnCount = 4, maxAliveOverride = 40, healthMultiplier = 1.1f, label = "Surge" },
                new() { startTime = 405f, duration = 25f, pattern = WaveSpawnPattern.Calm, label = "Before the deep" },
                new() { startTime = 430f, duration = 90f, pattern = WaveSpawnPattern.Boss, bossId = "boss_kraken", label = "Kraken rises" },
                new() { startTime = 520f, duration = 50f, pattern = WaveSpawnPattern.Swarm, spawnInterval = 0.9f, spawnCount = 4, maxAliveOverride = 42, healthMultiplier = 1.25f, label = "Aftermath" },
                new() { startTime = 570f, duration = 30f, pattern = WaveSpawnPattern.Boss, bossId = "boss_megalodon", label = "Megalodon" }
            };
            return timeline;
        }
    }
}
