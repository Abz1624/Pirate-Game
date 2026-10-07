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

        public static void Save(string fileName, string json)
        {
            // V5 M8z (7 Oct, owner): the last voyage is kept on every platform, for the "Download last voyage" buttons.
            if (json.Length < 900_000) // PlayerPrefs on WebGL hold about 1 MB in all
            {
                PlayerPrefs.SetString(LastSessionKey, json);
                PlayerPrefs.SetString(LastSessionNameKey, fileName);
                PlayerPrefs.Save();
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
