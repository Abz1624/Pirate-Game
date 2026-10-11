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

        [Header("Enemy leash (V5, owner 10 Oct 2026, D28)")]
        [Tooltip("An ordinary enemy left 45 m or more behind (and out of view) is put back every half second within 75 degrees " +
                 "of the ship's heading, just outside the run camera's view along that bearing: this far (m) past the view's edge, " +
                 "plus the enemy's own radius (covers the camera's follow lag, about 2 m at full boost, and the model past its hit " +
                 "radius). Was 31 m ahead (10 Oct), 26 m before: inside the view's top corners and sides.")]
        [AppliesFrom(0, "read at every leash check (half a second): applies at once")] [Min(0f)] public float enemyLeashViewMargin = EnemyLeashViewMarginDefault;
        [Tooltip("The run camera, as data (the match server has none): its height above the ship (V4SaltwindRun's FollowCamera " +
                 "offset y). A test checks it against the scene.")]
        [AppliesFrom(0, "read at every leash check (half a second): applies at once")] [Min(1f)] public float leashViewCameraHeight = LeashViewCameraHeightDefault;
        [Tooltip("The run camera's distance behind the ship (minus V4SaltwindRun's FollowCamera offset z); it looks at the ship.")]
        [AppliesFrom(0, "read at every leash check (half a second): applies at once")] [Min(0f)] public float leashViewCameraBack = LeashViewCameraBackDefault;
        [Tooltip("The run camera's vertical field of view (degrees; the scene's camera and BoostCameraZoom's resting value).")]
        [AppliesFrom(0, "read at every leash check (half a second): applies at once")] [Range(10f, 100f)] public float leashViewFieldOfView = LeashViewFieldOfViewDefault;
        [Tooltip("The widest screen the leash keeps enemies out of view on (width over height; 2.4 covers 16:9 to 21:9 " +
                 "ultrawides at 3440x1440). A narrower screen sees less of the sea.")]
        [AppliesFrom(0, "read at every leash check (half a second): applies at once")] [Range(1f, 3f)] public float leashViewMaxAspect = LeashViewMaxAspectDefault;
        [Tooltip("The follow camera trails the ship by about its speed times this (s; 1 / the scene FollowCamera's position " +
                 "smoothing): the view is also kept clear around that point.")]
        [AppliesFrom(0, "read at every leash check (half a second): applies at once")] [Min(0f)] public float leashViewLagSeconds = LeashViewLagSecondsDefault;
        [Tooltip("Online, the client draws its predicted ship ahead of the enemies it draws (its input delay plus the enemies' " +
                 "render delay: about 0.47 s at a 270 ms round trip): the view is also kept clear around the ship this far ahead (s).")]
        [AppliesFrom(0, "read at every leash check (half a second): applies at once")] [Min(0f)] public float leashViewLeadSeconds = LeashViewLeadSecondsDefault;

        [Tooltip("V5 M9b close (review; was a 45 m constant): an ordinary enemy this far (m) or more from the ship, and out of " +
                 "view, is moved back.")]
        [AppliesFrom(0, "read at every leash check (half a second): applies at once")] [Min(1f)] public float enemyLeashDistance = EnemyLeashDistanceDefault;
        [Tooltip("V5 M9b close (review; was a 10 m constant): an enemy is moved back only when it is this far (m) beyond where " +
                 "the leash would put it along its own bearing, so one just put back, sailing in, is not moved again.")]
        [AppliesFrom(0, "read at every leash check (half a second): applies at once")] [Min(0f)] public float enemyLeashHysteresis = EnemyLeashHysteresisDefault;

        public const float EnemyLeashDistanceDefault = 45f, EnemyLeashHysteresisDefault = 10f;
        /// <summary>Where the leash put enemies before D28 (26 m ahead, within 75 degrees of the heading): its fallback when
        /// the configured view has no footprint on the sea (a view reaching the horizon).</summary>
        public const float EnemyLeashFallbackReturnDistance = 26f;
        public const float EnemyLeashViewMarginDefault = 4f, LeashViewCameraHeightDefault = 25f, LeashViewCameraBackDefault = 20f,
            LeashViewFieldOfViewDefault = 50f, LeashViewMaxAspectDefault = 2.4f, LeashViewLagSecondsDefault = 0.125f, LeashViewLeadSecondsDefault = 0.5f;

        public override void Validate(ConfigValidationReport report)
        {
            report.Positive(this, nameof(tickRate), tickRate);
            report.Positive(this, nameof(shipSpeedMultiplier), shipSpeedMultiplier);
            if (maxStepsPerFrame < 1) report.Error(this, nameof(maxStepsPerFrame), "must be at least 1");
            if (enemyLeashViewMargin < 0f) report.Error(this, nameof(enemyLeashViewMargin), "must not be negative");
            if (enemyLeashDistance < 1f) report.Error(this, nameof(enemyLeashDistance), "must be at least 1 m");
            if (enemyLeashHysteresis < 0f) report.Error(this, nameof(enemyLeashHysteresis), "must not be negative");
            if (leashViewCameraHeight <= 0f) report.Error(this, nameof(leashViewCameraHeight), "must be above 0");
            if (leashViewMaxAspect < 1f) report.Error(this, nameof(leashViewMaxAspect), "must be at least 1");
            // The view must end on the sea (its top edge below the horizon: half the field of view under the camera's pitch),
            // or no enemy could be put outside it.
            var pitch = Mathf.Atan2(leashViewCameraHeight, Mathf.Max(0f, leashViewCameraBack)) * Mathf.Rad2Deg;
            if (leashViewFieldOfView * 0.5f >= pitch - 1f) report.Error(this, nameof(leashViewFieldOfView), "the view's top edge must look down at the sea");
        }
    }
}
