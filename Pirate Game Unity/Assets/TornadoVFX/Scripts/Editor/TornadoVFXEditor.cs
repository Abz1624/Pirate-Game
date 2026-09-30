using UnityEditor;
using UnityEngine;
using TornadoFX;

[CustomEditor(typeof(TornadoVFX))]
public class TornadoVFXEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var t = (TornadoVFX)target;

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUILayout.LabelField("Preview (Play Mode)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("State", Application.isPlaying ? t.CurrentState.ToString() : "-");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Play")) t.PlayTornado();
                if (GUILayout.Button("Stop")) t.StopTornado();
                if (GUILayout.Button("Stop Immediate")) t.StopImmediate();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("0")) t.SetIntensity(0f);
                if (GUILayout.Button("0.5")) t.SetIntensity(0.5f);
                if (GUILayout.Button("1")) t.SetIntensity(1f);
                if (GUILayout.Button("1.5")) t.SetIntensity(1.5f);
            }
        }
        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Enter Play Mode to preview. Shape values rebuild the meshes at runtime.", MessageType.Info);
        else
            Repaint();
    }
}
