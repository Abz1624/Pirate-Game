using HighSeas.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HighSeas.UI
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] Button playButton;
        [SerializeField] Button quitButton;
        [SerializeField] TextMeshProUGUI versionText;

        void Start()
        {
            if (playButton != null) playButton.onClick.AddListener(OnPlay);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuit);
            if (versionText != null) versionText.text = $"v{Application.version}";
        }

        void OnPlay()
        {
            AppRoot.Instance?.LoadScene("Run");
        }

        void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    public sealed class BootLoader : MonoBehaviour
    {
        [SerializeField] string nextScene = "MainMenu";
        [SerializeField] Slider progressBar;

        void Start()
        {
            AppRoot.EnsureInitialized();
            SceneManager.LoadScene(nextScene);
        }
    }

    public sealed class RunEndScreen : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] TextMeshProUGUI summaryText;
        [SerializeField] Button continueButton;

        void Start()
        {
            if (continueButton != null)
                continueButton.onClick.AddListener(() => AppRoot.Instance?.LoadScene("MainMenu"));
            if (RunManager.Instance != null)
                RunManager.Instance.StateChanged += OnStateChanged;
        }

        void OnStateChanged(RunState state)
        {
            if (state != RunState.RunComplete) return;
            if (panel != null) panel.SetActive(true);
            if (summaryText != null && RunManager.Instance != null)
            {
                summaryText.text =
                    $"Seed: {RunManager.Instance.RunSeed}\n" +
                    $"Time: {RunManager.Instance.ElapsedSeconds:0}s\n" +
                    $"Kills: {RunManager.Instance.KillCount}\n" +
                    $"Boss Kills: {RunManager.Instance.BossKills}\n" +
                    $"Gold: {RunManager.Instance.Gold}";
            }
        }
    }

    public sealed class TutorialOverlay : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] TextMeshProUGUI body;
        static bool _shown;

        void Start()
        {
            if (_shown || panel == null) return;
            _shown = true;
            panel.SetActive(true);
            if (body != null)
                body.text = "MOVE with WASD or joystick.\nWeapons AUTO-FIRE.\nPick upgrades on LEVEL UP.";
            Invoke(nameof(Hide), 8f);
        }

        void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }
    }

    public sealed class AccessibilitySettings : MonoBehaviour
    {
        public void SetUIScale(float scale)
        {
            if (AppRoot.Instance != null) AppRoot.Instance.Config.UIScale = scale;
        }

        public void SetScreenShake(bool enabled)
        {
            if (AppRoot.Instance != null) AppRoot.Instance.Config.ScreenShakeEnabled = enabled;
        }

        public void SetQuality(int tier)
        {
            if (AppRoot.Instance != null)
                AppRoot.Instance.Config.Quality = (QualityTier)Mathf.Clamp(tier, 0, 2);
        }
    }
}
