#if UNITY_EDITOR
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Editor
{
    [CustomEditor(typeof(WeaponDefinition))]
    public sealed class WeaponDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "fanSpreadDegrees");

            var pattern = (WeaponPattern)serializedObject.FindProperty("pattern").enumValueIndex;
            if (pattern == WeaponPattern.FanVolley)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("Fan Volley", EditorStyles.boldLabel);
                var spread = serializedObject.FindProperty("fanSpreadDegrees");
                EditorGUILayout.PropertyField(spread);
                EditorGUILayout.HelpBox(
                    "Total yaw arc for the volley. Lower = tighter cluster. Broadside default is 24°.",
                    MessageType.None);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
