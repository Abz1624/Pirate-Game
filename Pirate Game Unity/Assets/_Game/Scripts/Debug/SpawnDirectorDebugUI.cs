using HighSeas.Core;
using HighSeas.Enemies;
using HighSeas.V3;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HighSeas.Debugging
{
    /// <summary>
    /// In-run IMGUI panel to tweak <see cref="EncounterSpawner"/> at runtime (F9).
    /// </summary>
    public sealed class SpawnDirectorDebugUI : MonoBehaviour
    {
        const float PanelWidth = 380f;

        static readonly GUILayoutOption[] LabelWidth = { GUILayout.Width(168f) };

        EncounterSpawner _spawner;
        EnemyWorld _enemyWorld;
        bool _visible;
        bool _ambientEnabled = true;
        Vector2 _scroll;
        Rect _panelRect = new(12f, 72f, PanelWidth, 360f);

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
            _spawner = FindFirstObjectByType<EncounterSpawner>();
            _enemyWorld = FindFirstObjectByType<EnemyWorld>();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.f9Key.wasPressedThisFrame)
                return;

            _visible = !_visible;
            if (_spawner == null)
                _spawner = FindFirstObjectByType<EncounterSpawner>();

            var message = _visible ? "Encounter spawn debug ON (F9)" : "Encounter spawn debug OFF (F9)";
            V3ToastHud.Instance?.Show(message, 2f);
            Debug.Log(message);
        }

        void OnGUI()
        {
            if (!_visible || _spawner == null)
                return;

            _panelRect = GUI.Window(
                GetInstanceID(),
                _panelRect,
                DrawWindow,
                "Encounter Spawner (F9)");
        }

        void DrawWindow(int id)
        {
            GUI.DragWindow(new Rect(0f, 0f, PanelWidth, 22f));

            _scroll = GUILayout.BeginScrollView(_scroll);

            DrawLiveStats();
            GUILayout.Space(6f);
            DrawRuntimeMultipliers();
            GUILayout.Space(6f);
            DrawAmbientToggle();

            GUILayout.EndScrollView();
        }

        void DrawLiveStats()
        {
            GUILayout.Label("Live", GUI.skin.box);
            var run = RunManager.Instance;
            var elapsed = run != null ? run.ElapsedSeconds : 0f;
            var active = _enemyWorld != null ? _enemyWorld.ActiveCount : 0;
            GUILayout.Label($"Elapsed: {elapsed:F1}s ({elapsed / 60f:F1} min)");
            GUILayout.Label($"Active enemies: {active} / cap {_spawner.GetEnemyCapForDebug()}");
            GUILayout.Label($"Effective threat / sec: {_spawner.GetEffectiveThreatPerSecond():F2}");
            GUILayout.Label($"Ambient schedule: {_spawner.AmbientScheduleEnabled}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh refs"))
            {
                _spawner = FindFirstObjectByType<EncounterSpawner>();
                _enemyWorld = FindFirstObjectByType<EnemyWorld>();
            }
            GUILayout.EndHorizontal();
        }

        void DrawRuntimeMultipliers()
        {
            GUILayout.Label("Runtime multipliers", GUI.skin.box);

            var rate = _spawner.DebugSpawnRateMultiplier;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Spawn rate", LabelWidth);
            rate = GUILayout.HorizontalSlider(rate, 0.1f, 3f);
            GUILayout.Label($"{rate:F2}", GUILayout.Width(40f));
            GUILayout.EndHorizontal();
            _spawner.SetSpawnRateMultiplier(rate);
        }

        void DrawAmbientToggle()
        {
            GUILayout.Label("Schedule", GUI.skin.box);
            _ambientEnabled = GUILayout.Toggle(_ambientEnabled, "Ambient schedule entries");
            if (_ambientEnabled != _spawner.AmbientScheduleEnabled)
                _spawner.SetAmbientScheduleEnabled(_ambientEnabled);
        }
    }
}
