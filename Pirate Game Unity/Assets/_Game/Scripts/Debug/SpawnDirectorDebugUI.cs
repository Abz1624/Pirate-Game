using HighSeas.Core;
using HighSeas.Enemies;
using HighSeas.V3;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HighSeas.Debugging
{
    /// <summary>
    /// In-run IMGUI panel to tweak <see cref="SpawnDirector"/> and spawn settings at runtime (F9).
    /// Changes apply to a cloned <see cref="SpawnDirectorSettings"/> instance — asset files on disk are not modified.
    /// </summary>
    public sealed class SpawnDirectorDebugUI : MonoBehaviour
    {
        const float PanelWidth = 380f;

        static readonly GUILayoutOption[] LabelWidth = { GUILayout.Width(168f) };

        SpawnDirector _director;
        EnemyWorld _enemyWorld;
        bool _visible;
        bool _showPerEnemy;
        Vector2 _scroll;
        Rect _panelRect = new(12f, 72f, PanelWidth, 520f);

        public static SpawnDirectorDebugUI Ensure()
        {
            if (!V3PlaytestFlags.EnableSpawnDirectorDebugUi)
                return FindFirstObjectByType<SpawnDirectorDebugUI>();

            var existing = FindFirstObjectByType<SpawnDirectorDebugUI>();
            if (existing != null)
                return existing;

            var systems = GameObject.Find("Systems");
            var host = systems != null ? systems : new GameObject("SpawnDirectorDebugUI");
            return host.GetComponent<SpawnDirectorDebugUI>() ?? host.AddComponent<SpawnDirectorDebugUI>();
        }

        void Awake()
        {
            _director = FindFirstObjectByType<SpawnDirector>();
            _enemyWorld = FindFirstObjectByType<EnemyWorld>();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.f9Key.wasPressedThisFrame)
                return;

            _visible = !_visible;
            if (_director == null)
                _director = FindFirstObjectByType<SpawnDirector>();

            var message = _visible ? "Spawn debug ON (F9)" : "Spawn debug OFF (F9)";
            V3ToastHud.Instance?.Show(message, 2f);
            Debug.Log(message);
        }

        void OnGUI()
        {
            if (!_visible || _director == null)
                return;

            _panelRect = GUI.Window(
                GetInstanceID(),
                _panelRect,
                DrawWindow,
                "Spawn Director (F9)");
        }

        void DrawWindow(int id)
        {
            GUI.DragWindow(new Rect(0f, 0f, PanelWidth, 22f));

            _scroll = GUILayout.BeginScrollView(_scroll);

            DrawLiveStats();
            GUILayout.Space(6f);
            DrawRuntimeMultipliers();
            GUILayout.Space(6f);

            var settings = _director.GetMutableSettings();
            if (settings != null)
            {
                DrawGlobalSettings(settings);
                GUILayout.Space(6f);
                DrawPerEnemySettings(settings);
            }

            GUILayout.EndScrollView();
        }

        void DrawLiveStats()
        {
            GUILayout.Label("Live", GUI.skin.box);
            var run = RunManager.Instance;
            var elapsed = run != null ? run.ElapsedSeconds : 0f;
            var active = _enemyWorld != null ? _enemyWorld.ActiveCount : 0;
            GUILayout.Label($"Elapsed: {elapsed:F1}s ({elapsed / 60f:F1} min)");
            GUILayout.Label($"Active enemies: {active} / cap {_director.GetEnemyCapForDebug()}");
            GUILayout.Label($"Threat budget: {_director.DebugThreatBudget:F2}");
            GUILayout.Label($"Threat / sec: {_director.GetEffectiveThreatPerSecond():F2}");
            GUILayout.Label($"Elite chance: {_director.DebugEliteChance:P1}");
            GUILayout.Label(
                $"V3 pacing: {_director.DebugV3PacingEnabled} · Director V2: {_director.DebugDirectorV2Enabled}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear budget"))
                _director.ClearThreatBudget();
            if (GUILayout.Button("Refresh refs"))
            {
                _director = FindFirstObjectByType<SpawnDirector>();
                _enemyWorld = FindFirstObjectByType<EnemyWorld>();
            }
            GUILayout.EndHorizontal();
        }

        void DrawRuntimeMultipliers()
        {
            GUILayout.Label("Runtime multipliers", GUI.skin.box);

            var rate = _director.DebugSpawnRateMultiplier;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Spawn rate", LabelWidth);
            rate = GUILayout.HorizontalSlider(rate, 0.1f, 3f);
            GUILayout.Label($"{rate:F2}", GUILayout.Width(40f));
            GUILayout.EndHorizontal();
            _director.SetSpawnRateMultiplier(rate);

            var density = _director.DebugSpawnDensityMultiplier;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Spawn density", LabelWidth);
            density = GUILayout.HorizontalSlider(density, 0.1f, 3f);
            GUILayout.Label($"{density:F2}", GUILayout.Width(40f));
            GUILayout.EndHorizontal();
            _director.SetSpawnDensityMultiplier(density);

            var hp = _director.DebugEnemyHpMultiplier;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Enemy HP", LabelWidth);
            hp = GUILayout.HorizontalSlider(hp, 0.1f, 4f);
            GUILayout.Label($"{hp:F2}", GUILayout.Width(40f));
            GUILayout.EndHorizontal();

            var dmg = _director.DebugEnemyDamageMultiplier;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Enemy damage", LabelWidth);
            dmg = GUILayout.HorizontalSlider(dmg, 0.1f, 4f);
            GUILayout.Label($"{dmg:F2}", GUILayout.Width(40f));
            GUILayout.EndHorizontal();
            _director.SetEnemyCombatMultipliers(hp, dmg);
        }

        static void DrawGlobalSettings(SpawnDirectorSettings settings)
        {
            GUILayout.Label("SpawnDirectorSettings (runtime clone)", GUI.skin.box);

            settings.gracePeriodSeconds = SliderField("Grace period (s)", settings.gracePeriodSeconds, 0f, 30f);
            settings.minSecondsBetweenSpawns = SliderField("Min gap (s)", settings.minSecondsBetweenSpawns, 0.05f, 2f);
            settings.threatMultiplier = SliderField("Threat mult", settings.threatMultiplier, 0.1f, 2f);
            settings.budgetCapMultiplier = SliderField("Budget cap mult", settings.budgetCapMultiplier, 0.5f, 8f);
            settings.maxActiveEnemies = Mathf.RoundToInt(SliderField("Max active", settings.maxActiveEnemies, 0f, 200f));
            settings.eliteChanceMultiplier = SliderField("Elite chance mult", settings.eliteChanceMultiplier, 0f, 2f);
            settings.globalSpeedMultiplier = SliderField("Global speed mult", settings.globalSpeedMultiplier, 0.1f, 3f);
            settings.creatureChanceAtStart = SliderField("Creature mix (start)", settings.creatureChanceAtStart, 0f, 1f);
            settings.creatureChanceAtLateGame = SliderField("Creature mix (late)", settings.creatureChanceAtLateGame, 0f, 1f);
            settings.creatureChanceRampMinutes = SliderField("Creature ramp (min)", settings.creatureChanceRampMinutes, 0.5f, 12f);
            settings.spawnDistanceMin = SliderField("Spawn dist min", settings.spawnDistanceMin, 4f, 30f);
            settings.spawnDistanceMax = SliderField("Spawn dist max", settings.spawnDistanceMax, 6f, 40f);
        }

        void DrawPerEnemySettings(SpawnDirectorSettings settings)
        {
            _showPerEnemy = GUILayout.Toggle(_showPerEnemy, "Per-enemy tuning");
            if (!_showPerEnemy || settings.enemies == null)
                return;

            for (var i = 0; i < settings.enemies.Length; i++)
            {
                var tuning = settings.enemies[i];
                if (tuning == null || string.IsNullOrEmpty(tuning.enemyId))
                    continue;

                GUILayout.BeginVertical(GUI.skin.box);
                tuning.spawnEnabled = GUILayout.Toggle(tuning.spawnEnabled, tuning.enemyId);
                tuning.weight = SliderField("Weight", tuning.weight, 0f, 3f);
                tuning.maxConcurrent = Mathf.RoundToInt(SliderField("Max concurrent", tuning.maxConcurrent, 0f, 40f));
                tuning.speedMultiplier = SliderField("Speed mult", tuning.speedMultiplier, 0.1f, 3f);
                tuning.availableAfterMinutes = SliderField("After min", tuning.availableAfterMinutes, 0f, 10f);
                GUILayout.EndVertical();
            }
        }

        static float SliderField(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, LabelWidth);
            value = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.Label($"{value:F2}", GUILayout.Width(40f));
            GUILayout.EndHorizontal();
            return value;
        }
    }
}
