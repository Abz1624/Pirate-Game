using UnityEngine;

namespace RoguePirates.V4.Config
{

    /// <summary>Run-level settings for the V4 composition root.</summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Run Config", fileName = "RunConfig")]
    public sealed class RunConfig : V4ConfigAsset
    {
        [Tooltip("V4 module ticks per second (fixed step).")] [AppliesFrom(0, "read when the run starts")] public float tickRate = 60f;
        [Tooltip("At most this many ticks per rendered frame; the rest is dropped.")] [AppliesFrom(0, "read when the run starts")] public int maxStepsPerFrame = 5;
        [Tooltip("V5 (demo baseline, 30 Sep 2026): the ship's top speed and acceleration, times this, on top of its move-speed stat. V3/V4: 1.")]
        [AppliesFrom(0, "read when the run starts")] [Min(0.1f)] public float shipSpeedMultiplier = 1f;

        [Header("Island impact (V5, owner 1 Oct 2026; all 0 = V3: slide along, no damage)")]
        [Tooltip("Only hits faster than this (m/s into the shore) bounce and hurt.")] [AppliesFrom(0, "read when the run starts")] [Min(0f)] public float islandImpactMinSpeed = 2f;
        [Tooltip("Share of the impact speed given back as a bounce.")] [AppliesFrom(0, "read when the run starts")] [Min(0f)] public float islandBounceRestitution = 0.6f;
        [Tooltip("Fixed push-off speed (m/s) added to every bounce.")] [AppliesFrom(0, "read when the run starts")] [Min(0f)] public float islandBouncePushSpeed = 3f;
        [Tooltip("Hull lost per impact.")] [AppliesFrom(0, "read when the run starts")] [Min(0f)] public float islandImpactDamage = 5f;
        [Tooltip("Seconds before another impact can bounce.")] [AppliesFrom(0, "read when the run starts")] [Min(0f)] public float islandImpactCooldown = 0.75f;
        [Tooltip("Seconds before another impact can cost hull (holding the stick into a shore costs at most this often).")] [AppliesFrom(0, "read when the run starts")] [Min(0f)] public float islandDamageCooldown = 2f;
        [Tooltip("Seconds the bounce's push takes to fade out.")] [AppliesFrom(0, "read when the run starts")] [Min(0.05f)] public float islandBounceSeconds = 0.5f;

        [Header("Enemy leash (V5, owner 10 Oct 2026)")]
        [Tooltip("Where an ordinary enemy left 45 m or more behind is put back: this far from the ship, ahead of it (within 75 " +
                 "degrees of its heading), every half second. The top-down camera (25 m up, 20 m back, 50 degree field of view) " +
                 "sees 30.5 m ahead of the ship at the top of the screen, so 31 m is just outside the view there (owner, 10 Oct; " +
                 "was a 26 m constant, and leashed enemies popped in just inside the top edge). Below the 45 m leash distance.")]
        [Min(1f)] public float enemyLeashReturnDistance = EnemyLeashReturnDistanceDefault;

        /// <summary>The leash distance (an ordinary enemy this far from the ship is moved back); the return distance must stay below it.</summary>
        public const float EnemyLeashDistance = 45f;
        public const float EnemyLeashReturnDistanceDefault = 31f;

        public override void Validate(ConfigValidationReport report)
        {
            report.Positive(this, nameof(tickRate), tickRate);
            report.Positive(this, nameof(shipSpeedMultiplier), shipSpeedMultiplier);
            if (maxStepsPerFrame < 1) report.Error(this, nameof(maxStepsPerFrame), "must be at least 1");
            if (enemyLeashReturnDistance <= 0f || enemyLeashReturnDistance >= EnemyLeashDistance)
                report.Error(this, nameof(enemyLeashReturnDistance), $"must be above 0 and below the leash distance ({EnemyLeashDistance} m)");
        }
    }
}
