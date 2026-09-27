using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using RoguePirates.V4.Config;
using RoguePirates.V4.Legacy;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// Generates the V4 config assets from the live V3 data: the stage profile, the Resources encounter schedule and
    /// its spawn profiles, the enemy and weapon definitions after CombatWeaponCatalog/CombatSpecialistEnemyCatalog
    /// (applied to a deep clone, so no V3 asset changes), the combat-profile registry, the ambient composition tables
    /// and DemoPacingSettings. Existing V4 assets are kept unless <c>overwrite</c> is set, so design edits survive.
    /// </summary>
    public static class V3ConfigMigrator
    {
        public const string DataRoot = "Assets/_Game/V4/Data";
        public const string ProfilePath = "Assets/_Game/Data/Balance/SaltwindSoloStageBalanceProfile.asset";
        public const string SchedulePath = "Assets/_Game/Resources/EncounterSpawnSchedule.asset";
        public const string PacingPath = "Assets/_Game/Resources/DemoPacingSettings.asset";
        public const string ConfigSetPath = DataRoot + "/ConfigSets/SaltwindDefault.asset";

        [MenuItem("RoguePirates/V4/Migrate V3 Data to Configs")]
        static void MigrateFromMenu() => Debug.Log(Migrate(false));

        /// <summary>
        /// M1/M2 generation from the V3 sources. To rebuild the data from scratch run, in order: this,
        /// <see cref="ApplyM3"/> (which re-applies <see cref="ApplyPlaytestDecisions"/> itself).
        /// </summary>
        public static string Migrate(bool overwrite)
        {
            var log = new StringBuilder();
            // Clean reload first: it unloads the definitions and everything referencing them, then load the sources.
            var sourceDb = LoadCleanDatabase();
            var profile = AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(ProfilePath);
            var schedule = AssetDatabase.LoadAssetAtPath<EncounterSpawnSchedule>(SchedulePath);
            var pacing = AssetDatabase.LoadAssetAtPath<DemoPacingSettings>(PacingPath);
            if (profile == null || schedule == null || pacing == null) return "missing V3 source assets";
            var originalEnemyIds = new HashSet<string>(sourceDb.enemies.Where(e => e != null).Select(e => e.id));
            var originalWeaponIds = new HashSet<string>(sourceDb.weapons.Where(w => w != null).Select(w => w.id));
            var db = CloneDatabase(sourceDb);
            var experiment = CombatExperimentContext.Active;
            CombatWeaponCatalog.ApplyToDatabase(db, experiment);
            CombatSpecialistEnemyCatalog.ApplyToDatabase(db, experiment);

            EnsureFolder(DataRoot);
            var all = new List<V4ConfigAsset>();

            // Archetypes: the authored enemies (specialist aliases become variants in M2).
            var archetypes = new Dictionary<string, EnemyArchetypeConfig>();
            foreach (var def in db.enemies.Where(e => e != null && originalEnemyIds.Contains(e.id)))
            {
                var a = GetOrCreate<EnemyArchetypeConfig>($"{DataRoot}/Enemies/{def.id}.asset", overwrite, log, out var write);
                if (write) FillArchetype(a, def);
                archetypes[def.id] = a;
                all.Add(a);
            }

            // M2: drowned_pursuer/harrier exist only in V3's fallback database (not in the live one, register R-05).
            var fallback = FallbackEnemies();
            foreach (var def in fallback.Where(e => LegacyConfigMapping.FallbackOnlyEnemies.Contains(e.id)))
            {
                var a = GetOrCreate<EnemyArchetypeConfig>($"{DataRoot}/Enemies/{def.id}.asset", overwrite, log, out var write);
                if (write) { FillArchetype(a, def); a.inLegacyDatabase = false; EditorUtility.SetDirty(a); }
                archetypes[def.id] = a;
                all.Add(a);
            }
            foreach (var def in fallback) Object.DestroyImmediate(def);

            // M2: specialist aliases (database variants) and registry elites.
            var variants = new Dictionary<string, EnemyVariantConfig>();
            foreach (var alias in db.enemies.Where(e => e != null && !originalEnemyIds.Contains(e.id)))
            {
                LegacyConfigMapping.SpecialistTemplates.TryGetValue(alias.id, out var template);
                if (template == null || !archetypes.TryGetValue(template, out var baseArchetype)) { log.AppendLine("no template for " + alias.id); continue; }
                var v = GetOrCreate<EnemyVariantConfig>($"{DataRoot}/Variants/{alias.id}.asset", overwrite, log, out var write);
                if (write) FillAliasVariant(v, alias, baseArchetype);
                variants[alias.id] = v;
                all.Add(v);
            }
            foreach (var (eliteId, baseId) in LegacyConfigMapping.RegistryElites)
            {
                if (!EnemyCombatProfileRegistry.TryGet(eliteId, out var eliteProfile) || !archetypes.TryGetValue(baseId, out var baseArchetype)) continue;
                var v = GetOrCreate<EnemyVariantConfig>($"{DataRoot}/Variants/{eliteId}.asset", overwrite, log, out var write);
                if (write)
                {
                    v.configId = "variant." + eliteId;
                    v.variantId = eliteId;
                    v.kind = VariantKind.RegistryElite;
                    v.baseArchetype = baseArchetype;
                    v.displayName = "Elite " + baseArchetype.displayName;
                    v.combat = new ArchetypeCombat();
                    LegacyConfigMapping.CopyFields(eliteProfile, v.combat);
                    v.combat.registered = true;
                    EditorUtility.SetDirty(v);
                }
                variants[eliteId] = v;
                all.Add(v);
            }

            // Spawners: every profile the schedule references.
            var spawners = new Dictionary<SpawnProfile, EnemySpawnerConfig>();
            foreach (var p in ScheduleProfiles(schedule))
            {
                var s = GetOrCreate<EnemySpawnerConfig>($"{DataRoot}/Spawners/{p.ResolvedId}.asset", overwrite, log, out var write);
                if (write)
                {
                    s.configId = "spawner." + p.ResolvedId;
                    LegacyConfigMapping.ReadSpawner(p, s);
                    s.anchor = p.enemy != null && archetypes.TryGetValue(p.enemy.id, out var anchor) ? anchor : null;
                    EditorUtility.SetDirty(s);
                }
                spawners[p] = s;
                all.Add(s);
            }

            var difficulty = GetOrCreate<DifficultyCurveConfig>($"{DataRoot}/Difficulty/SaltwindDifficulty.asset", overwrite, log, out var writeDifficulty);
            var timeline = GetOrCreate<WaveTimelineConfig>($"{DataRoot}/Timelines/SaltwindTimeline.asset", overwrite, log, out var writeTimeline);
            if (writeDifficulty) difficulty.configId = "difficulty.saltwind";
            if (writeTimeline)
            {
                timeline.configId = "timeline.saltwind";
                BuildPhases(timeline, schedule, pacing, spawners, archetypes);
            }
            if (writeTimeline || writeDifficulty)
            {
                // ReadProfile needs the phases; when only one asset is rewritten, read into a scratch copy of the other.
                var t = writeTimeline ? timeline : Object.Instantiate(timeline);
                var d = writeDifficulty ? difficulty : Object.Instantiate(difficulty);
                LegacyConfigMapping.ReadProfile(profile, t, d);
                if (writeTimeline) ConfigureM3Phases(timeline, id => archetypes.TryGetValue(id, out var a) ? a : null);
                if (!writeTimeline) Object.DestroyImmediate(t);
                if (!writeDifficulty) Object.DestroyImmediate(d);
                EditorUtility.SetDirty(timeline);
                EditorUtility.SetDirty(difficulty);
            }
            // Link pool entries to archetypes/variants created since the timeline was generated (idempotent; values untouched).
            foreach (var entry in timeline.phases.SelectMany(p => p.pools).SelectMany(p => p.entries))
            {
                if (entry.archetype == null && archetypes.TryGetValue(entry.enemyId, out var archetype)) entry.archetype = archetype;
                if (entry.archetype == null && entry.variant == null && variants.TryGetValue(entry.enemyId, out var variant)) entry.variant = variant;
            }
            EditorUtility.SetDirty(timeline);
            all.Add(timeline);
            all.Add(difficulty);

            foreach (var w in db.weapons.Where(w => w != null))
            {
                var c = GetOrCreate<WeaponConfig>($"{DataRoot}/Weapons/{w.id}.asset", overwrite, log, out var write);
                if (write) FillWeapon(c, w, !originalWeaponIds.Contains(w.id));
                all.Add(c);
            }

            var run = GetOrCreate<RunConfig>($"{DataRoot}/Run/RunConfig.asset", overwrite, log, out var writeRun);
            if (writeRun) { run.configId = "run"; EditorUtility.SetDirty(run); }
            all.Add(run);
            var exp = GetOrCreate<ExperimentConfig>($"{DataRoot}/Run/ExperimentConfig.asset", overwrite, log, out var writeExp);
            if (writeExp) FillExperiment(exp, experiment);
            all.Add(exp);

            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(ConfigSetPath);
            if (set == null)
            {
                EnsureFolder(Path.GetDirectoryName(ConfigSetPath).Replace('\\', '/'));
                set = ScriptableObject.CreateInstance<ConfigSet>();
                AssetDatabase.CreateAsset(set, ConfigSetPath);
            }
            set.setId = "saltwind-default";
            set.configs = all.Distinct().OrderBy(c => c.configId, StringComparer.Ordinal).ToList();
            EditorUtility.SetDirty(set);

            DestroyClone(db, sourceDb);
            AssetDatabase.SaveAssets();
            var report = ConfigValidator.Validate(set);
            log.AppendLine($"config set: {set.configs.Count} configs, hash {set.ComputeHash().Substring(0, 12)}");
            log.Append(report);
            log.Append(ApplyM8World());
            return log.ToString();
        }

        // ---- Sources ------------------------------------------------------------------------------------

        /// <summary>Reloads the database and its definitions from disk, dropping in-memory catalog edits (R-73). Tests use it too: a play session leaves the loaded database catalog-mutated.</summary>
        public static GameDatabase LoadCleanDatabase()
        {
            // After a Play Mode session the Editor keeps statics (domain reload happens on entering Play Mode only), so
            // CombatExperimentContext still caches the profile Play Mode destroyed and reports it as null; the catalogs
            // then skip silently (register R-82). Re-resolve it.
            if (CombatExperimentContext.Active == null) CombatExperimentContext.ResetForTests();
            var db = GameDatabaseLoader.Load();
            // Unloading an asset does not re-resolve loaded objects that reference it, so unload the referrers too
            // (profile, schedules, spawn profiles); they reload from disk with fresh references on next access.
            var referrers = new Object[]
            {
                AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(ProfilePath),
                AssetDatabase.LoadAssetAtPath<EncounterSpawnSchedule>(SchedulePath),
                AssetDatabase.LoadAssetAtPath<EncounterSpawnSchedule>("Assets/_Game/Data/EncounterSpawnSchedule.asset"),
            }.Concat(AssetDatabase.FindAssets("t:SpawnProfile").Select(g => AssetDatabase.LoadAssetAtPath<SpawnProfile>(AssetDatabase.GUIDToAssetPath(g))));
            var candidates = db.enemies.Cast<Object>().Concat(db.weapons).Concat(referrers).Append(db).Where(o => o != null).Distinct().ToList();
            foreach (var dirty in candidates.Where(EditorUtility.IsDirty))
                Debug.LogWarning($"V3ConfigMigrator: {dirty.name} has unsaved changes and is read as-is (values may include in-memory edits).");
            var unload = candidates.Where(o => !EditorUtility.IsDirty(o)).ToList();
            foreach (var o in unload) Resources.UnloadAsset(o);
            return GameDatabaseLoader.Load();
        }

        static GameDatabase CloneDatabase(GameDatabase source)
        {
            var clone = Object.Instantiate(source);
            clone.enemies = source.enemies.Select(e => e != null ? Object.Instantiate(e) : null).ToArray();
            clone.weapons = source.weapons.Select(w => w != null ? Object.Instantiate(w) : null).ToArray();
            for (var i = 0; i < clone.enemies.Length; i++) if (clone.enemies[i] != null) clone.enemies[i].name = source.enemies[i].name;
            for (var i = 0; i < clone.weapons.Length; i++) if (clone.weapons[i] != null) clone.weapons[i].name = source.weapons[i].name;
            return clone;
        }

        static void DestroyClone(GameDatabase clone, GameDatabase source)
        {
            var keep = new HashSet<Object>(source.enemies.Cast<Object>().Concat(source.weapons).Where(o => o != null));
            foreach (var o in clone.enemies.Cast<Object>().Concat(clone.weapons).Where(o => o != null && !keep.Contains(o)))
                Object.DestroyImmediate(o);
            Object.DestroyImmediate(clone);
        }

        static IEnumerable<SpawnProfile> ScheduleProfiles(EncounterSpawnSchedule s)
        {
            var list = new List<SpawnProfile>();
            if (s.entries != null) list.AddRange(s.entries.Select(e => e.profile));
            list.AddRange(new[] { s.hammerheadBeat, s.krakenBeat, s.swarmPulse, s.drownedPursuer, s.drownedHarrier,
                s.routeTreasureRaider, s.routeCursedRingSwordfish, s.routeCursedRingCannon, s.powderCacheAmbush, s.bossSummon });
            return list.Where(p => p != null).Distinct();
        }

        // ---- Timeline -----------------------------------------------------------------------------------

        static void BuildPhases(WaveTimelineConfig t, EncounterSpawnSchedule schedule, DemoPacingSettings pacing,
            Dictionary<SpawnProfile, EnemySpawnerConfig> spawners, Dictionary<string, EnemyArchetypeConfig> archetypes)
        {
            var shares = (float[])Composition("OrdinaryShareByBand");
            var ordinary = (string[][])Composition("OrdinaryFamilies");
            var specialist = (string[][])Composition("SpecialistFamilies");
            t.phases.Clear();
            var entries = schedule.entries ?? Array.Empty<ScheduledSpawnEntry>();
            for (var i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e.profile == null) continue;
                var phase = new TimelinePhase
                {
                    phaseId = LegacyConfigMapping.BandPrefix + i,
                    label = $"Band {i}",
                    trigger = new PhaseTrigger { kind = PhaseTriggerKind.Time, timeSeconds = e.startTimeSeconds },
                    duration = new PhaseDuration { kind = PhaseDurationKind.UntilNext },
                    spawner = spawners[e.profile],
                    ratePerSecond = 1f / e.profile.timing.spawnIntervalSeconds,
                };
                var band = Math.Min(i, shares.Length - 1);
                phase.pools.Add(Pool("ordinary", shares[band], ordinary[band], archetypes));
                phase.pools.Add(Pool("specialist", 1f - shares[band], specialist[band], archetypes));
                t.phases.Add(phase);
            }

            t.phases.Add(new TimelinePhase
            {
                phaseId = LegacyConfigMapping.ReefCrab, label = "Reef Crab miniboss (timing applies from M3)",
                trigger = new PhaseTrigger { timeSeconds = pacing.firstMinibossSeconds }, pauseAmbient = true,
                duration = new PhaseDuration { kind = PhaseDurationKind.UntilCleared },
                pools = { Pool("miniboss", 1f, new[] { "reef_crab_miniboss" }, archetypes) }
            });
            AddBeat(t, LegacyConfigMapping.EliteA, "Elite Hammerhead", schedule.hammerheadBeat, spawners, pauseAmbient: true);
            AddBeat(t, LegacyConfigMapping.SwarmA, "Swarm pulse", schedule.swarmPulse, spawners, pauseAmbient: false);
            AddBeat(t, LegacyConfigMapping.SwarmB, "Swarm pulse", schedule.swarmPulse, spawners, pauseAmbient: false);
            AddBeat(t, LegacyConfigMapping.EliteB, "Elite Kraken", schedule.krakenBeat, spawners, pauseAmbient: true);
            t.phases.Add(new TimelinePhase
            {
                phaseId = LegacyConfigMapping.Boss, label = "Barnacle King", isBoss = true,
                trigger = new PhaseTrigger { kind = PhaseTriggerKind.Event, eventName = "BeaconActivated" },
                duration = new PhaseDuration { kind = PhaseDurationKind.UntilCleared },
                spawner = Spawner(schedule.bossSummon, spawners)
            });
            t.phases.Add(new TimelinePhase
            {
                phaseId = LegacyConfigMapping.PowderCache, label = "Powder Cache ambush",
                trigger = new PhaseTrigger { kind = PhaseTriggerKind.Event, eventName = "PowderCacheOpened" },
                duration = new PhaseDuration { kind = PhaseDurationKind.UntilCleared },
                spawner = Spawner(schedule.powderCacheAmbush, spawners)
            });
            t.phases.Add(new TimelinePhase
            {
                phaseId = LegacyConfigMapping.Overtime, label = "Overtime", isOvertime = true,
                trigger = new PhaseTrigger { timeSeconds = 600f }, // ReadProfile sets it to the profile's stage deadline
                spawner = Spawner(schedule.drownedPursuer, spawners), alternateSpawner = Spawner(schedule.drownedHarrier, spawners)
            });
            t.phases.Add(new TimelinePhase
            {
                phaseId = LegacyConfigMapping.Storm, label = "Storm", isStorm = true,
                trigger = new PhaseTrigger { timeSeconds = 720f },
                warning = new PhaseWarning { leadSeconds = 10f, template = "The storm closes in!" }
            });
        }

        static void AddBeat(WaveTimelineConfig t, string id, string label, SpawnProfile profile, Dictionary<SpawnProfile, EnemySpawnerConfig> spawners, bool pauseAmbient) =>
            t.phases.Add(new TimelinePhase
            {
                phaseId = id, label = label, pauseAmbient = pauseAmbient,
                duration = new PhaseDuration { kind = pauseAmbient ? PhaseDurationKind.UntilCleared : PhaseDurationKind.Seconds },
                spawner = Spawner(profile, spawners)
            });

        /// <summary>The tier V3's ambient composition unlocks an enemy by (EnemyTierLogic keeps it in a table by id).</summary>
        static ConfigEnums.EnemyTier SpawnTierOf(string enemyId) => (ConfigEnums.EnemyTier)(int)EnemyTierLogic.ResolveTierFromId(enemyId);

        /// <summary>
        /// M3: the scripted phases the V4 spawn controller runs, with V3's values: the Reef Crab miniboss
        /// (V3PressureBeatDirector + EncounterSpawner.TrySpawnMiniboss: 22 m ring, loot-stream angle, Rich, HP x4,
        /// damage x1.15, waits out the boss fight and any arena, retries until it spawns), the two scheduled elites
        /// (their beat spawner, skipped for good during the boss fight, 20 s retry window, Marks index 0/1), beat
        /// warnings at the stage's eventWarningSeconds, and V3's storm warning text.
        /// </summary>
        public static void ConfigureM3Phases(WaveTimelineConfig t, Func<string, EnemyArchetypeConfig> archetype)
        {
            if (t.Phase(LegacyConfigMapping.ReefCrab) is TimelinePhase reef)
            {
                reef.label = "Reef Crab miniboss";
                reef.pools.Clear();
                reef.pauseAmbient = true;
                reef.warning.leadSeconds = t.eventWarningSeconds;
                reef.focus = new FocusSpawn
                {
                    enabled = true, placement = FocusPlacement.RingAroundPlayer, archetype = archetype("reef_crab_miniboss"),
                    ringDistance = 22f, affix = ConfigEnums.EliteAffix.Rich, speedMultiplier = 1f, hpMultiplier = 4f, damageMultiplier = 1.15f,
                    bossConflict = BossConflict.WaitUntilBossEnds, retryWindowSeconds = 0f, scheduledEliteIndex = -1,
                    waitForOtherArena = true, arrivalToast = string.Empty,
                };
            }
            var elites = new[] { (LegacyConfigMapping.EliteA, 0), (LegacyConfigMapping.EliteB, 1) };
            foreach (var (id, index) in elites)
            {
                if (!(t.Phase(id) is TimelinePhase elite)) continue;
                elite.pauseAmbient = true;
                elite.warning.leadSeconds = t.eventWarningSeconds;
                elite.focus = new FocusSpawn
                {
                    enabled = true, placement = FocusPlacement.SpawnerProfile, bossConflict = BossConflict.SkipPermanently,
                    retryWindowSeconds = SaltwindPressureBeatLogic.SpawnRetryWindowSeconds, scheduledEliteIndex = index,
                    arrivalToast = "{label} has arrived!",
                };
            }
            foreach (var id in new[] { LegacyConfigMapping.SwarmA, LegacyConfigMapping.SwarmB })
                if (t.Phase(id) is TimelinePhase pulse) pulse.warning.leadSeconds = t.eventWarningSeconds;
            if (t.phases.FirstOrDefault(p => p.isStorm) is TimelinePhase storm)
                storm.warning.template = "DREAD TIDE — storm boundary in {lead} seconds!";
        }

        public const string EncounterOnlySchedulePath = DataRoot + "/Legacy/V4EncounterOnlySchedule.asset";

        [MenuItem("RoguePirates/V4/Apply M3 Config Changes")]
        static void ApplyM3FromMenu() => Debug.Log(ApplyM3());

        /// <summary>
        /// M3, in place and idempotent: spawn tiers on archetypes and variants, the scripted phases (see
        /// <see cref="ConfigureM3Phases"/>), tier-unlock rules persisted on the difficulty asset, and the encounter-only
        /// schedule the kept legacy spawner gets in the scene (the Resources schedule without ambient entries).
        /// </summary>
        public static string ApplyM3()
        {
            var log = new StringBuilder();
            var archetypes = new Dictionary<string, EnemyArchetypeConfig>();
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyArchetypeConfig", new[] { DataRoot + "/Enemies" }))
            {
                var a = AssetDatabase.LoadAssetAtPath<EnemyArchetypeConfig>(AssetDatabase.GUIDToAssetPath(guid));
                archetypes[a.enemyId] = a;
                a.spawnTier = SpawnTierOf(a.enemyId);
                EditorUtility.SetDirty(a);
                log.AppendLine($"{a.enemyId}: spawnTier {a.spawnTier}");
            }
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyVariantConfig", new[] { DataRoot + "/Variants" }))
            {
                var v = AssetDatabase.LoadAssetAtPath<EnemyVariantConfig>(AssetDatabase.GUIDToAssetPath(guid));
                v.spawnTier = SpawnTierOf(v.variantId);
                EditorUtility.SetDirty(v);
                log.AppendLine($"{v.variantId}: spawnTier {v.spawnTier}");
            }
            var timeline = AssetDatabase.LoadAssetAtPath<WaveTimelineConfig>($"{DataRoot}/Timelines/SaltwindTimeline.asset");
            if (timeline != null)
            {
                ConfigureM3Phases(timeline, id => archetypes.TryGetValue(id, out var a) ? a : null);
                EditorUtility.SetDirty(timeline);
                log.AppendLine("timeline: scripted phases configured");
            }
            var difficulty = AssetDatabase.LoadAssetAtPath<DifficultyCurveConfig>($"{DataRoot}/Difficulty/SaltwindDifficulty.asset");
            if (difficulty != null) { EditorUtility.SetDirty(difficulty); log.AppendLine($"difficulty: {difficulty.tierUnlocks.Count} tier-unlock rules"); }

            var source = AssetDatabase.LoadAssetAtPath<EncounterSpawnSchedule>(SchedulePath);
            if (source != null)
            {
                EnsureFolder(DataRoot + "/Legacy");
                var target = AssetDatabase.LoadAssetAtPath<EncounterSpawnSchedule>(EncounterOnlySchedulePath);
                if (target == null)
                {
                    target = ScriptableObject.CreateInstance<EncounterSpawnSchedule>();
                    AssetDatabase.CreateAsset(target, EncounterOnlySchedulePath);
                }
                EditorUtility.CopySerialized(source, target);
                target.name = "V4EncounterOnlySchedule";
                target.entries = Array.Empty<ScheduledSpawnEntry>();
                EditorUtility.SetDirty(target);
                log.AppendLine("encounter-only schedule written");
            }
            AssetDatabase.SaveAssets();
            // ApplyM3 resets variant tiers and may rebuild the focus blocks: re-apply the later decisions on top.
            log.Append(ApplyPlaytestDecisions());
            return log.ToString();
        }

        public const string WeaponRulesPath = DataRoot + "/Weapons/WeaponRules.asset";

        [MenuItem("RoguePirates/V4/Apply M5 Weapon Configs")]
        static void ApplyM5FromMenu() => Debug.Log(ApplyM5());

        /// <summary>
        /// M5, in place and idempotent: each weapon's behavior and pattern blocks (V3 WeaponController's pattern plus its
        /// weapon-id branches, with the constants it and the juice settings held), its projectile model, the six
        /// evolutions from WeaponEvolutionCatalog, and the weapon rules; all added to the config set.
        /// </summary>
        public static string ApplyM5()
        {
            var log = new StringBuilder();
            var juice = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
            var weapons = new Dictionary<string, WeaponConfig>();
            foreach (var guid in AssetDatabase.FindAssets("t:WeaponConfig", new[] { DataRoot + "/Weapons" }))
            {
                var w = AssetDatabase.LoadAssetAtPath<WeaponConfig>(AssetDatabase.GUIDToAssetPath(guid));
                weapons[w.weaponId] = w;
                FillWeaponBehavior(w, juice);
                EditorUtility.SetDirty(w);
                log.AppendLine($"{w.weaponId}: {w.behavior}{(w.lane.enabled ? " (lane)" : "")}{(w.projectile.burstShots > 1 ? $" (burst {w.projectile.burstShots})" : "")}");
            }

            EnsureFolder(DataRoot + "/Evolutions");
            var all = new List<V4ConfigAsset>();
            foreach (var def in WeaponEvolutionCatalog.Definitions)
            {
                var path = $"{DataRoot}/Evolutions/{def.evolutionId}.asset";
                var e = AssetDatabase.LoadAssetAtPath<WeaponEvolutionConfig>(path);
                if (e == null)
                {
                    e = ScriptableObject.CreateInstance<WeaponEvolutionConfig>();
                    AssetDatabase.CreateAsset(e, path);
                }
                e.configId = "evolution." + def.evolutionId;
                e.evolutionId = def.evolutionId;
                e.displayName = def.displayName;
                e.description = def.description;
                e.requiredItemId = def.requiredItemId;
                e.baseWeapon = weapons.TryGetValue(def.baseWeaponId, out var baseWeapon) ? baseWeapon : null;
                e.effect = EvolutionEffectOf(def.behaviorModifierId);
                EditorUtility.SetDirty(e);
                all.Add(e);
                log.AppendLine($"{def.evolutionId}: {e.effect} on {def.baseWeaponId}");
            }

            var rules = AssetDatabase.LoadAssetAtPath<WeaponRulesConfig>(WeaponRulesPath);
            if (rules == null)
            {
                rules = ScriptableObject.CreateInstance<WeaponRulesConfig>();
                AssetDatabase.CreateAsset(rules, WeaponRulesPath);
            }
            rules.configId = "weapon_rules";
            EditorUtility.SetDirty(rules);
            all.Add(rules);

            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(ConfigSetPath);
            if (set != null)
            {
                var added = 0;
                foreach (var c in all)
                    if (!set.configs.Contains(c)) { set.configs.Add(c); added++; }
                set.configs = set.configs.Where(c => c != null).OrderBy(c => c.configId, StringComparer.Ordinal).ToList();
                EditorUtility.SetDirty(set);
                log.AppendLine($"config set: {added} added, {set.configs.Count} configs");
            }
            AssetDatabase.SaveAssets();
            // ApplyM5 rebuilds V3's values; the fixes approved at the M5 gate and the M6 effect data go back on top.
            log.Append(ApplyM5PlaytestFixes());
            log.Append(ApplyM6Visuals());
            if (set != null) log.Append(ConfigValidator.Validate(set));
            return log.ToString();
        }

        [MenuItem("RoguePirates/V4/Apply M5 Playtest Fixes")]
        static void ApplyM5PlaytestFixesFromMenu() => Debug.Log(ApplyM5PlaytestFixes());

        /// <summary>
        /// Fixes approved at the M5 gate, in place and idempotent (the v3-parity preset turns each back off): the level
        /// factor on every weapon (R-21), Broadside Quantity (R-22), chain lightning (R-19), the Stormshot lane arc (R-18),
        /// Swivel's burst gap (R-87), the mortar ring and no effect draws (R-27, R-70), item attack speed once, cooldown
        /// overshoot carried and Roxie's salvo (R-22, R-88 in the weapon rules).
        /// </summary>
        public static string ApplyM5PlaytestFixes()
        {
            var log = new StringBuilder();
            foreach (var guid in AssetDatabase.FindAssets("t:WeaponConfig", new[] { DataRoot + "/Weapons" }))
            {
                var w = AssetDatabase.LoadAssetAtPath<WeaponConfig>(AssetDatabase.GUIDToAssetPath(guid));
                w.levelDamageFactor = true;
                w.projectile.v3FullCooldownAfterFirstShot = false;
                w.predictedArea.v3ImpactEffectDraws = false;
                w.predictedArea.telegraphMatchesImpact = true;
                w.areaStrike.chain = true;
                if (w.behavior == ConfigEnums.WeaponBehavior.Fan) w.fan.quantityAddsPellets = true;
                EditorUtility.SetDirty(w);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:WeaponEvolutionConfig", new[] { DataRoot + "/Evolutions" }))
            {
                var e = AssetDatabase.LoadAssetAtPath<WeaponEvolutionConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (e.laneBurst.laneArcDegrees <= 0f) e.laneBurst.laneArcDegrees = 40f;
                EditorUtility.SetDirty(e);
            }
            var rules = AssetDatabase.LoadAssetAtPath<WeaponRulesConfig>(WeaponRulesPath);
            if (rules != null)
            {
                rules.itemAttackSpeedAppliedOnce = true;
                rules.carryCooldownOvershoot = true;
                rules.salvoUsesLightningWeapon = true;
                rules.salvoLevelFactor = 1f;
                EditorUtility.SetDirty(rules);
            }
            AssetDatabase.SaveAssets();
            log.AppendLine("M5 playtest fixes applied (R-18, R-19, R-21, R-22, R-27, R-70, R-87, R-88)");
            return log.ToString();
        }

        [MenuItem("RoguePirates/V4/Apply M6 Weapon Visuals")]
        static void ApplyM6VisualsFromMenu() => Debug.Log(ApplyM6Visuals());

        /// <summary>
        /// M6: the effect choices V3's juice controller and aim overlay made by weapon id, written as weapon and rule data
        /// so the V4 presenters never compare ids. Idempotent. Values are V3's: cannon muzzle for the cannon weapons,
        /// Broadside's 1.12 fallback muzzle scale, the overlay shapes (Mortar predicted area with a 4 m minimum, Swivel
        /// target fan, Long Nine forward fan, Lightning Mast strike disc with a fading 0.55 s flash), the gold cutlass
        /// ring, the Cannonboat model for boarding skiffs, and the Powder Kegs / Mortar bindings for blasts and impacts.
        /// </summary>
        public static string ApplyM6Visuals()
        {
            var cannonMuzzle = new HashSet<string> { "long_nine", "deck_cannons", "broadside_battery", "swivel_guns", "grapeshot", "mortar" };
            var db = GameDatabaseLoader.Load();
            var skiff = db != null ? db.GetEnemy("cannonboat") : null;
            var count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:WeaponConfig", new[] { DataRoot + "/Weapons" }))
            {
                var w = AssetDatabase.LoadAssetAtPath<WeaponConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (w == null) continue;
                var v = w.visual ?? (w.visual = new WeaponEffectsVisual());
                v.cannonMuzzle = cannonMuzzle.Contains(w.weaponId);
                v.fallbackMuzzleScale = w.weaponId == "broadside_battery" ? 1.12f : 1f;
                v.aimShape = w.weaponId switch
                {
                    "mortar" => ConfigEnums.AimShape.PredictedArea,
                    "swivel_guns" => ConfigEnums.AimShape.TargetFan,
                    "long_nine" => ConfigEnums.AimShape.ForwardFan,
                    "lightning_mast" => ConfigEnums.AimShape.StrikeDisc,
                    _ => ConfigEnums.AimShape.ByTargeting
                };
                v.aimTargetFanDegrees = 22f;
                v.aimMinAreaRadius = w.weaponId == "mortar" ? 4f : 2f;
                v.firePulseSeconds = w.weaponId == "lightning_mast" ? 0.55f : 0.18f;
                v.firePulseFades = w.weaponId == "lightning_mast";
                v.orbitRingTinted = w.weaponId == "flying_cutlasses";
                v.orbitRingColor = new Color(1f, 0.88f, 0.28f, 1f);
                v.summonModel = w.behavior == ConfigEnums.WeaponBehavior.Summon && skiff != null ? skiff.visualPrefab : null;
                v.summonModelScale = 0.42f;
                EditorUtility.SetDirty(w);
                count++;
            }
            var rules = AssetDatabase.LoadAssetAtPath<WeaponRulesConfig>(WeaponRulesPath);
            if (rules != null)
            {
                rules.blastVfxBinding = "powder_kegs";
                rules.impactVfxBinding = "mortar";
                EditorUtility.SetDirty(rules);
            }
            AssetDatabase.SaveAssets();
            return $"M6 weapon visuals written for {count} weapons" + (skiff == null || skiff.visualPrefab == null ? " (no Cannonboat model found for skiffs)" : string.Empty) + "\n";
        }

        [MenuItem("RoguePirates/V4/Apply M8 Enemy World Rules")]
        static void ApplyM8WorldFromMenu() => Debug.Log(ApplyM8World());

        /// <summary>
        /// M8: the per-enemy rules the V3 enemy world hard-coded by enemy id, as archetype and variant data (V3 matched
        /// the exact id, so each id's rules sit on its own asset). Idempotent; everything else is reset to "no rule".
        /// </summary>
        public static string ApplyM8World()
        {
            var log = new StringBuilder();
            var rules = new Dictionary<string, ArchetypeWorldRules>();
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyArchetypeConfig", new[] { DataRoot + "/Enemies" }))
            {
                var a = AssetDatabase.LoadAssetAtPath<EnemyArchetypeConfig>(AssetDatabase.GUIDToAssetPath(guid));
                a.world = new ArchetypeWorldRules();
                rules[a.enemyId] = a.world;
                EditorUtility.SetDirty(a);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyVariantConfig", new[] { DataRoot + "/Variants" }))
            {
                var v = AssetDatabase.LoadAssetAtPath<EnemyVariantConfig>(AssetDatabase.GUIDToAssetPath(guid));
                v.world = new ArchetypeWorldRules();
                rules[v.variantId] = v.world;
                EditorUtility.SetDirty(v);
            }

            ArchetypeWorldRules Rules(string id)
            {
                if (rules.TryGetValue(id, out var r)) { log.AppendLine($"{id}: world rules"); return r; }
                log.AppendLine($"WARNING {id}: no archetype or variant");
                return new ArchetypeWorldRules();
            }
            // EnemyWorld.Spawn (initial charge timer, initial attack cooldown), UpdateView and TickBehavior (swordfish).
            Rules("swordfish").initialChargeTimer = new FloatOverride { enabled = true, value = 0f };
            Rules("swordfish").swordfishSpringDash = true;
            Rules("cannonboat").initialAttackCooldown = new FloatOverride { enabled = true, value = 1.1f };
            Rules("drowned_harrier").initialAttackCooldown = new FloatOverride { enabled = true, value = 0.9f };
            // SaltwindCombatAgentStateStore.GetOrCreate.
            Rules("powder_barge").initialCombatPhaseTimer = 2f;
            // SaltwindCombatBehaviorSystem / SaltwindSpecialistCombat repair targets.
            Rules("repair_tender").excludedFromRepair = true;
            // SaltwindCombatBehaviorSystem.TryTickMovement (R-100).
            Rules("hammerhead").eliteLatchesSecondChargeForAll = true;
            // EnemyRangedAttackRules.SupportsRangedAttacks.
            foreach (var id in new[] { "cannonboat", "gunner_cog", "bomb_boat", "manta_ray", "drowned_harrier" }) Rules(id).alwaysRanged = true;
            // EnemyWorld.TickRangedAttack / GetAttackRecovery / GetHostileShotProfile (enemies without a combat profile).
            Rules("reef_crab_miniboss").noProfilelessRangedAttack = true;
            Rules("cannonboat").rangedMinRangeFactor = new FloatOverride { enabled = true, value = 0.8f };
            Rules("cannonboat").rangedMaxRangeFactor = new FloatOverride { enabled = true, value = 1.15f };
            Rules("cannonboat").rangedRecovery = new FloatOverride { enabled = true, value = 2f };
            Rules("manta_ray").rangedFlankShots = true;
            void Shot(string id, float speed, float life, float hit, float visual)
            {
                var r = Rules(id);
                r.ownShotProfile = true;
                r.shotSpeed = speed; r.shotLife = life; r.shotHitRadius = hit; r.shotVisualRadius = visual;
            }
            Shot("drowned_harrier", 10f, 1.45f, 0.24f, 0.14f);
            Shot("cannonboat", 9f, 1.55f, 0.38f, 0.16f);
            // EnemyWorld.ApplyDamage: no damage highlight for Powder Kegs hits.
            foreach (var guid in AssetDatabase.FindAssets("t:WeaponConfig", new[] { DataRoot + "/Weapons" }))
            {
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (weapon.visual == null) continue;
                weapon.visual.noHitHighlight = weapon.weaponId == "powder_kegs";
                EditorUtility.SetDirty(weapon);
            }
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        public const string StageContentPath = DataRoot + "/Stage/StageContent_Saltwind.asset";

        [MenuItem("RoguePirates/V4/Apply M7 Stage Content")]
        static void ApplyM7FromMenu() => Debug.Log(ApplyM7());

        /// <summary>
        /// M7: the stage content config from the V3 stage profile (placement counts, chest prices, landmark values, Beacon
        /// hint times) and the constants POIManager, V3DiscoverySystem and V3InteractionController kept, with the boss and
        /// Powder Cache spawners; plus the Barnacle King's boss config. Both join the config set. Idempotent. The adopted
        /// decision "no explosive barrels" is the one V4 default that differs from V3 (the v3-parity preset restores 2).
        /// </summary>
        public static string ApplyM7()
        {
            var log = new StringBuilder();
            var profile = AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(ProfilePath);
            var pacing = AssetDatabase.LoadAssetAtPath<DemoPacingSettings>(PacingPath);
            if (!AssetDatabase.IsValidFolder(DataRoot + "/Stage")) AssetDatabase.CreateFolder(DataRoot, "Stage");
            var stage = AssetDatabase.LoadAssetAtPath<StageContentConfig>(StageContentPath);
            if (stage == null)
            {
                stage = ScriptableObject.CreateInstance<StageContentConfig>();
                AssetDatabase.CreateAsset(stage, StageContentPath);
            }
            stage.configId = "stage.saltwind";
            if (profile != null)
            {
                stage.salvageChestCount = profile.salvageChestCount;
                stage.explosiveBarrelFuseSeconds = profile.explosiveBarrelFuseSeconds;
                stage.explosiveBarrelRadius = profile.explosiveBarrelRadius;
                stage.explosiveBarrelDamage = profile.explosiveBarrelDamage;
                stage.chestBasePrice = profile.chestBasePrice;
                stage.chestPriceGrowth = profile.chestPriceGrowth;
                stage.chestPriceIncrementBase = profile.chestPriceIncrementBase;
                stage.disableGoldTriggeredChestSpawn = profile.disableGoldTriggeredChestSpawn;
                stage.disableAutomaticRouteEvents = profile.disableAutomaticRouteEvents;
                stage.repairWreckMaxHullFraction = profile.repairWreckMaxHullFraction;
                stage.powderCacheBonusGold = profile.powderCacheBonusGold;
                stage.signalBuoyChargeSeconds = profile.signalBuoyOccupancySeconds;
                stage.beaconDirectionHintSeconds = profile.beaconDirectionHintSeconds;
                stage.beaconExactMarkerSeconds = profile.beaconExactMarkerSeconds;
            }
            stage.explosiveBarrelCount = 0; // adopted decision (V3 profile: 2)
            stage.chestSpawnGoldFraction = GameConstants.V3ChestSpawnGoldFraction;
            stage.chestInteractRange = 6f;
            stage.chestPlanUsesLoadout = false;
            stage.interactRange = HighSeas.V3.V3InteractionController.DefaultRange;
            stage.repairWreckHealFraction = 0.25f;
            stage.powderCacheAmbushCount = 3;
            stage.powderCacheTriggerRadius = 8f;
            stage.signalBuoyRadius = 4f;
            stage.signalBuoyRewards = new[]
            {
                new BuoyReward { stat = (int)StatId.Damage, multiply = 1.1f, label = "+10% damage for this run" },
                new BuoyReward { stat = (int)StatId.MoveSpeed, add = 0.5f, label = "+0.5 move speed for this run" },
                new BuoyReward { stat = (int)StatId.PickupRadius, add = 0.8f, label = "+0.8 pickup radius for this run" },
            };
            stage.treasureChartRevealCount = 3;
            stage.lighthouseDamageMultiplier = 1.08f;
            stage.discoveryRadius = HighSeas.V3.V3DiscoverySystem.DiscoveryRadius;
            stage.exploreCellSize = HighSeas.V3.V3DiscoverySystem.ExploreCellSize;
            stage.exploreRadius = 10f;
            stage.beaconInteractRange = 6f;
            stage.beaconHoldSeconds = HighSeas.V3.V3InteractionController.BossHoldSeconds;
            stage.bossTelegraphSeconds = pacing != null ? pacing.bossTelegraphSeconds : 0.7f;
            stage.beaconFoundByProximityAfterHints = false;
            stage.beaconExactMarkerAfterHint = false;
            stage.continueAfterBossVictory = true;
            stage.bossSpawner = FindSpawner("spawner.saltwind_boss");
            stage.powderCacheSpawner = FindSpawner("spawner.saltwind_powder_cache");
            EditorUtility.SetDirty(stage);
            log.AppendLine($"stage content: {StageContentPath} (boss spawner {(stage.bossSpawner != null ? "ok" : "MISSING")}, powder cache spawner {(stage.powderCacheSpawner != null ? "ok" : "MISSING")})");

            log.AppendLine(V4BossConfigGenerator.Generate());
            var boss = AssetDatabase.LoadAssetAtPath<BossConfig>(V4BossConfigGenerator.AssetPath);

            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(ConfigSetPath);
            if (set != null)
            {
                foreach (var c in new V4ConfigAsset[] { stage, boss })
                    if (c != null && !set.configs.Contains(c)) set.configs.Add(c);
                set.configs = set.configs.Where(c => c != null).OrderBy(c => c.configId, StringComparer.Ordinal).ToList();
                EditorUtility.SetDirty(set);
                log.AppendLine($"config set: {set.configs.Count} configs");
            }
            AssetDatabase.SaveAssets();
            // ApplyM7 rebuilds V3's values; the fixes approved at the M7 gate go back on top.
            log.Append(ApplyM7PlaytestDecisions());
            if (set != null) log.Append(ConfigValidator.Validate(set));
            return log.ToString();
        }

        [MenuItem("RoguePirates/V4/Apply M7 Playtest Decisions")]
        static void ApplyM7PlaytestDecisionsFromMenu() => Debug.Log(ApplyM7PlaytestDecisions());

        /// <summary>
        /// The fixes approved at the M7 playtest gate (the v3-parity preset turns them off again): R-47 chest loot knows the
        /// loadout, R-93/R-94 Beacon discovery after the hints, R-96 skipped chests offered again and the Beacon retry after
        /// a failed summon, R-97 Powder Cache payout, R-101 the prompt lets go of a used landmark.
        /// </summary>
        public static string ApplyM7PlaytestDecisions()
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageContentConfig>(StageContentPath);
            if (stage == null) return "stage content config missing\n";
            stage.chestPlanUsesLoadout = true;
            stage.beaconFoundByProximityAfterHints = true;
            stage.beaconExactMarkerAfterHint = true;
            stage.forgetChestSkipWhenLeaving = true;
            stage.beaconRetryAfterFailedSummon = true;
            stage.powderCachePaysWhenCleared = true;
            stage.promptReleasesUsedLandmark = true;
            stage.bossFightSurvivesLevelUp = true; // R-102
            EditorUtility.SetDirty(stage);
            // A more noticeable Barnacle King charge (V3: 2.6x speed for 0.4 s, about 2.6 m): 6x for 0.9 s, about 13.5 m.
            var boss = AssetDatabase.LoadAssetAtPath<BossConfig>(V4BossConfigGenerator.AssetPath);
            if (boss != null)
            {
                boss.charge.speedMultiplier = 6f;
                boss.charge.executeSeconds = 0.9f;
                EditorUtility.SetDirty(boss);
            }
            AssetDatabase.SaveAssets();
            return "M7 playtest decisions applied (R-47, R-93, R-94, R-96, R-97, R-101, R-102, boss charge)\n";
        }

        static EnemySpawnerConfig FindSpawner(string configId)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:EnemySpawnerConfig", new[] { DataRoot + "/Spawners" }))
            {
                var spawner = AssetDatabase.LoadAssetAtPath<EnemySpawnerConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (spawner != null && spawner.configId == configId) return spawner;
            }
            return null;
        }

        static ConfigEnums.EvolutionEffect EvolutionEffectOf(string modifierId) => modifierId switch
        {
            "stormshot_cannon" => ConfigEnums.EvolutionEffect.LaneWithBurst,
            "kings_broadside" => ConfigEnums.EvolutionEffect.OppositeSideFollowUp,
            "devils_wake" => ConfigEnums.EvolutionEffect.WakeKegs,
            "bladed_tide" => ConfigEnums.EvolutionEffect.PulsingOrbit,
            "hellfire_nozzle" => ConfigEnums.EvolutionEffect.WideConeWithPatches,
            "tempest_mast" => ConfigEnums.EvolutionEffect.JumpAndDelayedStrike,
            _ => ConfigEnums.EvolutionEffect.None,
        };

        /// <summary>
        /// The behavior and blocks V3 WeaponController gave each weapon through its pattern and weapon-id branches. Every
        /// value is V3's (WeaponController constants, the juice settings for Swivel Guns, the Long Nine beam and pellet
        /// lifetimes, ProjectileWorld's homing list and projectile models).
        /// </summary>
        static void FillWeaponBehavior(WeaponConfig w, DemoJuiceSettings juice)
        {
            var id = w.weaponId;
            w.projectile = new ProjectileBlock();
            w.lane = new LaneBlock();
            w.fan = new FanBlock();
            w.orbit = new OrbitBlock();
            w.stream = new StreamBlock();
            w.trail = new TrailBlock();
            w.mine = new MineBlock();
            w.areaStrike = new AreaStrikeBlock();
            w.aura = new AuraBlock();
            w.predictedArea = new PredictedAreaBlock();
            w.summon = new SummonBlock();
            w.levelDamageFactor = false;

            switch (w.pattern)
            {
                case ConfigEnums.WeaponPattern.AutoProjectile:
                    w.behavior = ConfigEnums.WeaponBehavior.Projectile;
                    w.levelDamageFactor = true; // V3 applies the level factor to projectiles and lanes only (R-21)
                    break;
                case ConfigEnums.WeaponPattern.FanVolley: w.behavior = ConfigEnums.WeaponBehavior.Fan; break;
                case ConfigEnums.WeaponPattern.Orbit: w.behavior = ConfigEnums.WeaponBehavior.Orbit; break;
                case ConfigEnums.WeaponPattern.TrailZone:
                    w.behavior = id == "flamethrower" || id == "burning_oil" ? ConfigEnums.WeaponBehavior.ConeStream : ConfigEnums.WeaponBehavior.Trail;
                    break;
                case ConfigEnums.WeaponPattern.MineDrop: w.behavior = ConfigEnums.WeaponBehavior.Mine; break;
                case ConfigEnums.WeaponPattern.ChainLightning: w.behavior = ConfigEnums.WeaponBehavior.AreaStrike; break;
                case ConfigEnums.WeaponPattern.AuraPulse: w.behavior = ConfigEnums.WeaponBehavior.Aura; break;
                case ConfigEnums.WeaponPattern.PredictedAoE: w.behavior = ConfigEnums.WeaponBehavior.PredictedArea; break;
                case ConfigEnums.WeaponPattern.SummonChase: w.behavior = ConfigEnums.WeaponBehavior.Summon; break;
            }

            // ProjectileWorld.ShouldApplyHoming.
            w.projectile.homing = id == "swivel_guns" || id == "deck_cannons" || id == "long_nine";
            if (id == "swivel_guns")
            {
                // FireSwivelShot / BeginSwivelBurst (values from the juice settings, V3's defaults otherwise).
                w.projectile.burstShots = 2;
                w.projectile.burstGapSeconds = juice != null ? juice.swivelBetweenShotDelay : 0.18f;
                w.projectile.damageMultiplier = juice != null ? juice.swivelDamageMult : 0.55f;
                w.projectile.sizeMultiplier = juice != null ? juice.swivelProjectileSizeMult : 0.55f;
                w.projectile.minSize = 0.32f;
                w.projectile.lifeSeconds = 2.4f;
                w.projectile.leadWithActualSpeed = false;
                w.projectile.closeDefenseTargeting = true;
            }
            if (id == "long_nine")
            {
                w.lane.enabled = true;
                w.lane.beam = true;
                w.lane.beamHalfWidth = juice != null ? juice.longNineBeamHalfWidth : 0.32f;
            }
            if (id == "harpoon_gun") w.projectile.tarOnHit = true;
            if (id == "broadside_battery")
            {
                w.fan.quantityAddsPellets = false;
                w.fan.minSpeed = 42f;
                w.fan.hitRadius = 1.05f;
                var life = 1.2f;
                if (juice != null && juice.TryGetWeaponVfx(id, out var binding) && binding.projectileLifeMult > 0f) life *= binding.projectileLifeMult;
                w.fan.lifeSeconds = life;
                w.fan.muzzlePerPellet = false;
                w.fan.trackPelletTargets = false;
                w.fan.splashOnDespawn = false;
            }

            // ProjectileWorld.ResolveProjectileVisual.
            w.projectileVisual = new ProjectileVisual();
            if (juice != null)
            {
                if (juice.TryGetWeaponVfx(id, out var vfx) && vfx.projectilePrefab != null)
                {
                    w.projectileVisual.prefab = vfx.projectilePrefab;
                    w.projectileVisual.scaleMultiplier = vfx.projectileScaleMult;
                }
                else if (id == "swivel_guns")
                {
                    w.projectileVisual.prefab = juice.smallProjectilePrefab != null ? juice.smallProjectilePrefab : juice.projectilePrefab;
                    w.projectileVisual.scaleMultiplier = juice.swivelProjectileSizeMult;
                    w.projectileVisual.radiusMultiplier = 0.85f;
                }
                else if (id == "harpoon_gun" && juice.harpoonProjectilePrefab != null)
                {
                    w.projectileVisual.prefab = juice.harpoonProjectilePrefab;
                    w.projectileVisual.scaleMultiplier = juice.harpoonProjectileScale;
                }
            }
        }

        public const string PoolCatalogPath = DataRoot + "/Pools/SaltwindPools.asset";
        public const string PoolStressMarkerPath = DataRoot + "/Pools/PoolStressMarker.prefab";

        [MenuItem("RoguePirates/V4/Apply M4 Pool Catalog")]
        static void ApplyM4FromMenu() => Debug.Log(ApplyM4());

        /// <summary>
        /// M4, idempotent: the pool catalog asset (derived enemy view pools on, not prewarmed until M8; two Module Lab
        /// stress pools on a collider-free marker prefab) added to the config set.
        /// </summary>
        public static string ApplyM4()
        {
            var log = new StringBuilder();
            EnsureFolder(DataRoot + "/Pools");
            var marker = AssetDatabase.LoadAssetAtPath<GameObject>(PoolStressMarkerPath);
            if (marker == null)
            {
                var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(temp.GetComponent<Collider>());
                temp.name = "PoolStressMarker";
                temp.transform.localScale = Vector3.one * 0.6f;
                marker = PrefabUtility.SaveAsPrefabAsset(temp, PoolStressMarkerPath);
                Object.DestroyImmediate(temp);
                log.AppendLine("stress marker prefab created");
            }

            var catalog = AssetDatabase.LoadAssetAtPath<PoolCatalogConfig>(PoolCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<PoolCatalogConfig>();
                AssetDatabase.CreateAsset(catalog, PoolCatalogPath);
                log.AppendLine("pool catalog created");
            }
            catalog.configId = "pools.saltwind";
            catalog.entries = new List<PoolCatalogEntry>
            {
                new PoolCatalogEntry { key = "lab/stress_recycle", prefab = marker, prewarm = 48, cap = 48, overflow = ConfigEnums.PoolOverflowPolicy.RecycleOldest, moduleLabOnly = true },
                new PoolCatalogEntry { key = "lab/stress_grow", prefab = marker, prewarm = 16, cap = 16, overflow = ConfigEnums.PoolOverflowPolicy.Grow, moduleLabOnly = true },
            };
            EditorUtility.SetDirty(catalog);

            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(ConfigSetPath);
            if (set != null && !set.configs.Contains(catalog))
            {
                set.configs.Add(catalog);
                set.configs = set.configs.Where(c => c != null).OrderBy(c => c.configId, StringComparer.Ordinal).ToList();
                EditorUtility.SetDirty(set);
                log.AppendLine("added to the config set");
            }
            AssetDatabase.SaveAssets();
            if (set != null) log.Append(ConfigValidator.Validate(set));
            return log.ToString();
        }

        [MenuItem("RoguePirates/V4/Apply M1-M3 Playtest Decisions")]
        static void ApplyPlaytestDecisionsFromMenu() => Debug.Log(ApplyPlaytestDecisions());

        /// <summary>
        /// Decisions from the combined M1–M3 playtest, in place and idempotent:
        /// R-03 the scheduled elite beats spawn their elite variant, flagged elite (the beat spawners' elite block);
        /// R-05 overtime spawners anchor the Drowned archetypes, which get placeholder models (the proxy ships V3's
        /// fallback resolver uses, marked placeholderModel for future art). The v3-parity preset reverts all of it.
        /// </summary>
        public static string ApplyPlaytestDecisions()
        {
            var log = new StringBuilder();
            T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
            var timeline = Load<WaveTimelineConfig>($"{DataRoot}/Timelines/SaltwindTimeline.asset");
            foreach (var (phaseId, spawnerFile, variantFile) in new[]
                     {
                         (LegacyConfigMapping.EliteA, "saltwind_elite_hammerhead", "elite_hammerhead"),
                         (LegacyConfigMapping.EliteB, "saltwind_elite_kraken", "elite_kraken_spawn"),
                     })
            {
                var spawner = Load<EnemySpawnerConfig>($"{DataRoot}/Spawners/{spawnerFile}.asset");
                var variant = Load<EnemyVariantConfig>($"{DataRoot}/Variants/{variantFile}.asset");
                if (spawner == null || variant == null || timeline?.Phase(phaseId) == null) { log.AppendLine($"missing {phaseId} data"); continue; }
                spawner.elite.isElite = true;
                EditorUtility.SetDirty(spawner);
                timeline.Phase(phaseId).focus.variant = variant;
                // The V3 beat kraken counted toward the area-denial cap as a kraken_spawn; keep that for the elite.
                var baseArchetype = variant.baseArchetype;
                if (baseArchetype != null && baseArchetype.concurrencyTags != null) variant.concurrencyTags = (string[])baseArchetype.concurrencyTags.Clone();
                if (baseArchetype != null) variant.spawnTier = baseArchetype.spawnTier;
                // V3's elite registry entries never set contact damage (default on), unlike their base enemies; the
                // elite profiles had no reader in V3, so keep the base enemy's rule (the plain beat Kraken never rams).
                if (baseArchetype != null && variant.combat != null && baseArchetype.combat != null)
                    variant.combat.contactDamageEnabled = baseArchetype.combat.contactDamageEnabled;
                EditorUtility.SetDirty(variant);
                log.AppendLine($"{phaseId}: spawns {variant.variantId}, spawner flagged elite");
            }
            if (timeline != null) EditorUtility.SetDirty(timeline);

            // Base HP and damage are the enemies V3's overtime actually spawned (shark, cannonboat); the timeline's overtime
            // spawn ramp (1x at the deadline to 2x five minutes later) makes them progressively stronger.
            foreach (var (spawnerFile, enemy, model, scale, statsFrom) in new[]
                     {
                         ("drowned_pursuer", "drowned_pursuer", "Assets/GameObject/SmuggleBoat.prefab", 1.05f, "shark"),
                         ("drowned_harrier", "drowned_harrier", "Assets/GameObject/CannonboatNavy.prefab", 0.95f, "cannonboat"),
                     })
            {
                var spawner = Load<EnemySpawnerConfig>($"{DataRoot}/Spawners/{spawnerFile}.asset");
                var archetype = Load<EnemyArchetypeConfig>($"{DataRoot}/Enemies/{enemy}.asset");
                var source = Load<EnemyArchetypeConfig>($"{DataRoot}/Enemies/{statsFrom}.asset");
                if (spawner == null || archetype == null || source == null) { log.AppendLine($"missing {enemy} data"); continue; }
                archetype.stats.baseHP = source.stats.baseHP;
                archetype.stats.baseDamage = source.stats.baseDamage;
                spawner.anchor = archetype;
                EditorUtility.SetDirty(spawner);
                archetype.visual.visualPrefab = Load<GameObject>(model);
                archetype.visual.visualScale = scale;
                archetype.visual.placeholderModel = true;
                EditorUtility.SetDirty(archetype);
                log.AppendLine($"{spawnerFile}: anchors {enemy} (placeholder model {System.IO.Path.GetFileNameWithoutExtension(model)}, HP {archetype.stats.baseHP} damage {archetype.stats.baseDamage} from {statsFrom})");
            }
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        static EnemySpawnerConfig Spawner(SpawnProfile p, Dictionary<SpawnProfile, EnemySpawnerConfig> spawners) =>
            p != null && spawners.TryGetValue(p, out var s) ? s : null;

        static SpawnPool Pool(string id, float share, IEnumerable<string> ids, Dictionary<string, EnemyArchetypeConfig> archetypes)
        {
            var pool = new SpawnPool { poolId = id, share = share };
            foreach (var enemyId in ids)
                pool.entries.Add(new PoolEntry { enemyId = enemyId, archetype = archetypes.TryGetValue(enemyId, out var a) ? a : null, weight = 1f });
            return pool;
        }

        static object Composition(string field) =>
            typeof(SaltwindAmbientCompositionLogic).GetField(field, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(null)
            ?? throw new MissingFieldException(nameof(SaltwindAmbientCompositionLogic), field);

        // ---- Archetypes, weapons, experiment ------------------------------------------------------------

        static void FillArchetype(EnemyArchetypeConfig a, EnemyDefinition d)
        {
            a.configId = "enemy." + d.id;
            a.enemyId = d.id;
            a.displayName = d.displayName;
            a.category = (ConfigEnums.EnemyCategory)(int)d.category;
            a.tier = (ConfigEnums.EnemyTier)(int)d.tier;
            a.spawnTier = SpawnTierOf(d.id);
            a.behavior = (ConfigEnums.EnemyBehavior)(int)d.behavior;
            a.biomeTag = d.biomeTag;
            a.stats = new ArchetypeStats { baseHP = d.baseHP, baseDamage = d.baseDamage, moveSpeed = d.moveSpeed, radius = d.radius, threatCost = d.threatCost, attackCooldown = d.attackCooldown, preferredRange = d.preferredRange };
            a.rewards = new ArchetypeRewards { xpValue = d.xpValue, goldValue = d.goldValue };
            a.visual = new ArchetypeVisual { visualPrefab = d.visualPrefab, visualScale = d.visualScale, visualYawOffset = d.visualYawOffset, visualHeightOffset = d.visualHeightOffset, placeholderColor = d.placeholderColor };
            a.tags = d.tags != null ? (string[])d.tags.Clone() : Array.Empty<string>();
            a.concurrencyTags = LegacyConfigMapping.LegacyConcurrencyTags.TryGetValue(d.id, out var concurrency) ? (string[])concurrency.Clone() : Array.Empty<string>();
            a.isBoss = d.isBoss;
            a.isMiniboss = d.isMiniboss;
            a.combat = new ArchetypeCombat();
            if (EnemyCombatProfileRegistry.TryGet(d.id, out var p))
            {
                LegacyConfigMapping.CopyFields(p, a.combat);
                a.combat.registered = true;
            }
            EditorUtility.SetDirty(a);
        }

        /// <summary>V3's fallback enemy list (GameDatabaseLoader.CreateEnemies), for enemies only it defines.</summary>
        static EnemyDefinition[] FallbackEnemies() =>
            (EnemyDefinition[])typeof(GameDatabaseLoader).GetMethod("CreateEnemies", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

        static void FillAliasVariant(EnemyVariantConfig v, EnemyDefinition alias, EnemyArchetypeConfig baseArchetype)
        {
            v.configId = "variant." + alias.id;
            v.variantId = alias.id;
            v.kind = VariantKind.DatabaseAlias;
            v.baseArchetype = baseArchetype;
            v.displayName = alias.displayName;
            v.behavior = new BehaviorOverride { enabled = true, value = (ConfigEnums.EnemyBehavior)(int)alias.behavior };
            v.threatCost = new IntOverride { enabled = true, value = alias.threatCost };
            v.baseHP = new FloatOverride { enabled = true, value = alias.baseHP };
            v.moveSpeed = new FloatOverride { enabled = true, value = alias.moveSpeed };
            v.baseDamage = new FloatOverride();
            v.tags = alias.tags != null ? (string[])alias.tags.Clone() : Array.Empty<string>();
            v.spawnTier = SpawnTierOf(alias.id);
            v.inheritBaseVisual = true; // R-13 decision: template model as a stand-in until specialists get their own models (V3: none)
            v.concurrencyTags = LegacyConfigMapping.LegacyConcurrencyTags.TryGetValue(alias.id, out var tags) ? (string[])tags.Clone() : Array.Empty<string>();
            v.combat = new ArchetypeCombat();
            if (EnemyCombatProfileRegistry.TryGet(alias.id, out var profile))
            {
                LegacyConfigMapping.CopyFields(profile, v.combat);
                v.combat.registered = true;
            }
            EditorUtility.SetDirty(v);
        }

        static void FillWeapon(WeaponConfig c, WeaponDefinition w, bool runtimeCatalogWeapon)
        {
            c.configId = "weapon." + w.id;
            c.weaponId = w.id;
            c.displayName = w.displayName;
            c.pattern = (ConfigEnums.WeaponPattern)(int)w.pattern;
            c.targeting = (ConfigEnums.TargetingMode)(int)w.targeting;
            c.tags = w.tags != null ? (string[])w.tags.Clone() : Array.Empty<string>();
            c.baseDamage = w.baseDamage; c.baseCooldown = w.baseCooldown; c.range = w.range; c.baseQuantity = w.baseQuantity;
            c.projectileSpeed = w.projectileSpeed; c.duration = w.duration; c.size = w.size; c.knockback = w.knockback;
            c.fanSpreadDegrees = w.fanSpreadDegrees; c.targetArcDegrees = w.targetArcDegrees;
            c.preferElite = w.preferElite; c.preferHighestHull = w.preferHighestHull; c.maxLevel = w.maxLevel;
            c.validUpgradeAxes = (w.validUpgradeAxes ?? Array.Empty<UpgradeAxis>()).Select(x => (ConfigEnums.UpgradeAxis)(int)x).ToArray();
            c.upgradeAxes = (w.upgradeAxisConfigs ?? Array.Empty<WeaponUpgradeAxisConfig>()).Select(x => new WeaponUpgradeAxis
            {
                axis = (ConfigEnums.UpgradeAxis)(int)x.axis, baseIncrement = x.baseIncrement, offerWeight = x.offerWeight,
                maxAccumulated = x.maxAccumulated, displayFormat = x.displayFormat ?? string.Empty
            }).ToArray();
            c.procCoefficient = w.procCoefficient;
            c.projectileColor = w.projectileColor;
            c.runtimeCatalogWeapon = runtimeCatalogWeapon;
            EditorUtility.SetDirty(c);
        }

        static void FillExperiment(ExperimentConfig e, CombatExperimentProfile p)
        {
            e.configId = "experiment";
            e.legacyProfileId = p.profileId;
            LegacyConfigMapping.CopyFields(p, e);
            EditorUtility.SetDirty(e);
        }

        // ---- Asset helpers ------------------------------------------------------------------------------

        static T GetOrCreate<T>(string path, bool overwrite, StringBuilder log, out bool write) where T : V4ConfigAsset
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                write = overwrite;
                log.AppendLine((overwrite ? "updated " : "kept ") + path);
                return existing;
            }
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            write = true;
            log.AppendLine("created " + path);
            return asset;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
