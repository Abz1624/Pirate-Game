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
                if (!writeTimeline) Object.DestroyImmediate(t);
                if (!writeDifficulty) Object.DestroyImmediate(d);
                EditorUtility.SetDirty(timeline);
                EditorUtility.SetDirty(difficulty);
            }
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
            return log.ToString();
        }

        // ---- Sources ------------------------------------------------------------------------------------

        /// <summary>Reloads the database and its definitions from disk, dropping in-memory catalog edits (R-73).</summary>
        static GameDatabase LoadCleanDatabase()
        {
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
