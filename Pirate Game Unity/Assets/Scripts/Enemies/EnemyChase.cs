using UnityEngine;

namespace PirateGame.Enemies
{
    /// <summary>
    /// Simple pursuit AI toward a target (usually the player ship).
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyChase : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 2f;
        [SerializeField] float turnSpeed = 8f;
        [Tooltip("Voxel mob models usually face -Z like the player ships.")]
        [SerializeField] bool modelForwardIsNegativeZ = true;

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
            if (modelForwardIsNegativeZ)
                lookRotation *= Quaternion.Euler(0f, 180f, 0f);

            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, turnSpeed * Time.deltaTime);
        }

        public static Quaternion RotationToward(Vector3 worldDirection, bool modelForwardIsNegativeZ)
        {
            if (worldDirection.sqrMagnitude < 0.0001f)
                return Quaternion.identity;

            var lookRotation = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
            if (modelForwardIsNegativeZ)
                lookRotation *= Quaternion.Euler(0f, 180f, 0f);

            return lookRotation;
        }
    }
}
