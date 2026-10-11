using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using RoguePirates.V4.Foundation.Core;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M9c S1: the per-session clock. A UnityFrame clock is Unity's frame (single player unchanged); a Driven clock moves
    /// only when its owner steps it (a match server's session; a hold is "not stepped", never <c>Time.timeScale</c>). The
    /// structure test holds the run's gameplay code (Foundation, Runtime) to the session clock: Unity's frame time is read in
    /// <see cref="SessionClock"/> only, apart from the visual-only reads on the allow-list below (each with its reason).
    /// </summary>
    [Category("V5")]
    public sealed class V5SessionClockTests
    {
        [Test]
        public void SessionClock_UnityFrameMatchesTime()
        {
            var clock = new SessionClock();
            Assert.AreEqual(SessionClock.ClockMode.UnityFrame, clock.Mode);
            Assert.IsFalse(clock.IsDriven);
            Assert.AreEqual(Time.deltaTime, clock.DeltaTime);
            Assert.AreEqual(Time.timeAsDouble, clock.Time);
            Assert.AreEqual(Time.time, clock.TimeSingle);
            Assert.AreEqual(Time.fixedDeltaTime, clock.FixedDeltaTime);
            Assert.AreEqual(Time.frameCount, clock.FrameCount);
            Assert.AreEqual(Time.timeScale <= 0f, clock.Held);
            Assert.IsFalse(clock.Stepping);
            Assert.Throws<System.InvalidOperationException>(() => clock.BeginStep(0.1f), "a UnityFrame clock is Unity's: nobody steps it");
            Assert.Throws<System.InvalidOperationException>(() => clock.SetHeld(true), "nor holds it");
        }

        [Test]
        public void SessionClock_DrivenHoldsWhenNotStepped()
        {
            var clock = new SessionClock(SessionClock.ClockMode.Driven, 1f / 30f);
            Assert.IsTrue(clock.IsDriven);
            Assert.AreEqual(0.0, clock.Time);
            Assert.AreEqual(0, clock.FrameCount);
            Assert.AreEqual(0f, clock.DeltaTime, "no step, no delta time");
            Assert.AreEqual(1f / 30f, clock.FixedDeltaTime);

            const float dt = 1f / 30f;
            clock.BeginStep(dt);
            Assert.IsTrue(clock.Stepping);
            Assert.AreEqual(dt, clock.DeltaTime);
            Assert.AreEqual((double)dt, clock.Time);
            Assert.AreEqual(1, clock.FrameCount);
            Assert.Throws<System.InvalidOperationException>(() => clock.BeginStep(dt), "no step inside a step");
            clock.EndStep();
            Assert.IsFalse(clock.Stepping);
            Assert.AreEqual(0f, clock.DeltaTime, "between steps nothing moves");

            // Not stepped (Unity frames going by) = held: the clock stands still whatever Unity's time does.
            var time = clock.Time;
            var frames = clock.FrameCount;
            Assert.AreEqual(time, clock.Time);
            Assert.AreEqual(frames, clock.FrameCount);

            clock.SetHeld(true);
            Assert.IsTrue(clock.Held);
            clock.SetHeld(false);
            Assert.IsFalse(clock.Held);

            for (var i = 0; i < 29; i++) { clock.BeginStep(dt); clock.EndStep(); }
            Assert.AreEqual(30, clock.FrameCount);
            Assert.AreEqual(30L, clock.Steps);
            var expected = 0.0;
            for (var i = 0; i < 30; i++) expected += dt;
            Assert.AreEqual(expected, clock.Time, "the steps' sum, in double");
            Assert.Throws<System.ArgumentOutOfRangeException>(() => clock.BeginStep(0f));
        }

        // ---- Structure ----------------------------------------------------------------------------------------------------

        const string GameRoot = "Assets/_Game/";

        /// <summary>Unity's frame time and its time scale (scaled time, the fixed step, the frame count). Unscaled and real time
        /// (UI animation, performance probes, network timers) are wall-clock by intent and not banned here.</summary>
        static readonly Regex FrameTime = new Regex(@"\bTime\s*\.\s*(deltaTime|time|timeAsDouble|fixedDeltaTime|fixedTime|fixedTimeAsDouble|timeScale|frameCount|smoothDeltaTime|captureDeltaTime|maximumDeltaTime|inFixedTimeStep)\b");

        /// <summary>The visual-only reads left on Unity's frame (file, how many, why). Anything else in Foundation or Runtime
        /// reads the run's <see cref="SessionClock"/>.</summary>
        static readonly (string file, int count, string reason)[] VisualOnly =
        {
            ("V4/Foundation/Core/SessionClock.cs", 6, "the session clock itself: its UnityFrame mode is Unity's frame"),
            ("V4/Foundation/Player/FollowCamera.cs", 3, "the drawn camera's follow and rotation smoothing (LateUpdateView, UpdateSmoothedAnchor, SmoothFactor); the simulation view gameplay asks reads the session clock"),
            ("V4/Foundation/Pickups/FlyingHealthBillboard.cs", 1, "the floating hull number's fade and rise"),
            ("V4/Foundation/POI/SalvageChestTreasureGlow.cs", 1, "a chest's glow pulse"),
            ("V4/Foundation/POI/SalvageChestWaterFloat.cs", 1, "a chest's bob on the water (height only; the interaction reads its position on the water plane)"),
            ("V4/Foundation/VFX/EnemyBurnFlash.cs", 2, "a burning enemy's flash"),
            ("V4/Foundation/VFX/WorldHealthBarBatch.cs", 1, "how often the health bars are depth-sorted"),
            ("V4/Foundation/World/PlayBoundsWallSegment.cs", 1, "the storm wall's texture scroll"),
            ("V4/Foundation/Platform/TouchAbilityButton.cs", 1, "touch controls go inert while Unity's frame is frozen (the dev console's time slider)"),
            ("V4/Foundation/Platform/TouchActionButton.cs", 1, "touch controls go inert while Unity's frame is frozen (the dev console's time slider)"),
            ("V4/Foundation/Platform/VirtualJoystick.cs", 1, "touch controls go inert while Unity's frame is frozen (the dev console's time slider)"),
        };

        static string Strip(string source)
        {
            // Comments (line, doc and block) and string literals are not code.
            var sb = new StringBuilder(source.Length);
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                if (c == '/' && i + 1 < source.Length && source[i + 1] == '/') { while (i < source.Length && source[i] != '\n') i++; sb.Append('\n'); continue; }
                if (c == '/' && i + 1 < source.Length && source[i + 1] == '*') { i += 2; while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/')) { if (source[i] == '\n') sb.Append('\n'); i++; } i++; continue; }
                if (c == '"')
                {
                    var verbatim = i > 0 && source[i - 1] == '@';
                    sb.Append('"');
                    i++;
                    while (i < source.Length && source[i] != '"' && (verbatim || source[i] != '\n')) { if (!verbatim && source[i] == '\\') i++; i++; }
                    sb.Append('"');
                    continue;
                }
                if (c == '\'' && i + 2 < source.Length) { var end = source.IndexOf('\'', i + 1 + (source[i + 1] == '\\' ? 2 : 1)); if (end > i && end - i <= 4) { sb.Append("' '"); i = end; continue; } }
                sb.Append(c);
            }
            return sb.ToString();
        }

        static Dictionary<string, List<string>> FrameTimeReads(params string[] folders)
        {
            var hits = new Dictionary<string, List<string>>();
            foreach (var folder in folders)
            {
                var root = Path.Combine(Application.dataPath, "_Game", folder);
                foreach (var path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    var rel = path.Substring(Path.Combine(Application.dataPath, "_Game").Length + 1).Replace('\\', '/');
                    var lines = Strip(File.ReadAllText(path)).Split('\n');
                    for (var n = 0; n < lines.Length; n++)
                        foreach (Match m in FrameTime.Matches(lines[n]))
                        {
                            if (!hits.TryGetValue(rel, out var list)) hits[rel] = list = new List<string>();
                            list.Add($"{rel}:{n + 1}: {m.Value}");
                        }
                }
            }
            return hits;
        }

        [Test]
        public void Structure_NoFrameTimeInGameplay_FoundationAndRuntime()
        {
            var hits = FrameTimeReads("V4/Foundation", "V4/Runtime");
            var problems = new List<string>();
            foreach (var kv in hits)
            {
                var allowed = VisualOnly.FirstOrDefault(a => a.file == kv.Key);
                if (allowed.file == null) { problems.AddRange(kv.Value.Select(h => h + "  (gameplay: read the run's SessionClock)")); continue; }
                if (kv.Value.Count != allowed.count)
                    problems.Add($"{kv.Key}: {kv.Value.Count} frame-time reads, the allow-list has {allowed.count} ({allowed.reason}); route new ones through the SessionClock or update the list with a reason:\n    " + string.Join("\n    ", kv.Value));
            }
            foreach (var a in VisualOnly)
                if (!hits.ContainsKey(a.file)) problems.Add($"{a.file}: on the allow-list but reads no frame time any more (remove it)");
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        /// <summary>The server's match code never writes Unity's time scale (a hold is the session not being stepped).</summary>
        [Test]
        public void Structure_TheServerNeverWritesTimeScale()
        {
            var write = new Regex(@"\bTime\s*\.\s*timeScale\s*=[^=]");
            var problems = new List<string>();
            foreach (var folder in new[] { "V5/Net", "V4/Foundation", "V4/Runtime" })
            {
                var root = Path.Combine(Application.dataPath, "_Game", folder);
                foreach (var path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    var lines = Strip(File.ReadAllText(path)).Split('\n');
                    for (var n = 0; n < lines.Length; n++)
                        if (write.IsMatch(lines[n])) problems.Add($"{path}:{n + 1}");
                }
            }
            Assert.IsEmpty(problems, "Time.timeScale written in shipped match or gameplay code:\n" + string.Join("\n", problems));
        }
    }
}
