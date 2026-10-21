using MoreMountains.Feedbacks;
using RoguePirates.V4.Foundation.Player;
using UnityEngine;

namespace RoguePirates.FeelIntegration
{
    /// <summary>
    /// Feel feedback: a slight, damped side-to-side sway of the player ship's visual model (see ShipSwayOffset).
    /// Add it to any MMF_Player via "Transform > Rogue Pirates Ship Sway".
    /// </summary>
    [AddComponentMenu("")]
    [System.Serializable]
    [FeedbackHelp("Rogue Pirates: a slight side-to-side roll of the ship's visual model that fades out. " +
                  "Visual only (physics untouched). Target defaults to the ship the camera follows (its VisualTiltPivot).")]
    [FeedbackPath("Transform/Rogue Pirates Ship Sway")]
    public class MMF_ShipSway : MMF_Feedback
    {
        public static bool FeedbackTypeAuthorized = true;

        public override float FeedbackDuration
        {
            get => ApplyTimeMultiplier(Duration);
            set => Duration = value;
        }

        [MMFInspectorGroup("Ship Sway", true, 13)]
        [Tooltip("Largest roll angle in degrees at full intensity.")]
        public float Angle = 4f;
        [Tooltip("How long the sway takes to settle (seconds).")]
        public float Duration = 0.7f;
        [Tooltip("Sways per second (left-right cycles).")]
        public float Frequency = 2.2f;
        [Tooltip("Lean away from the feedback's position (e.g. a cannon's muzzle) instead of a random side.")]
        public bool RecoilFromPosition = false;
        [Tooltip("Transform to sway. Leave empty to use the followed ship's visual pivot.")]
        public Transform Target;

        protected override void CustomPlayFeedback(Vector3 position, float feedbacksIntensity = 1.0f)
        {
            if (!Active || !FeedbackTypeAuthorized) return;
            var target = Target != null ? Target : FollowedShipVisual();
            if (target == null) return;
            var sway = target.GetComponent<ShipSwayOffset>();
            if (sway == null) sway = target.gameObject.AddComponent<ShipSwayOffset>();
            var sign = 0f;
            if (RecoilFromPosition)
            {
                var right = target.right; right.y = 0f;
                var side = Vector3.Dot(position - target.position, right);
                if (Mathf.Abs(side) > 0.05f) sign = side > 0f ? 1f : -1f;
            }
            sway.Sway(Angle * ComputeIntensity(feedbacksIntensity, position), FeedbackDuration, Frequency, sign);
        }

        static Transform FollowedShipVisual()
        {
            PlayerShipController ship = null;
            var cam = Camera.main;
            var follow = cam != null ? cam.GetComponent<FollowCamera>() : null;
            if (follow != null && follow.target != null) ship = follow.target.GetComponent<PlayerShipController>();
            if (ship == null) ship = Object.FindFirstObjectByType<PlayerShipController>();
            if (ship == null) return null;
            return ship.VisualModel != null ? ship.VisualModel : ship.transform;
        }
    }
}
