#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Stage;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M9c S0: a byte-exact fingerprint of one voyage, written as a human-readable golden under
    /// plan/v5/goldens/voyage-fingerprint-&lt;case&gt;.txt. One "key: value" line per field: the per-minute run log (the M9a
    /// RunLogBuilder minutes, one line per minute, and their SHA-256), the enemies at fixed sim times (count and a SHA-256
    /// of every active unit's id, definition, flags, position and hp bits) and the final state of each ship (run clock,
    /// gold, gross gold, kills, boss kills, XP, level, hull and max hull, position, every stat, weapons with levels and
    /// upgrades, Logbooks, items, Captain). Floats are written round-trip ("R"), so equal text means equal bits. Lines
    /// starting with '#' are comments and are not compared. A mismatch fails with a field-by-field diff; the actual text is
    /// always written to Temp/V5Fingerprint/ so a new golden can be reviewed and copied in. Editor only.
    /// </summary>
    public sealed class VoyageFingerprint
    {
        public readonly string CaseId;
        readonly StringBuilder _sb = new StringBuilder();

        public VoyageFingerprint(string caseId, string description)
        {
            CaseId = caseId;
            _sb.Append("# V5 M9c S0 voyage fingerprint (byte-exact single-player gate; see plan/v5/phase-09c-multiship/PLAN.md)\n");
            foreach (var line in description.Split('\n')) _sb.Append("# ").Append(line.TrimEnd()).Append('\n');
            Line("case", caseId);
            // The view gameplay asks what is on screen (spawn placement, the leash) takes the drawn camera's aspect, i.e. the
            // Game view's size: a different Game view size moves the spawns. Recorded so a mismatch says so first.
            var cam = Camera.main;
            Line("view.aspect", cam != null ? F(cam.aspect) : "none");
        }

        public string Text => _sb.ToString();

        public static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
        public static string F(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        public void Line(string key, object value)
        {
            var text = value is float f ? F(f) : value is double d ? F(d) : Convert.ToString(value, CultureInfo.InvariantCulture);
            _sb.Append(key).Append(": ").Append(text).Append('\n');
        }

        public static string Sha256(string text)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
                var sb = new StringBuilder(64);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        // ---- The run log --------------------------------------------------------------------------------------------

        /// <summary>The M9a per-minute log (RunLogBuilder.MinutesJson): its hash, then one line per minute.</summary>
        public void RunLog(RoguePirates.V4.Telemetry.RunLogBuilder log)
        {
            var h = log.Header;
            if (h != null)
            {
                Line("runlog.header", $"seed={h.Seed} seedSource={h.SeedSource} configHash={h.ConfigHash} balanceVersion={h.BalanceVersion} contentHash={h.ContentHash} " +
                                      $"stageProfile={h.StageProfile} captain={h.Captain} startingWeapon={h.StartingWeapon}");
            }
            Line("runlog.end", log.EndReason);
            RunLog(log.MinutesJson());
        }

        public void RunLog(string minutesJson)
        {
            Line("runlog.minutes.sha256", Sha256(minutesJson));
            var split = (minutesJson ?? string.Empty).Replace("[{\"m\":", "[\n{\"m\":").Replace(",{\"m\":", "\n{\"m\":").Split('\n');
            for (var i = 0; i < split.Length; i++) Line("runlog." + i.ToString("00"), split[i]);
        }

        // ---- Enemies ------------------------------------------------------------------------------------------------

        /// <summary>Every active enemy unit (slot order): id, definition, elite/boss flags, position and hp as raw bits.</summary>
        public void Enemies(string label, V4Game game)
        {
            var sim = game != null && game.Shell != null && game.Shell.Enemies != null ? game.Shell.Enemies.Simulation : null;
            if (sim == null) { Line("enemies@" + label, "no enemy simulation"); return; }
            var agents = sim.Agents;
            var bytes = new List<byte>(4096);
            int count = 0, elites = 0, bosses = 0;
            for (var i = 0; i < agents.Length; i++)
            {
                if (!agents[i].active) continue;
                var a = agents[i];
                count++;
                if (a.isElite) elites++;
                if (a.isBoss) bosses++;
                bytes.AddRange(BitConverter.GetBytes(i));
                bytes.AddRange(BitConverter.GetBytes(a.id));
                bytes.AddRange(BitConverter.GetBytes(a.definitionIndex));
                bytes.Add((byte)((a.isElite ? 1 : 0) | (a.isBoss ? 2 : 0)));
                bytes.AddRange(BitConverter.GetBytes((int)a.affix));
                bytes.AddRange(BitConverter.GetBytes(a.position.x));
                bytes.AddRange(BitConverter.GetBytes(a.position.y));
                bytes.AddRange(BitConverter.GetBytes(a.position.z));
                bytes.AddRange(BitConverter.GetBytes(a.hp));
                bytes.AddRange(BitConverter.GetBytes(a.maxHp));
            }
            string hash;
            using (var sha = SHA256.Create())
            {
                var h = sha.ComputeHash(bytes.ToArray());
                var sb = new StringBuilder(64);
                foreach (var b in h) sb.Append(b.ToString("x2"));
                hash = sb.ToString();
            }
            var run = RunManager.Instance;
            Line("enemies@" + label, $"t={(run != null ? F(run.ElapsedSeconds) : "-")} count={count} elites={elites} bosses={bosses} sha256={hash}");
            var ship = game.Shell.Player;
            if (ship != null)
                Line("ship@" + label, $"position={F(ship.Position.x)} {F(ship.Position.z)} hull={(ship.Damage != null ? F(ship.Damage.CurrentHP) : "-")} islandImpacts={ship.IslandImpactCount} islandDamage={ship.IslandDamageCount}");
        }

        // ---- A ship's final state -----------------------------------------------------------------------------------

        /// <summary>The listed per-ship fields (ship 0 is the only ship before M9c's roster).</summary>
        public void Ship(int seat, V4Game game, PlayerShipController ship)
        {
            var p = "ship[" + seat + "].";
            var run = RunManager.Instance;
            if (run != null)
            {
                Line(p + "run.time", run.ElapsedSeconds);
                Line(p + "run.state", run.State);
                Line(p + "run.overtime", run.IsOvertime);
                Line(p + "gold", run.Gold);
                Line(p + "grossGold", run.GrossGoldEarned);
                Line(p + "grossGoldSpent", run.GrossGoldSpent);
                Line(p + "kills", run.KillCount);
                Line(p + "bossKills", run.BossKills);
                Line(p + "objectiveSecured", run.ObjectiveSecured);
                foreach (StatId stat in Enum.GetValues(typeof(StatId))) Line(p + "stat." + stat, run.Stats.Get(stat));
            }
            if (ship != null)
            {
                var hull = ship.Damage;
                Line(p + "hull", hull != null ? hull.CurrentHP : -1f);
                Line(p + "maxHull", hull != null ? hull.MaxHP : -1f);
                var pos = ship.Position;
                Line(p + "position", F(pos.x) + " " + F(pos.y) + " " + F(pos.z));
                Line(p + "islandImpacts", ship.IslandImpactCount);
                Line(p + "islandDamage", ship.IslandDamageCount);
            }
            var build = game != null ? game.Build : null;
            if (build == null) return;
            var progression = build.Progression;
            Line(p + "level", progression.Level);
            Line(p + "xp", progression.CurrentXp);
            Line(p + "xpToNext", progression.XpToNext);
            Line(p + "pendingLevelUps", progression.PendingLevelUps);
            Line(p + "captain", build.Captain.Config != null ? build.Captain.Config.captainKey : "none");
            var weapons = progression.Loadout.Weapons;
            Line(p + "weapons", weapons.Count);
            for (var i = 0; i < weapons.Count; i++)
            {
                var w = weapons[i];
                Line(p + "weapon[" + i + "]",
                    $"{w.Id} level={w.Level} evolution={progression.EvolutionOf(w.Id) ?? "-"} damage={F(w.DamageBonus)} quantity={w.QuantityBonus} " +
                    $"cooldown={F(w.CooldownMult)} speed={F(w.SpeedMult)} size={F(w.SizeMult)} duration={F(w.DurationMult)} knockback={F(w.KnockbackMult)} slow={F(w.SlowBonus)}");
            }
            var logbooks = new List<string>();
            foreach (var id in progression.OwnedLogbooks) logbooks.Add(id + ":" + progression.LogbookLevel(id));
            Line(p + "logbooks", logbooks.Count == 0 ? "-" : string.Join(" ", logbooks));
            var items = new List<string>();
            foreach (var id in build.Items.Items) items.Add(id + "x" + build.Items.StackCount(id));
            Line(p + "items", items.Count == 0 ? "-" : string.Join(" ", items));
            if (game.Context != null && game.Context.TryGet<IStageContent>(out var stage))
                Line(p + "stage", $"bossSummoned={stage.BossSummoned} bossDefeated={stage.BossDefeated} paidChests={stage.PaidChestCount}");
        }

        // ---- The golden ---------------------------------------------------------------------------------------------

        /// <summary>
        /// Pins the device settings a voyage reads into its simulation, for a fingerprint: the quality tier Mid and full effects
        /// (the effect density sets how many pieces a pickup burst scatters, so it moves the seeded pickup stream), and no
        /// automatic tier (it measures the wall-clock frame time). A tier the Editor carried from an earlier run (AutoQuality's
        /// PlayerPrefs, applied by the first presented run of a session) made the solo server voyage differ inside the full
        /// suite. Dispose puts the player's settings and the app's tier back.
        /// </summary>
        public sealed class PinnedDevice : IDisposable
        {
            readonly RoguePirates.V4.Foundation.Platform.GameSettings _settings;
            readonly QualityTier _tier;

            public PinnedDevice()
            {
                AppRoot.EnsureInitialized();
                _settings = RoguePirates.V4.Foundation.Platform.GameSettings.Current;
                _tier = AppRoot.Instance.Config.Quality;
                var pinned = _settings.Copy();
                pinned.quality = (int)QualityTier.Mid;
                pinned.reducedEffects = false;
                RoguePirates.V4.Foundation.Platform.GameSettings.UseForTests(pinned);
                AppRoot.Instance.Config.Quality = QualityTier.Mid;
            }

            public void Dispose()
            {
                RoguePirates.V4.Foundation.Platform.GameSettings.UseForTests(_settings);
                if (AppRoot.Instance != null) AppRoot.Instance.Config.Quality = _tier;
            }
        }

        public static string GoldenPath(string caseId) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "plan", "v5", "goldens", "voyage-fingerprint-" + caseId + ".txt"));

        public static string ActualPath(string caseId) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "V5Fingerprint", "voyage-fingerprint-" + caseId + ".actual.txt"));

        /// <summary>Writes the actual text to Temp, then compares it with the golden (comments ignored); fails with a diff.</summary>
        public void AssertMatchesGolden()
        {
            var actualPath = WriteActual();
            var goldenPath = GoldenPath(CaseId);
            if (!File.Exists(goldenPath))
                Assert.Fail($"No golden for {CaseId} at {goldenPath}. The actual fingerprint is at {actualPath}: review it and copy it there once it is proven stable.");
            var diff = Diff(File.ReadAllText(goldenPath), Text);
            if (diff != null) Assert.Fail($"Voyage fingerprint {CaseId} differs from {goldenPath} (actual: {actualPath}):\n{diff}");
            Debug.Log($"[V5 M9c S0] fingerprint {CaseId} matched the golden {goldenPath}");
        }

        /// <summary>
        /// The golden, or else one known variant (a file next to it; only the fields the variant records are compared, as
        /// it may predate newer fields). Logs which one matched and returns its name; fails with both diffs otherwise.
        /// </summary>
        public string AssertMatchesGoldenOrVariant(string variantName)
        {
            var actualPath = WriteActual();
            var goldenPath = GoldenPath(CaseId);
            if (!File.Exists(goldenPath))
                Assert.Fail($"No golden for {CaseId} at {goldenPath}. The actual fingerprint is at {actualPath}.");
            var diff = Diff(File.ReadAllText(goldenPath), Text);
            if (diff == null)
            {
                Debug.Log($"[V5 M9c S0] fingerprint {CaseId} matched the golden {goldenPath}");
                return "golden";
            }
            var variantPath = Path.ChangeExtension(goldenPath, null) + "." + variantName + ".txt";
            var variantDiff = File.Exists(variantPath) ? Diff(File.ReadAllText(variantPath), Text, onlyExpectedKeys: true) : "no variant file";
            if (variantDiff == null)
            {
                Debug.LogWarning($"[V5 M9c S0] fingerprint {CaseId} matched the known variant {variantName} ({variantPath}), not the golden");
                return variantName;
            }
            Assert.Fail($"Voyage fingerprint {CaseId} matches neither {goldenPath} nor its known variant {variantPath} (actual: {actualPath}).\n" +
                        $"Against the golden:\n{diff}\nAgainst variant {variantName}:\n{variantDiff}");
            return null;
        }

        /// <summary>Writes the text so far to Temp/V5Fingerprint (before coverage checks, so a failing run leaves it).</summary>
        public string WriteActual()
        {
            var actualPath = ActualPath(CaseId);
            Directory.CreateDirectory(Path.GetDirectoryName(actualPath));
            File.WriteAllText(actualPath, Text);
            Debug.Log($"[V5 M9c S0] fingerprint {CaseId} written to {actualPath}\n{Text}");
            return actualPath;
        }

        /// <summary>A field-by-field diff (null when equal): keys missing, added or changed, comments ignored.</summary>
        public static string Diff(string expected, string actual, bool onlyExpectedKeys = false)
        {
            var e = Parse(expected);
            var a = Parse(actual);
            var sb = new StringBuilder();
            var n = 0;
            foreach (var kv in e)
            {
                if (!a.TryGetValue(kv.Key, out var av)) { sb.Append("  - ").Append(kv.Key).Append(": ").Append(kv.Value).Append("   (missing)\n"); n++; continue; }
                if (av == kv.Value) continue;
                sb.Append("  ~ ").Append(kv.Key).Append("\n      expected: ").Append(kv.Value).Append("\n      actual:   ").Append(av).Append('\n');
                n++;
            }
            if (!onlyExpectedKeys)
                foreach (var kv in a)
                    if (!e.ContainsKey(kv.Key)) { sb.Append("  + ").Append(kv.Key).Append(": ").Append(kv.Value).Append("   (new)\n"); n++; }
            return n == 0 ? null : n + " field(s) differ:\n" + sb;
        }

        static Dictionary<string, string> Parse(string text)
        {
            var map = new Dictionary<string, string>();
            foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
            {
                if (raw.Length == 0 || raw[0] == '#') continue;
                var i = raw.IndexOf(": ", StringComparison.Ordinal);
                var key = i < 0 ? raw : raw.Substring(0, i);
                var value = i < 0 ? string.Empty : raw.Substring(i + 2);
                map[key] = value;
            }
            return map;
        }
    }
}
#endif
