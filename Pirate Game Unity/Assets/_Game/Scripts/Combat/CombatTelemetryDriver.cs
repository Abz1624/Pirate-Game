using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Player;
using HighSeas.Weapons;
using UnityEngine;

namespace HighSeas.V3
{
    public sealed class CombatTelemetryDriver : MonoBehaviour
    {
        PlayerShipController _player;
        ProjectileWorld _projectiles;

        void Start()
        {
            _player = FindFirstObjectByType<PlayerShipController>();
            _projectiles = FindFirstObjectByType<ProjectileWorld>();
        }

        void Update()
        {
            if (RunManager.Instance == null || RunManager.Instance.IsPaused) return;
            if (_player == null) return;

            var heading = Mathf.Repeat(_player.transform.eulerAngles.y, 360f);
            RunCombatTelemetry.Instance?.TickMovement(_player.Position, heading, Time.deltaTime);
            RunCombatTelemetry.Instance?.SamplePerformance(0, FindFirstObjectByType<Enemies.EnemyWorld>()?.ActiveCount ?? 0);
        }
    }
}
