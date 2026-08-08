using UnityEngine;

namespace PirateGame.Combat
{
    /// <summary>
    /// Place on empty GameObjects to mark cannon fire points on the ship hull.
    /// Forward (blue Z axis) is the default fire direction when not auto-aiming.
    /// </summary>
    public class CannonMount : MonoBehaviour
    {
        [SerializeField] bool enabled = true;

        public bool Enabled => enabled;

        public void SetEnabled(bool value) => enabled = value;

        void OnDrawGizmos()
        {
            if (!enabled) return;

            Gizmos.color = new Color(1f, 0.65f, 0.1f, 0.9f);
            Gizmos.DrawSphere(transform.position, 0.15f);
            Gizmos.DrawRay(transform.position, transform.forward * 1.5f);
        }
    }
}
