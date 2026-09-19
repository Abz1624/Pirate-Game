#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HighSeas.Data;
using HighSeas.Core;
using HighSeas.Enemies;
using HighSeas.UI;
using RoguePirates.Simulation.Balance;
using UnityEditor;
using UnityEngine;

namespace HighSeas.Editor
{
    /// <summary>Single-player Monte Carlo front end for the deterministic simulation assembly.</summary>
    public sealed class HighSeasBalanceLabWindow : EditorWindow
    {
        enum OfferPolicy
        {
            GreedyDps,
            Survival,
            Mobility,
            Economy,
            Evolution
        }

        const string ScenarioPreference = "HighSeas.BalanceLab.Scenario";
        BalanceLabScenario _scenario;
        BalanceLabSummary _summary;
        Vector2 _scroll;
        GameDatabase _database;
        int _weaponIndex;
        int _enemyIndex;
        int _bossIndex;
        string _offerAnalysis;
        BalanceCalibrationReport _calibration;
        BalanceRegressionPreset _regressionPreset;
        string _analysisReport;
        BalanceLabSummary _baselineSummary;

        [MenuItem("HighSeas/Balance Lab")]
        public static void Open()
        {
            var window = GetWindow<HighSeasBalanceLabWindow>("Balance Lab");
            window.minSize = new Vector2(460f, 580f);
            window.Show();
        }

        void OnEnable()
        {
            _scenario = new BalanceLabScenario();
            var saved = EditorPrefs.GetString(ScenarioPreference, string.Empty);
            if (!string.IsNullOrEmpty(saved))
                _scenario = JsonUtility.FromJson<BalanceLabScenario>(saved) ?? _scenario;
        }

        void OnDisable() => EditorPrefs.SetString(ScenarioPreference, JsonUtility.ToJson(_scenario));

        void OnGUI()
        {
            EditorGUILayout.LabelField("Saltwind Single-Player Balance Lab", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Runs deterministic combat batches without entering Play Mode. It never edits Data assets. " +
                "Tune authored assets in High Seas Tuning, then use this window to compare a modeled scenario.", MessageType.Info);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawScenario();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Run Batch", GUILayout.Height(30))) Run();
            if (GUILayout.Button("Analyze Offers", GUILayout.Height(30))) AnalyzeOffers();
            if (GUILayout.Button("Import Live Telemetry", GUILayout.Height(30))) ImportTelemetry();
            if (GUILayout.Button("Apply Regression Preset", GUILayout.Height(30))) { _scenario = BalanceLabPresets.Apply(_scenario, _regressionPreset); _summary = null; }
            if (GUILayout.Button("Sensitivity + Diversity", GUILayout.Height(30))) AnalyzeScenario();
            if (GUILayout.Button("Capture Baseline", GUILayout.Height(30))) CaptureBaseline();
            if (GUILayout.Button("Compare to Baseline", GUILayout.Height(30))) CompareBaseline();
            if (GUILayout.Button("Save Scenario Asset", GUILayout.Height(30))) SaveScenarioAsset();
            if (GUILayout.Button("Load Scenario Asset", GUILayout.Height(30))) LoadScenarioAsset();
            if (GUILayout.Button("Reset")) { _scenario = new BalanceLabScenario(); _summary = null; }
            if (GUILayout.Button("Export CSV") && _summary != null) ExportCsv();
            EditorGUILayout.EndHorizontal();
            DrawSummary();
            if (!string.IsNullOrEmpty(_offerAnalysis))
                EditorGUILayout.HelpBox(_offerAnalysis, MessageType.None);
            if (_calibration != null)
                EditorGUILayout.HelpBox(_calibration.ToHumanSummary(), MessageType.Info);
            if (!string.IsNullOrEmpty(_analysisReport))
                EditorGUILayout.HelpBox(_analysisReport, MessageType.None);
        }

