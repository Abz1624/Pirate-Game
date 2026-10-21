using UnityEngine;

namespace RoguePirates.FeelIntegration
{
    /// <summary>
    /// Runs before everything else (-500) to take CameraKickOffset's shake off the camera before physics and the run's
    /// ticks read it, so the shake stays purely visual (the game's simulation view is a child of the drawn camera).
    /// Added automatically by CameraKickOffset.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class CameraKickRestorer : MonoBehaviour
    {
        CameraKickOffset _kick;

        void FixedUpdate() => RestoreNow();
        void Update() => RestoreNow();

        void RestoreNow()
        {
            if (_kick == null) _kick = GetComponent<CameraKickOffset>();
            if (_kick != null) _kick.Restore();
        }
    }
}
