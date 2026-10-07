using RoguePirates.V4.Foundation.Balance;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoguePirates.V4.Foundation.Core
{
    public sealed class AppRoot : MonoBehaviour
    {
        public static AppRoot Instance { get; private set; }

        public SaveService Save { get; private set; }

        /// <summary>PlayMode tests: swap to an isolated save without touching production PlayerPrefs.</summary>
        public void ReplaceSave(SaveService save) => Save = save ?? throw new ArgumentNullException(nameof(save));
        public ConfigService Config { get; private set; }
        public AudioService Audio { get; private set; }

        // V5 M8z (7 Oct): app-lifetime services the outer assemblies own (the online services, the 1v1 session), created
        // once by their composition code and registered here, so they live and die with the app root (they were a lazy
        // DontDestroyOnLoad singleton and a static class with mutable state).
        readonly System.Collections.Generic.Dictionary<Type, object> _services = new System.Collections.Generic.Dictionary<Type, object>();

        /// <summary>V5 M8z (7 Oct): registers an app-lifetime service (replaces one of the same type).</summary>
        public void Register<T>(T service) where T : class => _services[typeof(T)] = service ?? throw new ArgumentNullException(nameof(service));

        /// <summary>V5 M8z (7 Oct): the registered app-lifetime service of this type.</summary>
        public bool TryGet<T>(out T service) where T : class
        {
            service = _services.TryGetValue(typeof(T), out var s) ? s as T : null;
            return service != null;
        }

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
            RoguePirates.V4.Foundation.Platform.GameSettings.Current.Apply(); // V5 M7: the saved settings
        }

        public static void EndActiveSaltwindRunContext()
        {
            StageBalanceProfileContext.EndRun();
            Progression.ActiveRun.IsActive = false;
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
