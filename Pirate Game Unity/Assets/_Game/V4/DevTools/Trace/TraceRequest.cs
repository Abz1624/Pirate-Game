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
        public string label = "trace";
        public int seed = 101;
        public float captureDeltaTime = 1f / 60f;
        public float bossAtSeconds = 240f;
        /// <summary>The harness kills the boss at this time through EnemyWorld.ApplyDamage (the weapon-hit path), so
        /// victory, objective and settlement are covered by exact traces. The pilot kites the boss instead of fighting it.</summary>
        public float killBossAtSeconds = 300f;
        public float maxSimSeconds = 900f;
        public int pauseResponseFrames = 3;
        public string pilot = PlayerPilot.PatternId;
        public string outPath;
        public string commit;
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
