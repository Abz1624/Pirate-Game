using UnityEngine;
namespace Oicaimang
{
    public class ChangePosEndLaser1 : MonoBehaviour
    {
        [SerializeField] Transform effectEndLaser;
        [SerializeField] LineRenderer laser;
        [SerializeField] Transform posShotgun;

        public Transform centerPoint;
        public float radius = 5f;
        public float angularSpeed = 2f;

        private float angle = 0f;

        void Update()
        {
            angle += angularSpeed * Time.deltaTime;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            if (centerPoint != null)
            {
                transform.position = new Vector3(centerPoint.position.x + x, centerPoint.position.y + y, transform.position.z);
            }
            else
            {
                transform.position = new Vector3(x, y, transform.position.z);
            }
            effectEndLaser.position = (Vector2)transform.position;
            laser.SetPosition(1, new Vector3(transform.position.x, transform.position.y, 0));
            Vector2 direction = transform.position - posShotgun.position;
            float angleRotate = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            posShotgun.transform.rotation = Quaternion.Euler(0f, 0f, angleRotate);
        }
    }
}