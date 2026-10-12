using System.Runtime.InteropServices;
using UnityEngine;

namespace RoguePirates.Hackathon
{
    /// <summary>
    /// Hackathon (12 Oct): copies text to the device clipboard. Desktop, Android and the Editor use
    /// <see cref="GUIUtility.systemCopyBuffer"/>; a WebGL player also writes the browser clipboard through
    /// HackathonClipboard.jslib (systemCopyBuffer alone stays inside the player there).
    /// </summary>
    public static class Web3Clipboard
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void HackathonCopyToClipboard(string text);
#endif

        public static void Copy(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            GUIUtility.systemCopyBuffer = text;
#if UNITY_WEBGL && !UNITY_EDITOR
            try { HackathonCopyToClipboard(text); }
            catch (System.Exception e) { Debug.LogWarning("Web3Clipboard: " + e.Message); }
#endif
        }
    }
}
