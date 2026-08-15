using UnityEngine;

namespace PirateGame.Progression
{
    public class PickupMagnet : MonoBehaviour
    {
        [SerializeField] PlayerRunProgress progress;

        void Awake()
        {
            if (progress == null)
                progress = GetComponent<PlayerRunProgress>();
        }

        void Update()
        {
            if (progress == null) return;

            var radius = progress.PickupRadius;
            var pickups = FindObjectsByType<PickupItem>(FindObjectsSortMode.None);
            foreach (var pickup in pickups)
            {
                var offset = pickup.transform.position - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude <= radius * radius)
                    pickup.BeginMagnet(transform);
            }
        }
    }
}
