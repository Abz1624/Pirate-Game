using HighSeas.Core;
using HighSeas.Enemies;
using HighSeas.V3;
using UnityEngine;

namespace HighSeas.Debugging
{
    /// <summary>World-space labels for active enemies (id, behavior, profile, combat stats).</summary>
    public sealed class EnemyIdentityDebugLabels : MonoBehaviour
    {
        static GUIStyle _labelStyle;

        public static void Ensure()
        {
            if (!V3PlaytestFlags.EnableEnemyIdentityLabels)
                return;

            if (FindFirstObjectByType<EnemyIdentityDebugLabels>() != null)
                return;

            var systems = GameObject.Find("Systems");
            var host = systems != null ? systems : new GameObject("EnemyIdentityDebugLabels");
            if (host.GetComponent<EnemyIdentityDebugLabels>() == null)
                host.AddComponent<EnemyIdentityDebugLabels>();
        }

        void OnGUI()
        {
            if (!V3PlaytestFlags.EnableEnemyIdentityLabels)
                return;

            var world = FindFirstObjectByType<EnemyWorld>();
            var cam = Camera.main;
            if (world == null || cam == null || world.Definitions == null)
                return;

            _labelStyle ??= CreateLabelStyle();

            var agents = world.Agents;
            for (var i = 0; i < agents.Length; i++)
            {
                var agent = agents[i];
                if (!agent.active)
                    continue;

                if (agent.definitionIndex < 0 || agent.definitionIndex >= world.Definitions.Length)
                    continue;

                var def = world.Definitions[agent.definitionIndex];
                if (def == null)
                    continue;

                var worldPos = agent.position + Vector3.up * (def.radius + 2.1f);
                var screen = cam.WorldToScreenPoint(worldPos);
                if (screen.z <= 0f)
                    continue;

                var profileName = agent.spawnProfile != null
                    ? agent.spawnProfile.ResolvedId
                    : "(legacy spawn)";
                var text =
                    $"{def.displayName}\n" +
                    $"id: {def.id} · {def.behavior}\n" +
                    $"profile: {profileName}\n" +
                    $"HP {agent.hp:F0}/{agent.maxHp:F0} · dmg {agent.damage:F0} · spd {agent.moveSpeed:F1}";

                var rect = new Rect(screen.x - 110f, Screen.height - screen.y - 56f, 220f, 52f);
                DrawOutlinedLabel(rect, text);
            }
        }

        static GUIStyle CreateLabelStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.UpperCenter,
                wordWrap = true,
                richText = false
            };
            style.normal.textColor = new Color(1f, 0.98f, 0.85f);
            return style;
        }

        static void DrawOutlinedLabel(Rect rect, string text)
        {
            var shadow = new Color(0f, 0f, 0f, 0.85f);
            var shadowRect = rect;
            shadowRect.x += 1f;
            shadowRect.y += 1f;
            _labelStyle.normal.textColor = shadow;
            GUI.Label(shadowRect, text, _labelStyle);
            _labelStyle.normal.textColor = new Color(1f, 0.98f, 0.85f);
            GUI.Label(rect, text, _labelStyle);
        }
    }
}
