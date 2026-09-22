using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace RoguePirates.Simulation.Balance
{
    public sealed class SaltwindBalanceLabBatchOptions
    {
        public int BaseSeed = 12000;
        public int SeedsPerCell = 100;
        public bool IncludeQuickMatrix;
    }

    public sealed class SaltwindBalanceLabCellSummary
    {
        public SaltwindLabBuildPolicy BuildPolicy;
        public BalanceSkillPreset Skill;
        public SaltwindLabUnlockPool UnlockPool;
        public int Runs;
        public BalanceLabSummary Simulation;
        public string BehaviorLabel = string.Empty;
        public string SuspiciousDominanceFlag = string.Empty;
    }

    public sealed class SaltwindBalanceLabMatrixReport
    {
        public string ProfileContentHash = string.Empty;
        public int BalanceVersion;
        public int TotalRuns;
        public readonly List<SaltwindBalanceLabCellSummary> Cells = new();
        public string BehaviorDisclaimer = SaltwindLabBehaviorLabels.Movement + " " +
                                           SaltwindLabBehaviorLabels.Collection + " " +
                                           SaltwindLabBehaviorLabels.Offers;

        public string ToHumanSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Saltwind lab matrix: {TotalRuns} runs, profile {ProfileContentHash}, balance v{BalanceVersion}");
            sb.AppendLine(BehaviorDisclaimer);
            for (var i = 0; i < Cells.Count; i++)
            {
                var c = Cells[i];
                sb.Append(c.Simulation.ToHumanSummary())
                    .Append(" | policy ").Append(c.BuildPolicy)
                    .Append(" | skill ").Append(c.Skill)
                    .Append(" | pool ").Append(c.UnlockPool);
                if (!string.IsNullOrEmpty(c.SuspiciousDominanceFlag))
                    sb.Append(" | ").Append(c.SuspiciousDominanceFlag);
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }

    public static class SaltwindBalanceLabScenarioFactory
    {
        static readonly string[] PrecisionWeapons = { "long_nine", "swivel_guns", "harpoon_gun", "deck_cannons" };
        static readonly string[] BroadsideWeapons = { "deck_cannons", "broadside_battery", "powder_kegs", "flying_cutlasses" };
        static readonly string[] AreaWeapons = { "lightning_mast", "flamethrower", "mortar", "deck_cannons" };

        public static BalanceLabScenario Create(
            StageBalanceProfileExport export,
            int seed,
            int runs,
            SaltwindLabBuildPolicy policy,
            SaltwindLabUnlockPool pool,
            BalanceSkillPreset skill)
        {
            if (export == null) throw new ArgumentNullException(nameof(export));
            var deadline = SaltwindLabRuntimeParityLogic.QuoteStageDeadline(export);
            var scenario = new BalanceLabScenario
            {
                Name = $"Saltwind/{policy}/{pool}/{skill}",
                Seed = seed,
                Runs = Math.Max(1, runs),
                SkillPreset = skill,
                DurationSeconds = deadline + 300f,
                BossSpawnSeconds = deadline,
                DirectorMaxActiveEnemies = export.Spawn.MaxActiveEnemies,
                DirectorEnabled = true,
                WeaponSlotCap = 6,
                WeaponCount = 1,
                SimulateUpgradePath = true,
                EnablePois = true,
                SalvageChestsOpened = 6,
                MerchantVisits = 5,
                PlannedMerchantPurchases = 5,
                MerchantItemPrice = SaltwindLabRuntimeParityLogic.QuoteChestPrice(export, 0),
                StageProfile = export,
                StageProfileContentHash = export.ContentHash ?? string.Empty,
                StageBalanceVersion = export.BalanceVersion,
                SaltwindBuildPolicy = policy,
                SaltwindUnlockPool = pool,
                BehaviorModelLabel = SaltwindLabBehaviorLabels.ForPolicy(policy)
            };

            var starter = ResolveWeaponQueue(policy, pool, export);
            scenario.WeaponId = starter[0];
            scenario.WeaponLoadout.Clear();
            scenario.WeaponLoadout.Add(CreateWeaponEntry(starter[0], scenario));
            return scenario;
        }

        static BalanceWeaponLoadoutEntry CreateWeaponEntry(string id, BalanceLabScenario scenario) => new()
        {
            Id = id,
            Damage = scenario.WeaponBaseDamage,
            Cooldown = scenario.WeaponBaseCooldown,
            Range = scenario.WeaponRange,
            ProjectileSpeed = scenario.WeaponProjectileSpeed,
            TargetMultiplier = scenario.WeaponPatternTargetMultiplier
        };

        public static IReadOnlyList<string> ResolveWeaponQueue(
            SaltwindLabBuildPolicy policy,
            SaltwindLabUnlockPool pool,
            StageBalanceProfileExport export)
        {
            var queue = new List<string>();
            switch (policy)
            {
                case SaltwindLabBuildPolicy.Precision:
                    queue.AddRange(PrecisionWeapons);
                    break;
                case SaltwindLabBuildPolicy.Broadside:
                    queue.AddRange(BroadsideWeapons);
                    break;
                case SaltwindLabBuildPolicy.AreaControl:
                    queue.AddRange(AreaWeapons);
                    break;
                default:
                    queue.Add("deck_cannons");
                    break;
            }

            if (pool == SaltwindLabUnlockPool.FreshSave && export?.StarterWeaponIds != null)
            {
                for (var i = queue.Count - 1; i >= 0; i--)
                {
                    if (!ContainsId(export.StarterWeaponIds, queue[i]))
                        queue.RemoveAt(i);
                }
            }

            if (queue.Count == 0) queue.Add("deck_cannons");
            return queue;
        }

        static bool ContainsId(IReadOnlyList<string> ids, string id)
        {
            for (var i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id) return true;
            }

            return false;
        }
    }

    public static class SaltwindBalanceLabBatchRunner
    {
        static readonly SaltwindLabBuildPolicy[] Policies =
        {
            SaltwindLabBuildPolicy.Precision,
            SaltwindLabBuildPolicy.Broadside,
            SaltwindLabBuildPolicy.AreaControl
        };

        static readonly BalanceSkillPreset[] Skills = { BalanceSkillPreset.Average, BalanceSkillPreset.Expert };
        static readonly SaltwindLabUnlockPool[] Pools =
        {
            SaltwindLabUnlockPool.FreshSave,
            SaltwindLabUnlockPool.MarksUnlocked
        };

        public static SaltwindBalanceLabMatrixReport RunMatrix(
            StageBalanceProfileExport export,
            SaltwindBalanceLabBatchOptions options = null)
        {
            if (export == null) throw new ArgumentNullException(nameof(export));
            options ??= new SaltwindBalanceLabBatchOptions();
            var report = new SaltwindBalanceLabMatrixReport
            {
                ProfileContentHash = export.ContentHash ?? string.Empty,
                BalanceVersion = export.BalanceVersion
            };

            for (var p = 0; p < Policies.Length; p++)
            {
                for (var s = 0; s < Skills.Length; s++)
                {
                    for (var u = 0; u < Pools.Length; u++)
                    {
                        var scenario = SaltwindBalanceLabScenarioFactory.Create(
                            export,
                            options.BaseSeed,
                            options.SeedsPerCell,
                            Policies[p],
                            Pools[u],
                            Skills[s]);
                        var summary = BalanceLabRunner.Run(scenario);
                        report.Cells.Add(new SaltwindBalanceLabCellSummary
                        {
                            BuildPolicy = Policies[p],
                            Skill = Skills[s],
                            UnlockPool = Pools[u],
                            Runs = summary.Runs,
                            Simulation = summary,
                            BehaviorLabel = scenario.BehaviorModelLabel
                        });
                        report.TotalRuns += summary.Runs;
                    }
                }
            }

            FlagSuspiciousDominance(report);
            return report;
        }

        static void FlagSuspiciousDominance(SaltwindBalanceLabMatrixReport report)
        {
            if (report.Cells.Count < 3) return;
            var best = 0f;
            var bestPolicy = SaltwindLabBuildPolicy.None;
            var worst = 1f;
            for (var i = 0; i < report.Cells.Count; i++)
            {
                var win = report.Cells[i].Simulation.WinRate;
                if (win > best)
                {
                    best = win;
                    bestPolicy = report.Cells[i].BuildPolicy;
                }

                if (win < worst) worst = win;
            }

            if (best - worst > 0.45f)
            {
                for (var i = 0; i < report.Cells.Count; i++)
                {
                    if (report.Cells[i].BuildPolicy == bestPolicy)
                        report.Cells[i].SuspiciousDominanceFlag =
                            "Review: policy leads all cells by >45pt boss win spread — verify live before tuning.";
                }
            }
        }

        public static void WriteReportFiles(SaltwindBalanceLabMatrixReport report, string directory)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "saltwind-matrix-summary.txt"), report.ToHumanSummary());
            WriteCsv(report, Path.Combine(directory, "saltwind-matrix.csv"));
            File.WriteAllText(Path.Combine(directory, "saltwind-matrix-meta.json"),
                "{\"profileHash\":\"" + (report.ProfileContentHash ?? string.Empty) + "\",\"balanceVersion\":" +
                report.BalanceVersion.ToString(CultureInfo.InvariantCulture) + ",\"totalRuns\":" +
                report.TotalRuns.ToString(CultureInfo.InvariantCulture) + "}");
        }

        static void WriteCsv(SaltwindBalanceLabMatrixReport report, string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("policy,skill,pool,runs,survival_rate,boss_win_rate,avg_kills,avg_level,avg_gold,peak_enemies_p50,boss_kill_p50");
            for (var i = 0; i < report.Cells.Count; i++)
            {
                var c = report.Cells[i];
                var s = c.Simulation;
                sb.Append(c.BuildPolicy).Append(',')
                    .Append(c.Skill).Append(',')
                    .Append(c.UnlockPool).Append(',')
                    .Append(s.Runs).Append(',')
                    .Append(s.SurvivalRate.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(s.WinRate.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(s.AverageKills.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(s.AverageLevel.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(s.AverageGold.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(s.AveragePeakEnemies.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(s.BossKillP50.ToString(CultureInfo.InvariantCulture)).AppendLine();
            }

            File.WriteAllText(path, sb.ToString());
        }
    }
}
