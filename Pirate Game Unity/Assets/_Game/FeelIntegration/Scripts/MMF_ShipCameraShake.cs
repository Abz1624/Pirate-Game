using MoreMountains.Feedbacks;
using RoguePirates.V4.Foundation.Player;
using UnityEngine;

namespace RoguePirates.FeelIntegration
{
    /// <summary>
    /// Feel feedback: camera shake that works with the game's FollowCamera (see CameraKickOffset): a fading jitter, an
    /// optional sideways recoil push read from the feedback's position, and an optional tiny roll.
    /// Add it to any MMF_Player via "Camera > Rogue Pirates Ship Shake".
    /// </summary>
    [AddComponentMenu("")]
    [System.Serializable]
    [FeedbackHelp("Rogue Pirates: camera shake applied after the game's FollowCamera. Shake = fading jitter (m); " +
                  "Recoil = a sideways push (m) away from / toward the feedback's position; Roll = a tiny camera roll (degrees). " +
                  "The player's intensity scales all three.")]
    [FeedbackPath("Camera/Rogue Pirates Ship Shake")]
    public class MMF_ShipCameraShake : MMF_Feedback
    {
        public static bool FeedbackTypeAuthorized = true;

        public enum RecoilMode { None, AwayFromPosition, TowardPosition }

        public override float FeedbackDuration
        {
            get => ApplyTimeMultiplier(Duration);
            set => Duration = value;
        }

        [MMFInspectorGroup("Ship Shake", true, 12)]
        [Tooltip("Shake offset in metres at full intensity.")]
        public float Amplitude = 0.06f;
        [Tooltip("How long the shake takes to fade (seconds).")]
        public float Duration = 0.15f;
        [Tooltip("How fast the shake jitters (higher = buzzier).")]
        public float Frequency = 32f;
        [Tooltip("Shake only left and right (no up/down or front/back movement).")]
        public bool SidewaysOnly = true;

        [MMFInspectorGroup("Recoil & Roll", true, 14)]
        [Tooltip("Sideways push of the camera, read from the feedback's position relative to the ship.")]
        public RecoilMode Recoil = RecoilMode.None;
        [Tooltip("Recoil push in metres at full intensity.")]
        public float RecoilAmount = 0f;
        [Tooltip("Camera roll in degrees at full intensity (0 = none).")]
        public float RollDegrees = 0f;
        [Tooltip("Camera to shake. Leave empty to use the Main Camera.")]
        public Camera TargetCamera;

        protected override void CustomPlayFeedback(Vector3 position, float feedbacksIntensity = 1.0f)
        {
            if (!Active || !FeedbackTypeAuthorized) return;
            var cam = TargetCamera != null ? TargetCamera : Camera.main;
            if (cam == null) return;
            var kick = cam.GetComponent<CameraKickOffset>();
            if (kick == null) kick = cam.gameObject.AddComponent<CameraKickOffset>();

            var k = ComputeIntensity(feedbacksIntensity, position);
            var recoilDir = Vector3.zero;
            if (Recoil != RecoilMode.None && RecoilAmount > 0f)
            {
                var follow = cam.GetComponent<FollowCamera>();
                var center = follow != null && follow.target != null ? follow.target.position : cam.transform.position;
                var toPos = position - center;
                toPos.y = 0f;
                recoilDir = Recoil == RecoilMode.AwayFromPosition ? -toPos : toPos;
            }
            kick.Kick(Amplitude * k, FeedbackDuration, Frequency, SidewaysOnly, recoilDir, RecoilAmount * k, RollDegrees * k);
        }
    }
}
