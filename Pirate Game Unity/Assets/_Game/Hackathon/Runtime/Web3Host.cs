using System.Collections;
using Solana.Unity.SDK;
using UnityEngine;

namespace RoguePirates.Hackathon
{
    /// <summary>
    /// Hackathon (12 Oct): the SDK's scene helpers on one kept object: its MainThreadDispatcher (the in-game wallet saves
    /// through it) and the WebGL wallet chooser drawn over the game's menus.
    /// </summary>
    public sealed class Web3Host : MonoBehaviour
    {
        public static Web3Host Instance { get; private set; }

        public static Web3Host Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Web3Host");
            DontDestroyOnLoad(go);
            if (!MainThreadDispatcher.Exists()) go.AddComponent<MainThreadDispatcher>();
            Instance = go.AddComponent<Web3Host>();
            return Instance;
        }

        /// <summary>The SDK's WebGL wallet chooser (more than one browser wallet installed) draws on top of the menu.</summary>
        public void RaiseWalletChooser() => StartCoroutine(RaiseChooser());

        IEnumerator RaiseChooser()
        {
            for (var i = 0; i < 600; i++)
            {
                var ui = SolanaWalletAdapterWebGL.WalletAdapterUI;
                if (ui != null)
                {
                    foreach (var c in ui.GetComponentsInChildren<Canvas>(true))
                    {
                        c.overrideSorting = true;
                        c.sortingOrder = 30000;
                    }
                    var root = ui.GetComponent<Canvas>();
                    if (root != null) root.sortingOrder = 30000;
                    yield break;
                }
                yield return null;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
