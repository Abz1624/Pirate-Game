using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Editor;
using RoguePirates.V4.Legacy;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Tests
{
    [Category("V4")]
    public sealed class V4ConfigM1Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        const string ProfilePath = "Assets/_Game/Data/Balance/SaltwindSoloStageBalanceProfile.asset";
        const string SchedulePath = "Assets/_Game/Resources/EncounterSpawnSchedule.asset";

        readonly List<Object> _created = new List<Object>();
        ConfigSet _set;

        [SetUp]
        public void SetUp()
        {
            _set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);
            Assert.IsNotNull(_set, "SaltwindDefault config set missing (run RoguePirates/V4/Migrate V3 Data to Configs)");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        T Clone<T>(T source) where T : Object { var c = Object.Instantiate(source); _created.Add(c); return c; }
        WaveTimelineConfig Timeline => _set.All<WaveTimelineConfig>().Single();
        DifficultyCurveConfig Difficulty => _set.All<DifficultyCurveConfig>().Single();

        static Dictionary<string, object> PublicFields(object o) =>
            o.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public).ToDictionary(f => f.Name, f => f.GetValue(o));

        static string Show(object v) => v is Array a ? "[" + string.Join(",", a.Cast<object>()) + "]" : v?.ToString() ?? "null";

        // ---- enum mirrors ----------------------------------------------------------------------------------

        [Test]
        public void ConfigEnums_MirrorTheV3Enums()
        {
            var pairs = new (Type mine, Type v3)[]
            {
                (typeof(ConfigEnums.EnemyBehavior), typeof(EnemyBehavior)), (typeof(ConfigEnums.EnemyCategory), typeof(EnemyCategory)),
                (typeof(ConfigEnums.EnemyTier), typeof(EnemyTier)), (typeof(ConfigEnums.EliteAffix), typeof(EliteAffix)),
                (typeof(ConfigEnums.WeaponPattern), typeof(WeaponPattern)), (typeof(ConfigEnums.TargetingMode), typeof(TargetingMode)),
                (typeof(ConfigEnums.UpgradeAxis), typeof(UpgradeAxis)), (typeof(ConfigEnums.SpawnMode), typeof(SpawnMode)),
                (typeof(ConfigEnums.SpawnGroupLayout), typeof(SpawnGroupLayout)), (typeof(ConfigEnums.SpawnFallbackPolicy), typeof(SpawnFallbackPolicy)),
                (typeof(ConfigEnums.EnemyMovementPattern), typeof(EnemyMovementPattern)), (typeof(ConfigEnums.EnemyAttackPattern), typeof(EnemyAttackPattern)),
                (typeof(ConfigEnums.EnemySpecialBehavior), typeof(EnemySpecialBehavior)), (typeof(ConfigEnums.SaltwindCombatArchetype), typeof(SaltwindCombatArchetype)),
            };
            foreach (var (mine, v3) in pairs)
            {
                var a = Enum.GetNames(mine).Select(n => $"{n}={(int)Enum.Parse(mine, n)}");
                var b = Enum.GetNames(v3).Select(n => $"{n}={(int)Enum.Parse(v3, n)}");
                CollectionAssert.AreEqual(b, a, $"{mine.Name} differs from V3");
            }
        }

        // ---- ownership and legacy mapping ---------------------------------------------------------------

        [Test]
        public void EveryStageProfileField_IsOwnedOrPassThrough()
        {
            var all = typeof(StageBalanceProfile).GetFields(BindingFlags.Instance | BindingFlags.Public).Select(f => f.Name).ToList();
            var owned = new HashSet<string>(V3ConfigMapping.OwnedProfileFields);
            var passed = V3ConfigMapping.PassThroughProfileFields.Keys.ToHashSet();
            CollectionAssert.IsEmpty(all.Where(f => !owned.Contains(f) && !passed.Contains(f)), "unmapped StageBalanceProfile fields");
            CollectionAssert.IsEmpty(owned.Intersect(passed), "a field is both owned and pass-through");
            CollectionAssert.IsEmpty(owned.Concat(passed).Where(f => !all.Contains(f)), "mapping names a field the profile does not have");
        }

        [Test]
        public void RuntimeProfile_FromDefaults_EqualsTheV3Asset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(ProfilePath);
            var runtime = Clone(asset);
            V3ConfigMapping.ApplyProfile(Timeline, Difficulty, runtime);
            var a = PublicFields(asset);
            var b = PublicFields(runtime);
            var diffs = a.Keys.Where(k => Show(a[k]) != Show(b[k])).Select(k => $"{k}: {Show(a[k])} -> {Show(b[k])}").ToList();
            CollectionAssert.IsEmpty(diffs, "default config changed profile fields");
            Assert.AreEqual(asset.ToExport().ContentHash, runtime.ToExport().ContentHash, "profile export hash must not change");
        }

        [Test]
        public void RuntimeSpawnProfiles_FromDefaults_EqualTheV3Assets()
        {
            var schedule = AssetDatabase.LoadAssetAtPath<EncounterSpawnSchedule>(SchedulePath);
            var profiles = (schedule.entries ?? Array.Empty<ScheduledSpawnEntry>()).Select(e => e.profile)
                .Concat(new[] { schedule.hammerheadBeat, schedule.krakenBeat, schedule.swarmPulse, schedule.drownedPursuer, schedule.drownedHarrier, schedule.powderCacheAmbush, schedule.bossSummon })
                .Where(p => p != null).Distinct().ToList();
            Assert.AreEqual(12, profiles.Count);
            // Defaults with the v3-parity preset (it reverts the playtest decisions: elite beats R-03, Drowned anchors R-05).
            using var parity = new ConfigSetRuntime(_set);
            var errors = new List<string>();
            Assert.IsTrue(PresetCodec.Apply(parity, PresetCodec.FromJson(File.ReadAllText(ParityPresetPath)), 0f, errors), string.Join("\n", errors));
            foreach (var p in profiles)
            {
                var spawner = parity.All<EnemySpawnerConfig>().Single(s => s.legacyProfileId == p.ResolvedId);
                Assert.AreEqual(p.enemy != null ? p.enemy.id : null, spawner.anchor != null ? spawner.anchor.enemyId : null, $"{p.ResolvedId} anchor");
                var clone = Clone(p);
                V3ConfigMapping.ApplySpawner(spawner, clone);
                Assert.AreEqual(JsonUtility.ToJson(p), JsonUtility.ToJson(clone), $"{p.ResolvedId} changed by its default config");
            }
        }

        static string ParityPresetPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "plan", "v4-verification", "presets", "v3-parity.json"));

        [Test]
        public void ParityPreset_PhaseIndices_TargetTheElitePhases()
        {
            // The preset addresses timeline phases by index; reordering the phases must not silently retarget it.
            var timeline = _set.All<WaveTimelineConfig>().Single();
            var json = File.ReadAllText(ParityPresetPath);
            var indices = System.Text.RegularExpressions.Regex.Matches(json, @"phases\[(\d+)\]\.focus\.variant")
                .Cast<System.Text.RegularExpressions.Match>().Select(m => int.Parse(m.Groups[1].Value)).ToList();
            CollectionAssert.AreEquivalent(new[] { V3ConfigMapping.EliteA, V3ConfigMapping.EliteB }, indices.Select(i => timeline.phases[i].phaseId));
            foreach (var i in indices) Assert.IsNotNull(timeline.phases[i].focus.variant, timeline.phases[i].phaseId + " spawns its elite variant by default (R-03)");
        }

        [Test]
        public void RuntimeSchedule_FromDefaults_EqualsTheV3Schedule()
        {
            var schedule = AssetDatabase.LoadAssetAtPath<EncounterSpawnSchedule>(SchedulePath);
            var clone = Clone(schedule);
            var map = new Dictionary<SpawnProfile, SpawnProfile>();
            SpawnProfile CloneOf(SpawnProfile p) => p == null ? null : map.TryGetValue(p, out var c) ? c : map[p] = Clone(p);
            foreach (var e in clone.entries) e.profile = CloneOf(e.profile);
            // Band 0 is 0.5/s by decision (R-01); the V3 comparison uses the v3-parity value, 0.6.
            using (var runtime = new ConfigSetRuntime(_set))
            {
                var band0 = runtime.Single<WaveTimelineConfig>().phases.FindIndex(p => p.phaseId == V3ConfigMapping.BandPrefix + "0");
                Assert.IsTrue(runtime.TryApplyEdit("timeline.saltwind", $"phases[{band0}].ratePerSecond", "0.6", 0f, out var error), error);
                V3ConfigMapping.ApplySchedule(runtime.Single<WaveTimelineConfig>(), clone, s => map.Values.FirstOrDefault(c => c.ResolvedId == s.legacyProfileId), atRunStart: true);
            }
            Assert.AreEqual(schedule.gracePeriodSeconds, clone.gracePeriodSeconds);
            Assert.AreEqual(schedule.maxActiveEnemies, clone.maxActiveEnemies);
            Assert.AreEqual(schedule.globalMinSpawnGapSeconds, clone.globalMinSpawnGapSeconds);
            for (var i = 0; i < schedule.entries.Length; i++)
            {
                Assert.AreEqual(schedule.entries[i].startTimeSeconds, clone.entries[i].startTimeSeconds, $"entry {i} start");
                Assert.AreEqual(schedule.entries[i].profile.timing.spawnIntervalSeconds, clone.entries[i].profile.timing.spawnIntervalSeconds, $"band {i} interval (1/rate must round-trip exactly)");
            }
        }

        [Test]
        public void EditingABandRate_ChangesOnlyThatBandsInterval()
        {
            using (var runtime = new ConfigSetRuntime(_set))
            {
                Assert.IsTrue(runtime.TryApplyEdit("timeline.saltwind", "phases[2].ratePerSecond", "4", 0f, out var error), error);
                var schedule = Clone(AssetDatabase.LoadAssetAtPath<EncounterSpawnSchedule>(SchedulePath));
                var map = schedule.entries.ToDictionary(e => e.profile, e => Clone(e.profile));
                foreach (var e in schedule.entries) e.profile = map[e.profile];
                V3ConfigMapping.ApplySchedule(runtime.Single<WaveTimelineConfig>(), schedule, s => map.Values.FirstOrDefault(c => c.ResolvedId == s.legacyProfileId), atRunStart: false);
                Assert.AreEqual(0.25f, schedule.entries[2].profile.timing.spawnIntervalSeconds, 1e-6f);
                Assert.AreEqual(1f / 1.2f, schedule.entries[1].profile.timing.spawnIntervalSeconds, 1e-6f);
            }
        }

        // ---- migration parity against V3 data ----------------------------------------------------------

        [Test]
        public void Archetypes_MatchTheCatalogAppliedV3Definitions()
        {
            var db = RoguePirates.V4.Editor.V3ConfigMigrator.LoadCleanDatabase();
            var clone = Clone(db);
            clone.enemies = db.enemies.Select(e => e != null ? Clone(e) : null).ToArray();
            clone.weapons = db.weapons.Select(w => w != null ? Clone(w) : null).ToArray();
            CombatSpecialistEnemyCatalog.ApplyToDatabase(clone, CombatExperimentContext.Active);
            CombatWeaponCatalog.ApplyToDatabase(clone, CombatExperimentContext.Active);
            foreach (var a in _set.All<EnemyArchetypeConfig>().Where(a => a.inLegacyDatabase))
            {
                var d = clone.GetEnemy(a.enemyId);
                Assert.IsNotNull(d, a.enemyId);
                Assert.AreEqual((int)d.behavior, (int)a.behavior, $"{a.enemyId} behavior");
                Assert.AreEqual(d.threatCost, a.stats.threatCost, $"{a.enemyId} threat");
                Assert.AreEqual(d.baseHP, a.stats.baseHP, $"{a.enemyId} hp");
                Assert.AreEqual(d.baseDamage, a.stats.baseDamage, $"{a.enemyId} damage");
                Assert.AreEqual(d.moveSpeed, a.stats.moveSpeed, $"{a.enemyId} speed");
                Assert.AreEqual(d.radius, a.stats.radius, $"{a.enemyId} radius");
                Assert.AreEqual(d.xpValue, a.rewards.xpValue, $"{a.enemyId} xp");
                Assert.AreEqual(d.isBoss, a.isBoss);
                Assert.AreEqual(EnemyCombatProfileRegistry.TryGet(a.enemyId, out _), a.combat.registered, $"{a.enemyId} registry");
            }
            foreach (var w in _set.All<WeaponConfig>())
            {
                var d = clone.GetWeapon(w.weaponId);
                Assert.IsNotNull(d, w.weaponId);
                Assert.AreEqual(d.baseDamage, w.baseDamage, $"{w.weaponId} damage");
                Assert.AreEqual(d.baseCooldown, w.baseCooldown, $"{w.weaponId} cooldown");
                Assert.AreEqual(d.range, w.range, $"{w.weaponId} range");
                Assert.AreEqual((int)d.targeting, (int)w.targeting, $"{w.weaponId} targeting");
                Assert.AreEqual(d.targetArcDegrees, w.targetArcDegrees, $"{w.weaponId} arc");
                Assert.AreEqual(d.procCoefficient, w.procCoefficient, $"{w.weaponId} proc");
            }
        }

        [Test]
        public void DifficultyDefaults_ReproduceTheV3Formulas()
        {
            var d = Difficulty;
            for (var minute = 0f; minute <= 20f; minute += 0.5f)
            {
                var hp = 20f * d.enemyHpPerMinute.Evaluate(minute) * d.hpMultipliers.tier[0] * d.hpMultipliers.stage[0] * d.globalHpMultiplier;
                Assert.AreEqual(GameMath.EnemyHP(20f, minute, 1, 0), hp, 1e-4f, $"hp at minute {minute}");
                var dmg = 8f * d.enemyDamagePerMinute.Evaluate(minute) * d.damageMultipliers.tier[0] * d.damageMultipliers.stage[0];
                Assert.AreEqual(GameMath.EnemyDamage(8f, minute, 1, 0), dmg, 1e-4f, $"damage at minute {minute}");
            }
        }

        // ---- console, presets, validation ---------------------------------------------------------------

        [Test]
        public void EveryConfigField_HasAConsoleRenderer()
        {
            var types = typeof(V4ConfigAsset).Assembly.GetTypes().Where(t => typeof(V4ConfigAsset).IsAssignableFrom(t) && !t.IsAbstract).ToList();
            Assert.GreaterOrEqual(types.Count, 7);
            // Any field Unity would serialize (public or [SerializeField], at any depth) that the walker cannot
            // handle would be missing from the hash, presets and console.
            var missing = new List<string>();
            var seen = new HashSet<Type>();
            void Check(Type t, string at)
            {
                if (!seen.Add(t)) return;
                for (var b = t; b != null && b != typeof(ScriptableObject) && b != typeof(object); b = b.BaseType)
                    foreach (var f in b.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (f.IsDefined(typeof(NonSerializedAttribute)) || (!f.IsPublic && !f.IsDefined(typeof(SerializeField)))) continue;
                        var kind = ConfigFieldWalker.KindOf(f.FieldType);
                        if (!kind.HasValue) { missing.Add($"{at}.{f.Name} ({f.FieldType.Name})"); continue; }
                        if (kind == ConfigFieldKind.Nested) Check(f.FieldType, $"{at}.{f.Name}");
                        if (kind == ConfigFieldKind.List && ConfigFieldWalker.KindOf(ConfigFieldWalker.ElementType(f.FieldType)) == ConfigFieldKind.Nested)
                            Check(ConfigFieldWalker.ElementType(f.FieldType), $"{at}.{f.Name}[]");
                    }
            }
            foreach (var type in types) Check(type, type.Name);
            CollectionAssert.IsEmpty(missing, "fields with no console renderer / hash support");
        }

        [Test]
        public void SaltwindSet_IsValid_AndPresetRoundTrips()
        {
            var report = ConfigValidator.Validate(_set);
            Assert.IsTrue(report.IsValid, report.ToString());
            string json, hash;
            using (var runtime = new ConfigSetRuntime(_set))
            {
                runtime.TryApplyEdit("timeline.saltwind", "phases[2].ratePerSecond", "4", 0f, out _);
                runtime.TryApplyEdit("spawner.saltwind_ambient_band_3", "placement.distanceMax", "20", 0f, out _);
                hash = runtime.Hash;
                json = PresetCodec.ToJson(PresetCodec.Capture(runtime, "band2x", runtime.Hash));
            }
            using (var fresh = new ConfigSetRuntime(_set))
            {
                var defaults = fresh.Hash;
                Assert.AreNotEqual(defaults, hash);
                Assert.IsTrue(PresetCodec.Apply(fresh, PresetCodec.FromJson(json), 0f, null));
                Assert.AreEqual(hash, fresh.Hash);
            }
        }

        [Test]
        public void Validator_TimelineRules()
        {
            var t = Clone(Timeline);
            t.reservedEncounterSlots = t.maxActiveEnemies;
            t.phases[0].rateMultiplier = 0f;
            t.phases.Add(new TimelinePhase { phaseId = "a", trigger = new PhaseTrigger { kind = PhaseTriggerKind.AfterPhaseCleared, afterPhaseId = "b" } });
            t.phases.Add(new TimelinePhase { phaseId = "b", trigger = new PhaseTrigger { kind = PhaseTriggerKind.AfterPhaseCleared, afterPhaseId = "a" } });
            t.phases.First(p => p.isOvertime).trigger.timeSeconds = 500f;
            t.phases[0].pools[0].entries.ForEach(e => e.weight = 0f);
            var report = new ConfigValidationReport();
            t.Validate(report);
            var text = report.ToString();
            StringAssert.Contains("reserved slots", text);
            StringAssert.Contains("rateMultiplier", text);
            StringAssert.Contains("trigger cycle", text);
            StringAssert.Contains("stage deadline", text);
            StringAssert.Contains("weights add up to 0", text);
        }

        [Test]
        public void Validator_RemainingTimelineRules()
        {
            var t = Clone(Timeline);
            t.phases.Add(new TimelinePhase { phaseId = "storm2", isStorm = true });
            t.storm.contractionEndSeconds = t.storm.contractionStartSeconds - 1f;
            t.phases[0].trigger.timeSeconds = -1f;
            t.phases[1].ratePerSecond = -1f;
            t.phases[2].warning.leadSeconds = -1f;
            var spawner = Clone(_set.All<EnemySpawnerConfig>().First());
            spawner.timing.maxActiveFromProfile = 2;
            t.phases[3].spawner = spawner;
            t.phases[3].burstCount = 5;
            var report = new ConfigValidationReport();
            t.Validate(report);
            var text = report.ToString();
            StringAssert.Contains("more than one storm phase", text);
            StringAssert.Contains("contraction ends before it starts", text);
            StringAssert.Contains("phases[0].trigger.timeSeconds", text);
            StringAssert.Contains("phases[1].ratePerSecond", text);
            StringAssert.Contains("phases[2].warning.leadSeconds", text);
            StringAssert.Contains("larger than the spawner cap", text);
        }

        [Test]
        public void MirrorCopies_LeaveNoFieldUnmatched()
        {
            var p = ScriptableObject.CreateInstance<SpawnProfile>();
            _created.Add(p);
            var c = ScriptableObject.CreateInstance<EnemySpawnerConfig>();
            _created.Add(c);
            foreach (var (v3, v4) in new (object, object)[] { (p.timing, c.timing), (p.placement, c.placement), (p.elite, c.elite), (p.movement, c.movement), (p.attack, c.attack) })
            {
                CollectionAssert.IsEmpty(V3ConfigMapping.CopyFields(v3, v4), $"{v4.GetType().Name} has fields V3 lacks");
                CollectionAssert.IsEmpty(V3ConfigMapping.CopyFields(v4, v3), $"{v3.GetType().Name} has fields the V4 mirror lacks");
            }
        }

        [Test]
        public void Archetypes_CombatRewardsAndVisuals_MatchV3()
        {
            var db = RoguePirates.V4.Editor.V3ConfigMigrator.LoadCleanDatabase();
            foreach (var a in _set.All<EnemyArchetypeConfig>().Where(a => a.inLegacyDatabase))
            {
                var d = db.GetEnemy(a.enemyId);
                Assert.AreEqual(d.goldValue, a.rewards.goldValue, $"{a.enemyId} gold");
                Assert.AreEqual(d.visualScale, a.visual.visualScale, $"{a.enemyId} visual scale");
                Assert.AreSame(d.visualPrefab, a.visual.visualPrefab, $"{a.enemyId} prefab");
                Assert.AreEqual(d.attackCooldown, a.stats.attackCooldown, $"{a.enemyId} attack cooldown");
                Assert.AreEqual(d.preferredRange, a.stats.preferredRange, $"{a.enemyId} preferred range");
                if (!EnemyCombatProfileRegistry.TryGet(a.enemyId, out var profile)) continue;
                var expected = new ArchetypeCombat();
                V3ConfigMapping.CopyFields(profile, expected);
                expected.registered = true;
                Assert.AreEqual(JsonUtility.ToJson(expected), JsonUtility.ToJson(a.combat), $"{a.enemyId} combat profile");
            }
        }

        [Test]
        public void Weapons_EveryMirroredField_MatchesTheCatalogAppliedV3Definition()
        {
            var db = RoguePirates.V4.Editor.V3ConfigMigrator.LoadCleanDatabase();
            var clone = Clone(db);
            clone.enemies = db.enemies.Select(e => e != null ? Clone(e) : null).ToArray();
            clone.weapons = db.weapons.Select(w => w != null ? Clone(w) : null).ToArray();
            CombatWeaponCatalog.ApplyToDatabase(clone, CombatExperimentContext.Active);
            var shared = typeof(WeaponDefinition).GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(f => typeof(WeaponConfig).GetField(f.Name) != null && (f.FieldType.IsPrimitive || f.FieldType == typeof(string) || f.FieldType.IsEnum || f.FieldType == typeof(Color)))
                .ToList();
            Assert.Greater(shared.Count, 15);
            foreach (var w in _set.All<WeaponConfig>())
            {
                var d = clone.GetWeapon(w.weaponId);
                foreach (var f in shared)
                {
                    var v3 = f.GetValue(d);
                    var v4 = typeof(WeaponConfig).GetField(f.Name).GetValue(w);
                    var a = f.FieldType.IsEnum ? ((int)v3).ToString() : ConfigFieldWalker.Format(v3);
                    var b = v4 != null && v4.GetType().IsEnum ? ((int)v4).ToString() : ConfigFieldWalker.Format(v4);
                    Assert.AreEqual(a, b, $"{w.weaponId}.{f.Name}");
                }
                // M9: the axes are V3's effective ones (the asset's, else its pattern defaults), as its level-ups read them.
                Assert.AreEqual(d.GetUpgradeConfigs().Count, w.upgradeAxes.Length, $"{w.weaponId} axes");
            }
        }

        [Test]
        public void TimelinePools_AndReefCrabTime_MatchTheV3Tables()
        {
            object Table(string name) => typeof(SaltwindAmbientCompositionLogic).GetField(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetValue(null);
            var shares = (float[])Table("OrdinaryShareByBand");
            var ordinary = (string[][])Table("OrdinaryFamilies");
            var specialist = (string[][])Table("SpecialistFamilies");
            for (var band = 0; band < 5; band++)
            {
                var phase = Timeline.Phase(V3ConfigMapping.BandPrefix + band);
                Assert.AreEqual(shares[band], phase.pools[0].share, $"band {band} ordinary share");
                Assert.AreEqual(1f - shares[band], phase.pools[1].share, 1e-6f, $"band {band} specialist share");
                CollectionAssert.AreEqual(ordinary[band], phase.pools[0].entries.Select(e => e.enemyId).ToArray(), $"band {band} ordinary pool");
                CollectionAssert.AreEqual(specialist[band], phase.pools[1].entries.Select(e => e.enemyId).ToArray(), $"band {band} specialist pool");
            }
            var pacing = AssetDatabase.LoadAssetAtPath<DemoPacingSettings>("Assets/_Game/Resources/DemoPacingSettings.asset");
            Assert.AreEqual(pacing.firstMinibossSeconds, Timeline.Phase(V3ConfigMapping.ReefCrab).trigger.timeSeconds);
        }

        [Test]
        public void Validator_SpawnerWeaponDifficultyRules()
        {
            var s = Clone(_set.All<EnemySpawnerConfig>().First());
            s.anchor = null;
            s.timing.spawnIntervalSeconds = 0f;
            s.placement.distanceMax = s.placement.distanceMin - 1f;
            s.timing.maxActiveFromProfile = 2;
            s.timing.spawnCount = 5;
            var w = Clone(_set.All<WeaponConfig>().First());
            w.baseCooldown = 0f;
            foreach (var axis in w.upgradeAxes) axis.offerWeight = 0f;
            var d = Clone(Difficulty);
            d.pressureMax = d.pressureMin - 0.1f;
            var report = new ConfigValidationReport();
            s.Validate(report);
            w.Validate(report);
            d.Validate(report);
            var text = report.ToString();
            StringAssert.Contains("no anchor", text);
            StringAssert.Contains("spawnIntervalSeconds", text);
            StringAssert.Contains("below distanceMin", text);
            StringAssert.Contains("above the spawner cap", text);
            StringAssert.Contains("baseCooldown", text);
            StringAssert.Contains("below pressureMin", text);
        }
    }
}
