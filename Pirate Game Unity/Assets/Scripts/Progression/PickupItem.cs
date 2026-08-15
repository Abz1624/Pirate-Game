using PirateGame.Effects;
using UnityEngine;

namespace PirateGame.Progression
{
    public enum PickupKind
    {
        Xp,
        Gold,
        Health
    }

    /// <summary>
    /// Collectible orb — booty coins, gems, etc.
    /// </summary>
    public class PickupItem : MonoBehaviour
    {
        [SerializeField] PickupKind kind = PickupKind.Xp;
        [SerializeField] float value = 5f;
        [SerializeField] float magnetSpeed = 12f;
        [SerializeField] float spawnPopDuration = 0.35f;
        [SerializeField] float spawnRiseHeight = 0.6f;
        [SerializeField] Color spawnBurstColor = new(0.35f, 0.85f, 1f, 1f);
        [SerializeField] float targetWorldScale = 0.1f;

        Transform target;
        bool magnetized;
        bool spawning = true;
        float spawnTimer;
        float spin;
        Vector3 baseScale;
        Vector3 spawnOrigin;

        public PickupKind Kind => kind;
        public float Value => value;

        public void Initialize(PickupKind pickupKind, float amount)
        {
            kind = pickupKind;
            value = amount;
        }

        public void PlaySpawnPop(Vector3 worldPosition, Color? burstColor = null)
        {
            spawnOrigin = worldPosition;
            transform.position = worldPosition;
            spawning = true;
            spawnTimer = 0f;
            ApplyTargetScale();
            baseScale = transform.localScale;
            transform.localScale = Vector3.zero;
            SimpleBurstEffect.PlayCrystalSpawn(worldPosition, burstColor ?? spawnBurstColor);
        }

        void Awake()
        {
            ApplyTargetScale();
            baseScale = transform.localScale;
            spawnOrigin = transform.position;
        }

        void ApplyTargetScale()
        {
            if (targetWorldScale <= 0f) return;
            transform.localScale = Vector3.one * targetWorldScale;
        }

        void Update()
        {
            spin += Time.deltaTime * 120f;
            transform.rotation = Quaternion.Euler(0f, spin, 0f);

            if (spawning)
            {
                spawnTimer += Time.deltaTime;
                var t = Mathf.Clamp01(spawnTimer / spawnPopDuration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                transform.localScale = baseScale * eased;

                var pos = spawnOrigin;
                pos.y += Mathf.Sin(eased * Mathf.PI) * spawnRiseHeight;
                transform.position = pos;

                if (t >= 1f)
                {
                    spawning = false;
                    transform.position = spawnOrigin;
                }

                return;
            }

            if (!magnetized || target == null) return;

            var to = target.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.25f)
            {
                Collect(target.gameObject);
                return;
            }

            transform.position += to.normalized * (magnetSpeed * Time.deltaTime);
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            Collect(other.gameObject);
        }

        public void BeginMagnet(Transform player)
        {
            if (spawning) return;
            target = player;
            magnetized = true;
        }

        void Collect(GameObject player)
        {
            var progress = player.GetComponent<PlayerRunProgress>();
            switch (kind)
            {
                case PickupKind.Xp:
                    progress?.AddXp(value);
                    break;
                case PickupKind.Gold:
                    progress?.AddXp(value * 0.5f);
                    break;
                case PickupKind.Health:
                    player.GetComponent<Combat.Health>()?.Heal(value);
                    break;
            }

            SimpleBurstEffect.Play(transform.position, spawnBurstColor, 0.5f, 0.25f, 5);
            Destroy(gameObject);
        }
    }
}
