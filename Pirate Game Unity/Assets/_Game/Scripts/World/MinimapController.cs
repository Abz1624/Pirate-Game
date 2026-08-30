using HighSeas.Core;
using HighSeas.Enemies;
using HighSeas.Weapons;
using UnityEngine;

namespace HighSeas.World
{
    public sealed class MinimapController : MonoBehaviour
    {
        [SerializeField] RectTransform playerIcon;
        [SerializeField] RectTransform container;
        [SerializeField] float mapScale = 0.5f;

        Player.PlayerShipController _player;

        void Update()
        {
            if (_player == null) _player = FindFirstObjectByType<Player.PlayerShipController>();
            if (_player == null || playerIcon == null) return;
            var pos = _player.Position;
            playerIcon.anchoredPosition = new Vector2(pos.x, pos.z) * mapScale;
        }
    }

    public sealed class PerformanceHarness : MonoBehaviour
    {
        [SerializeField] EnemyWorld enemyWorld;
        [SerializeField] int spawnBurst = 50;

        void Awake()
        {
            if (enemyWorld == null)
                enemyWorld = FindFirstObjectByType<EnemyWorld>();
        }

        void Update()
        {
            if (!Input.GetKeyDown(KeyCode.F9)) return;
            if (enemyWorld == null)
            {
                enemyWorld = FindFirstObjectByType<EnemyWorld>();
                if (enemyWorld == null) return;
            }

            var center = transform.position;
            var player = FindFirstObjectByType<Player.PlayerShipController>();
            if (player != null)
                center = player.Position;

            for (var i = 0; i < spawnBurst; i++)
            {
                var angle = Random.Range(0f, Mathf.PI * 2f);
                var dist = Random.Range(10f, 20f);
                var pos = center + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                var defIndex = i % Mathf.Max(1, enemyWorld.Definitions?.Length ?? 1);
                enemyWorld.Spawn(defIndex, pos);
            }
        }
    }
}
