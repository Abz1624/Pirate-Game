using System.IO;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace RoguePirates.V4.Telemetry
{
    /// <summary>
    /// Playtest (6 Oct, owner): where a recorded session goes. Editor: the repo's plan/playtest/sessions (from
    /// Application.dataPath/../../). Desktop build: the same folder when the build sits inside the repo, else
    /// persistentDataPath/sessions. WebGL: a browser download (RoguePiratesSession.jslib) and the last session kept in
    /// PlayerPrefs (<see cref="LastSessionKey"/>) so it is not lost.
    /// </summary>
    public static class SessionFile
    {
        public const string LastSessionKey = "rp.session.last";
        public const string LastSessionNameKey = "rp.session.last.name";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void RPSession_Download(string fileName, string text);
#endif

        /// <summary>
        /// V5 M8z (7 Oct, review): the largest session kept in PlayerPrefs (characters, about 256 KB of JSON). PlayerPrefs on
        /// WebGL hold about 1 MB in all, shared with the saves and the profile: a larger session is not kept (it is still
        /// downloaded or written to its file).
        /// </summary>
        public const int MaxKeptChars = 256 * 1024;

        /// <summary>V5 M8z (7 Oct, review): whether a session this size is kept for "Download last voyage".</summary>
        public static bool Keeps(string json) => !string.IsNullOrEmpty(json) && json.Length <= MaxKeptChars;

        public static void Save(string fileName, string json)
        {
            // V5 M8z (7 Oct, owner): the last voyage is kept on every platform, for the "Download last voyage" buttons.
            // V5 M8z (7 Oct, review): capped at MaxKeptChars, and a failed write (a full store) never stops the download below.
            try
            {
                if (Keeps(json))
                {
                    PlayerPrefs.SetString(LastSessionKey, json);
                    PlayerPrefs.SetString(LastSessionNameKey, fileName);
                }
                else
                {
                    // Not kept: the previous voyage's copy goes too, so "Download last voyage" never hands out an older one.
                    PlayerPrefs.DeleteKey(LastSessionKey);
                    PlayerPrefs.DeleteKey(LastSessionNameKey);
                    Debug.LogWarning($"Session {fileName} ({json?.Length ?? 0} characters) is over {MaxKeptChars} and is not kept for 'Download last voyage'.");
                }
                PlayerPrefs.Save();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Session {fileName}: could not keep it in PlayerPrefs ({e.Message}); it is still saved below.");
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            RPSession_Download(fileName, json);
            Debug.Log($"Session recorded: {fileName} downloaded by the browser (also kept in PlayerPrefs '{LastSessionKey}').");
#else
            var dir = Folder();
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, fileName);
            File.WriteAllText(path, json);
            Debug.Log($"Session recorded: {Path.GetFullPath(path)}");
#endif
        }

        /// <summary>V5 M8z (7 Oct, owner): a finished voyage's session is kept (see <see cref="Save"/>).</summary>
        public static bool HasLast => !string.IsNullOrEmpty(PlayerPrefs.GetString(LastSessionKey, string.Empty));

        /// <summary>V5 M8z (7 Oct, owner: "forgot to save it"): the last voyage again, as a browser download (WebGL) or
        /// written to the sessions folder (elsewhere). Returns a short message for the player.</summary>
        public static string DownloadLast()
        {
            var json = PlayerPrefs.GetString(LastSessionKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return "No voyage recorded yet.";
            var name = PlayerPrefs.GetString(LastSessionNameKey, "session-last.json");
#if UNITY_WEBGL && !UNITY_EDITOR
            RPSession_Download(name, json);
            return "Downloading " + name;
#else
            var dir = Folder();
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, name);
            File.WriteAllText(path, json);
            return "Saved " + Path.GetFullPath(path);
#endif
        }

        /// <summary>The sessions folder for this platform (not WebGL).</summary>
        public static string Folder()
        {
            var repo = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var plan = Path.Combine(repo, "plan", "playtest");
            if (Application.isEditor || Directory.Exists(plan)) return Path.Combine(plan, "sessions");
            return Path.Combine(Application.persistentDataPath, "sessions");
        }
    }
}
