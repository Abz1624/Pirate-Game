using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Enemies;
using UnityEngine;

namespace HighSeas.Debugging
{
    /// <summary>World-space labels for active enemies (id, behavior, profile, combat stats).</summary>
    public sealed class EnemyIdentityDebugLabels : MonoBehaviour
    {
        static GUIStyle _labelStyle;

        public static void Ensure()
        {
            if (FindFirstObjectByType<EnemyIdentityDebugLabels>() != null)
                return;

            var systems = GameObject.Find("Systems");
            var host = systems != null ? systems : new GameObject("EnemyIdentityDebugLabels");
            if (host.GetComponent<EnemyIdentityDebugLabels>() == null)
                host.AddComponent<EnemyIdentityDebugLabels>();
        }

        void OnGUI()
        {
            if (!CombatDebugOverlays.Visible)
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
                var role = def.isBoss ? "Boss" : def.isMiniboss ? "Miniboss" : "Enemy";
                var tagLine = def.tags != null && def.tags.Length > 0
                    ? string.Join(", ", def.tags)
                    : "—";
                var text =
                    $"{def.displayName} · {role}\n" +
                    $"id: {def.id} · {def.category} · {def.behavior}\n" +
                    $"tags: {tagLine}\n" +
                    $"HP {agent.hp:F0}/{agent.maxHp:F0} · atk {agent.damage:F0} · spd {agent.moveSpeed:F1} · hit r {def.radius:F1}\n" +
                    $"profile: {profileName}";

                var rect = new Rect(screen.x - 120f, Screen.height - screen.y - 72f, 240f, 68f);
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
