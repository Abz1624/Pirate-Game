using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using RoguePirates.V4.DevTools.Trace;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    [Category("V4")]
    public sealed class TraceComparerTests
    {
        static readonly string[] Base =
        {
            "{\"type\":\"header\",\"seed\":101,\"label\":\"a\"}",
            "{\"type\":\"spawn\",\"t\":2.0167,\"f\":121,\"uid\":1,\"enemy\":\"swordfish\"}",
            "{\"type\":\"level\",\"t\":30.1,\"f\":1800,\"level\":1}",
            "{\"type\":\"minute\",\"m\":0,\"spawns\":{\"swordfish\":20},\"kills\":10,\"gold\":3,\"level\":1,\"hull\":100}",
            "{\"type\":\"death\",\"t\":150}"
        };

        [Test]
        public void Exact_IgnoresHeaderAndFrameNumbers()
        {
            var other = (string[])Base.Clone();
            other[0] = "{\"type\":\"header\",\"seed\":101,\"label\":\"b\"}";
            other[1] = other[1].Replace("\"f\":121", "\"f\":122");
            Assert.IsTrue(TraceComparer.CompareExact(Base, other).Match);
        }

        [Test]
        public void Exact_ReportsTheFirstDifferentLine()
        {
            var other = (string[])Base.Clone();
            other[2] = other[2].Replace("30.1", "30.2");
            var result = TraceComparer.CompareExact(Base, other);
            Assert.IsFalse(result.Match);
            StringAssert.Contains("line 1", result.Differences[0]);
        }

        [Test]
        public void Parity_AcceptsSmallMinuteDrift_RejectsLargeOne()
        {
            var close = (string[])Base.Clone();
            close[3] = close[3].Replace("\"swordfish\":20", "\"swordfish\":21");
            Assert.IsTrue(TraceComparer.CompareParity(Base, close).Match);

            var far = (string[])Base.Clone();
            far[3] = far[3].Replace("\"swordfish\":20", "\"swordfish\":30");
            Assert.IsFalse(TraceComparer.CompareParity(Base, far).Match);
        }

        [Test]
        public void Exact_RejectsTracesWithDifferentKeyHeaderFields()
        {
            var other = (string[])Base.Clone();
            other[0] = "{\"type\":\"header\",\"seed\":202,\"label\":\"a\"}";
            var result = TraceComparer.CompareExact(Base, other);
            Assert.IsFalse(result.Match);
            StringAssert.Contains("header seed", result.Differences[0]);
        }

        [Test]
        public void Parity_ChecksEventTimes()
        {
            var late = (string[])Base.Clone();
            late[4] = "{\"type\":\"death\",\"t\":160}";
            Assert.IsFalse(TraceComparer.CompareParity(Base, late).Match);
        }
    }

    [Category("V4")]
    public sealed class V4StructureTests
    {
        static readonly string[] V4Assemblies =
        {
            "RoguePirates.V4.Config", "RoguePirates.V4", "RoguePirates.V4.Presentation", "RoguePirates.V4.DevTools"
        };

        static string GameRoot => Path.Combine(Application.dataPath, "_Game");

        static IEnumerable<Assembly> LoadedV4Assemblies() =>
            AppDomain.CurrentDomain.GetAssemblies().Where(a => V4Assemblies.Contains(a.GetName().Name));

        [Test]
        public void NoAdapter_OutlivesItsDeletionPhase()
        {
            var overdue = new List<string>();
            foreach (var assembly in LoadedV4Assemblies())
            foreach (var type in assembly.GetTypes())
            {
                var attr = type.GetCustomAttribute<DeletedInPhaseAttribute>();
                if (attr != null && attr.Phase <= V4Phase.Current)
                    overdue.Add($"{type.FullName} (delete in M{attr.Phase})");
            }
            Assert.IsEmpty(overdue, "Adapters past their deletion phase: " + string.Join(", ", overdue));
        }

        [Test]
        public void AssemblyReferences_FollowTheLayering()
        {
            // Config <- Runtime <- Presentation <- DevTools; the legacy runtime never references V4.
            var forbidden = new Dictionary<string, string[]>
            {
                ["V4/Config/RoguePirates.V4.Config.asmdef"] = new[] { "RoguePirates.V4\"", "RoguePirates.V4.Presentation", "RoguePirates.V4.DevTools", "HighSeas.Runtime" },
                ["V4/Runtime/RoguePirates.V4.asmdef"] = new[] { "RoguePirates.V4.Presentation", "RoguePirates.V4.DevTools", "RoguePirates.V4.Editor", "HighSeas.Editor" },
                ["V4/Presentation/RoguePirates.V4.Presentation.asmdef"] = new[] { "RoguePirates.V4.DevTools", "RoguePirates.V4.Editor", "HighSeas.Editor" },
                ["V4/DevTools/RoguePirates.V4.DevTools.asmdef"] = new[] { "RoguePirates.V4.Editor", "HighSeas.Editor" },
                ["Scripts/HighSeas.Runtime.asmdef"] = new[] { "RoguePirates.V4" }
            };
            foreach (var pair in forbidden)
            {
                var text = File.ReadAllText(Path.Combine(GameRoot, pair.Key));
                foreach (var name in pair.Value)
                    Assert.IsFalse(text.Contains("\"" + name.TrimEnd('"') + "\""), $"{pair.Key} must not reference {name.TrimEnd('"')}");
            }

            // References by GUID would slip past the name checks above, so V4 asmdefs must reference by name.
            foreach (var asmdef in Directory.GetFiles(Path.Combine(GameRoot, "V4"), "*.asmdef", SearchOption.AllDirectories))
                StringAssert.DoesNotContain("\"GUID:", File.ReadAllText(asmdef), $"{Path.GetFileName(asmdef)} must reference assemblies by name");
        }

        [Test]
        public void DevTools_IsCompiledOnlyInEditorAndDevelopmentBuilds()
        {
            var text = File.ReadAllText(Path.Combine(GameRoot, "V4/DevTools/RoguePirates.V4.DevTools.asmdef"));
            StringAssert.Contains("UNITY_EDITOR || DEVELOPMENT_BUILD", text);
        }

        /// <summary>
        /// M3: V4 never drives the legacy spawner. The kept EncounterSpawner (Beacon boss, Powder Cache, Dead Calm
        /// sink) is touched only by LegacyEncounterExecutor (LegacySpawnAdapters.cs) and the scene setup tool; the
        /// legacy WeaponController only by V4LegacyBridge until M5/M9.
        /// </summary>
        [Test]
        public void OnlyNamedAdapters_ReferenceLegacySpawnerAndWeaponController()
        {
            var allowed = new Dictionary<string, string[]>
            {
                ["EncounterSpawner"] = new[] { "Runtime/Legacy/LegacySpawnAdapters.cs", "Editor/V4SceneSetup.cs" },
                // M5: the switched-off controller is the loadout record: the bridge, the record adapter and the scene setup.
                ["WeaponController"] = new[] { "Runtime/Legacy/V4LegacyBridge.cs", "Runtime/Legacy/LegacyWeaponAdapters.cs", "Runtime/Legacy/LegacyWeaponLab.cs", "Editor/V4SceneSetup.cs" },
            };
            var offenders = Scan(@"\b(EncounterSpawner|WeaponController)\b", (rel, match) => !allowed[match].Contains(rel));
            Assert.IsEmpty(offenders, "EncounterSpawner / WeaponController outside their adapters: " + string.Join(", ", offenders));
        }

        /// <summary>M3: the V3 spawn directors are replaced; nothing outside Legacy/ (and the V3-golden trace fallback) uses them.</summary>
        [Test]
        public void NoV4Code_UsesTheV3SpawnDirectors()
        {
            var offenders = Scan(@"\b(V3PressureBeatDirector|V3OvertimeDirector|V3SaltwindStormDirector|V3StormEdgeDamage|V3RouteEventDirector|SpawnDirectorDebugUI|V3MinibossArena)\b",
                (rel, match) => !rel.StartsWith("Runtime/Legacy/") && !(rel == "DevTools/Trace/RunTraceRecorder.cs" && match == "V3MinibossArena"));
            Assert.IsEmpty(offenders, "V3 spawn directors used by V4 code: " + string.Join(", ", offenders));
        }

        /// <summary>Code lines (not comments) under V4/ matching the pattern where <paramref name="offends"/> says so.</summary>
        static List<string> Scan(string pattern, System.Func<string, string, bool> offends)
        {
            var regex = new Regex(pattern);
            var root = Path.Combine(GameRoot, "V4");
            var offenders = new List<string>();
            foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var rel = file.Substring(root.Length + 1).Replace('\\', '/');
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    var code = lines[i].Trim();
                    if (code.StartsWith("//")) continue;
                    foreach (Match m in regex.Matches(code))
                        if (offends(rel, m.Groups[1].Value)) offenders.Add($"{rel}:{i + 1} {m.Groups[1].Value}");
                }
            }
            return offenders;
        }
    }
}
