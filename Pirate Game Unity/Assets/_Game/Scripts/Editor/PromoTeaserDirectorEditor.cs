#if UNITY_EDITOR
using HighSeas.Promo;
using UnityEditor;
using UnityEngine;

namespace HighSeas.Editor
{
    [CustomEditor(typeof(PromoTeaserDirector))]
    public sealed class PromoTeaserDirectorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var director = (PromoTeaserDirector)target;
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Play mode", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Swarm −50", GUILayout.Height(26)))
                    director.AdjustEnemyCount(-50);
                if (GUILayout.Button("Swarm +50", GUILayout.Height(26)))
                    director.AdjustEnemyCount(50);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Safety −0.25", GUILayout.Height(26)))
                    director.AdjustPlayerSpawnClearance(-0.25f);
                if (GUILayout.Button("Safety +0.25", GUILayout.Height(26)))
                    director.AdjustPlayerSpawnClearance(0.25f);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Dmg size −", GUILayout.Height(26)))
                    director.AdjustDamageNumberSize(-1);
                if (GUILayout.Button("Dmg size +", GUILayout.Height(26)))
                    director.AdjustDamageNumberSize(1);
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button("Toggle flip damage numbers", GUILayout.Height(26)))
                    director.ToggleDamageNumberFlip();

                if (GUILayout.Button("Randomize islands", GUILayout.Height(28)))
                    director.RandomizeIslands();
                if (GUILayout.Button("Randomize player position", GUILayout.Height(28)))
                    director.RandomizePlayerPosition();
                if (GUILayout.Button("Randomize swarm", GUILayout.Height(28)))
                    director.RandomizeSwarm();
                if (GUILayout.Button("Start recording", GUILayout.Height(32)))
                    director.StartRecording();
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play mode. Use the buttons (or in-game overlay) to randomize islands, player, and swarm separately, then Start recording.",
                    MessageType.Info);
            }
        }
    }
}
#endif
