using PirateGame.Combat;
using PirateGame.Effects;
using PirateGame.Enemies;
using UnityEngine;

namespace PirateGame.Progression
{
    public class EnemyLootDropper : MonoBehaviour
    {
        [SerializeField] float xpPerCrystal = 5f;
        [SerializeField] int crystalCount = 1;
        [SerializeField] int bossCrystalCount = 4;
        [SerializeField] float scatterRadius = 1.1f;
        [SerializeField] Color deathBurstColor = new(1f, 0.35f, 0.15f, 1f);
        [SerializeField] Color crystalSpawnColor = new(0.35f, 0.85f, 1f, 1f);

        Health health;
        bool isBoss;
        bool dropped;
        bool suppressLoot;

        public void SuppressLoot() => suppressLoot = true;

        void Awake()
        {
            health = GetComponent<Health>();
            isBoss = name.ToLowerInvariant().StartsWith("boss_");
            if (health != null)
                health.Died += OnDied;
        }

        void OnDestroy()
        {
            if (health != null)
                health.Died -= OnDied;
        }

        void OnDied(Health _)
        {
            if (dropped) return;
            dropped = true;

            FindFirstObjectByType<PlayerRunProgress>()?.RegisterKill(isBoss);

            if (suppressLoot) return;

            var position = transform.position;
            position.y = 0.25f;
            SimpleBurstEffect.Play(position, deathBurstColor, isBoss ? 2.4f : 1.4f, isBoss ? 0.6f : 0.45f);

            var spawner = FindFirstObjectByType<EnemySpawner>();
            if (spawner == null) return;

            var count = isBoss ? bossCrystalCount : crystalCount;
            var valueEach = isBoss ? xpPerCrystal * 2f : xpPerCrystal;
            spawner.SpawnCrystalDrops(position, count, valueEach, scatterRadius, crystalSpawnColor);
        }
    }
}
