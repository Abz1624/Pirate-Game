using RoguePirates.V4.Foundation.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoguePirates.V4
{
    /// <summary>V4 boot scene: initializes the app root and loads the V4 main menu.</summary>
    public sealed class V4BootLoader : MonoBehaviour
    {
        [SerializeField] string nextScene = V4SceneNames.MainMenu;

        void Start()
        {
            // Playtest (3 Oct): the boot scene has no camera; a listener for the frames before the menu loads (console warning).
            if (FindFirstObjectByType<AudioListener>() == null) new GameObject("BootAudioListener", typeof(AudioListener));
            AppRoot.EnsureInitialized();
            SceneManager.LoadScene(nextScene);
        }
    }
}
