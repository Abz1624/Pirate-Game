using MoreMountains.Feedbacks;
using RoguePirates.V4.Foundation.Player;
using UnityEngine;

namespace RoguePirates.FeelIntegration
{
    /// <summary>
    /// Feel feedback: a quick springy pop or squash of the player ship's visual model (see ShipPunchOffset).
    /// Add it to any MMF_Player via "Transform > Rogue Pirates Ship Punch".
    /// </summary>
    [AddComponentMenu("")]
    [System.Serializable]
    [FeedbackHelp("Rogue Pirates: a quick springy scale pop (or squash) of the ship's model. Visual only. " +
                  "Amount 0.05 = 5% bigger at the peak. Target defaults to the followed ship's visual pivot.")]
    [FeedbackPath("Transform/Rogue Pirates Ship Punch")]
    public class MMF_ShipPunch : MMF_Feedback
    {
        public static bool FeedbackTypeAuthorized = true;

        public override float FeedbackDuration
        {
            get => ApplyTimeMultiplier(Duration);
            set => Duration = value;
        }

        [MMFInspectorGroup("Ship Punch", true, 15)]
        [Tooltip("Peak scale change at full intensity (0.05 = 5%).")]
        public float Amount = 0.05f;
        [Tooltip("How long the pop takes to settle (seconds).")]
        public float Duration = 0.3f;
        [Tooltip("Wobbles per second.")]
        public float Frequency = 5f;
        [Tooltip("Widen and flatten (an impact) instead of a uniform pop.")]
        public bool Squash = false;
        [Tooltip("Transform to punch. Leave empty to use the followed ship's visual pivot.")]
        public Transform Target;

        protected override void CustomPlayFeedback(Vector3 position, float feedbacksIntensity = 1.0f)
        {
            if (!Active || !FeedbackTypeAuthorized) return;
            var target = Target != null ? Target : FollowedShipVisual();
            if (target == null) return;
            var punch = target.GetComponent<ShipPunchOffset>();
            if (punch == null) punch = target.gameObject.AddComponent<ShipPunchOffset>();
            punch.Punch(Amount * ComputeIntensity(feedbacksIntensity, position), FeedbackDuration, Frequency, Squash);
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
