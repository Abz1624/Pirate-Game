using HighSeas.Balance;
using HighSeas.V3;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HighSeas.Core
{
    public sealed class AppRoot : MonoBehaviour
    {
        public static AppRoot Instance { get; private set; }

        public SaveService Save { get; private set; }

        /// <summary>PlayMode tests: swap to an isolated save without touching production PlayerPrefs.</summary>
        public void ReplaceSave(SaveService save) => Save = save ?? throw new ArgumentNullException(nameof(save));
        public ConfigService Config { get; private set; }
        public AudioService Audio { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoBootstrap() => EnsureInitialized();

        public static void EnsureInitialized()
        {
            if (Instance != null) return;
            var go = new GameObject("AppRoot");
            go.AddComponent<AppRoot>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Save = new SaveService();
            Config = new ConfigService();
            Audio = new AudioService();
        }

        public void LoadScene(string sceneName)
        {
            if (StageBalanceProfileContext.IsActive &&
                sceneName != V3SceneNames.SaltwindRun &&
                !IsLocalMultiplayerRunScene(sceneName))
            {
                EndActiveSaltwindRunContext();
            }

            SceneManager.LoadScene(sceneName);
        }

        static bool IsLocalMultiplayerRunScene(string sceneName) =>
            sceneName == V3SceneNames.PvPBattle ||
            sceneName == V3SceneNames.CoOpRun ||
            sceneName == V3SceneNames.SplitScreenCoOpRun;

        public static void EndActiveSaltwindRunContext()
        {
            StageBalanceProfileContext.EndRun();
            V3RunContext.IsActive = false;
            V3RunContext.Layout = null;
        }
    }

    public sealed class ConfigService
    {
        public QualityTier Quality { get; set; } = QualityTier.Mid;
        public bool ScreenShakeEnabled { get; set; } = true;
        public float UIScale { get; set; } = 1f;
    }

    public sealed class AudioService
    {
        public int MaxVoicesPerFamily { get; set; } = 6;
    }
}
