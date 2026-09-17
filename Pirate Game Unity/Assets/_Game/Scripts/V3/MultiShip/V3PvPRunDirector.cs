using HighSeas.Core;
using HighSeas.Player;
using HighSeas.UI;
using HighSeas.V3;
using UnityEngine;

namespace HighSeas.V3.MultiShip
{
    /// <summary>PvP win condition: destroy the rival player ship.</summary>
    public sealed class V3PvPRunDirector : MonoBehaviour
    {
        public static V3PvPRunDirector Instance { get; private set; }
        public static bool RivalDefeated { get; private set; }

        PlayerShipController _rival;

        public static void Ensure(PlayerShipController rival)
        {
            if (!V3RunSession.IsPvP || rival == null)
                return;
            if (Instance != null)
            {
                Instance.BindRival(rival);
                return;
            }

            var go = new GameObject("V3PvPRunDirector");
            var director = go.AddComponent<V3PvPRunDirector>();
            director.BindRival(rival);
        }

        public static void ResetState() => RivalDefeated = false;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            UnbindRival();
            if (Instance == this)
                Instance = null;
        }

        void BindRival(PlayerShipController rival)
        {
            UnbindRival();
            _rival = rival;
            if (_rival?.Damage != null)
                _rival.Damage.Died += OnRivalDied;
        }

        void UnbindRival()
        {
            if (_rival?.Damage != null)
                _rival.Damage.Died -= OnRivalDied;
            _rival = null;
        }

        void OnRivalDied()
        {
            if (RivalDefeated || RunManager.Instance == null)
                return;

            RivalDefeated = true;
            RunManager.Instance.SecureObjective();
            RunManager.Instance.CompleteRun(true);
            LevelSystem.Instance?.CancelPendingLevelUps();
            V3ToastHud.Instance?.Show("Rival sunk — you win the duel!", 5f);
        }
    }
}