        void DrawScenario()
        {
            _scenario.Name = EditorGUILayout.TextField("Scenario name", _scenario.Name);
            _scenario.Seed = EditorGUILayout.IntField("First seed", _scenario.Seed);
            _scenario.Runs = EditorGUILayout.IntSlider("Seeds / runs", _scenario.Runs, 1, 1000);
            _scenario.DurationSeconds = EditorGUILayout.Slider("Duration (seconds)", _scenario.DurationSeconds, 10f, 900f);
            _scenario.InputPolicy = (SimInputPolicy)EditorGUILayout.EnumPopup("Movement policy", _scenario.InputPolicy);
            _scenario.SkillPreset = (BalanceSkillPreset)EditorGUILayout.EnumPopup("Player skill preset", _scenario.SkillPreset);
            _regressionPreset = (BalanceRegressionPreset)EditorGUILayout.EnumPopup("Regression preset", _regressionPreset);
            DrawAuthoredContent();
            Section("Player and character");
            _scenario.Captain = (BalanceLabCaptain)EditorGUILayout.EnumPopup("Captain", _scenario.Captain);
            EditorGUILayout.LabelField("Captain baseline", CaptainDescription(_scenario.Captain), EditorStyles.miniLabel);
            _scenario.PlayerHullMultiplier = EditorGUILayout.Slider("Hull", _scenario.PlayerHullMultiplier, .25f, 3f);
            _scenario.PlayerMoveSpeedMultiplier = EditorGUILayout.Slider("Move speed", _scenario.PlayerMoveSpeedMultiplier, .5f, 2f);
            _scenario.XpGainMultiplier = EditorGUILayout.Slider("XP gains", _scenario.XpGainMultiplier, .25f, 3f);
            _scenario.XpRequirementMultiplier = EditorGUILayout.Slider("XP required", _scenario.XpRequirementMultiplier, .25f, 3f);
            Section("Weapons, offers, and items");
            _scenario.WeaponSlotCap = EditorGUILayout.IntSlider("Weapon slot cap", _scenario.WeaponSlotCap, 1, 6);
            _scenario.WeaponCount = EditorGUILayout.IntSlider("Expected active weapons", _scenario.WeaponCount, 1, _scenario.WeaponSlotCap);
            _scenario.WeaponDamageMultiplier = EditorGUILayout.Slider("Weapon damage", _scenario.WeaponDamageMultiplier, .25f, 3f);
            _scenario.WeaponCooldownMultiplier = EditorGUILayout.Slider("Weapon cooldown", _scenario.WeaponCooldownMultiplier, .25f, 2f);
            _scenario.ProjectileSpeedMultiplier = EditorGUILayout.Slider("Projectile speed", _scenario.ProjectileSpeedMultiplier, .25f, 3f);
            _scenario.ProjectileRadiusMultiplier = EditorGUILayout.Slider("Projectile size", _scenario.ProjectileRadiusMultiplier, .25f, 3f);
            _scenario.ItemDamageMultiplier = EditorGUILayout.Slider("Item damage effect", _scenario.ItemDamageMultiplier, .25f, 3f);
            _scenario.SimulateUpgradePath = EditorGUILayout.Toggle("Simulate upgrade path", _scenario.SimulateUpgradePath);
            using (new EditorGUI.DisabledScope(!_scenario.SimulateUpgradePath))
            {
                _scenario.DamageUpgradePerLevel = EditorGUILayout.Slider("Damage upgrade / level", _scenario.DamageUpgradePerLevel, 0f, .30f);
                _scenario.EvolutionLevel = EditorGUILayout.IntSlider("Evolution level", _scenario.EvolutionLevel, 1, 20);
                _scenario.EvolutionDamageMultiplier = EditorGUILayout.Slider("Evolution damage", _scenario.EvolutionDamageMultiplier, 1f, 3f);
            }
            EditorGUILayout.LabelField("Equipped weapon simulation", _scenario.WeaponLoadout.Count > 0 ? _scenario.WeaponLoadout.Count + " authored weapons" : "legacy expected-loadout proxy", EditorStyles.miniLabel);
            Section("Enemies and boss");
            _scenario.DirectorEnabled = EditorGUILayout.Toggle("Spawn director enabled", _scenario.DirectorEnabled);
            _scenario.SpawnDensityMultiplier = EditorGUILayout.Slider("Spawn density", _scenario.SpawnDensityMultiplier, .25f, 3f);
            EditorGUILayout.LabelField("Director snapshot", $"threat {_scenario.DirectorThreatMultiplier:F2} · cap {_scenario.DirectorMaxActiveEnemies} · selected weight {_scenario.EnemySpawnWeight:F2}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Selected availability", $"{_scenario.EnemyAvailableAfterSeconds:F0}s–{(_scenario.EnemyAvailableUntilSeconds > 0f ? _scenario.EnemyAvailableUntilSeconds.ToString("F0") : "end")} · enemy cap {_scenario.EnemySpawnMaxConcurrent}", EditorStyles.miniLabel);
            _scenario.EnemyHullMultiplier = EditorGUILayout.Slider("Enemy hull", _scenario.EnemyHullMultiplier, .25f, 3f);
            _scenario.EnemyDamageMultiplier = EditorGUILayout.Slider("Enemy damage", _scenario.EnemyDamageMultiplier, .25f, 3f);
            _scenario.EnemyMoveSpeedMultiplier = EditorGUILayout.Slider("Enemy speed", _scenario.EnemyMoveSpeedMultiplier, .25f, 2f);
            _scenario.BossSpawnSeconds = EditorGUILayout.Slider("Boss time", _scenario.BossSpawnSeconds, 10f, 900f);
            _scenario.BossHullMultiplier = EditorGUILayout.Slider("Boss hull", _scenario.BossHullMultiplier, .25f, 4f);
            _scenario.BossDamageMultiplier = EditorGUILayout.Slider("Boss damage", _scenario.BossDamageMultiplier, .25f, 4f);
            Section("POI route assumptions");
            _scenario.EnablePois = EditorGUILayout.Toggle("Enable POI route", _scenario.EnablePois);
            using (new EditorGUI.DisabledScope(!_scenario.EnablePois))
            {
                _scenario.SalvageChestsOpened = EditorGUILayout.IntSlider("Salvage chests opened", _scenario.SalvageChestsOpened, 0, 12);
                _scenario.RepairWrecksUsed = EditorGUILayout.IntSlider("Repair Wrecks used", _scenario.RepairWrecksUsed, 0, 1);
                _scenario.PowderCachesCleared = EditorGUILayout.IntSlider("Powder Caches cleared", _scenario.PowderCachesCleared, 0, 1);
                _scenario.SignalBuoysCharged = EditorGUILayout.IntSlider("Signal Buoys charged", _scenario.SignalBuoysCharged, 0, 1);
                _scenario.TreasureChartsUsed = EditorGUILayout.IntSlider("Treasure Charts used", _scenario.TreasureChartsUsed, 0, 1);
                _scenario.PoiDamageMultiplier = EditorGUILayout.Slider("POI damage reward", _scenario.PoiDamageMultiplier, .25f, 3f);
                _scenario.PoiHealingMultiplier = EditorGUILayout.Slider("POI healing reward", _scenario.PoiHealingMultiplier, .25f, 3f);
                _scenario.PoiXpMultiplier = EditorGUILayout.Slider("POI XP reward", _scenario.PoiXpMultiplier, .25f, 3f);
                _scenario.PoiTravelSeconds = EditorGUILayout.Slider("POI travel seconds", _scenario.PoiTravelSeconds, 0f, 60f);
                _scenario.PoiInteractionRiskDamage = EditorGUILayout.Slider("POI interaction risk", _scenario.PoiInteractionRiskDamage, 0f, 30f);
                _scenario.PoiFailureChance = EditorGUILayout.Slider("POI failure chance", _scenario.PoiFailureChance, 0f, .75f);
            }
            Section("Economy / merchant route");
            _scenario.StartingGold = EditorGUILayout.IntField("Starting gold", _scenario.StartingGold);
            _scenario.MerchantVisits = EditorGUILayout.IntSlider("Merchant visits", _scenario.MerchantVisits, 0, 4);
            _scenario.MerchantItemPrice = EditorGUILayout.IntField("Merchant item price", _scenario.MerchantItemPrice);
            _scenario.MerchantRerollPrice = EditorGUILayout.IntField("Merchant reroll price", _scenario.MerchantRerollPrice);
            _scenario.PlannedMerchantPurchases = EditorGUILayout.IntSlider("Planned purchases", _scenario.PlannedMerchantPurchases, 0, 6);
            _scenario.MerchantItemPowerMultiplier = EditorGUILayout.Slider("Merchant item power", _scenario.MerchantItemPowerMultiplier, 1f, 2f);
            EditorGUILayout.LabelField("Target bands", EditorStyles.boldLabel);
            _scenario.TargetAverageSurvivalMin = EditorGUILayout.Slider("Average survival min", _scenario.TargetAverageSurvivalMin, 0f, 1f);
            _scenario.TargetAverageSurvivalMax = EditorGUILayout.Slider("Average survival max", _scenario.TargetAverageSurvivalMax, 0f, 1f);
            _scenario.TargetMerchantAffordabilityMin = EditorGUILayout.Slider("Merchant afford min", _scenario.TargetMerchantAffordabilityMin, 0f, 1f);
        }

