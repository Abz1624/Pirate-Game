using System;
using System.Collections.Generic;
using System.IO;
using RoguePirates.V4.DevTools.Trace;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// Starts one pinned trace run in the open Editor: snapshots the PlayerPrefs a run can touch, pins the Captain,
    /// fixes the Game view resolution, writes the trace request and enters Play Mode. Prefs are restored when the
    /// Editor returns to Edit Mode. Callable from `unity command eval`.
    /// </summary>
    [InitializeOnLoad]
    public static class V4TraceSession
    {
        const string SnapshotKey = "v4trace.prefsSnapshot";
        const string StatusKey = "v4trace.status";

        static readonly string[] IntKeys =
        {
            "v3_selected_captain", "v3_home_seed", "v3_first_conquest_complete",
            "v3_saltwind_runs_completed", "combat_captain_actives_enabled"
        };
        static readonly string[] StringKeys = { "highseas_meta_v1", "highseas_run_snapshot_v1" };

        [Serializable] sealed class PrefEntry { public string key; public bool has; public bool isInt; public int i; public string s; }
        [Serializable] sealed class PrefSnapshot { public List<PrefEntry> entries = new List<PrefEntry>(); }

        static V4TraceSession()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static string Status => SessionState.GetString(StatusKey, "idle");

        /// <summary>Starts a trace. Returns "started" or a reason it could not start.</summary>
        public static string Run(string scenePath, int seed, string outPath, string label,
            float bossAtSeconds = 240f, float maxSimSeconds = 900f)
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return "busy: already in Play Mode";
            if (!File.Exists(Path.Combine(Directory.GetParent(Application.dataPath).FullName, scenePath)))
                return $"missing scene {scenePath}";

            var active = EditorSceneManager.GetActiveScene();
            if (active.path != scenePath)
            {
                if (active.isDirty) return $"active scene {active.path} has unsaved changes";
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            UnloadGameDatabase();
            SnapshotPrefs();
            PlayerPrefs.SetInt("v3_selected_captain", 0); // Rook
            PlayerPrefs.SetInt("combat_captain_actives_enabled", 0);
            PlayerPrefs.Save();

            var fullOut = Path.IsPathRooted(outPath)
                ? outPath
                : Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "..", outPath));
            if (File.Exists(fullOut)) File.Delete(fullOut);

            new TraceRequest
            {
                label = label,
                seed = seed,
                outPath = fullOut,
                bossAtSeconds = bossAtSeconds,
                maxSimSeconds = maxSimSeconds
            }.WriteRequestFile();

            PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "V4 Trace 1080p");
            SessionState.SetString(StatusKey, $"running {label} -> {fullOut}");
            EditorApplication.isPlaying = true;
            return "started";
        }

        /// <summary>
        /// Enters Play Mode on a scene for the automated playable check (no trace request): prefs are snapshotted
        /// and restored like a trace, the Captain is pinned to Rook and the Game view is fixed at 1080p.
        /// </summary>
        public static string BeginPlayableCheck(string scenePath)
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return "busy: already in Play Mode";
            var active = EditorSceneManager.GetActiveScene();
            if (active.path != scenePath)
            {
                if (active.isDirty) return $"active scene {active.path} has unsaved changes";
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            UnloadGameDatabase();
            SnapshotPrefs();
            PlayerPrefs.SetInt("v3_selected_captain", 0);
            PlayerPrefs.Save();
            PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "V4 Trace 1080p");
            SessionState.SetString(StatusKey, $"playable check on {scenePath}");
            EditorApplication.isPlaying = true;
            return "started";
        }

        /// <summary>
        /// Each run applies the weapon/specialist catalogs to the loaded GameDatabase in memory, and the stage profile
        /// hash snapshots those stats before that step (R-73). Unloading makes every run start from the asset on disk.
        /// </summary>
        static void UnloadGameDatabase()
        {
            var database = HighSeas.Core.GameDatabaseLoader.Load();
            if (database != null && !EditorUtility.IsDirty(database)) Resources.UnloadAsset(database);
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            if (!string.IsNullOrEmpty(SessionState.GetString(SnapshotKey, string.Empty)))
            {
                RestorePrefs();
                SessionState.SetString(StatusKey, "idle (last run finished, prefs restored)");
            }
        }

        static void SnapshotPrefs()
        {
            var snapshot = new PrefSnapshot();
            foreach (var key in IntKeys)
                snapshot.entries.Add(new PrefEntry { key = key, has = PlayerPrefs.HasKey(key), isInt = true, i = PlayerPrefs.GetInt(key, 0) });
            foreach (var key in StringKeys)
                snapshot.entries.Add(new PrefEntry { key = key, has = PlayerPrefs.HasKey(key), s = PlayerPrefs.GetString(key, string.Empty) });
            SessionState.SetString(SnapshotKey, JsonUtility.ToJson(snapshot));
        }

        static void RestorePrefs()
        {
            var snapshot = JsonUtility.FromJson<PrefSnapshot>(SessionState.GetString(SnapshotKey, "{}"));
            if (snapshot?.entries != null)
            {
                foreach (var e in snapshot.entries)
                {
                    if (!e.has) { PlayerPrefs.DeleteKey(e.key); continue; }
                    if (e.isInt) PlayerPrefs.SetInt(e.key, e.i);
                    else PlayerPrefs.SetString(e.key, e.s);
                }
                PlayerPrefs.Save();
            }
            SessionState.EraseString(SnapshotKey);
        }

        /// <summary>Compares two trace files; mode "exact" or "parity". Returns a readable report.</summary>
        public static string Compare(string expectedPath, string actualPath, string mode = "exact")
        {
            var root = Directory.GetParent(Application.dataPath).FullName;
            string Full(string p) => Path.IsPathRooted(p) ? p : Path.GetFullPath(Path.Combine(root, "..", p));
            var a = File.ReadAllLines(Full(expectedPath));
            var b = File.ReadAllLines(Full(actualPath));
            var result = mode == "parity" ? TraceComparer.CompareParity(a, b) : TraceComparer.CompareExact(a, b);
            return result.ToString();
        }
    }
}
