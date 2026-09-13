using HighSeas.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HighSeas.V3
{
    public sealed class V3BootLoader : MonoBehaviour
    {
        [SerializeField] string nextScene = V3SceneNames.MainMenu;

        void Start()
        {
            AppRoot.EnsureInitialized();
            SceneManager.LoadScene(nextScene);
        }
    }
}
