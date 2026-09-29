using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Player;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Enemies
{
    public sealed class RunCombatSignals : MonoBehaviour
    {
        public static RunCombatSignals Instance { get; private set; }

        float _kiteTimer;
        float _standStillTimer;
        float _dpsWindow;
        float _damageInWindow;
        PlayerShipController _player;

        public float KiteSeconds => _kiteTimer;
        public float StandStillSeconds => _standStillTimer;
        public float RecentDps => _dpsWindow > 0.01f ? _damageInWindow / _dpsWindow : 0f;
        public float PlayerHpFraction
        {
            get
            {
                if (_player == null || _player.Damage == null || RunManager.Instance == null)
                    return 1f;
                var max = RunManager.Instance.Stats.MaxHull;
                return max > 0f ? _player.Damage.CurrentHP / max : 1f;
            }
        }

        public static RunCombatSignals Ensure(PlayerShipController player)
        {
            if (Instance != null)
            {
                Instance._player = player;
                return Instance;
            }

            var go = new GameObject("RunCombatSignals");
            var signals = go.AddComponent<RunCombatSignals>();
            signals._player = player;
            return signals;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void NotifyPlayerDamaged(float amount)
        {
            _damageInWindow += amount;
        }

        public void Tick(float dt, int activeEnemies, Vector3 playerPos, Vector3 playerVelocity)
        {
            _dpsWindow += dt;
            if (_dpsWindow >= 2f)
            {
                _damageInWindow = 0f;
                _dpsWindow = 0f;
            }

            playerVelocity.y = 0f;
            var speed = playerVelocity.magnitude;
            if (speed > 4.5f)
            {
                _kiteTimer += dt;
                _standStillTimer = 0f;
            }
            else if (speed < 0.6f)
            {
                _standStillTimer += dt;
                _kiteTimer = Mathf.Max(0f, _kiteTimer - dt * 0.5f);
            }
            else
            {
                _kiteTimer = Mathf.Max(0f, _kiteTimer - dt);
                _standStillTimer = Mathf.Max(0f, _standStillTimer - dt);
            }

            _activeEnemies = activeEnemies;
        }

        int _activeEnemies;
        public int ActiveEnemyCount => _activeEnemies;

        public bool IsSignalActive(AdaptiveSignal signal, AdaptiveCounterSettings settings)
        {
            if (settings.enabledSignals == null)
                return false;
            var enabled = false;
            for (var i = 0; i < settings.enabledSignals.Length; i++)
            {
                if (settings.enabledSignals[i] == signal)
                {
                    enabled = true;
                    break;
                }
            }

            if (!enabled)
                return false;

            return signal switch
            {
                AdaptiveSignal.PlayerKiteTime => _kiteTimer >= settings.kiteThresholdSeconds,
                AdaptiveSignal.PlayerStandStill => _standStillTimer >= settings.standStillThresholdSeconds,
                AdaptiveSignal.HighPlayerDps => RecentDps >= settings.highDpsThreshold,
                AdaptiveSignal.LowPlayerHp => PlayerHpFraction <= settings.lowHpFraction,
                AdaptiveSignal.ManyActiveEnemies => _activeEnemies >= settings.manyEnemiesThreshold,
                _ => false
            };
        }
    }
}
