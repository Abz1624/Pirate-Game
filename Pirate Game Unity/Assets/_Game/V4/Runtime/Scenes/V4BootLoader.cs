using HighSeas.Core;
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
            AppRoot.EnsureInitialized();
            SceneManager.LoadScene(nextScene);
        }
    }
}
