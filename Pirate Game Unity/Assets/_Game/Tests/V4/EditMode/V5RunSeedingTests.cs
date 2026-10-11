using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Ports;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M9a (I9a.2): every gameplay draw goes through the run's seeded streams. A source scan fails when gameplay code
    /// under V4/Runtime or V4/Foundation uses UnityEngine.Random or System.Random outside the sanctioned stream class
    /// (RunRng.cs: RunRng, RunStream, RunSeeds); the seed can be injected from outside (a server-issued seed, M10).
    /// </summary>
    [Category("V5")]
    public sealed class V5RunSeedingTests
    {
        /// <summary>
        /// Files under the scanned folders that may draw directly, each with its reason. Presentation-only and VFX draws
        /// live in V4/Presentation (not gameplay, not scanned); none are left inside V4/Runtime or V4/Foundation.
        /// </summary>
        static readonly Dictionary<string, string> Allowlist = new Dictionary<string, string>
        {
            ["V4/Foundation/Core/RunRng.cs"] = "the sanctioned stream class: RunRng (the run's five streams), RunStream (a module's own seeded stream), RunSeeds (a new local seed)",
        };

        static readonly string[] ScannedRoots = { "V4/Runtime", "V4/Foundation" };

        static readonly Regex[] Direct =
        {
            new Regex(@"\bUnityEngine\s*\.\s*Random\b"),
            new Regex(@"\bSystem\s*\.\s*Random\b"),
            new Regex(@"\bnew\s+Random\s*\("),
            new Regex(@"\busing\s+Random\s*="),
        };

        /// <summary>UnityEngine.Random's static members reached through <c>using UnityEngine;</c>.</summary>
        static readonly Regex BareUnityRandom = new Regex(@"(?<![\w.])Random\s*\.\s*(Range|value|insideUnitCircle|insideUnitSphere|onUnitSphere|rotation|rotationUniform|InitState|state|ColorHSV)\b");
        /// <summary>
        /// A member named Random (an IRunRandom/IPickupRandom field or property): inside the type that declares it (and its
        /// nested types), "Random.Range" is that member. V5 M9a (review): the exemption covers only that type's body, not the
        /// whole file, so another type in the same file using UnityEngine's bare <c>Random.</c> is still found.
        /// </summary>
        static readonly Regex RandomMember = new Regex(@"\b(?!(?:return|new|else|case|throw|await|yield|in|out|ref|is|as|using|goto)\b)[\w<>.\[\]]+\s+Random\s*(=|;|\{)");
        static readonly Regex TypeDeclaration = new Regex(@"\b(class|struct|record|interface)\s+\w+[^{;]*\{");

        /// <summary>
        /// V5 M9a (review): a clock or GUID used as a seed (a run's draws must come from its seed, which only RunSeeds in
        /// RunRng.cs may pick). Found per clause (the code split at ; , { }): a clause naming one of these sources and a seed
        /// (any identifier containing "seed", a RunStream or RunRng, InitState, a Random constructor, GetHashCode or .Ticks).
        /// Timestamps and ids (DateTime.Now.ToString, Guid.NewGuid().ToString) are not seeds.
        /// </summary>
        static readonly Regex ClockOrGuid = new Regex(@"\bEnvironment\s*\.\s*TickCount(64)?\b|\bDateTime(Offset)?\s*\.\s*(Now|UtcNow)\b|\bGuid\s*\.\s*NewGuid\s*\(");
        static readonly Regex SeedUse = new Regex(@"(?i:seed)|\bRunStream\b|\bRunRng\b|\bInitState\b|\bRandom\s*\(|\bGetHashCode\s*\(|\.\s*Ticks\b");

        static string GameRoot => Path.Combine(Application.dataPath, "_Game");

        /// <summary>The code with comments and string literals blanked (so a doc comment naming UnityEngine.Random is fine).</summary>
        internal static string StripCommentsAndStrings(string source)
        {
            source = Regex.Replace(source, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            source = Regex.Replace(source, @"@""(?:[^""]|"""")*""", "\"\"");
            source = Regex.Replace(source, @"""(?:\\.|[^""\\\r\n])*""", "\"\"");
            source = Regex.Replace(source, @"'(?:\\.|[^'\\\r\n])'", "' '");
            source = Regex.Replace(source, @"//[^\r\n]*", " ");
            return source;
        }

        /// <summary>The direct draws (and clock or GUID seeds) in one file's code (empty when none).</summary>
        internal static List<string> Violations(string source)
        {
            var code = StripCommentsAndStrings(source);
            var found = new List<string>();
            foreach (var r in Direct)
                foreach (Match m in r.Matches(code)) found.Add(m.Value);
            var exempt = RandomMemberScopes(code);
            foreach (Match m in BareUnityRandom.Matches(code))
                if (!exempt.Any(span => m.Index > span.start && m.Index < span.end)) found.Add(m.Value);
            foreach (var clause in code.Split(';', ',', '{', '}'))
                if (ClockOrGuid.IsMatch(clause) && SeedUse.IsMatch(clause))
                    found.Add("seed from " + ClockOrGuid.Match(clause).Value.Trim() + ": " + Regex.Replace(clause.Trim(), @"\s+", " "));
            return found;
        }

        /// <summary>The bodies (opening and closing brace index) of the types that declare a member named Random.</summary>
        static List<(int start, int end)> RandomMemberScopes(string code)
        {
            var types = new List<(int start, int end)>();
            foreach (Match t in TypeDeclaration.Matches(code))
            {
                var open = t.Index + t.Length - 1;
                var depth = 0;
                for (var i = open; i < code.Length; i++)
                {
                    if (code[i] == '{') depth++;
                    else if (code[i] == '}' && --depth == 0) { types.Add((open, i)); break; }
                }
            }
            var scopes = new List<(int start, int end)>();
            foreach (Match m in RandomMember.Matches(code))
            {
                (int start, int end)? inner = null; // the innermost type body around the declaration
                foreach (var span in types)
                    if (m.Index > span.start && m.Index < span.end && (inner == null || span.end - span.start < inner.Value.end - inner.Value.start)) inner = span;
                if (inner != null) scopes.Add(inner.Value);
            }
            return scopes;
        }

        static IEnumerable<string> ScannedFiles() =>
            ScannedRoots.SelectMany(r => Directory.GetFiles(Path.Combine(GameRoot, r), "*.cs", SearchOption.AllDirectories));

        static string Relative(string path) => path.Substring(GameRoot.Length + 1).Replace('\\', '/');

        [Test]
        public void GameplayCode_DrawsOnlyFromTheRunsSeededStreams()
        {
            var problems = new List<string>();
            var scanned = 0;
            foreach (var file in ScannedFiles())
            {
                scanned++;
                var rel = Relative(file);
                if (Allowlist.ContainsKey(rel)) continue;
                var v = Violations(File.ReadAllText(file));
                if (v.Count > 0) problems.Add($"{rel}: {string.Join(", ", v.Distinct())}");
            }
            Assert.Greater(scanned, 200, "the scan reached the gameplay code");
            Assert.IsEmpty(problems, "Gameplay code must draw from the run's seeded streams (RunRng / RunStream in V4/Foundation/Core/RunRng.cs), " +
                                     "not UnityEngine.Random or System.Random:\n" + string.Join("\n", problems));
        }

        /// <summary>
        /// V5 M9b close (determinism): the simulation code in the Runtime folders listed in <see cref="SimulationRoots"/>
        /// (enemies, spawning, weapons, stage, progression, difficulty, rewards) reads no frame clock and no drawn camera.
        /// Foundation is not scanned: its gameplay MonoBehaviours still read the frame clock in places (RunManager's elapsed
        /// time, DamageReceiver's hit immunity, the ship's roll and its island clock off DirectStep, ShipBoostController),
        /// and the spawn placement there takes its camera and ship pose from the run's port. A frame clock
        /// (Time.deltaTime, Time.time, the unscaled and realtime clocks, Time.frameCount) makes a timer depend on the frame
        /// rate (and on the match server, which steps at 30 Hz, run differently); the main camera follows the ship's
        /// interpolated transform, so asking it what is on screen depends on the frame's render phase (the seed-8282
        /// minute-0 spawns differed between two plays). They take the simulation's dt and run clock, and the follow
        /// camera's simulation view (FollowCamera.SimulationViewOf).
        /// </summary>
        static readonly string[] SimulationRoots =
        {
            "V4/Runtime/Enemies", "V4/Runtime/Spawning", "V4/Runtime/Weapons", "V4/Runtime/Stage", "V4/Runtime/Progression",
            "V4/Runtime/Difficulty", "V4/Runtime/Rewards",
        };

        static readonly Regex FrameClock = new Regex(@"\bTime\s*\.\s*(deltaTime|time|timeAsDouble|unscaledDeltaTime|unscaledTime|realtimeSinceStartup|realtimeSinceStartupAsDouble|frameCount|smoothDeltaTime)\b");
        static readonly Regex DrawnCamera = new Regex(@"(?<!SimulationViewOf\(\s*)\bCamera\s*\.\s*main\b");

        /// <summary>The frame clocks and drawn-camera reads in one file's code.</summary>
        internal static List<string> RenderStateReads(string source)
        {
            var code = StripCommentsAndStrings(source);
            var found = new List<string>();
            foreach (Match m in FrameClock.Matches(code)) found.Add(m.Value);
            foreach (Match m in DrawnCamera.Matches(code)) found.Add(m.Value);
            return found;
        }

        [Test]
        public void SimulationCode_ReadsNoFrameClockOrDrawnCamera()
        {
            var problems = new List<string>();
            var scanned = 0;
            foreach (var file in SimulationRoots.SelectMany(r => Directory.GetFiles(Path.Combine(GameRoot, r), "*.cs", SearchOption.AllDirectories)))
            {
                scanned++;
                var v = RenderStateReads(File.ReadAllText(file));
                if (v.Count > 0) problems.Add($"{Relative(file)}: {string.Join(", ", v.Distinct())}");
            }
            Assert.Greater(scanned, 60, "the scan reached the simulation code");
            Assert.IsEmpty(problems, "The simulation must step on its own dt and run clock and ask the follow camera's simulation view, " +
                                     "not the frame clock or the drawn camera:\n" + string.Join("\n", problems));

            // The scanner finds each form and ignores comments and the simulation view.
            Assert.IsNotEmpty(RenderStateReads("class A { void F() { t -= Time.deltaTime; } }"));
            Assert.IsNotEmpty(RenderStateReads("class A { bool F() => UnityEngine.Time.time < _until; }"));
            Assert.IsNotEmpty(RenderStateReads("class A { int F() => Time.frameCount; }"));
            Assert.IsNotEmpty(RenderStateReads("class A { Camera F() => Camera.main; }"));
            Assert.IsEmpty(RenderStateReads("class A { Camera F() => FollowCamera.SimulationViewOf(Camera.main); /* Time.time */ }"));
            Assert.IsEmpty(RenderStateReads("class A { float F(float dt) => Sim.RunSeconds + dt; // was Time.deltaTime\n }"));
        }

        [Test]
        public void Allowlist_NamesExistingFilesThatStillNeedIt_WithReasons()
        {
            foreach (var kv in Allowlist)
            {
                var path = Path.Combine(GameRoot, kv.Key);
                Assert.IsTrue(File.Exists(path), kv.Key + " exists");
                Assert.IsNotEmpty(Violations(File.ReadAllText(path)), kv.Key + " still draws directly (else drop it from the allowlist)");
                Assert.Greater(kv.Value.Length, 20, kv.Key + " has a reason");
            }
        }

        /// <summary>The scanner itself finds each form (so an empty result means clean code, not a blind scan).</summary>
        [Test]
        public void Scanner_FindsEveryForm_AndIgnoresCommentsStringsAndSeededMembers()
        {
            Assert.IsNotEmpty(Violations("class A { float f = UnityEngine.Random.value; }"));
            Assert.IsNotEmpty(Violations("using UnityEngine; class A { float f = Random.Range(0f, 1f); }"));
            Assert.IsNotEmpty(Violations("using UnityEngine; class A { void F() { var q = Random.rotation; } }"));
            Assert.IsNotEmpty(Violations("class A { System.Random r = new System.Random(1); }"));
            Assert.IsNotEmpty(Violations("using System; class A { Random r = new Random(); }"));
            Assert.IsNotEmpty(Violations("using Random = UnityEngine.Random; class A { }"));
            Assert.IsEmpty(Violations("class A { // UnityEngine.Random.Range was used here\n /* System.Random */ string s = \"Random.Range\"; }"));
            Assert.IsEmpty(Violations("class A { public IRunRandom Random; float F() => Random.DirectorFloat(); }"));
            Assert.IsEmpty(Violations("class A { public IPickupRandom Random = null; float F() => Random.Range(0f, 1f); }"));
            Assert.IsEmpty(Violations("class A { RunStream _r = new RunStream(5); }"));

            // V5 M9a (review): a member named Random exempts only its own type's body (and nested types), not the whole file
            Assert.IsEmpty(Violations("using UnityEngine; class A { public IPickupRandom Random; class N { float F() => Random.Range(0f, 1f); } float G() => Random.Range(0f, 1f); }"));
            Assert.IsNotEmpty(Violations("using UnityEngine; class A { public IPickupRandom Random; } class B { float F() => Random.Range(0f, 1f); }"));
            Assert.IsNotEmpty(Violations("using UnityEngine; class A { float F() => Random.value; } class B { public IRunRandom Random { get; set; } }"));
            Assert.IsNotEmpty(Violations("using UnityEngine; class A { object G() { return Random; } float F() => Random.Range(0f, 1f); }"), "return Random; is not a declaration");

            // V5 M9a (review): clocks and GUIDs as seeds
            Assert.IsNotEmpty(Violations("class A { int seed = System.Environment.TickCount; }"));
            Assert.IsNotEmpty(Violations("class A { RunStream _r = new RunStream(DateTime.Now.Millisecond); }"));
            Assert.IsNotEmpty(Violations("class A { void F() { RunSeed = Guid.NewGuid().GetHashCode(); } }"));
            Assert.IsNotEmpty(Violations("class A { void F() { UnityEngine.Random.InitState((int)DateTime.UtcNow.Ticks); } }"));
            Assert.IsNotEmpty(Violations("class A { void F() { Begin(seed: (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()); } }"));
            Assert.IsEmpty(Violations("class A { void F() { RunId = Guid.NewGuid().ToString(\"N\"); var s = DateTime.Now.ToString(\"o\"); var r = new X { seed = _seed, at = DateTime.UtcNow.ToString(\"o\") }; } }"));
        }

        [Test]
        public void RunStream_IsTheSameSequenceAsTheStreamsItReplaced()
        {
            // The rerouted modules (elite lever, barks, pickups, practice bot) used System.Random(seed ^ salt): unchanged draws.
            var stream = new RunStream(12345 ^ 0x5EED11);
            var old = new System.Random(12345 ^ 0x5EED11);
            for (var i = 0; i < 50; i++) Assert.AreEqual(old.NextDouble(), stream.NextDouble());
            Assert.AreEqual(old.Next(7), stream.Next(7));
            Assert.AreEqual(old.Next(3, 9), stream.Next(3, 9));
        }

        [Test]
        public void NewLocalSeeds_AreNonZero_AndLeaveTheGlobalRandomAlone()
        {
            Random.InitState(77);
            var before = Random.state;
            var seeds = new HashSet<int>();
            for (var i = 0; i < 100; i++)
            {
                var s = WorldPort.ResolveSeed(0, retry: true);
                Assert.Greater(s, 0);
                seeds.Add(s);
            }
            Assert.Greater(seeds.Count, 95, "fresh seeds differ");
            Assert.AreEqual(before, Random.state, "no draw from UnityEngine.Random");
            Assert.AreEqual(4242, WorldPort.ResolveSeed(4242, retry: true), "an explicit seed is kept");
        }

        [Test]
        public void SeedCanBeInjected_AsServerIssued_AndTheOriginIsRecorded()
        {
            try
            {
                RunRequest.RequestIssuedSeed(987654, "srv-run-1");
                var (seed, retry) = RunRequest.Consume();
                Assert.AreEqual(987654, seed);
                Assert.IsFalse(retry);
                Assert.AreEqual(SeedSource.Issued, RunRequest.ConsumedOrigin.Source);
                Assert.AreEqual("srv-run-1", RunRequest.ConsumedOrigin.IssuedRunId);

                RunRequest.RequestSeed(55);
                Assert.AreEqual(55, RunRequest.Consume().seed);
                Assert.AreEqual(SeedSource.Requested, RunRequest.ConsumedOrigin.Source);
                Assert.IsNull(RunRequest.ConsumedOrigin.IssuedRunId);

                Assert.AreEqual(0, RunRequest.Consume().seed, "nothing requested: the run resolves its own seed");
                Assert.AreEqual(SeedSource.Local, RunRequest.ConsumedOrigin.Source);

                Assert.Throws<System.ArgumentException>(() => RunRequest.RequestIssuedSeed(0, "x"));
            }
            finally { RunRequest.Consume(); }
        }

        [Test]
        public void SameSeed_GivesTheSameStreams()
        {
            var a = new RunRng(31337);
            var b = new RunRng(31337);
            for (var i = 0; i < 20; i++)
            {
                Assert.AreEqual(a.DirectorFloat(), b.DirectorFloat());
                Assert.AreEqual(a.LootFloat(), b.LootFloat());
                Assert.AreEqual(a.CombatFloat(), b.CombatFloat());
                Assert.AreEqual(a.MapInt(0, 1000), b.MapInt(0, 1000));
                Assert.AreEqual(a.PowerupFloat(), b.PowerupFloat());
            }
            Assert.AreNotEqual(new RunRng(1).DirectorFloat(), new RunRng(2).DirectorFloat());
        }
    }
}
