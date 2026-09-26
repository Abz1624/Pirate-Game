using System;
using System.IO;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Trace
{
    /// <summary>
    /// Parameters for one scripted, seeded trace run. The Editor writes it to Temp/ before entering Play Mode
    /// (the domain reloads on Play, so statics can't carry it); the runtime consumes it at BeforeSceneLoad.
    /// </summary>
    [Serializable]
    public sealed class TraceRequest
    {
        /// <summary>Empty first scene of the trace player build; TraceBoot loads the requested run scene from it.</summary>
        public const string LauncherSceneName = "V4TraceLauncher";

        public string label = "trace";
        public int seed = 101;
        public float captureDeltaTime = 1f / 60f;
        public float bossAtSeconds = 240f;
        /// <summary>The harness kills the boss at this time through EnemyWorld.ApplyDamage (the weapon-hit path), so
        /// victory, objective and settlement are covered by exact traces. The pilot kites the boss instead of fighting it.</summary>
        public float killBossAtSeconds = 300f;
        /// <summary>The harness kills a miniboss-arena focus enemy (Reef Crab, elite beats) this long after the arena
        /// opens, as a player would; the kiting pilot never fights it, and the arena pauses ambient spawns until it dies.
        /// Negative disables.</summary>
        public float killArenaFocusAfterSeconds = 20f;
        public float maxSimSeconds = 900f;
        public int pauseResponseFrames = 3;
        public string pilot = PlayerPilot.PatternId;
        public string outPath;
        /// <summary>Run scene path or name; used by player builds, which start in the empty launcher scene.</summary>
        public string scene;
        public string commit;
        /// <summary>Optional config preset (JSON file) applied before the run starts; empty = default configs.</summary>
        public string presetPath;
        public long createdUtcTicks;
        public bool stopPlayModeWhenDone = true;

        public static string RequestFilePath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "v4-trace-request.json"));

        public void WriteRequestFile()
        {
            var path = RequestFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            createdUtcTicks = DateTime.UtcNow.Ticks;
            File.WriteAllText(path, JsonUtility.ToJson(this, true));
        }

        /// <summary>Player builds: <c>-v4trace &lt;path&gt;</c> on the command line; the file is kept (the orchestrator owns it).</summary>
        public static TraceRequest TryReadFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-v4trace") continue;
                try { return JsonUtility.FromJson<TraceRequest>(File.ReadAllText(args[i + 1])); }
                catch (Exception e) { Debug.LogError($"V4 trace: could not read {args[i + 1]}: {e.Message}"); return null; }
            }
            return null;
        }

        /// <summary>Reads and deletes the pending request file; null when none is pending.</summary>
        public static TraceRequest TryConsume()
        {
            var path = RequestFilePath;
            if (!File.Exists(path)) return null;
            try
            {
                var request = JsonUtility.FromJson<TraceRequest>(File.ReadAllText(path));
                File.Delete(path);
                // A request left behind by a trace whose Play Mode never started must not hijack a later manual session.
                if (request == null || DateTime.UtcNow.Ticks - request.createdUtcTicks > TimeSpan.FromMinutes(2).Ticks)
                {
                    Debug.LogWarning($"V4 trace: ignored a stale request file ({request?.label}).");
                    return null;
                }
                return request;
            }
            catch (Exception e)
            {
                Debug.LogError($"V4 trace: could not read {path}: {e.Message}");
                return null;
            }
        }
    }
}
