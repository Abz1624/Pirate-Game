using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Player;
using HighSeas.World;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Applies escalating damage when the ship leaves the playable map bounds.</summary>
    public sealed class V3StormEdgeDamage : MonoBehaviour
    {
        const float RunStartGraceSeconds = 8f;

        DamageReceiver _damage;
        float _toastCooldown;
        float _runGrace;

        public static void Ensure(PlayerShipController ship)
        {
            if (ship == null) return;
            if (ship.GetComponent<V3StormEdgeDamage>() != null) return;
            ship.gameObject.AddComponent<V3StormEdgeDamage>();
        }

        void Awake()
        {
            _damage = GetComponent<DamageReceiver>();
            _runGrace = RunStartGraceSeconds;
        }

        void Update()
        {
            if (!V3RunContext.IsActive) return;
            if (RunManager.Instance == null || RunManager.Instance.IsRunEnded || RunManager.Instance.IsPaused) return;
            if (_damage == null || !_damage.IsAlive) return;

            if (_runGrace > 0f)
            {
                _runGrace -= Time.deltaTime;
                return;
            }

            var pos = transform.position;
            if (PlayBounds.IsInsideStormSafeZone(pos))
                return;

            var export = StageBalanceProfileContext.Export;
            if (StageBalanceProfileContext.IsActive && export != null)
            {
                if (!SaltwindFinaleLogic.IsStormDamageActive(RunManager.Instance.ElapsedSeconds, export))
                    return;

                var depth = PlayBounds.GetStormDepth(pos);
                var dps = export.Overtime.StormOutsideDamagePerSecond;
                var damage = dps * (1f + depth * 0.08f) * Time.deltaTime;
                _damage.TakeDamage(damage, pos, false);
            }
            else
            {
                var depth = PlayBounds.GetStormDepth(pos);
                var damage = PlayBounds.EdgeDamagePerSecond * (1f + depth * 0.12f) * Time.deltaTime;
                _damage.TakeDamage(damage, pos, false);
            }

            _toastCooldown -= Time.deltaTime;
            if (_toastCooldown <= 0f)
            {
                _toastCooldown = 2.5f;
                V3ToastHud.Instance?.Show("STORM EDGE — return to open water!", 2f);
            }
        }
    }
}

