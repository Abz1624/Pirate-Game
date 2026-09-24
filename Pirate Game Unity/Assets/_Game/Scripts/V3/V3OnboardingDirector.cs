using HighSeas.Core;
using HighSeas.Meta;
using HighSeas.Player;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Soft tutorial prompts for Saltwind solo runs.</summary>
    public sealed class V3OnboardingDirector : MonoBehaviour
    {
        const string RunsCompletedKey = "v3_saltwind_runs_completed";

        public static V3OnboardingDirector Instance { get; private set; }

        bool _movePrompted;
        bool _weaponPrompted;
        bool _killPrompted;
        bool _levelPrompted;
        bool _goalShown;

        public static V3OnboardingDirector Ensure()
        {
            if (Instance != null) return Instance;
            var systems = GameObject.Find("Systems");
            if (systems == null) return null;
            return systems.GetComponent<V3OnboardingDirector>() ??
                   systems.AddComponent<V3OnboardingDirector>();
        }

        void Awake()
        {
            Instance = this;
            if (TriggerBus.Instance != null)
                TriggerBus.Instance.Kill += OnEnemyKilled;
        }

        void OnEnemyKilled(bool _, bool __, Vector3 ___, string ____) => NotifyFirstKill();

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ResetForRun()
        {
            _movePrompted = false;
            _weaponPrompted = false;
            _killPrompted = false;
            _levelPrompted = false;
            _goalShown = false;
        }

        public bool ShouldShowFullTutorial => PlayerPrefs.GetInt(RunsCompletedKey, 0) < 2;

        void Update()
        {
            if (!ShouldShowFullTutorial || RunManager.Instance == null || RunManager.Instance.IsRunEnded)
                return;
            if (RunManager.Instance.IsPaused) return;

            var player = FindFirstObjectByType<PlayerShipController>();
            if (player == null) return;

            if (!_goalShown)
            {
                _goalShown = true;
                V3ToastHud.Instance?.Show(
                    "Explore → Kill → Collect → Level Up → Survive → Beacon → Boss",
                    6f);
            }

            if (!_movePrompted && player.Velocity.sqrMagnitude > 4f)
            {
                _movePrompted = true;
                V3ToastHud.Instance?.Show(
                    "Swivel Guns fire on their own at the nearest enemy — just sail",
                    5f);
            }
        }

        public void NotifyFirstKill()
        {
            if (!ShouldShowFullTutorial || _killPrompted) return;
            _killPrompted = true;
            V3ToastHud.Instance?.Show("Collect XP crystals to level up", 4f);
        }

        public void NotifyLevelUpShown()
        {
            if (!ShouldShowFullTutorial || _levelPrompted) return;
            _levelPrompted = true;
            V3ToastHud.Instance?.Show("Pick an upgrade — weapons and logbooks build your ship", 4f);
        }

        public static void RecordRunCompleted()
        {
            var n = PlayerPrefs.GetInt(RunsCompletedKey, 0);
            PlayerPrefs.SetInt(RunsCompletedKey, n + 1);
            PlayerPrefs.Save();
        }
    }
}
