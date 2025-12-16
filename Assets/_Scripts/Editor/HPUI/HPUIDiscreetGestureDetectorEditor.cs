using UnityEditor;
using UnityEngine.Events;
using _Scripts;

[CustomEditor(typeof(HPUIDiscreetGestureDetector)), CanEditMultipleObjects]
public class HPUIDiscreetGestureDetectorEditor : Editor
{
    private bool showAdvanced = false;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.name == "m_Script")
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.PropertyField(iterator, true);
                EditorGUI.EndDisabledGroup();
                continue;
            }

            if (IsUnityEvent(iterator))
            {
                EditorGUILayout.PropertyField(iterator, true);
            }
        }

        EditorGUILayout.Space();
        showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced Tuning Properties", true);

        if (showAdvanced)
        {
            EditorGUI.indentLevel++;

            iterator = serializedObject.GetIterator();
            enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (iterator.name == "m_Script")
                    continue;

                if (!IsUnityEvent(iterator))
                {
                    EditorGUILayout.PropertyField(iterator, true);
                }
            }

            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private bool IsUnityEvent(SerializedProperty property)
    {
        return property.propertyType == SerializedPropertyType.Generic &&
               typeof(UnityEventBase).IsAssignableFrom(
                   fieldInfoFor(property)?.FieldType
               );
    }

    private System.Reflection.FieldInfo fieldInfoFor(SerializedProperty property)
    {
        var targetType = target.GetType();
        return targetType.GetField(
            property.name,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public
        );
    }
}
