using UnityEngine;

namespace PirateGame.Enemies
{
    /// <summary>
    /// Simple pursuit AI toward a target (usually the player ship).
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyChase : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 4f;
        [SerializeField] float turnSpeed = 8f;

        Transform target;

        public float MoveSpeed
        {
            get => moveSpeed;
            set => moveSpeed = Mathf.Max(0f, value);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void Initialize(Transform newTarget, float speed)
        {
            target = newTarget;
            MoveSpeed = speed;
        }

        void Update()
        {
            if (target == null || moveSpeed <= 0f) return;

            var toTarget = target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f) return;

            var direction = toTarget.normalized;
            transform.position += direction * (moveSpeed * Time.deltaTime);

            var lookRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, turnSpeed * Time.deltaTime);
        }
    }
}