        static void Section(string title)
        {
            EditorGUILayout.Space(7);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        void DrawAuthoredContent()
        {
            EnsureDatabase();
            if (_database == null) return;
            Section("Authored content snapshot");
            EditorGUILayout.LabelField("Values below are copied from GameDatabase for this lab run only.", EditorStyles.miniLabel);
            _weaponIndex = EditorGUILayout.Popup("Weapon", _weaponIndex, Labels(_database.weapons));
            var weapon = At(_database.weapons, _weaponIndex);
            if (weapon != null)
            {
                _scenario.WeaponId = weapon.id;
                _scenario.WeaponBaseDamage = weapon.baseDamage;
                _scenario.WeaponBaseCooldown = weapon.baseCooldown;
                _scenario.WeaponRange = weapon.range;
                _scenario.WeaponProjectileSpeed = weapon.projectileSpeed;
                _scenario.WeaponPatternTargetMultiplier = PatternTargetMultiplier(weapon.pattern, weapon.baseQuantity, weapon.duration);
                EditorGUILayout.LabelField($"{weapon.baseDamage} damage · {weapon.baseCooldown:F2}s · {weapon.range:F0}m", EditorStyles.miniLabel);
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add selected weapon to loadout")) AddSelectedWeaponToLoadout(weapon);
            if (GUILayout.Button("Snapshot first slot-cap weapons")) SnapshotWeaponLoadout();
            EditorGUILayout.EndHorizontal();
            DrawEditableLoadout();
            _enemyIndex = EditorGUILayout.Popup("Standard enemy", _enemyIndex, Labels(_database.enemies));
            var enemy = At(_database.enemies, _enemyIndex);
            if (enemy != null)
            {
                _scenario.EnemyId = enemy.id;
                _scenario.EnemyBaseHull = enemy.baseHP;
                _scenario.EnemyBaseDamage = enemy.baseDamage;
                _scenario.EnemyBaseMoveSpeed = enemy.moveSpeed;
                _scenario.EnemyXpValue = enemy.xpValue;
                CopyDirectorSnapshot(enemy.id);
                EditorGUILayout.LabelField($"{enemy.baseHP} hull · {enemy.baseDamage} damage · {enemy.moveSpeed:F2} speed · {enemy.xpValue} XP", EditorStyles.miniLabel);
            }
            var bosses = Bosses(_database.enemies);
            _bossIndex = EditorGUILayout.Popup("Boss", _bossIndex, Labels(bosses));
            var boss = At(bosses, _bossIndex);
            if (boss != null)
            {
                _scenario.BossId = boss.id;
                _scenario.BossBaseHull = boss.baseHP;
                _scenario.BossBaseDamage = boss.baseDamage;
                _scenario.BossBaseMoveSpeed = boss.moveSpeed;
                _scenario.BossBehavior = MapBehavior(boss.behavior);
                _scenario.BossAttackCooldown = boss.attackCooldown;
                _scenario.BossPreferredRange = boss.preferredRange;
                EditorGUILayout.LabelField($"{boss.baseHP} hull · {boss.baseDamage} damage · {boss.moveSpeed:F2} speed", EditorStyles.miniLabel);
            }
            if (GUILayout.Button("Snapshot full enabled enemy roster")) SnapshotEnemyRoster();
            if (GUILayout.Button("Snapshot authored scripted waves")) SnapshotWaves();
        }

        void EnsureDatabase()
        {
            if (_database == null)
                _database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Game/Data/GameDatabase.asset");
        }

        void CopyDirectorSnapshot(string enemyId)
        {
            _scenario.DirectorThreatMultiplier = 1f;
            _scenario.DirectorMaxActiveEnemies = 28;
            _scenario.EnemySpawnWeight = 1f;
            _scenario.EnemySpawnEnabled = true;
            _scenario.EnemySpawnMaxConcurrent = 0;
            _scenario.EnemyAvailableAfterSeconds = 0f;
            _scenario.EnemyAvailableUntilSeconds = 0f;
            _scenario.EnemyDirectorSpeedMultiplier = 1f;
        }

        void SnapshotWeaponLoadout()
        {
            _scenario.WeaponLoadout.Clear();
            if (_database?.weapons == null) return;
            for (var i = 0; i < _database.weapons.Length && _scenario.WeaponLoadout.Count < _scenario.WeaponSlotCap; i++)
            {
                var weapon = _database.weapons[i];
                if (weapon == null) continue;
                _scenario.WeaponLoadout.Add(new BalanceWeaponLoadoutEntry
                {
                    Id = weapon.id, Damage = weapon.baseDamage, Cooldown = weapon.baseCooldown,
                    Range = weapon.range, ProjectileSpeed = weapon.projectileSpeed,
                    TargetMultiplier = PatternTargetMultiplier(weapon.pattern, weapon.baseQuantity, weapon.duration), SizeMultiplier = weapon.size
                });
            }
        }

        void AddSelectedWeaponToLoadout(WeaponDefinition weapon)
        {
            if (weapon == null || _scenario.WeaponLoadout.Count >= _scenario.WeaponSlotCap) return;
            _scenario.WeaponLoadout.Add(new BalanceWeaponLoadoutEntry
            {
                Id = weapon.id, Damage = weapon.baseDamage, Cooldown = weapon.baseCooldown, Range = weapon.range,
                ProjectileSpeed = weapon.projectileSpeed, TargetMultiplier = PatternTargetMultiplier(weapon.pattern, weapon.baseQuantity, weapon.duration), SizeMultiplier = weapon.size
            });
        }

        void DrawEditableLoadout()
        {
            if (_scenario.WeaponLoadout.Count == 0) return;
            EditorGUILayout.LabelField("Equipped loadout", EditorStyles.miniBoldLabel);
            for (var i = _scenario.WeaponLoadout.Count - 1; i >= 0; i--)
            {
                var entry = _scenario.WeaponLoadout[i];
                if (entry == null) { _scenario.WeaponLoadout.RemoveAt(i); continue; }
                EditorGUILayout.BeginHorizontal();
                entry.Id = EditorGUILayout.TextField(entry.Id, GUILayout.Width(130f));
                entry.Damage = EditorGUILayout.FloatField(entry.Damage, GUILayout.Width(48f));
                entry.Cooldown = EditorGUILayout.FloatField(entry.Cooldown, GUILayout.Width(48f));
                entry.Range = EditorGUILayout.FloatField(entry.Range, GUILayout.Width(48f));
                if (GUILayout.Button("−", GUILayout.Width(24f))) _scenario.WeaponLoadout.RemoveAt(i);
                EditorGUILayout.EndHorizontal();
            }
        }

        void SnapshotEnemyRoster()
        {
            _scenario.EnemyRoster.Clear();
            if (_database?.enemies == null) return;
            for (var i = 0; i < _database.enemies.Length; i++)
            {
                var enemy = _database.enemies[i];
                if (enemy == null || enemy.isBoss) continue;
                _scenario.EnemyRoster.Add(new BalanceEnemyRosterEntry
                {
                    Id = enemy.id, Enabled = true, Weight = 1f,
                    MaxConcurrent = 0,
                    AvailableAfterSeconds = 0f,
                    AvailableUntilSeconds = 0f,
                    Hull = enemy.baseHP, Damage = enemy.baseDamage, MoveSpeed = enemy.moveSpeed,
                    AttackCooldown = enemy.attackCooldown, PreferredRange = enemy.preferredRange,
                    Xp = enemy.xpValue, Gold = enemy.goldValue, Behavior = MapBehavior(enemy.behavior)
                });
            }
        }

        void SnapshotWaves()
        {
            _scenario.ScriptedWaves.Clear();
            _scenario.UseScriptedWaves = false;
            _scenario.ContinueAfterScriptedWaves = true;
        }

        static BalanceEnemyBehavior MapBehavior(EnemyBehavior behavior) => behavior switch
        {
            EnemyBehavior.MaintainRange or EnemyBehavior.FormationRanged or EnemyBehavior.StationaryTurret => BalanceEnemyBehavior.Ranged,
            EnemyBehavior.TelegraphedCharge or EnemyBehavior.DiveLunge or EnemyBehavior.PhaseDash => BalanceEnemyBehavior.Charge,
            EnemyBehavior.SuicideApproach => BalanceEnemyBehavior.Suicide,
            EnemyBehavior.TankChaser => BalanceEnemyBehavior.Tank,
            EnemyBehavior.FlankingChaser => BalanceEnemyBehavior.Flank,
            _ => BalanceEnemyBehavior.Chaser
        };

        static T At<T>(T[] values, int index) where T : UnityEngine.Object =>
            values != null && values.Length > 0 ? values[Mathf.Clamp(index, 0, values.Length - 1)] : null;

        static string[] Labels<T>(T[] values) where T : UnityEngine.Object
        {
            if (values == null || values.Length == 0) return new[] { "(none)" };
            var labels = new string[values.Length];
            for (var i = 0; i < values.Length; i++) labels[i] = values[i] != null ? values[i].name : "(missing)";
            return labels;
        }

        static EnemyDefinition[] Bosses(EnemyDefinition[] enemies)
        {
            var bosses = new List<EnemyDefinition>();
            if (enemies != null)
            {
                for (var i = 0; i < enemies.Length; i++)
                    if (enemies[i] != null && enemies[i].isBoss)
                        bosses.Add(enemies[i]);
            }
            return bosses.ToArray();
        }

        static float PatternTargetMultiplier(WeaponPattern pattern, int quantity, float duration)
        {
            var emissions = Mathf.Max(1, quantity);
            return pattern switch
            {
                WeaponPattern.FanVolley => emissions * 0.55f,
                WeaponPattern.ChainLightning => emissions * 1.6f,
                WeaponPattern.MineDrop => emissions * 1.8f,
                WeaponPattern.Orbit => emissions * 1.25f,
                WeaponPattern.TrailZone => emissions * Mathf.Clamp(duration, 1f, 3f),
                WeaponPattern.AuraPulse => emissions * 1.4f,
                WeaponPattern.PredictedAoE => emissions * 1.3f,
                WeaponPattern.SummonChase => emissions * 1.15f,
                _ => emissions
            };
        }

        void AnalyzeOffers()
        {
            EnsureDatabase();
            var settings = AssetDatabase.LoadAssetAtPath<LevelUpSettings>("Assets/_Game/Data/LevelUpSettings.asset");
            var pacing = AssetDatabase.LoadAssetAtPath<DemoPacingSettings>("Assets/_Game/Data/DemoPacingSettings.asset");
            if (_database == null || settings == null || pacing == null)
            {
                _offerAnalysis = "Offer analysis requires GameDatabase, LevelUpSettings, and DemoPacingSettings in Assets/_Game/Data/.";
                return;
            }

            const int runs = 200;
            const int levels = 12;
            var text = "Offer analysis — 200 seeded 12-level streams per policy:\n";
            foreach (OfferPolicy policy in Enum.GetValues(typeof(OfferPolicy)))
                text += "\n" + BuildOfferSummary(RunOfferPolicy(policy, runs, levels, settings, pacing), runs * levels, policy);
            _offerAnalysis = text;
        }

        (Dictionary<string, int> shown, Dictionary<string, int> picked) RunOfferPolicy(
            OfferPolicy policy, int runs, int levels, LevelUpSettings settings, DemoPacingSettings pacing)
        {
            var shown = new Dictionary<string, int>();
            var picked = new Dictionary<string, int>();
            for (var run = 0; run < runs; run++)
            {
                var rng = new RunRng(_scenario.Seed + run);
                var owned = new List<string> { _scenario.WeaponId };
                var weaponLevels = new Dictionary<string, int> { [_scenario.WeaponId] = 1 };
                var logbooks = new List<string>();
                var logbookLevels = new Dictionary<string, int>();
                for (var level = 1; level <= levels; level++)
                {
                    var offers = LevelOfferGenerator.Generate(_database, settings, pacing, rng, 0f,
                        owned, weaponLevels, logbooks, logbookLevels, _scenario.WeaponSlotCap, 2,
                        level == 1, playerLevel: level, useFillStateWeighting: true);
                    var bestIndex = -1;
                    var bestScore = float.MinValue;
                    for (var i = 0; i < offers.Count; i++)
                    {
                        var offer = offers[i];
                        if (offer.kind == LevelOfferKind.Continue) continue;
                        Count(shown, OfferKey(offer));
                        var score = OfferScore(offer, weaponLevels, policy);
                        if (score > bestScore) { bestScore = score; bestIndex = i; }
                    }
                    if (bestIndex < 0) continue;
                    var chosen = offers[bestIndex];
                    Count(picked, OfferKey(chosen));
                    ApplyChoice(chosen, owned, weaponLevels, logbooks, logbookLevels);
                }
            }
            return (shown, picked);
        }

        float OfferScore(LevelOffer offer, Dictionary<string, int> weaponLevels, OfferPolicy policy)
        {
            var weapon = _database.GetWeapon(offer.id);
            var weaponScore = weapon != null ? WeaponScore(weapon) : 0f;
            var upgradeScore = weaponScore * (1f + offer.nextLevel * 0.12f);
            if (policy == OfferPolicy.GreedyDps)
            {
                if (offer.kind == LevelOfferKind.NewWeapon) return weaponScore;
                if (offer.kind == LevelOfferKind.WeaponUpgrade || offer.kind == LevelOfferKind.WeaponEvolution) return upgradeScore;
                return offer.kind == LevelOfferKind.NewRunItem ? 0.35f : 0.2f;
            }
            if (policy == OfferPolicy.Evolution)
            {
                if (offer.kind == LevelOfferKind.NewRunItem && IsEvolutionItem(offer.id)) return 10f;
                if (offer.kind == LevelOfferKind.WeaponEvolution) return 9f;
                if (offer.kind == LevelOfferKind.WeaponUpgrade) return offer.nextLevel >= 10 ? 7f : 1f + offer.nextLevel * 0.3f;
                return offer.kind == LevelOfferKind.NewWeapon ? weaponScore * 0.08f : 0.1f;
            }

            var logbook = _database.GetLogbook(offer.id);
            if (policy == OfferPolicy.Survival)
            {
                if (logbook != null) return DefensiveStatScore(logbook.primaryStat, logbook.perLevelAdditive);
                if (offer.kind == LevelOfferKind.NewRunItem) return SurvivalItemScore(offer.id);
                return upgradeScore * 0.15f;
            }

            if (logbook != null) return MobilityOrEconomyStatScore(logbook.primaryStat, logbook.perLevelAdditive, policy);
            if (offer.kind == LevelOfferKind.NewRunItem) return policy == OfferPolicy.Mobility ? MobilityItemScore(offer.id) : EconomyItemScore(offer.id);
            return upgradeScore * 0.1f;
        }

        static float DefensiveStatScore(StatId stat, float value) => stat switch
        {
            StatId.Hull or StatId.HullRegen or StatId.Armor or StatId.Shield or StatId.Evasion => 5f + value,
            StatId.MoveSpeed => 2.5f + value,
            _ => 0.1f
        };

        static float MobilityOrEconomyStatScore(StatId stat, float value, OfferPolicy policy)
        {
            if (policy == OfferPolicy.Mobility)
                return stat is StatId.MoveSpeed or StatId.PickupRadius ? 6f + value : 0.1f;
            return stat is StatId.GoldGain or StatId.DoubloonGain or StatId.Luck or StatId.XPGain ? 6f + value : 0.1f;
        }

        static bool IsEvolutionItem(string id) => id is "grapeshot_pouch" or "double_fuse_cord" or "serrated_scabbard" or "broadside_drum" or "dragons_breath_nozzle" or "copper_conductor";
        static float SurvivalItemScore(string id) => id is "reinforced_keel" or "ghost_lantern" ? 7f : 0.2f;
        static float MobilityItemScore(string id) => id is "cursed_compass" or "stormglass" ? 7f : 0.2f;
        static float EconomyItemScore(string id) => id is "lucky_doubloon" or "golden_figurehead" or "admirals_ledger" ? 7f : 0.2f;

        static float WeaponScore(WeaponDefinition weapon) =>
            weapon.baseDamage * PatternTargetMultiplier(weapon.pattern, weapon.baseQuantity, weapon.duration) /
            Mathf.Max(0.05f, weapon.baseCooldown);

        static void ApplyChoice(LevelOffer choice, List<string> owned, Dictionary<string, int> weaponLevels,
            List<string> logbooks, Dictionary<string, int> logbookLevels)
        {
            if (choice.kind == LevelOfferKind.NewWeapon && !owned.Contains(choice.id))
            {
                owned.Add(choice.id);
                weaponLevels[choice.id] = 1;
            }
            else if (choice.kind == LevelOfferKind.WeaponUpgrade || choice.kind == LevelOfferKind.WeaponEvolution)
                weaponLevels[choice.id] = choice.nextLevel;
            else if (choice.kind == LevelOfferKind.Logbook)
            {
                if (!logbooks.Contains(choice.id)) logbooks.Add(choice.id);
                logbookLevels[choice.id] = choice.nextLevel;
            }
        }

        static string OfferKey(LevelOffer offer) => offer.kind + ":" + offer.id;
        static void Count(Dictionary<string, int> counts, string key) => counts[key] = counts.TryGetValue(key, out var value) ? value + 1 : 1;

        static string BuildOfferSummary((Dictionary<string, int> shown, Dictionary<string, int> picked) data, int totalLevels, OfferPolicy policy)
        {
            var ranked = new List<KeyValuePair<string, int>>(data.picked);
            ranked.Sort((a, b) => b.Value.CompareTo(a.Value));
            var text = policy + ": ";
            for (var i = 0; i < Mathf.Min(5, ranked.Count); i++)
            {
                var key = ranked[i].Key;
                data.shown.TryGetValue(key, out var offerCount);
                text += key + " picked " + (ranked[i].Value / (float)totalLevels).ToString("P0") +
                    " · shown " + (offerCount / (float)totalLevels).ToString("P0") + "\n";
            }
            return text;
        }

        static string CaptainDescription(BalanceLabCaptain captain) => captain switch
        {
            BalanceLabCaptain.Brine => "110 hull, 5.7 speed",
            BalanceLabCaptain.Flint => "90 hull, 6.3 speed",
            BalanceLabCaptain.Richie => "95 hull, 6.1 speed",
            BalanceLabCaptain.Ember => "92 hull, 5.9 speed",
            BalanceLabCaptain.Roxie => "88 hull, 6.5 speed",
            _ => "100 hull, 6.2 speed"
        };

        void Run()
        {
            _scenario.Normalize();
            _summary = BalanceLabRunner.Run(_scenario);
            Debug.Log("Balance Lab: " + _summary.ToHumanSummary());
        }

        void ImportTelemetry()
        {
            if (_summary == null)
            {
                EditorUtility.DisplayDialog("Run a batch first", "Run the selected scenario before comparing it to a live telemetry export.", "OK");
                return;
            }
            var path = EditorUtility.OpenFilePanel("Import RunCombatTelemetry JSON", Application.persistentDataPath, "json");
            if (string.IsNullOrEmpty(path)) return;
            _calibration = BalanceLabCalibration.Compare(_summary, BalanceLiveTelemetry.Parse(File.ReadAllText(path)));
        }

        void AnalyzeScenario()
        {
            _scenario.Normalize();
            _summary ??= BalanceLabRunner.Run(_scenario);
            _analysisReport = BalanceLabAnalysis.TargetBandReport(_summary, _scenario) + "\n" +
                BalanceLabAnalysis.SensitivityReport(_scenario) + "\n" + BalanceLabAnalysis.BuildDiversityReport(_scenario);
        }

        void CaptureBaseline()
        {
            _scenario.Normalize();
            _baselineSummary = BalanceLabRunner.Run(_scenario);
            _analysisReport = "Captured baseline: " + _baselineSummary.ToHumanSummary();
        }

        void CompareBaseline()
        {
            _scenario.Normalize();
            _summary = BalanceLabRunner.Run(_scenario);
            _analysisReport = BalanceLabAnalysis.Compare(_baselineSummary, _summary);
        }

        void SaveScenarioAsset()
        {
            var path = EditorUtility.SaveFilePanelInProject("Save balance scenario", _scenario.Name.Replace(' ', '_'), "balance.json", "Store a versionable Balance Lab scenario in the project.");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, JsonUtility.ToJson(_scenario, true));
            AssetDatabase.Refresh();
        }

        void LoadScenarioAsset()
        {
            var path = EditorUtility.OpenFilePanel("Load balance scenario", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path)) return;
            var loaded = JsonUtility.FromJson<BalanceLabScenario>(File.ReadAllText(path));
            if (loaded != null) { _scenario = loaded; _summary = null; _analysisReport = null; }
        }

