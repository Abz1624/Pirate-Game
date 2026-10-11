using System;
using System.Collections;
using RoguePirates.V4;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoguePirates.V5.Net
{
    /// <summary>
    /// V5 M9b (I9b.1): the server composition. It is the shipped run (V4Game in V4SaltwindRun: the same modules, ports and
    /// config) with switches set before the run starts: no presenters or console (<see cref="RunRequest.Headless"/>),
    /// the run's module clock at the net tick, 30 Hz (a <c>run.tickRate</c> preset, applied before anything reads config),
    /// and (V5 M9c S1) a Driven session clock (<see cref="RunRequest.DrivenClock"/>): the run moves only when the server
    /// steps it, once per net tick (<see cref="NetServer"/>), never on Unity's frame. Nothing of the gameplay is forked: <see cref="NetServer.Attach"/> reads the run through its context and
    /// ports and steps its ship through <see cref="RoguePirates.V4.Ports.ShipMotionPort"/>.
    /// </summary>
    public static class NetServerComposition
    {
        /// <summary>The run config's schema the preset is written against (RunConfig.asset).</summary>
        const int RunConfigSchema = 1;

        public static string ThirtyHertzPreset()
        {
            var preset = new ConfigPreset { name = "v5-net-server" };
            var entry = new ConfigPresetEntry { configId = "run", schemaVersion = RunConfigSchema };
            entry.edits.Add(new ConfigPresetEdit { path = nameof(RunConfig.tickRate), value = NetProtocol.TickRate.ToString() });
            preset.configs.Add(entry);
            return PresetCodec.ToJson(preset);
        }

        /// <summary>Call before the run scene loads (directly, or through a Scenario Lab load).</summary>
        public static void PrepareHeadlessRun()
        {
            RunRequest.Headless = true;
            RunRequest.DrivenClock = true; // V5 M9c S1
            RunRequest.RequestPreset(ThirtyHertzPreset());
        }

        /// <summary>Back to a normal client session (tests call it in their tear-down).</summary>
        public static void EndHeadlessRun()
        {
            RunRequest.Headless = false;
            RunRequest.DrivenClock = false;
            RunRequest.ClearActivePreset();
        }

        /// <summary>A server's own run start (the spike scene's Server role; the Linux server in part 2): the run scene on
        /// <paramref name="seed"/>, waiting until it plays.</summary>
        public static IEnumerator LoadRun(int seed, Action<V4Game> ready, float timeoutSeconds = 30f)
        {
            AppRoot.EnsureInitialized();
            PrepareHeadlessRun();
            RunRequest.ClearPvp();
            RunRequest.RequestSeed(seed);
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            V4Game game = null;
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (game == null) game = UnityEngine.Object.FindFirstObjectByType<V4Game>();
                if (game != null && game.IsRunning && RunManager.Instance != null && RunManager.Instance.State == RunState.Playing) break;
                yield return null;
            }
            // V5 M9b part 2b: the level-up cards go to the player (NetServer.Attach registers NetLevelUps as the run's card
            // presenter); part 2a's automatic level-ups without cards are gone.
            ready?.Invoke(game != null && game.IsRunning ? game : null);
        }
    }
}
