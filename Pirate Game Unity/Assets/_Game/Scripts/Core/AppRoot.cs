using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HighSeas.Core
{
    public sealed class AppRoot : MonoBehaviour
    {
        public static AppRoot Instance { get; private set; }

        public SaveService Save { get; private set; }
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
            SceneManager.LoadScene(sceneName);
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
