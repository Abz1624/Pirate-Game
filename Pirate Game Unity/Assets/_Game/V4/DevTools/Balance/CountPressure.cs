using System;
using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Config;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Balance
{
    /// <summary>
    /// V5 M3d (owner, 1 Oct 2026, notes e, h, k and the sketch): pressure from enemy count, never from time-scaled HP or
    /// damage; a peak-and-valley crowd rising to the 10:00 climax; an enemy that looks the same behaves the same. The
    /// values live here (with their values before M3d) so the Editor command, the simulator's tuning and the tests share
    /// them. <see cref="Apply"/> writes them to any set of configs (runtime copies or assets).
    /// </summary>
    public static class CountPressure
    {
        /// <summary>The tuned numbers (the simulator searches these).</summary>
        public struct Tuning
        {
            /// <summary>× every keyframe's floor share (the crowd's height) and rate multiplier: one scale for the whole curve,
            /// so the sketch's order of peaks and valleys is kept (review M3d #1).</summary>
            public float FloorScale, RateScale;
            /// <summary>Fixed HP: the Barnacle King's base HP, the scheduled elites' HP multiplier, the Reef Crab's.</summary>
            public float BossBaseHp, EliteHammerheadHp, EliteKrakenHp, ReefCrabHp;
            /// <summary>× every horde's count.</summary>
            public float HordeScale;
            /// <summary>h: the stronger types that enter late (manta ray, boarding barge, kraken spawn, shield frigate, powder
            /// barge): × their base HP and damage (fixed per type, whenever they appear).</summary>
            public float LateHp, LateDamage;
            /// <summary>The specialist pool's share in the last two bands (the rest ordinary).</summary>
            public float Band3Specialists, Band4Specialists;
            /// <summary>Overtime (after 10:00, the endless ramp, D8): its HP base and the stat ramp it starts and ends at.</summary>
            public float OvertimeHpBase, OvertimeRampStart, OvertimeRampEnd;

            /// <summary>The tuning chosen in the simulator (1 Oct 2026; `plan/v5/phase-03d-pressure/tuning.md`).</summary>
            public static Tuning Default => new Tuning
            {
                FloorScale = 1f, RateScale = 1f, BossBaseHp = 2800f, EliteHammerheadHp = 5.5f, EliteKrakenHp = 12f, ReefCrabHp = 4.5f, HordeScale = 1f,
                LateHp = 2f, LateDamage = 2f, Band3Specialists = 0.55f, Band4Specialists = 0.65f, OvertimeHpBase = 3f, OvertimeRampStart = 2f, OvertimeRampEnd = 3.5f,
            };
        }

        /// <summary>Plain edits (config id, path, value, value before M3d).</summary>
        public static readonly (string configId, string path, string value, string pre)[] Edits =
        {
            // h: no HP or damage growth with time in the voyage (overtime keeps its own ramp).
            ("difficulty.saltwind", "enemyHpPerMinute.c1", "0", "0.0495"),
            ("difficulty.saltwind", "enemyHpPerMinute.c2", "0", "0.0099"),
            ("difficulty.saltwind", "ordinaryHp.c1", "0", "0.044"),
            ("difficulty.saltwind", "ordinaryHp.c2", "0", "0.0055"),
            ("difficulty.saltwind", "enemyDamagePerMinute.c1", "0", "0.0175"),
            ("difficulty.saltwind", "ordinaryDamage.c1", "0", "0.042"),
            ("difficulty.saltwind", "ordinaryDamage.c2", "0", "0.007"),
            // h: the director keeps its count levers and elite chance; no HP or damage levers.
            ("director.saltwind", "toughnessLever", "false", "true"),
            ("director.saltwind", "capHpLever", "false", "true"),
            ("director.saltwind", "focusLever", "false", "true"),
            // M3c's interim flat scale goes (fixed per-type stats replace it).
            ("experiment", "enemyHpMultiplier", "1", "0.85"),
            ("experiment", "enemyDamageMultiplier", "1", "0.85"),
            // e: keyframed waves; k: no per-band attack overrides.
            ("timeline.saltwind", "flow.smoothWaves", "true", "false"),
            ("timeline.saltwind", "spawnerCombatOverrides", "false", "true"),
            // k: every id that attacks differently looks different (tint r,g,b,a and view size; models set by the command).
            ("enemy.cutthroat_skiff", "visual.tint", "1,0.62,0.55,1", "1,1,1,1"),
            ("enemy.boarding_barge", "visual.tint", "0.6,0.75,1,1", "1,1,1,1"),
            ("enemy.boarding_barge", "visual.viewSize", "1.15", "1"),
            ("variant.needle_sloop", "look.tint", "1,1,0.35,1", "1,1,1,1"),
            ("variant.needle_sloop", "look.viewSize", "0.9", "1"),
            ("variant.ram_raider", "look.tint", "1,0.45,0.45,1", "1,1,1,1"),
            ("variant.ram_raider", "look.viewSize", "1.05", "1"),
            ("variant.repair_tender", "look.tint", "0.55,1,0.55,1", "1,1,1,1"),
            ("variant.powder_barge", "look.tint", "1,0.6,0.3,1", "1,1,1,1"),
            ("variant.powder_barge", "look.viewSize", "1.25", "1"),
            ("variant.bomb_ketch", "look.tint", "0.8,0.55,1,1", "1,1,1,1"),
            ("variant.bomb_ketch", "look.viewSize", "1.2", "1"),
            ("variant.shield_frigate", "look.tint", "0.65,0.7,0.85,1", "1,1,1,1"),
            ("variant.shield_frigate", "look.viewSize", "1.15", "1"), // × the gunner cog's 1.15: drawn 1.32×
            ("variant.net_cutter", "look.tint", "1,0.7,0.4,1", "1,1,1,1"),
            ("enemy.gunner_cog", "visual.tint", "0.85,0.35,0.35,1", "1,1,1,1"), // the cannon boat's hull, crimson and larger: three shots
            ("enemy.gunner_cog", "visual.viewSize", "1.15", "1"),
            ("enemy.drowned_harrier", "visual.tint", "0.7,1,0.8,1", "1,1,1,1"),
            ("enemy.drowned_pursuer", "visual.tint", "0.7,1,0.8,1", "1,1,1,1"),
        };

        /// <summary>Models (config id, path, asset path of the model, before). None since the M3d review: the Brigg is heavy
        /// (48 particle systems) and is Captain Hank's boat, so the gunner cog keeps its cog model under its own tint and
        /// size; the mechanism (a variant's <c>look.model</c>) stays for new art.</summary>
        public static readonly (string configId, string path, string model, string pre)[] Models = Array.Empty<(string, string, string, string)>();

        /// <summary>
        /// e: the crowd's keyframes (start s, floor share of the ambient cap, rate ×, top-up interval s, top-ups per tick),
        /// read from the owner's sketch: small close peaks early (~0:25, ~1:20, ~2:20), a bigger one at 3:35-3:45 (the horde
        /// lands at 3:35 after its lull, the floor peaks at 3:45 before the horde's release window), a long valley under it
        /// rising gently with ~60 s waves, two big peaks (~7:30, ~9:35) towards the 10:00 climax. Final values: one tuning
        /// scale for all, so the order holds (review M3d #1).
        /// </summary>
        public static readonly (float t, float share, float rate, float interval, int perTick)[] Keyframes =
        {
            (0f, 0.10f, 1.0f, 1.0f, 2), (25f, 0.30f, 1.15f, 0.8f, 3), (45f, 0.14f, 0.85f, 1.0f, 2),
            (80f, 0.34f, 1.2f, 0.8f, 3), (100f, 0.18f, 0.9f, 1.0f, 2), (140f, 0.38f, 1.2f, 0.75f, 3),
            (185f, 0.26f, 0.95f, 1.0f, 3), (205f, 0.40f, 1.1f, 0.8f, 4), (225f, 0.66f, 1.35f, 0.6f, 5), (250f, 0.30f, 0.65f, 1.0f, 3),
            (275f, 0.40f, 0.75f, 0.8f, 3), (300f, 0.34f, 0.65f, 1.0f, 3), (335f, 0.46f, 0.8f, 0.75f, 4),
            (360f, 0.38f, 0.7f, 1.0f, 3), (395f, 0.48f, 0.8f, 0.75f, 4), (420f, 0.54f, 1.0f, 0.6f, 5),
            (450f, 0.80f, 1.35f, 0.5f, 6), (480f, 0.46f, 0.9f, 1.0f, 3), (510f, 0.64f, 1.1f, 0.6f, 5),
            (545f, 0.82f, 1.3f, 0.5f, 6), (575f, 0.92f, 1.45f, 0.4f, 7), (600f, 0.95f, 1.45f, 0.4f, 7),
        };

        /// <summary>The hordes on the peaks (time s, count); an early one makes the first minute busy (owner: dull until 3-4 min).</summary>
        public static readonly (float t, int count)[] Hordes = { (35f, 10), (215f, 16), (440f, 20), (560f, 28) };

        static readonly Dictionary<string, Func<Tuning, string>> TuningPaths = new Dictionary<string, Func<Tuning, string>>
        {
            ["enemy.barnacle_king|stats.baseHP"] = t => F(t.BossBaseHp),
            ["spawner.saltwind_elite_hammerhead|elite.hpMultiplier"] = t => F(t.EliteHammerheadHp),
            ["spawner.saltwind_elite_kraken|elite.hpMultiplier"] = t => F(t.EliteKrakenHp),
            ["difficulty.saltwind|overtimeHpMultiplierBase"] = t => F(t.OvertimeHpBase),
            ["timeline.saltwind|overtime.spawnStatRampStart"] = t => F(t.OvertimeRampStart),
            ["timeline.saltwind|overtime.spawnStatRampEnd"] = t => F(t.OvertimeRampEnd),
        };

        /// <summary>
        /// The per-minute targets for the new shape (path, value, value before M3d), derived from the design, not from what
        /// the simulator measured (owner, 1 Oct 2026: targets hold a value in a tight band). Alive share in minutes 1-10: at
        /// least × 0.75 the design's floor over the minute (<see cref="MeanFloorShare"/>: the crowd reaches the floor the
        /// sketch asks for); the ceilings stay M2's (the band rate's spawns ride above the floor, so the floor gives no
        /// ceiling). Kills keep their M2 bands. Damage taken: at least 0.05 of 100
        /// hull in each minute from 1:00 to 4:00 (owner note e: not on autopilot; the first minute is the ramp-in), and at
        /// most 0.6 in any minute from 5:00 to 10:00 (owner: too hard after 6 min; no minute sinks an average build).
        /// </summary>
        public static readonly (string path, string value, string pre)[] TargetEdits = BuildTargetEdits();

        /// <summary>The hordes' fixed windows the floor drops in (the timeline's release settings, which M3d keeps): the lull
        /// before each horde, and its release window that opens a delay after it, both at the release's floor multiplier.</summary>
        public const float HordeLullSeconds = 10f, HordeReleaseDelaySeconds = 20f, HordeReleaseSeconds = 15f, ReleaseFloorMultiplier = 0.4f;

        /// <summary>The design's floor share (tuning scale 1) averaged over [from, to) seconds: the line between keyframes,
        /// lowered in the hordes' lulls and release windows (fixed times); arenas and the boss fight depend on the player
        /// and are not in it.</summary>
        public static float MeanFloorShare(float from, float to, float floorScale = 1f)
        {
            var sum = 0f;
            var n = 0;
            for (var t = from; t < to; t += 1f, n++)
            {
                var i = 0;
                while (i + 1 < Keyframes.Length && Keyframes[i + 1].t <= t) i++;
                var a = Keyframes[i];
                var b = i + 1 < Keyframes.Length ? Keyframes[i + 1] : a;
                var k = b.t > a.t ? Mathf.Clamp01((t - a.t) / (b.t - a.t)) : 0f;
                var share = Mathf.Clamp01(Mathf.Lerp(a.share, b.share, k) * floorScale);
                foreach (var h in Hordes)
                    if ((t >= h.t - HordeLullSeconds && t < h.t) || (t >= h.t + HordeReleaseDelaySeconds && t < h.t + HordeReleaseDelaySeconds + HordeReleaseSeconds))
                        share *= ReleaseFloorMultiplier;
                sum += share;
            }
            return n > 0 ? sum / n : 0f;
        }

        static (string, string, string)[] BuildTargetEdits()
        {
            var preAlive = new[] { (0.03f, 0.30f), (0.10f, 0.45f), (0.15f, 0.55f), (0.12f, 0.55f), (0.20f, 0.65f), (0.18f, 0.70f), (0.25f, 0.75f), (0.30f, 0.85f), (0.22f, 0.80f), (0.35f, 0.90f) };
            // (the ceilings, Item2, are kept as they are)
            string F2(float v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            var list = new List<(string, string, string)>();
            for (var m = 0; m < preAlive.Length; m++)
            {
                var mean = MeanFloorShare(m * 60f, m * 60f + 60f, Tuning.Default.FloorScale);
                list.Add(($"minutes[{m}].aliveShare.min", F2(mean * 0.75f), F2(preAlive[m].Item1)));
            }
            for (var m = 1; m <= 3; m++) list.Add(($"minutes[{m}].damageTakenShare.min", "0.05", "NaN"));
            var preMax = new[] { 0.8f, 0.9f, 1f, 1f, 1.1f };
            for (var m = 5; m <= 9; m++) list.Add(($"minutes[{m}].damageTakenShare.max", "0.6", F2(preMax[m - 5])));
            return list.ToArray();
        }

        /// <summary>The late types' base HP and damage before M3d (archetypes; the variants' own HP; their damage is the base's).</summary>
        public static readonly (string configId, float hp, float damage)[] LateTypes =
        {
            ("enemy.manta_ray", 22f, 8f), ("enemy.boarding_barge", 24f, 8f), ("enemy.kraken_spawn", 28f, 11f),
            ("variant.shield_frigate", 45f, 9f), ("variant.powder_barge", 40f, 8f),
        };

        /// <summary>The values the tuned paths held before M3d.</summary>
        public static readonly (string configId, string path, string pre)[] TunedBefore =
        {
            ("enemy.barnacle_king", "stats.baseHP", "1400"),
            ("spawner.saltwind_elite_hammerhead", "elite.hpMultiplier", "6"),
            ("spawner.saltwind_elite_kraken", "elite.hpMultiplier", "6"),
            ("difficulty.saltwind", "overtimeHpMultiplierBase", "1.25"),
            ("timeline.saltwind", "overtime.spawnStatRampStart", "1"),
            ("timeline.saltwind", "overtime.spawnStatRampEnd", "1.5"),
        };

        /// <summary>The flow before M3d (one step per minute; start s, floor share, rate ×, top-up interval s, per tick) and its hordes (time s, count).</summary>
        public static readonly (float t, float share, float rate, float interval, int perTick)[] PreWaves =
        {
            (0f, 0.08f, 1f, 1f, 2), (60f, 0.2f, 1f, 1f, 3), (120f, 0.32f, 1f, 0.75f, 4), (180f, 0.22f, 0.9f, 1f, 3), (240f, 0.38f, 1f, 0.75f, 4),
            (300f, 0.26f, 0.95f, 1f, 3), (360f, 0.45f, 1f, 0.6f, 5), (420f, 0.55f, 1.05f, 0.5f, 5), (480f, 0.4f, 0.9f, 0.75f, 4), (540f, 0.62f, 1.1f, 0.5f, 6),
        };
        public static readonly (float t, int count, float speed, float hp)[] PreHordes = { (105f, 14, 1.25f, 0.8f), (330f, 14, 1.2f, 0.8f), (460f, 18, 1.35f, 0.7f), (555f, 24, 1.3f, 0.7f) };
        /// <summary>The specialist pool's share in bands 3 and 4, and the Reef Crab's HP ×, before M3d.</summary>
        public const float PreBand3Specialists = 0.35f, PreBand4Specialists = 0.4f, PreReefCrabHp = 3.8f;

        /// <summary>
        /// Puts every value M3d wrote back to its value before M3d (tests that measure earlier phases, e.g. V5 M2's calibration).
        /// The models are restored when <paramref name="loadModel"/> is given. Returns what went wrong.
        /// </summary>
        public static List<string> Revert(Func<string, V4ConfigAsset> get, Func<string, GameObject> loadModel = null)
        {
            var errors = new List<string>();
            void Set(string id, string path, string text)
            {
                var c = get(id);
                if (c == null) { errors.Add($"{id}: missing"); return; }
                if (!ConfigFieldWalker.TryParse(ConfigPath.TypeAt(c, path), text, out var v)) { errors.Add($"{id}.{path}: '{text}'"); return; }
                ConfigPath.Set(c, path, v);
            }
            foreach (var e in Edits) Set(e.configId, e.path, e.pre);
            foreach (var e in TunedBefore) Set(e.configId, e.path, e.pre);
            if (loadModel != null)
                foreach (var m in Models)
                {
                    var c = get(m.configId);
                    if (c == null) { errors.Add($"{m.configId}: missing"); continue; }
                    ConfigPath.Set(c, m.path, string.IsNullOrEmpty(m.pre) ? null : loadModel(m.pre));
                }
            foreach (var (id, hp, damage) in LateTypes)
            {
                switch (get(id))
                {
                    case EnemyArchetypeConfig a: a.stats.baseHP = hp; a.stats.baseDamage = damage; break;
                    case EnemyVariantConfig v: v.baseHP.enabled = true; v.baseHP.value = hp; v.baseDamage.enabled = false; v.baseDamage.value = 0f; break;
                    default: errors.Add($"{id}: missing"); break;
                }
            }
            var timeline = get("timeline.saltwind") as WaveTimelineConfig;
            if (timeline == null) { errors.Add("timeline missing"); return errors; }
            foreach (var (band, share) in new[] { ("band_3", PreBand3Specialists), ("band_4", PreBand4Specialists) })
            {
                var phase = timeline.phases.FirstOrDefault(p => p.phaseId == band);
                if (phase == null || phase.pools.Count < 2) { errors.Add($"{band}: no specialist pool"); continue; }
                phase.pools[0].share = 1f - share;
                phase.pools[1].share = share;
            }
            foreach (var p in timeline.phases.Where(p => p.focus != null && p.focus.enabled && p.focus.scheduledEliteIndex < 0))
                p.focus.hpMultiplier = PreReefCrabHp;
            timeline.flow.waves = PreWaves.Select(k => new MinuteWave
            {
                startSeconds = k.t, minAliveShare = k.share, rateMultiplier = k.rate, topUpIntervalSeconds = k.interval, maxTopUpPerTick = k.perTick,
            }).ToList();
            var ordered = timeline.flow.hordes.OrderBy(h => h.timeSeconds).ToList();
            for (var i = 0; i < ordered.Count && i < PreHordes.Length; i++)
            {
                ordered[i].timeSeconds = PreHordes[i].t;
                ordered[i].count = PreHordes[i].count;
                ordered[i].speedMultiplier = PreHordes[i].speed;
                ordered[i].hpMultiplier = PreHordes[i].hp;
            }
            if (get(RepairTender) is EnemyVariantConfig tender) { tender.baseDamage.enabled = false; tender.baseDamage.value = 0f; }
            else errors.Add(RepairTender + ": missing");
            return errors;
        }

        static string F(float v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>The repair tender is built on the boarding barge: its damage stays the barge's before M3d (8), not the
        /// late type's (review M3d #13).</summary>
        public const string RepairTender = "variant.repair_tender";
        public const float RepairTenderDamage = 8f;

        /// <summary>
        /// Writes every M3d value to the configs <paramref name="get"/> resolves (by config id); the models are loaded by
        /// <paramref name="loadModel"/> (asset path → model; the Editor passes AssetDatabase). Returns what went wrong.
        /// </summary>
        public static List<string> Apply(Func<string, V4ConfigAsset> get, Tuning t, Func<string, GameObject> loadModel = null)
        {
            var errors = new List<string>();
            void Set(string id, string path, string text)
            {
                var c = get(id);
                if (c == null) { errors.Add($"{id}: missing"); return; }
                try
                {
                    if (!ConfigFieldWalker.TryParse(ConfigPath.TypeAt(c, path), text, out var v)) { errors.Add($"{id}.{path}: '{text}'"); return; }
                    ConfigPath.Set(c, path, v);
                }
                catch (Exception e) { errors.Add($"{id}.{path}: {e.Message}"); }
            }
            foreach (var e in Edits) Set(e.configId, e.path, e.value);
            foreach (var kv in TuningPaths)
            {
                var parts = kv.Key.Split('|');
                Set(parts[0], parts[1], kv.Value(t));
            }
            if (loadModel != null)
                foreach (var m in Models)
                {
                    var c = get(m.configId);
                    if (c == null) { errors.Add($"{m.configId}: missing"); continue; }
                    ConfigPath.Set(c, m.path, string.IsNullOrEmpty(m.model) ? null : loadModel(m.model));
                }
            foreach (var (id, hp, damage) in LateTypes)
            {
                switch (get(id))
                {
                    case EnemyArchetypeConfig a: a.stats.baseHP = hp * t.LateHp; a.stats.baseDamage = damage * t.LateDamage; break;
                    case EnemyVariantConfig v:
                        v.baseHP.enabled = true; v.baseHP.value = hp * t.LateHp;
                        v.baseDamage.enabled = !Mathf.Approximately(t.LateDamage, 1f); v.baseDamage.value = v.baseDamage.enabled ? damage * t.LateDamage : 0f;
                        break;
                    default: errors.Add($"{id}: missing"); break;
                }
            }
            if (get(RepairTender) is EnemyVariantConfig tender) { tender.baseDamage.enabled = true; tender.baseDamage.value = RepairTenderDamage; }
            else errors.Add(RepairTender + ": missing");
            var timeline = get("timeline.saltwind") as WaveTimelineConfig;
            if (timeline == null) { errors.Add("timeline missing"); return errors; }
            foreach (var (band, share) in new[] { ("band_3", t.Band3Specialists), ("band_4", t.Band4Specialists) })
            {
                var phase = timeline.phases.FirstOrDefault(p => p.phaseId == band);
                if (phase == null || phase.pools.Count < 2) { errors.Add($"{band}: no specialist pool"); continue; }
                phase.pools[0].share = 1f - share;
                phase.pools[1].share = share;
            }
            // The Reef Crab (the miniboss arena: a focus phase that is not a scheduled elite).
            foreach (var p in timeline.phases.Where(p => p.focus != null && p.focus.enabled && p.focus.scheduledEliteIndex < 0))
                p.focus.hpMultiplier = t.ReefCrabHp;
            var flow = timeline.flow;
            flow.smoothWaves = true;
            flow.waves = Keyframes.Select(k => new MinuteWave
            {
                startSeconds = k.t, minAliveShare = Mathf.Clamp01(k.share * t.FloorScale), rateMultiplier = k.rate * t.RateScale,
                topUpIntervalSeconds = k.interval, maxTopUpPerTick = k.perTick,
            }).ToList();
            var ordered = flow.hordes.OrderBy(h => h.timeSeconds).ToList();
            if (ordered.Count > 0 && ordered.Count < Hordes.Length)
            {
                // The early horde: a copy of the first (a ring of swordfish).
                var first = ordered[0];
                var early = new HordeEvent
                {
                    hordeId = "early_ring", label = first.label, formation = first.formation, archetype = first.archetype, distance = first.distance,
                    spacing = first.spacing, speedMultiplier = first.speedMultiplier, hpMultiplier = first.hpMultiplier, damageMultiplier = first.damageMultiplier,
                    warningLeadSeconds = first.warningLeadSeconds, warningToast = first.warningToast,
                };
                flow.hordes.Insert(0, early);
                ordered.Insert(0, early);
            }
            for (var i = 0; i < ordered.Count && i < Hordes.Length; i++)
            {
                ordered[i].timeSeconds = Hordes[i].t;
                ordered[i].count = Mathf.Max(1, Mathf.RoundToInt(Hordes[i].count * t.HordeScale));
                // k: a horde ship is the ordinary ship it looks like (same speed, HP and damage); a horde is more ships at once.
                ordered[i].speedMultiplier = ordered[i].hpMultiplier = ordered[i].damageMultiplier = 1f;
            }
            return errors;
        }
    }
}
