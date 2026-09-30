using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using RoguePirates.V4.Config;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Balance
{
    /// <summary>A check of one value against its target band.</summary>
    public struct TargetCheck
    {
        public string Name;
        public float Value;
        public TargetBand Band;
        public bool Met => Band.Contains(Value);
        public override string ToString() => $"{Name}: {Value:0.##} (target {Band}) {(Met ? "met" : "MISSED")}";
    }

    /// <summary>What one personality's runs looked like (medians and spreads).</summary>
    public sealed class PersonalitySummary
    {
        public PlayerPersonality Personality;
        public readonly List<SimRunResult> Runs = new List<SimRunResult>();
        /// <summary>Share of all hull lost, per enemy kind, most first.</summary>
        public readonly List<(string source, float share)> DamageSources = new List<(string, float)>();
        public float ItemsBy10, EvolutionMinuteP25;
        public float DeathP10, DeathMedian, DeathP90, BossWinRate, BossMinuteMedian, LevelAt5, LevelAt10, KillsPerMinute, Gold, Rum, EvolutionRate,
            EvolutionMinuteMedian, SunkShare;
        public readonly List<TargetCheck> Checks = new List<TargetCheck>();
    }

    /// <summary>One minute of the baseline personality's runs (medians) with the model's values and the targets.</summary>
    public sealed class ModelMinute
    {
        public int Minute, RunsReaching;
        public float AliveShare, Kills, Level, DamageTakenShare, Gold, SingleTargetDps, FodderHp, EliteHp, FodderTtk, EliteTtk, Pressure;
        public MinuteTargets Target;
        public readonly List<TargetCheck> Checks = new List<TargetCheck>();
    }

    /// <summary>A batch: every personality's runs, the baseline model and the pacing checks.</summary>
    public sealed class SimBatchResult
    {
        public readonly List<PersonalitySummary> Personalities = new List<PersonalitySummary>();
        public readonly List<ModelMinute> Model = new List<ModelMinute>();
        public readonly List<TargetCheck> Pacing = new List<TargetCheck>();
        public readonly List<(string weapon, int runs, float deathMedian)> WeaponPicks = new List<(string, int, float)>();
        public readonly List<(string a, string b, float deathMedian, float bossWin)> WeaponPairs = new List<(string, string, float, float)>();
        /// <summary>The baseline personality's runs per Captain: median end minute and offense at 10:00.</summary>
        public readonly List<(string captain, int runs, float endMedian, float offenseAt10, float bossWin)> Captains = new List<(string, int, float, float, float)>();
        public string ConfigHash, DirectorMode;
        public int RunsPerPersonality, Seed;
        public double Seconds;

        public PersonalitySummary For(string id) => Personalities.FirstOrDefault(p => p.Personality.personalityId == id);
        public IEnumerable<TargetCheck> AllChecks => Personalities.SelectMany(p => p.Checks).Concat(Model.SelectMany(m => m.Checks)).Concat(Pacing);
        public int Missed => AllChecks.Count(c => !c.Met);
    }

    /// <summary>
    /// V5 M2: runs the simulator for every personality (runs rotate through the Captains so builds vary), then builds the
    /// per-minute balance model from the baseline personality's runs and checks everything against
    /// <see cref="BalanceTargetsConfig"/>.
    /// </summary>
    public static class SimBatch
    {
        public static SimBatchResult Run(SimContent content, int runsPerPersonality, int seed, IEnumerable<PlayerPersonality> only = null, bool weaponPairs = false)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var result = new SimBatchResult { RunsPerPersonality = runsPerPersonality, Seed = seed, ConfigHash = content.ConfigHash, DirectorMode = content.Mode.ToString() };
            var personalities = (only ?? content.Personalities.personalities).ToList();
            var captains = content.Captains;
            foreach (var p in personalities)
            {
                var summary = new PersonalitySummary { Personality = p };
                for (var i = 0; i < runsPerPersonality; i++)
                {
                    var captain = captains.Count > 0 ? captains[i % captains.Count].captainKey : null;
                    summary.Runs.Add(new SimRun(content, p, RunSeed(seed, p.personalityId, i), captain).Play());
                }
                Summarise(summary, content.Targets);
                result.Personalities.Add(summary);
            }
            var baseline = result.For(content.Targets != null ? content.Targets.baselinePersonalityId : null) ?? result.Personalities.FirstOrDefault();
            if (baseline != null)
            {
                BuildModel(result, baseline, content);
                BuildPacing(result, baseline, content);
                BuildWeaponPicks(result, baseline);
                foreach (var g in baseline.Runs.GroupBy(r => r.CaptainKey).OrderBy(g => g.Key))
                    result.Captains.Add((g.Key, g.Count(), Median(g.Select(r => r.EndMinutes)),
                        MedianOf(g.Where(r => r.OffenseAt10 > 0f).Select(r => r.OffenseAt10)), g.Count(r => r.BossWon) / (float)g.Count()));
                if (weaponPairs) RunWeaponPairs(result, content, baseline.Personality, seed);
            }
            result.Seconds = watch.Elapsed.TotalSeconds;
            return result;
        }

        /// <summary>A run's seed from the batch seed, the personality and the index (stable when personalities are added).</summary>
        public static int RunSeed(int seed, string personalityId, int index)
        {
            unchecked
            {
                var h = seed * 486187739 + index * 16777619;
                foreach (var ch in personalityId ?? string.Empty) h = h * 31 + ch;
                return h & 0x7fffffff;
            }
        }

        public static float Percentile(IList<float> values, float p)
        {
            if (values == null || values.Count == 0) return float.NaN;
            var sorted = values.OrderBy(v => v).ToList();
            var x = Mathf.Clamp01(p) * (sorted.Count - 1);
            var lo = Mathf.FloorToInt(x);
            var hi = Mathf.Min(sorted.Count - 1, lo + 1);
            return Mathf.Lerp(sorted[lo], sorted[hi], x - lo);
        }

        static float Median(IEnumerable<float> values) => Percentile(values.ToList(), 0.5f);

        static void Summarise(PersonalitySummary s, BalanceTargetsConfig targets)
        {
            var ends = s.Runs.Select(r => r.EndMinutes).ToList();
            s.DeathP10 = Percentile(ends, 0.1f);
            s.DeathMedian = Percentile(ends, 0.5f);
            s.DeathP90 = Percentile(ends, 0.9f);
            s.SunkShare = s.Runs.Count(r => r.Died) / (float)Mathf.Max(1, s.Runs.Count);
            s.BossWinRate = s.Runs.Count(r => r.BossWon) / (float)Mathf.Max(1, s.Runs.Count);
            s.BossMinuteMedian = s.Runs.Any(r => r.BossWon) ? Median(s.Runs.Where(r => r.BossWon).Select(r => r.BossKillSeconds / 60f)) : float.NaN;
            s.LevelAt5 = MedianOf(s.Runs.Where(r => r.LevelAt5 >= 0).Select(r => (float)r.LevelAt5));
            s.LevelAt10 = MedianOf(s.Runs.Where(r => r.LevelAt10 >= 0).Select(r => (float)r.LevelAt10));
            s.KillsPerMinute = Median(s.Runs.Select(r => r.Kills / Mathf.Max(0.25f, r.EndMinutes)));
            s.Gold = Median(s.Runs.Select(r => (float)r.GoldEarned));
            s.Rum = Median(s.Runs.Select(r => (float)r.Rum));
            s.EvolutionRate = s.Runs.Count(r => r.Evolutions.Count > 0) / (float)Mathf.Max(1, s.Runs.Count);
            s.ItemsBy10 = Median(s.Runs.Where(r => r.EndSeconds >= 600f).Select(r => (float)r.ItemsBy10).DefaultIfEmpty(float.NaN));
            s.EvolutionMinuteP25 = s.Runs.Any(r => r.Evolutions.Count > 0) ? Percentile(s.Runs.Where(r => r.Evolutions.Count > 0).Select(r => r.Evolutions[0].time / 60f).ToList(), 0.25f) : float.NaN;
            s.EvolutionMinuteMedian = s.Runs.Any(r => r.Evolutions.Count > 0) ? Median(s.Runs.Where(r => r.Evolutions.Count > 0).Select(r => r.Evolutions[0].time / 60f)) : float.NaN;
            var bySource = new Dictionary<string, float>();
            foreach (var r in s.Runs) foreach (var kv in r.DamageBySource) bySource[kv.Key] = (bySource.TryGetValue(kv.Key, out var v) ? v : 0f) + kv.Value;
            var total = bySource.Values.Sum();
            if (total > 0f) s.DamageSources.AddRange(bySource.OrderByDescending(kv => kv.Value).Select(kv => (kv.Key, kv.Value / total)));
            var t = targets?.For(s.Personality.personalityId);
            if (t == null) return;
            s.Checks.Add(new TargetCheck { Name = $"{s.Personality.displayName}: median end minute", Value = s.DeathMedian, Band = t.deathMinute });
            s.Checks.Add(new TargetCheck { Name = $"{s.Personality.displayName}: boss win rate", Value = s.BossWinRate, Band = t.bossWinRate });
            if (!float.IsNaN(t.levelAt10.min) || !float.IsNaN(t.levelAt10.max))
                s.Checks.Add(new TargetCheck { Name = $"{s.Personality.displayName}: level at 10:00", Value = float.IsNaN(s.LevelAt10) ? 0f : s.LevelAt10, Band = t.levelAt10 });
        }

        static float MedianOf(IEnumerable<float> values)
        {
            var list = values.ToList();
            return list.Count > 0 ? Percentile(list, 0.5f) : float.NaN;
        }

        static void BuildModel(SimBatchResult result, PersonalitySummary baseline, SimContent c)
        {
            var cap = c.Timeline != null && c.Timeline.maxActiveEnemies > 0 ? c.Timeline.maxActiveEnemies : 80;
            var last = c.Targets != null && c.Targets.minutes.Count > 0 ? c.Targets.minutes.Max(m => m.minute) : 14;
            var fodder = FodderDef(c);
            var elite = EliteDef(c, out var eliteHpMult);
            var efficiency = c.Targets != null ? Mathf.Clamp01(c.Targets.calibration.hitEfficiency) : 1f;
            for (var m = 0; m <= last; m++)
            {
                var runs = baseline.Runs.Where(r => r.Minutes.Count > m).ToList();
                var row = new ModelMinute { Minute = m, RunsReaching = runs.Count };
                if (runs.Count > 0)
                {
                    row.AliveShare = Median(runs.Select(r => r.Minutes[m].AliveAverage)) / cap;
                    row.Kills = Median(runs.Select(r => (float)r.Minutes[m].Kills));
                    row.Level = Median(runs.Select(r => (float)r.Minutes[m].Level));
                    row.DamageTakenShare = Median(runs.Select(r => r.Minutes[m].DamageTaken / 100f));
                    row.Gold = Median(runs.Select(r => (float)r.Minutes[m].Gold));
                    row.SingleTargetDps = Median(runs.Select(r => r.Minutes[m].SingleTargetDps));
                    row.Pressure = Median(runs.Select(r => r.Minutes[m].Pressure));
                }
                var t = (m + 0.5f) * 60f;
                row.FodderHp = fodder != null ? SpawnHp(c, fodder.BaseHp, t) : 0f;
                row.EliteHp = elite != null ? SpawnHp(c, elite.BaseHp, t) * eliteHpMult : 0f;
                // Time to kill one enemy with every weapon on it: its HP ÷ (single-target DPS × the calibrated hit efficiency).
                var landed = row.SingleTargetDps * efficiency;
                row.FodderTtk = landed > 0f ? row.FodderHp / landed : float.PositiveInfinity;
                row.EliteTtk = landed > 0f ? row.EliteHp / landed : float.PositiveInfinity;
                row.Target = c.Targets?.minutes.FirstOrDefault(x => x.minute == m);
                if (row.Target != null && runs.Count >= Mathf.Max(3, baseline.Runs.Count / 4))
                {
                    void Add(string name, float value, TargetBand band)
                    {
                        if (float.IsNaN(band.min) && float.IsNaN(band.max)) return;
                        row.Checks.Add(new TargetCheck { Name = $"minute {m}: {name}", Value = value, Band = band });
                    }
                    Add("alive share", row.AliveShare, row.Target.aliveShare);
                    Add("kills", row.Kills, row.Target.killsPerMinute);
                    Add("level", row.Level, row.Target.level);
                    Add("damage taken (share of 100 hull)", row.DamageTakenShare, row.Target.damageTakenShare);
                    Add("gold", row.Gold, row.Target.goldPerMinute);
                }
                result.Model.Add(row);
            }
        }

        /// <summary>HP an ambient enemy spawns with at this time (curve × ordinary time; stage 0, tier 1; no levers).</summary>
        public static float SpawnHp(SimContent c, float baseHp, float seconds)
        {
            var hp = RoguePirates.V4.Difficulty.CurveEvaluator.EnemyHp(c.Curve, baseHp, seconds / 60f, 1, 0);
            var d = c.Curve;
            if (d != null && seconds >= d.ordinaryScalingDelaySeconds) hp *= d.ordinaryHp.Evaluate((seconds - d.ordinaryScalingDelaySeconds) / 60f);
            return hp * (c.Experiment != null ? Mathf.Max(0.1f, c.Experiment.enemyHpMultiplier) : 1f);
        }

        /// <summary>The fodder the model measures: the weakest enemy of the first band's ordinary pool.</summary>
        static SimEnemyDef FodderDef(SimContent c)
        {
            var band = c.Timeline?.phases.Where(p => p.ratePerSecond > 0f && p.pools.Count > 0).OrderBy(p => p.trigger.timeSeconds).FirstOrDefault();
            return band?.pools.SelectMany(p => p.entries).Select(c.Enemy).Where(d => d != null).OrderBy(d => d.BaseHp).FirstOrDefault();
        }

        /// <summary>
        /// The elite the model measures: the first scheduled elite beat's enemy (its variant when it has one) with the HP
        /// the game gives it — the focus multiplier, the spawner's elite multiplier and the Rich affix of an elite without one.
        /// </summary>
        static SimEnemyDef EliteDef(SimContent c, out float hpMult)
        {
            hpMult = 1f;
            var phase = c.Timeline?.phases.Where(p => p.focus != null && p.focus.enabled && p.focus.scheduledEliteIndex >= 0 && p.spawner != null)
                .OrderBy(p => p.trigger.timeSeconds).FirstOrDefault();
            if (phase == null) return null;
            var e = phase.spawner.elite;
            hpMult = phase.focus.hpMultiplier * (e.isElite ? e.hpMultiplier * SimRun.AffixHp(e.affix == ConfigEnums.EliteAffix.None ? ConfigEnums.EliteAffix.Rich : e.affix) : 1f);
            var id = phase.focus.variant != null ? phase.focus.variant.variantId : phase.spawner.anchor != null ? phase.spawner.anchor.enemyId : null;
            return c.Enemy(id);
        }

        static void BuildPacing(SimBatchResult result, PersonalitySummary baseline, SimContent c)
        {
            var targets = c.Targets;
            if (targets == null) return;
            var first = baseline.Runs.Select(r => r.LevelUpTimes.Where(t => t < 60f).ToList()).Where(l => l.Count > 0).Select(l => 60f / l.Count).ToList();
            var to10 = baseline.Runs.Where(r => r.EndSeconds >= 600f).Select(r => 600f / Mathf.Max(1, r.LevelUpTimes.Count(t => t < 600f))).ToList();
            if (first.Count > 0) result.Pacing.Add(new TargetCheck { Name = "seconds between level-ups in minute 1", Value = Percentile(first, 0.5f), Band = targets.levelUpGapMinute1 });
            if (to10.Count > 0) result.Pacing.Add(new TargetCheck { Name = "seconds between level-ups over 0:00–10:00", Value = Percentile(to10, 0.5f), Band = targets.levelUpGapTo10 });
            var fodder = baseline.Runs.Where(r => r.FodderTtkCount > 0).Select(r => r.FodderTtkSum / r.FodderTtkCount).ToList();
            var elite = baseline.Runs.Where(r => r.EliteTtkCount > 0).Select(r => r.EliteTtkSum / r.EliteTtkCount).ToList();
            var miniboss = baseline.Runs.Where(r => r.MinibossTtkCount > 0).Select(r => r.MinibossTtkSum / r.MinibossTtkCount).ToList();
            if (miniboss.Count > 0) result.Pacing.Add(new TargetCheck { Name = "miniboss time to kill (s)", Value = Percentile(miniboss, 0.5f), Band = targets.minibossTtkSeconds });
            var boss = baseline.Runs.Where(r => r.BossWon).Select(r => r.BossKillSeconds - r.BossSummonSeconds).ToList();
            if (fodder.Count > 0) result.Pacing.Add(new TargetCheck { Name = "fodder time to kill (s, first hit to sunk)", Value = Percentile(fodder, 0.5f), Band = targets.fodderTtkSeconds });
            if (elite.Count > 0) result.Pacing.Add(new TargetCheck { Name = "scheduled elite time to kill (s)", Value = Percentile(elite, 0.5f), Band = targets.eliteTtkSeconds });
            if (boss.Count > 0) result.Pacing.Add(new TargetCheck { Name = "Barnacle King fight (s, summon to sunk)", Value = Percentile(boss, 0.5f), Band = targets.bossTtkSeconds });
        }

        static void BuildWeaponPicks(SimBatchResult result, PersonalitySummary baseline)
        {
            var weapons = baseline.Runs.SelectMany(r => r.Weapons).Distinct().OrderBy(w => w);
            foreach (var w in weapons)
            {
                var with = baseline.Runs.Where(r => r.Weapons.Contains(w)).Select(r => r.EndMinutes).ToList();
                result.WeaponPicks.Add((w, with.Count, Percentile(with, 0.5f)));
            }
            result.WeaponPicks.Sort((a, b) => b.deathMedian.CompareTo(a.deathMedian));
        }

        /// <summary>Every weapon pair as the first two weapons (the baseline personality, a few runs each; M3 extends this).</summary>
        static void RunWeaponPairs(SimBatchResult result, SimContent c, PlayerPersonality p, int seed)
        {
            var weapons = c.WeaponsInOrder.Where(w => c.IsWeaponAvailable(w.weaponId)).Select(w => w.weaponId).ToList();
            const int runs = 6;
            for (var i = 0; i < weapons.Count; i++)
            for (var j = i + 1; j < weapons.Count; j++)
            {
                var ends = new List<float>();
                var wins = 0;
                for (var n = 0; n < runs; n++)
                {
                    var run = new SimRun(c, p, RunSeed(seed, weapons[i] + "+" + weapons[j], n), c.Captains.Count > 0 ? c.Captains[n % c.Captains.Count].captainKey : null);
                    run.Build.Progression.DevSetWeapons(new[] { weapons[i], weapons[j] });
                    run.Build.SyncSlots();
                    var r = run.Play();
                    ends.Add(r.EndMinutes);
                    if (r.BossWon) wins++;
                }
                result.WeaponPairs.Add((weapons[i], weapons[j], Percentile(ends, 0.5f), wins / (float)runs));
            }
            result.WeaponPairs.Sort((a, b) => b.deathMedian.CompareTo(a.deathMedian));
        }

        /// <summary>
        /// The director's on-track offense growth measured from a personality's runs (Observe: the director watches, the
        /// levers stay still): the median offense at each minute's end ÷ the median starting offense.
        /// </summary>
        public static List<(float minute, float growth)> OnTrackGrowth(SimContent content, PlayerPersonality p, int runs, int seed, int minutes = 15)
        {
            var mode = content.DirectorModeOverride;
            content.DirectorModeOverride = DirectorMode.Observe;
            try
            {
                var results = new List<SimRunResult>();
                for (var i = 0; i < runs; i++)
                    results.Add(new SimRun(content, p, RunSeed(seed, p.personalityId + ":growth", i), content.Captains.Count > 0 ? content.Captains[i % content.Captains.Count].captainKey : null).Play());
                var start = Percentile(results.Select(r => r.Offense0).Where(o => o > 0f).ToList(), 0.5f);
                var curve = new List<(float, float)> { (0f, 1f) };
                for (var m = 0; m < minutes; m++)
                {
                    var at = results.Where(r => r.Minutes.Count > m && r.Minutes[m].Offense > 0f).Select(r => r.Minutes[m].Offense).ToList();
                    if (at.Count < Math.Max(3, runs / 5)) break;
                    curve.Add((m + 1, Percentile(at, 0.5f) / Mathf.Max(0.0001f, start)));
                }
                return curve;
            }
            finally { content.DirectorModeOverride = mode; }
        }

        // ---- Report -----------------------------------------------------------------------------------------------------

        static string F(float v, string format = "0.#") => float.IsNaN(v) ? "–" : float.IsInfinity(v) ? "∞" : v.ToString(format, CultureInfo.InvariantCulture);

        /// <summary>The Markdown report (plan/v5/phase-02-model/model-report.md).</summary>
        public static string Report(SimBatchResult r, SimContent c, string title = "V5 balance model and simulator report")
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# {title}");
            sb.AppendLine();
            sb.AppendLine($"Config hash `{r.ConfigHash.Substring(0, Math.Min(12, r.ConfigHash.Length))}` · director {r.DirectorMode} · {r.RunsPerPersonality} runs per personality · seed {r.Seed} · {r.Seconds:0.0} s.");
            sb.AppendLine($"Targets missed: **{r.Missed}** of {r.AllChecks.Count()}.");
            sb.AppendLine();
            sb.AppendLine("## Personalities");
            sb.AppendLine();
            sb.AppendLine("| Personality | Share | End minute p10 / median / p90 | Sunk | Boss won | Boss sunk at | Level 5:00 / 10:00 | Kills/min | Gold | Rum | Evolved (minute) | Targets |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var p in r.Personalities)
            {
                var checks = p.Checks.Count == 0 ? "–" : string.Join("; ", p.Checks.Select(x => (x.Met ? "✓ " : "✗ ") + x.Name.Substring(x.Name.IndexOf(':') + 2) + " " + F(x.Value, "0.##") + " (" + x.Band + ")"));
                sb.AppendLine($"| {p.Personality.displayName} | {p.Personality.share:P0} | {F(p.DeathP10)} / **{F(p.DeathMedian)}** / {F(p.DeathP90)} | {p.SunkShare:P0} | {p.BossWinRate:P0} | {F(p.BossMinuteMedian)} | {F(p.LevelAt5, "0")} / {F(p.LevelAt10, "0")} | {F(p.KillsPerMinute, "0")} | {F(p.Gold, "0")} | {F(p.Rum, "0")} | {p.EvolutionRate:P0} ({F(p.EvolutionMinuteMedian)}) | {checks} |");
            }
            var weighted = r.Personalities.Sum(p => p.Personality.share * p.DeathMedian) / Mathf.Max(0.0001f, r.Personalities.Sum(p => p.Personality.share));
            sb.AppendLine();
            sb.AppendLine($"Most players (weighted by share): median end minute ≈ **{F(weighted)}**.");
            sb.AppendLine();
            sb.AppendLine("Items by 10:00 (runs that reached it) and evolutions: " + string.Join("; ", r.Personalities.Select(p => $"{p.Personality.displayName} {F(p.ItemsBy10)} items, evolved {p.EvolutionRate:P0} (first at {F(p.EvolutionMinuteP25)}–{F(p.EvolutionMinuteMedian)} min, p25–median)")) + ".");
            if (r.Captains.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Captains (baseline personality)");
                sb.AppendLine();
                sb.AppendLine("| Captain | Runs | Median end minute | Offense at 10:00 | Boss won |");
                sb.AppendLine("|---|---|---|---|---|");
                foreach (var c2 in r.Captains) sb.AppendLine($"| {c2.captain} | {c2.runs} | {F(c2.endMedian)} | {F(c2.offenseAt10, "0")} | {c2.bossWin:P0} |");
            }
            sb.AppendLine();
            sb.AppendLine("## Baseline per minute (model)");
            sb.AppendLine();
            sb.AppendLine("Medians of the baseline personality's runs that reached the minute. Model TTK = the fodder's / first scheduled elite's HP at the minute ÷ (the build's single-target DPS × the calibrated hit efficiency); it depends directly on that efficiency (fitted on crowd kills), so the measured fight lengths under Pacing are the ones checked.");
            sb.AppendLine();
            sb.AppendLine("| Min | Runs | Alive (share of cap) | Kills | Level | Damage taken (per 100 hull) | Gold | DPS (1 target) | Fodder HP / TTK | Elite HP / TTK | Pressure | Checks |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var m in r.Model)
            {
                var checks = m.Checks.Count == 0 ? "–" : string.Join("; ", m.Checks.Select(x => (x.Met ? "✓ " : "✗ ") + x.Name.Substring(x.Name.IndexOf(':') + 2) + " (" + x.Band + ")"));
                sb.AppendLine($"| {m.Minute} | {m.RunsReaching} | {F(m.AliveShare, "0.00")} | {F(m.Kills, "0")} | {F(m.Level, "0")} | {F(m.DamageTakenShare, "0.00")} | {F(m.Gold, "0")} | {F(m.SingleTargetDps, "0")} | {F(m.FodderHp, "0")} / {F(m.FodderTtk, "0.00")} s | {F(m.EliteHp, "0")} / {F(m.EliteTtk, "0.0")} s | {F(m.Pressure, "0.00")} | {checks} |");
            }
            sb.AppendLine();
            sb.AppendLine("## Pacing (baseline)");
            sb.AppendLine();
            foreach (var p in r.Pacing) sb.AppendLine($"- {(p.Met ? "✓" : "✗")} {p.Name}: **{F(p.Value, "0.##")}** (target {p.Band})");
            sb.AppendLine();
            sb.AppendLine("## Weapons in the baseline runs");
            sb.AppendLine();
            sb.AppendLine("| Weapon | Runs carrying it | Median end minute |");
            sb.AppendLine("|---|---|---|");
            foreach (var w in r.WeaponPicks) sb.AppendLine($"| {w.weapon} | {w.runs} | {F(w.deathMedian)} |");
            if (r.WeaponPairs.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Weapon pairs (the first two weapons, baseline personality, 6 runs each)");
                sb.AppendLine();
                sb.AppendLine("| Pair | Median end minute | Boss won |");
                sb.AppendLine("|---|---|---|");
                foreach (var w in r.WeaponPairs) sb.AppendLine($"| {w.a} + {w.b} | {F(w.deathMedian)} | {w.bossWin:P0} |");
            }
            return sb.ToString();
        }
    }
}