        void DrawSummary()
        {
            if (_summary == null) return;
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(_summary.ToHumanSummary(), MessageType.None);
            EditorGUILayout.HelpBox(_summary.Diagnostics(), _summary.SurvivalRate < .35f ? MessageType.Warning : MessageType.Info);
            EditorGUILayout.HelpBox(BalanceLabAnalysis.TargetBandReport(_summary, _scenario), MessageType.None);
            EditorGUILayout.HelpBox(BalanceLabAnalysis.PercentileReport(_summary), MessageType.None);
            DrawSampleCurves(_summary);
        }

        static void DrawSampleCurves(BalanceLabSummary summary)
        {
            if (summary.Samples.Count == 0 || summary.Samples[0].TimeSeries.Count == 0) return;
            var hull = new AnimationCurve();
            var enemies = new AnimationCurve();
            var gold = new AnimationCurve();
            foreach (var point in summary.Samples[0].TimeSeries)
            {
                hull.AddKey(point.Seconds, point.HullFraction);
                enemies.AddKey(point.Seconds, point.EnemyCount);
                gold.AddKey(point.Seconds, point.Gold);
            }
            EditorGUILayout.LabelField("Sample-run timeline", EditorStyles.boldLabel);
            EditorGUILayout.CurveField("Hull fraction", hull);
            EditorGUILayout.CurveField("Enemy pressure", enemies);
            EditorGUILayout.CurveField("Gold", gold);
        }

        void ExportCsv()
        {
            var path = EditorUtility.SaveFilePanel("Export balance lab results", Application.dataPath, "balance_lab_results", "csv");
            if (string.IsNullOrEmpty(path)) return;
            var csv = new StringBuilder("seed,kills,level,objective_secured,player_survived,sim_seconds,peak_enemies\n");
            foreach (var run in _summary.Samples)
                csv.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},{5:F3},{6}\n", run.Seed, run.Kills, run.Level, run.ObjectiveSecured, run.PlayerSurvived, run.SimSeconds, run.PeakEnemyCount);
            File.WriteAllText(path, csv.ToString());
            AssetDatabase.Refresh();
            Debug.Log("Balance Lab exported " + path);
        }
    }
}
#endif
