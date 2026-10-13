using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using RoguePirates.V4.Config;
using RoguePirates.V4.Difficulty;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Progression;
using RoguePirates.V4.Spawning;
using RoguePirates.V4.Weapons;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// V5 M8z (I8z.7, owner 7 Oct 2026: one simulator, "but also ensure that it can read the game's real configs"): writes
    /// the numbers the JavaScript Monte Carlo model (plan/playtest/montecarlo/sim.mjs) reads to its data.json, from the
    /// config set the game runs on (SaltwindDefault, copied into a <see cref="ConfigSetRuntime"/> as V4Game does, with an
    /// optional preset applied), resolved the game's way: variants through <see cref="EnemyStatResolver"/>, scheduled
    /// elites with their spawner's elite multiplier and affix, the XP table through <see cref="ProgressionMath"/>, each
    /// weapon's hits per firing and coverage as <see cref="PlayerPowerIndex"/> counts them. The schema is export_data.py's
    /// (the YAML fallback; compare the two with check_export.mjs) plus the fields only the game resolves.
    /// Run: menu RoguePirates ▸ V5 ▸ Balance: export the Monte Carlo data, or
    /// <c>unity command eval "return RoguePirates.V4.Editor.MonteCarloExport.Run();"</c>.
    /// </summary>
    public static class MonteCarloExport
    {
        public const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";

        /// <summary>plan/playtest/montecarlo next to the Unity project.</summary>
        public static string ModelFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "plan", "playtest", "montecarlo"));

        [MenuItem("RoguePirates/V5/Balance: export the Monte Carlo data (data.json)")]
        public static void Menu() => Debug.Log(Run());

        /// <summary>Exports the default set to plan/playtest/montecarlo/data.json; returns a one-line summary.</summary>
        public static string Run() => RunTo(Path.Combine(ModelFolder, "data.json"), null);

        /// <summary>
        /// Exports to <paramref name="outPath"/>; <paramref name="presetPath"/> (optional) is a preset JSON applied first,
        /// as a run requested with that preset would.
        /// </summary>
        public static string RunTo(string outPath, string presetPath)
        {
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);
            if (set == null) return "MonteCarloExport: no config set at " + SetPath;
            using var rt = new ConfigSetRuntime(set);
            string preset = null;
            if (!string.IsNullOrEmpty(presetPath))
            {
                var errors = new List<string>();
                if (!PresetCodec.Apply(rt, PresetCodec.FromJson(File.ReadAllText(presetPath)), 0f, errors))
                    return "MonteCarloExport: preset failed: " + string.Join("; ", errors);
                preset = Path.GetFileName(presetPath);
            }
            var data = Build(rt, preset);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllText(outPath, Json.Write(data), new UTF8Encoding(false));
            var enemies = ((IDictionary)data["enemies"]).Count;
            var weapons = ((IDictionary)data["weapons"]).Count;
            var timeline = (Dictionary<string, object>)data["timeline"];
            return $"MonteCarloExport: wrote {outPath}: {enemies} enemies, {weapons} weapons, {((IList)timeline["bands"]).Count} bands, " +
                   $"{((IList)timeline["focus"]).Count} focus, {((IList)timeline["hordes"]).Count} hordes; config {rt.Hash.Substring(0, 10)}";
        }

        // ---- The data ---------------------------------------------------------------------------------------------------

        static readonly string[] BehaviorNames = { "Direct chaser", "Flanking chaser", "Telegraphed charge", "Keeps range", "Suicide approach", "Tank chaser", "Strafe burst",
            "Formation ranged", "Fast swarm", "Dive lunge", "Drift pulse", "Stationary turret", "Phase dash", "Tentacle zone", "Support buff" };
        static readonly string[] MoveNames = { "Default", "Direct chase", "Intercept", "Flank", "Orbit", "Keeps range", "Strafe", "Telegraphed charge", "Dive lunge", "Swarm", "Drift", "Stationary" };
        static readonly string[] AttackNames = { "Default", "Contact", "Single shot", "Burst volley", "Spread volley", "Charge", "Area denial", "Explodes", "None" };
        static readonly string[] WeaponTypeNames = { "Projectile", "Fan", "Orbit", "Cone stream", "Trail", "Mine", "Area strike", "Aura", "Predicted area", "Summon", "Chained mace" };
        static string Name(string[] names, int i) => i >= 0 && i < names.Length ? names[i] : i.ToString(CultureInfo.InvariantCulture);

        /// <summary>The affix HP multiplier the enemy world applies at spawn (EnemySimulation.Spawn).</summary>
        static float AffixHp(ConfigEnums.EliteAffix affix) =>
            affix == ConfigEnums.EliteAffix.Armored ? 1.8f : affix == ConfigEnums.EliteAffix.Swift ? 0.9f : affix == ConfigEnums.EliteAffix.Rich ? 1.25f : 1f;

        /// <summary>An elite spawner's affix as the game reads it (none = Rich, EnemySimulation.SpawnFromProfile).</summary>
        static ConfigEnums.EliteAffix EliteAffixOf(SpawnerElite e) => e.isElite && e.affix == ConfigEnums.EliteAffix.None ? ConfigEnums.EliteAffix.Rich : e.affix;

        static bool Ranged(ResolvedEnemyStats r, ArchetypeWorldRules world, IEnumerable<string> concurrency)
        {
            var a = r.combat != null ? r.combat.attackPattern : ConfigEnums.EnemyAttackPattern.UseDefinition;
            return a == ConfigEnums.EnemyAttackPattern.SingleShot || a == ConfigEnums.EnemyAttackPattern.BurstVolley || a == ConfigEnums.EnemyAttackPattern.SpreadVolley
                   || (world != null && world.alwaysRanged) || (concurrency != null && concurrency.Contains("ranged_gunner"));
        }

        public static Dictionary<string, object> Build(ConfigSetRuntime rt, string preset = null)
        {
            var timelineConfig = rt.Single<WaveTimelineConfig>();
            var prog = rt.Single<ProgressionConfig>();
            var pick = rt.Single<PickupConfig>();
            var curve = rt.Single<DifficultyCurveConfig>();
            var director = rt.Single<DirectorConfig>();
            var unlocks = rt.Single<UnlockConfig>();
            var offerWeights = rt.Single<OfferWeightsConfig>();
            var run = rt.Single<RunConfig>();
            var stage = rt.Single<StageContentConfig>();
            var experiment = rt.Single<ExperimentConfig>();
            var personalities = rt.Single<PlayerPersonalityConfig>();
            var disabled = new HashSet<string>(unlocks != null && unlocks.disabledWeaponIds != null ? unlocks.disabledWeaponIds : Array.Empty<string>());

            // Enemies: archetypes, then variants resolved the game's way.
            var enemies = new Dictionary<string, object>();
            var sheetEnemies = new List<object>();
            void AddEnemy(ResolvedEnemyStats r, string baseId, bool variant, ArchetypeWorldRules world, string[] concurrency)
            {
                if (r == null || string.IsNullOrEmpty(r.id)) return;
                var c = r.combat;
                var ranged = Ranged(r, world, concurrency);
                enemies[r.id] = new Dictionary<string, object>
                {
                    ["id"] = r.id, ["name"] = string.IsNullOrEmpty(r.displayName) ? r.id : r.displayName, ["hp"] = r.baseHP, ["dmg"] = r.baseDamage, ["speed"] = r.moveSpeed,
                    ["xp"] = (float)r.xpValue, ["gold"] = (float)r.goldValue, ["boss"] = r.isBoss, ["miniboss"] = r.isMiniboss, ["elite"] = r.isElite,
                    ["base"] = baseId, ["ranged"] = ranged, ["tier"] = (int)r.tier, ["behavior"] = (int)r.behavior, ["radius"] = r.radius, ["threat"] = (float)r.threatCost,
                    ["tags"] = (r.tags ?? Array.Empty<string>()).Cast<object>().ToList(), ["concurrency"] = (concurrency ?? Array.Empty<string>()).Cast<object>().ToList(),
                    ["combat"] = c == null ? null : new Dictionary<string, object>
                    {
                        ["move"] = (int)c.movementPattern, ["attack"] = (int)c.attackPattern, ["range"] = c.attackRange, ["cooldown"] = c.attackCooldown > 0f ? c.attackCooldown : r.attackCooldown,
                        ["windup"] = c.attackWindUp, ["shots"] = c.projectileCount, ["shotSpeed"] = c.projectileSpeed, ["contact"] = c.contactDamageEnabled,
                        ["chargeCooldown"] = c.chargeCooldown,
                    },
                };
                var role = r.isBoss ? "Boss" : r.isMiniboss ? "Miniboss" : r.isElite ? "Elite" : variant ? "Variant" : "Ordinary";
                sheetEnemies.Add(new Dictionary<string, object>
                {
                    ["id"] = r.id, ["name"] = string.IsNullOrEmpty(r.displayName) ? r.id : r.displayName, ["role"] = role, ["kind"] = r.category == ConfigEnums.EnemyCategory.Ship ? "Ship" : "Creature",
                    ["base"] = baseId, ["hp"] = r.baseHP, ["dmg"] = r.baseDamage, ["speed"] = r.moveSpeed, ["radius"] = r.radius, ["xp"] = (float)r.xpValue, ["gold"] = (float)r.goldValue,
                    ["behavior"] = Name(BehaviorNames, (int)r.behavior), ["move"] = Name(MoveNames, c != null ? (int)c.movementPattern : 0),
                    ["attack"] = Name(AttackNames, c != null ? (int)c.attackPattern : 0), ["range"] = c != null ? c.attackRange : 0f,
                    ["cooldown"] = c != null && c.attackCooldown > 0f ? c.attackCooldown : r.attackCooldown, ["windup"] = c != null ? c.attackWindUp : 0f,
                    ["shots"] = c != null ? c.projectileCount : 0, ["shotSpeed"] = c != null ? c.projectileSpeed : 0f, ["contact"] = c == null || c.contactDamageEnabled,
                    ["ranged"] = ranged, ["threat"] = (float)r.threatCost,
                });
            }
            foreach (var a in rt.All<EnemyArchetypeConfig>().OrderBy(a => a.enemyId, StringComparer.Ordinal))
                AddEnemy(EnemyStatResolver.Resolve(a), null, false, a.world, a.concurrencyTags);
            foreach (var v in rt.All<EnemyVariantConfig>().Where(v => v.baseArchetype != null).OrderBy(v => v.variantId, StringComparer.Ordinal))
                AddEnemy(EnemyStatResolver.Resolve(v), v.baseArchetype.enemyId, true, v.world, v.concurrencyTags);

            string EntryId(PoolEntry e) => e.variant != null ? e.variant.variantId : e.archetype != null ? e.archetype.enemyId : e.enemyId;

            // Timeline: ambient bands, focus phases (minibosses and scheduled elites, with or without an arena), pulses, the flow.
            var bands = new List<object>();
            var focus = new List<object>();
            var pulses = new List<object>();
            object bossPhase = null;
            foreach (var ph in timelineConfig.phases.OrderBy(p => p.trigger.timeSeconds))
            {
                if (PhaseKinds.IsAmbient(ph) && !ph.isOvertime && ph.pools.Count > 0)
                {
                    bands.Add(new Dictionary<string, object>
                    {
                        ["t"] = ph.trigger.timeSeconds, ["rate"] = ph.ratePerSecond, ["id"] = ph.phaseId,
                        ["pools"] = ph.pools.Select(pool => (object)new Dictionary<string, object>
                        {
                            ["share"] = pool.share,
                            ["entries"] = pool.entries.Select(e => (object)new List<object> { EntryId(e), e.weight }).ToList(),
                        }).ToList(),
                    });
                }
                else if (PhaseKinds.IsFocus(ph))
                {
                    var f = ph.focus;
                    var sp = ph.spawner;
                    var anchor = sp != null && sp.anchor != null ? sp.anchor.enemyId : null;
                    var enemyId = f.variant != null ? f.variant.variantId : f.archetype != null ? f.archetype.enemyId : anchor;
                    var elite = f.placement == FocusPlacement.SpawnerProfile && sp != null && sp.elite.isElite;
                    // The HP the enemy world gives it before the time-to-kill floor: focus × (elite spawner: its multiplier × its affix).
                    var hpMult = f.hpMultiplier;
                    var dmgMult = f.damageMultiplier;
                    if (f.placement == FocusPlacement.SpawnerProfile && sp != null && sp.elite.isElite)
                    {
                        hpMult *= sp.elite.hpMultiplier * AffixHp(EliteAffixOf(sp.elite));
                        dmgMult *= sp.elite.damageMultiplier;
                    }
                    else if (f.placement == FocusPlacement.RingAroundPlayer) hpMult *= AffixHp(f.affix);
                    focus.Add(new Dictionary<string, object>
                    {
                        ["id"] = ph.phaseId, ["t"] = ph.trigger.timeSeconds, ["enemy"] = enemyId, ["hpMult"] = hpMult, ["dmgMult"] = dmgMult,
                        ["arena"] = ph.pauseAmbient, ["elite"] = elite, ["scheduledElite"] = f.scheduledEliteIndex >= 0, ["waitForOtherArena"] = f.waitForOtherArena,
                    });
                }
                else if (PhaseKinds.IsPulse(ph))
                {
                    pulses.Add(new Dictionary<string, object>
                    {
                        ["t"] = ph.trigger.timeSeconds, ["seconds"] = ph.duration.kind == PhaseDurationKind.Seconds ? ph.duration.seconds : 0f, ["mult"] = ph.rateMultiplier, ["id"] = ph.phaseId,
                    });
                }
                else if (ph.isBoss && ph.spawner != null && ph.spawner.anchor != null)
                    bossPhase = new Dictionary<string, object> { ["id"] = ph.phaseId, ["t"] = ph.trigger.timeSeconds, ["enemy"] = ph.spawner.anchor.enemyId, ["trigger"] = ph.trigger.kind.ToString() };
            }
            var flow = timelineConfig.flow;
            var o = timelineConfig.overtime;
            var timeline = new Dictionary<string, object>
            {
                ["deadline"] = timelineConfig.stageDeadlineSeconds, ["cap"] = timelineConfig.maxActiveEnemies, ["reserved"] = timelineConfig.reservedEncounterSlots,
                ["bands"] = bands, ["focus"] = focus, ["pulses"] = pulses,
                ["waves"] = flow.waves.Select(w => (object)new List<object> { w.startSeconds, w.minAliveShare, w.rateMultiplier }).ToList(),
                ["hordes"] = flow.hordes.Select(h => (object)new List<object> { h.timeSeconds, h.archetype != null ? h.archetype.enemyId : null, h.count }).ToList(),
                ["overtime"] = new Dictionary<string, object>
                {
                    ["interval"] = o.initialSpawnIntervalSeconds, ["floor"] = o.intervalFloorSeconds, ["decay"] = o.intervalDecayPerStep, ["step"] = o.pressureStepSeconds,
                    ["pursuerFirst"] = o.pursuerWeightFirstTwoMinutes, ["pursuerAfter"] = o.pursuerWeightAfterTwoMinutes,
                    ["rampStart"] = o.spawnStatRampStart, ["rampEnd"] = o.spawnStatRampEnd, ["rampSeconds"] = o.spawnStatRampSeconds,
                },
                ["tagCaps"] = timelineConfig.tagCaps.Where(c => c != null && !string.IsNullOrEmpty(c.tag)).GroupBy(c => c.tag).ToDictionary(g => g.Key, g => (object)g.First().max),
                // Only the game resolves these (export_data.py does not read them).
                ["grace"] = timelineConfig.graceSeconds, ["spawnGap"] = timelineConfig.globalMinSpawnGapSeconds, ["bossAmbientRate"] = timelineConfig.bossAmbientRateMultiplier,
                ["flow"] = new Dictionary<string, object>
                {
                    ["enabled"] = flow.enabled, ["smooth"] = flow.smoothWaves,
                    ["waves"] = flow.waves.Select(w => (object)new Dictionary<string, object>
                    {
                        ["t"] = w.startSeconds, ["share"] = w.minAliveShare, ["rate"] = w.rateMultiplier, ["topUpInterval"] = w.topUpIntervalSeconds, ["maxTopUp"] = w.maxTopUpPerTick,
                    }).ToList(),
                    ["hordes"] = flow.hordes.Select(h => (object)new Dictionary<string, object>
                    {
                        ["id"] = h.hordeId, ["t"] = h.timeSeconds, ["enemy"] = h.archetype != null ? h.archetype.enemyId : null, ["count"] = h.count,
                        ["hpMult"] = h.hpMultiplier, ["dmgMult"] = h.damageMultiplier,
                    }).ToList(),
                    ["release"] = flow.release == null ? null : new Dictionary<string, object>
                    {
                        ["rate"] = flow.release.rateMultiplier, ["floor"] = flow.release.floorMultiplier, ["afterArena"] = flow.release.afterArenaSeconds,
                        ["afterBoss"] = flow.release.afterBossKillSeconds, ["hordeDelay"] = flow.release.hordeReleaseDelaySeconds, ["afterHorde"] = flow.release.afterHordeSeconds,
                        ["bossFightFloor"] = flow.release.bossFightFloorMultiplier, ["preHordeLull"] = flow.release.preHordeLullSeconds,
                    },
                },
                ["storm"] = new Dictionary<string, object>
                {
                    ["start"] = timelineConfig.storm.contractionStartSeconds, ["end"] = timelineConfig.storm.contractionEndSeconds, ["dps"] = timelineConfig.storm.outsideDamagePerSecond,
                },
                ["boss"] = bossPhase,
            };

            // Weapons: the base numbers, upgrade axes, and what the director's offense (the time-to-kill floors' DPS) counts.
            var weapons = new Dictionary<string, object>();
            var sheetWeapons = new List<object>();
            var rum = (unlocks != null && unlocks.weaponUnlocks != null ? unlocks.weaponUnlocks : Array.Empty<WeaponUnlock>())
                .Where(u => u != null && !string.IsNullOrEmpty(u.weaponId)).GroupBy(u => u.weaponId).ToDictionary(g => g.Key, g => g.First().rumCost);
            var starters = unlocks != null && unlocks.starterWeaponIds != null ? unlocks.starterWeaponIds : Array.Empty<string>();
            foreach (var w in rt.All<WeaponConfig>().Where(w => !string.IsNullOrEmpty(w.weaponId)).OrderBy(w => w.weaponId, StringComparer.Ordinal))
            {
                var coverage = director.behaviorCoverage != null && (int)w.behavior < director.behaviorCoverage.Length ? director.behaviorCoverage[(int)w.behavior] : 1f;
                var off = disabled.Contains(w.weaponId);
                if (!off)
                    weapons[w.weaponId] = new Dictionary<string, object>
                    {
                        ["id"] = w.weaponId, ["name"] = w.displayName, ["damage"] = w.baseDamage, ["cooldown"] = w.baseCooldown, ["quantity"] = w.baseQuantity, ["behavior"] = (int)w.behavior,
                        ["maxLevel"] = w.maxLevel, ["spin"] = w.orbit.angularSpeedDegrees, ["blades"] = w.orbit.baseBlades, ["pierce"] = w.projectile.pierce, ["lane"] = w.lane.enabled,
                        ["axes"] = (w.upgradeAxes ?? Array.Empty<WeaponUpgradeAxis>()).Select(x => (object)new Dictionary<string, object>
                        {
                            ["axis"] = (int)x.axis, ["inc"] = x.baseIncrement, ["weight"] = x.offerWeight, ["cap"] = x.maxAccumulated,
                        }).ToList(),
                        ["levelDamageFactor"] = w.levelDamageFactor, ["hitsPerFiring"] = WeaponStatResolver.HitsPerFiring(w), ["coverage"] = coverage,
                        ["databaseOrder"] = w.databaseOrder, ["rum"] = rum.TryGetValue(w.weaponId, out var cost) ? cost : 0,
                        // V5 M9a (I9a.3): the Cursed Doubloons' bonus gold per kill and its cap a run (the run checks' gold bound).
                        ["goldPerKill"] = w.goldPerKill, ["goldPerKillRunCap"] = w.goldPerKillRunCap,
                        // V5 M8z (7 Oct, Q20): the rank milestones (the simulator applies them from their level on).
                        ["milestones"] = (w.milestones ?? Array.Empty<WeaponMilestone>()).Where(m => m != null).Select(m => (object)new Dictionary<string, object>
                        {
                            ["level"] = m.level, ["quantity"] = m.extraQuantity, ["size"] = m.sizeMultiplier, ["duration"] = m.durationMultiplier, ["slow"] = m.extraSlow,
                            ["pierce"] = m.extraPierce, ["spin"] = m.spinMultiplier, ["slowSeconds"] = m.extraSlowSeconds, ["echoes"] = m.echoes,
                        }).ToList(),
                    };
                sheetWeapons.Add(new Dictionary<string, object>
                {
                    ["id"] = w.weaponId, ["name"] = w.displayName, ["type"] = Name(WeaponTypeNames, (int)w.behavior), ["tags"] = (w.tags ?? Array.Empty<string>()).Cast<object>().ToList(),
                    ["damage"] = w.baseDamage, ["cooldown"] = w.baseCooldown, ["quantity"] = w.baseQuantity, ["range"] = w.range, ["speed"] = w.projectileSpeed, ["pierce"] = w.projectile.pierce,
                    ["blades"] = w.behavior == ConfigEnums.WeaponBehavior.Orbit ? w.orbit.baseBlades : 0, ["spin"] = w.behavior == ConfigEnums.WeaponBehavior.Orbit ? w.orbit.angularSpeedDegrees : 0f,
                    ["maxLevel"] = w.maxLevel, ["rum"] = rum.TryGetValue(w.weaponId, out var r2) ? r2 : 0, ["starter"] = starters.Contains(w.weaponId) && !rum.ContainsKey(w.weaponId),
                    ["off"] = off, ["text"] = w.description ?? string.Empty,
                });
            }

            var logbooks = rt.All<LogbookConfig>().Where(l => !string.IsNullOrEmpty(l.logbookId)).OrderBy(l => l.logbookId, StringComparer.Ordinal).ToDictionary(l => l.logbookId, l => (object)new Dictionary<string, object>
            {
                ["id"] = l.logbookId, ["name"] = l.displayName, ["stat"] = l.stat, ["add"] = l.perLevelAdditive, ["max"] = l.maxLevel, ["mult"] = l.perLevelMultiplicative, ["inPool"] = l.inSaltwindPool,
                // Hackathon (13 Oct): the Chaos Logbook rolls one of these Logbooks' stats per rank (null for every other Logbook).
                ["randomFrom"] = l.randomStat ? (l.randomStatFrom ?? Array.Empty<string>()).Cast<object>().ToList() : null,
            });
            var items = rt.All<ItemConfig>().Where(i => !string.IsNullOrEmpty(i.itemId)).OrderBy(i => i.itemId, StringComparer.Ordinal).ToDictionary(i => i.itemId, i => (object)new Dictionary<string, object>
            {
                ["id"] = i.itemId, ["name"] = i.displayName, ["rarity"] = i.rarity, ["inChest"] = i.inChestPool,
                ["mods"] = (i.statModifiers ?? Array.Empty<ItemStatModifier>()).Select(m => (object)new List<object> { m.stat, m.additive, m.multiplicative }).ToList(),
                ["effects"] = (i.effects ?? Array.Empty<ItemEffect>()).Where(e => e != null && e.active).Select(e => (object)new List<object> { e.kind.ToString(), e.value }).ToList(),
            });
            var evolutions = rt.All<WeaponEvolutionConfig>().Where(e => !string.IsNullOrEmpty(e.evolutionId)).OrderBy(e => e.evolutionId, StringComparer.Ordinal).ToDictionary(e => e.evolutionId, e => (object)new Dictionary<string, object>
            {
                ["id"] = e.evolutionId, ["name"] = e.displayName, ["weapon"] = e.baseWeapon != null && weapons.ContainsKey(e.baseWeapon.weaponId) ? e.baseWeapon.weaponId : null,
                ["item"] = e.requiredItemId, ["effect"] = e.effect.ToString(),
                ["empower"] = e.effect == ConfigEnums.EvolutionEffect.Empowered ? new Dictionary<string, object>
                {
                    ["damage"] = e.empower.damageMultiplier, ["cooldown"] = e.empower.cooldownMultiplier, ["quantity"] = e.empower.extraQuantity, ["size"] = e.empower.sizeMultiplier,
                } : null,
            });
            // A Captain whose weapon is switched off starts with the First Conquest weapon (from config, no content id here).
            var fallbackWeapon = unlocks != null && !string.IsNullOrEmpty(unlocks.firstConquestWeaponId) ? unlocks.firstConquestWeaponId : starters.FirstOrDefault();
            var captains = rt.All<CaptainConfig>().Where(c => !string.IsNullOrEmpty(c.captainKey)).OrderBy(c => c.captainKey, StringComparer.Ordinal).ToDictionary(c => c.captainKey, c => (object)new Dictionary<string, object>
            {
                ["id"] = c.captainKey, ["name"] = string.IsNullOrEmpty(c.displayName) ? c.captainKey : c.displayName,
                ["weapon"] = weapons.ContainsKey(c.startingWeaponId ?? string.Empty) ? c.startingWeaponId : fallbackWeapon, ["hull"] = c.baseHull,
                // Only the game resolves these: the passive damage per player level and its cap (the director's DPS counts it).
                ["damagePerLevel"] = c.passive != null ? c.passive.additiveDamagePerLevel : 0f, ["damageCap"] = c.passive != null ? c.passive.additiveDamageCap : 0f,
            });

            // The XP to the next level from level 0, as the game resolves it (the fitted table, extended past its end).
            var xpToNext = new List<object>();
            for (var level = 0; level < 150; level++) xpToNext.Add(ProgressionMath.XpToNext(prog, level));

            var data = new Dictionary<string, object>
            {
                ["enemies"] = enemies,
                ["timeline"] = timeline,
                ["weapons"] = weapons,
                ["logbooks"] = logbooks,
                ["items"] = items,
                ["evolutions"] = evolutions,
                ["captains"] = captains,
                ["progression"] = new Dictionary<string, object>
                {
                    ["xpTable"] = (prog.xpToNextTable ?? Array.Empty<int>()).Cast<object>().ToList(), ["weaponSlots"] = prog.weaponSlots, ["logbookSlots"] = prog.logbookSlots,
                    ["levelsPerRarity"] = (prog.levelsPerRarity ?? Array.Empty<int>()).Cast<object>().ToList(), ["rarityMult"] = (prog.rarityMultipliers ?? Array.Empty<float>()).Cast<object>().ToList(),
                    ["evolutionLevel"] = prog.evolutionUnlockLevel, ["maxWeaponLevel"] = prog.maxWeaponLevel,
                    // Only the game resolves these.
                    ["startLevel"] = prog.startLevel, ["xpToNext"] = xpToNext, ["newWeaponDamageBonus"] = prog.newWeaponDamageBonus, ["cardsPerLevel"] = prog.cardsPerLevel,
                    ["refreshesPerRun"] = prog.refreshesPerRun, ["rarityWeights"] = (prog.rarityBaseWeights ?? Array.Empty<float>()).Cast<object>().ToList(),
                    ["upgradePowerMultiplier"] = prog.upgradePowerMultiplier, ["linesPerRarity"] = (prog.upgradeLinesPerRarity ?? Array.Empty<int>()).Cast<object>().ToList(), ["rarePityLevelUps"] = prog.rarePityLevelUps, ["pityMinRarity"] = prog.pityMinRarity,
                },
                ["pickups"] = new Dictionary<string, object>
                {
                    ["xpMult"] = pick.xpPickupMultiplier, ["healValue"] = (float)pick.healthDropValue, ["healFraction"] = pick.healthDropHullFraction,
                    ["lowWait"] = pick.healthDropLowHullWaitMultiplier, ["healMin"] = pick.healthDropIntervalMin, ["healMax"] = pick.healthDropIntervalMax,
                    ["lowHull"] = pick.healthDropLowHullFraction, ["healChance"] = pick.healthDropChance,
                    // A kill's XP barrel bursts into this many pieces (× the quality tier's effects density), each paying at
                    // least 1 XP (PickupModule.Burst): the XP a kill pays depends on the piece count, not only its xpValue.
                    ["burstMin"] = pick.burstCountMin, ["burstMax"] = pick.burstCountMax, ["burstMinimum"] = pick.burstMinimum,
                },
                ["curve"] = new Dictionary<string, object>
                {
                    ["hp"] = new List<object> { curve.enemyHpPerMinute.c0, curve.enemyHpPerMinute.c1, curve.enemyHpPerMinute.c2 },
                    ["dmg"] = new List<object> { curve.enemyDamagePerMinute.c0, curve.enemyDamagePerMinute.c1, curve.enemyDamagePerMinute.c2 },
                    ["global_hp"] = curve.globalHpMultiplier,
                    ["ordinaryDelay"] = curve.ordinaryScalingDelaySeconds,
                    ["ordinaryHp"] = new List<object> { curve.ordinaryHp.c0, curve.ordinaryHp.c1, curve.ordinaryHp.c2 },
                    ["ordinaryDmg"] = new List<object> { curve.ordinaryDamage.c0, curve.ordinaryDamage.c1, curve.ordinaryDamage.c2 },
                    ["overtimeHpBase"] = curve.overtimeHpMultiplierBase, ["overtimeHpExponent"] = curve.overtimeHpMultiplierExponentPerMinute,
                    ["overtimeDamagePerMinute"] = curve.overtimeDamageBonusPerMinute, ["pressureMin"] = curve.pressureMin, ["pressureMax"] = curve.pressureMax,
                    ["polynomial"] = curve.enemyHpPerMinute.mode == CurveMode.Polynomial && curve.enemyDamagePerMinute.mode == CurveMode.Polynomial,
                },
                ["director"] = new Dictionary<string, object>
                {
                    ["killBand"] = director.killBandLever, ["bandSeconds"] = director.killBandSeconds, ["high"] = director.killBandHighRatio, ["low"] = director.killBandLowRatio,
                    ["lowRate"] = director.killBandLowRateMultiplier, ["eliteChance"] = director.killBandEliteChance, ["eliteHp"] = director.eliteHpMultiplier,
                    ["ttk"] = new Dictionary<string, object>
                    {
                        ["boss"] = director.bossMinSeconds, ["arena"] = director.arenaFocusMinSeconds, ["focus"] = director.focusMinSeconds, ["elite"] = director.adaptiveEliteMinSeconds,
                    },
                    // Only the game resolves these.
                    ["mode"] = director.mode.ToString(), ["minSpawns"] = director.killBandMinSpawns,
                    ["bossHpAsIfAtSeconds"] = director.bossHpAsIfAtSeconds, ["arenaMaxPauseSeconds"] = director.arenaMaxPauseSeconds,
                    ["eliteAffixHp"] = AffixHp(director.eliteAffix), ["eliteDamage"] = director.eliteDamageMultiplier,
                    ["threatRate"] = new Dictionary<string, object> { ["on"] = director.threatRateLever, ["gain"] = director.threatRateGain, ["min"] = director.threatRateMin, ["max"] = director.threatRateMax },
                    ["eliteLever"] = new Dictionary<string, object> { ["on"] = director.eliteLever, ["chanceAtMax"] = director.eliteChanceAtMax },
                    ["toughness"] = new Dictionary<string, object> { ["on"] = director.toughnessLever, ["hpAtMax"] = director.toughnessHpAtMax, ["dmgAtMax"] = director.toughnessDamageAtMax },
                    ["focusLever"] = new Dictionary<string, object> { ["on"] = director.focusLever, ["hpAtMax"] = director.focusHpAtMax, ["dmgAtMax"] = director.focusDamageAtMax },
                    ["capHp"] = new Dictionary<string, object> { ["on"] = director.capHpLever, ["atMax"] = director.capHpMultiplierAtMax },
                    ["coverage"] = (director.behaviorCoverage ?? Array.Empty<float>()).Cast<object>().ToList(), ["evolutionOffense"] = director.evolutionOffenseMultiplier,
                },
                ["armorWorks"] = true,
                ["sheet"] = new Dictionary<string, object> { ["enemies"] = sheetEnemies, ["weapons"] = sheetWeapons },
                ["starterWeapons"] = starters.Cast<object>().ToList(),
                // Only the game resolves these.
                ["offerWeights"] = (offerWeights != null && offerWeights.rows != null ? offerWeights.rows : Array.Empty<OfferWeightRow>()).Where(r => r != null).Select(r => (object)new Dictionary<string, object>
                {
                    ["maxWeapons"] = r.maxWeapons, ["weaponUpgrade"] = r.weaponUpgrade, ["newWeapon"] = r.newWeapon, ["logbookUpgrade"] = r.logbookUpgrade, ["newLogbook"] = r.newLogbook, ["runItem"] = r.runItem,
                }).ToList(),
                ["run"] = new Dictionary<string, object>
                {
                    ["islandImpactDamage"] = run != null ? run.islandImpactDamage : 0f, ["islandDamageCooldown"] = run != null ? run.islandDamageCooldown : 0f,
                    ["islandImpactMinSpeed"] = run != null ? run.islandImpactMinSpeed : 0f, ["shipSpeedMultiplier"] = run != null ? run.shipSpeedMultiplier : 1f,
                },
                ["unlocks"] = new Dictionary<string, object>
                {
                    ["disabled"] = disabled.OrderBy(x => x, StringComparer.Ordinal).Cast<object>().ToList(),
                    ["unlockEverything"] = DemoMode.UnlockEverything,
                    ["rum"] = rum.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => (object)x.Value),
                },
                ["stage"] = stage == null ? null : new Dictionary<string, object>
                {
                    ["evolutionItemChance"] = stage.evolutionItemChance, ["chestBasePrice"] = stage.chestBasePrice, ["chestPriceGrowth"] = stage.chestPriceGrowth,
                    ["chestPriceIncrementBase"] = stage.chestPriceIncrementBase, ["salvageChests"] = stage.salvageChestCount, ["repairWreckHealFraction"] = stage.repairWreckHealFraction,
                    ["beaconHintSeconds"] = stage.beaconDirectionHintSeconds, ["beaconMarkerSeconds"] = stage.beaconExactMarkerSeconds,
                    // V5 M9a (I9a.3): the gold a kill drops (KillFanout.ResolveKillGold) and a cleared Powder Cache's gold.
                    ["killGold"] = stage.killGold == null ? null : new Dictionary<string, object>
                    {
                        ["ordinaryChance"] = stage.killGold.ordinaryEnemyGoldChance, ["ordinaryAmount"] = stage.killGold.ordinaryEnemyGoldAmount,
                        ["specialistAmount"] = stage.killGold.specialistEnemyGoldAmount, ["scheduledEliteAmount"] = stage.killGold.scheduledEliteGoldAmount,
                    },
                    ["powderCacheBonusGold"] = stage.powderCacheBonusGold, ["powderCacheEliteCount"] = stage.powderCacheEliteCount,
                },
                // V5 M9a (I9a.3): the player personalities (plan/v5/PLAYER_PERSONALITIES.md); the run checks' bounds come from
                // the strongest one (plan/playtest/montecarlo/plausibility.mjs).
                ["personalities"] = (personalities != null ? personalities.personalities : new List<PlayerPersonality>()).Select(p => (object)new Dictionary<string, object>
                {
                    ["id"] = p.personalityId, ["name"] = p.displayName, ["share"] = p.share, ["dodgeSkill"] = p.dodgeSkill, ["contactAvoidance"] = p.contactAvoidance,
                    ["movement"] = p.movement.ToString(), ["pickupGreed"] = p.pickupGreed, ["chests"] = p.chests.ToString(), ["landmarks"] = (int)p.landmarks,
                    ["bossTiming"] = p.bossTiming.ToString(), ["bossMinute"] = p.bossMinute, ["cards"] = p.cards.ToString(), ["stop"] = p.stop.ToString(),
                    ["stopMinutes"] = p.stopMinutes,
                }).ToList(),
                ["experiment"] = experiment == null ? null : new Dictionary<string, object>
                {
                    ["spawnDensity"] = experiment.spawnDensityMultiplier, ["enemyHp"] = experiment.enemyHpMultiplier, ["enemyDamage"] = experiment.enemyDamageMultiplier,
                    ["evolutions"] = experiment.weaponEvolutionsEnabled,
                },
                ["demo"] = new Dictionary<string, object> { ["autoBossSeconds"] = DemoMode.AutoBossSeconds },
                ["meta"] = new Dictionary<string, object>
                {
                    ["source"] = "unity", ["setId"] = rt.SetId, ["configHash"] = rt.Hash, ["preset"] = preset,
                    ["exported"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture), ["unity"] = Application.unityVersion,
                },
            };
            return data;
        }

        // ---- A small JSON writer (dictionaries, lists, strings, numbers, booleans, null) --------------------------------------

        public static class Json
        {
            public static string Write(object value)
            {
                var sb = new StringBuilder();
                Write(sb, value, 0);
                sb.Append('\n');
                return sb.ToString();
            }

            static void Indent(StringBuilder sb, int depth) { sb.Append('\n'); sb.Append(' ', depth); }

            static void Write(StringBuilder sb, object v, int depth)
            {
                switch (v)
                {
                    case null: sb.Append("null"); return;
                    case string s: WriteString(sb, s); return;
                    case bool b: sb.Append(b ? "true" : "false"); return;
                    case float f: sb.Append(Number(f)); return;
                    case double d: sb.Append(Number(d)); return;
                    case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); return;
                    case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); return;
                    case IDictionary dict:
                    {
                        if (dict.Count == 0) { sb.Append("{}"); return; }
                        sb.Append('{');
                        var first = true;
                        foreach (DictionaryEntry e in dict)
                        {
                            if (!first) sb.Append(',');
                            first = false;
                            Indent(sb, depth + 1);
                            WriteString(sb, Convert.ToString(e.Key, CultureInfo.InvariantCulture));
                            sb.Append(": ");
                            Write(sb, e.Value, depth + 1);
                        }
                        Indent(sb, depth);
                        sb.Append('}');
                        return;
                    }
                    case IEnumerable list:
                    {
                        var items = list.Cast<object>().ToList();
                        if (items.Count == 0) { sb.Append("[]"); return; }
                        // Short lists of plain values stay on one line.
                        var flat = items.All(x => x == null || x is string || x is bool || x is float || x is double || x is int || x is long);
                        sb.Append('[');
                        for (var i = 0; i < items.Count; i++)
                        {
                            if (i > 0) sb.Append(flat ? ", " : ",");
                            if (!flat) Indent(sb, depth + 1);
                            Write(sb, items[i], depth + 1);
                        }
                        if (!flat) Indent(sb, depth);
                        sb.Append(']');
                        return;
                    }
                    default:
                        if (v is Enum) { sb.Append(Convert.ToInt32(v, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)); return; }
                        WriteString(sb, Convert.ToString(v, CultureInfo.InvariantCulture));
                        return;
                }
            }

            static string Number(double d)
            {
                if (double.IsNaN(d) || double.IsInfinity(d)) return "null";
                return d.ToString("R", CultureInfo.InvariantCulture);
            }

            static string Number(float f)
            {
                if (float.IsNaN(f) || float.IsInfinity(f)) return "null";
                return f.ToString("R", CultureInfo.InvariantCulture);
            }

            static void WriteString(StringBuilder sb, string s)
            {
                sb.Append('"');
                foreach (var c in s)
                {
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default:
                            if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            else sb.Append(c);
                            break;
                    }
                }
                sb.Append('"');
            }
        }
    }
}
